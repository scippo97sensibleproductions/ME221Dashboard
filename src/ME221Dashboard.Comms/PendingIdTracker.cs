namespace ME221Dashboard.Comms;

/// <summary>
/// Tracks unique entity IDs pending UI notification with O(1) deduplication.
/// Compacts to a <see cref="ReadOnlyMemory{T}"/> for zero-copy event dispatch.
///
/// Used by LiveDataService to batch entity IDs between 40ms throttle intervals
/// without O(n²) linear scans.
/// </summary>
public sealed class PendingIdTracker(int initialCapacity = 128)
{
    private readonly HashSet<int> _set = new(initialCapacity);
    private int[] _ids = new int[initialCapacity];
    private int _count = 0;

    /// <summary>
    /// Adds an entity ID if not already pending. O(1) via HashSet.
    /// </summary>
    /// <returns><c>true</c> if the ID was added; <c>false</c> if already pending.</returns>
    public bool Add(int entityId)
    {
        if (!_set.Add(entityId))
            return false;

        if (_count == _ids.Length)
            Array.Resize(ref _ids, _ids.Length * 2);

        _ids[_count++] = entityId;
        return true;
    }

    /// <summary>
    /// Returns current pending IDs as a memory slice over the internal array.
    /// The memory is valid until <see cref="Clear"/> is called.
    /// </summary>
    public ReadOnlyMemory<int> GetPendingMemory() => _ids.AsMemory(0, _count);

    /// <summary>Number of unique pending IDs.</summary>
    public int Count => _count;

    /// <summary>
    /// Resets the tracker. Clears both the HashSet and the count.
    /// The internal array is retained for reuse.
    /// </summary>
    public void Clear()
    {
        _set.Clear();
        _count = 0;
    }
}
