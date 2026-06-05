// 文件作用：封装 Windows.Media.Ocr，本地识别截图或图片字节，供 OCR 工具和实时翻译复用。
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace 小工具集合.Services.Ocr;

public sealed class WindowsOcrService
{
    public async Task<string> RecognizeAsync(byte[] imageBytes, string language, CancellationToken cancellationToken = default)
    {
        if (imageBytes.Length == 0)
        {
            throw new InvalidOperationException("没有可识别的图片数据。");
        }

        OcrEngine engine = CreateEngine(language);
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(imageBytes);
            await writer.StoreAsync().AsTask(cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync().AsTask(cancellationToken).ConfigureAwait(false);
        }

        stream.Seek(0);
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken).ConfigureAwait(false);
        using SoftwareBitmap bitmap = await decoder
            .GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied)
            .AsTask(cancellationToken)
            .ConfigureAwait(false);
        OcrResult result = await engine.RecognizeAsync(bitmap).AsTask(cancellationToken).ConfigureAwait(false);
        string text = result.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Windows OCR 没有识别到可用文本。");
        }

        return text;
    }

    public static bool IsAvailable()
    {
        try
        {
            return OcrEngine.TryCreateFromUserProfileLanguages() is not null;
        }
        catch
        {
            return false;
        }
    }

    private static OcrEngine CreateEngine(string language)
    {
        string tag = NormalizeLanguageTag(language);
        OcrEngine? engine;
        if (tag == "auto")
        {
            engine = OcrEngine.TryCreateFromUserProfileLanguages();
        }
        else
        {
            // Windows OCR 依赖系统安装的 OCR 语言包；没有语言包时要返回友好错误。
            var winLanguage = new Language(tag);
            if (!OcrEngine.IsLanguageSupported(winLanguage))
            {
                throw new InvalidOperationException($"当前系统未安装 Windows OCR 语言包：{tag}");
            }

            engine = OcrEngine.TryCreateFromLanguage(winLanguage);
        }

        return engine ?? throw new InvalidOperationException("当前系统无法创建 Windows OCR 引擎，请检查系统语言包。");
    }

    private static string NormalizeLanguageTag(string value)
    {
        return value.Trim() switch
        {
            "" => "auto",
            "auto" => "auto",
            "自动" => "auto",
            "zh" or "chs" or "中文" or "中文简体" => "zh-Hans",
            "zh-Hant" or "cht" or "中文繁体" => "zh-Hant",
            "en" or "eng" or "英文" => "en-US",
            "ja" or "jpn" or "日文" => "ja-JP",
            "ko" or "kor" or "韩文" => "ko-KR",
            "fr" or "法文" => "fr-FR",
            "de" or "德文" => "de-DE",
            "es" or "西班牙文" => "es-ES",
            "ru" or "俄文" => "ru-RU",
            var other => other
        };
    }
}
