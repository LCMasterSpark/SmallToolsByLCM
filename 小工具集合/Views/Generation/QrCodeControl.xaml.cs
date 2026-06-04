// 文件作用：实现 QR Code 生成、预览、复制和 PNG 保存的交互逻辑。
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using QRCoder;
using 小工具集合.Views.FunLab;

namespace 小工具集合.Views.Generation;

public partial class QrCodeControl : UserControl, IInteractiveToolView, IDisposable
{
    private const string DefaultRepositoryUrl = "https://github.com/LCMasterSpark/SmallToolsByLCM";

    private static readonly IReadOnlyList<QrContentTemplate> ContentTemplates =
    [
        new(
            "text",
            "普通文本",
            "最通用，微信和其他扫码器通常都会按文字展示。",
            "你好，LCMasterSpark！\nLCM的工具箱 QR Code 测试"),
        new(
            "url",
            "网址",
            "微信最稳识别链接；打开页面之前一般会先展示网址。",
            DefaultRepositoryUrl),
        new(
            "wifi",
            "Wi-Fi",
            "系统相机和专门扫码器更懂 Wi-Fi；微信可能按纯文本展示。",
            "WIFI:T:WPA;S:LCM-WiFi;P:12345678;;"),
        new(
            "vcard",
            "联系人 vCard",
            "通讯录/专业扫码器通常能识别联系人；微信可能按纯文本展示。",
            "BEGIN:VCARD\nVERSION:3.0\nFN:LCMasterSpark\nORG:LCM的工具箱\nURL:https://github.com/LCMasterSpark\nEND:VCARD"),
        new(
            "email",
            "邮件",
            "部分扫码器会打开邮件草稿；微信大概率当作文本。",
            "mailto:test@example.com?subject=QR%20Code%20Test&body=Hello%20LCM"),
        new(
            "phone",
            "电话",
            "系统相机通常能识别拨号；微信可能只显示文本。",
            "tel:+8613800138000"),
        new(
            "sms",
            "短信",
            "部分扫码器会打开短信草稿；微信可能只显示文本。",
            "SMSTO:+8613800138000:你好，LCMasterSpark")
    ];

    private byte[]? lastPngBytes;
    private string lastContent = string.Empty;
    private bool isDisposed;

    public QrCodeControl()
    {
        InitializeComponent();
        ContentTemplateComboBox.ItemsSource = ContentTemplates;
        ContentTemplateComboBox.DisplayMemberPath = nameof(QrContentTemplate.Name);
        ContentTemplateComboBox.SelectedIndex = 1;
        ContentTemplateComboBox.SelectionChanged += ContentTemplateComboBox_SelectionChanged;
        ApplySelectedTemplate(generate: false);
        UpdatePixelsLabel();
        Loaded += (_, _) =>
        {
            GenerateQrCode(showEmptyError: false);
            InputTextBox.Focus();
        };
        Unloaded += (_, _) => Deactivate();
    }

    public void Deactivate()
    {
        lastPngBytes = null;
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

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        GenerateQrCode(showEmptyError: true);
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureQrCodeReady())
        {
            return;
        }

        if (QrPreviewImage.Source is not BitmapSource bitmapSource)
        {
            SetStatus("没有可复制的二维码图片。", isError: true);
            return;
        }

        try
        {
            Clipboard.SetImage(bitmapSource);
            SetStatus("二维码图片已复制到剪贴板。");
        }
        catch (Exception ex)
        {
            SetStatus($"复制失败：{ex.Message}", isError: true);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureQrCodeReady() || lastPngBytes is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "保存 QR Code",
            FileName = "qrcode.png",
            DefaultExt = ".png",
            Filter = "PNG 图片 (*.png)|*.png|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            File.WriteAllBytes(dialog.FileName, lastPngBytes);
            SetStatus($"已保存：{dialog.FileName}");
        }
        catch (Exception ex)
        {
            SetStatus($"保存失败：{ex.Message}", isError: true);
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        InputTextBox.Clear();
        ClearPreview();
        SetStatus("已清空，准备生成新的二维码。");
    }

    private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(InputTextBox.Text))
        {
            ClearPreview();
            SetStatus("准备生成二维码。");
        }
    }

    private void OptionChanged(object sender, RoutedEventArgs e)
    {
        RegenerateIfPreviewExists();
    }

    private void PixelsPerModuleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdatePixelsLabel();
        RegenerateIfPreviewExists();
    }

    private void ColorTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        RegenerateIfPreviewExists();
    }

    private void ContentTemplateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplySelectedTemplate(generate: true);
    }

    private void ApplySelectedTemplate(bool generate)
    {
        if (ContentTemplateComboBox.SelectedItem is not QrContentTemplate template)
        {
            return;
        }

        InputTextBox.Text = template.Sample;
        TemplateHintTextBlock.Text = template.Hint;

        if (generate)
        {
            GenerateQrCode(showEmptyError: false);
        }
    }

    private bool EnsureQrCodeReady()
    {
        if (lastPngBytes is not null && lastContent == InputTextBox.Text)
        {
            return true;
        }

        return GenerateQrCode(showEmptyError: true);
    }

    private bool GenerateQrCode(bool showEmptyError)
    {
        string content = InputTextBox.Text;
        if (string.IsNullOrWhiteSpace(content))
        {
            ClearPreview();
            if (showEmptyError)
            {
                SetStatus("先输入一点内容，空二维码很哲学但扫不出来。", isError: true);
            }

            return false;
        }

        try
        {
            byte[] darkColor = ParseHexColor(DarkColorTextBox.Text, "深色");
            byte[] lightColor = ParseHexColor(LightColorTextBox.Text, "浅色");
            int pixelsPerModule = Math.Clamp((int)Math.Round(PixelsPerModuleSlider.Value), 4, 24);
            bool drawQuietZones = QuietZoneCheckBox.IsChecked == true;

            using QRCodeGenerator generator = new();
            using QRCodeData data = generator.CreateQrCode(content, GetErrorCorrectionLevel());
            PngByteQRCode qrCode = new(data);
            byte[] pngBytes = qrCode.GetGraphic(pixelsPerModule, darkColor, lightColor, drawQuietZones);

            lastPngBytes = pngBytes;
            lastContent = content;
            QrPreviewImage.Source = CreateBitmapImage(pngBytes);
            EmptyPreviewTextBlock.Visibility = Visibility.Collapsed;
            PreviewInfoTextBlock.Text = $"内容长度：{content.Length} 字符 / 像素倍率：{pixelsPerModule} / 纠错：{GetErrorCorrectionName()}";
            SetStatus("二维码已生成。");
            return true;
        }
        catch (Exception ex)
        {
            lastPngBytes = null;
            QrPreviewImage.Source = null;
            EmptyPreviewTextBlock.Visibility = Visibility.Visible;
            PreviewInfoTextBlock.Text = string.Empty;
            SetStatus($"生成失败：{TrimMessage(ex.Message)}", isError: true);
            return false;
        }
    }

    private void RegenerateIfPreviewExists()
    {
        if (lastPngBytes is not null)
        {
            GenerateQrCode(showEmptyError: false);
        }
    }

    private void ClearPreview()
    {
        lastPngBytes = null;
        lastContent = string.Empty;
        QrPreviewImage.Source = null;
        EmptyPreviewTextBlock.Visibility = Visibility.Visible;
        PreviewInfoTextBlock.Text = string.Empty;
    }

    private void UpdatePixelsLabel()
    {
        if (PixelsPerModuleTextBlock is null || PixelsPerModuleSlider is null)
        {
            return;
        }

        PixelsPerModuleTextBlock.Text = $"{(int)Math.Round(PixelsPerModuleSlider.Value)} px/module";
    }

    private QRCodeGenerator.ECCLevel GetErrorCorrectionLevel()
    {
        return ErrorCorrectionComboBox.SelectedIndex switch
        {
            0 => QRCodeGenerator.ECCLevel.L,
            2 => QRCodeGenerator.ECCLevel.Q,
            3 => QRCodeGenerator.ECCLevel.H,
            _ => QRCodeGenerator.ECCLevel.M
        };
    }

    private string GetErrorCorrectionName()
    {
        return ErrorCorrectionComboBox.SelectedIndex switch
        {
            0 => "L",
            2 => "Q",
            3 => "H",
            _ => "M"
        };
    }

    private static BitmapImage CreateBitmapImage(byte[] pngBytes)
    {
        using MemoryStream stream = new(pngBytes);
        BitmapImage image = new();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static byte[] ParseHexColor(string text, string fieldName)
    {
        string value = text.Trim().TrimStart('#');
        if (value.Length != 6 && value.Length != 8)
        {
            throw new FormatException($"{fieldName}颜色请使用 #RRGGBB 或 #RRGGBBAA。");
        }

        if (!uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            throw new FormatException($"{fieldName}颜色不是有效十六进制。");
        }

        byte red = byte.Parse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte green = byte.Parse(value.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte blue = byte.Parse(value.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte alpha = value.Length == 8
            ? byte.Parse(value.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : byte.MaxValue;

        return [red, green, blue, alpha];
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusTextBlock.Text = message;
        StatusTextBlock.Foreground = isError
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 99, 99))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(150, 150, 150));
    }

    private static string TrimMessage(string message)
    {
        return message.Length <= 120 ? message : string.Concat(message.AsSpan(0, 120), "...");
    }

    private sealed record QrContentTemplate(string Id, string Name, string Hint, string Sample);
}
