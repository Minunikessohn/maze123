#nullable enable

using System;
using System.Collections.Generic;
using Maze.Game;

namespace Maze.Network;

public sealed class SessionStartPayload
{
    public const string CurrentContractVersion = "phase3-v1";

    public string ContractVersion { get; set; } = CurrentContractVersion;
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public long HostPeerId { get; set; }
    public long RecipientPeerId { get; set; }
    public bool IsAuthoritativeHostStart { get; set; } = true;
    public MazeGameConfig GameConfig { get; set; } = new();
    public GameWorldSnapshot World { get; set; } = new();
    public List<PlayerSnapshot> Players { get; set; } = new();
}