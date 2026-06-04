// 文件作用：连接扫雷 UI、游戏规则、计时器和玩家战绩保存。
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using 小工具集合.Services;
using 小工具集合.Services.FunLab;

namespace 小工具集合.Views.FunLab;

public partial class MinesweeperControl : UserControl, IInteractiveToolView, IDisposable
{
    private const double CellSize = 34;
    private const int ArcadeSafeCellCount = 40;
    private static readonly Brush HiddenCellBrush = new SolidColorBrush(Color.FromRgb(52, 54, 59));
    private static readonly Brush RevealedCellBrush = new SolidColorBrush(Color.FromRgb(174, 174, 174));
    private static readonly Brush MineCellBrush = new SolidColorBrush(Color.FromRgb(230, 68, 68));
    private static readonly Brush FlagCellBrush = new SolidColorBrush(Color.FromRgb(167, 118, 16));
    private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(241, 241, 241));
    private static readonly Brush CellBorderBrush = new SolidColorBrush(Color.FromRgb(92, 96, 104));
    private static readonly Brush RevealedCellBorderBrush = new SolidColorBrush(Color.FromRgb(218, 218, 218));
    private static readonly Brush MineTextBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
    private static readonly Brush FlagTextBrush = new SolidColorBrush(Color.FromRgb(255, 244, 166));
    private static readonly Brush LifeTextBrush = new SolidColorBrush(Color.FromRgb(202, 25, 58));

    private readonly AppStateService appStateService = new();
    private readonly DispatcherTimer timer;
    private MinesweeperState minesweeperState = new();
    private MinesweeperPlayerStats currentPlayerStats = new();
    private string currentPlayer = "LCMasterSpark";
    private MinesweeperGame? game;
    private Button[,] buttons = new Button[0, 0];
    private DateTime startTime;
    private bool gameEnded;

    public MinesweeperControl()
    {
        InitializeComponent();
        LoadSavedState();
        DifficultyComboBox.ItemsSource = new[] { "宝宝模式", "简单", "中等", "困难", "巨型棋盘" };
        DifficultyComboBox.SelectedIndex = 1;
        BoardSizeComboBox.ItemsSource = new[] { "5 x 5", "8 x 8", "12 x 12", "15 x 15" };
        BoardSizeComboBox.SelectedIndex = 2;
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => UpdateTimer();
        StartNewGame();
        Unloaded += (_, _) => Deactivate();
    }

    public void Deactivate()
    {
        timer.Stop();
        SaveState();
    }

    public void Dispose()
    {
        Deactivate();
    }

    private void NewGameButton_Click(object sender, RoutedEventArgs e)
    {
        CommitPlayerName();
        StartNewGame();
    }

    private void PlayerNameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        CommitPlayerName();
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void PlayerNameTextBox_Commit(object sender, RoutedEventArgs e)
    {
        CommitPlayerName();
    }

    private void ArcadeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }

        DifficultyComboBox.IsEnabled = ArcadeCheckBox.IsChecked != true;
        if (ArcadeCheckBox.IsChecked == true && BoardSizeComboBox.SelectedIndex == 0)
        {
            BoardSizeComboBox.SelectedIndex = 1;
        }

        StartNewGame();
    }

    private void StartNewGame()
    {
        int size = GetBoardSize();
        bool isArcade = ArcadeCheckBox.IsChecked == true;
        if (isArcade && size == 5)
        {
            size = 8;
            BoardSizeComboBox.SelectedIndex = 1;
        }

        int mines = isArcade ? size * size - ArcadeSafeCellCount : GetMineCount(size);
        game = new MinesweeperGame(size, size, mines, isArcade);
        buttons = new Button[size, size];
        BoardGrid.Rows = size;
        BoardGrid.Columns = size;
        BoardGrid.Children.Clear();
        gameEnded = false;
        startTime = DateTime.Now;
        timer.Start();

        for (int row = 0; row < size; row++)
        {
            for (int column = 0; column < size; column++)
            {
                Button button = CreateCellButton(row, column);
                buttons[row, column] = button;
                BoardGrid.Children.Add(button);
            }
        }

        StatusText.Text = isArcade ? "街机模式：3 条生命，固定 40 个安全格。" : "新局已开始，第一步会避开地雷。";
        LastResultText.Text = string.IsNullOrWhiteSpace(currentPlayerStats.LastResult)
            ? $"以玩家名保存战绩：{currentPlayer}"
            : $"{currentPlayer} 最近：{currentPlayerStats.LastResult}";
        UpdateStatsText();
        UpdateTimer();
    }

    private Button CreateCellButton(int row, int column)
    {
        var button = new Button
        {
            Width = CellSize,
            Height = CellSize,
            Margin = new Thickness(2),
            Tag = (row, column),
            Background = HiddenCellBrush,
            Foreground = TextBrush,
            BorderBrush = CellBorderBrush,
            BorderThickness = new Thickness(1),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Content = string.Empty,
            Cursor = Cursors.Hand
        };
        button.Click += CellButton_Click;
        button.MouseRightButtonUp += CellButton_RightClick;
        return button;
    }

    private void CellButton_Click(object sender, RoutedEventArgs e)
    {
        if (game is null || gameEnded || sender is not Button button)
        {
            return;
        }

        var (row, column) = ((int Row, int Column))button.Tag;
        MinesweeperRevealResult result = game.Reveal(row, column);
        if (result.HitMine)
        {
            button.Content = "💣";
            button.Background = MineCellBrush;
            button.BorderBrush = MineTextBrush;
            button.Foreground = MineTextBrush;
            if (result.GameOver)
            {
                EndGame(false, game.IsArcadeMode ? "生命耗尽，街机模式结束。" : "踩到地雷，游戏结束。");
            }
            else
            {
                StatusText.Text = $"踩到地雷，剩余生命：{game.Lives}/{game.MaxLives}。";
                UpdateStatsText();
            }

            return;
        }

        foreach (MinesweeperCellReveal cell in result.RevealedCells)
        {
            Button revealedButton = buttons[cell.Row, cell.Column];
            revealedButton.IsHitTestVisible = false;
            revealedButton.Background = RevealedCellBrush;
            revealedButton.BorderBrush = RevealedCellBorderBrush;
            revealedButton.Content = GetRevealedCellContent(cell);
            revealedButton.Foreground = GetRevealedCellBrush(cell);
            if (cell.RecoveredLife)
            {
                StatusText.Text = $"获得补给，生命：{game.Lives}/{game.MaxLives}。";
            }
        }

        if (game.HasWon() || result.Won)
        {
            EndGame(true, "成功排除所有地雷。");
        }
        else if (result.RevealedCells.Count > 0 && !result.RevealedCells.Any(cell => cell.RecoveredLife))
        {
            StatusText.Text = "继续排查。";
        }

        UpdateStatsText();
    }

    private void CellButton_RightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (game is null || gameEnded || sender is not Button button)
        {
            return;
        }

        var (row, column) = ((int Row, int Column))button.Tag;
        if (!game.MinesPlaced)
        {
            StatusText.Text = "请先左键揭开第一格。";
            return;
        }

        if (game.IsRevealed(row, column))
        {
            return;
        }

        bool isFlagged = game.ToggleFlag(row, column);
        button.Content = isFlagged ? "🚩" : string.Empty;
        button.Background = isFlagged ? FlagCellBrush : HiddenCellBrush;
        button.BorderBrush = isFlagged ? FlagTextBrush : CellBorderBrush;
        button.Foreground = isFlagged ? FlagTextBrush : TextBrush;
        if (game.HasWon())
        {
            EndGame(true, "所有地雷都被正确标记。");
        }
        else
        {
            StatusText.Text = isFlagged ? "已标记可疑格。" : "已取消标记。";
        }

        UpdateStatsText();
    }

    private void EndGame(bool isWin, string message)
    {
        if (game is null)
        {
            return;
        }

        gameEnded = true;
        timer.Stop();
        TimeSpan elapsed = DateTime.Now - startTime;
        if (!isWin)
        {
            RevealAllMines();
        }

        foreach (Button button in buttons)
        {
            button.IsHitTestVisible = false;
        }

        StatusText.Text = message;
        string resultSummary = $"{(isWin ? "胜利" : "失败")} | 用时：{elapsed:mm\\:ss} | 标记：{game.FlagCount}/{game.MineCount} | 安全格：{game.RevealedSafeCount}";
        currentPlayerStats.RecordResult(isWin, elapsed, resultSummary);
        LastResultText.Text = $"{currentPlayer}：{resultSummary}";
        SaveState();
        UpdateStatsText();
    }

    private void RevealAllMines()
    {
        if (game is null)
        {
            return;
        }

        foreach ((int row, int column) in game.EnumerateMines())
        {
            buttons[row, column].Content = "💣";
            buttons[row, column].Background = MineCellBrush;
            buttons[row, column].BorderBrush = MineTextBrush;
            buttons[row, column].Foreground = MineTextBrush;
        }
    }

    private static string GetRevealedCellContent(MinesweeperCellReveal cell)
    {
        if (cell.RecoveredLife)
        {
            return "❤️";
        }

        return cell.AdjacentCount > 0 ? cell.AdjacentCount.ToString() : string.Empty;
    }

    private static Brush GetRevealedCellBrush(MinesweeperCellReveal cell)
    {
        if (cell.RecoveredLife)
        {
            return LifeTextBrush;
        }

        return cell.AdjacentCount switch
        {
            1 => new SolidColorBrush(Color.FromRgb(0, 96, 223)),
            2 => new SolidColorBrush(Color.FromRgb(0, 128, 64)),
            3 => new SolidColorBrush(Color.FromRgb(196, 43, 28)),
            4 => new SolidColorBrush(Color.FromRgb(98, 60, 210)),
            5 => new SolidColorBrush(Color.FromRgb(176, 64, 0)),
            6 => new SolidColorBrush(Color.FromRgb(0, 120, 130)),
            7 => new SolidColorBrush(Color.FromRgb(92, 92, 92)),
            8 => new SolidColorBrush(Color.FromRgb(32, 32, 32)),
            _ => TextBrush
        };
    }

    private int GetBoardSize()
    {
        return BoardSizeComboBox.SelectedIndex switch
        {
            0 => 5,
            1 => 8,
            3 => 15,
            _ => 12
        };
    }

    private int GetMineCount(int size)
    {
        int requested = DifficultyComboBox.SelectedIndex switch
        {
            0 => 5,
            2 => 25,
            3 => 50,
            4 => 110,
            _ => 10
        };

        return Math.Min(requested, size * size - 1);
    }

    private void UpdateStatsText()
    {
        if (game is null)
        {
            return;
        }

        RunStatsText.Text = $"{currentPlayer}：{currentPlayerStats.Wins} 胜 / {currentPlayerStats.Losses} 负";
        MineText.Text = $"雷数：{game.MineCount}";
        FlagText.Text = $"标记：{game.FlagCount}/{game.MineCount}";
        LifeText.Text = game.IsArcadeMode ? $"生命：{Math.Max(game.Lives, 0)}/{game.MaxLives}" : "生命：普通模式";
        BestTimeText.Text = $"最佳：{FormatBestTime(currentPlayerStats.BestTimeSeconds)}";
    }

    private void UpdateTimer()
    {
        TimerText.Text = $"用时：{DateTime.Now - startTime:mm\\:ss}";
    }

    private void LoadSavedState()
    {
        minesweeperState = appStateService.LoadMinesweeper();
        currentPlayer = MinesweeperState.NormalizePlayerName(minesweeperState.CurrentPlayer);
        currentPlayerStats = minesweeperState.GetOrCreatePlayer(currentPlayer);
        PlayerNameTextBox.Text = currentPlayer;
        SaveState();
    }

    private void CommitPlayerName()
    {
        string normalized = MinesweeperState.NormalizePlayerName(PlayerNameTextBox.Text);
        if (normalized == currentPlayer)
        {
            PlayerNameTextBox.Text = normalized;
            return;
        }

        currentPlayer = normalized;
        currentPlayerStats = minesweeperState.GetOrCreatePlayer(currentPlayer);
        PlayerNameTextBox.Text = currentPlayer;
        LastResultText.Text = $"{currentPlayer} 最近：{currentPlayerStats.LastResult}";
        SaveState();
        UpdateStatsText();
    }

    private void SaveState()
    {
        minesweeperState.CurrentPlayer = currentPlayer;
        minesweeperState.Players[currentPlayer] = currentPlayerStats;
        appStateService.SaveMinesweeper(minesweeperState);
    }

    private static string FormatBestTime(int? bestTimeSeconds)
    {
        if (bestTimeSeconds is null)
        {
            return "-";
        }

        return TimeSpan.FromSeconds(bestTimeSeconds.Value).ToString("mm\\:ss");
    }
}
