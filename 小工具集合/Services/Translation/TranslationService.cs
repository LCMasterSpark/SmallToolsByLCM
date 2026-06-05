// 文件作用：统一封装文本翻译提供商，供普通翻译、文件翻译和实时翻译复用。
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace 小工具集合.Services.Translation;

public sealed record TranslationRequest(
    string Text,
    string SourceLanguage,
    string TargetLanguage,
    string Provider,
    TranslationSettings Settings);

public sealed record TranslationResponse(string Text, string Provider, string Detail = "");

public sealed class TranslationService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TranslationResponse> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new InvalidOperationException("请输入要翻译的文本。");
        }

        // 调用热点：三个翻译入口最终都会进到这里。新增引擎时只扩展 provider 分支，
        // 不要把具体 HTTP 协议散落到 ToolProcessor 或实时翻译控件里。
        string provider = NormalizeProvider(request.Provider);
        return provider switch
        {
            "LibreTranslate" => await TranslateLibreAsync(request, cancellationToken).ConfigureAwait(false),
            "Azure" => await TranslateAzureAsync(request, cancellationToken).ConfigureAwait(false),
            "DeepL" => await TranslateDeepLAsync(request, cancellationToken).ConfigureAwait(false),
            "Google" => await TranslateGoogleAsync(request, cancellationToken).ConfigureAwait(false),
            "Baidu" => await TranslateBaiduAsync(request, cancellationToken).ConfigureAwait(false),
            "Youdao" => await TranslateYoudaoAsync(request, cancellationToken).ConfigureAwait(false),
            "OpenAI-compatible" => await TranslateOpenAiCompatibleAsync(request, cancellationToken).ConfigureAwait(false),
            "Ollama" => await TranslateOllamaAsync(request, cancellationToken).ConfigureAwait(false),
            "Mock" => new TranslationResponse($"[{request.TargetLanguage}] {request.Text}", "Mock"),
            _ => throw new InvalidOperationException($"暂不支持翻译引擎：{request.Provider}")
        };
    }

    public static string NormalizeProvider(string value)
    {
        return value.Trim() switch
        {
            "" => "LibreTranslate",
            "LibreTranslate" => "LibreTranslate",
            "Azure Translator" => "Azure",
            "Azure" => "Azure",
            "DeepL" => "DeepL",
            "Google Translate" => "Google",
            "Google" => "Google",
            "百度翻译" => "Baidu",
            "Baidu" => "Baidu",
            "网易有道" => "Youdao",
            "Youdao" => "Youdao",
            "OpenAI-compatible" => "OpenAI-compatible",
            "Ollama" => "Ollama",
            "Mock" => "Mock",
            var other => other
        };
    }

    public static string NormalizeLanguage(string value)
    {
        // UI 使用中文显示值，外部 API 使用短语言码；这层映射是两者之间的边界。
        return value.Trim() switch
        {
            "" => "auto",
            "自动" => "auto",
            "中文" => "zh",
            "英文" => "en",
            "日文" => "ja",
            "韩文" => "ko",
            "法文" => "fr",
            "德文" => "de",
            "西班牙文" => "es",
            "俄文" => "ru",
            var other => other
        };
    }

    public static IReadOnlyList<string> ProviderOptions { get; } =
    [
        "LibreTranslate",
        "Azure Translator",
        "DeepL",
        "Google Translate",
        "百度翻译",
        "网易有道",
        "OpenAI-compatible",
        "Ollama"
    ];

    public static IReadOnlyList<string> LanguageOptions { get; } =
    [
        "自动",
        "中文",
        "英文",
        "日文",
        "韩文",
        "法文",
        "德文",
        "西班牙文",
        "俄文"
    ];

    private static async Task<TranslationResponse> TranslateLibreAsync(TranslationRequest request, CancellationToken cancellationToken)
    {
        string endpoint = RequireUrl(request.Settings.LibreTranslateEndpoint, "请先在设置中配置 LibreTranslate Endpoint。");
        var payload = new Dictionary<string, object?>
        {
            ["q"] = request.Text,
            ["source"] = NormalizeLanguage(request.SourceLanguage),
            ["target"] = NormalizeLanguage(request.TargetLanguage),
            ["format"] = "text"
        };
        if (!string.IsNullOrWhiteSpace(request.Settings.LibreTranslateApiKey))
        {
            payload["api_key"] = request.Settings.LibreTranslateApiKey;
        }

        using HttpResponseMessage response = await Client.PostAsync(
            endpoint,
            JsonContent(payload),
            cancellationToken).ConfigureAwait(false);
        string json = await ReadResponseAsync(response, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        string text = ReadString(document.RootElement, "translatedText");
        return new TranslationResponse(text, "LibreTranslate", endpoint);
    }

    private static async Task<TranslationResponse> TranslateAzureAsync(TranslationRequest request, CancellationToken cancellationToken)
    {
        string endpoint = RequireUrl(request.Settings.AzureEndpoint, "请先在设置中配置 Azure Translator Endpoint。").TrimEnd('/');
        string key = RequireValue(request.Settings.AzureKey, "请先在设置中配置 Azure Translator Key。");
        string region = request.Settings.AzureRegion.Trim();
        string source = NormalizeLanguage(request.SourceLanguage);
        string target = NormalizeLanguage(request.TargetLanguage);
        string url = $"{endpoint}/translate?api-version=3.0&to={Uri.EscapeDataString(target)}";
        if (source != "auto")
        {
            url += "&from=" + Uri.EscapeDataString(source);
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent(new[] { new { Text = request.Text } })
        };
        message.Headers.Add("Ocp-Apim-Subscription-Key", key);
        if (!string.IsNullOrWhiteSpace(region))
        {
            message.Headers.Add("Ocp-Apim-Subscription-Region", region);
        }

        using HttpResponseMessage response = await Client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        string json = await ReadResponseAsync(response, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement translations = document.RootElement[0].GetProperty("translations");
        return new TranslationResponse(ReadString(translations[0], "text"), "Azure Translator", endpoint);
    }

    private static async Task<TranslationResponse> TranslateDeepLAsync(TranslationRequest request, CancellationToken cancellationToken)
    {
        string endpoint = RequireUrl(request.Settings.DeepLApiUrl, "请先在设置中配置 DeepL API URL。");
        string key = RequireValue(request.Settings.DeepLApiKey, "请先在设置中配置 DeepL API Key。");
        var pairs = new Dictionary<string, string>
        {
            ["auth_key"] = key,
            ["text"] = request.Text,
            ["target_lang"] = NormalizeLanguage(request.TargetLanguage).ToUpperInvariant()
        };
        string source = NormalizeLanguage(request.SourceLanguage);
        if (source != "auto")
        {
            pairs["source_lang"] = source.ToUpperInvariant();
        }

        using HttpResponseMessage response = await Client.PostAsync(endpoint, new FormUrlEncodedContent(pairs), cancellationToken).ConfigureAwait(false);
        string json = await ReadResponseAsync(response, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        return new TranslationResponse(ReadString(document.RootElement.GetProperty("translations")[0], "text"), "DeepL", endpoint);
    }

    private static async Task<TranslationResponse> TranslateGoogleAsync(TranslationRequest request, CancellationToken cancellationToken)
    {
        string key = RequireValue(request.Settings.GoogleApiKey, "请先在设置中配置 Google Translate API Key。");
        string url = "https://translation.googleapis.com/language/translate/v2?key=" + Uri.EscapeDataString(key);
        var payload = new Dictionary<string, object?>
        {
            ["q"] = request.Text,
            ["target"] = NormalizeLanguage(request.TargetLanguage),
            ["format"] = "text"
        };
        string source = NormalizeLanguage(request.SourceLanguage);
        if (source != "auto")
        {
            payload["source"] = source;
        }

        using HttpResponseMessage response = await Client.PostAsync(url, JsonContent(payload), cancellationToken).ConfigureAwait(false);
        string json = await ReadResponseAsync(response, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement translations = document.RootElement.GetProperty("data").GetProperty("translations");
        return new TranslationResponse(ReadString(translations[0], "translatedText"), "Google Translate", "Google Cloud Translate v2");
    }

    private static async Task<TranslationResponse> TranslateBaiduAsync(TranslationRequest request, CancellationToken cancellationToken)
    {
        string appId = RequireValue(request.Settings.BaiduAppId, "请先在设置中配置百度翻译 App ID。");
        string secret = RequireValue(request.Settings.BaiduSecret, "请先在设置中配置百度翻译密钥。");
        string salt = RandomNumberGenerator.GetInt32(100000, 999999).ToString(CultureInfo.InvariantCulture);
        string source = NormalizeLanguage(request.SourceLanguage) == "auto" ? "auto" : NormalizeLanguage(request.SourceLanguage);
        string target = NormalizeLanguage(request.TargetLanguage);
        string sign = Md5Hex(appId + request.Text + salt + secret);
        var pairs = new Dictionary<string, string>
        {
            ["q"] = request.Text,
            ["from"] = source,
            ["to"] = target,
            ["appid"] = appId,
            ["salt"] = salt,
            ["sign"] = sign
        };

        using HttpResponseMessage response = await Client.PostAsync("https://fanyi-api.baidu.com/api/trans/vip/translate", new FormUrlEncodedContent(pairs), cancellationToken).ConfigureAwait(false);
        string json = await ReadResponseAsync(response, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("error_msg", out JsonElement error))
        {
            throw new InvalidOperationException(error.GetString() ?? "百度翻译返回错误。");
        }

        string text = string.Join(Environment.NewLine, document.RootElement.GetProperty("trans_result").EnumerateArray().Select(item => ReadString(item, "dst")));
        return new TranslationResponse(text, "百度翻译", "fanyi-api.baidu.com");
    }

    private static async Task<TranslationResponse> TranslateYoudaoAsync(TranslationRequest request, CancellationToken cancellationToken)
    {
        string appKey = RequireValue(request.Settings.YoudaoAppKey, "请先在设置中配置网易有道 App Key。");
        string secret = RequireValue(request.Settings.YoudaoAppSecret, "请先在设置中配置网易有道 App Secret。");
        string salt = Guid.NewGuid().ToString("N");
        string curtime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        string sign = Sha256Hex(appKey + TruncateForYoudao(request.Text) + salt + curtime + secret);
        var pairs = new Dictionary<string, string>
        {
            ["q"] = request.Text,
            ["from"] = NormalizeLanguage(request.SourceLanguage),
            ["to"] = NormalizeLanguage(request.TargetLanguage),
            ["appKey"] = appKey,
            ["salt"] = salt,
            ["sign"] = sign,
            ["signType"] = "v3",
            ["curtime"] = curtime
        };

        using HttpResponseMessage response = await Client.PostAsync("https://openapi.youdao.com/api", new FormUrlEncodedContent(pairs), cancellationToken).ConfigureAwait(false);
        string json = await ReadResponseAsync(response, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        string code = ReadString(document.RootElement, "errorCode", required: false);
        if (!string.IsNullOrWhiteSpace(code) && code != "0")
        {
            throw new InvalidOperationException("网易有道返回错误码：" + code);
        }

        string text = string.Join(Environment.NewLine, document.RootElement.GetProperty("translation").EnumerateArray().Select(item => item.GetString()));
        return new TranslationResponse(text, "网易有道", "openapi.youdao.com");
    }

    private static async Task<TranslationResponse> TranslateOpenAiCompatibleAsync(TranslationRequest request, CancellationToken cancellationToken)
    {
        string baseUrl = RequireUrl(request.Settings.OpenAiBaseUrl, "请先在设置中配置 OpenAI-compatible Base URL。").TrimEnd('/');
        string model = RequireValue(request.Settings.OpenAiModel, "请先在设置中配置模型名。");
        string key = request.Settings.OpenAiApiKey.Trim();
        // OpenAI-compatible 被用来兼容国内大模型、自建网关和 OpenAI 标准接口。
        // 这里保持最小 chat/completions 负载，避免绑定某一家厂商的专有字段。
        var payload = new
        {
            model,
            temperature = 0.2,
            messages = new[]
            {
                new { role = "system", content = BuildSystemPrompt(request) },
                new { role = "user", content = request.Text }
            }
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions")
        {
            Content = JsonContent(payload)
        };
        if (!string.IsNullOrWhiteSpace(key))
        {
            message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
        }

        using HttpResponseMessage response = await Client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        string json = await ReadResponseAsync(response, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        string text = ReadString(document.RootElement.GetProperty("choices")[0].GetProperty("message"), "content");
        return new TranslationResponse(text.Trim(), "OpenAI-compatible", baseUrl);
    }

    private static async Task<TranslationResponse> TranslateOllamaAsync(TranslationRequest request, CancellationToken cancellationToken)
    {
        string endpoint = RequireUrl(request.Settings.OllamaEndpoint, "请先在设置中配置 Ollama Endpoint。").TrimEnd('/');
        string model = RequireValue(request.Settings.OllamaModel, "请先在设置中配置 Ollama 模型名。");
        var payload = new
        {
            model,
            stream = false,
            prompt = BuildSystemPrompt(request) + Environment.NewLine + Environment.NewLine + request.Text
        };

        using HttpResponseMessage response = await Client.PostAsync($"{endpoint}/api/generate", JsonContent(payload), cancellationToken).ConfigureAwait(false);
        string json = await ReadResponseAsync(response, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        return new TranslationResponse(ReadString(document.RootElement, "response").Trim(), "Ollama", endpoint);
    }

    private static string BuildSystemPrompt(TranslationRequest request)
    {
        // 大模型翻译要尽量约束输出，否则容易夹带解释、Markdown 或寒暄。
        var builder = new StringBuilder();
        builder.Append("Translate the user text");
        string source = NormalizeLanguage(request.SourceLanguage);
        if (source != "auto")
        {
            builder.Append(" from ").Append(source);
        }

        builder.Append(" to ").Append(NormalizeLanguage(request.TargetLanguage)).Append(". Return only the translation.");
        if (request.Settings.UseGlossary && !string.IsNullOrWhiteSpace(request.Settings.Glossary))
        {
            builder.Append(" Follow this glossary when applicable: ").Append(request.Settings.Glossary);
        }

        return builder.ToString();
    }

    private static StringContent JsonContent<T>(T payload)
    {
        return new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
    }

    private static async Task<string> ReadResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            string message = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase ?? response.StatusCode.ToString() : body;
            throw new InvalidOperationException($"翻译接口返回失败：{(int)response.StatusCode} {message}");
        }

        return body;
    }

    private static string ReadString(JsonElement element, string propertyName, bool required = true)
    {
        if (element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }

        if (!required)
        {
            return string.Empty;
        }

        throw new InvalidOperationException("翻译接口返回结构变化，缺少字段：" + propertyName);
    }

    private static string RequireValue(string value, string message)
    {
        return string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException(message) : value.Trim();
    }

    private static string RequireUrl(string value, string message)
    {
        string url = RequireValue(value, message);
        return Uri.TryCreate(url, UriKind.Absolute, out _) ? url : throw new InvalidOperationException("翻译接口地址不是有效 URL：" + url);
    }

    private static string Md5Hex(string value)
    {
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static string Sha256Hex(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static string TruncateForYoudao(string value)
    {
        return value.Length <= 20 ? value : value[..10] + value.Length.ToString(CultureInfo.InvariantCulture) + value[^10..];
    }
}
