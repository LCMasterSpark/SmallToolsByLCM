// 文件作用：绘制点击穿透的置顶准星 overlay，并按设置定位到目标屏幕。
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MediaBrush = System.Windows.Media.Brush;

namespace 小工具集合.Services.ScreenPointer;

public sealed class ScreenPointerOverlayWindow : Window
{
    private const int GwlExstyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;
    private const int WsExToolwindow = 0x00000080;

    private readonly ScreenService screenService;
    private readonly Canvas crosshairCanvas;
    private CrosshairSettings settings = new();
    private bool sourceReady;

    public ScreenPointerOverlayWindow(ScreenService screenService)
    {
        this.screenService = screenService;
        crosshairCanvas = new Canvas
        {
            Background = Brushes.Transparent,
            IsHitTestVisible = false,
            SnapsToDevicePixels = true
        };

        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Content = crosshairCanvas;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;
        ShowInTaskbar = false;
        SizeToContent = SizeToContent.Manual;
        Topmost = true;
        WindowStyle = WindowStyle.None;
        SourceInitialized += OnSourceInitialized;
    }

    public void ApplySettings(CrosshairSettings newSettings)
    {
        settings = newSettings.Clone();

        if (!settings.IsEnabled)
        {
            Hide();
            return;
        }

        Redraw();
        PositionWindow();

        if (!IsVisible)
        {
            Show();
        }

        Topmost = false;
        Topmost = true;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        sourceReady = true;
        IntPtr handle = new WindowInteropHelper(this).Handle;
        int style = GetWindowLong(handle, GwlExstyle);
        SetWindowLong(handle, GwlExstyle, style | WsExTransparent | WsExLayered | WsExToolwindow);
        PositionWindow();
    }

    private void Redraw()
    {
        double side = Math.Max(48, GetRequiredSide());
        Width = side;
        Height = side;
        crosshairCanvas.Width = side;
        crosshairCanvas.Height = side;
        crosshairCanvas.Children.Clear();

        CrosshairStyleSettings styleSettings = settings.CurrentStyleSettings;
        var brush = new SolidColorBrush(styleSettings.Color) { Opacity = styleSettings.Opacity };
        var outlineBrush = new SolidColorBrush(styleSettings.OutlineColor) { Opacity = styleSettings.Opacity };
        double center = side / 2;

        crosshairCanvas.RenderTransform = new RotateTransform(settings.Rotation, center, center);

        switch (settings.Style)
        {
            case CrosshairStyle.Cross:
                DrawCrossOutline(center, styleSettings.Primary, styleSettings.Gap, styleSettings.StrokeThickness, outlineBrush);
                DrawCross(center, styleSettings.Primary, styleSettings.Gap, brush, styleSettings.StrokeThickness);
                break;
            case CrosshairStyle.Dot:
                AddCircleOutline(center, center, Math.Max(1, styleSettings.Primary / 2), outlineBrush);
                AddCircle(center, center, Math.Max(1, styleSettings.Primary / 2), brush, true, 0);
                break;
            case CrosshairStyle.Ring:
                DrawRing(center, brush);
                break;
            case CrosshairStyle.TShape:
                DrawTShapeOutline(center, outlineBrush);
                DrawTShape(center, brush);
                break;
            case CrosshairStyle.Corners:
                DrawCornersOutline(center, outlineBrush);
                DrawCorners(center, brush);
                break;
            case CrosshairStyle.Image:
                DrawImage(side);
                break;
            case CrosshairStyle.DotCross:
            default:
                AddCircleOutline(center, center, Math.Max(1, styleSettings.Secondary / 2), outlineBrush);
                DrawCrossOutline(center, styleSettings.Primary, styleSettings.Gap, styleSettings.StrokeThickness, outlineBrush);
                AddCircle(center, center, Math.Max(1, styleSettings.Secondary / 2), brush, true, 0);
                DrawCross(center, styleSettings.Primary, styleSettings.Gap, brush, styleSettings.StrokeThickness);
                break;
        }
    }

    private double GetRequiredSide()
    {
        CrosshairStyleSettings styleSettings = settings.CurrentStyleSettings;
        double size = settings.Style switch
        {
            CrosshairStyle.Cross => (styleSettings.Primary + styleSettings.Gap) * 2 + (styleSettings.StrokeThickness + OutlinePadding()) * 4,
            CrosshairStyle.Dot => styleSettings.Primary + OutlinePadding() * 4 + 16,
            CrosshairStyle.Ring => RingOuterDiameter(styleSettings) + styleSettings.StrokeThickness * 4 + 16,
            CrosshairStyle.TShape => Math.Max(styleSettings.Primary * 2, styleSettings.Secondary + styleSettings.Gap) + (styleSettings.StrokeThickness + OutlinePadding()) * 4 + 16,
            CrosshairStyle.Corners => styleSettings.Primary + (styleSettings.StrokeThickness + OutlinePadding()) * 4 + 16,
            CrosshairStyle.Image => styleSettings.Primary + 16,
            CrosshairStyle.DotCross => (styleSettings.Primary + styleSettings.Gap) * 2 + (styleSettings.StrokeThickness + OutlinePadding()) * 4,
            _ => 96
        };

        return Math.Min(520, Math.Max(48, size));
    }

    private double OutlinePadding()
    {
        return IsOutlineActive() ? OutlineThickness() : 0;
    }

    private double OutlineThickness()
    {
        int even = (int)Math.Round(settings.CurrentStyleSettings.OutlineThickness / 2, MidpointRounding.AwayFromZero) * 2;
        return Math.Clamp(even, 2, 12);
    }

    private bool IsOutlineActive()
    {
        return settings.CurrentStyleSettings.OutlineEnabled
            && settings.Style != CrosshairStyle.Image
            && settings.Style != CrosshairStyle.Ring
            && OutlineThickness() > 0;
    }

    private void DrawCross(double center, double lineLength, double gap, MediaBrush brush, double thickness)
    {
        lineLength = Math.Max(1, lineLength);
        gap = Math.Max(0, gap);
        thickness = Math.Max(1, thickness);

        AddLine(center - gap - lineLength, center, center - gap, center, brush, thickness);
        AddLine(center + gap, center, center + gap + lineLength, center, brush, thickness);
        AddLine(center, center - gap - lineLength, center, center - gap, brush, thickness);
        AddLine(center, center + gap, center, center + gap + lineLength, brush, thickness);
    }

    private void DrawCrossOutline(double center, double lineLength, double gap, double thickness, MediaBrush outlineBrush)
    {
        if (IsOutlineActive())
        {
            DrawCross(center, lineLength, gap, outlineBrush, thickness + OutlineThickness() * 2);
        }
    }

    private void DrawRing(double center, MediaBrush brush)
    {
        CrosshairStyleSettings styleSettings = settings.CurrentStyleSettings;
        double outerRadius = RingOuterDiameter(styleSettings) / 2;
        double innerRadius = Math.Max(0, styleSettings.Gap / 2);
        double thickness = Math.Max(1, Math.Min(styleSettings.StrokeThickness, outerRadius - innerRadius));
        double strokeRadius = Math.Max(1, innerRadius + thickness / 2);
        AddCircle(center, center, strokeRadius, brush, false, thickness);
    }

    private static double RingOuterDiameter(CrosshairStyleSettings styleSettings)
    {
        return Math.Max(2, styleSettings.Gap + styleSettings.StrokeThickness * 2);
    }

    private void DrawTShape(double center, MediaBrush brush, double? thicknessOverride = null)
    {
        CrosshairStyleSettings styleSettings = settings.CurrentStyleSettings;
        double halfHorizontal = Math.Max(1, styleSettings.Primary / 2);
        double verticalLength = Math.Max(1, styleSettings.Secondary);
        double gap = Math.Max(0, styleSettings.Gap);
        double thickness = Math.Max(1, thicknessOverride ?? styleSettings.StrokeThickness);

        AddLine(center - halfHorizontal, center, center + halfHorizontal, center, brush, thickness);
        AddLine(center, center - gap - verticalLength, center, center - gap, brush, thickness);
    }

    private void DrawTShapeOutline(double center, MediaBrush outlineBrush)
    {
        if (IsOutlineActive())
        {
            DrawTShape(center, outlineBrush, settings.CurrentStyleSettings.StrokeThickness + OutlineThickness() * 2);
        }
    }

    private void DrawCorners(double center, MediaBrush brush, double? thicknessOverride = null)
    {
        CrosshairStyleSettings styleSettings = settings.CurrentStyleSettings;
        double halfSize = Math.Max(8, styleSettings.Primary / 2);
        double arm = Math.Clamp(styleSettings.Secondary, 2, halfSize);
        double gap = Math.Clamp(styleSettings.Gap, 0, halfSize - 2);
        double thickness = Math.Max(1, thicknessOverride ?? styleSettings.StrokeThickness);
        double left = center - halfSize;
        double right = center + halfSize;
        double top = center - halfSize;
        double bottom = center + halfSize;
        double innerLeft = center - gap;
        double innerRight = center + gap;
        double innerTop = center - gap;
        double innerBottom = center + gap;

        AddLine(left, innerTop, Math.Min(left + arm, innerLeft), innerTop, brush, thickness);
        AddLine(innerLeft, top, innerLeft, Math.Min(top + arm, innerTop), brush, thickness);
        AddLine(Math.Max(right - arm, innerRight), innerTop, right, innerTop, brush, thickness);
        AddLine(innerRight, top, innerRight, Math.Min(top + arm, innerTop), brush, thickness);
        AddLine(left, innerBottom, Math.Min(left + arm, innerLeft), innerBottom, brush, thickness);
        AddLine(innerLeft, Math.Max(bottom - arm, innerBottom), innerLeft, bottom, brush, thickness);
        AddLine(Math.Max(right - arm, innerRight), innerBottom, right, innerBottom, brush, thickness);
        AddLine(innerRight, Math.Max(bottom - arm, innerBottom), innerRight, bottom, brush, thickness);
    }

    private void DrawCornersOutline(double center, MediaBrush outlineBrush)
    {
        if (IsOutlineActive())
        {
            DrawCorners(center, outlineBrush, settings.CurrentStyleSettings.StrokeThickness + OutlineThickness() * 2);
        }
    }

    private void AddCircleOutline(double centerX, double centerY, double radius, MediaBrush outlineBrush)
    {
        if (IsOutlineActive())
        {
            AddCircle(centerX, centerY, radius + OutlineThickness() / 2, outlineBrush, true, 0);
        }
    }

    private void DrawImage(double side)
    {
        if (string.IsNullOrWhiteSpace(settings.ImagePath) || !File.Exists(settings.ImagePath))
        {
            return;
        }

        double imageSize = Math.Max(16, settings.CurrentStyleSettings.Primary);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(settings.ImagePath);
        bitmap.DecodePixelWidth = (int)imageSize;
        bitmap.EndInit();
        bitmap.Freeze();

        var image = new Image
        {
            Source = bitmap,
            Width = imageSize,
            Height = imageSize,
            Opacity = settings.CurrentStyleSettings.Opacity,
            Stretch = Stretch.Uniform,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(image, (side - imageSize) / 2);
        Canvas.SetTop(image, (side - imageSize) / 2);
        crosshairCanvas.Children.Add(image);
    }

    private void AddLine(double x1, double y1, double x2, double y2, MediaBrush brush, double thickness)
    {
        crosshairCanvas.Children.Add(new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = brush,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            IsHitTestVisible = false
        });
    }

    private void AddCircle(double centerX, double centerY, double radius, MediaBrush brush, bool fill, double thickness)
    {
        var ellipse = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = fill ? brush : Brushes.Transparent,
            Stroke = fill ? null : brush,
            StrokeThickness = thickness,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(ellipse, centerX - radius);
        Canvas.SetTop(ellipse, centerY - radius);
        crosshairCanvas.Children.Add(ellipse);
    }

    private void PositionWindow()
    {
        if (!sourceReady)
        {
            return;
        }

        DisplayInfo display = screenService.GetTargetDisplay(settings.TargetScreenDeviceName);
        double centerX = display.Bounds.Left + display.Bounds.Width / 2 + (settings.LockToCenter ? 0 : settings.OffsetX);
        double centerY = display.Bounds.Top + display.Bounds.Height / 2 + (settings.LockToCenter ? 0 : settings.OffsetY);
        PresentationSource? source = PresentationSource.FromVisual(this);
        Matrix transform = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        Point centerDip = transform.Transform(new Point(centerX, centerY));
        Left = centerDip.X - Width / 2;
        Top = centerDip.Y - Height / 2;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
