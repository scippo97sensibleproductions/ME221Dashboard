using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using ME221.Emulator.Application;
using ME221.Emulator.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ME221.Emulator.Presentation;

public sealed class TcpServer(
    int port,
    IServiceScopeFactory scopeFactory,
    EmulatorConsole console,
    ILoggerFactory loggerFactory)
    : IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<string, (Task Task, IServiceScope Scope)> _sessions = new();

    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();

        while (!_cts.Token.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token);
                var endpoint = ((IPEndPoint)client.Client.RemoteEndPoint!).ToString();
                console.Connected(endpoint);

                var scope = scopeFactory.CreateScope();
                var router = scope.ServiceProvider.GetRequiredService<CommandRouter>();
                var entityStore = scope.ServiceProvider.GetRequiredService<EntityStore>();
                var state = scope.ServiceProvider.GetRequiredService<EcuState>();
                var sensorSimulator = scope.ServiceProvider.GetRequiredService<SensorSimulator>();

                var session = new TcpClientSession(client, router, entityStore, state, console, sensorSimulator,
                    loggerFactory.CreateLogger<TcpClientSession>(),
                    loggerFactory.CreateLogger<ReportingOrchestrator>());

                var sessionTask = RunSessionAsync(session, endpoint, scope, _cts.Token);
                _sessions.TryAdd(endpoint, (sessionTask, scope));

                _ = sessionTask.ContinueWith(t =>
                {
                    _sessions.TryRemove(endpoint, out _);
                    scope.Dispose();
                    session.Dispose();
                    client.Dispose();

                    if (t.IsFaulted)
                        console.Error($"Session {endpoint} ended with error: {t.Exception?.InnerException?.Message}");
                    else
                        console.Disconnected(endpoint, abnormal: false);
                });
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex)
            {
                console.Error($"Socket error: {ex.Message}");
            }
            catch (Exception ex)
            {
                console.Error($"Accept error: {ex.Message}");
                client?.Dispose();
            }
        }

        await Task.WhenAll(_sessions.Values.Select(s => s.Task));
    }

    private async Task RunSessionAsync(TcpClientSession session, string endpoint, IServiceScope scope, CancellationToken ct)
    {
        try
        {
            await session.RunAsync(ct);
        }
        catch (Exception ex)
        {
            console.Error($"Session {endpoint} error: {ex.Message}");
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
    }

    public void Dispose()
    {
        Stop();
        foreach (var (task, scope) in _sessions.Values)
        {
            scope.Dispose();
        }
        _sessions.Clear();
    }
}
