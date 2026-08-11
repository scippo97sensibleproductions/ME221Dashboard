namespace ME221.Data.Models;

public sealed class WarningPoint
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public float Value { get; set; }
    public string Direction { get; set; } = "max";
    public string LevelId { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}
