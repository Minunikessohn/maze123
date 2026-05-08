#nullable enable

using System;
using System.Collections.Generic;
using Godot;

namespace Maze.Network;

public partial class MultiplayerSession : Node
{
    private const int DefaultMaxClients = 4;

    private ENetMultiplayerPeer? _peer;
    private readonly HashSet<long> _connectedPeers = new();

    public SessionRole Role { get; private set; } = SessionRole.Offline;
    public ConnectionStatus Status { get; private set; } = ConnectionStatus.Offline;
    public string StatusMessage { get; private set; } = "Keine Sitzung aktiv.";
    public long LocalPeerId => Multiplayer.MultiplayerPeer is null ? 0L : Multiplayer.GetUniqueId();
    public IReadOnlyCollection<long> ConnectedPeerIds => _connectedPeers;

    public event Action<SessionRole, ConnectionStatus, string>? StateChanged;
    public event Action<long>? PeerJoined;
    public event Action<long>? PeerLeft;

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

    public Error StartHost(int port, int maxClients = DefaultMaxClients)
    {
        StopSession();

        if (!IsValidPort(port))
        {
            return Fail("Ungueltiger Port fuer Host-Session.");
        }

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
        }

        ApplyState(SessionRole.Host, ConnectionStatus.Hosting, $"Host aktiv auf Port {port}. Warte auf Clients.");
        return Error.Ok;
    }

    public Error JoinSession(string address, int port)
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

    private void OnPeerConnected(long peerId)
    {
        if (_connectedPeers.Add(peerId))
        {
            ApplyState(Role, Status, $"Peer {peerId} verbunden. Aktive Peers: {_connectedPeers.Count}.");
            PeerJoined?.Invoke(peerId);
        }
    }

    private void OnPeerDisconnected(long peerId)
    {
        if (_connectedPeers.Remove(peerId))
        {
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
        }

        ApplyState(SessionRole.Client, ConnectionStatus.Connected, $"Mit Host verbunden. Lokale Peer-ID: {localPeerId}.");
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
    }

    private void ApplyState(SessionRole role, ConnectionStatus status, string message)
    {
        Role = role;
        Status = status;
        StatusMessage = string.IsNullOrWhiteSpace(message) ? "Keine Sitzung aktiv." : message;
        StateChanged?.Invoke(Role, Status, StatusMessage);
    }

    private static bool IsValidPort(int port) => port is >= 1 and <= 65535;
}