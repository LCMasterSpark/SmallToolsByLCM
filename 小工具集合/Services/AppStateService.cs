using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using 小工具集合.Services.FunLab;
using 小工具集合.Services.ScreenPointer;

namespace 小工具集合.Services;

public sealed class AppState
{
    public AppPreferences Preferences { get; set; } = new();
    public CrosshairSettings ScreenPointer { get; set; } = new();
    public MinesweeperState Minesweeper { get; set; } = new();
}

public sealed class AppStateService
{
    private readonly string filePath;
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public AppStateService()
    {
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        filePath = Path.Combine(AppContext.BaseDirectory, "app-state.json");
    }

    public bool StateFileExists => File.Exists(filePath);

    public AppPreferences LoadPreferences()
    {
        return LoadState().Preferences ?? new AppPreferences();
    }

    public CrosshairSettings LoadScreenPointer()
    {
        return LoadState().ScreenPointer ?? new CrosshairSettings();
    }

    public MinesweeperState LoadMinesweeper()
    {
        return LoadState().Minesweeper ?? new MinesweeperState();
    }

    public void SavePreferences(AppPreferences preferences)
    {
        AppState state = LoadState();
        state.Preferences = preferences;
        SaveState(state);
    }

    public void SaveScreenPointer(CrosshairSettings settings)
    {
        AppState state = LoadState();
        state.ScreenPointer = settings;
        SaveState(state);
    }

    public void SaveMinesweeper(MinesweeperState minesweeper)
    {
        AppState state = LoadState();
        state.Minesweeper = minesweeper;
        SaveState(state);
    }

    private AppState LoadState()
    {
        if (!File.Exists(filePath))
        {
            return new AppState();
        }

        try
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<AppState>(json, jsonOptions) ?? new AppState();
        }
        catch
        {
            return new AppState();
        }
    }

    private void SaveState(AppState state)
    {
        string json = JsonSerializer.Serialize(state, jsonOptions);
        File.WriteAllText(filePath, json);
    }
}
