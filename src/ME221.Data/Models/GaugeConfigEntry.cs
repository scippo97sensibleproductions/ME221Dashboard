namespace ME221.Data.Models;

public sealed class GaugeConfigEntry
{
    public int Id { get; set; }
    public int GridRow { get; set; }
    public int GridColumn { get; set; }
    public int RowSpan { get; set; } = 2;
    public int ColumnSpan { get; set; } = 2;
    public int DisplayType { get; set; }
    public int ShapeCategory { get; set; }
    public double SweepAngle { get; set; } = 220.0;
    public int ArcPosition { get; set; }
    public string? IconName { get; set; }
    public double IconOffsetX { get; set; }
    public double IconOffsetY { get; set; }
    public double IconSize { get; set; } = 0.5;
    public int DigitalStyle { get; set; }
    public string? TexturePath { get; set; }
    public double NeedleStartAngle { get; set; } = 135.0;
    public double NeedleEndAngle { get; set; } = 405.0;
    public double NeedleOffsetX { get; set; }
    public double NeedleOffsetY { get; set; }
    public double NeedleWidth { get; set; } = 2.5;
    public double NeedleLength { get; set; } = 1.0;
    public List<NeedleCurvePoint>? NeedleCurve { get; set; }
    public double Scale { get; set; } = 1.0;
    public double FontSizeScale { get; set; } = 1.0;
    public List<ColorStop>? ColorStops { get; set; }
    public double ColorHysteresis { get; set; } = 0.03;
    public double LabelVerticalOffset { get; set; }
    public bool ShowName { get; set; } = true;
    public bool ShowUnit { get; set; } = true;
    public bool ShowValue { get; set; } = true;
    public bool SmoothingEnabled { get; set; }
    public double SmoothingFactor { get; set; } = 0.3;
    public int BarValuePosition { get; set; } = 4; // 0-8 grid (0=TopLeft, 4=Center, 8=BottomRight)
    public int BarUnitPosition { get; set; } = 7;
    public int BarNamePosition { get; set; } = 8;

    public double? X { get; set; }
    public double? Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public double FractionX { get; set; }
    public double FractionY { get; set; }
    public double WidthFraction { get; set; } = 0.22;
    public double HeightFraction { get; set; } = 0.28;

    // Chart-specific
    public int ChartTimeWindowSec { get; set; } = 30;
    public double? ChartYMin { get; set; }
    public double? ChartYMax { get; set; }
    public string ChartLineColor { get; set; } = "#22c8e6";
    public double ChartLineWidth { get; set; } = 2;
    public bool ChartShowGrid { get; set; } = true;
    public bool ChartFillUnder { get; set; }
    public bool ChartShowLabels { get; set; } = true;
    public int ChartPrecision { get; set; } = 1;
    public string TextColor { get; set; } = "#ffffff";
    public int ZIndex { get; set; }
}
