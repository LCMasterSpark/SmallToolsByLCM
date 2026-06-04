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
            Height = 800
        };
        var minesweeper = new MinesweeperState();
        minesweeper.GetOrCreatePlayer("Tester").RecordResult(true, TimeSpan.FromSeconds(7), "测试胜利");

        service.SavePreferences(preferences);
        service.SaveMinesweeper(minesweeper);

        AppPreferences loadedPreferences = service.LoadPreferences();
        MinesweeperState loadedMinesweeper = service.LoadMinesweeper();

        Assert.True(File.Exists(stateFilePath));
        Assert.Equal("qrCode", loadedPreferences.LastToolId);
        Assert.Equal(1280, loadedPreferences.Width);
        Assert.Equal("Tester", loadedMinesweeper.CurrentPlayer);
        Assert.True(loadedMinesweeper.Players.ContainsKey("Tester"));
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
