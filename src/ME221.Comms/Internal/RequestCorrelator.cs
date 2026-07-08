using System.Collections.Concurrent;
using ME221.Comms.Messages;
using Microsoft.Extensions.Logging;
using static ME221.Comms.Internal.RequestCorrelatorLog;

namespace ME221.Comms.Internal;

/// <summary>
/// Thread-safe correlation engine that matches incoming responses to pending requests.
///
/// Uses a concurrent dictionary keyed by (class, command) to map
/// pending requests to their TaskCompletionSource. When a response arrives,
/// the correlator looks up the matching TCS and completes it.
/// </summary>
internal sealed class RequestCorrelator : IAsyncDisposable
{
    private readonly ConcurrentDictionary<(byte Class, byte Command), TaskCompletionSource<Response>> _pendingRequests;
    private readonly ILogger<RequestCorrelator>? _logger;

    /// <summary>
    /// Creates a new RequestCorrelator with the given logger.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public RequestCorrelator(ILogger<RequestCorrelator>? logger = null)
    {
        _logger = logger;
        _pendingRequests = new ConcurrentDictionary<(byte Class, byte Command), TaskCompletionSource<Response>>();
    }

    /// <summary>
    /// Registers a pending request and returns a TaskCompletionSource for the response.
    /// </summary>
    /// <typeparam name="TResponse">The expected response type.</typeparam>
    /// <param name="request">The request to correlate.</param>
    /// <returns>A TaskCompletionSource that will be completed when the response arrives.</returns>
    public TaskCompletionSource<Response> Register<TResponse>(Request request)
        where TResponse : Response
    {
        var tcs = new TaskCompletionSource<Response>(TaskCreationOptions.RunContinuationsAsynchronously);

        var key = (request.Class, request.Command);

        // Guard: if a TCS is already pending for this key (e.g. heartbeat SendAck
        // fired while previous SendAck is still in-flight), cancel the old one and
        // replace rather than silently orphaning it. The old TCS would otherwise
        // block forever on await tcs.Task.WaitAsync() with the dispose CTS.
        _pendingRequests.AddOrUpdate(
            key,
            tcs,
            (_, existing) =>
            {
                if (!existing.Task.IsCompleted)
                    existing.TrySetCanceled();
                return tcs;
            });

        if (_logger is not null)
        {
            LogRegisteredRequest(_logger, request.Type, request.Class, request.Command);
        }
        return tcs;
    }

    /// <summary>
    /// Tries to correlate an incoming response with a pending request.
    /// </summary>
    /// <param name="response">The incoming response frame.</param>
    /// <returns><c>true</c> if the response was correlated and the TCS was completed; otherwise <c>false</c>.</returns>
    public bool TryCorrelate(Response response)
    {
        var key = (response.Class, response.Command);

        if (_pendingRequests.TryRemove(key, out var tcs))
        {
            var taskCompleted = tcs.Task.IsCompleted;
            if (_logger is not null)
            {
                LogTryCorrelate(_logger, response.Class, response.Command, true, taskCompleted);
            }

            if (!taskCompleted)
            {
                tcs.TrySetResult(response);
                if (_logger is not null)
                {
                    LogCorrelatedResponse(_logger, response.Type, response.Class, response.Command);
                }
                return true;
            }
        }
        else
        {
            if (_logger is not null)
            {
                LogTryCorrelate(_logger, response.Class, response.Command, false, false);
            }
        }

        if (_logger is not null)
        {
            LogNoPendingRequest(_logger, response.Type, response.Class, response.Command);
        }
        return false;
    }

    /// <summary>
    /// Cancels all pending requests.
    /// </summary>
    public void CancelAll()
    {
        foreach (var kvp in _pendingRequests)
        {
            if (!kvp.Value.Task.IsCompleted)
            {
                kvp.Value.TrySetCanceled();
            }
        }
        _pendingRequests.Clear();
    }

    /// <summary>
    /// Removes the pending TCS for the given request (e.g. on timeout or cancellation).
    /// Prevents stale TCS from being overwritten and poisoning subsequent requests.
    /// </summary>
    public void RemovePending(Request request)
    {
        var key = (request.Class, request.Command);
        if (_pendingRequests.TryRemove(key, out var tcs) && !tcs.Task.IsCompleted)
        {
            tcs.TrySetCanceled();
        }
    }

    public ValueTask DisposeAsync()
    {
        CancelAll();
        return default;
    }
}
