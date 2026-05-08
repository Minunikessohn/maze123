#nullable enable

using System;
using System.Collections.Generic;

namespace Maze.Network;

public sealed class PlayerSnapshotBatch
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<PlayerSnapshot> Players { get; set; } = new();
}