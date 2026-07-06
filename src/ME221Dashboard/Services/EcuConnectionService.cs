using ME221.Comms;
using ME221.Comms.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services;

public sealed class EcuConnectionService(
    IChannelFactory channelFactory,
    ILogger<EcuConnectionService>? logger = null,
    ILoggerFactory? loggerFactory = null)
    : IEcuConnectionService, IAsyncDisposable
{
    private readonly ILogger<EcuConnectionService> _logger = logger ?? NullLogger<EcuConnectionService>.Instance;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private ProtocolService? _protocolService;
    private ConnectionState _state = ConnectionState.Disconnected;
    private CancellationTokenSource? _monitorCts;

    public ConnectionState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            var old = _state;
            _state = value;
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(old, value, null));
        }
    }

    public ProtocolInfo? ProtocolInfo { get; private set; }
    public string? LastError { get; private set; }

    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    public async Task<bool> ConnectAsync(ConnectionTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_state is ConnectionState.Connected or ConnectionState.Connecting)
                return true;

            LastError = null;
            State = ConnectionState.Connecting;

            var maxAttempts = target is ConnectionTarget.Serial ? 3 : 1;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var channel = channelFactory.Create(target);
                    var protocolLogger = loggerFactory?.CreateLogger<ProtocolService>();
                    var service = new ProtocolService(channel, protocolLogger, loggerFactory);

                    try
                    {
                        await service.OpenAsync(cancellationToken).ConfigureAwait(false);

#if ANDROID
                        if (target is ConnectionTarget.Serial)
                        {
                            // After USB re-enumeration the ECU's serial bridge needs time
                            // to settle before it can respond to the probe.
                            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                        }
#endif

                        await ProbeEcuAsync(service, cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        await service.DisposeAsync().ConfigureAwait(false);
                        throw;
                    }

                    _protocolService = service;
                    service.HeartbeatFailed += OnHeartbeatFailed;
                    StartMonitor();
                    State = ConnectionState.Connected;
                    return true;
                }
                catch (Exception ex) when (attempt < maxAttempts && ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Connection attempt {Attempt}/{Max} failed, retrying", attempt, maxAttempts);
                    await CleanupAsync().ConfigureAwait(false);
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                }
            }

            // Last attempt's exception propagated to outer catch; this satisfies the compiler
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to connect to {Target}", target);
            LastError = ex.Message;
            TransitionToError(LastError);
            return false;
        }
        catch (OperationCanceledException)
        {
            LastError = "Operation was cancelled";
            TransitionToError(LastError);
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task ProbeEcuAsync(ProtocolService service, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            await service.SendAsync<GetEcuInfoResponse>(new GetEcuInfoRequest(), cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("ECU did not respond to connection probe within 5 seconds.");
        }
    }

    public async Task DisconnectAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_state is ConnectionState.Disconnected) return;
            State = ConnectionState.Disconnecting;
            // Skip DisableReporting — the ECU may be unresponsive (e.g. during cranking).
            // Just tear down the channel immediately.
            await CleanupAsync().ConfigureAwait(false);
            State = ConnectionState.Disconnected;
        }
        catch (OperationCanceledException) { State = ConnectionState.Disconnected; }
        finally { _lock.Release(); }
    }

    public ProtocolService GetProtocolService()
    {
        return _protocolService ?? throw new InvalidOperationException("Not connected to any ECU");
    }

    public async Task<ProtocolInfo> EnableReportingAsync(CancellationToken cancellationToken = default)
    {
        if (ProtocolInfo != null && _state == ConnectionState.Connected)
            return ProtocolInfo;

        var service = GetProtocolService();

        var response = await service.SendAsync<SetStateResponse>(
            new SetStateRequest(true), cancellationToken).ConfigureAwait(false);

        var ecuInfo = await GetEcuInfoAsync(service).ConfigureAwait(false);

        var entityMap = new List<EntityMapEntry>();
        if (response.ProtoVersion == ReportingVersion.V2 && response.Entities.Count > 0)
        {
            foreach (var entity in response.Entities)
                entityMap.Add(new EntityMapEntry(entity.Id, entity.Type));
        }

        ProtocolInfo = new ProtocolInfo(
            response.ProtoVersion,
            ecuInfo.Product,
            ecuInfo.Model,
            ecuInfo.Version,
            entityMap);

        return ProtocolInfo;
    }

    public async Task DisableReportingAsync()
    {
        if (_protocolService is null) return;
        // Clear immediately so a concurrent EnableReportingAsync always sends SetState(true)
        ProtocolInfo = null;
        try
        {
            await _protocolService.SendAsync<SetStateResponse>(
                new SetStateRequest(false)).ConfigureAwait(false);
        }
        catch (InvalidOperationException) { }
        catch (IOException) { }
        catch (TimeoutException) { }
        catch (OperationCanceledException) { }
    }

    public void PauseHeartbeat()
    {
        _protocolService?.PauseHeartbeat();
    }

    public void ResumeHeartbeat()
    {
        _protocolService?.ResumeHeartbeat();
    }

    public async ValueTask DisposeAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogCritical("SHUTDOWN: EcuConnectionService.DisposeAsync START");
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            _logger.LogCritical("SHUTDOWN: CleanupAsync START (state={State})", _state);
            await CleanupAsync().ConfigureAwait(false);
            _logger.LogCritical("SHUTDOWN: CleanupAsync DONE in {Elapsed}ms", sw.ElapsedMilliseconds);
        }
        finally { _lock.Release(); _lock.Dispose(); loggerFactory?.Dispose(); }
        _logger.LogCritical("SHUTDOWN: EcuConnectionService.DisposeAsync DONE in {Elapsed}ms", sw.ElapsedMilliseconds);
    }

    private static async Task<(string Product, string Model, string Version)> GetEcuInfoAsync(ProtocolService protocol)
    {
        try
        {
            var response = await protocol.SendAsync<GetEcuInfoResponse>(new GetEcuInfoRequest()).ConfigureAwait(false);
            return (response.ProductName, response.ModelName, response.Version);
        }
        catch (OperationCanceledException) { return ("Unknown", "Unknown", "Unknown"); }
    }

    private void TransitionToError(string error)
    {
        ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(_state, ConnectionState.Error, error));
        _state = ConnectionState.Disconnected;
        ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(ConnectionState.Error, ConnectionState.Disconnected, error));
    }

    private void StartMonitor()
    {
        StopMonitor();
        _monitorCts = new CancellationTokenSource();
        _ = MonitorConnectionAsync(_monitorCts.Token);
    }

    private void StopMonitor()
    {
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _monitorCts = null;
    }

    private void OnHeartbeatFailed(object? sender, EventArgs e) => _ = HandleConnectionLostAsync();

    private async Task HandleConnectionLostAsync()
    {
        if (_state != ConnectionState.Connected) return;
        _logger.LogWarning("Connection lost");
        await CleanupAsync().ConfigureAwait(false);
        TransitionToError("Connection lost");
    }

    private async Task MonitorConnectionAsync(CancellationToken ct)
    {
        try
        {
            int openCheckFailures = 0;
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(2000, ct).ConfigureAwait(false);
                if (ct.IsCancellationRequested) return;

                var svc = _protocolService;
                if (svc is not null && _state == ConnectionState.Connected)
                {
                    if (!svc.IsOpen)
                    {
                        openCheckFailures++;
                        if (openCheckFailures >= 3)
                        {
                            await HandleConnectionLostAsync().ConfigureAwait(false);
                            return;
                        }
                    }
                    else
                    {
                        openCheckFailures = 0;
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task CleanupAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogCritical("SHUTDOWN: CleanupAsync — StopMonitor");
        StopMonitor();
        if (_protocolService is not null)
        {
            _logger.LogCritical("SHUTDOWN: CleanupAsync — DisposeAsync ProtocolService");
            _protocolService.HeartbeatFailed -= OnHeartbeatFailed;
            await _protocolService.DisposeAsync().ConfigureAwait(false);
            _protocolService = null;
            _logger.LogCritical("SHUTDOWN: CleanupAsync — ProtocolService disposed in {Elapsed}ms", sw.ElapsedMilliseconds);
        }
        ProtocolInfo = null;
    }
}
