using ME221.Data.Models;

namespace ME221.Data.Infrastructure;

/// <summary>
/// Whole-list save merge for warning settings: incoming records replace existing by
/// DataId, unenumerated existing records are preserved (R7 — never prune), and
/// incoming-only dataIds are appended after the existing order.
/// </summary>
public static class WarningMerge
{
    public static List<DataLinkWarningSetting> MergeSave(
        List<DataLinkWarningSetting> existing,
        List<DataLinkWarningSetting> incoming)
    {
        var result = new List<DataLinkWarningSetting>(existing);
        foreach (var inc in incoming)
        {
            var idx = result.FindIndex(s => s.DataId == inc.DataId);
            if (idx >= 0)
                result[idx] = inc;
            else
                result.Add(inc);
        }
        return result;
    }
}
