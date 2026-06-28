using BenchmarkDotNet.Attributes;
using ME221Dashboard.Comms;

namespace ME221.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for PendingIdTracker — the O(1) dedup utility used in LiveDataService.
/// Compares the new HashSet-based approach against the legacy O(n²) linear scan.
/// </summary>
[MemoryDiagnoser]
public class PendingIdTrackerBenchmarks
{
    private int[] _entityIds = null!;
    private int[] _allDuplicateIds = null!;
    private int[] _halfDuplicateIds = null!;

    [Params(10, 50)]
    public int EntityCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Fresh IDs (1..N) — first frame, all new
        _entityIds = new int[EntityCount];
        for (var i = 0; i < EntityCount; i++)
            _entityIds[i] = i + 1;

        // All duplicates — same IDs as above
        _allDuplicateIds = new int[EntityCount];
        Array.Copy(_entityIds, _allDuplicateIds, EntityCount);

        // Half duplicates — first half new, second half duplicates
        _halfDuplicateIds = new int[EntityCount];
        for (var i = 0; i < EntityCount; i++)
            _halfDuplicateIds[i] = i < EntityCount / 2 ? EntityCount + i + 1 : i - EntityCount / 2 + 1;
    }

    // ── PendingIdTracker (new: O(1) dedup) ──────────────────────────────

    /// <summary>
    /// New approach: all entities are fresh (worst case for Add — all succeed).
    /// </summary>
    [Benchmark(Description = "Tracker_Add_AllNew")]
    public int Tracker_Add_AllNew()
    {
        var tracker = new PendingIdTracker(EntityCount);
        for (var i = 0; i < _entityIds.Length; i++)
            tracker.Add(_entityIds[i]);
        return tracker.Count;
    }

    /// <summary>
    /// New approach: all entities are duplicates (best case — HashSet rejects all).
    /// </summary>
    [Benchmark(Description = "Tracker_Add_AllDuplicates")]
    public int Tracker_Add_AllDuplicates()
    {
        var tracker = new PendingIdTracker(EntityCount);
        // Pre-fill with the IDs
        for (var i = 0; i < _entityIds.Length; i++)
            tracker.Add(_entityIds[i]);
        // Now add duplicates
        for (var i = 0; i < _allDuplicateIds.Length; i++)
            tracker.Add(_allDuplicateIds[i]);
        return tracker.Count;
    }

    /// <summary>
    /// New approach: half new, half duplicates.
    /// </summary>
    [Benchmark(Description = "Tracker_Add_HalfDuplicates")]
    public int Tracker_Add_HalfDuplicates()
    {
        var tracker = new PendingIdTracker(EntityCount);
        // Pre-fill first half
        for (var i = 0; i < EntityCount / 2; i++)
            tracker.Add(_entityIds[i]);
        // Add mixed
        for (var i = 0; i < _halfDuplicateIds.Length; i++)
            tracker.Add(_halfDuplicateIds[i]);
        return tracker.Count;
    }

    // ── Legacy linear scan (old: O(n²) dedup) ───────────────────────────

    /// <summary>
    /// Legacy approach: linear scan dedup — all entities fresh.
    /// </summary>
    [Benchmark(Description = "Legacy_Add_AllNew")]
    public int Legacy_Add_AllNew()
    {
        var pendingIds = new int[EntityCount];
        var pendingCount = 0;
        for (var i = 0; i < _entityIds.Length; i++)
        {
            var id = _entityIds[i];
            var isDupe = false;
            for (var j = 0; j < pendingCount; j++)
            {
                if (pendingIds[j] == id) { isDupe = true; break; }
            }
            if (!isDupe)
                pendingIds[pendingCount++] = id;
        }
        return pendingCount;
    }

    /// <summary>
    /// Legacy approach: linear scan dedup — all duplicates (worst case for O(n²)).
    /// </summary>
    [Benchmark(Description = "Legacy_Add_AllDuplicates")]
    public int Legacy_Add_AllDuplicates()
    {
        var pendingIds = new int[EntityCount];
        var pendingCount = 0;
        // Pre-fill
        for (var i = 0; i < _entityIds.Length; i++)
            pendingIds[pendingCount++] = _entityIds[i];
        // Now scan for dupes — every entity matches, scanning full array each time
        for (var i = 0; i < _allDuplicateIds.Length; i++)
        {
            var id = _allDuplicateIds[i];
            var isDupe = false;
            for (var j = 0; j < pendingCount; j++)
            {
                if (pendingIds[j] == id) { isDupe = true; break; }
            }
            if (!isDupe)
                pendingIds[pendingCount++] = id;
        }
        return pendingCount;
    }

    /// <summary>
    /// Legacy approach: linear scan dedup — half new, half duplicates.
    /// </summary>
    [Benchmark(Description = "Legacy_Add_HalfDuplicates")]
    public int Legacy_Add_HalfDuplicates()
    {
        var pendingIds = new int[EntityCount];
        var pendingCount = 0;
        // Pre-fill first half
        for (var i = 0; i < EntityCount / 2; i++)
            pendingIds[pendingCount++] = _entityIds[i];
        // Add mixed
        for (var i = 0; i < _halfDuplicateIds.Length; i++)
        {
            var id = _halfDuplicateIds[i];
            var isDupe = false;
            for (var j = 0; j < pendingCount; j++)
            {
                if (pendingIds[j] == id) { isDupe = true; break; }
            }
            if (!isDupe)
                pendingIds[pendingCount++] = id;
        }
        return pendingCount;
    }
}
