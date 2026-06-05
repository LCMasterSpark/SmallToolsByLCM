// 文件作用：把 ToolParameterValue 渲染为 WPF 动态参数控件，隔离主窗口 code-behind 的控件生成逻辑。
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using 小工具集合.Models;

namespace 小工具集合.Views.Controls;

public sealed class DynamicParameterControlBuilder
{
    private readonly IToolParameterDialogService dialogService;
    private static readonly Brush LabelBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200));
    private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(241, 241, 241));
    private static readonly Brush MutedBrush = new SolidColorBrush(Color.FromRgb(150, 150, 150));
    private static readonly Brush InputBrush = new SolidColorBrush(Color.FromRgb(31, 31, 31));
    private static readonly Brush ButtonBrush = new SolidColorBrush(Color.FromRgb(45, 45, 48));
    private static readonly Brush ButtonHoverBrush = new SolidColorBrush(Color.FromRgb(62, 62, 66));
    private static readonly Brush BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70));
    private static readonly Brush SelectionBrush = new SolidColorBrush(Color.FromRgb(75, 45, 94));

    public DynamicParameterControlBuilder(IToolParameterDialogService dialogService)
    {
        this.dialogService = dialogService;
    }

    public void Build(Panel targetPanel, IEnumerable<ToolParameterValue> parameters)
    {
        targetPanel.Children.Clear();

        foreach (ToolParameterValue parameter in parameters)
        {
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
                Foreground = LabelBrush
            });

            block.Children.Add(CreateInput(parameter));
            targetPanel.Children.Add(block);
        }
    }

    private FrameworkElement CreateInput(ToolParameterValue parameter)
    {
        return parameter.Definition.Kind switch
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
    }

    private static TextBox CreateTextBox(ToolParameterValue parameter)
    {
        var textBox = new TextBox { Width = 260, Height = 30, Text = parameter.Value };
        ApplyTextBoxStyle(textBox);
        textBox.TextChanged += (_, _) => parameter.Value = textBox.Text;
        return textBox;
    }

    private static PasswordBox CreatePasswordBox(ToolParameterValue parameter)
    {
        var passwordBox = new PasswordBox { Width = 260, Height = 30 };
        ApplyPasswordBoxStyle(passwordBox);
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
        ApplyTextBoxStyle(textBox);
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
        ApplyComboBoxStyle(comboBox);
        comboBox.SelectionChanged += (_, _) => parameter.Value = comboBox.SelectedItem?.ToString() ?? string.Empty;
        parameter.Value = comboBox.SelectedItem?.ToString() ?? parameter.Value;
        return comboBox;
    }

    private FrameworkElement CreateFilePicker(ToolParameterValue parameter, bool openFile)
    {
        var panel = new StackPanel { Width = 260, Orientation = Orientation.Vertical };
        var textBox = new TextBox { Height = 30, Text = parameter.Value };
        ApplyTextBoxStyle(textBox);
        textBox.TextChanged += (_, _) => parameter.Value = textBox.Text;

        var button = new Button
        {
            Content = "浏览",
            Width = 92,
            Height = 30,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        ApplyButtonStyle(button);
        button.Click += (_, _) =>
        {
            string? fileName = openFile ? dialogService.PickInputFile() : dialogService.PickOutputFile();
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

    private FrameworkElement CreateFileListPicker(ToolParameterValue parameter)
    {
        var panel = new StackPanel { Width = 260, Orientation = Orientation.Vertical };
        var textBox = new TextBox
        {
            Height = 92,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = parameter.Value
        };
        ApplyTextBoxStyle(textBox);
        textBox.TextChanged += (_, _) => parameter.Value = textBox.Text;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        var addButton = new Button { Content = "添加文件", Width = 110, Height = 34, Margin = new Thickness(0, 0, 8, 0) };
        var clearButton = new Button { Content = "清空", Width = 76, Height = 34 };
        ApplyButtonStyle(addButton);
        ApplyButtonStyle(clearButton);
        addButton.Click += (_, _) =>
        {
            string[] files = dialogService.PickMultipleFiles();
            if (files.Length == 0)
            {
                return;
            }

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

    private FrameworkElement CreateDirectoryPicker(ToolParameterValue parameter)
    {
        var panel = new StackPanel { Width = 260, Orientation = Orientation.Vertical };
        var textBox = new TextBox { Height = 30, Text = parameter.Value };
        ApplyTextBoxStyle(textBox);
        textBox.TextChanged += (_, _) => parameter.Value = textBox.Text;

        var button = new Button
        {
            Content = "浏览目录",
            Width = 104,
            Height = 30,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        ApplyButtonStyle(button);
        button.Click += (_, _) =>
        {
            string? folder = dialogService.PickFolder();
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
        ApplyTextBoxStyle(textBox);
        parameter.Value = textBox.Text;
        textBox.TextChanged += (_, _) => parameter.Value = textBox.Text;
        return textBox;
    }

    private static CheckBox CreateCheckBox(ToolParameterValue parameter)
    {
        string value = string.IsNullOrWhiteSpace(parameter.Value) ? parameter.Definition.DefaultValue : parameter.Value;
        var checkBox = new CheckBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = TextBrush,
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
            Foreground = MutedBrush
        };
    }

    private static void ApplyButtonStyle(Button button)
    {
        button.Background = ButtonBrush;
        button.Foreground = TextBrush;
        button.BorderBrush = BorderBrush;
        button.BorderThickness = new Thickness(1);
        button.Cursor = System.Windows.Input.Cursors.Hand;
        button.Padding = new Thickness(8, 0, 8, 0);
        button.MouseEnter += (_, _) => button.Background = ButtonHoverBrush;
        button.MouseLeave += (_, _) => button.Background = ButtonBrush;
        ApplyButtonMotion(button);
    }

    private static void ApplyButtonMotion(Button button)
    {
        var scale = new ScaleTransform(1, 1);
        button.RenderTransformOrigin = new Point(0.5, 0.5);
        button.RenderTransform = scale;
        button.PreviewMouseDown += (_, _) => AnimateScale(scale, 0.97, 55);
        button.PreviewMouseUp += (_, _) => AnimateScale(scale, 1.0, 120);
        button.MouseLeave += (_, _) => AnimateScale(scale, 1.0, 100);
    }

    private static void AnimateScale(ScaleTransform scale, double value, int milliseconds)
    {
        var animation = new DoubleAnimation(value, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    private static void ApplyTextBoxStyle(TextBox textBox)
    {
        textBox.Background = InputBrush;
        textBox.Foreground = TextBrush;
        textBox.CaretBrush = TextBrush;
        textBox.SelectionBrush = SelectionBrush;
        textBox.BorderBrush = BorderBrush;
        textBox.BorderThickness = new Thickness(1);
        textBox.Padding = new Thickness(8, 4, 8, 4);
    }

    private static void ApplyPasswordBoxStyle(PasswordBox passwordBox)
    {
        passwordBox.Background = InputBrush;
        passwordBox.Foreground = TextBrush;
        passwordBox.CaretBrush = TextBrush;
        passwordBox.BorderBrush = BorderBrush;
        passwordBox.BorderThickness = new Thickness(1);
        passwordBox.Padding = new Thickness(8, 4, 8, 4);
    }

    private static void ApplyComboBoxStyle(ComboBox comboBox)
    {
        comboBox.Background = InputBrush;
        comboBox.Foreground = TextBrush;
        comboBox.BorderBrush = BorderBrush;
        comboBox.BorderThickness = new Thickness(1);
        comboBox.Padding = new Thickness(6, 2, 6, 2);
    }

    private static bool IsParameterTrue(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1" || value == "是";
    }
}
