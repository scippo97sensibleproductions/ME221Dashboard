using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using ME221.Comms.Internal;
using ME221.Comms.Messages;
using ME221.Emulator.Application;
using ME221.Emulator.Domain;
using ME221.Emulator.Messages;
using Microsoft.Extensions.Logging;

namespace ME221.Emulator.Presentation;

public sealed class TcpClientSession : IDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly CommandRouter _router;
    private readonly EntityStore _entityStore;
    private readonly EcuState _state;
    private readonly EmulatorConsole _console;
    private readonly SensorSimulator _sensorSimulator;
    private readonly ILogger<TcpClientSession> _logger;
    private readonly ReportingOrchestrator _orchestrator;
    private readonly string _sessionId;
    private readonly List<byte> _buffer = [];
    private int _totalBytesRead;
    private int _framesReceived;
    private int _framesSent;

    public TcpClientSession(
        TcpClient client,
        CommandRouter router,
        EntityStore entityStore,
        EcuState state,
        EmulatorConsole console,
        SensorSimulator sensorSimulator,
        ILogger<TcpClientSession> logger,
        ILogger<ReportingOrchestrator> orchestratorLogger)
    {
        _client = client;
        _stream = client.GetStream();
        _router = router;
        _entityStore = entityStore;
        _state = state;
        _console = console;
        _sensorSimulator = sensorSimulator;
        _logger = logger;
        _sessionId = ((IPEndPoint)client.Client.RemoteEndPoint!).ToString();

        _orchestrator = new ReportingOrchestrator(entityStore, state, sensorSimulator, orchestratorLogger, console, _sessionId, SendReportFrameAsync);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var rentBuffer = ArrayPool<byte>.Shared.Rent(4096);
        var endpoint = ((IPEndPoint)_client.Client.RemoteEndPoint!).ToString();
        _logger.LogInformation("EmulatorSession[{Endpoint}]: session started", endpoint);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var bytesRead = await _stream.ReadAsync(rentBuffer.AsMemory(0, 4096), ct);
                if (bytesRead == 0)
                {
                    _logger.LogInformation("EmulatorSession[{Endpoint}]: client disconnected gracefully (total RX={TotalBytes}, frames RX={FramesRX} TX={FramesTX})",
                        endpoint, _totalBytesRead, _framesReceived, _framesSent);
                    break;
                }

                _totalBytesRead += bytesRead;
                _buffer.AddRange(rentBuffer.AsSpan(0, bytesRead));

                await ProcessBufferAsync();
            }
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "EmulatorSession[{Endpoint}]: IO error", endpoint);
        }
        catch (SocketException ex)
        {
            _logger.LogWarning(ex, "EmulatorSession[{Endpoint}]: socket error", endpoint);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("EmulatorSession[{Endpoint}]: session cancelled", endpoint);
        }
        finally
        {
            _orchestrator.Stop();
            ArrayPool<byte>.Shared.Return(rentBuffer);
            _buffer.Clear();
            _logger.LogInformation("EmulatorSession[{Endpoint}]: session ended (total RX={TotalBytes}, frames RX={FramesRX} TX={FramesTX})",
                endpoint, _totalBytesRead, _framesReceived, _framesSent);
        }
    }

    private async Task ProcessBufferAsync()
    {
        while (_buffer.Count > 0)
        {
            // Peek into the list's backing array without allocating
            var data = CollectionsMarshal.AsSpan(_buffer);

            if (FrameParser.TryParse(data, out var frame, out var consumed))
            {
                _buffer.RemoveRange(0, consumed);
                _framesReceived++;
                _console.FrameReceived(_sessionId);

                if (frame is not null)
                {
                    var response = await DispatchFrameAsync(frame);
                    await SendResponseAsync(response);
                }
            }
            else
            {
                // Parse failure — slow path. Discard garbage and notify client.
                // Only log at Debug level to avoid spam on marginal links.
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    var hexDump = Convert.ToHexString(data[..Math.Min(data.Length, 32)]);
                    _logger.LogDebug("EmulatorSession: parse fail — {Bytes} bytes, hex: {Hex}", data.Length, hexDump);
                }

                if (data.Length >= WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength + WireFormat.HeaderLengthAfterSync)
                {
                    var type = data[WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength];
                    var classId = data[WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength + 1];
                    var command = data[WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength + 2];

                    if (type == WireFormat.RequestType)
                    {
                        var errorResponse = new StatusResponse(classId, command, MessageStatus.Failure);
                        await SendResponseAsync(errorResponse);
                        break;
                    }
                }

                // Buffer too large — invalid sync, clear to avoid infinite loop
                if (data.Length > WireFormat.FixedFrameOverhead + 4096)
                {
                    _buffer.Clear();
                }
                break;
            }
        }
    }

    private async ValueTask<MessageFrame> DispatchFrameAsync(MessageFrame request)
    {
        if (request.Type != WireFormat.RequestType)
        {
            return new StatusResponse(request.Class, request.Command, MessageStatus.Failure);
        }

        var handler = _router.TryRoute(request.Class, request.Command);

        if (handler is not null)
        {
            var response = await handler.HandleAsync(request);

            // Start/stop reporting orchestration based on state changes
            if (request.Class == WireFormat.ClassReporting && request.Command == WireFormat.ReportingSetState)
            {
                if (_state.ReportingEnabled)
                {
                    _logger.LogInformation("EmulatorSession: reporting ENABLED — starting orchestration at {Freq}Hz", _state.ReportingFrequency);
                    _orchestrator.Start(_state.ReportingFrequency);
                }
                else
                {
                    _logger.LogInformation("EmulatorSession: reporting DISABLED — stopping orchestration");
                    _orchestrator.Stop();
                }
            }

            return response;
        }

        _logger.LogWarning("EmulatorSession: no handler for Class={Class:X2} Cmd={Command:X2}", request.Class, request.Command);
        return new StatusResponse(request.Class, request.Command, MessageStatus.UnsupportedRequest);
    }

    private async ValueTask SendResponseAsync(MessageFrame response)
    {
        using var pooled = FrameBuilder.Build(response);
        await _stream.WriteAsync(pooled.Memory);
        _framesSent++;
        _console.FrameSent(_sessionId);
    }

    private async ValueTask SendReportFrameAsync(MessageFrame report)
    {
        using var pooled = FrameBuilder.Build(report);
        await _stream.WriteAsync(pooled.Memory);
        _framesSent++;
        _console.FrameSent(_sessionId);
    }

    public void Dispose()
    {
        _orchestrator.Dispose();
        _stream.Dispose();
        _client.Dispose();
    }
}
