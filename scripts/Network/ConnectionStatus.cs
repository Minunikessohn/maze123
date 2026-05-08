#nullable enable

namespace Maze.Network;

public enum ConnectionStatus
{
    Offline,
    Starting,
    Hosting,
    Connecting,
    Connected,
    Error
}