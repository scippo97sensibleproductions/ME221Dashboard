using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

var config = DefaultConfig.Instance
    .AddJob(Job.ShortRun.WithWarmupCount(3).WithIterationCount(5))
    .AddDiagnoser(MemoryDiagnoser.Default);

BenchmarkRunner.Run(typeof(Program).Assembly, config);
