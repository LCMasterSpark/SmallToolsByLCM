// 文件作用：实现截图 OCR 的图片来源获取、OCR.space 调用和本地回退提示。
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using 小工具集合.Models;
using 小工具集合.Services.Ocr;
using Forms = System.Windows.Forms;

namespace 小工具集合.Services;

public sealed partial class ToolProcessor
{
    private const string OcrSpaceEndpoint = "https://api.ocr.space/parse/image";

    private static string RecognizeImageText(ToolRequest request)
    {
        string source = GetParameterOrDefault(request, "source", "图片文件");
        string language = GetOcrLanguage(GetParameterOrDefault(request, "language", "自动"));
        string engine = GetParameterOrDefault(request, "engine", "2");
        bool scale = IsTrue(GetParameterOrDefault(request, "scale", "true"));
        bool networkEnabled = IsTrue(GetParameterOrDefault(request, "networkEnabled", "true"));
        string apiKey = GetParameterOrDefault(request, "apiKey", "helloworld");

        byte[] imageBytes = LoadOcrImageBytes(request, source);
        var attempts = new StringBuilder();

        if (networkEnabled)
        {
            try
            {
                string onlineText = RunOcrSpaceAsync(imageBytes, apiKey, language, engine, scale)
                    .GetAwaiter()
                    .GetResult();
                return onlineText;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException)
            {
                attempts.AppendLine("在线 OCR 失败：" + TrimMessage(ex.Message));
            }
        }
        else
        {
            attempts.AppendLine("在线 OCR 已在设置中关闭。");
        }

        try
        {
            string localText = new WindowsOcrService()
                .RecognizeAsync(imageBytes, language)
                .GetAwaiter()
                .GetResult();
            return "识别文本：" + Environment.NewLine
                + localText
                + Environment.NewLine
                + Environment.NewLine
                + "来源：Windows OCR 本地识别";
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            attempts.AppendLine("Windows OCR 回退失败：" + TrimMessage(ex.Message));
        }

        attempts.AppendLine("建议：安装对应 Windows OCR 语言包，或填写 OCR.space API Key 并保持联网。");
        throw new InvalidOperationException(attempts.ToString().Trim());
    }

    public static string ParseOcrSpaceResponse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        bool isErrored = root.TryGetProperty("IsErroredOnProcessing", out JsonElement errored)
            && errored.ValueKind == JsonValueKind.True;
        string rootError = ReadOcrError(root);

        if (isErrored && !string.IsNullOrWhiteSpace(rootError))
        {
            throw new InvalidOperationException(rootError);
        }

        if (!root.TryGetProperty("ParsedResults", out JsonElement results) || results.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("OCR.space 返回结构缺少 ParsedResults。");
        }

        var builder = new StringBuilder();
        foreach (JsonElement result in results.EnumerateArray())
        {
            string pageError = ReadOcrError(result);
            if (!string.IsNullOrWhiteSpace(pageError))
            {
                builder.AppendLine("[页面错误] " + pageError);
                continue;
            }

            if (result.TryGetProperty("ParsedText", out JsonElement parsedText) && parsedText.ValueKind == JsonValueKind.String)
            {
                builder.AppendLine(parsedText.GetString());
            }
        }

        string text = builder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("OCR.space 没有识别到可用文本。");
        }

        return "识别文本：" + Environment.NewLine + text + Environment.NewLine + Environment.NewLine + "来源：OCR.space Free OCR API";
    }

    private static async Task<string> RunOcrSpaceAsync(byte[] imageBytes, string apiKey, string language, string engine, bool scale)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(string.IsNullOrWhiteSpace(apiKey) ? "helloworld" : apiKey), "apikey");
        content.Add(new StringContent(language), "language");
        content.Add(new StringContent("false"), "isOverlayRequired");
        content.Add(new StringContent(engine), "OCREngine");
        content.Add(new StringContent(scale ? "true" : "false"), "scale");
        content.Add(new ByteArrayContent(imageBytes), "file", "lcm-ocr.png");

        using var request = new HttpRequestMessage(HttpMethod.Post, OcrSpaceEndpoint)
        {
            Content = content
        };
        request.Headers.UserAgent.ParseAdd("LCM-Toolbox/3.1");
        request.Headers.TryAddWithoutValidation("apikey", string.IsNullOrWhiteSpace(apiKey) ? "helloworld" : apiKey);

        using HttpResponseMessage response = await HttpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        return ParseOcrSpaceResponse(json);
    }

    private static byte[] LoadOcrImageBytes(ToolRequest request, string source)
    {
        if (source.Contains("剪贴板", StringComparison.Ordinal))
        {
            return ReadClipboardImageBytes();
        }

        if (source.Contains("截图", StringComparison.Ordinal))
        {
            return CaptureScreenBytes();
        }

        string file = GetParameter(request, "imageFile");
        if (string.IsNullOrWhiteSpace(file))
        {
            throw new InvalidOperationException("请选择图片文件，或把来源切换为剪贴板/全屏截图。");
        }

        if (!File.Exists(file))
        {
            throw new FileNotFoundException("图片文件不存在。", file);
        }

        return File.ReadAllBytes(file);
    }

    private static byte[] ReadClipboardImageBytes()
    {
        byte[]? bytes = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (!Clipboard.ContainsImage())
                {
                    throw new InvalidOperationException("剪贴板里没有图片。");
                }

                BitmapSource? image = Clipboard.GetImage();
                if (image is null)
                {
                    throw new InvalidOperationException("读取剪贴板图片失败。");
                }

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                using var stream = new MemoryStream();
                encoder.Save(stream);
                bytes = stream.ToArray();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new InvalidOperationException(failure.Message, failure);
        }

        return bytes ?? throw new InvalidOperationException("读取剪贴板图片失败。");
    }

    private static byte[] CaptureScreenBytes()
    {
        Rectangle bounds = Forms.Screen.AllScreens
            .Select(screen => screen.Bounds)
            .Aggregate(Rectangle.Union);
        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private static string GetOcrLanguage(string value)
    {
        return value switch
        {
            "中文简体" => "chs",
            "中文繁体" => "cht",
            "英文" => "eng",
            "日文" => "jpn",
            "韩文" => "kor",
            _ => "auto"
        };
    }

    private static string GetParameterOrDefault(ToolRequest request, string key, string fallback)
    {
        string value = GetParameter(request, key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string ReadOcrError(JsonElement element)
    {
        foreach (string propertyName in new[] { "ErrorMessage", "ErrorDetails" })
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                return property.GetString() ?? string.Empty;
            }

            if (property.ValueKind == JsonValueKind.Array)
            {
                return string.Join("; ", property.EnumerateArray().Select(item => item.ToString()));
            }
        }

        return string.Empty;
    }
}
