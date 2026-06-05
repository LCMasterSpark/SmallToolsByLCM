// 文件作用：验证统一 app-state.json 的分段保存和损坏文件容错行为。
using System.IO;
using 小工具集合.Services;
using 小工具集合.Services.FunLab;

namespace 小工具集合.Tests;

public sealed class AppStateServiceTests : IDisposable
{
    private readonly string stateFilePath = Path.Combine(AppContext.BaseDirectory, "app-state.json");

    public AppStateServiceTests()
    {
        DeleteStateFile();
    }

    public void Dispose()
    {
        DeleteStateFile();
    }

    [Fact]
    public void SaveSections_PreservesExistingState()
    {
        var service = new AppStateService();
        var preferences = new AppPreferences
        {
            Theme = "Dark",
            LastToolId = "qrCode",
            Width = 1280,
            Height = 800,
            IsUiSoundEnabled = false,
            FavoriteToolIds = ["qrCode"],
            RecentToolIds = ["screenshotOcr", "qrCode"],
            IsNetworkEnabled = false,
            OcrSpaceApiKey = "test-key",
            TaskHistoryLimit = 12
        };
        var tasks = new TaskHistoryState
        {
            Records =
            [
                new TaskHistoryRecord
                {
                    ToolId = "csvCleaner",
                    ToolName = "CSV 清洗",
                    InputFiles = ["a.csv"],
                    FailedFiles = ["a.csv"],
                    FailureCount = 1,
                    ErrorMessage = "failed"
                }
            ]
        };
        var minesweeper = new MinesweeperState();
        minesweeper.GetOrCreatePlayer("Tester").RecordResult(true, TimeSpan.FromSeconds(7), "测试胜利");
        var translation = new TranslationSettings
        {
            DefaultProvider = "Ollama",
            OllamaEndpoint = "http://localhost:11434",
            OllamaModel = "qwen-test"
        };

        service.SavePreferences(preferences);
        service.SaveTasks(tasks);
        service.SaveMinesweeper(minesweeper);
        service.SaveTranslation(translation);

        AppPreferences loadedPreferences = service.LoadPreferences();
        TaskHistoryState loadedTasks = service.LoadTasks();
        MinesweeperState loadedMinesweeper = service.LoadMinesweeper();
        TranslationSettings loadedTranslation = service.LoadTranslation();

        Assert.True(File.Exists(stateFilePath));
        Assert.Equal("qrCode", loadedPreferences.LastToolId);
        Assert.Equal(1280, loadedPreferences.Width);
        Assert.False(loadedPreferences.IsUiSoundEnabled);
        Assert.Contains("qrCode", loadedPreferences.FavoriteToolIds);
        Assert.Contains("screenshotOcr", loadedPreferences.RecentToolIds);
        Assert.False(loadedPreferences.IsNetworkEnabled);
        Assert.Equal("test-key", loadedPreferences.OcrSpaceApiKey);
        Assert.Single(loadedTasks.Records);
        Assert.Equal("csvCleaner", loadedTasks.Records[0].ToolId);
        Assert.Equal("Tester", loadedMinesweeper.CurrentPlayer);
        Assert.True(loadedMinesweeper.Players.ContainsKey("Tester"));
        Assert.Equal("Ollama", loadedTranslation.DefaultProvider);
        Assert.Equal("qwen-test", loadedTranslation.OllamaModel);
    }

    [Fact]
    public void CorruptStateFile_FallsBackToDefaults()
    {
        File.WriteAllText(stateFilePath, "{ this is not valid json");

        var service = new AppStateService();
        AppPreferences preferences = service.LoadPreferences();
        MinesweeperState minesweeper = service.LoadMinesweeper();

        Assert.Equal("base64", preferences.LastToolId);
        Assert.Equal("LCMasterSpark", minesweeper.CurrentPlayer);
        Assert.Empty(minesweeper.Players);
    }

    private void DeleteStateFile()
    {
        if (File.Exists(stateFilePath))
        {
            File.Delete(stateFilePath);
        }
    }
}
