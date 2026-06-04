// 文件作用：定义扫雷按玩家名保存的战绩状态模型。
namespace 小工具集合.Services.FunLab;

public sealed class MinesweeperState
{
    public string CurrentPlayer { get; set; } = "LCMasterSpark";
    public Dictionary<string, MinesweeperPlayerStats> Players { get; set; } = [];

    public MinesweeperPlayerStats GetOrCreatePlayer(string playerName)
    {
        string normalized = NormalizePlayerName(playerName);
        CurrentPlayer = normalized;
        if (!Players.TryGetValue(normalized, out MinesweeperPlayerStats? stats))
        {
            stats = new MinesweeperPlayerStats();
            Players[normalized] = stats;
        }

        return stats;
    }

    public static string NormalizePlayerName(string? playerName)
    {
        return string.IsNullOrWhiteSpace(playerName) ? "LCMasterSpark" : playerName.Trim();
    }
}

public sealed class MinesweeperPlayerStats
{
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int? BestTimeSeconds { get; set; }
    public string LastResult { get; set; } = "暂无战绩";
    public DateTimeOffset? LastPlayedAt { get; set; }

    public void RecordResult(bool isWin, TimeSpan elapsed, string resultSummary)
    {
        if (isWin)
        {
            Wins++;
            int seconds = Math.Max(1, (int)Math.Ceiling(elapsed.TotalSeconds));
            BestTimeSeconds = BestTimeSeconds is null ? seconds : Math.Min(BestTimeSeconds.Value, seconds);
        }
        else
        {
            Losses++;
        }

        LastResult = resultSummary;
        LastPlayedAt = DateTimeOffset.Now;
    }
}
