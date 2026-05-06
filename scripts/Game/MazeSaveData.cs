using System;
using System.Collections.Generic;
using Godot;

namespace Maze.Game;

public sealed class MazeSaveData
{
    public string SaveId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public MazeGameConfig Config { get; set; } = new();
    public List<MazeCellSaveData> Cells { get; set; } = new();
    public MazePointSaveData StartCell { get; set; } = new();
    public MazePointSaveData GoalCell { get; set; } = new();
    public List<TrapSaveData> Traps { get; set; } = new();
    public List<MazePointSaveData> MonsterSpawnCells { get; set; } = new();
}

public sealed class MazeCellSaveData
{
    public int X { get; set; }
    public int Y { get; set; }
    public bool NorthWall { get; set; } = true;
    public bool EastWall { get; set; } = true;
    public bool SouthWall { get; set; } = true;
    public bool WestWall { get; set; } = true;
}

public sealed class MazePointSaveData
{
    public int X { get; set; }
    public int Y { get; set; }

    public MazePointSaveData()
    {
    }

    public MazePointSaveData(int x, int y)
    {
        X = x;
        Y = y;
    }

    public Vector2I ToVector2I() => new(X, Y);

    public static MazePointSaveData FromVector2I(Vector2I point) => new(point.X, point.Y);
}

public sealed class TrapSaveData
{
    public string TrapId { get; set; } = TrapDefinition.DefaultTrapId;
    public MazePointSaveData Cell { get; set; } = new();
    public bool IsArmed { get; set; } = true;
}