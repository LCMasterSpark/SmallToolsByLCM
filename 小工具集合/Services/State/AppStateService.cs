// 文件作用：提供统一 app-state.json 的读取、分段更新和容错回退。
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
    public TaskHistoryState Tasks { get; set; } = new();
    public TranslationSettings Translation { get; set; } = new();
}

public sealed class TranslationSettings
{
    public string DefaultProvider { get; set; } = "LibreTranslate";
    public string DefaultSourceLanguage { get; set; } = "自动";
    public string DefaultTargetLanguage { get; set; } = "中文";
    public string LibreTranslateEndpoint { get; set; } = "https://libretranslate.com/translate";
    public string LibreTranslateApiKey { get; set; } = string.Empty;
    public string AzureEndpoint { get; set; } = string.Empty;
    public string AzureRegion { get; set; } = string.Empty;
    public string AzureKey { get; set; } = string.Empty;
    public string DeepLApiUrl { get; set; } = "https://api-free.deepl.com/v2/translate";
    public string DeepLApiKey { get; set; } = string.Empty;
    public string GoogleApiKey { get; set; } = string.Empty;
    public string BaiduAppId { get; set; } = string.Empty;
    public string BaiduSecret { get; set; } = string.Empty;
    public string YoudaoAppKey { get; set; } = string.Empty;
    public string YoudaoAppSecret { get; set; } = string.Empty;
    public string OpenAiBaseUrl { get; set; } = "https://api.openai.com/v1";
    public string OpenAiApiKey { get; set; } = string.Empty;
    public string OpenAiModel { get; set; } = "gpt-4o-mini";
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "qwen2.5:7b";
    public bool UseGlossary { get; set; }
    public string Glossary { get; set; } = string.Empty;
    public int LiveRefreshMilliseconds { get; set; } = 1500;
    public double LiveOverlayOpacity { get; set; } = 0.9;
    public bool LiveOverlayClickThrough { get; set; } = true;
    public bool LiveOverlayFollowTargetWindow { get; set; }
}

public sealed class TaskHistoryState
{
    public List<TaskHistoryRecord> Records { get; set; } = [];
}

public sealed class TaskHistoryRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ToolId { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset FinishedAt { get; set; } = DateTimeOffset.Now;
    public string OutputDirectory { get; set; } = string.Empty;
    public List<string> InputFiles { get; set; } = [];
    public List<string> FailedFiles { get; set; } = [];
    public Dictionary<string, string> Parameters { get; set; } = [];
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public bool Succeeded => FailureCount == 0;
    public bool CanRetry => FailedFiles.Count > 0;
    public string StatusLabel => Succeeded ? "成功" : "失败";
    public string SummaryText => $"{SuccessCount} 成功 / {FailureCount} 失败 / {InputFiles.Count} 文件";
    public string FinishedAtText => FinishedAt.ToLocalTime().ToString("MM-dd HH:mm:ss");
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

    public TaskHistoryState LoadTasks()
    {
        return LoadState().Tasks ?? new TaskHistoryState();
    }

    public TranslationSettings LoadTranslation()
    {
        return LoadState().Translation ?? new TranslationSettings();
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

    public void SaveTasks(TaskHistoryState tasks)
    {
        AppState state = LoadState();
        state.Tasks = tasks;
        SaveState(state);
    }

    public void SaveTranslation(TranslationSettings translation)
    {
        AppState state = LoadState();
        state.Translation = translation;
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
