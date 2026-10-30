using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using ME221Dashboard.Services.Mirror;
using Xunit.Sdk;

namespace ME221Dashboard.Tests.Mirror;

/// <summary>
/// Test-side viewer socket that pumps incoming frames in the background.
///
/// The pump matters for every "server pushes a final frame, then closes" test:
/// the server writes and closes without waiting, so a client that only starts
/// reading after triggering the action races the close and loses the frame —
/// the buffered bytes die with the socket and the test sees an abort instead of
/// the message. Reading continuously, the way a real viewer does, removes that
/// race; request/response tests still read one frame at a time from the queue.
/// </summary>
public sealed class MirrorSocketClient : IAsyncDisposable
{
    private readonly ClientWebSocket _socket;
    private readonly Channel<string> _frames = Channel.CreateUnbounded<string>();
    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource<bool> _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _pump;
    private bool _disposed;

    private MirrorSocketClient(ClientWebSocket socket)
    {
        _socket = socket;
        _pump = Task.Run(PumpAsync);
    }

    public static async Task<MirrorSocketClient> ConnectAsync(int port)
    {
        var socket = new ClientWebSocket();
        socket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        await socket.ConnectAsync(new Uri($"wss://127.0.0.1:{port}/"), CancellationToken.None);
        return new MirrorSocketClient(socket);
    }

    public async Task SendAsync(object message)
    {
        var bytes = Encoding.UTF8.GetBytes(MirrorProtocol.Serialize(message));
        await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
    }

    /// <summary>Next raw JSON frame; throws when none arrives within the timeout.</summary>
    public async Task<string> NextRawAsync(int timeoutMs = 5000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            return await _frames.Reader.ReadAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new XunitException($"No frame arrived within {timeoutMs} ms");
        }
        catch (ChannelClosedException)
        {
            throw new XunitException("The session closed before the expected frame arrived");
        }
    }

    /// <summary>Next frame parsed as <typeparamref name="T"/>; fails when it is a different type.</summary>
    public async Task<T> NextAsync<T>(int timeoutMs = 5000) where T : class
    {
        var json = await NextRawAsync(timeoutMs);
        return MirrorProtocol.Parse(json) as T
            ?? throw new XunitException($"Expected a {typeof(T).Name} frame, received: {json}");
    }

    /// <summary>True when the server closed the socket before the timeout elapsed.</summary>
    public async Task<bool> WaitForCloseAsync(int timeoutMs = 5000)
    {
        return await Task.WhenAny(_closed.Task, Task.Delay(timeoutMs)) == _closed.Task;
    }

    private async Task PumpAsync()
    {
        var buffer = new byte[16 * 1024];
        var frame = new MemoryStream();
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                frame.Write(buffer, 0, result.Count);
                if (!result.EndOfMessage)
                    continue;

                _frames.Writer.TryWrite(Encoding.UTF8.GetString(frame.GetBuffer(), 0, (int)frame.Length));
                frame.SetLength(0);
            }
        }
        catch
        {
            // Any transport fault means the session is gone; the close signal below
            // is what tests assert on, so the specific exception is not interesting.
        }
        finally
        {
            _frames.Writer.TryComplete();
            _closed.TrySetResult(true);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        try { _cts.Cancel(); } catch { /* already disposed */ }
        try
        {
            await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        }
        catch { /* socket already gone */ }

        try { await _pump; } catch { /* pump swallows its own faults */ }
        _socket.Dispose();
        _cts.Dispose();
    }
}
