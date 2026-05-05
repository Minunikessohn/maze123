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
    public Vector2I StartCell { get; set; } = Vector2I.Zero;
    public Vector2I GoalCell { get; set; } = Vector2I.Zero;
    public List<Vector2I> TrapCells { get; set; } = new();
    public List<Vector2I> MonsterSpawnCells { get; set; } = new();
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