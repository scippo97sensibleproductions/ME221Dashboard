namespace ME221.Data.Models;

public sealed class WarningLevel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public bool Autolog { get; set; }
    public bool Flash { get; set; }
    public int Order { get; set; }
}
