// 文件作用：手动查询 GitHub Release，并把 latest release JSON 转为应用内更新状态。
using System.Net.Http;
using System.Text.Json;

namespace 小工具集合.Services;

public sealed record UpdateCheckResult(
    bool Success,
    bool HasUpdate,
    string CurrentVersion,
    string LatestVersion,
    string ReleaseUrl,
    string DownloadUrl,
    string Message)
{
    public static UpdateCheckResult Fail(string currentVersion, string message) =>
        new(false, false, currentVersion, string.Empty, string.Empty, string.Empty, message);
}

public sealed class UpdateCheckService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/LCMasterSpark/SmallToolsByLCM/releases/latest";
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    public async Task<UpdateCheckResult> CheckLatestAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.UserAgent.ParseAdd("LCM-Toolbox/3.1");
            using HttpResponseMessage response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseLatestReleaseJson(json, currentVersion);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return UpdateCheckResult.Fail(currentVersion, "检查更新失败：" + ex.Message);
        }
    }

    public static UpdateCheckResult ParseLatestReleaseJson(string json, string currentVersion)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        string tag = GetString(root, "tag_name");
        string releaseUrl = GetString(root, "html_url");
        string latestVersion = NormalizeVersion(tag);
        string current = NormalizeVersion(currentVersion);
        string downloadUrl = string.Empty;

        if (root.TryGetProperty("assets", out JsonElement assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement asset in assets.EnumerateArray())
            {
                string name = GetString(asset, "name");
                string browserDownloadUrl = GetString(asset, "browser_download_url");
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = browserDownloadUrl;
                    break;
                }
            }
        }

        bool hasUpdate = CompareVersions(latestVersion, current) > 0;
        string message = hasUpdate
            ? $"发现新版本：{tag}"
            : $"当前已是最新版本：{currentVersion}";

        return new UpdateCheckResult(true, hasUpdate, currentVersion, tag, releaseUrl, downloadUrl, message);
    }

    private static int CompareVersions(string left, string right)
    {
        Version leftVersion = Version.TryParse(left, out Version? parsedLeft) ? parsedLeft : new Version(0, 0);
        Version rightVersion = Version.TryParse(right, out Version? parsedRight) ? parsedRight : new Version(0, 0);
        return leftVersion.CompareTo(rightVersion);
    }

    private static string NormalizeVersion(string value)
    {
        string normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        int suffixIndex = normalized.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0)
        {
            normalized = normalized[..suffixIndex];
        }

        return string.IsNullOrWhiteSpace(normalized) ? "0.0" : normalized;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }
}
