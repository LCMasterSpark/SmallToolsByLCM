// 文件作用：承接主窗口代码隐藏逻辑、动态参数控件和互动工具宿主。
// Copyright (c) 2026 LCMasterSpark. Licensed under the MIT License.
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MahApps.Metro.Controls;
using 小工具集合.Models;
using 小工具集合.Services;
using 小工具集合.Views.Controls;
using 小工具集合.Views.FunLab;
using 小工具集合.Views.Shell;
using 小工具集合.ViewModels;

namespace 小工具集合;

/// <summary>
/// 主窗口代码隐藏。布局和绑定放在 XAML 中，
/// 这里负责动态参数控件以及不适合写在 XAML 里的对话框交互。
/// </summary>
public partial class MainWindow : MetroWindow
{
    private readonly MainWindowViewModel _viewModel;
    private readonly SoundService _soundService;
    private readonly IToolParameterDialogService _parameterDialogService;
    private readonly DynamicParameterControlBuilder _parameterControlBuilder;
    private readonly IInteractiveToolViewFactory _interactiveToolViewFactory;
    private bool _isToolBrowserCollapsed;
    private bool _isShowingCopyright;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        _soundService = new SoundService { IsEnabled = _viewModel.IsUiSoundEnabled };
        _parameterDialogService = new WindowsToolParameterDialogService();
        _parameterControlBuilder = new DynamicParameterControlBuilder(_parameterDialogService);
        _interactiveToolViewFactory = new InteractiveToolViewFactory();
        DataContext = _viewModel;
        ApplyTheme(_viewModel.Theme);
        _viewModel.Parameters.CollectionChanged += Parameters_CollectionChanged;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.ToolExecutionCompleted += ViewModel_ToolExecutionCompleted;
        BuildParameterControls();
        UpdateInteractiveHost();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // 批处理工具允许先完成当前项目；正在处理或仍有文件队列时阻止关闭窗口。
        if (_viewModel.HasActiveOrQueuedWork)
        {
            e.Cancel = true;
            MessageBox.Show(
                this,
                _viewModel.IsBusy ? "当前有任务正在处理，请等待完成后再关闭程序。" : "当前文件批处理队列中还有文件。请先清空队列后再关闭程序。",
                "无法关闭",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        base.OnClosing(e);
        DisposeInteractiveHost();
    }

    private void GroupButton_Click(object sender, RoutedEventArgs e)
    {
        _soundService.Play(UiSound.Click);
        HideCopyrightWorkspace();
        if (sender is Button { Tag: ToolGroup group })
        {
            _viewModel.SearchText = string.Empty;
            _viewModel.SelectedGroup = group;
        }
    }

    private void ToolItemButton_Click(object sender, RoutedEventArgs e)
    {
        _soundService.Play(UiSound.Click);
        if (sender is Button { Tag: ToolBrowserItem item })
        {
            HideCopyrightWorkspace();
            _viewModel.SelectToolItem(item);
            if (!string.IsNullOrWhiteSpace(item.Tool.InteractiveViewKey) && InteractiveHost.Content is null)
            {
                UpdateInteractiveHost();
            }
        }
    }

    private void ToggleToolBrowserButton_Click(object sender, RoutedEventArgs e)
    {
        _soundService.Play(UiSound.Click);
        _isToolBrowserCollapsed = !_isToolBrowserCollapsed;
        ToolBrowserPanel.Visibility = _isToolBrowserCollapsed ? Visibility.Collapsed : Visibility.Visible;
        ToolBrowserColumn.Width = _isToolBrowserCollapsed ? new GridLength(0) : new GridLength(286);
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        _soundService.Play(UiSound.Click);
        if (string.IsNullOrEmpty(_viewModel.OutputText))
        {
            _viewModel.StatusText = "没有可复制的输出。";
            return;
        }

        Clipboard.SetText(_viewModel.OutputText);
        _viewModel.StatusText = "输出已复制到剪贴板。";
    }

    private void Parameters_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        BuildParameterControls();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedTool))
        {
            HideCopyrightWorkspace();
            UpdateInteractiveHost();
        }

        if (e.PropertyName == nameof(MainWindowViewModel.IsUiSoundEnabled))
        {
            _soundService.IsEnabled = _viewModel.IsUiSoundEnabled;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.Theme))
        {
            ApplyTheme(_viewModel.Theme);
        }
    }

    private void ViewModel_ToolExecutionCompleted(object? sender, bool isSuccess)
    {
        _soundService.Play(isSuccess ? UiSound.Success : UiSound.Error);
        FlashStatusBar(isSuccess ? Color.FromRgb(104, 33, 122) : Color.FromRgb(185, 38, 38));
    }

    private void CommandButton_Click(object sender, RoutedEventArgs e)
    {
        _soundService.Play(UiSound.Click);
    }

    private void ExecuteButton_Click(object sender, RoutedEventArgs e)
    {
        _soundService.Play(UiSound.Execute);
    }

    private void CopyrightButton_Click(object sender, RoutedEventArgs e)
    {
        _soundService.Play(UiSound.Click);
        ShowSettingsWorkspace("关于与引用");
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _soundService.Play(UiSound.Click);
        ShowSettingsWorkspace("设置中心");
    }

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        _soundService.Play(UiSound.Click);
        ShowSettingsWorkspace("关于与引用");
    }

    private void BrowseDefaultOutputButton_Click(object sender, RoutedEventArgs e)
    {
        _soundService.Play(UiSound.Click);
        string? folder = _parameterDialogService.PickFolder();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            _viewModel.DefaultOutputDirectory = folder;
        }
    }

    private void OpenStateFolderButton_Click(object sender, RoutedEventArgs e)
    {
        _soundService.Play(UiSound.Click);
        Process.Start(new ProcessStartInfo
        {
            FileName = AppContext.BaseDirectory,
            UseShellExecute = true
        });
    }

    private void SoundToggle_Click(object sender, RoutedEventArgs e)
    {
        _soundService.Play(UiSound.Click);
    }

    private void SoundToggle_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _soundService.Play(UiSound.Hover);
    }

    private void ShowSettingsWorkspace(string statusText)
    {
        ShowCopyrightWorkspace();
        _viewModel.StatusText = statusText;
    }

    private void ShowCopyrightWorkspace()
    {
        _isShowingCopyright = true;
        DisposeInteractiveHost();
        CopyrightWorkspace.Visibility = Visibility.Visible;
        _viewModel.StatusText = "版权与引用";
    }

    private void HideCopyrightWorkspace()
    {
        if (!_isShowingCopyright)
        {
            return;
        }

        _isShowingCopyright = false;
        CopyrightWorkspace.Visibility = Visibility.Collapsed;
    }

    private void FlashStatusBar(Color color)
    {
        if (StatusBarBorder.Background is not SolidColorBrush currentBrush)
        {
            return;
        }

        var flashBrush = new SolidColorBrush(color);
        StatusBarBorder.Background = flashBrush;
        var animation = new ColorAnimation
        {
            To = currentBrush.Color,
            Duration = TimeSpan.FromMilliseconds(420),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        animation.Completed += (_, _) => StatusBarBorder.Background = currentBrush;
        flashBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
    }

    private void ApplyTheme(string theme)
    {
        if (theme == "VS Blue")
        {
            SetBrush("VsAccentBrush", Color.FromRgb(0, 122, 204));
            SetBrush("VsAccentHoverBrush", Color.FromRgb(28, 151, 234));
            SetBrush("VsSelectionBrush", Color.FromRgb(38, 79, 120));
            GlowBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204));
        }
        else
        {
            SetBrush("VsAccentBrush", Color.FromRgb(104, 33, 122));
            SetBrush("VsAccentHoverBrush", Color.FromRgb(122, 47, 143));
            SetBrush("VsSelectionBrush", Color.FromRgb(75, 45, 94));
            GlowBrush = new SolidColorBrush(Color.FromRgb(104, 33, 122));
        }
    }

    private void SetBrush(string key, Color color)
    {
        Resources[key] = new SolidColorBrush(color);
    }

    private void UpdateInteractiveHost()
    {
        DisposeInteractiveHost();
        // 互动工具生命周期热点：凡是带 overlay、计时器或后台状态的控件，
        // 都必须在 DisposeInteractiveHost 中释放，避免切换工具后残留窗口。
        InteractiveHost.Content = _interactiveToolViewFactory.Create(_viewModel.SelectedTool.InteractiveViewKey);
    }

    private void DisposeInteractiveHost()
    {
        if (InteractiveHost.Content is IInteractiveToolView interactiveToolView)
        {
            interactiveToolView.Deactivate();
        }

        if (InteractiveHost.Content is IDisposable disposable)
        {
            disposable.Dispose();
        }

        InteractiveHost.Content = null;
    }

    private void BuildParameterControls()
    {
        _parameterControlBuilder.Build(ParametersPanel, _viewModel.Parameters);
    }
}
