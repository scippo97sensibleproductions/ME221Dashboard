namespace ME221.Data.Models;

/// <summary>
/// An overlay line plotted on a Chart gauge alongside the primary entity line.
/// EntityId is the datalink ID; Color is the line color (hex string).
/// </summary>
public sealed class ChartOverlayEntry
{
    public int EntityId { get; set; }
    public string? Color { get; set; }
    public double LineWidth { get; set; } = 1.5;
    public int LineStyle { get; set; } // 0=solid 1=dashed 2=dotted
}
