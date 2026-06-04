using System.Windows.Media;

namespace 小工具集合.Services.ScreenPointer;

public sealed class CrosshairSettings
{
    public bool IsEnabled { get; set; } = true;
    public CrosshairStyle Style { get; set; } = CrosshairStyle.DotCross;
    public string? ImagePath { get; set; }
    public Dictionary<CrosshairStyle, CrosshairStyleSettings> Styles { get; set; } = CreateDefaultStyles();
    public double Rotation { get; set; }
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public bool LockToCenter { get; set; } = true;
    public string? TargetScreenDeviceName { get; set; }

    public CrosshairStyleSettings CurrentStyleSettings => GetStyleSettings(Style);

    public CrosshairStyleSettings GetStyleSettings(CrosshairStyle style)
    {
        Styles ??= CreateDefaultStyles();
        if (!Styles.TryGetValue(style, out CrosshairStyleSettings? styleSettings))
        {
            styleSettings = CrosshairStyleSettings.CreateDefault(style);
            Styles[style] = styleSettings;
        }

        styleSettings.EnsureDefaults(style);
        return styleSettings;
    }

    public CrosshairSettings Clone()
    {
        var clone = (CrosshairSettings)MemberwiseClone();
        clone.Styles = Styles.ToDictionary(pair => pair.Key, pair => pair.Value.Clone());
        return clone;
    }

    private static Dictionary<CrosshairStyle, CrosshairStyleSettings> CreateDefaultStyles()
    {
        return Enum.GetValues<CrosshairStyle>().ToDictionary(style => style, CrosshairStyleSettings.CreateDefault);
    }
}

public sealed class CrosshairStyleSettings
{
    public string ColorHex { get; set; } = "#FF00E5FF";
    public string OutlineColorHex { get; set; } = "#FF000000";
    public double Opacity { get; set; } = 0.95;
    public bool OutlineEnabled { get; set; }
    public double OutlineThickness { get; set; } = 2;
    public double Primary { get; set; }
    public double Secondary { get; set; }
    public double StrokeThickness { get; set; }
    public double Gap { get; set; }

    public Color Color => ParseColor(ColorHex, Colors.DeepSkyBlue);
    public Color OutlineColor => ParseColor(OutlineColorHex, Colors.Black);

    public static CrosshairStyleSettings CreateDefault(CrosshairStyle style)
    {
        return style switch
        {
            CrosshairStyle.Cross => new CrosshairStyleSettings { Primary = 42, StrokeThickness = 3, Gap = 8 },
            CrosshairStyle.Dot => new CrosshairStyleSettings { Primary = 6 },
            CrosshairStyle.Ring => new CrosshairStyleSettings { Primary = 32, StrokeThickness = 3, Gap = 26 },
            CrosshairStyle.TShape => new CrosshairStyleSettings { Primary = 68, Secondary = 42, StrokeThickness = 3, Gap = 8 },
            CrosshairStyle.Corners => new CrosshairStyleSettings { Primary = 72, Secondary = 18, StrokeThickness = 3, Gap = 14 },
            CrosshairStyle.Image => new CrosshairStyleSettings { Primary = 72 },
            CrosshairStyle.DotCross => new CrosshairStyleSettings { Primary = 38, Secondary = 5, StrokeThickness = 3, Gap = 9 },
            _ => new CrosshairStyleSettings { Primary = 38, Secondary = 5, StrokeThickness = 3, Gap = 9 }
        };
    }

    public void EnsureDefaults(CrosshairStyle style)
    {
        if (string.IsNullOrWhiteSpace(ColorHex))
        {
            ColorHex = "#FF00E5FF";
        }

        if (string.IsNullOrWhiteSpace(OutlineColorHex))
        {
            OutlineColorHex = "#FF000000";
        }

        if (Opacity <= 0)
        {
            Opacity = 0.95;
        }

        if (OutlineThickness <= 0)
        {
            OutlineThickness = 2;
        }

        switch (style)
        {
            case CrosshairStyle.Cross:
                Primary = DefaultIfUnset(Primary, 42);
                StrokeThickness = DefaultIfUnset(StrokeThickness, 3);
                Gap = Math.Max(0, Gap);
                break;
            case CrosshairStyle.Dot:
                Primary = DefaultIfUnset(Primary, 6);
                break;
            case CrosshairStyle.Ring:
                StrokeThickness = DefaultIfUnset(StrokeThickness, 3);
                Gap = Math.Max(0, Gap);
                Primary = Math.Max(2, Gap + StrokeThickness * 2);
                break;
            case CrosshairStyle.TShape:
                Primary = DefaultIfUnset(Primary, 68);
                Secondary = DefaultIfUnset(Secondary, 42);
                StrokeThickness = DefaultIfUnset(StrokeThickness, 3);
                Gap = Math.Max(0, Gap);
                break;
            case CrosshairStyle.Corners:
                Primary = DefaultIfUnset(Primary, 72);
                Secondary = DefaultIfUnset(Secondary, 18);
                StrokeThickness = DefaultIfUnset(StrokeThickness, 3);
                Gap = Math.Max(0, Gap);
                break;
            case CrosshairStyle.Image:
                Primary = DefaultIfUnset(Primary, 72);
                break;
            case CrosshairStyle.DotCross:
            default:
                Primary = DefaultIfUnset(Primary, 38);
                Secondary = DefaultIfUnset(Secondary, 5);
                StrokeThickness = DefaultIfUnset(StrokeThickness, 3);
                Gap = Math.Max(0, Gap);
                break;
        }
    }

    public CrosshairStyleSettings Clone()
    {
        return (CrosshairStyleSettings)MemberwiseClone();
    }

    private static double DefaultIfUnset(double value, double fallback)
    {
        return value > 0 ? value : fallback;
    }

    private static Color ParseColor(string colorHex, Color fallback)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(colorHex);
        }
        catch
        {
            return fallback;
        }
    }
}
