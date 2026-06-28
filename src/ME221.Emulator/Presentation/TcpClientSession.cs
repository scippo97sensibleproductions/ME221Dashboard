using System.Buffers;
using System.Net;
using System.Net.Sockets;
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
                _logger.LogTrace("EmulatorSession[{Endpoint}]: read {BytesRead} bytes, buffer now {BufferSize} bytes (total RX={TotalBytes})",
                    endpoint, bytesRead, _buffer.Count, _totalBytesRead);

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
            var data = _buffer.ToArray().AsSpan();

            _logger.LogTrace("EmulatorSession: ProcessBuffer — {BufferSize} bytes in buffer, trying TryParse...", _buffer.Count);

            if (FrameParser.TryParse(data, out var frame, out var consumed))
            {
                _buffer.RemoveRange(0, consumed);
                _framesReceived++;
                _console.FrameReceived(_sessionId);

                if (frame is not null)
                {
                    _logger.LogInformation("EmulatorSession: parsed frame #{FramesRX} — {Type} Class={Class:X2} Cmd={Command:X2} PayloadLen={PayloadLen} (consumed={Consumed})",
                        _framesReceived,
                        frame.Type == WireFormat.RequestType ? "REQ" : frame.Type == WireFormat.ResponseType ? "RSP" : $"{frame.Type:X2}",
                        frame.Class, frame.Command, frame.Payload.Length, consumed);

                    var response = await DispatchFrameAsync(frame);
                    await SendResponseAsync(response);
                }
                else
                {
                    _logger.LogWarning("EmulatorSession: TryParse succeeded but frame is null (consumed={Consumed})", consumed);
                }
            }
            else
            {
                _logger.LogTrace("EmulatorSession: TryParse FAILED on {BufferSize} bytes", _buffer.Count);

                // Try to extract class/command from partial data to send a failure response
                if (data.Length >= WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength + WireFormat.HeaderLengthAfterSync)
                {
                    var type = data[WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength];
                    var classId = data[WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength + 1];
                    var command = data[WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength + 2];

                    // Only send a failure response if this looks like a valid request type
                    if (type == WireFormat.RequestType)
                    {
                        _logger.LogWarning("EmulatorSession: parse failure — sending error response for class={Class:X2} cmd={Command:X2}", classId, command);
                        _console.Error($"Frame parse failure -- sending error response for class {classId:X2} cmd {command:X2}");
                        var errorResponse = new StatusResponse(classId, command, MessageStatus.Failure);
                        await SendResponseAsync(errorResponse);
                        // Don't clear buffer -- try again with remaining data
                        break;
                    }
                }

                // Buffer too large or invalid sync -- clear to avoid infinite loop
                if (data.Length > WireFormat.FixedFrameOverhead + 4096)
                {
                    _logger.LogError("EmulatorSession: buffer too large ({BufferSize}), clearing", _buffer.Count);
                    _console.Error("Frame parse failure -- buffer too large, clearing");
                    _buffer.Clear();
                }
                else
                {
                    _logger.LogTrace("EmulatorSession: waiting for more data — incomplete frame ({BufferSize} bytes, need >= {Overhead})",
                        _buffer.Count, WireFormat.FixedFrameOverhead);
                    _console.Error($"Frame parse failure -- incomplete frame ({data.Length} bytes, need >= {WireFormat.FixedFrameOverhead})");
                }
                break;
            }
        }
    }

    private async ValueTask<MessageFrame> DispatchFrameAsync(MessageFrame request)
    {
        if (request.Type != WireFormat.RequestType)
        {
            _logger.LogWarning("EmulatorSession: received non-request frame type={Type}, returning failure", request.Type);
            return new StatusResponse(request.Class, request.Command, MessageStatus.Failure);
        }

        var handler = _router.TryRoute(request.Class, request.Command);

        if (handler is not null)
        {
            _logger.LogDebug("EmulatorSession: dispatching Class={Class:X2} Cmd={Command:X2} to handler={Handler}",
                request.Class, request.Command, handler.GetType().Name);

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
        var bytes = pooled.Memory;
        await _stream.WriteAsync(bytes);
        _framesSent++;
        _console.FrameSent(_sessionId);
        _logger.LogDebug("EmulatorSession: sent response #{FramesTX} — {Type} Class={Class:X2} Cmd={Command:X2} ({Bytes} bytes)",
            _framesSent,
            response.Type == WireFormat.ResponseType ? "RSP" : $"{response.Type:X2}",
            response.Class, response.Command, bytes.Length);
    }

    private async ValueTask SendReportFrameAsync(MessageFrame report)
    {
        using var pooled = FrameBuilder.Build(report);
        var bytes = pooled.Memory;
        await _stream.WriteAsync(bytes);
        _framesSent++;
        _console.FrameSent(_sessionId);
        _logger.LogDebug("EmulatorSession: sent report frame #{FramesTX} — {Type} Class={Class:X2} Cmd={Command:X2} ({Bytes} bytes)",
            _framesSent,
            report.Type == WireFormat.ResponseType ? "RSP" : $"{report.Type:X2}",
            report.Class, report.Command, bytes.Length);
    }

    public void Dispose()
    {
        _orchestrator.Dispose();
        _stream.Dispose();
        _client.Dispose();
    }
}
