namespace 小工具集合.Services.FunLab;

public sealed class MinesweeperGame
{
    private const int ArcadeStartLives = 3;
    private const int ArcadeMaxLives = 3;
    private const double HeartSpawnChance = 0.08;

    private readonly bool[,] mines;
    private readonly bool[,] hearts;
    private readonly bool[,] flagged;
    private readonly bool[,] revealed;
    private readonly int[,] adjacentCounts;
    private readonly Random random = new();

    public MinesweeperGame(int rows, int columns, int mineCount, bool isArcadeMode)
    {
        Rows = rows;
        Columns = columns;
        MineCount = Math.Min(mineCount, rows * columns - 1);
        IsArcadeMode = isArcadeMode;
        Lives = isArcadeMode ? ArcadeStartLives : 0;
        mines = new bool[rows, columns];
        hearts = new bool[rows, columns];
        flagged = new bool[rows, columns];
        revealed = new bool[rows, columns];
        adjacentCounts = new int[rows, columns];
    }

    public int Rows { get; }
    public int Columns { get; }
    public int MineCount { get; }
    public bool IsArcadeMode { get; }
    public int Lives { get; private set; }
    public int MaxLives => ArcadeMaxLives;
    public int FlagCount { get; private set; }
    public int CorrectFlagCount { get; private set; }
    public int RevealedSafeCount { get; private set; }
    public bool MinesPlaced { get; private set; }

    public bool IsMine(int row, int column) => mines[row, column];
    public bool IsFlagged(int row, int column) => flagged[row, column];
    public bool IsRevealed(int row, int column) => revealed[row, column];

    public void PrepareBoard(int? safeRow = null, int? safeColumn = null)
    {
        if (MinesPlaced)
        {
            return;
        }

        int placed = 0;
        while (placed < MineCount)
        {
            int row = random.Next(Rows);
            int column = random.Next(Columns);
            if (mines[row, column] || (safeRow == row && safeColumn == column))
            {
                continue;
            }

            mines[row, column] = true;
            placed++;
        }

        if (IsArcadeMode)
        {
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    hearts[row, column] = !mines[row, column] && random.NextDouble() < HeartSpawnChance;
                }
            }
        }

        CalculateAdjacentCounts();
        MinesPlaced = true;
    }

    public bool ToggleFlag(int row, int column)
    {
        if (revealed[row, column])
        {
            return flagged[row, column];
        }

        flagged[row, column] = !flagged[row, column];
        if (flagged[row, column])
        {
            FlagCount++;
            if (mines[row, column])
            {
                CorrectFlagCount++;
            }
        }
        else
        {
            FlagCount--;
            if (mines[row, column])
            {
                CorrectFlagCount--;
            }
        }

        return flagged[row, column];
    }

    public MinesweeperRevealResult Reveal(int row, int column)
    {
        if (flagged[row, column] || revealed[row, column])
        {
            return new MinesweeperRevealResult([], false, false, false);
        }

        if (!MinesPlaced)
        {
            PrepareBoard(row, column);
        }

        if (mines[row, column])
        {
            revealed[row, column] = true;
            if (!IsArcadeMode)
            {
                return new MinesweeperRevealResult([], true, true, false);
            }

            Lives--;
            return new MinesweeperRevealResult([], true, Lives <= 0, false);
        }

        var cells = new List<MinesweeperCellReveal>();
        RevealSafeCell(row, column, cells);
        return new MinesweeperRevealResult(cells, false, false, HasWon());
    }

    public bool HasWon()
    {
        bool allSafeRevealed = RevealedSafeCount == Rows * Columns - MineCount;
        bool allMinesFlagged = !IsArcadeMode && CorrectFlagCount == MineCount && FlagCount == MineCount;
        return allSafeRevealed || allMinesFlagged;
    }

    public IEnumerable<(int Row, int Column)> EnumerateMines()
    {
        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                if (mines[row, column])
                {
                    yield return (row, column);
                }
            }
        }
    }

    private void RevealSafeCell(int row, int column, List<MinesweeperCellReveal> cells)
    {
        if (row < 0 || row >= Rows || column < 0 || column >= Columns)
        {
            return;
        }

        if (revealed[row, column] || flagged[row, column] || mines[row, column])
        {
            return;
        }

        revealed[row, column] = true;
        RevealedSafeCount++;
        bool recoveredLife = false;
        if (hearts[row, column])
        {
            int previousLives = Lives;
            Lives = Math.Min(Lives + 1, ArcadeMaxLives);
            hearts[row, column] = false;
            recoveredLife = Lives > previousLives;
        }

        int adjacentCount = adjacentCounts[row, column];
        cells.Add(new MinesweeperCellReveal(row, column, adjacentCount, recoveredLife));
        if (adjacentCount != 0)
        {
            return;
        }

        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0)
                {
                    continue;
                }

                RevealSafeCell(row + dr, column + dc, cells);
            }
        }
    }

    private void CalculateAdjacentCounts()
    {
        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                if (mines[row, column])
                {
                    adjacentCounts[row, column] = -1;
                    continue;
                }

                int count = 0;
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0)
                        {
                            continue;
                        }

                        int r = row + dr;
                        int c = column + dc;
                        if (r >= 0 && r < Rows && c >= 0 && c < Columns && mines[r, c])
                        {
                            count++;
                        }
                    }
                }

                adjacentCounts[row, column] = count;
            }
        }
    }
}

public sealed record MinesweeperCellReveal(int Row, int Column, int AdjacentCount, bool RecoveredLife);

public sealed record MinesweeperRevealResult(IReadOnlyList<MinesweeperCellReveal> RevealedCells, bool HitMine, bool GameOver, bool Won);
