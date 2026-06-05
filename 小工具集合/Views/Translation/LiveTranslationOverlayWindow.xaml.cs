// 文件作用：控制实时翻译悬浮窗文本、关闭按钮、任务栏显示和点击穿透窗口样式。
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

namespace 小工具集合.Views.Translation;

public partial class LiveTranslationOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;
    private bool _clickThrough;

    public LiveTranslationOverlayWindow()
    {
        InitializeComponent();
    }

    public event EventHandler? OverlayCloseRequested;

    public void SetText(string text, string status)
    {
        TranslationText.Text = string.IsNullOrWhiteSpace(text) ? "等待识别..." : text;
        StatusText.Text = status;
    }

    public void SetClickThrough(bool enabled)
    {
        _clickThrough = enabled;
        ApplyClickThrough();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        OverlayCloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OverlayHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_clickThrough && e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyClickThrough();
    }

    private void ApplyClickThrough()
    {
        // 悬浮窗默认像字幕层：展示结果但不挡鼠标。
        // 切到编辑模式时会撤掉 WS_EX_TRANSPARENT，方便拖动和调整大小。
        nint hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == 0)
        {
            return;
        }

        int style = GetWindowLong(hwnd, GwlExStyle);
        style |= WsExLayered;
        style = _clickThrough ? style | WsExTransparent : style & ~WsExTransparent;
        SetWindowLong(hwnd, GwlExStyle, style);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);
}
