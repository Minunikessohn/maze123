#nullable enable

using Godot;
using Maze.Game;

namespace Maze.Network;

public sealed class PlayerRuntimeState
{
    public MazePointSaveData CurrentCell { get; set; } = new();
    public float WorldX { get; set; }
    public float WorldY { get; set; }
    public float WorldZ { get; set; }
    public float RotationY { get; set; }
    public float CurrentStamina { get; set; } = 1f;
    public float MaximumStamina { get; set; } = 1f;
    public bool IsMoving { get; set; }
    public bool IsSprinting { get; set; }
    public bool IsAlive { get; set; } = true;
    public bool GoalReached { get; set; }
    public bool IsManualMode { get; set; }

    public Vector3 GetWorldPosition() => new(WorldX, WorldY, WorldZ);

    public void SetWorldPosition(Vector3 worldPosition)
    {
        WorldX = worldPosition.X;
        WorldY = worldPosition.Y;
        WorldZ = worldPosition.Z;
    }
}