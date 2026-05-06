using Godot;

namespace Maze.Game;

public sealed class TrapDefinition
{
    public const string DefaultTrapId = "monster-trap";

    public string TrapId { get; set; } = DefaultTrapId;
    public Vector2I Cell { get; set; }
    public bool IsArmed { get; set; } = true;
}