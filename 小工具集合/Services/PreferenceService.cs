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
    public string Theme { get; set; } = "Light";
    public string LastToolId { get; set; } = "base64";
    public double Width { get; set; } = 1100;
    public double Height { get; set; } = 760;
}

/// <summary>
/// 在 LocalApplicationData 下读取和保存偏好设置。
/// 配置文件不存在或无法读取时会回退到默认值。
/// </summary>
public sealed class PreferenceService
{
    private readonly AppStateService _appStateService = new();
    private readonly string _legacyFilePath;

    public PreferenceService()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "小工具集合");
        Directory.CreateDirectory(folder);
        _legacyFilePath = Path.Combine(folder, "preferences.json");
    }

    public AppPreferences Load()
    {
        if (_appStateService.StateFileExists)
        {
            return _appStateService.LoadPreferences();
        }

        try
        {
            if (!File.Exists(_legacyFilePath))
            {
                return new AppPreferences();
            }

            string json = File.ReadAllText(_legacyFilePath);
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
