#nullable enable

using Maze.Game;

namespace Maze.Network;

public sealed class GameWorldSnapshot
{
    public MazeSaveData SaveData { get; set; } = new();
    public GameFlowState FlowState { get; set; } = GameFlowState.Loading;
    public float DayNightProgress { get; set; }
    public bool IsManualMode { get; set; }
    public bool GoalReached { get; set; }
}