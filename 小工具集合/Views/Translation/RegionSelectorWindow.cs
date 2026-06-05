// 文件作用：提供全屏透明拖拽框选窗口，用于实时翻译选择 OCR 捕获区域。
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DrawingRectangle = System.Drawing.Rectangle;
using Forms = System.Windows.Forms;

namespace 小工具集合.Views.Translation;

public sealed class RegionSelectorWindow : Window
{
    private readonly Canvas _canvas = new();
    private readonly Border _selection = new()
    {
        BorderBrush = Brushes.DeepSkyBlue,
        BorderThickness = new Thickness(2),
        Background = new SolidColorBrush(Color.FromArgb(48, 0, 122, 204)),
        Visibility = Visibility.Collapsed
    };
    private Point _start;

    public RegionSelectorWindow()
    {
        DrawingRectangle bounds = Forms.Screen.AllScreens
            .Select(screen => screen.Bounds)
            .Aggregate(DrawingRectangle.Union);
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = new SolidColorBrush(Color.FromArgb(44, 0, 0, 0));
        Topmost = true;
        ShowInTaskbar = false;
        Cursor = Cursors.Cross;
        Content = _canvas;
        _canvas.Children.Add(_selection);
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
            }
        };
    }

    public Rect SelectedRegion { get; private set; }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(this);
        _selection.Visibility = Visibility.Visible;
        Canvas.SetLeft(_selection, _start.X);
        Canvas.SetTop(_selection, _start.Y);
        _selection.Width = 0;
        _selection.Height = 0;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!IsMouseCaptured)
        {
            return;
        }

        Point current = e.GetPosition(this);
        double x = Math.Min(_start.X, current.X);
        double y = Math.Min(_start.Y, current.Y);
        double width = Math.Abs(current.X - _start.X);
        double height = Math.Abs(current.Y - _start.Y);
        Canvas.SetLeft(_selection, x);
        Canvas.SetTop(_selection, y);
        _selection.Width = width;
        _selection.Height = height;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        Point end = e.GetPosition(this);
        double x = Math.Min(_start.X, end.X) + Left;
        double y = Math.Min(_start.Y, end.Y) + Top;
        double width = Math.Abs(end.X - _start.X);
        double height = Math.Abs(end.Y - _start.Y);
        if (width < 8 || height < 8)
        {
            DialogResult = false;
            return;
        }

        SelectedRegion = new Rect(x, y, width, height);
        DialogResult = true;
    }
}
