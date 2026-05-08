#nullable enable

using System;
using System.Collections.Generic;
using Maze.Game;
using Godot;

namespace Maze.Network;

public sealed class WorldRuntimeSnapshot
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public GameFlowState FlowState { get; set; } = GameFlowState.Loading;
    public float DayNightProgress { get; set; }
    public List<MazePointSaveData> ActiveMonsterCells { get; set; } = new();
    public List<MazePointSaveData> ActiveTrapCells { get; set; } = new();
}
