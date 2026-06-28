using ME221.Comms;
using ME221.Comms.Internal;
using ME221.Comms.Messages;
using ME221Dashboard.Comms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services;

public class EntitiesUpdatedEventArgs(ReadOnlyMemory<int> entityIds, int count) : EventArgs
{
    public ReadOnlyMemory<int> EntityIds { get; } = entityIds;
    public int Count { get; } = count;
}

public interface ILiveDataService
{
    bool IsRunning { get; }
    float? this[int entityId] { get; }
    IReadOnlySet<int> EntityIds { get; }
    event EventHandler<EntitiesUpdatedEventArgs>? EntitiesUpdated;
    Task StartAsync(ProtocolService protocolService, int reportingVersion, Dictionary<ushort, (ReportingType Type, int Size)> entityMap);
    Task StopAsync();
    void UpdateExternalValue(int entityId, float value);
}

public sealed class LiveDataService(ILogger<LiveDataService>? logger = null) : ILiveDataService, IDisposable
{
    private readonly ILogger<LiveDataService> _logger = logger ?? NullLogger<LiveDataService>.Instance;
    private readonly Dictionary<int, float> _values = new();
    private readonly Lock _lock = new();
    private readonly PendingIdTracker _pendingTracker = new();
    private ProtocolService? _protocolService;
    private Task? _receiveTask;
    private CancellationTokenSource? _cts;
    private int _reportingVersion;

    private (ushort Id, ReportingType Type, int Size)[] _orderedMap = [];
    private ReportEntity[] _entityBuffer = [];
    private int _frameCount;
    private long _lastNotifyTime;

    public bool IsRunning => _receiveTask is not null && !_receiveTask.IsCompleted;

    public float? this[int entityId]
    {
        get { lock (_lock) { return _values.TryGetValue(entityId, out var value) ? value : null; } }
    }

    public IReadOnlySet<int> EntityIds
    {
        get { lock (_lock) { return new HashSet<int>(_values.Keys); } }
    }

    public event EventHandler<EntitiesUpdatedEventArgs>? EntitiesUpdated;

    public Task StartAsync(ProtocolService protocolService, int reportingVersion, Dictionary<ushort, (ReportingType Type, int Size)> entityMap)
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _values.Clear();
            _pendingTracker.Clear();
            _frameCount = 0;

            _protocolService = protocolService;
            _reportingVersion = reportingVersion;

            var mapCount = entityMap?.Count ?? 0;
            _orderedMap = new (ushort, ReportingType, int)[mapCount];
            _entityBuffer = new ReportEntity[mapCount];
            if (entityMap is { Count: > 0 })
            {
                var i = 0;
                foreach (var kvp in entityMap)
                    _orderedMap[i++] = (kvp.Key, kvp.Value.Type, kvp.Value.Size);
            }

            _logger.LogInformation("Live data service starting: version={ReportingVersion}, entities={MapCount}", reportingVersion, mapCount);

            _cts = new CancellationTokenSource();
            _receiveTask = Task.Run(ReceiveLoop, _cts.Token);
        }
        return Task.CompletedTask;
    }

    private async Task ReceiveLoop()
    {
        try
        {
            var protocolService = _protocolService;
            var cts = _cts;
            if (protocolService is null || cts is null) return;

            var orderedMap = _orderedMap;
            var entityBuffer = _entityBuffer;

            lock (_lock)
            {
                _pendingTracker.Clear();
                _lastNotifyTime = Environment.TickCount64;
            }

            await foreach (var frame in protocolService.UncorrelatedFrames(cts.Token).ConfigureAwait(false))
            {
                _frameCount++;

                if (frame.Type != WireFormat.ResponseType ||
                    frame.Class != WireFormat.ClassReporting ||
                    frame.Command != WireFormat.ReportingSendReport)
                    continue;

                var entityCount = 0;

                if (_reportingVersion == 2 && orderedMap.Length > 0)
                {
                    entityCount = ReportParser.ParseV2Report(frame.Payload.Span, entityBuffer.AsSpan(), orderedMap.AsSpan());
                }
                else
                {
                    var v1Report = new SendReportResponse(frame.Payload.Span);
                    entityCount = Math.Min(v1Report.Entities.Length, entityBuffer.Length);
                    for (var i = 0; i < entityCount; i++)
                        entityBuffer[i] = v1Report.Entities.Span[i];
                }

                if (entityCount == 0) continue;

                ReadOnlyMemory<int> idsToNotify = default;
                int notifyCount = 0;
                lock (_lock)
                {
                    for (var i = 0; i < entityCount; i++)
                    {
                        var entity = entityBuffer[i];
                        _values[entity.Id] = entity.Value;
                        _pendingTracker.Add((int)entity.Id);
                    }

                    var now = Environment.TickCount64;
                    if (now - _lastNotifyTime >= 40 && _pendingTracker.Count > 0)
                    {
                        idsToNotify = _pendingTracker.GetPendingMemory();
                        notifyCount = _pendingTracker.Count;
                        _pendingTracker.Clear();
                    }
                    _lastNotifyTime = now;
                }

                if (notifyCount > 0)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                        EntitiesUpdated?.Invoke(this, new EntitiesUpdatedEventArgs(idsToNotify, notifyCount)));
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException ex) { _logger.LogError(ex, "Live data receive loop error (frame {FrameCount})", _frameCount); }
        catch (InvalidOperationException ex) { _logger.LogError(ex, "Live data receive loop error (frame {FrameCount})", _frameCount); }
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? ctsToCancel;
        lock (_lock)
        {
            _protocolService = null;
            _receiveTask = null;
            _values.Clear();
            _pendingTracker.Clear();
            _orderedMap = [];
            _entityBuffer = [];
            ctsToCancel = _cts;
            _cts = null;
        }
        if (ctsToCancel is not null)
        {
            await ctsToCancel.CancelAsync().ConfigureAwait(false);
            ctsToCancel.Dispose();
        }
    }

    public void UpdateExternalValue(int entityId, float value)
    {
        var singleId = new[] { entityId };
        lock (_lock) { _values[entityId] = value; }
        MainThread.BeginInvokeOnMainThread(() =>
            EntitiesUpdated?.Invoke(this, new EntitiesUpdatedEventArgs(singleId, 1)));
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _cts = null;
            _protocolService = null;
            _receiveTask = null;
            _pendingTracker.Clear();
        }
        GC.SuppressFinalize(this);
    }
}
