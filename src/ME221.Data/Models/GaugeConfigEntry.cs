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
    public int WedgeStyle { get; set; }
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
    public double SmoothingResponseMs { get; set; }
    public double SpikeGatePercent { get; set; }
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

    public List<ValueTransformStep>? TransformSteps { get; set; }
    public string? CustomUnitLabel { get; set; }
    public bool ShowHistogram { get; set; }

    // Multi-entity support (Wedge, LED Ring, Multi-Ring gauges)
    public List<LinkedEntityEntry>? LinkedEntities { get; set; }

    // ── Gauge customization v2 ──
    // Arc scale marks
    public int TickCount { get; set; } = 3;
    public bool TickLabels { get; set; }
    public int TickLabelEvery { get; set; } = 1;
    public int TickSide { get; set; } // 0=inside 1=outside
    // Arc redline band
    public double RedlineStart { get; set; } // 0 = off
    public double RedlineWidth { get; set; } = 2;
    public string RedlineColor { get; set; } = "#E03131";
    // Arc needle shape
    public int NeedleShape { get; set; } // 0=line 1=tapered 2=paddle 3=counterweighted
    // Bar
    public int BarOrientation { get; set; } // 0=auto 1=horizontal 2=vertical
    public double BarThickness { get; set; } // 0 = auto
    public bool BarTicks { get; set; }
    public bool BarMinMaxLabels { get; set; }
    public double BarRedlineStart { get; set; } // 0 = off
    public string BarRedlineColor { get; set; } = "#E03131";
    // Text
    public bool ColorStopColoring { get; set; }
    public int PanelStyle { get; set; } // 0=none 1=pill 2=glass 3=card
    public double FlashThreshold { get; set; } // 0 = off
    // Digital theming
    public string LedColor { get; set; } = "#ff3333";
    public string DigitBgColor { get; set; } = "#1a1a1a";
    public double GlowStrength { get; set; } // 0 = current baseline
    public int DigitDecimals { get; set; } = -1; // -1 = auto
    public bool ZeroPadding { get; set; }
    public int MinDigitCount { get; set; }
    public bool RollAnimation { get; set; }
    public double RollSpeedMs { get; set; } = 300;
    // LedRing geometry
    public int SegmentCount { get; set; } = 36;
    public double SegmentGap { get; set; }
    public double RingStartAngle { get; set; }
    public double RingSweepAngle { get; set; } = 360;
    public double AmberThreshold { get; set; } = 0.7;
    public double RedThreshold { get; set; } = 0.85;
    // MultiRing geometry
    public int RingCount { get; set; } = 5;
    public double RingWidth { get; set; } // 0 = auto
    public double RingGap { get; set; } // 0 = auto
    public bool PeakHoldEnabled { get; set; } = true;
    public double PeakHoldAutoResetSec { get; set; }
    // WedgeBar
    public int WedgeSegmentCount { get; set; } = 32;
    public double WedgeRedlineStart { get; set; } = 0.8;
    // Shift-light gauge: how far before the shift point the first segment lights (R13).
    public double RampWidthRpm { get; set; } = 1500;
    // Shift-light gauge: color-zone count for the bar (1..3, default 3).
    public int ZoneCount { get; set; } = 3;
    // Chart overlays / style
    public List<ChartOverlayEntry>? ChartOverlays { get; set; }
    public int OverlayPillPosition { get; set; } // 0=topRight 1=topLeft 2=bottomRight 3=bottomLeft
    public double OverlayFontScale { get; set; } = 1;
    public int ChartLineStyle { get; set; } // 0=solid 1=dashed 2=dotted
    public string ChartBackgroundColor { get; set; } = "";
}
