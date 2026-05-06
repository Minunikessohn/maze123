#nullable enable

using System;
using Godot;
using Maze.Game.Settings;

namespace Maze.UI;

public partial class PauseMenu : Control
{
    private enum MenuMode
    {
        Visual,
        Audio
    }

    private Button _visualButton = null!;
    private Button _audioButton = null!;
    private Label _modeTitleLabel = null!;
    private Label _modeDescriptionLabel = null!;
    private VisualSettingsPanel _visualSettingsPanel = null!;
    private AudioSettingsPanel _audioSettingsPanel = null!;
    private Button _returnToMainMenuButton = null!;

    public event Action<VisualSettings>? VisualSettingsChanged;
    public event Action<AudioSettings>? AudioSettingsChanged;
    public event Action? ReturnToMainMenuRequested;

    public override void _Ready()
    {
        _visualButton = GetNode<Button>("Center/Panel/Margin/VBox/ModeButtons/VisualButton");
        _audioButton = GetNode<Button>("Center/Panel/Margin/VBox/ModeButtons/AudioButton");
        _modeTitleLabel = GetNode<Label>("Center/Panel/Margin/VBox/ModeTitle");
        _modeDescriptionLabel = GetNode<Label>("Center/Panel/Margin/VBox/ModeDescription");
        _visualSettingsPanel = GetNode<VisualSettingsPanel>("Center/Panel/Margin/VBox/Content/VisualSettingsPanel");
        _audioSettingsPanel = GetNode<AudioSettingsPanel>("Center/Panel/Margin/VBox/Content/AudioSettingsPanel");
        _returnToMainMenuButton = GetNode<Button>("Center/Panel/Margin/VBox/Actions/ReturnToMainMenuButton");

        _visualButton.Pressed += () => SetMode(MenuMode.Visual);
        _audioButton.Pressed += () => SetMode(MenuMode.Audio);
        _visualSettingsPanel.SettingsChanged += settings => VisualSettingsChanged?.Invoke(settings);
        _audioSettingsPanel.SettingsChanged += settings => AudioSettingsChanged?.Invoke(settings);
        _returnToMainMenuButton.Pressed += () => ReturnToMainMenuRequested?.Invoke();

        SetMode(MenuMode.Visual);
    }

    public void SetVisualSettings(VisualSettings settings) =>
        _visualSettingsPanel.SetSettings(settings);

    public void SetAudioSettings(AudioSettings settings) =>
        _audioSettingsPanel.SetSettings(settings);

    private void SetMode(MenuMode mode)
    {
        _visualButton.SetPressedNoSignal(mode == MenuMode.Visual);
        _audioButton.SetPressedNoSignal(mode == MenuMode.Audio);
        _visualSettingsPanel.Visible = mode == MenuMode.Visual;
        _audioSettingsPanel.Visible = mode == MenuMode.Audio;

        if (mode == MenuMode.Visual)
        {
            _modeTitleLabel.Text = "Visuelles";
            _modeDescriptionLabel.Text = "Passe Helligkeit, Sichtfeld und Effektstaerke waehrend des laufenden Labyrinths an.";
            return;
        }

        _modeTitleLabel.Text = "Ton";
        _modeDescriptionLabel.Text = "Steuere Lautstaerken fuer Monster, Laufgeraeusche, Zielsignal und die Gesamtlautstaerke.";
    }
}