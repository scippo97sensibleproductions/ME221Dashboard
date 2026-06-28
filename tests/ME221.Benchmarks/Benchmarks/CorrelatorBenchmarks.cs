using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using ME221.Comms.Internal;
using ME221.Comms.Messages;

namespace ME221.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for RequestCorrelator — Register + TryCorrelate round-trip.
/// This runs on every ECU command. Measures allocation and throughput.
/// </summary>
[MemoryDiagnoser]
public class CorrelatorBenchmarks
{
    private RequestCorrelator _correlator = null!;
    private GetEcuInfoRequest _request = null!;

    [GlobalSetup]
    public void Setup()
    {
        _correlator = new RequestCorrelator();
        _request = new GetEcuInfoRequest();
    }

    [Benchmark]
    public (bool correlated, bool completed) RegisterAndCorrelate()
    {
        var tcs = _correlator.Register<GetEcuInfoResponse>(_request);

        // Simulate the response arriving with matching (Class, Command)
        var response = new GetEcuInfoResponse(stackalloc byte[]
        {
            (byte)MessageStatus.Success, // status
            0x01, 0x02, 0x03,            // product
            0x04, 0x05,                  // model
            0x06, 0x07, 0x08, 0x09      // version
        });

        var correlated = _correlator.TryCorrelate(response);
        var completed = tcs.Task.IsCompleted;
        return (correlated, completed);
    }

    [Benchmark]
    public TaskCompletionSource<Response> Register_Only()
    {
        return _correlator.Register<GetEcuInfoResponse>(_request);
    }

    [Benchmark]
    public bool TryCorrelate_Only()
    {
        var tcs = _correlator.Register<GetEcuInfoResponse>(_request);
        var response = new GetEcuInfoResponse(stackalloc byte[]
        {
            (byte)MessageStatus.Success, 0x01, 0x02, 0x03,
            0x04, 0x05, 0x06, 0x07, 0x08, 0x09
        });
        return _correlator.TryCorrelate(response);
    }
}
