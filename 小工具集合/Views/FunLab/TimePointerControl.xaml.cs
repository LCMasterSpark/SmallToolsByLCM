// 文件作用：实现指向任务栏时间区域的点击穿透箭头 overlay。
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace 小工具集合.Views.FunLab;

public partial class TimePointerControl : UserControl, IInteractiveToolView, IDisposable
{
    private const int AbmGetTaskbarPos = 0x00000005;
    private const int AbeLeft = 0;
    private const int AbeTop = 1;
    private const int AbeRight = 2;
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const double TargetInset = 40;
    private static readonly TimeSpan AutomationProbeInterval = TimeSpan.FromMilliseconds(500);

    private readonly DispatcherTimer arrowTimer;
    private Window? overlayWindow;
    private Canvas? overlayCanvas;
    private Line? directionLine;
    private Polygon? directionHead;
    private DateTime lastAutomationProbe = DateTime.MinValue;
    private Point? cachedAutomationTarget;
    private bool isArrowTracking;

    public TimePointerControl()
    {
        InitializeComponent();
        arrowTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16.6)
        };
        arrowTimer.Tick += (_, _) => UpdateArrow();
        Unloaded += (_, _) => Deactivate();
    }

    public void Deactivate()
    {
        StopArrowTracking();
    }

    public void Dispose()
    {
        Deactivate();
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        StartArrowTracking();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        StopArrowTracking();
    }

    private void StartArrowTracking()
    {
        isArrowTracking = true;
        StartButton.IsEnabled = false;
        StatusText.Text = "运行中";
        EnsureOverlayWindow();
        UpdateArrow();
        arrowTimer.Start();
    }

    private void StopArrowTracking()
    {
        isArrowTracking = false;
        arrowTimer.Stop();
        overlayWindow?.Close();
        overlayWindow = null;
        overlayCanvas = null;
        directionLine = null;
        directionHead = null;
        StartButton.IsEnabled = true;
        StatusText.Text = "待机";
    }

    private void EnsureOverlayWindow()
    {
        if (overlayWindow is not null)
        {
            return;
        }

        overlayCanvas = new Canvas
        {
            Background = Brushes.Transparent,
            IsHitTestVisible = false
        };

        directionLine = new Line
        {
            Stroke = Brushes.Red,
            StrokeThickness = 7,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        directionHead = new Polygon
        {
            Fill = Brushes.Red
        };

        overlayCanvas.Children.Add(directionLine);
        overlayCanvas.Children.Add(directionHead);

        overlayWindow = new Window
        {
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Content = overlayCanvas,
            IsHitTestVisible = false,
            Left = SystemParameters.VirtualScreenLeft,
            Top = SystemParameters.VirtualScreenTop,
            Width = SystemParameters.VirtualScreenWidth,
            Height = SystemParameters.VirtualScreenHeight,
            ResizeMode = ResizeMode.NoResize,
            ShowActivated = false,
            ShowInTaskbar = false,
            Topmost = true,
            WindowStyle = WindowStyle.None
        };

        overlayWindow.SourceInitialized += (_, _) => MakeWindowClickThrough(overlayWindow);
        overlayWindow.Show();
    }

    private void UpdateArrow()
    {
        if (!isArrowTracking || overlayCanvas is null || directionLine is null || directionHead is null)
        {
            return;
        }

        KeepOverlayOnVirtualScreen();
        Point sourceScreen = PointToScreen(new Point(ActualWidth / 2, ActualHeight / 2));
        Point targetScreen = ShortenTargetBeforeClock(sourceScreen, GetTaskbarClockTargetPoint());
        Point source = overlayCanvas.PointFromScreen(sourceScreen);
        Point target = overlayCanvas.PointFromScreen(targetScreen);
        UpdateArrowGeometry(source, target);
    }

    private static Point ShortenTargetBeforeClock(Point source, Point target)
    {
        Vector direction = target - source;
        if (direction.Length < 0.01)
        {
            return target;
        }

        direction.Normalize();
        return target - (direction * TargetInset);
    }

    private void KeepOverlayOnVirtualScreen()
    {
        if (overlayWindow is null)
        {
            return;
        }

        overlayWindow.Left = SystemParameters.VirtualScreenLeft;
        overlayWindow.Top = SystemParameters.VirtualScreenTop;
        overlayWindow.Width = SystemParameters.VirtualScreenWidth;
        overlayWindow.Height = SystemParameters.VirtualScreenHeight;
    }

    private Point GetTaskbarClockTargetPoint()
    {
        if (DateTime.UtcNow - lastAutomationProbe > AutomationProbeInterval)
        {
            lastAutomationProbe = DateTime.UtcNow;
            cachedAutomationTarget = TryGetTaskbarClockTargetPointFromAutomation();
        }

        return cachedAutomationTarget ?? GetEstimatedTaskbarClockTargetPoint();
    }

    private static Point? TryGetTaskbarClockTargetPointFromAutomation()
    {
        try
        {
            IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);
            if (taskbarHandle == IntPtr.Zero)
            {
                return null;
            }

            AutomationElement taskbar = AutomationElement.FromHandle(taskbarHandle);
            Point estimatedTarget = GetEstimatedTaskbarClockTargetPoint();
            var condition = new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Pane),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Custom));

            List<AutomationCandidate> candidates = taskbar.FindAll(TreeScope.Descendants, condition)
                .Cast<AutomationElement>()
                .Select(element => TryCreateAutomationCandidate(element, estimatedTarget))
                .Where(candidate => candidate is not null)
                .Select(candidate => candidate!.Value)
                .OrderBy(candidate => candidate.Score)
                .ToList();

            if (candidates.Count == 0)
            {
                return null;
            }

            Rect bounds = candidates[0].Bounds;
            return new Point(bounds.Left + bounds.Width / 2.0, bounds.Top + bounds.Height / 2.0);
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException)
        {
            return null;
        }
    }

    private static AutomationCandidate? TryCreateAutomationCandidate(AutomationElement element, Point estimatedTarget)
    {
        Rect bounds = element.Current.BoundingRectangle;
        if (bounds.IsEmpty || bounds.Width < 8 || bounds.Height < 8 || bounds.Width > 360 || bounds.Height > 140)
        {
            return null;
        }

        Point center = new(bounds.Left + bounds.Width / 2.0, bounds.Top + bounds.Height / 2.0);
        double score = GetDistance(center, estimatedTarget);
        string name = element.Current.Name ?? string.Empty;
        if (LooksLikeClockElementName(name))
        {
            score -= 600;
        }

        if (bounds.Contains(estimatedTarget))
        {
            score -= 250;
        }

        return new AutomationCandidate(bounds, score);
    }

    private static bool LooksLikeClockElementName(string name)
    {
        return name.Contains("日期", StringComparison.OrdinalIgnoreCase)
            || name.Contains("时间", StringComparison.OrdinalIgnoreCase)
            || name.Contains("时钟", StringComparison.OrdinalIgnoreCase)
            || name.Contains("date", StringComparison.OrdinalIgnoreCase)
            || name.Contains("time", StringComparison.OrdinalIgnoreCase)
            || name.Contains("clock", StringComparison.OrdinalIgnoreCase)
            || name.Contains("calendar", StringComparison.OrdinalIgnoreCase);
    }

    private static double GetDistance(Point first, Point second)
    {
        double x = first.X - second.X;
        double y = first.Y - second.Y;
        return Math.Sqrt(x * x + y * y);
    }

    private static Point GetEstimatedTaskbarClockTargetPoint()
    {
        var appBarData = new APPBARDATA
        {
            cbSize = Marshal.SizeOf<APPBARDATA>()
        };

        IntPtr result = SHAppBarMessage(AbmGetTaskbarPos, ref appBarData);
        if (result == IntPtr.Zero)
        {
            return new Point(SystemParameters.PrimaryScreenWidth - 24, SystemParameters.PrimaryScreenHeight - 24);
        }

        RECT rect = appBarData.rc;
        return appBarData.uEdge switch
        {
            AbeTop => new Point(rect.Right - 80, rect.Top + (rect.Bottom - rect.Top) / 2.0),
            AbeLeft => new Point(rect.Left + (rect.Right - rect.Left) / 2.0, rect.Bottom - 46),
            AbeRight => new Point(rect.Left + (rect.Right - rect.Left) / 2.0, rect.Bottom - 46),
            _ => new Point(rect.Right - 80, rect.Top + (rect.Bottom - rect.Top) / 2.0)
        };
    }

    private void UpdateArrowGeometry(Point source, Point target)
    {
        if (directionLine is null || directionHead is null)
        {
            return;
        }

        Vector direction = target - source;
        if (direction.Length < 0.01)
        {
            direction = new Vector(1, 1);
        }

        direction.Normalize();
        Point lineEnd = target - direction * 18;
        directionLine.X1 = source.X;
        directionLine.Y1 = source.Y;
        directionLine.X2 = lineEnd.X;
        directionLine.Y2 = lineEnd.Y;

        Vector normal = new(-direction.Y, direction.X);
        const double headLength = 34;
        const double headWidth = 26;
        Point basePoint = target - direction * headLength;

        directionHead.Points.Clear();
        directionHead.Points.Add(target);
        directionHead.Points.Add(basePoint + normal * (headWidth / 2));
        directionHead.Points.Add(basePoint - normal * (headWidth / 2));
    }

    private static void MakeWindowClickThrough(Window window)
    {
        IntPtr handle = new WindowInteropHelper(window).Handle;
        int styles = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, styles | WsExTransparent | WsExToolWindow);
    }

    private readonly record struct AutomationCandidate(Rect Bounds, double Score);

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern IntPtr SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uCallbackMessage;
        public int uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
