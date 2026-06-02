using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;
using Microsoft.Win32;
using 小工具集合.Models;
using 小工具集合.ViewModels;

namespace 小工具集合;

/// <summary>
/// 主窗口代码隐藏。布局和绑定放在 XAML 中，
/// 这里负责动态参数控件以及不适合写在 XAML 里的对话框交互。
/// </summary>
public partial class MainWindow : MetroWindow
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
        _viewModel.Parameters.CollectionChanged += Parameters_CollectionChanged;
        BuildParameterControls();
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
    }

    private void GroupButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ToolGroup group })
        {
            _viewModel.SelectedGroup = group;
        }
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
                Margin = new Thickness(0, 0, 18, 12)
            };

            block.Children.Add(new TextBlock
            {
                Text = parameter.Definition.Name,
                Margin = new Thickness(0, 0, 0, 6),
                FontSize = 12.5,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(75, 85, 99))
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
            Width = 240,
            Height = 34,
            Text = parameter.Value
        };
        textBox.TextChanged += (_, _) => parameter.Value = textBox.Text;
        return textBox;
    }

    private static PasswordBox CreatePasswordBox(ToolParameterValue parameter)
    {
        var passwordBox = new PasswordBox
        {
            Width = 240,
            Height = 34
        };
        passwordBox.PasswordChanged += (_, _) => parameter.Value = passwordBox.Password;
        return passwordBox;
    }

    private static TextBox CreateMultilineBox(ToolParameterValue parameter)
    {
        var textBox = new TextBox
        {
            Width = 420,
            Height = 88,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = parameter.Value
        };
        textBox.TextChanged += (_, _) => parameter.Value = textBox.Text;
        return textBox;
    }

    private static ComboBox CreateComboBox(ToolParameterValue parameter)
    {
        var comboBox = new ComboBox
        {
            Width = 180,
            Height = 34,
            ItemsSource = parameter.Definition.Options,
            SelectedItem = string.IsNullOrWhiteSpace(parameter.Value) ? parameter.Definition.DefaultValue : parameter.Value
        };
        comboBox.SelectionChanged += (_, _) => parameter.Value = comboBox.SelectedItem?.ToString() ?? string.Empty;
        parameter.Value = comboBox.SelectedItem?.ToString() ?? parameter.Value;
        return comboBox;
    }

    private static FrameworkElement CreateFilePicker(ToolParameterValue parameter, bool openFile)
    {
        var panel = new StackPanel
        {
            Width = 520,
            Orientation = Orientation.Vertical
        };

        var textBox = new TextBox
        {
            Height = 32,
            Text = parameter.Value
        };
        textBox.TextChanged += (_, _) => parameter.Value = textBox.Text;

        var button = new Button
        {
            Content = "浏览",
            Width = 120,
            Height = 34,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
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
            Width = 520,
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
        textBox.TextChanged += (_, _) => parameter.Value = textBox.Text;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 8, 0, 0)
        };
        var addButton = new Button { Content = "添加文件", Width = 110, Height = 34, Margin = new Thickness(0, 0, 8, 0) };
        var clearButton = new Button { Content = "清空", Width = 76, Height = 34 };
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
            Width = 520,
            Orientation = Orientation.Vertical
        };

        var textBox = new TextBox
        {
            Height = 32,
            Text = parameter.Value
        };
        textBox.TextChanged += (_, _) => parameter.Value = textBox.Text;

        var button = new Button
        {
            Content = "浏览目录",
            Width = 120,
            Height = 34,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
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
            Width = 92,
            Height = 34,
            Text = string.IsNullOrWhiteSpace(parameter.Value) ? parameter.Definition.DefaultValue : parameter.Value
        };
        parameter.Value = textBox.Text;
        textBox.TextChanged += (_, _) => parameter.Value = textBox.Text;
        return textBox;
    }

    private static CheckBox CreateCheckBox(ToolParameterValue parameter)
    {
        var checkBox = new CheckBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            IsChecked = parameter.Definition.DefaultValue.Equals("true", StringComparison.OrdinalIgnoreCase)
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
            MaxWidth = 420,
            VerticalAlignment = VerticalAlignment.Center
        };
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
