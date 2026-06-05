// 文件作用：持久化主窗口偏好，并兼容迁移旧 preferences.json。
using System.IO;
using System.Text.Json;

namespace 小工具集合.Services;

/// <summary>
/// 用于持久化界面状态的小型配置对象。
/// 工具输入内容和密钥不会保存在这里。
/// </summary>
public sealed class AppPreferences
{
    public string Theme { get; set; } = "VS Purple";
    public string LastToolId { get; set; } = "base64";
    public double Width { get; set; } = 1100;
    public double Height { get; set; } = 760;
    public bool IsUiSoundEnabled { get; set; } = true;
    public List<string> FavoriteToolIds { get; set; } = [];
    public List<string> RecentToolIds { get; set; } = [];
    public string DefaultOutputDirectory { get; set; } = string.Empty;
    public bool RestoreLastToolOnStartup { get; set; } = true;
    public bool IsNetworkEnabled { get; set; } = true;
    public string OcrSpaceApiKey { get; set; } = "helloworld";
    public string OcrPriority { get; set; } = "OnlineThenWindows";
    public bool SaveTaskHistory { get; set; } = true;
    public int TaskHistoryLimit { get; set; } = 50;
    public string FailedTaskRetryPolicy { get; set; } = "RetryFailedOnly";
}

/// <summary>
/// 在 LocalApplicationData 下读取和保存偏好设置。
/// 配置文件不存在或无法读取时会回退到默认值。
/// </summary>
public sealed class PreferenceService
{
    private readonly AppStateService _appStateService = new();
    private readonly string _legacyFilePath;
    private readonly string _oldLegacyFilePath;

    public PreferenceService()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LCM的工具箱");
        Directory.CreateDirectory(folder);
        _legacyFilePath = Path.Combine(folder, "preferences.json");
        _oldLegacyFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "小工具集合",
            "preferences.json");
    }

    public AppPreferences Load()
    {
        if (_appStateService.StateFileExists)
        {
            return _appStateService.LoadPreferences();
        }

        try
        {
            string legacyPath = File.Exists(_legacyFilePath) ? _legacyFilePath : _oldLegacyFilePath;
            if (!File.Exists(legacyPath))
            {
                return new AppPreferences();
            }

            string json = File.ReadAllText(legacyPath);
            AppPreferences preferences = JsonSerializer.Deserialize<AppPreferences>(json) ?? new AppPreferences();
            _appStateService.SavePreferences(preferences);
            return preferences;
        }
        catch
        {
            // 偏好文件损坏不应影响工具窗口正常打开。
            return new AppPreferences();
        }
    }

    public void Save(AppPreferences preferences)
    {
        _appStateService.SavePreferences(preferences);
    }
}
