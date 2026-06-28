using BenchmarkDotNet.Attributes;
using ME221.Data.Infrastructure;
using ME221.Data.Models;

namespace ME221.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for TableSerializer — table data serialization/deserialization.
/// Runs when user opens a table in the editor (cold path, but on ARM7).
/// </summary>
[MemoryDiagnoser]
public class TableSerializerBenchmarks
{
    private TableDefinition _def1x16 = null!;
    private TableDefinition _def16x16 = null!;
    private TableDefinition _def32x32 = null!;

    private float[] _input0_16 = null!;
    private float[] _input1_1 = null!;
    private float[] _output_16 = null!;

    private float[] _input0_16x16 = null!;
    private float[] _input1_16x16 = null!;
    private float[] _output_16x16 = null!;

    private float[] _input0_32x32 = null!;
    private float[] _input1_32x32 = null!;
    private float[] _output_32x32 = null!;

    private byte[] _serialized1x16 = null!;
    private byte[] _serialized16x16 = null!;
    private byte[] _serialized32x32 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _def1x16 = new TableDefinition { Id = 1, TableType = "T1x16", Rows = 1, Cols = 16 };
        _def16x16 = new TableDefinition { Id = 2, TableType = "T16x16", Rows = 16, Cols = 16 };
        _def32x32 = new TableDefinition { Id = 3, TableType = "T32x32", Rows = 32, Cols = 32 };

        _input0_16 = Enumerable.Range(0, 16).Select(i => (float)i * 100).ToArray();
        _input1_1 = [0f];
        _output_16 = Enumerable.Range(0, 16).Select(i => (float)i * 1.5f).ToArray();

        _input0_16x16 = Enumerable.Range(0, 16).Select(i => (float)i * 100).ToArray();
        _input1_16x16 = Enumerable.Range(0, 16).Select(i => (float)i * 100).ToArray();
        _output_16x16 = Enumerable.Range(0, 256).Select(i => (float)i * 0.5f).ToArray();

        _input0_32x32 = Enumerable.Range(0, 32).Select(i => (float)i * 50).ToArray();
        _input1_32x32 = Enumerable.Range(0, 32).Select(i => (float)i * 50).ToArray();
        _output_32x32 = Enumerable.Range(0, 1024).Select(i => (float)i * 0.25f).ToArray();

        _serialized1x16 = TableSerializer.Serialize(_def1x16, true, _input0_16, _input1_1, _output_16);
        _serialized16x16 = TableSerializer.Serialize(_def16x16, true, _input0_16x16, _input1_16x16, _output_16x16);
        _serialized32x32 = TableSerializer.Serialize(_def32x32, true, _input0_32x32, _input1_32x32, _output_32x32);
    }

    // ─── Serialize ──────────────────────────────────────────────────────────

    [Benchmark(Baseline = true)]
    public byte[] Serialize_1x16() => TableSerializer.Serialize(_def1x16, true, _input0_16, _input1_1, _output_16);

    [Benchmark]
    public byte[] Serialize_16x16() => TableSerializer.Serialize(_def16x16, true, _input0_16x16, _input1_16x16, _output_16x16);

    [Benchmark]
    public byte[] Serialize_32x32() => TableSerializer.Serialize(_def32x32, true, _input0_32x32, _input1_32x32, _output_32x32);

    // ─── Deserialize (zero-copy span path) ──────────────────────────────────

    [Benchmark]
    public TableWireData Deserialize_1x16() => TableSerializer.Deserialize(_def1x16, _serialized1x16);

    [Benchmark]
    public TableWireData Deserialize_16x16() => TableSerializer.Deserialize(_def16x16, _serialized16x16);

    [Benchmark]
    public TableWireData Deserialize_32x32() => TableSerializer.Deserialize(_def32x32, _serialized32x32);
}
