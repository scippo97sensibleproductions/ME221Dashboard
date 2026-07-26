namespace ME221.Data.Models;

/// <summary>
/// An entity linked to a multi-entity gauge (e.g., Multi-Ring).
/// EntityId is the datalink ID; Color is the ring/bar accent (hex string).
/// </summary>
public sealed class LinkedEntityEntry
{
    public int EntityId { get; set; }
    public string? Color { get; set; }
}
