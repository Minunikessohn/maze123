#nullable enable

using Maze.Game;

namespace Maze.Network;

public sealed class PlayerIdentity
{
    public long PeerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int PlayerSlot { get; set; }
    public bool IsHost { get; set; }
    public MazePointSaveData AssignedSpawnCell { get; set; } = new();
}