// 文件作用：验证扫雷核心规则和按玩家名保存的统计模型。
using 小工具集合.Services.FunLab;

namespace 小工具集合.Tests;

public sealed class MinesweeperGameTests
{
    [Fact]
    public void FirstReveal_PlacesMinesButKeepsClickedCellSafe()
    {
        var game = new MinesweeperGame(rows: 8, columns: 8, mineCount: 10, isArcadeMode: false);

        MinesweeperRevealResult result = game.Reveal(0, 0);

        Assert.True(game.MinesPlaced);
        Assert.False(result.HitMine);
        Assert.False(game.IsMine(0, 0));
        Assert.True(game.IsRevealed(0, 0));
    }

    [Fact]
    public void FlaggingEveryMineWinsNormalMode()
    {
        var game = new MinesweeperGame(rows: 6, columns: 6, mineCount: 6, isArcadeMode: false);
        game.PrepareBoard(safeRow: 0, safeColumn: 0);

        foreach ((int row, int column) in game.EnumerateMines())
        {
            Assert.True(game.ToggleFlag(row, column));
        }

        Assert.Equal(game.MineCount, game.FlagCount);
        Assert.Equal(game.MineCount, game.CorrectFlagCount);
        Assert.True(game.HasWon());
    }

    [Fact]
    public void PlayerStats_NormalizesNameAndRecordsBestTime()
    {
        var state = new MinesweeperState();

        MinesweeperPlayerStats stats = state.GetOrCreatePlayer("  LCM  ");
        stats.RecordResult(isWin: true, TimeSpan.FromSeconds(9.2), "首胜");
        stats.RecordResult(isWin: true, TimeSpan.FromSeconds(12.0), "再胜");
        stats.RecordResult(isWin: false, TimeSpan.FromSeconds(3.0), "踩雷");

        Assert.Equal("LCM", state.CurrentPlayer);
        Assert.Same(stats, state.GetOrCreatePlayer("LCM"));
        Assert.Equal(2, stats.Wins);
        Assert.Equal(1, stats.Losses);
        Assert.Equal(10, stats.BestTimeSeconds);
        Assert.Equal("踩雷", stats.LastResult);
        Assert.NotNull(stats.LastPlayedAt);
        Assert.Equal("LCMasterSpark", MinesweeperState.NormalizePlayerName("   "));
    }
}
