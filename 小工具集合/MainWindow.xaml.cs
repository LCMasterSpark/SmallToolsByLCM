// 文件作用：承接主窗口代码隐藏逻辑、动态参数控件和互动工具宿主。
// Copyright (c) 2026 LCMasterSpark. Licensed under the MIT License.
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MahApps.Metro.Controls;
using Microsoft.Win32;
using 小工具集合.Models;
using 小工具集合.Views.Generation;
using 小工具集合.Views.FunLab;
using 小工具集合.ViewModels;

namespace 小工具集合;

/// <summary>
/// 主窗口代码隐藏。布局和绑定放在 XAML 中，
/// 这里负责动态参数控件以及不适合写在 XAML 里的对话框交互。
/// </summary>
public partial class MainWindow : MetroWindow
{
    private readonly MainWindowViewModel _viewModel;
    private bool _isToolBrowserCollapsed;
    private static readonly Brush GeneratedLabelBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200));
    private static readonly Brush GeneratedTextBrush = new SolidColorBrush(Color.FromRgb(241, 241, 241));
    private static readonly Brush GeneratedMutedBrush = new SolidColorBrush(Color.FromRgb(150, 150, 150));
    private static readonly Brush GeneratedInputBrush = new SolidColorBrush(Color.FromRgb(31, 31, 31));
    private static readonly Brush GeneratedButtonBrush = new SolidColorBrush(Color.FromRgb(45, 45, 48));
    private static readonly Brush GeneratedButtonHoverBrush = new SolidColorBrush(Color.FromRgb(62, 62, 66));
    private static readonly Brush GeneratedBorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70));
    private static readonly Brush GeneratedSelectionBrush = new SolidColorBrush(Color.FromRgb(9, 71, 113));

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
        _viewModel.Parameters.CollectionChanged += Parameters_CollectionChanged;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
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
        if (sender is Button { Tag: ToolGroup group })
        {
            _viewModel.SearchText = string.Empty;
            _viewModel.SelectedGroup = group;
        }
    }

    private void ToolItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ToolBrowserItem item })
        {
            _viewModel.SelectToolItem(item);
        }
    }

    private void ToggleToolBrowserButton_Click(object sender, RoutedEventArgs e)
    {
        _isToolBrowserCollapsed = !_isToolBrowserCollapsed;
        ToolBrowserPanel.Visibility = _isToolBrowserCollapsed ? Visibility.Collapsed : Visibility.Visible;
        ToolBrowserColumn.Width = _isToolBrowserCollapsed ? new GridLength(0) : new GridLength(286);
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
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
            UpdateInteractiveHost();
        }
    }

    private void UpdateInteractiveHost()
    {
        DisposeInteractiveHost();
        InteractiveHost.Content = _viewModel.SelectedTool.InteractiveViewKey switch
        {
            "powerChecker" => new PowerCheckerControl(),
            "timePointer" => new TimePointerControl(),
            "minesweeper" => new MinesweeperControl(),
            "qrCode" => new QrCodeControl(),
            "screenPointer" => new ScreenPointerControl(),
            _ => null
        };
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
        ParametersPanel.Children.Clear();

        foreach (ToolParameterValue parameter in _viewModel.Parameters)
        {
            // ToolCatalog 定义参数元数据；这里把每个定义转换成具体 WPF 控件，
            // 并同步控件值到运行时参数。
            var block = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 0, 14)
            };

            block.Children.Add(new TextBlock
            {
                Text = parameter.Definition.Name,
                Margin = new Thickness(0, 0, 0, 6),
                FontSize = 12.5,
                Foreground = GeneratedLabelBrush
            });

            FrameworkElement input = parameter.Definition.Kind switch
            {
                ToolParameterKind.Password => CreatePasswordBox(parameter),
                ToolParameterKind.Multiline => CreateMultilineBox(parameter),
                ToolParameterKind.Combo => CreateComboBox(parameter),
                ToolParameterKind.FileOpen => CreateFilePicker(parameter, true),
                ToolParameterKind.FileSave => CreateFilePicker(parameter, false),
                ToolParameterKind.FileList => CreateFileListPicker(parameter),
                ToolParameterKind.Directory => CreateDirectoryPicker(parameter),
                ToolParameterKind.Number => CreateNumberBox(parameter),
                ToolParameterKind.CheckBox => CreateCheckBox(parameter),
                ToolParameterKind.ReadOnly => CreateReadOnlyText(parameter),
                _ => CreateTextBox(parameter)
            };

            block.Children.Add(input);
            ParametersPanel.Children.Add(block);
        }
    }

    private static TextBox CreateTextBox(ToolParameterValue parameter)
    {
        var textBox = new TextBox
        {
            Width = 260,
            Height = 30,
            Text = parameter.Value
        };
        ApplyGeneratedTextBoxStyle(textBox);
        textBox.TextChanged += (_, _) => parameter.Value = textBox.Text;
        return textBox;
    }

    private static PasswordBox CreatePasswordBox(ToolParameterValue parameter)
    {
        var passwordBox = new PasswordBox
        {
            Width = 260,
            Height = 30
        };
        ApplyGeneratedPasswordBoxStyle(passwordBox);
        passwordBox.PasswordChanged += (_, _) => parameter.Value = passwordBox.Password;
        return passwordBox;
    }

    private static TextBox CreateMultilineBox(ToolParameterValue parameter)
    {
        var textBox = new TextBox
        {
            Width = 260,
            Height = 88,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = parameter.Value
        };
        ApplyGeneratedTextBoxStyle(textBox);
        textBox.TextChanged += (_, _) => parameter.Value = textBox.Text;
        return textBox;
    }

    private static ComboBox CreateComboBox(ToolParameterValue parameter)
    {
        var comboBox = new ComboBox
        {
            Width = 260,
            Height = 30,
            ItemsSource = parameter.Definition.Options,
            SelectedItem = string.IsNullOrWhiteSpace(parameter.Value) ? parameter.Definition.DefaultValue : parameter.Value
        };
        ApplyGeneratedComboBoxStyle(comboBox);
        comboBox.SelectionChanged += (_, _) => parameter.Value = comboBox.SelectedItem?.ToString() ?? string.Empty;
        parameter.Value = comboBox.SelectedItem?.ToString() ?? parameter.Value;
        return comboBox;
    }

    private static FrameworkElement CreateFilePicker(ToolParameterValue parameter, bool openFile)
    {
        var panel = new StackPanel
        {
            Width = 260,
            Orientation = Orientation.Vertical
        };

        var textBox = new TextBox
        {
            Height = 30,
            Text = parameter.Value
        };
        ApplyGeneratedTextBoxStyle(textBox);
        textBox.TextChanged += (_, _) => parameter.Value = textBox.Text;

        var button = new Button
        {
            Content = "浏览",
            Width = 92,
            Height = 30,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        ApplyGeneratedButtonStyle(button);
        button.Click += (_, _) =>
        {
            string? fileName = openFile ? PickInputFile() : PickOutputFile();
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                textBox.Text = fileName;
                parameter.Value = fileName;
            }
        };

        panel.Children.Add(textBox);
        panel.Children.Add(button);
        return panel;
    }

    private static FrameworkElement CreateFileListPicker(ToolParameterValue parameter)
    {
        var panel = new StackPanel
        {
            Width = 260,
            Orientation = Orientation.Vertical
        };

        var textBox = new TextBox
        {
            Height = 92,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = parameter.Value
        };
        ApplyGeneratedTextBoxStyle(textBox);
        textBox.TextChanged += (_, _) => parameter.Value = textBox.Text;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 8, 0, 0)
        };
        var addButton = new Button { Content = "添加文件", Width = 110, Height = 34, Margin = new Thickness(0, 0, 8, 0) };
        var clearButton = new Button { Content = "清空", Width = 76, Height = 34 };
        ApplyGeneratedButtonStyle(addButton);
        ApplyGeneratedButtonStyle(clearButton);
        addButton.Click += (_, _) =>
        {
            string[] files = PickMultipleFiles();
            if (files.Length == 0)
            {
                return;
            }

            // 保留已有队列，并把新选择的文件逐行追加，保持和 ToolProcessor.GetInputFiles 的解析方式一致。
            string existing = string.IsNullOrWhiteSpace(textBox.Text) ? string.Empty : textBox.Text.TrimEnd() + Environment.NewLine;
            textBox.Text = existing + string.Join(Environment.NewLine, files);
            parameter.Value = textBox.Text;
        };
        clearButton.Click += (_, _) =>
        {
            textBox.Clear();
            parameter.Value = string.Empty;
        };
        buttons.Children.Add(addButton);
        buttons.Children.Add(clearButton);

        panel.Children.Add(textBox);
        panel.Children.Add(buttons);
        return panel;
    }

    private static FrameworkElement CreateDirectoryPicker(ToolParameterValue parameter)
    {
        var panel = new StackPanel
        {
            Width = 260,
            Orientation = Orientation.Vertical
        };

        var textBox = new TextBox
        {
            Height = 30,
            Text = parameter.Value
        };
        ApplyGeneratedTextBoxStyle(textBox);
        textBox.TextChanged += (_, _) => parameter.Value = textBox.Text;

        var button = new Button
        {
            Content = "浏览目录",
            Width = 104,
            Height = 30,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        ApplyGeneratedButtonStyle(button);
        button.Click += (_, _) =>
        {
            string? folder = PickFolder();
            if (!string.IsNullOrWhiteSpace(folder))
            {
                textBox.Text = folder;
                parameter.Value = folder;
            }
        };

        panel.Children.Add(textBox);
        panel.Children.Add(button);
        return panel;
    }

    private static TextBox CreateNumberBox(ToolParameterValue parameter)
    {
        var textBox = new TextBox
        {
            Width = 96,
            Height = 30,
            Text = string.IsNullOrWhiteSpace(parameter.Value) ? parameter.Definition.DefaultValue : parameter.Value
        };
        ApplyGeneratedTextBoxStyle(textBox);
        parameter.Value = textBox.Text;
        textBox.TextChanged += (_, _) => parameter.Value = textBox.Text;
        return textBox;
    }

    private static CheckBox CreateCheckBox(ToolParameterValue parameter)
    {
        string value = string.IsNullOrWhiteSpace(parameter.Value)
            ? parameter.Definition.DefaultValue
            : parameter.Value;
        var checkBox = new CheckBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = GeneratedTextBrush,
            IsChecked = IsParameterTrue(value)
        };
        parameter.Value = checkBox.IsChecked == true ? "true" : "false";
        checkBox.Checked += (_, _) => parameter.Value = "true";
        checkBox.Unchecked += (_, _) => parameter.Value = "false";
        return checkBox;
    }

    private static TextBlock CreateReadOnlyText(ToolParameterValue parameter)
    {
        return new TextBlock
        {
            Text = parameter.Definition.DefaultValue,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 260,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = GeneratedMutedBrush
        };
    }

    private static void ApplyGeneratedButtonStyle(Button button)
    {
        button.Background = GeneratedButtonBrush;
        button.Foreground = GeneratedTextBrush;
        button.BorderBrush = GeneratedBorderBrush;
        button.BorderThickness = new Thickness(1);
        button.Cursor = System.Windows.Input.Cursors.Hand;
        button.Padding = new Thickness(8, 0, 8, 0);
        button.MouseEnter += (_, _) => button.Background = GeneratedButtonHoverBrush;
        button.MouseLeave += (_, _) => button.Background = GeneratedButtonBrush;
    }

    private static void ApplyGeneratedTextBoxStyle(TextBox textBox)
    {
        textBox.Background = GeneratedInputBrush;
        textBox.Foreground = GeneratedTextBrush;
        textBox.CaretBrush = GeneratedTextBrush;
        textBox.SelectionBrush = GeneratedSelectionBrush;
        textBox.BorderBrush = GeneratedBorderBrush;
        textBox.BorderThickness = new Thickness(1);
        textBox.Padding = new Thickness(8, 4, 8, 4);
    }

    private static void ApplyGeneratedPasswordBoxStyle(PasswordBox passwordBox)
    {
        passwordBox.Background = GeneratedInputBrush;
        passwordBox.Foreground = GeneratedTextBrush;
        passwordBox.CaretBrush = GeneratedTextBrush;
        passwordBox.BorderBrush = GeneratedBorderBrush;
        passwordBox.BorderThickness = new Thickness(1);
        passwordBox.Padding = new Thickness(8, 4, 8, 4);
    }

    private static void ApplyGeneratedComboBoxStyle(ComboBox comboBox)
    {
        comboBox.Background = GeneratedInputBrush;
        comboBox.Foreground = GeneratedTextBrush;
        comboBox.BorderBrush = GeneratedBorderBrush;
        comboBox.BorderThickness = new Thickness(1);
        comboBox.Padding = new Thickness(6, 2, 6, 2);
    }

    private static bool IsParameterTrue(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1" || value == "是";
    }

    private static string? PickInputFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "所有文件 (*.*)|*.*",
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string? PickOutputFile()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "所有文件 (*.*)|*.*",
            AddExtension = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string[] PickMultipleFiles()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "常用文件|*.mp4;*.jpg;*.jpeg;*.png;*.webp;*.raw;*.enc|所有文件 (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileNames : [];
    }

    private static string? PickFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择输出目录"
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
