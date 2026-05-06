#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Maze.Game;
using Maze.Save;

namespace Maze.UI;

public partial class MainMenu : Control
{
    private static readonly Vector2 DesiredPanelSize = new(920f, 620f);
    private static readonly Vector2 ViewportPadding = new(96f, 96f);

    private enum MenuMode
    {
        NewMaze,
        LoadMaze,
        DeleteMaze
    }

    private Button _newMazeButton = null!;
    private Button _loadMazeButton = null!;
    private Button _deleteMazeButton = null!;
    private PanelContainer _panel = null!;
    private Label _modeTitleLabel = null!;
    private Label _modeDescriptionLabel = null!;
    private NewMazePanel _newMazePanel = null!;
    private SaveListPanel _loadMazePanel = null!;
    private SaveListPanel _deleteMazePanel = null!;
    private Button _actionButton = null!;
    private MenuMode _currentMode = MenuMode.NewMaze;

    public event Action<string, MazeGameConfig>? StartNewMazeRequested;
    public event Action<string>? LoadMazeRequested;
    public event Action<string>? DeleteMazeRequested;

    public override void _Ready()
    {
        _panel = GetNode<PanelContainer>("Center/Panel");
        _newMazeButton = GetNode<Button>("Center/Panel/Margin/VBox/ModeButtons/NewMazeButton");
        _loadMazeButton = GetNode<Button>("Center/Panel/Margin/VBox/ModeButtons/LoadMazeButton");
        _deleteMazeButton = GetNode<Button>("Center/Panel/Margin/VBox/ModeButtons/DeleteMazeButton");
        _modeTitleLabel = GetNode<Label>("Center/Panel/Margin/VBox/ModeTitle");
        _modeDescriptionLabel = GetNode<Label>("Center/Panel/Margin/VBox/ModeDescription");
        _newMazePanel = GetNode<NewMazePanel>("Center/Panel/Margin/VBox/Content/NewMazePanel");
        _loadMazePanel = GetNode<SaveListPanel>("Center/Panel/Margin/VBox/Content/LoadMazePanel");
        _deleteMazePanel = GetNode<SaveListPanel>("Center/Panel/Margin/VBox/Content/DeleteMazePanel");
        _actionButton = GetNode<Button>("Center/Panel/Margin/VBox/ActionRow/ActionButton");

        _newMazeButton.Pressed += () => SetMode(MenuMode.NewMaze);
        _loadMazeButton.Pressed += () => SetMode(MenuMode.LoadMaze);
        _deleteMazeButton.Pressed += () => SetMode(MenuMode.DeleteMaze);
        _actionButton.Pressed += OnActionPressed;
        _loadMazePanel.SelectionChanged += UpdateActionButtonState;
        _deleteMazePanel.SelectionChanged += UpdateActionButtonState;
        GetViewport().SizeChanged += UpdateResponsiveLayout;

        UpdateResponsiveLayout();
        SetMode(MenuMode.NewMaze);
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

    private void SetMode(MenuMode mode)
    {
        _currentMode = mode;
        _newMazeButton.SetPressedNoSignal(mode == MenuMode.NewMaze);
        _loadMazeButton.SetPressedNoSignal(mode == MenuMode.LoadMaze);
        _deleteMazeButton.SetPressedNoSignal(mode == MenuMode.DeleteMaze);

        _newMazePanel.Visible = mode == MenuMode.NewMaze;
        _loadMazePanel.Visible = mode == MenuMode.LoadMaze;
        _deleteMazePanel.Visible = mode == MenuMode.DeleteMaze;

        switch (mode)
        {
            case MenuMode.NewMaze:
                _modeTitleLabel.Text = "Neues Labyrinth";
                _modeDescriptionLabel.Text = "Konfiguriere Groesse, Darstellung und spaetere Gameplay-Regeln fuer einen neuen Lauf.";
                _actionButton.Text = "Spiel starten";
                break;
            case MenuMode.LoadMaze:
                _modeTitleLabel.Text = "Gespeicherte Labyrinthe";
                _modeDescriptionLabel.Text = "Waehle einen vorhandenen Spielstand und setze das Labyrinth exakt mit gespeicherter Struktur fort.";
                _actionButton.Text = "Laden";
                break;
            default:
                _modeTitleLabel.Text = "Labyrinth loeschen";
                _modeDescriptionLabel.Text = "Entferne einen gespeicherten Spielstand dauerhaft aus dem lokalen Save-Ordner.";
                _actionButton.Text = "Loeschen";
                break;
        }

        UpdateActionButtonState();
    }

    private void UpdateActionButtonState()
    {
        _actionButton.Disabled = _currentMode switch
        {
            MenuMode.NewMaze => false,
            MenuMode.LoadMaze => string.IsNullOrWhiteSpace(_loadMazePanel.SelectedSaveId),
            MenuMode.DeleteMaze => string.IsNullOrWhiteSpace(_deleteMazePanel.SelectedSaveId),
            _ => true
        };
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

    private void UpdateResponsiveLayout()
    {
        Vector2 availableSize = GetViewportRect().Size - ViewportPadding;
        _panel.CustomMinimumSize = new Vector2(
            Mathf.Min(DesiredPanelSize.X, Mathf.Max(0f, availableSize.X)),
            Mathf.Min(DesiredPanelSize.Y, Mathf.Max(0f, availableSize.Y)));
    }
}