using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using 小工具集合.Services.ScreenPointer;
using 小工具集合.Views.FunLab;
using Forms = System.Windows.Forms;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace 小工具集合.Views.Generation;

public partial class ScreenPointerControl : UserControl, IInteractiveToolView, IDisposable
{
    private readonly ScreenPointerSettingsService settingsService = new();
    private readonly ScreenService screenService = new();
    private readonly System.Windows.Threading.DispatcherTimer saveTimer;
    private ScreenPointerOverlayWindow? overlayWindow;
    private CrosshairSettings settings;
    private bool isLoading = true;
    private CrosshairStyle activeStyle;
    private bool isDisposed;

    public ScreenPointerControl()
    {
        settings = settingsService.Load();
        NormalizeAllStyleColors();
        activeStyle = settings.Style;
        saveTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        saveTimer.Tick += (_, _) =>
        {
            saveTimer.Stop();
            settingsService.Save(settings);
        };

        InitializeComponent();
        Loaded += (_, _) =>
        {
            LoadUi();
            ApplyAndSaveSettings();
        };
        Unloaded += (_, _) => Deactivate();
    }

    public void Deactivate()
    {
        if (isDisposed)
        {
            return;
        }

        FlushSettingsSave();
        CloseOverlay();
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        Deactivate();
        isDisposed = true;
    }

    private void LoadUi()
    {
        isLoading = true;

        StyleComboBox.ItemsSource = new[]
        {
            new NamedStyle("十字", CrosshairStyle.Cross),
            new NamedStyle("圆点", CrosshairStyle.Dot),
            new NamedStyle("圆环", CrosshairStyle.Ring),
            new NamedStyle("T 型", CrosshairStyle.TShape),
            new NamedStyle("四角括号", CrosshairStyle.Corners),
            new NamedStyle("圆点 + 十字", CrosshairStyle.DotCross),
            new NamedStyle("图片", CrosshairStyle.Image)
        };
        StyleComboBox.DisplayMemberPath = nameof(NamedStyle.Name);
        StyleComboBox.SelectedValuePath = nameof(NamedStyle.Style);
        StyleComboBox.SelectedValue = settings.Style;

        LoadDisplays(settings.TargetScreenDeviceName);
        EnabledToggle.IsChecked = settings.IsEnabled;
        LockCenterCheckBox.IsChecked = settings.LockToCenter;
        RotationSlider.Value = settings.Rotation;
        OffsetXSlider.Value = settings.OffsetX;
        OffsetYSlider.Value = settings.OffsetY;
        LoadCurrentStyleIntoUi();

        isLoading = false;
        UpdateLabels();
    }

    private void LoadDisplays(string? selectedDeviceName)
    {
        IReadOnlyList<DisplayInfo> displays = screenService.GetDisplays();
        DisplayComboBox.ItemsSource = displays;
        DisplayComboBox.DisplayMemberPath = nameof(DisplayInfo.DisplayName);
        DisplayComboBox.SelectedValuePath = nameof(DisplayInfo.DeviceName);
        DisplayComboBox.SelectedValue = selectedDeviceName
            ?? displays.FirstOrDefault(display => display.IsPrimary)?.DeviceName
            ?? displays[0].DeviceName;
    }

    private void LoadCurrentStyleIntoUi()
    {
        bool wasLoading = isLoading;
        isLoading = true;
        CrosshairStyleSettings styleSettings = settings.CurrentStyleSettings;
        ColorTextBox.Text = styleSettings.ColorHex;
        OutlineColorTextBox.Text = styleSettings.OutlineColorHex;
        OpacitySlider.Value = styleSettings.Opacity;
        OutlineEnabledCheckBox.IsChecked = styleSettings.OutlineEnabled;
        OutlineThicknessSlider.Value = NormalizeOutlineThickness(styleSettings.OutlineThickness);
        ConfigureParamControlsForStyle(settings.Style);
        isLoading = wasLoading;
        UpdateLabels();
    }

    private void SettingsControlChanged(object sender, RoutedEventArgs e)
    {
        if (isLoading)
        {
            return;
        }

        ReadSettingsFromUi();
        ApplyAndSaveSettings();
    }

    private void StyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoading || StyleComboBox.SelectedValue is not CrosshairStyle style)
        {
            return;
        }

        ReadSettingsFromUi(activeStyle);
        settings.Style = style;
        activeStyle = style;
        LoadCurrentStyleIntoUi();
        ApplyAndSaveSettings();
    }

    private void LockCenterCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isLoading)
        {
            return;
        }

        if (LockCenterCheckBox.IsChecked == true)
        {
            OffsetXSlider.Value = 0;
            OffsetYSlider.Value = 0;
        }

        ReadSettingsFromUi();
        ApplyAndSaveSettings();
    }

    private void RefreshDisplaysButton_Click(object sender, RoutedEventArgs e)
    {
        string? selected = DisplayComboBox.SelectedValue as string;
        isLoading = true;
        LoadDisplays(selected);
        isLoading = false;
        ReadSettingsFromUi();
        ApplyAndSaveSettings();
        StatusText.Text = "显示器列表已刷新。";
    }

    private void ChooseColorButton_Click(object sender, RoutedEventArgs e)
    {
        Color current = settings.CurrentStyleSettings.Color;
        using var dialog = new Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B)
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            SetColorHex($"#{current.A:X2}{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}");
        }
    }

    private void ChooseOutlineColorButton_Click(object sender, RoutedEventArgs e)
    {
        Color current = settings.CurrentStyleSettings.OutlineColor;
        using var dialog = new Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B)
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            SetOutlineColorHex($"#{current.A:X2}{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}");
        }
    }

    private void PresetColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string colorHex)
        {
            SetColorHex(colorHex);
        }
    }

    private void UploadImageButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择准星图片",
            Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        settings.ImagePath = dialog.FileName;
        StyleComboBox.SelectedValue = CrosshairStyle.Image;
        ReadSettingsFromUi();
        ApplyAndSaveSettings();
    }

    private void SliderValueTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        CommitSliderValueTextBox(sender);
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void SliderValueTextBox_Commit(object sender, RoutedEventArgs e)
    {
        CommitSliderValueTextBox(sender);
    }

    private void SetColorHex(string colorHex)
    {
        if (!TryNormalizeColorHex(colorHex, out string normalized))
        {
            return;
        }

        settings.CurrentStyleSettings.ColorHex = normalized;
        isLoading = true;
        ColorTextBox.Text = normalized;
        isLoading = false;
        ApplyAndSaveSettings();
    }

    private void SetOutlineColorHex(string colorHex)
    {
        if (!TryNormalizeColorHex(colorHex, out string normalized))
        {
            return;
        }

        settings.CurrentStyleSettings.OutlineColorHex = normalized;
        isLoading = true;
        OutlineColorTextBox.Text = normalized;
        isLoading = false;
        ApplyAndSaveSettings();
    }

    private void ReadSettingsFromUi()
    {
        ReadSettingsFromUi(activeStyle);
    }

    private void ReadSettingsFromUi(CrosshairStyle styleToSave)
    {
        settings.IsEnabled = EnabledToggle.IsChecked == true;
        settings.Style = activeStyle;
        settings.TargetScreenDeviceName = DisplayComboBox.SelectedValue as string;

        CrosshairStyleSettings styleSettings = settings.GetStyleSettings(styleToSave);
        if (TryNormalizeColorHex(ColorTextBox.Text, out string normalizedColor))
        {
            styleSettings.ColorHex = normalizedColor;
        }

        if (TryNormalizeColorHex(OutlineColorTextBox.Text, out string normalizedOutlineColor))
        {
            styleSettings.OutlineColorHex = normalizedOutlineColor;
        }

        styleSettings.Opacity = Math.Round(OpacitySlider.Value, 2);
        styleSettings.OutlineEnabled = OutlineEnabledCheckBox.IsChecked == true;
        styleSettings.OutlineThickness = NormalizeOutlineThickness(OutlineThicknessSlider.Value);
        settings.Rotation = Math.Round(RotationSlider.Value);
        settings.LockToCenter = LockCenterCheckBox.IsChecked == true;
        settings.OffsetX = settings.LockToCenter ? 0 : Math.Round(OffsetXSlider.Value);
        settings.OffsetY = settings.LockToCenter ? 0 : Math.Round(OffsetYSlider.Value);
        ReadStyleParametersFromUi(styleToSave, styleSettings);
        ClampDependentStyleSliders(styleToSave, styleSettings);

        if (settings.LockToCenter)
        {
            OffsetXSlider.Value = 0;
            OffsetYSlider.Value = 0;
        }
    }

    private void ReadStyleParametersFromUi(CrosshairStyle style, CrosshairStyleSettings styleSettings)
    {
        switch (style)
        {
            case CrosshairStyle.Cross:
                styleSettings.Primary = Round(PrimaryParamSlider.Value);
                styleSettings.StrokeThickness = Round(ThicknessSlider.Value);
                styleSettings.Gap = Round(GapSlider.Value);
                break;
            case CrosshairStyle.Dot:
            case CrosshairStyle.Image:
                styleSettings.Primary = Round(PrimaryParamSlider.Value);
                break;
            case CrosshairStyle.Ring:
                styleSettings.StrokeThickness = Round(ThicknessSlider.Value);
                styleSettings.Gap = Round(GapSlider.Value);
                styleSettings.Primary = CalculateRingOuterDiameter(styleSettings);
                break;
            case CrosshairStyle.TShape:
            case CrosshairStyle.DotCross:
                styleSettings.Primary = Round(PrimaryParamSlider.Value);
                styleSettings.Secondary = Round(SecondaryParamSlider.Value);
                styleSettings.StrokeThickness = Round(ThicknessSlider.Value);
                styleSettings.Gap = Round(GapSlider.Value);
                break;
            case CrosshairStyle.Corners:
                styleSettings.Primary = Round(PrimaryParamSlider.Value);
                styleSettings.Secondary = Math.Min(Round(SecondaryParamSlider.Value), styleSettings.Primary / 2);
                styleSettings.StrokeThickness = Round(ThicknessSlider.Value);
                styleSettings.Gap = Math.Min(Round(GapSlider.Value), styleSettings.Primary / 2 - 2);
                break;
        }
    }

    private void ClampDependentStyleSliders(CrosshairStyle style, CrosshairStyleSettings styleSettings)
    {
        bool wasLoading = isLoading;
        isLoading = true;

        if (style == CrosshairStyle.Ring)
        {
            styleSettings.Gap = Math.Max(0, styleSettings.Gap);
            styleSettings.Primary = CalculateRingOuterDiameter(styleSettings);
            PrimaryParamSlider.Value = styleSettings.Primary;
            GapSlider.Value = styleSettings.Gap;
        }
        else if (style == CrosshairStyle.Corners)
        {
            double maxArm = Math.Max(2, styleSettings.Primary / 2);
            double maxGap = Math.Max(0, styleSettings.Primary / 2 - 2);
            SecondaryParamSlider.Maximum = maxArm;
            GapSlider.Maximum = maxGap;
            styleSettings.Secondary = Math.Min(styleSettings.Secondary, maxArm);
            styleSettings.Gap = Math.Min(styleSettings.Gap, maxGap);
            SecondaryParamSlider.Value = styleSettings.Secondary;
            GapSlider.Value = styleSettings.Gap;
        }

        isLoading = wasLoading;
    }

    private void ApplyAndSaveSettings()
    {
        UpdateLabels();
        if (settings.IsEnabled)
        {
            EnsureOverlayWindow();
        }

        overlayWindow?.ApplySettings(settings);
        ScheduleSettingsSave();
        StatusText.Text = settings.IsEnabled ? "准星已应用到目标屏幕。" : "准星已关闭。";
    }

    private void EnsureOverlayWindow()
    {
        overlayWindow ??= new ScreenPointerOverlayWindow(screenService);
    }

    private void CloseOverlay()
    {
        overlayWindow?.Close();
        overlayWindow = null;
    }

    private void ScheduleSettingsSave()
    {
        saveTimer.Stop();
        saveTimer.Start();
    }

    private void FlushSettingsSave()
    {
        saveTimer.Stop();
        settingsService.Save(settings);
    }

    private void ConfigureParamControlsForStyle(CrosshairStyle style)
    {
        bool wasLoading = isLoading;
        isLoading = true;
        CrosshairStyleSettings styleSettings = settings.GetStyleSettings(style);

        switch (style)
        {
            case CrosshairStyle.Cross:
                ConfigureSlider(PrimaryParamSlider, 4, 120, styleSettings.Primary, true);
                ConfigureSlider(SecondaryParamSlider, 1, 40, 1, false);
                ConfigureSlider(ThicknessSlider, 1, 16, styleSettings.StrokeThickness, true);
                ConfigureSlider(GapSlider, 0, 60, styleSettings.Gap, true);
                break;
            case CrosshairStyle.Dot:
                ConfigureSlider(PrimaryParamSlider, 2, 60, styleSettings.Primary, true);
                ConfigureSlider(SecondaryParamSlider, 1, 40, 1, false);
                ConfigureSlider(ThicknessSlider, 1, 16, 1, false);
                ConfigureSlider(GapSlider, 0, 60, 0, false);
                break;
            case CrosshairStyle.Ring:
                styleSettings.Primary = CalculateRingOuterDiameter(styleSettings);
                ConfigureSlider(PrimaryParamSlider, 8, 220, styleSettings.Primary, false);
                ConfigureSlider(SecondaryParamSlider, 1, 40, 1, false);
                ConfigureSlider(ThicknessSlider, 1, 24, styleSettings.StrokeThickness, true);
                ConfigureSlider(GapSlider, 0, 180, styleSettings.Gap, true);
                break;
            case CrosshairStyle.TShape:
                ConfigureSlider(PrimaryParamSlider, 8, 180, styleSettings.Primary, true);
                ConfigureSlider(SecondaryParamSlider, 4, 120, styleSettings.Secondary, true);
                ConfigureSlider(ThicknessSlider, 1, 16, styleSettings.StrokeThickness, true);
                ConfigureSlider(GapSlider, 0, 60, styleSettings.Gap, true);
                break;
            case CrosshairStyle.Corners:
                ConfigureSlider(PrimaryParamSlider, 24, 180, styleSettings.Primary, true);
                ConfigureSlider(SecondaryParamSlider, 4, 80, styleSettings.Secondary, true);
                ConfigureSlider(ThicknessSlider, 1, 16, styleSettings.StrokeThickness, true);
                ConfigureSlider(GapSlider, 0, Math.Max(0, styleSettings.Primary / 2 - 2), styleSettings.Gap, true);
                break;
            case CrosshairStyle.Image:
                ConfigureSlider(PrimaryParamSlider, 16, 240, styleSettings.Primary, true);
                ConfigureSlider(SecondaryParamSlider, 1, 40, 1, false);
                ConfigureSlider(ThicknessSlider, 1, 16, 1, false);
                ConfigureSlider(GapSlider, 0, 60, 0, false);
                break;
            case CrosshairStyle.DotCross:
                ConfigureSlider(PrimaryParamSlider, 4, 120, styleSettings.Primary, true);
                ConfigureSlider(SecondaryParamSlider, 2, 40, styleSettings.Secondary, true);
                ConfigureSlider(ThicknessSlider, 1, 16, styleSettings.StrokeThickness, true);
                ConfigureSlider(GapSlider, 0, 60, styleSettings.Gap, true);
                break;
        }

        isLoading = wasLoading;
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        UpdateColorPreview();
        OpacityLabel.Text = "透明度";
        OutlineThicknessLabel.Text = "边框厚度";
        bool outlineAvailable = settings.Style != CrosshairStyle.Image && settings.Style != CrosshairStyle.Ring;
        OutlineEnabledCheckBox.IsEnabled = outlineAvailable;
        OutlineThicknessSlider.IsEnabled = outlineAvailable && OutlineEnabledCheckBox.IsChecked == true;
        OutlineThicknessValueTextBox.IsEnabled = OutlineThicknessSlider.IsEnabled;
        RotationLabel.Text = "旋转";
        OffsetXLabel.Text = "水平偏移";
        OffsetYLabel.Text = "垂直偏移";
        OffsetXSlider.IsEnabled = LockCenterCheckBox.IsChecked != true;
        OffsetYSlider.IsEnabled = LockCenterCheckBox.IsChecked != true;
        OffsetXValueTextBox.IsEnabled = OffsetXSlider.IsEnabled;
        OffsetYValueTextBox.IsEnabled = OffsetYSlider.IsEnabled;
        ImagePathTextBlock.Text = string.IsNullOrWhiteSpace(settings.ImagePath)
            ? "尚未选择图片准星"
            : settings.ImagePath;
        UpdateStyleParameterLabels();
        UpdateSliderValueTextBoxes();
    }

    private void UpdateStyleParameterLabels()
    {
        switch (settings.Style)
        {
            case CrosshairStyle.Cross:
                PrimaryParamLabel.Text = "线长";
                SecondaryParamLabel.Text = "辅助参数";
                ThicknessLabel.Text = "线宽";
                GapLabel.Text = "中心间距";
                break;
            case CrosshairStyle.Dot:
                PrimaryParamLabel.Text = "圆点直径";
                SecondaryParamLabel.Text = "辅助参数";
                ThicknessLabel.Text = "线宽";
                GapLabel.Text = "中心间距";
                break;
            case CrosshairStyle.Ring:
                PrimaryParamLabel.Text = "外径";
                SecondaryParamLabel.Text = "辅助参数";
                ThicknessLabel.Text = "线宽";
                GapLabel.Text = "内孔/留白";
                break;
            case CrosshairStyle.TShape:
                PrimaryParamLabel.Text = "横线长度";
                SecondaryParamLabel.Text = "竖线长度";
                ThicknessLabel.Text = "线宽";
                GapLabel.Text = "中心间距";
                break;
            case CrosshairStyle.Corners:
                PrimaryParamLabel.Text = "外框尺寸";
                SecondaryParamLabel.Text = "角臂长度";
                ThicknessLabel.Text = "线宽";
                GapLabel.Text = "内侧留白";
                break;
            case CrosshairStyle.Image:
                PrimaryParamLabel.Text = "图片尺寸";
                SecondaryParamLabel.Text = "辅助参数";
                ThicknessLabel.Text = "线宽";
                GapLabel.Text = "中心间距";
                break;
            case CrosshairStyle.DotCross:
                PrimaryParamLabel.Text = "线长";
                SecondaryParamLabel.Text = "圆点直径";
                ThicknessLabel.Text = "线宽";
                GapLabel.Text = "中心间距";
                break;
        }

        SecondaryParamValueTextBox.IsEnabled = SecondaryParamSlider.IsEnabled;
        ThicknessValueTextBox.IsEnabled = ThicknessSlider.IsEnabled;
        GapValueTextBox.IsEnabled = GapSlider.IsEnabled;
        bool hidePrimaryParam = settings.Style == CrosshairStyle.Ring;
        PrimaryParamHeader.Visibility = hidePrimaryParam ? Visibility.Collapsed : Visibility.Visible;
        PrimaryParamSlider.Visibility = hidePrimaryParam ? Visibility.Collapsed : Visibility.Visible;
        PrimaryParamValueTextBox.IsEnabled = PrimaryParamSlider.IsEnabled && !hidePrimaryParam;
    }

    private void UpdateColorPreview()
    {
        CrosshairStyleSettings styleSettings = settings.CurrentStyleSettings;
        ColorPreviewBorder.Background = new SolidColorBrush(styleSettings.Color);
        ColorTextBox.BorderBrush = TryNormalizeColorHex(ColorTextBox.Text, out _) ? Brushes.Gray : Brushes.IndianRed;
        OutlineColorPreviewBorder.Background = new SolidColorBrush(styleSettings.OutlineColor);
        OutlineColorTextBox.BorderBrush = TryNormalizeColorHex(OutlineColorTextBox.Text, out _) ? Brushes.Gray : Brushes.IndianRed;
    }

    private static void ConfigureSlider(Slider slider, double minimum, double maximum, double value, bool isEnabled)
    {
        slider.Minimum = minimum;
        slider.Maximum = Math.Max(minimum, maximum);
        slider.Value = Math.Clamp(value, slider.Minimum, slider.Maximum);
        slider.TickFrequency = slider.TickFrequency <= 0 ? 1 : slider.TickFrequency;
        slider.IsEnabled = isEnabled;
    }

    private void UpdateSliderValueTextBoxes()
    {
        SetTextBoxValue(OpacityValueTextBox, OpacitySlider.Value, "0.##");
        SetTextBoxValue(OutlineThicknessValueTextBox, OutlineThicknessSlider.Value, "0");
        SetTextBoxValue(PrimaryParamValueTextBox, PrimaryParamSlider.Value, "0");
        SetTextBoxValue(SecondaryParamValueTextBox, SecondaryParamSlider.Value, "0");
        SetTextBoxValue(ThicknessValueTextBox, ThicknessSlider.Value, "0");
        SetTextBoxValue(GapValueTextBox, GapSlider.Value, "0");
        SetTextBoxValue(RotationValueTextBox, RotationSlider.Value, "0");
        SetTextBoxValue(OffsetXValueTextBox, OffsetXSlider.Value, "0");
        SetTextBoxValue(OffsetYValueTextBox, OffsetYSlider.Value, "0");
    }

    private void CommitSliderValueTextBox(object sender)
    {
        if (sender is not WpfTextBox textBox || !TryGetSliderForValueTextBox(textBox, out Slider slider))
        {
            return;
        }

        if (!double.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value)
            && !double.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            UpdateSliderValueTextBoxes();
            return;
        }

        value = Math.Clamp(value, slider.Minimum, slider.Maximum);
        if (slider == OutlineThicknessSlider)
        {
            value = NormalizeOutlineThickness(value);
        }
        else if (slider != OpacitySlider)
        {
            value = Math.Round(value);
        }

        slider.Value = value;
        ReadSettingsFromUi();
        ApplyAndSaveSettings();
    }

    private bool TryGetSliderForValueTextBox(WpfTextBox textBox, out Slider slider)
    {
        slider = textBox switch
        {
            _ when textBox == OpacityValueTextBox => OpacitySlider,
            _ when textBox == OutlineThicknessValueTextBox => OutlineThicknessSlider,
            _ when textBox == PrimaryParamValueTextBox => PrimaryParamSlider,
            _ when textBox == SecondaryParamValueTextBox => SecondaryParamSlider,
            _ when textBox == ThicknessValueTextBox => ThicknessSlider,
            _ when textBox == GapValueTextBox => GapSlider,
            _ when textBox == RotationValueTextBox => RotationSlider,
            _ when textBox == OffsetXValueTextBox => OffsetXSlider,
            _ when textBox == OffsetYValueTextBox => OffsetYSlider,
            _ => null!
        };
        return slider is not null;
    }

    private static void SetTextBoxValue(WpfTextBox textBox, double value, string format)
    {
        if (!textBox.IsKeyboardFocusWithin)
        {
            textBox.Text = value.ToString(format, CultureInfo.CurrentCulture);
        }
    }

    private void NormalizeAllStyleColors()
    {
        foreach (CrosshairStyle style in Enum.GetValues<CrosshairStyle>())
        {
            CrosshairStyleSettings styleSettings = settings.GetStyleSettings(style);
            styleSettings.ColorHex = NormalizeColorHexOrDefault(styleSettings.ColorHex);
            styleSettings.OutlineColorHex = NormalizeColorHexOrDefault(styleSettings.OutlineColorHex, "#FF000000");
            styleSettings.OutlineThickness = NormalizeOutlineThickness(styleSettings.OutlineThickness);
        }
    }

    private static double Round(double value)
    {
        return Math.Round(value);
    }

    private static double NormalizeOutlineThickness(double value)
    {
        int even = (int)Math.Round(value / 2, MidpointRounding.AwayFromZero) * 2;
        return Math.Clamp(even, 2, 12);
    }

    private static string NormalizeColorHexOrDefault(string? colorHex, string fallback = "#FF00E5FF")
    {
        return TryNormalizeColorHex(colorHex, out string normalized) ? normalized : fallback;
    }

    private static bool TryNormalizeColorHex(string? colorHex, out string normalized)
    {
        normalized = "#FF00E5FF";
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            return false;
        }

        string value = colorHex.Trim();
        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        if (value.Length != 6 && value.Length != 8)
        {
            return false;
        }

        if (!value.All(Uri.IsHexDigit))
        {
            return false;
        }

        normalized = value.Length == 6 ? $"#FF{value.ToUpperInvariant()}" : $"#{value.ToUpperInvariant()}";
        return true;
    }

    private static double CalculateRingOuterDiameter(CrosshairStyleSettings styleSettings)
    {
        return Math.Max(2, styleSettings.Gap + styleSettings.StrokeThickness * 2);
    }

    private sealed record NamedStyle(string Name, CrosshairStyle Style)
    {
        public override string ToString()
        {
            return Name;
        }
    }
}
