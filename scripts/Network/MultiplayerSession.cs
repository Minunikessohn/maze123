#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using Maze.Game;

namespace Maze.Network;

public partial class MultiplayerSession : Node
{
    private const int DefaultMaxClients = 4;
    private const long HostPeerId = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private ENetMultiplayerPeer? _peer;
    private readonly HashSet<long> _connectedPeers = new();
    private readonly Dictionary<long, string> _registeredPlayerNames = new();
    private readonly HashSet<long> _synchronizedPeers = new();
    private string _localPlayerName = "Spieler";
    private string _activeSessionStartId = string.Empty;

    public SessionRole Role { get; private set; } = SessionRole.Offline;
    public ConnectionStatus Status { get; private set; } = ConnectionStatus.Offline;
    public string StatusMessage { get; private set; } = "Keine Sitzung aktiv.";
    public long LocalPeerId => Multiplayer.MultiplayerPeer is null ? 0L : Multiplayer.GetUniqueId();
    public IReadOnlyCollection<long> ConnectedPeerIds => _connectedPeers;

    public event Action<SessionRole, ConnectionStatus, string>? StateChanged;
    public event Action<long>? PeerJoined;
    public event Action<long>? PeerLeft;
    public event Action<SessionStartPayload>? SessionStartReceived;
    public event Action<long, string>? SessionStartAcknowledged;
    public event Action<long, PlayerSnapshot>? ClientPlayerSnapshotReceived;
    public event Action<PlayerSnapshotBatch>? PlayerSnapshotBatchReceived;
    public event Action<WorldRuntimeSnapshot>? WorldSnapshotReceived;

    public override void _Ready()
    {
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
    }

    public override void _ExitTree()
    {
        Multiplayer.PeerConnected -= OnPeerConnected;
        Multiplayer.PeerDisconnected -= OnPeerDisconnected;
        Multiplayer.ConnectedToServer -= OnConnectedToServer;
        Multiplayer.ConnectionFailed -= OnConnectionFailed;
        Multiplayer.ServerDisconnected -= OnServerDisconnected;
        ReleasePeer();
    }

    public Error StartHost(string playerName, int port, int maxClients = DefaultMaxClients)
    {
        StopSession();

        if (!IsValidPort(port))
        {
            return Fail("Ungueltiger Port fuer Host-Session.");
        }

        _localPlayerName = SanitizePlayerName(playerName, "Host");

        ApplyState(SessionRole.Host, ConnectionStatus.Starting, $"Host startet auf Port {port}...");
        ENetMultiplayerPeer peer = new();
        Error result = peer.CreateServer(port, maxClients);
        if (result != Error.Ok)
        {
            peer.Dispose();
            return Fail($"Host konnte nicht gestartet werden: {result}");
        }

        _peer = peer;
        Multiplayer.MultiplayerPeer = peer;
        _connectedPeers.Clear();
        long localPeerId = Multiplayer.GetUniqueId();
        if (localPeerId > 0)
        {
            _connectedPeers.Add(localPeerId);
            RegisterPlayerName(localPeerId, _localPlayerName);
            _synchronizedPeers.Add(localPeerId);
        }

        ApplyState(SessionRole.Host, ConnectionStatus.Hosting, $"Host aktiv auf Port {port}. Warte auf Clients.");
        return Error.Ok;
    }

    public Error JoinSession(string playerName, string address, int port)
    {
        StopSession();

        if (string.IsNullOrWhiteSpace(address))
        {
            return Fail("Bitte eine gueltige Host-Adresse eintragen.");
        }

        if (!IsValidPort(port))
        {
            return Fail("Ungueltiger Port fuer Client-Verbindung.");
        }

        _localPlayerName = SanitizePlayerName(playerName, "Spieler");
        string sanitizedAddress = address.Trim();
        ApplyState(SessionRole.Client, ConnectionStatus.Connecting, $"Verbinde zu {sanitizedAddress}:{port}...");
        ENetMultiplayerPeer peer = new();
        Error result = peer.CreateClient(sanitizedAddress, port);
        if (result != Error.Ok)
        {
            peer.Dispose();
            return Fail($"Client konnte Verbindung nicht starten: {result}");
        }

        _peer = peer;
        Multiplayer.MultiplayerPeer = peer;
        _connectedPeers.Clear();
        return Error.Ok;
    }

    public void StopSession(string message = "Sitzung beendet.")
    {
        ReleasePeer();
        ApplyState(SessionRole.Offline, ConnectionStatus.Offline, message);
    }

    public IReadOnlyList<PlayerIdentity> BuildPlayerIdentities(MazePointSaveData defaultSpawnCell)
    {
        List<long> orderedPeerIds = _connectedPeers.OrderBy(peerId => peerId).ToList();
        List<PlayerIdentity> players = new(orderedPeerIds.Count);

        for (int index = 0; index < orderedPeerIds.Count; index++)
        {
            long peerId = orderedPeerIds[index];
            bool isHost = peerId == HostPeerId || (Role == SessionRole.Host && peerId == LocalPeerId);
            players.Add(new PlayerIdentity
            {
                PeerId = peerId,
                PlayerName = ResolvePlayerName(peerId, isHost),
                PlayerSlot = index,
                IsHost = isHost,
                AssignedSpawnCell = new MazePointSaveData(defaultSpawnCell.X, defaultSpawnCell.Y)
            });
        }

        return players;
    }

    public Error BroadcastSessionStart(SessionStartPayload payload)
    {
        if (Role != SessionRole.Host)
        {
            GD.PrintErr("[MultiplayerSession] Nur der Host darf den Startvertrag senden.");
            return Error.Failed;
        }

        if (payload is null)
        {
            GD.PrintErr("[MultiplayerSession] Startvertrag fehlt.");
            return Error.InvalidData;
        }

        if (string.IsNullOrWhiteSpace(payload.SessionId))
        {
            payload.SessionId = Guid.NewGuid().ToString("N");
        }

        payload.ContractVersion = SessionStartPayload.CurrentContractVersion;
        payload.CreatedAtUtc = DateTime.UtcNow;
        payload.HostPeerId = LocalPeerId;
        _activeSessionStartId = payload.SessionId;
        _synchronizedPeers.Clear();

        if (LocalPeerId > 0)
        {
            _synchronizedPeers.Add(LocalPeerId);
        }

        int remoteRecipients = 0;
        foreach (long peerId in _connectedPeers.OrderBy(peerId => peerId))
        {
            if (peerId == LocalPeerId)
            {
                continue;
            }

            SessionStartPayload peerPayload = ClonePayloadForRecipient(payload, peerId);
            string serializedPayload = JsonSerializer.Serialize(peerPayload, JsonOptions);
            RpcId(peerId, nameof(ReceiveSessionStartPayloadRpc), serializedPayload);
            remoteRecipients++;
        }

        ApplyState(
            SessionRole.Host,
            ConnectionStatus.Hosting,
            remoteRecipients > 0
                ? $"Startvertrag {_activeSessionStartId} an {remoteRecipients} Client(s) gesendet."
                : "Host aktiv. Noch keine Clients fuer den Startvertrag verbunden.");
        return Error.Ok;
    }

    public void ConfirmSessionStartApplied(string sessionId)
    {
        if (Role != SessionRole.Client || string.IsNullOrWhiteSpace(sessionId) || !string.Equals(sessionId, _activeSessionStartId, StringComparison.Ordinal))
        {
            return;
        }

        if (LocalPeerId > 0)
        {
            _synchronizedPeers.Add(LocalPeerId);
        }

        RpcId(HostPeerId, nameof(AcknowledgeSessionStartRpc), sessionId);
        ApplyState(SessionRole.Client, ConnectionStatus.Synchronized, $"Startvertrag {sessionId} angewendet. Client ist synchronisiert.");
    }

    public Error SendLocalPlayerSnapshot(PlayerSnapshot snapshot)
    {
        if (Role != SessionRole.Client || Multiplayer.MultiplayerPeer is null)
        {
            return Error.Unavailable;
        }

        string serializedSnapshot = JsonSerializer.Serialize(snapshot, JsonOptions);
        RpcId(HostPeerId, nameof(ReceiveClientPlayerSnapshotRpc), serializedSnapshot);
        return Error.Ok;
    }

    public Error BroadcastPlayerSnapshots(PlayerSnapshotBatch snapshotBatch)
    {
        if (Role != SessionRole.Host || Multiplayer.MultiplayerPeer is null)
        {
            return Error.Unavailable;
        }

        string serializedBatch = JsonSerializer.Serialize(snapshotBatch, JsonOptions);
        foreach (long peerId in _connectedPeers.OrderBy(peerId => peerId))
        {
            if (peerId == LocalPeerId)
            {
                continue;
            }

            RpcId(peerId, nameof(ReceivePlayerSnapshotBatchRpc), serializedBatch);
        }

        return Error.Ok;
    }

    public Error BroadcastWorldSnapshot(WorldRuntimeSnapshot worldSnapshot)
    {
        if (Role != SessionRole.Host || Multiplayer.MultiplayerPeer is null)
        {
            return Error.Unavailable;
        }

        string serializedSnapshot = JsonSerializer.Serialize(worldSnapshot, JsonOptions);
        foreach (long peerId in _connectedPeers.OrderBy(peerId => peerId))
        {
            if (peerId == LocalPeerId)
            {
                continue;
            }

            RpcId(peerId, nameof(ReceiveWorldSnapshotRpc), serializedSnapshot);
        }

        return Error.Ok;
    }

    private void OnPeerConnected(long peerId)
    {
        if (_connectedPeers.Add(peerId))
        {
            _synchronizedPeers.Remove(peerId);
            ApplyState(Role, Status, $"Peer {peerId} verbunden. Aktive Peers: {_connectedPeers.Count}.");
            PeerJoined?.Invoke(peerId);
        }
    }

    private void OnPeerDisconnected(long peerId)
    {
        if (_connectedPeers.Remove(peerId))
        {
            _registeredPlayerNames.Remove(peerId);
            _synchronizedPeers.Remove(peerId);
            ApplyState(Role, Status, $"Peer {peerId} getrennt. Aktive Peers: {_connectedPeers.Count}.");
            PeerLeft?.Invoke(peerId);
        }
    }

    private void OnConnectedToServer()
    {
        _connectedPeers.Clear();
        long localPeerId = Multiplayer.GetUniqueId();
        if (localPeerId > 0)
        {
            _connectedPeers.Add(localPeerId);
            RegisterPlayerName(localPeerId, _localPlayerName);
        }

        ApplyState(SessionRole.Client, ConnectionStatus.Connected, $"Mit Host verbunden. Lokale Peer-ID: {localPeerId}.");
        RpcId(HostPeerId, nameof(RegisterClientPlayerRpc), _localPlayerName);
    }

    private void OnConnectionFailed()
    {
        ReleasePeer();
        ApplyState(SessionRole.Offline, ConnectionStatus.Error, "Verbindung zum Host fehlgeschlagen.");
    }

    private void OnServerDisconnected()
    {
        ReleasePeer();
        ApplyState(SessionRole.Offline, ConnectionStatus.Error, "Verbindung zum Host wurde getrennt.");
    }

    private Error Fail(string message)
    {
        ReleasePeer();
        ApplyState(SessionRole.Offline, ConnectionStatus.Error, message);
        GD.PrintErr($"[MultiplayerSession] {message}");
        return Error.Failed;
    }

    private void ReleasePeer()
    {
        if (_peer is not null)
        {
            _peer.Close();
            _peer.Dispose();
            _peer = null;
        }

        if (Multiplayer.MultiplayerPeer is not null)
        {
            Multiplayer.MultiplayerPeer = null;
        }

        _connectedPeers.Clear();
        _registeredPlayerNames.Clear();
        _synchronizedPeers.Clear();
        _activeSessionStartId = string.Empty;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RegisterClientPlayerRpc(string playerName)
    {
        if (Role != SessionRole.Host)
        {
            return;
        }

        long senderPeerId = Multiplayer.GetRemoteSenderId();
        if (senderPeerId <= 0)
        {
            return;
        }

        RegisterPlayerName(senderPeerId, playerName);
        ApplyState(SessionRole.Host, ConnectionStatus.Hosting, $"Lobby aktualisiert. Peer {senderPeerId} ist als '{ResolvePlayerName(senderPeerId, false)}' registriert.");
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveSessionStartPayloadRpc(string payloadJson)
    {
        if (Role != SessionRole.Client)
        {
            return;
        }

        SessionStartPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SessionStartPayload>(payloadJson, JsonOptions);
        }
        catch (Exception ex)
        {
            Fail($"Startvertrag konnte nicht gelesen werden: {ex.Message}");
            return;
        }

        if (payload is null)
        {
            Fail("Startvertrag fehlt oder ist leer.");
            return;
        }

        _activeSessionStartId = payload.SessionId;
        ApplyState(SessionRole.Client, ConnectionStatus.Connected, $"Startvertrag {payload.SessionId} empfangen. Welt wird synchronisiert.");
        SessionStartReceived?.Invoke(payload);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void AcknowledgeSessionStartRpc(string sessionId)
    {
        if (Role != SessionRole.Host || string.IsNullOrWhiteSpace(sessionId) || !string.Equals(sessionId, _activeSessionStartId, StringComparison.Ordinal))
        {
            return;
        }

        long senderPeerId = Multiplayer.GetRemoteSenderId();
        if (senderPeerId <= 0 || !_connectedPeers.Contains(senderPeerId))
        {
            return;
        }

        if (_synchronizedPeers.Add(senderPeerId))
        {
            int remotePeerCount = Math.Max(0, _connectedPeers.Count - 1);
            int readyPeerCount = Math.Max(0, _synchronizedPeers.Count - 1);
            ApplyState(SessionRole.Host, ConnectionStatus.Hosting, $"Client {senderPeerId} hat Startvertrag bestaetigt ({readyPeerCount}/{remotePeerCount}).");
            SessionStartAcknowledged?.Invoke(senderPeerId, sessionId);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void ReceiveClientPlayerSnapshotRpc(string snapshotJson)
    {
        if (Role != SessionRole.Host)
        {
            return;
        }

        long senderPeerId = Multiplayer.GetRemoteSenderId();
        if (senderPeerId <= 0)
        {
            return;
        }

        PlayerSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<PlayerSnapshot>(snapshotJson, JsonOptions);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[MultiplayerSession] Client-Snapshot konnte nicht gelesen werden: {ex.Message}");
            return;
        }

        if (snapshot is null || snapshot.Identity.PeerId != senderPeerId)
        {
            return;
        }

        ClientPlayerSnapshotReceived?.Invoke(senderPeerId, snapshot);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void ReceivePlayerSnapshotBatchRpc(string snapshotBatchJson)
    {
        if (Role != SessionRole.Client)
        {
            return;
        }

        PlayerSnapshotBatch? snapshotBatch;
        try
        {
            snapshotBatch = JsonSerializer.Deserialize<PlayerSnapshotBatch>(snapshotBatchJson, JsonOptions);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[MultiplayerSession] Snapshot-Batch konnte nicht gelesen werden: {ex.Message}");
            return;
        }

        if (snapshotBatch is null)
        {
            return;
        }

        PlayerSnapshotBatchReceived?.Invoke(snapshotBatch);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void ReceiveWorldSnapshotRpc(string worldSnapshotJson)
    {
        if (Role != SessionRole.Client)
        {
            return;
        }

        WorldRuntimeSnapshot? worldSnapshot;
        try
        {
            worldSnapshot = JsonSerializer.Deserialize<WorldRuntimeSnapshot>(worldSnapshotJson, JsonOptions);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[MultiplayerSession] World-Snapshot konnte nicht gelesen werden: {ex.Message}");
            return;
        }

        if (worldSnapshot is null)
        {
            return;
        }

        WorldSnapshotReceived?.Invoke(worldSnapshot);
    }

    private void ApplyState(SessionRole role, ConnectionStatus status, string message)
    {
        Role = role;
        Status = status;
        StatusMessage = string.IsNullOrWhiteSpace(message) ? "Keine Sitzung aktiv." : message;
        StateChanged?.Invoke(Role, Status, StatusMessage);
    }

    private void RegisterPlayerName(long peerId, string playerName)
    {
        _registeredPlayerNames[peerId] = SanitizePlayerName(playerName, peerId == HostPeerId ? "Host" : $"Spieler {peerId}");
    }

    private string ResolvePlayerName(long peerId, bool isHost)
    {
        if (_registeredPlayerNames.TryGetValue(peerId, out string? playerName) && !string.IsNullOrWhiteSpace(playerName))
        {
            return playerName;
        }

        if (peerId == LocalPeerId && !string.IsNullOrWhiteSpace(_localPlayerName))
        {
            return _localPlayerName;
        }

        return isHost ? "Host" : $"Spieler {peerId}";
    }

    private static string SanitizePlayerName(string? playerName, string fallbackName)
    {
        string sanitizedName = string.IsNullOrWhiteSpace(playerName) ? fallbackName : playerName.Trim();
        return sanitizedName.Length <= 24 ? sanitizedName : sanitizedName[..24];
    }

    private static SessionStartPayload ClonePayloadForRecipient(SessionStartPayload payload, long recipientPeerId)
    {
        return new SessionStartPayload
        {
            ContractVersion = payload.ContractVersion,
            SessionId = payload.SessionId,
            CreatedAtUtc = payload.CreatedAtUtc,
            HostPeerId = payload.HostPeerId,
            RecipientPeerId = recipientPeerId,
            IsAuthoritativeHostStart = payload.IsAuthoritativeHostStart,
            GameConfig = payload.GameConfig.Clone().Sanitize(),
            World = payload.World,
            Players = payload.Players
        };
    }

    private static bool IsValidPort(int port) => port is >= 1 and <= 65535;
}