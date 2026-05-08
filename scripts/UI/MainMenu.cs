#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Maze.Game;
using Maze.Network;
using Maze.Save;

namespace Maze.UI;

public partial class MainMenu : Control
{
    private static readonly Vector2 DesiredPanelSize = new(920f, 620f);
    private static readonly Vector2 ViewportPadding = new(96f, 96f);

    private enum SessionMode
    {
        Offline,
        Host,
        Join
    }

    private enum MenuMode
    {
        NewMaze,
        LoadMaze,
        DeleteMaze
    }

    private Button _newMazeButton = null!;
    private Button _loadMazeButton = null!;
    private Button _deleteMazeButton = null!;
    private Button _offlineSessionButton = null!;
    private Button _hostSessionButton = null!;
    private Button _joinSessionButton = null!;
    private PanelContainer _panel = null!;
    private Label _sessionModeTitleLabel = null!;
    private Label _sessionModeDescriptionLabel = null!;
    private Control _playerNameRow = null!;
    private LineEdit _playerNameEdit = null!;
    private Label _modeTitleLabel = null!;
    private Label _modeDescriptionLabel = null!;
    private Control _sessionAddressRow = null!;
    private LineEdit _sessionAddressEdit = null!;
    private SpinBox _sessionPortSpinBox = null!;
    private Label _sessionStatusLabel = null!;
    private Label _sessionSummaryLabel = null!;
    private Label _lobbyHintLabel = null!;
    private ItemList _lobbyPlayersList = null!;
    private NewMazePanel _newMazePanel = null!;
    private SaveListPanel _loadMazePanel = null!;
    private SaveListPanel _deleteMazePanel = null!;
    private Button _actionButton = null!;
    private Button _sessionActionButton = null!;
    private MenuMode _currentMode = MenuMode.NewMaze;
    private SessionMode _currentSessionMode = SessionMode.Offline;
    private SessionRole _sessionRole = SessionRole.Offline;
    private ConnectionStatus _connectionStatus = ConnectionStatus.Offline;
    private readonly List<long> _connectedPeerIds = new();
    private long _localPeerId;

    public event Action<string, MazeGameConfig>? StartNewMazeRequested;
    public event Action<string>? LoadMazeRequested;
    public event Action<string>? DeleteMazeRequested;
    public event Action<int>? HostSessionRequested;
    public event Action<string, int>? JoinSessionRequested;
    public event Action? LeaveSessionRequested;

    public override void _Ready()
    {
        _panel = GetNode<PanelContainer>("Center/Panel");
        _newMazeButton = GetNode<Button>("Center/Panel/Margin/VBox/ModeButtons/NewMazeButton");
        _loadMazeButton = GetNode<Button>("Center/Panel/Margin/VBox/ModeButtons/LoadMazeButton");
        _deleteMazeButton = GetNode<Button>("Center/Panel/Margin/VBox/ModeButtons/DeleteMazeButton");
        _offlineSessionButton = GetNode<Button>("Center/Panel/Margin/VBox/SessionPanel/Margin/VBox/SessionModeButtons/OfflineSessionButton");
        _hostSessionButton = GetNode<Button>("Center/Panel/Margin/VBox/SessionPanel/Margin/VBox/SessionModeButtons/HostSessionButton");
        _joinSessionButton = GetNode<Button>("Center/Panel/Margin/VBox/SessionPanel/Margin/VBox/SessionModeButtons/JoinSessionButton");
        _sessionModeTitleLabel = GetNode<Label>("Center/Panel/Margin/VBox/SessionPanel/Margin/VBox/SessionModeTitle");
        _sessionModeDescriptionLabel = GetNode<Label>("Center/Panel/Margin/VBox/SessionPanel/Margin/VBox/SessionModeDescription");
        _playerNameRow = GetNode<Control>("Center/Panel/Margin/VBox/SessionPanel/Margin/VBox/PlayerNameRow");
        _playerNameEdit = GetNode<LineEdit>("Center/Panel/Margin/VBox/SessionPanel/Margin/VBox/PlayerNameRow/PlayerNameEdit");
        _modeTitleLabel = GetNode<Label>("Center/Panel/Margin/VBox/ModeTitle");
        _modeDescriptionLabel = GetNode<Label>("Center/Panel/Margin/VBox/ModeDescription");
        _sessionAddressRow = GetNode<Control>("Center/Panel/Margin/VBox/SessionPanel/Margin/VBox/SessionAddressRow");
        _sessionAddressEdit = GetNode<LineEdit>("Center/Panel/Margin/VBox/SessionPanel/Margin/VBox/SessionAddressRow/SessionAddressEdit");
        _sessionPortSpinBox = GetNode<SpinBox>("Center/Panel/Margin/VBox/SessionPanel/Margin/VBox/SessionPortRow/SessionPortSpinBox");
        _sessionStatusLabel = GetNode<Label>("Center/Panel/Margin/VBox/SessionPanel/Margin/VBox/SessionStatusLabel");
        _sessionSummaryLabel = GetNode<Label>("Center/Panel/Margin/VBox/SessionPanel/Margin/VBox/LobbyPanel/Margin/VBox/SessionSummaryLabel");
        _lobbyHintLabel = GetNode<Label>("Center/Panel/Margin/VBox/SessionPanel/Margin/VBox/LobbyPanel/Margin/VBox/LobbyHintLabel");
        _lobbyPlayersList = GetNode<ItemList>("Center/Panel/Margin/VBox/SessionPanel/Margin/VBox/LobbyPanel/Margin/VBox/LobbyPlayersList");
        _newMazePanel = GetNode<NewMazePanel>("Center/Panel/Margin/VBox/Content/NewMazePanel");
        _loadMazePanel = GetNode<SaveListPanel>("Center/Panel/Margin/VBox/Content/LoadMazePanel");
        _deleteMazePanel = GetNode<SaveListPanel>("Center/Panel/Margin/VBox/Content/DeleteMazePanel");
        _actionButton = GetNode<Button>("Center/Panel/Margin/VBox/ActionRow/ActionButton");
        _sessionActionButton = GetNode<Button>("Center/Panel/Margin/VBox/SessionPanel/Margin/VBox/SessionActionRow/SessionActionButton");

        _newMazeButton.Pressed += () => SetMode(MenuMode.NewMaze);
        _loadMazeButton.Pressed += () => SetMode(MenuMode.LoadMaze);
        _deleteMazeButton.Pressed += () => SetMode(MenuMode.DeleteMaze);
        _offlineSessionButton.Pressed += () => SetSessionMode(SessionMode.Offline);
        _hostSessionButton.Pressed += () => SetSessionMode(SessionMode.Host);
        _joinSessionButton.Pressed += () => SetSessionMode(SessionMode.Join);
        _actionButton.Pressed += OnActionPressed;
        _sessionActionButton.Pressed += OnSessionActionPressed;
        _loadMazePanel.SelectionChanged += UpdateActionButtonState;
        _deleteMazePanel.SelectionChanged += UpdateActionButtonState;
        _playerNameEdit.TextChanged += _ => UpdateLobbyState();
        _sessionAddressEdit.TextChanged += _ =>
        {
            UpdateSessionActionState();
            UpdateLobbyState();
        };
        _sessionPortSpinBox.ValueChanged += _ =>
        {
            UpdateSessionActionState();
            UpdateLobbyState();
        };
        GetViewport().SizeChanged += UpdateResponsiveLayout;

        UpdateResponsiveLayout();
        SetMode(MenuMode.NewMaze);
        SetSessionMode(SessionMode.Offline);
        SetSessionState(SessionRole.Offline, ConnectionStatus.Offline, "Keine Sitzung aktiv.", Array.Empty<long>(), 0L);
    }

    public override void _ExitTree()
    {
        if (IsNodeReady())
        {
            GetViewport().SizeChanged -= UpdateResponsiveLayout;
        }
    }

    public void SetGeneratorOptions(IEnumerable<KeyValuePair<string, string>> generators) =>
        _newMazePanel.SetGeneratorOptions(generators);

    public void SetSaveSlots(IEnumerable<SaveSlotSummary> saveSlots)
    {
        List<SaveSlotSummary> items = saveSlots is List<SaveSlotSummary> list ? list : new List<SaveSlotSummary>(saveSlots);
        _loadMazePanel.SetSaveSlots(items);
        _deleteMazePanel.SetSaveSlots(items);
        UpdateActionButtonState();
    }

    public void SetSessionState(SessionRole role, ConnectionStatus status, string message, IEnumerable<long> connectedPeerIds, long localPeerId)
    {
        _sessionRole = role;
        _connectionStatus = status;
        _localPeerId = localPeerId;
        _connectedPeerIds.Clear();
        _connectedPeerIds.AddRange(connectedPeerIds.Distinct().OrderBy(peerId => peerId));
        _sessionStatusLabel.Text = string.IsNullOrWhiteSpace(message) ? "Keine Sitzung aktiv." : message;

        if (role == SessionRole.Host)
        {
            _currentSessionMode = SessionMode.Host;
        }
        else if (role == SessionRole.Client)
        {
            _currentSessionMode = SessionMode.Join;
        }

        UpdateSessionModePresentation();
        UpdateLobbyState();
        UpdateSessionActionState();
        UpdateActionButtonState();
    }

    private void SetMode(MenuMode mode)
    {
        _currentMode = mode;
        _newMazeButton.SetPressedNoSignal(mode == MenuMode.NewMaze);
        _loadMazeButton.SetPressedNoSignal(mode == MenuMode.LoadMaze);
        _deleteMazeButton.SetPressedNoSignal(mode == MenuMode.DeleteMaze);

        _newMazePanel.Visible = mode == MenuMode.NewMaze;
        _loadMazePanel.Visible = mode == MenuMode.LoadMaze;
        _deleteMazePanel.Visible = mode == MenuMode.DeleteMaze;

        UpdateModePresentation();
        UpdateActionButtonState();
    }

    private void SetSessionMode(SessionMode mode)
    {
        if (IsSessionActive())
        {
            return;
        }

        _currentSessionMode = mode;
        UpdateSessionModePresentation();
        UpdateLobbyState();
        UpdateModePresentation();
        UpdateSessionActionState();
    }

    private void UpdateActionButtonState()
    {
        bool sessionBusy = _connectionStatus is ConnectionStatus.Connecting or ConnectionStatus.Starting;
        bool clientOwnsSession = _sessionRole == SessionRole.Client || _connectionStatus == ConnectionStatus.Connected;
        bool hostLobbyActive = _sessionRole == SessionRole.Host || _connectionStatus == ConnectionStatus.Hosting;

        _actionButton.Disabled = _currentMode switch
        {
            MenuMode.NewMaze => clientOwnsSession || sessionBusy,
            MenuMode.LoadMaze => clientOwnsSession || sessionBusy || string.IsNullOrWhiteSpace(_loadMazePanel.SelectedSaveId),
            MenuMode.DeleteMaze => clientOwnsSession || hostLobbyActive || sessionBusy || string.IsNullOrWhiteSpace(_deleteMazePanel.SelectedSaveId),
            _ => true
        };

        UpdateModePresentation();
    }

    private void UpdateSessionActionState()
    {
        _offlineSessionButton.SetPressedNoSignal(_currentSessionMode == SessionMode.Offline);
        _hostSessionButton.SetPressedNoSignal(_currentSessionMode == SessionMode.Host);
        _joinSessionButton.SetPressedNoSignal(_currentSessionMode == SessionMode.Join);

        bool sessionActive = IsSessionActive();
        _offlineSessionButton.Disabled = sessionActive;
        _hostSessionButton.Disabled = sessionActive;
        _joinSessionButton.Disabled = sessionActive;
        _playerNameRow.Visible = _currentSessionMode != SessionMode.Offline || _sessionRole != SessionRole.Offline;
        _sessionAddressRow.Visible = _currentSessionMode == SessionMode.Join || _sessionRole == SessionRole.Client || _connectionStatus == ConnectionStatus.Connecting;

        if (sessionActive)
        {
            _sessionActionButton.Text = _connectionStatus switch
            {
                ConnectionStatus.Connecting => "Verbindung abbrechen",
                ConnectionStatus.Starting => "Host-Start abbrechen",
                _ => "Sitzung beenden"
            };
            _sessionActionButton.Disabled = false;
            return;
        }

        switch (_currentSessionMode)
        {
            case SessionMode.Host:
                _sessionActionButton.Text = "Host starten";
                _sessionActionButton.Disabled = false;
                break;
            case SessionMode.Join:
                _sessionActionButton.Text = "Verbinden";
                _sessionActionButton.Disabled = string.IsNullOrWhiteSpace(_sessionAddressEdit.Text);
                break;
            default:
                _sessionActionButton.Text = "Offline";
                _sessionActionButton.Disabled = true;
                break;
        }

        UpdateSessionModePresentation();
    }

    private void OnActionPressed()
    {
        switch (_currentMode)
        {
            case MenuMode.NewMaze:
                StartNewMazeRequested?.Invoke(_newMazePanel.GetRequestedSaveName(), _newMazePanel.BuildConfig());
                break;
            case MenuMode.LoadMaze:
                if (_loadMazePanel.SelectedSaveId is string loadSaveId)
                {
                    LoadMazeRequested?.Invoke(loadSaveId);
                }
                break;
            case MenuMode.DeleteMaze:
                if (_deleteMazePanel.SelectedSaveId is string deleteSaveId)
                {
                    DeleteMazeRequested?.Invoke(deleteSaveId);
                }
                break;
        }
    }

    private void OnSessionActionPressed()
    {
        if (IsSessionActive())
        {
            LeaveSessionRequested?.Invoke();
            return;
        }

        int port = Mathf.Clamp((int)Math.Round(_sessionPortSpinBox.Value), 1, 65535);

        switch (_currentSessionMode)
        {
            case SessionMode.Host:
                HostSessionRequested?.Invoke(port);
                break;
            case SessionMode.Join:
                JoinSessionRequested?.Invoke(_sessionAddressEdit.Text.Trim(), port);
                break;
        }
    }

    private void UpdateResponsiveLayout()
    {
        Vector2 availableSize = GetViewportRect().Size - ViewportPadding;
        _panel.CustomMinimumSize = new Vector2(
            Mathf.Min(DesiredPanelSize.X, Mathf.Max(0f, availableSize.X)),
            Mathf.Min(DesiredPanelSize.Y, Mathf.Max(0f, availableSize.Y)));
    }

    private void UpdateModePresentation()
    {
        switch (_currentMode)
        {
            case MenuMode.NewMaze:
                _modeTitleLabel.Text = "Neues Labyrinth";
                _modeDescriptionLabel.Text = _currentSessionMode switch
                {
                    SessionMode.Host => "Du legst die Welt fuer die Lobby fest. Erst danach koennen Clients spaeter denselben autoritativen Lauf erhalten.",
                    SessionMode.Join => "Clients starten in Phase 2 keine eigene Welt. Verbinde zuerst mit einem Host und warte auf dessen Spielstart.",
                    _ => "Konfiguriere Groesse, Darstellung und spaetere Gameplay-Regeln fuer einen neuen Offline-Lauf."
                };
                _actionButton.Text = _currentSessionMode switch
                {
                    SessionMode.Host => "Als Host neues Spiel starten",
                    SessionMode.Join => "Host waehlt den Start",
                    _ => "Offline starten"
                };
                break;
            case MenuMode.LoadMaze:
                _modeTitleLabel.Text = "Gespeicherte Labyrinthe";
                _modeDescriptionLabel.Text = _currentSessionMode == SessionMode.Host
                    ? "Der Host waehlt einen lokalen Spielstand als gemeinsame Ausgangswelt fuer die Lobby."
                    : "Waehle einen vorhandenen Spielstand und setze das Labyrinth lokal mit gespeicherter Struktur fort.";
                _actionButton.Text = _currentSessionMode == SessionMode.Host ? "Als Host laden" : "Offline laden";
                break;
            default:
                _modeTitleLabel.Text = "Labyrinth loeschen";
                _modeDescriptionLabel.Text = "Entferne einen gespeicherten Spielstand dauerhaft aus dem lokalen Save-Ordner.";
                _actionButton.Text = "Loeschen";
                break;
        }
    }

    private void UpdateSessionModePresentation()
    {
        switch (_currentSessionMode)
        {
            case SessionMode.Host:
                _sessionModeTitleLabel.Text = "Host-Lobby";
                _sessionModeDescriptionLabel.Text = IsSessionActive()
                    ? "Die Lobby ist offen. Waehl darunter ein neues Labyrinth oder einen Save und starte den Lauf als Host."
                    : "Starte zuerst einen Host. Danach bleibt das Menue offen, damit du den Weltstart fuer alle Clients festlegen kannst.";
                break;
            case SessionMode.Join:
                _sessionModeTitleLabel.Text = "Client-Beitritt";
                _sessionModeDescriptionLabel.Text = IsSessionActive()
                    ? "Du bist mit einer Lobby verbunden. In Phase 2 wartet der Client hier auf den spaeteren Host-Startvertrag."
                    : "Trage die Host-Adresse und den Port ein. Ein Join-Versuch blockiert keine unklaren Offline-Aktionen mehr.";
                break;
            default:
                _sessionModeTitleLabel.Text = "Offline-Lauf";
                _sessionModeDescriptionLabel.Text = "Starte lokal ohne Netzwerk. Host- und Join-Lobby bleiben getrennte Wege mit eigener Statusanzeige.";
                break;
        }
    }

    private void UpdateLobbyState()
    {
        _lobbyPlayersList.Clear();

        string playerName = GetRequestedPlayerName();
        int peerCount = _connectedPeerIds.Count;

        _sessionSummaryLabel.Text = _currentSessionMode switch
        {
            SessionMode.Host => IsSessionActive()
                ? $"Lokaler Host: {playerName} | Peer-ID {_localPeerId} | Verbundene Peers: {peerCount}"
                : $"Bereit als Host: {playerName} | Port {(int)Math.Round(_sessionPortSpinBox.Value)}",
            SessionMode.Join => IsSessionActive()
                ? $"Verbunden als {playerName} | Ziel {_sessionAddressEdit.Text.Trim()}:{(int)Math.Round(_sessionPortSpinBox.Value)} | Peer-ID {_localPeerId}"
                : $"Bereit zum Join als {playerName} | Ziel {_sessionAddressEdit.Text.Trim()}:{(int)Math.Round(_sessionPortSpinBox.Value)}",
            _ => "Keine Lobby aktiv. Offline-Laeufe verwenden nur lokale Saves und lokale Weltstarts."
        };

        _lobbyHintLabel.Text = _currentSessionMode switch
        {
            SessionMode.Host => "Lobby-Uebersicht",
            SessionMode.Join => "Verbindungs-Uebersicht",
            _ => "Offline-Uebersicht"
        };

        if (_connectedPeerIds.Count == 0)
        {
            _lobbyPlayersList.AddItem(_currentSessionMode == SessionMode.Offline
                ? "Keine Netzwerk-Teilnehmer."
                : "Noch keine Peers sichtbar.");
            _lobbyPlayersList.SetItemDisabled(0, true);
            return;
        }

        foreach (long peerId in _connectedPeerIds)
        {
            string peerLabel = peerId == _localPeerId
                ? $"Peer {peerId} (lokal: {playerName})"
                : $"Peer {peerId}";
            _lobbyPlayersList.AddItem(peerLabel);
        }
    }

    private string GetRequestedPlayerName()
    {
        string playerName = _playerNameEdit.Text.Trim();
        return string.IsNullOrWhiteSpace(playerName)
            ? (_currentSessionMode == SessionMode.Host ? "Host" : "Spieler")
            : playerName;
    }

    private bool IsSessionActive() =>
        _sessionRole is SessionRole.Host or SessionRole.Client
        || _connectionStatus is ConnectionStatus.Starting or ConnectionStatus.Hosting or ConnectionStatus.Connecting or ConnectionStatus.Connected;
}