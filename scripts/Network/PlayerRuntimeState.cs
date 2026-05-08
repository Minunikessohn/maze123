#nullable enable

using Maze.Game;

namespace Maze.Network;

public sealed class PlayerRuntimeState
{
    public MazePointSaveData CurrentCell { get; set; } = new();
    public float CurrentStamina { get; set; } = 1f;
    public float MaximumStamina { get; set; } = 1f;
    public bool IsAlive { get; set; } = true;
    public bool GoalReached { get; set; }
    public bool IsManualMode { get; set; }
}