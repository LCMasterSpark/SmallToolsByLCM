// 文件作用：实时翻译互动逻辑，管理 OCR 捕获、去重翻译、悬浮窗生命周期和点击穿透切换。
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using 小工具集合.Services;
using 小工具集合.Services.Ocr;
using 小工具集合.Services.Translation;
using 小工具集合.Views.FunLab;
using Forms = System.Windows.Forms;
using WpfRect = System.Windows.Rect;

namespace 小工具集合.Views.Translation;

public partial class LiveTranslatorControl : UserControl, IInteractiveToolView, IDisposable
{
    private readonly TranslationSettingsService _settingsService = new();
    private readonly TranslationService _translationService = new();
    private readonly WindowsOcrService _ocrService = new();
    private readonly DispatcherTimer _timer;
    private TranslationSettings _settings;
    private LiveTranslationOverlayWindow? _overlay;
    private WpfRect? _selectedRegion;
    private WindowTarget? _selectedTargetWindow;
    private string _lastRecognizedText = string.Empty;
    private bool _isRunning;
    private bool _isTicking;

    public LiveTranslatorControl()
    {
        InitializeComponent();
        _settings = _settingsService.Load();
        _timer = new DispatcherTimer();
        _timer.Tick += Timer_Tick;
        ProviderBox.ItemsSource = TranslationService.ProviderOptions;
        SourceLanguageBox.ItemsSource = TranslationService.LanguageOptions;
        TargetLanguageBox.ItemsSource = TranslationService.LanguageOptions.Where(item => item != "自动").ToList();
        ProviderBox.SelectedItem = TranslationService.NormalizeProvider(_settings.DefaultProvider);
        SourceLanguageBox.SelectedItem = ToDisplayLanguage(_settings.DefaultSourceLanguage);
        TargetLanguageBox.SelectedItem = ToDisplayLanguage(_settings.DefaultTargetLanguage) == "自动"
            ? "中文"
            : ToDisplayLanguage(_settings.DefaultTargetLanguage);
        InputModeBox.SelectedIndex = 2;
        OverlayPlacementBox.SelectedIndex = _settings.LiveOverlayFollowTargetWindow ? 1 : 0;
        IntervalBox.Text = _settings.LiveRefreshMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        RefreshTargetWindows();
        // 先创建但不强制显示悬浮窗，便于用户进入工具后直接切换穿透/编辑模式。
        EnsureOverlay();
        UpdateOverlayModeText();
    }

    public void Deactivate()
    {
        Stop();
        CloseOverlay();
    }

    public void Dispose()
    {
        Deactivate();
        _timer.Tick -= Timer_Tick;
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        Start();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        Stop();
    }

    private void ShowOverlayButton_Click(object sender, RoutedEventArgs e)
    {
        EnsureOverlay();
        _overlay!.Show();
        _overlay.Activate();
        _overlay.SetClickThrough(_settings.LiveOverlayClickThrough);
    }

    private void OverlayModeButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.LiveOverlayClickThrough = !_settings.LiveOverlayClickThrough;
        _settingsService.Save(_settings);
        _overlay?.SetClickThrough(_settings.LiveOverlayClickThrough);
        UpdateOverlayModeText();
    }

    private void RefreshWindowsButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshTargetWindows();
    }

    private void LockSelectedWindowButton_Click(object sender, RoutedEventArgs e)
    {
        if (TargetWindowBox.SelectedItem is not WindowTarget target)
        {
            FooterText.Text = "状态：请先在目标窗口下拉框里选一个窗口。";
            return;
        }

        _selectedTargetWindow = target;
        InputModeBox.SelectedIndex = 3;
        FooterText.Text = "状态：已锁定窗口 - " + target.Title;
        MoveOverlayIfNeeded();
    }

    private void TargetWindowBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedTargetWindow = TargetWindowBox.SelectedItem as WindowTarget;
    }

    private void OverlayPlacementBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_settings is null)
        {
            return;
        }

        _settings.LiveOverlayFollowTargetWindow = IsFollowMode;
        _settingsService.Save(_settings);
        MoveOverlayIfNeeded();
    }

    private void SelectRegionButton_Click(object sender, RoutedEventArgs e)
    {
        var selector = new RegionSelectorWindow { Owner = Window.GetWindow(this) };
        if (selector.ShowDialog() == true)
        {
            _selectedRegion = selector.SelectedRegion;
            FooterText.Text = $"状态：已选择区域 {_selectedRegion.Value.Width:0} x {_selectedRegion.Value.Height:0}。";
        }
    }

    private void CloseOverlayButton_Click(object sender, RoutedEventArgs e)
    {
        CloseOverlay();
    }

    private void Start()
    {
        _settings = _settingsService.Load();
        int interval = ParseInterval();
        _settings.LiveRefreshMilliseconds = interval;
        _settings.DefaultProvider = ProviderBox.SelectedItem?.ToString() ?? _settings.DefaultProvider;
        _settings.DefaultSourceLanguage = SourceLanguageBox.SelectedItem?.ToString() ?? _settings.DefaultSourceLanguage;
        _settings.DefaultTargetLanguage = TargetLanguageBox.SelectedItem?.ToString() ?? _settings.DefaultTargetLanguage;
        _settings.LiveOverlayFollowTargetWindow = IsFollowMode;
        _settingsService.Save(_settings);
        _timer.Interval = TimeSpan.FromMilliseconds(interval);
        _isRunning = true;
        EnsureOverlay();
        _overlay!.Show();
        _overlay.SetClickThrough(_settings.LiveOverlayClickThrough);
        MoveOverlayIfNeeded();
        FooterText.Text = "状态：运行中。";
        SmallStatusText.Text = "running";
        _timer.Start();
    }

    private void Stop()
    {
        _isRunning = false;
        _timer.Stop();
        FooterText.Text = "状态：已停止。";
        SmallStatusText.Text = "stopped";
        _overlay?.SetText(TranslatedTextBox.Text, "已停止");
    }

    private async void Timer_Tick(object? sender, EventArgs e)
    {
        // OCR + 网络翻译可能慢于刷新间隔；用 _isTicking 防止重入堆积请求。
        if (!_isRunning || _isTicking)
        {
            return;
        }

        _isTicking = true;
        try
        {
            await CaptureAndTranslateAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or ExternalException)
        {
            SetStatus("失败：" + ex.Message);
        }
        finally
        {
            _isTicking = false;
        }
    }

    private async Task CaptureAndTranslateAsync()
    {
        string text = await CaptureTextAsync();
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("没有识别到文本");
            return;
        }

        text = text.Trim();
        RecognizedTextBox.Text = text;
        // 自动 OCR 会频繁命中同一段文字，去重可以省 API 调用，也让悬浮窗不闪。
        if (string.Equals(text, _lastRecognizedText, StringComparison.Ordinal))
        {
            SetStatus("重复文本已跳过");
            return;
        }

        _lastRecognizedText = text;
        TranslationResponse response = await _translationService.TranslateAsync(new TranslationRequest(
            text,
            SourceLanguageBox.SelectedItem?.ToString() ?? "自动",
            TargetLanguageBox.SelectedItem?.ToString() ?? "中文",
            ProviderBox.SelectedItem?.ToString() ?? "LibreTranslate",
            _settings));
        TranslatedTextBox.Text = response.Text;
        MoveOverlayIfNeeded();
        _overlay?.SetText(response.Text, response.Provider);
        SetStatus("已更新");
    }

    private async Task<string> CaptureTextAsync()
    {
        // 一期不做进程 hook。前台窗口模式只截窗口区域 OCR，避免注入进程带来的权限和安全风险。
        string mode = (InputModeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "选择区域 OCR";
        if (mode.Contains("剪贴板", StringComparison.Ordinal))
        {
            return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
        }

        byte[] imageBytes = mode switch
        {
            string value when value.Contains("全屏", StringComparison.Ordinal) => CaptureScreenBytes(GetVirtualScreenRect()),
            string value when value.Contains("指定窗口", StringComparison.Ordinal) => CaptureScreenBytes(GetSelectedWindowRect()),
            string value when value.Contains("前台窗口", StringComparison.Ordinal) => CaptureScreenBytes(GetForegroundWindowRect()),
            _ => CaptureScreenBytes(_selectedRegion ?? throw new InvalidOperationException("请先点击“选择区域”框选 OCR 区域。"))
        };

        return await _ocrService.RecognizeAsync(imageBytes, SourceLanguageBox.SelectedItem?.ToString() ?? "自动");
    }

    private static byte[] CaptureScreenBytes(WpfRect rect)
    {
        int x = (int)Math.Round(rect.X);
        int y = (int)Math.Round(rect.Y);
        int width = Math.Max(1, (int)Math.Round(rect.Width));
        int height = Math.Max(1, (int)Math.Round(rect.Height));
        using var bitmap = new Bitmap(width, height);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height));
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private static WpfRect GetVirtualScreenRect()
    {
        Rectangle bounds = Forms.Screen.AllScreens
            .Select(screen => screen.Bounds)
            .Aggregate(Rectangle.Union);
        return new WpfRect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
    }

    private static WpfRect GetForegroundWindowRect()
    {
        nint hwnd = GetForegroundWindow();
        if (hwnd == 0 || !GetWindowRect(hwnd, out NativeRect rect))
        {
            throw new InvalidOperationException("无法获取前台窗口区域。");
        }

        int width = Math.Max(1, rect.Right - rect.Left);
        int height = Math.Max(1, rect.Bottom - rect.Top);
        return new WpfRect(rect.Left, rect.Top, width, height);
    }

    private WpfRect GetSelectedWindowRect()
    {
        WindowTarget? target = TargetWindowBox.SelectedItem as WindowTarget ?? _selectedTargetWindow;
        if (target is null)
        {
            throw new InvalidOperationException("请先在“目标窗口”里选择一个窗口。");
        }

        if (!GetWindowRect(target.Hwnd, out NativeRect rect))
        {
            throw new InvalidOperationException("目标窗口已关闭或无法读取窗口区域。");
        }

        _selectedTargetWindow = target with
        {
            Bounds = new WpfRect(rect.Left, rect.Top, Math.Max(1, rect.Right - rect.Left), Math.Max(1, rect.Bottom - rect.Top))
        };
        return _selectedTargetWindow.Bounds;
    }

    private void MoveOverlayIfNeeded()
    {
        if (_overlay is null || !IsFollowMode)
        {
            return;
        }

        if (TargetWindowBox.SelectedItem is null && _selectedTargetWindow is null)
        {
            return;
        }

        WpfRect targetRect = GetSelectedWindowRect();
        const double gap = 12;
        _overlay.Left = targetRect.Left + Math.Max(0, (targetRect.Width - _overlay.Width) / 2);
        _overlay.Top = targetRect.Bottom + gap;
        if (_overlay.Top + _overlay.Height > SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
        {
            _overlay.Top = Math.Max(SystemParameters.VirtualScreenTop, targetRect.Top - _overlay.Height - gap);
        }
    }

    private void EnsureOverlay()
    {
        if (_overlay is not null)
        {
            return;
        }

        _overlay = new LiveTranslationOverlayWindow
        {
            Opacity = Math.Clamp(_settings.LiveOverlayOpacity, 0.35, 1.0),
            Left = SystemParameters.WorkArea.Left + 80,
            Top = SystemParameters.WorkArea.Bottom - 220
        };
        _overlay.OverlayCloseRequested += Overlay_OverlayCloseRequested;
        _overlay.SetClickThrough(_settings.LiveOverlayClickThrough);
    }

    private void Overlay_OverlayCloseRequested(object? sender, EventArgs e)
    {
        CloseOverlay();
    }

    private void CloseOverlay()
    {
        if (_overlay is null)
        {
            return;
        }

        _overlay.OverlayCloseRequested -= Overlay_OverlayCloseRequested;
        _overlay.Close();
        _overlay = null;
    }

    private int ParseInterval()
    {
        return int.TryParse(IntervalBox.Text, out int interval)
            ? Math.Clamp(interval, 500, 10000)
            : 1500;
    }

    private void SetStatus(string status)
    {
        FooterText.Text = "状态：" + status;
        SmallStatusText.Text = status;
        _overlay?.SetText(TranslatedTextBox.Text, status);
    }

    private void UpdateOverlayModeText()
    {
        OverlayModeButton.Content = _settings.LiveOverlayClickThrough ? "切到编辑模式" : "切到穿透模式";
    }

    private bool IsFollowMode => (OverlayPlacementBox.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Contains("跟随", StringComparison.Ordinal) == true;

    private void RefreshTargetWindows(nint preferredHandle = 0)
    {
        IReadOnlyList<WindowTarget> windows = EnumerateWindowTargets();
        TargetWindowBox.ItemsSource = windows;
        WindowTarget? selected = preferredHandle != 0
            ? windows.FirstOrDefault(item => item.Hwnd == preferredHandle)
            : windows.FirstOrDefault(item => _selectedTargetWindow?.Hwnd == item.Hwnd) ?? windows.FirstOrDefault();
        TargetWindowBox.SelectedItem = selected;
        _selectedTargetWindow = selected;
    }

    private static IReadOnlyList<WindowTarget> EnumerateWindowTargets()
    {
        int currentProcessId = Process.GetCurrentProcess().Id;
        var targets = new List<WindowTarget>();
        EnumWindows((hwnd, _) =>
        {
            WindowTarget? target = CreateWindowTarget(hwnd, currentProcessId);
            if (target is not null)
            {
                targets.Add(target);
            }

            return true;
        }, 0);

        return targets
            .DistinctBy(item => item.Hwnd)
            .OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static WindowTarget? CreateWindowTarget(nint hwnd, int? currentProcessId = null)
    {
        if (hwnd == 0 || !IsWindowVisible(hwnd))
        {
            return null;
        }

        GetWindowThreadProcessId(hwnd, out int processId);
        if (currentProcessId is not null && processId == currentProcessId.Value)
        {
            return null;
        }

        int titleLength = GetWindowTextLength(hwnd);
        if (titleLength <= 0)
        {
            return null;
        }

        var titleBuilder = new StringBuilder(titleLength + 1);
        _ = GetWindowText(hwnd, titleBuilder, titleBuilder.Capacity);
        string title = titleBuilder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        if (!GetWindowRect(hwnd, out NativeRect rect))
        {
            return null;
        }

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width <= 40 || height <= 40)
        {
            return null;
        }

        string processName = TryGetProcessName(processId);
        return new WindowTarget(
            hwnd,
            string.IsNullOrWhiteSpace(processName) ? title : $"{title}  [{processName}]",
            new WpfRect(rect.Left, rect.Top, width, height));
    }

    private static string TryGetProcessName(int processId)
    {
        try
        {
            return Process.GetProcessById(processId).ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ToDisplayLanguage(string value)
    {
        return TranslationService.NormalizeLanguage(value) switch
        {
            "auto" => "自动",
            "zh" => "中文",
            "en" => "英文",
            "ja" => "日文",
            "ko" => "韩文",
            "fr" => "法文",
            "de" => "德文",
            "es" => "西班牙文",
            "ru" => "俄文",
            _ => value
        };
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, StringBuilder text, int count);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out int processId);

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRect
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;
    }

    private sealed record WindowTarget(nint Hwnd, string Title, WpfRect Bounds);
}
