#nullable enable

namespace Maze.Network;

public sealed class PlayerSnapshot
{
    public PlayerIdentity Identity { get; set; } = new();
    public PlayerRuntimeState RuntimeState { get; set; } = new();
}