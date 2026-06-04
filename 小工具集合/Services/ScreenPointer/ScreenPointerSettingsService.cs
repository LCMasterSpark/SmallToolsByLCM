using System.IO;
using System.Text.Json;
using 小工具集合.Services;

namespace 小工具集合.Services.ScreenPointer;

public sealed class ScreenPointerSettingsService
{
    private readonly AppStateService appStateService = new();
    private readonly string legacySettingsPath;
    private readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

    public ScreenPointerSettingsService()
    {
        legacySettingsPath = Path.Combine(AppContext.BaseDirectory, "screen-pointer-settings.json");
    }

    public CrosshairSettings Load()
    {
        if (appStateService.StateFileExists)
        {
            return appStateService.LoadScreenPointer();
        }

        if (!File.Exists(legacySettingsPath))
        {
            return new CrosshairSettings();
        }

        try
        {
            string json = File.ReadAllText(legacySettingsPath);
            CrosshairSettings settings = JsonSerializer.Deserialize<CrosshairSettings>(json) ?? new CrosshairSettings();
            appStateService.SaveScreenPointer(settings);
            return settings;
        }
        catch
        {
            return new CrosshairSettings();
        }
    }

    public void Save(CrosshairSettings settings)
    {
        appStateService.SaveScreenPointer(settings);
    }
}
