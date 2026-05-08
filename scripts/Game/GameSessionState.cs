#nullable enable

using System.Collections.Generic;
using Godot;
using Maze.Game.Settings;
using Maze.Model;
using Maze.Network;

namespace Maze.Game;

public sealed class GameSessionState
{
    public GameFlowState FlowState { get; set; } = GameFlowState.Boot;
    public MazeGameConfig? CurrentConfig { get; private set; }
    public global::Maze.Model.Maze? CurrentMaze { get; private set; }
    public Cell? StartCell { get; set; }
    public Cell? GoalCell { get; set; }
    public List<Vector2I> MonsterSpawnCells { get; } = new();
    public List<TrapDefinition> TrapDefinitions { get; } = new();
    public List<Vector2I> ActiveMonsterCells { get; } = new();
    public List<Vector2I> ActiveTrapCells { get; } = new();
    public float DayNightProgress { get; set; }
    public bool IsRunning { get; set; }
    public bool IsPaused { get; set; }
    public bool IsPlayerAlive { get; set; } = true;
    public bool GoalReached { get; set; }
    public bool IsManualMode { get; set; }
    public SessionRole SessionRole { get; set; } = SessionRole.Offline;
    public ConnectionStatus ConnectionStatus { get; set; } = ConnectionStatus.Offline;
    public string ConnectionMessage { get; set; } = "Keine Sitzung aktiv.";
    public long LocalPeerId { get; set; }
    public List<long> ConnectedPeerIds { get; } = new();
    public VisualSettings VisualSettings { get; } = new();
    public AudioSettings AudioSettings { get; } = new();

    public void ResetForNewGame(MazeGameConfig config, global::Maze.Model.Maze maze)
    {
        CurrentConfig = config.Clone().Sanitize();
        CurrentMaze = maze;
        FlowState = GameFlowState.Loading;
        StartCell = null;
        GoalCell = null;
        MonsterSpawnCells.Clear();
        TrapDefinitions.Clear();
        ActiveMonsterCells.Clear();
        ActiveTrapCells.Clear();
        DayNightProgress = 0f;
        IsRunning = false;
        IsPaused = false;
        IsPlayerAlive = true;
        GoalReached = false;
        IsManualMode = false;
    }

    public void UpdateNetworkSession(SessionRole role, ConnectionStatus status, string message, IEnumerable<long> connectedPeerIds, long localPeerId)
    {
        SessionRole = role;
        ConnectionStatus = status;
        ConnectionMessage = string.IsNullOrWhiteSpace(message) ? "Keine Sitzung aktiv." : message;
        LocalPeerId = localPeerId;
        ConnectedPeerIds.Clear();
        ConnectedPeerIds.AddRange(connectedPeerIds);
    }
}