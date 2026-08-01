using FluentAssertions;
using ME221Dashboard.Comms;
using Xunit;

namespace ME221Dashboard.Comms.Tests;

public class PendingIdTrackerTests
{
    [Fact]
    public void Add_NewId_ReturnsTrueAndCountsIt()
    {
        var tracker = new PendingIdTracker();

        tracker.Add(42).Should().BeTrue();
        tracker.Count.Should().Be(1);
    }

    [Fact]
    public void Add_DuplicateId_ReturnsFalseAndDoesNotCount()
    {
        var tracker = new PendingIdTracker();

        tracker.Add(42).Should().BeTrue();
        tracker.Add(42).Should().BeFalse();
        tracker.Count.Should().Be(1);
    }

    [Fact]
    public void GetPendingMemory_ReturnsAddedIds()
    {
        var tracker = new PendingIdTracker();

        tracker.Add(10);
        tracker.Add(20);
        tracker.Add(30);

        tracker.GetPendingMemory().ToArray().Should().Equal([10, 20, 30]);
    }

    [Fact]
    public void Clear_ResetsEverything()
    {
        var tracker = new PendingIdTracker();

        tracker.Add(10);
        tracker.Add(20);
        tracker.Clear();

        tracker.Count.Should().Be(0);
        tracker.GetPendingMemory().ToArray().Should().BeEmpty();
        // IDs can be re-added after clear
        tracker.Add(10).Should().BeTrue();
    }

    [Fact]
    public void Add_ManyIds_ExpandsCapacityWithoutLosingData()
    {
        var tracker = new PendingIdTracker(initialCapacity: 4);

        for (var i = 0; i < 1000; i++)
            tracker.Add(i);

        tracker.Count.Should().Be(1000);
        tracker.GetPendingMemory().ToArray().Should().Equal(Enumerable.Range(0, 1000).ToArray());
    }

    [Fact]
    public void Add_ManyIds_AfterClearReusesArray()
    {
        var tracker = new PendingIdTracker(initialCapacity: 2);

        tracker.Add(1);
        tracker.Add(2);
        tracker.Clear();
        tracker.Add(3);

        tracker.GetPendingMemory().ToArray().Should().Equal([3]);
    }

    [Fact]
    public void GetPendingMemory_IsZeroCopySlice_ReflectsArrayUntilClear()
    {
        var tracker = new PendingIdTracker();

        tracker.Add(7);
        var memory = tracker.GetPendingMemory();

        // Adding more IDs appends into the same internal array — the slice
        // spans the tracked range, so it must not contain the new ID.
        tracker.Add(8);
        memory.ToArray().Should().Equal([7]);
    }
}
