namespace ME221.Data.Models;

public sealed class BatchLedgerEntry
{
    public int DataId { get; set; }
    public string Outcome { get; set; } = "";
    public long Timestamp { get; set; }
}

public sealed class QueuedBanner
{
    public List<int> DataIds { get; set; } = [];
    public string Kind { get; set; } = "";
    public string Message { get; set; } = "";
    public long Timestamp { get; set; }
}

public sealed class UndoExpiryNotice
{
    public int DataId { get; set; }
    public long Timestamp { get; set; }
}
