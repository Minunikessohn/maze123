#nullable enable

using System.Collections.Generic;
using System.Linq;
using Godot;
using Maze.Game.Settings;
using Maze.Model;
using Maze.Network;

namespace Maze.Game;

public sealed class GameSessionState
{
    public const long OfflinePlayerId = 1;

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
    public SessionRole SessionRole { get; set; } = SessionRole.Offline;
    public ConnectionStatus ConnectionStatus { get; set; } = ConnectionStatus.Offline;
    public string ConnectionMessage { get; set; } = "Keine Sitzung aktiv.";
    public long LocalPeerId { get; set; }
    public List<long> ConnectedPeerIds { get; } = new();
    public Dictionary<long, PlayerRuntimeState> PlayerStates { get; } = new();
    public VisualSettings VisualSettings { get; } = new();
    public AudioSettings AudioSettings { get; } = new();

    public long EffectiveLocalPlayerId => LocalPeerId > 0 ? LocalPeerId : OfflinePlayerId;

    public bool IsPlayerAlive
    {
        get => GetOrCreatePlayerState(EffectiveLocalPlayerId).IsAlive;
        set => GetOrCreatePlayerState(EffectiveLocalPlayerId).IsAlive = value;
    }

    public bool GoalReached
    {
        get => GetOrCreatePlayerState(EffectiveLocalPlayerId).GoalReached;
        set => GetOrCreatePlayerState(EffectiveLocalPlayerId).GoalReached = value;
    }

    public bool IsManualMode
    {
        get => GetOrCreatePlayerState(EffectiveLocalPlayerId).IsManualMode;
        set => GetOrCreatePlayerState(EffectiveLocalPlayerId).IsManualMode = value;
    }

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
        PlayerStates.Clear();
        SetPlayerState(EffectiveLocalPlayerId, new PlayerRuntimeState());
    }

    public void UpdateNetworkSession(SessionRole role, ConnectionStatus status, string message, IEnumerable<long> connectedPeerIds, long localPeerId)
    {
        SessionRole = role;
        ConnectionStatus = status;
        ConnectionMessage = string.IsNullOrWhiteSpace(message) ? "Keine Sitzung aktiv." : message;
        LocalPeerId = localPeerId;
        ConnectedPeerIds.Clear();
        ConnectedPeerIds.AddRange(connectedPeerIds.Distinct());

        if (ConnectedPeerIds.Count == 0)
        {
            ConnectedPeerIds.Add(EffectiveLocalPlayerId);
        }

        EnsurePlayersRegistered(ConnectedPeerIds);
    }

    public PlayerRuntimeState GetOrCreatePlayerState(long peerId)
    {
        long effectivePeerId = peerId > 0 ? peerId : OfflinePlayerId;
        if (!PlayerStates.TryGetValue(effectivePeerId, out PlayerRuntimeState? state))
        {
            state = new PlayerRuntimeState();
            PlayerStates[effectivePeerId] = state;
        }

        return state;
    }

    public bool TryGetPlayerState(long peerId, out PlayerRuntimeState state)
    {
        long effectivePeerId = peerId > 0 ? peerId : OfflinePlayerId;
        return PlayerStates.TryGetValue(effectivePeerId, out state!);
    }

    public void SetPlayerState(long peerId, PlayerRuntimeState state)
    {
        long effectivePeerId = peerId > 0 ? peerId : OfflinePlayerId;
        PlayerStates[effectivePeerId] = ClonePlayerState(state);
    }

    public void EnsurePlayersRegistered(IEnumerable<long> peerIds)
    {
        foreach (long peerId in peerIds)
        {
            GetOrCreatePlayerState(peerId);
        }
    }

    public void RemovePlayerState(long peerId)
    {
        long effectivePeerId = peerId > 0 ? peerId : OfflinePlayerId;
        if (effectivePeerId == EffectiveLocalPlayerId)
        {
            return;
        }

        PlayerStates.Remove(effectivePeerId);
    }

    public void RetainOnlyPlayerState(long peerId)
    {
        long effectivePeerId = peerId > 0 ? peerId : OfflinePlayerId;

        foreach (long existingPeerId in new List<long>(PlayerStates.Keys))
        {
            if (existingPeerId == effectivePeerId)
            {
                continue;
            }

            PlayerStates.Remove(existingPeerId);
        }

        GetOrCreatePlayerState(effectivePeerId);
    }

    public IEnumerable<KeyValuePair<long, PlayerRuntimeState>> EnumerateMonsterTargetStates()
    {
        foreach ((long peerId, PlayerRuntimeState state) in PlayerStates)
        {
            if (!state.IsAlive || !state.IsManualMode)
            {
                continue;
            }

            yield return new KeyValuePair<long, PlayerRuntimeState>(peerId, state);
        }
    }

    private static PlayerRuntimeState ClonePlayerState(PlayerRuntimeState source)
    {
        return new PlayerRuntimeState
        {
            CurrentCell = new MazePointSaveData(source.CurrentCell.X, source.CurrentCell.Y),
            WorldX = source.WorldX,
            WorldY = source.WorldY,
            WorldZ = source.WorldZ,
            RotationY = source.RotationY,
            CurrentStamina = source.CurrentStamina,
            MaximumStamina = source.MaximumStamina,
            IsMoving = source.IsMoving,
            IsSprinting = source.IsSprinting,
            IsAlive = source.IsAlive,
            GoalReached = source.GoalReached,
            IsManualMode = source.IsManualMode
        };
    }
}