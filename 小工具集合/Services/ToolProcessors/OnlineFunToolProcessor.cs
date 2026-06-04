using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using 小工具集合.Models;

namespace 小工具集合.Services;

public sealed partial class ToolProcessor
{
    private const string HitokotoSourceUrl = "https://developer.hitokoto.cn/sentence/";
    private const string OpenMeteoSourceUrl = "https://open-meteo.com/";
    private const string IpApiSourceUrl = "https://ip-api.com/";
    private const string MyMemorySourceUrl = "https://mymemory.translated.net/doc/spec.php";
    private const string OnlineUserAgent = "SmallToolbox/1.5 (+local-wpf-tool)";

    private readonly record struct WeatherLocation(string Name, string Region, string Country, double Latitude, double Longitude);
    private readonly record struct HitokotoSnippet(string Text, string From, string FromWho, string Creator, string Uuid);

    private static string QueryOnlineHitokoto(ToolRequest request)
    {
        string category = GetParameter(request, "category");
        return QueryHitokoto(category, "随机一言");
    }

    private static string QueryOnlinePoemLine()
    {
        return QueryHitokoto("诗词", "诗词一句");
    }

    private static string QueryHitokoto(string category, string title)
    {
        HitokotoSnippet snippet = GetHitokotoSnippet(category);

        var builder = new StringBuilder();
        builder.AppendLine(title);
        builder.AppendLine();
        builder.AppendLine(snippet.Text);
        builder.AppendLine();
        AppendHitokotoMetadata(builder, snippet);
        builder.AppendLine($"来源：Hitokoto（{HitokotoSourceUrl}）");
        return builder.ToString();
    }

    private static bool UsesOnlineEnhancement(ToolRequest request)
    {
        return GetParameter(request, "enhancement") == "联网增强";
    }

    private static string EnhanceVariableNamesOnline(ToolRequest request, string localOutput)
    {
        return TryOnlineEnhancement(localOutput, () =>
        {
            string input = string.IsNullOrWhiteSpace(request.Input) ? "new value" : request.Input.Trim();
            string translated = ContainsCjk(input) ? TranslateText(input, "zh-CN", "en-US") : input;
            string[] words = TokenizeNameWords(translated);
            if (words.Length == 0)
            {
                throw new InvalidOperationException("联网翻译结果没有可用于命名的英文词。");
            }

            string camel = ToCamelCase(words);
            string pascal = ToPascalCase(words);
            string snake = string.Join('_', words);
            string kebab = string.Join('-', words);

            var builder = new StringBuilder();
            builder.AppendLine("联网增强变量名：");
            builder.AppendLine($"语义参考：{translated}");
            builder.AppendLine($"camelCase：{camel}");
            builder.AppendLine($"PascalCase：{pascal}");
            builder.AppendLine($"snake_case：{snake}");
            builder.AppendLine($"kebab-case：{kebab}");
            builder.AppendLine($"CONSTANT_CASE：{snake.ToUpperInvariant()}");
            builder.AppendLine($"来源：MyMemory（{MyMemorySourceUrl}）");
            builder.AppendLine();
            builder.AppendLine("本地候选：");
            builder.Append(localOutput.TrimEnd());
            return builder.ToString();
        });
    }

    private static string EnhanceCommitMessagesOnline(ToolRequest request, string localOutput)
    {
        return TryOnlineEnhancement(localOutput, () =>
        {
            string summary = string.IsNullOrWhiteSpace(request.Input) ? "更新小工具功能" : request.Input.Trim();
            string type = GetParameter(request, "type");
            if (string.IsNullOrWhiteSpace(type) || type == "自动")
            {
                type = InferCommitType(summary);
            }

            string english = ContainsCjk(summary) ? TranslateText(summary, "zh-CN", "en-US") : ToSentence(summary);
            string chinese = ContainsCjk(summary) ? summary : TranslateText(summary, "en-US", "zh-CN");
            string englishPhrase = ToSentence(english).TrimEnd('.');

            var builder = new StringBuilder();
            builder.AppendLine("联网增强 Commit 文案：");
            builder.AppendLine($"{type}: {englishPhrase}");
            builder.AppendLine($"{type}(tools): {englishPhrase}");
            builder.AppendLine($"PR 标题：{chinese}");
            builder.AppendLine($"Changelog：{chinese}");
            builder.AppendLine($"来源：MyMemory（{MyMemorySourceUrl}）");
            builder.AppendLine();
            builder.AppendLine("本地候选：");
            builder.Append(localOutput.TrimEnd());
            return builder.ToString();
        });
    }

    private static string EnhanceEmojiWrapOnline(ToolRequest request, string localOutput)
    {
        return TryOnlineEnhancement(localOutput, () =>
        {
            HitokotoSnippet snippet = GetHitokotoSnippet("随机");
            var builder = new StringBuilder();
            builder.AppendLine(localOutput.TrimEnd());
            builder.AppendLine();
            builder.AppendLine($"线上灵感：{snippet.Text}");
            AppendHitokotoMetadata(builder, snippet);
            builder.AppendLine($"来源：Hitokoto（{HitokotoSourceUrl}）");
            return builder.ToString();
        });
    }

    private static string EnhanceFakeLogOnline(ToolRequest request, string localOutput)
    {
        return TryOnlineEnhancement(localOutput, () =>
        {
            string service = GetParameter(request, "service");
            if (string.IsNullOrWhiteSpace(service))
            {
                service = "fun-lab";
            }

            HitokotoSnippet snippet = GetHitokotoSnippet("网络");
            DateTimeOffset now = DateTimeOffset.Now;
            var builder = new StringBuilder();
            builder.AppendLine(localOutput.TrimEnd());
            builder.AppendLine($"{now.AddSeconds(1):yyyy-MM-dd HH:mm:ss.fff zzz} [INFO] {service} remote_hint=\"{EscapeLogValue(snippet.Text)}\" source=\"Hitokoto\"");
            builder.AppendLine($"{now.AddSeconds(2):yyyy-MM-dd HH:mm:ss.fff zzz} [DEBUG] {service} remote_hint_source=\"{EscapeLogValue(snippet.From)}\"");
            builder.AppendLine($"来源：Hitokoto（{HitokotoSourceUrl}）");
            return builder.ToString();
        });
    }

    private static string EnhanceExcuseOnline(ToolRequest request, string localOutput)
    {
        return TryOnlineEnhancement(localOutput, () =>
        {
            HitokotoSnippet snippet = GetHitokotoSnippet("抖机灵");
            var builder = new StringBuilder();
            builder.AppendLine(localOutput.TrimEnd());
            builder.AppendLine();
            builder.AppendLine($"线上灵感：{snippet.Text}");
            builder.AppendLine("补充说法：我先按这个方向把复现路径和日志补齐。");
            AppendHitokotoMetadata(builder, snippet);
            builder.AppendLine($"来源：Hitokoto（{HitokotoSourceUrl}）");
            return builder.ToString();
        });
    }

    private static string QueryWeatherCard(string input)
    {
        string text = input.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("请输入城市名或经纬度，例如：北京 或 39.9,116.4。");
        }

        WeatherLocation location = TryParseCoordinates(text, out double latitude, out double longitude)
            ? new WeatherLocation("自定义坐标", string.Empty, string.Empty, latitude, longitude)
            : QueryOpenMeteoLocation(text);

        string weatherUrl = string.Format(
            CultureInfo.InvariantCulture,
            "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current_weather=true&timezone=auto",
            location.Latitude,
            location.Longitude);

        using JsonDocument document = GetOnlineJsonDocument("Open-Meteo", weatherUrl);
        if (!document.RootElement.TryGetProperty("current_weather", out JsonElement current))
        {
            throw new InvalidOperationException("联网查询失败（Open-Meteo）：返回内容缺少当前天气。");
        }

        double temperature = GetJsonDouble(current, "temperature");
        double windSpeed = GetJsonDouble(current, "windspeed");
        double windDirection = GetJsonDouble(current, "winddirection");
        int weatherCode = GetJsonInt(current, "weathercode");
        string time = GetJsonString(current, "time");

        var builder = new StringBuilder();
        builder.AppendLine("天气小卡");
        builder.AppendLine();
        builder.AppendLine($"位置：{FormatWeatherLocation(location)}");
        builder.AppendLine($"坐标：{location.Latitude.ToString("0.####", CultureInfo.InvariantCulture)}, {location.Longitude.ToString("0.####", CultureInfo.InvariantCulture)}");
        builder.AppendLine($"天气：{DescribeWeatherCode(weatherCode)}（WMO {weatherCode}）");
        builder.AppendLine($"温度：{temperature.ToString("0.#", CultureInfo.InvariantCulture)} °C");
        builder.AppendLine($"风速：{windSpeed.ToString("0.#", CultureInfo.InvariantCulture)} km/h");
        builder.AppendLine($"风向：{windDirection.ToString("0", CultureInfo.InvariantCulture)}°");
        if (!string.IsNullOrWhiteSpace(time))
        {
            builder.AppendLine($"更新时间：{time}");
        }

        builder.AppendLine($"来源：Open-Meteo（{OpenMeteoSourceUrl}）");
        return builder.ToString();
    }

    private static string QueryIpInfoCard(string input)
    {
        string query = input.Trim();
        string queryPath = string.IsNullOrWhiteSpace(query) ? string.Empty : "/" + Uri.EscapeDataString(query);
        string url = $"http://ip-api.com/json{queryPath}?lang=zh-CN&fields=status,message,query,country,regionName,city,isp,org,as,timezone,lat,lon";

        using JsonDocument document = GetOnlineJsonDocument("IP-API", url);
        JsonElement root = document.RootElement;
        string status = GetJsonString(root, "status");
        if (!status.Equals("success", StringComparison.OrdinalIgnoreCase))
        {
            string message = GetJsonString(root, "message");
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)
                ? "联网查询失败（IP-API）：接口未返回成功状态。"
                : $"联网查询失败（IP-API）：{message}");
        }

        var builder = new StringBuilder();
        builder.AppendLine("IP 信息小卡");
        builder.AppendLine();
        builder.AppendLine($"IP：{GetJsonString(root, "query")}");
        builder.AppendLine($"位置：{JoinNonEmpty(" / ", GetJsonString(root, "country"), GetJsonString(root, "regionName"), GetJsonString(root, "city"))}");
        builder.AppendLine($"ISP：{GetJsonString(root, "isp")}");
        builder.AppendLine($"组织：{GetJsonString(root, "org")}");
        builder.AppendLine($"ASN：{GetJsonString(root, "as")}");
        builder.AppendLine($"时区：{GetJsonString(root, "timezone")}");
        builder.AppendLine($"坐标：{FormatOptionalDouble(root, "lat")}, {FormatOptionalDouble(root, "lon")}");
        builder.AppendLine($"来源：IP-API（{IpApiSourceUrl}，免费非商业接口）");
        return builder.ToString();
    }

    private static WeatherLocation QueryOpenMeteoLocation(string city)
    {
        string url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city)}&count=1&language=zh&format=json";
        using JsonDocument document = GetOnlineJsonDocument("Open-Meteo Geocoding", url);
        if (!document.RootElement.TryGetProperty("results", out JsonElement results) || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"未找到城市：{city}");
        }

        JsonElement item = results[0];
        string name = GetJsonString(item, "name");
        double latitude = GetJsonDouble(item, "latitude");
        double longitude = GetJsonDouble(item, "longitude");
        return new WeatherLocation(
            string.IsNullOrWhiteSpace(name) ? city : name,
            GetJsonString(item, "admin1"),
            GetJsonString(item, "country"),
            latitude,
            longitude);
    }

    private static bool TryParseCoordinates(string input, out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;
        string[] parts = input.Split([',', '，'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out latitude)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out longitude))
        {
            throw new InvalidOperationException("经纬度格式不正确，请使用纬度,经度，例如：39.9,116.4。");
        }

        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            throw new InvalidOperationException("纬度需要在 -90 到 90 之间，经度需要在 -180 到 180 之间。");
        }

        return true;
    }

    private static JsonDocument GetOnlineJsonDocument(string sourceName, string url)
    {
        string content = GetOnlineText(sourceName, url);
        try
        {
            return JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"联网查询失败（{sourceName}）：返回内容不是预期 JSON。", ex);
        }
    }

    private static string GetOnlineText(string sourceName, string url)
    {
        try
        {
            using var cancellation = new CancellationTokenSource(OnlineFunRequestTimeout);
            using var message = new HttpRequestMessage(HttpMethod.Get, url);
            message.Headers.TryAddWithoutValidation("User-Agent", OnlineUserAgent);
            message.Headers.TryAddWithoutValidation("Accept", "application/json,text/plain;q=0.9,*/*;q=0.8");

            using HttpResponseMessage response = HttpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellation.Token)
                .GetAwaiter()
                .GetResult();
            string content = response.Content.ReadAsStringAsync(cancellation.Token).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"联网查询失败（{sourceName}）：HTTP {(int)response.StatusCode} {response.ReasonPhrase}。");
            }

            return content;
        }
        catch (OperationCanceledException ex)
        {
            throw new InvalidOperationException($"联网查询超时（{sourceName}，{OnlineFunRequestTimeout.TotalSeconds:0.#} 秒）。", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"联网查询失败（{sourceName}）：{TrimMessage(ex.Message)}", ex);
        }
    }

    private static string GetHitokotoCategoryCode(string category)
    {
        return category switch
        {
            "动画" => "a",
            "漫画" => "b",
            "游戏" => "c",
            "文学" => "d",
            "原创" => "e",
            "网络" => "f",
            "影视" => "h",
            "诗词" => "i",
            "哲学" => "k",
            "抖机灵" => "l",
            _ => string.Empty
        };
    }

    private static HitokotoSnippet GetHitokotoSnippet(string category)
    {
        string code = GetHitokotoCategoryCode(category);
        string url = string.IsNullOrEmpty(code)
            ? "https://v1.hitokoto.cn/?encode=json"
            : $"https://v1.hitokoto.cn/?encode=json&c={code}";

        using JsonDocument document = GetOnlineJsonDocument("Hitokoto", url);
        JsonElement root = document.RootElement;
        string text = GetJsonString(root, "hitokoto").Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("联网查询失败（Hitokoto）：返回内容缺少句子正文。");
        }

        return new HitokotoSnippet(
            text,
            GetJsonString(root, "from"),
            GetJsonString(root, "from_who"),
            GetJsonString(root, "creator"),
            GetJsonString(root, "uuid"));
    }

    private static void AppendHitokotoMetadata(StringBuilder builder, HitokotoSnippet snippet)
    {
        if (!string.IsNullOrWhiteSpace(snippet.From))
        {
            builder.AppendLine($"出处：{snippet.From}");
        }

        if (!string.IsNullOrWhiteSpace(snippet.FromWho))
        {
            builder.AppendLine($"作者：{snippet.FromWho}");
        }

        if (!string.IsNullOrWhiteSpace(snippet.Creator))
        {
            builder.AppendLine($"提供者：{snippet.Creator}");
        }

        if (!string.IsNullOrWhiteSpace(snippet.Uuid))
        {
            builder.AppendLine($"链接：https://hitokoto.cn?uuid={snippet.Uuid}");
        }
    }

    private static string TranslateText(string text, string from, string to)
    {
        string url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(text)}&langpair={Uri.EscapeDataString(from + "|" + to)}";
        using JsonDocument document = GetOnlineJsonDocument("MyMemory", url);
        JsonElement root = document.RootElement;
        int status = root.TryGetProperty("responseStatus", out JsonElement statusElement) && statusElement.TryGetInt32(out int value) ? value : 0;
        if (status != 200)
        {
            string details = GetJsonString(root, "responseDetails");
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(details)
                ? "联网翻译失败（MyMemory）：接口未返回成功状态。"
                : $"联网翻译失败（MyMemory）：{details}");
        }

        if (!root.TryGetProperty("responseData", out JsonElement responseData))
        {
            throw new InvalidOperationException("联网翻译失败（MyMemory）：返回内容缺少 responseData。");
        }

        string translated = WebUtility.HtmlDecode(GetJsonString(responseData, "translatedText")).Trim();
        return string.IsNullOrWhiteSpace(translated) ? text : translated;
    }

    private static string TryOnlineEnhancement(string localOutput, Func<string> onlineFactory)
    {
        try
        {
            return onlineFactory();
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or JsonException or OperationCanceledException)
        {
            return localOutput.TrimEnd()
                + Environment.NewLine
                + Environment.NewLine
                + $"联网增强失败：{TrimMessage(ex.Message)}";
        }
    }

    private static bool ContainsCjk(string text)
    {
        return text.Any(value => value is >= '\u3400' and <= '\u9fff');
    }

    private static string EscapeLogValue(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static string FormatWeatherLocation(WeatherLocation location)
    {
        return JoinNonEmpty(" / ", location.Country, location.Region, location.Name);
    }

    private static string DescribeWeatherCode(int code)
    {
        return code switch
        {
            0 => "晴朗",
            1 or 2 or 3 => "多云",
            45 or 48 => "雾",
            51 or 53 or 55 => "毛毛雨",
            56 or 57 => "冻毛毛雨",
            61 or 63 or 65 => "雨",
            66 or 67 => "冻雨",
            71 or 73 or 75 => "雪",
            77 => "雪粒",
            80 or 81 or 82 => "阵雨",
            85 or 86 => "阵雪",
            95 => "雷暴",
            96 or 99 => "雷暴伴冰雹",
            _ => "未知天气"
        };
    }

    private static string GetJsonString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return string.Empty;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() ?? string.Empty : property.ToString();
    }

    private static double GetJsonDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property) || !property.TryGetDouble(out double value))
        {
            throw new InvalidOperationException($"联网查询失败：返回内容缺少数值字段 {propertyName}。");
        }

        return value;
    }

    private static int GetJsonInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property) || !property.TryGetInt32(out int value))
        {
            throw new InvalidOperationException($"联网查询失败：返回内容缺少整数字段 {propertyName}。");
        }

        return value;
    }

    private static string FormatOptionalDouble(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement property) && property.TryGetDouble(out double value)
            ? value.ToString("0.####", CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private static string JoinNonEmpty(string separator, params string[] values)
    {
        string result = string.Join(separator, values.Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(result) ? "(无)" : result;
    }
}
