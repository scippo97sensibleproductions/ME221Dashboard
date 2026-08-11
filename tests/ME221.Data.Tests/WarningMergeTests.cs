using FluentAssertions;
using ME221.Data.Infrastructure;
using ME221.Data.Models;
using Xunit;

namespace ME221.Data.Tests;

public class WarningMergeTests
{
    private static DataLinkWarningSetting Setting(int dataId, string? markerLevelId = null, bool markerSet = false)
        => new() { DataId = dataId, Enabled = true, MigratedBoundsMarkerLevelId = markerLevelId, MigratedBoundsMarkerSet = markerSet };

    [Fact]
    public void MergeSave_IncomingReplacesExistingByDataId()
    {
        var existing = new List<DataLinkWarningSetting> { Setting(1), Setting(2) };
        var incoming = new List<DataLinkWarningSetting> { Setting(2) };

        var result = WarningMerge.MergeSave(existing, incoming);

        result.Should().HaveCount(2);
        result[0].DataId.Should().Be(1);
        result[1].DataId.Should().Be(2);
        result[1].Should().BeSameAs(incoming[0]);
    }

    [Fact]
    public void MergeSave_UnenumeratedExistingPreservedWithMarkers()
    {
        var existing = new List<DataLinkWarningSetting>
        {
            Setting(1, markerLevelId: "lvl-1", markerSet: true),
            Setting(2, markerLevelId: null, markerSet: true),
        };
        var incoming = new List<DataLinkWarningSetting> { Setting(3) };

        var result = WarningMerge.MergeSave(existing, incoming);

        result.Should().HaveCount(3);
        result[0].DataId.Should().Be(1);
        result[0].MigratedBoundsMarkerSet.Should().BeTrue();
        result[0].MigratedBoundsMarkerLevelId.Should().Be("lvl-1");
        result[1].DataId.Should().Be(2);
        result[1].MigratedBoundsMarkerSet.Should().BeTrue();
        result[2].DataId.Should().Be(3);
    }

    [Fact]
    public void MergeSave_NewDataIdsAppendedAfterExistingOrder()
    {
        var existing = new List<DataLinkWarningSetting> { Setting(1) };
        var incoming = new List<DataLinkWarningSetting> { Setting(2), Setting(3), Setting(1) };

        var result = WarningMerge.MergeSave(existing, incoming);

        result.Select(s => s.DataId).Should().Equal([1, 2, 3]);
        result[0].Should().BeSameAs(incoming[2]);
    }

    [Fact]
    public void MergeSave_EmptyIncomingKeepsEverything()
    {
        var existing = new List<DataLinkWarningSetting> { Setting(1), Setting(2) };

        var result = WarningMerge.MergeSave(existing, []);

        result.Select(s => s.DataId).Should().Equal([1, 2]);
    }
}
