#nullable enable

using System;
using Godot;
using Maze.Game.Settings;

namespace Maze.UI;

public partial class AudioSettingsPanel : VBoxContainer
{
    private HSlider _monsterSlider = null!;
    private HSlider _footstepSlider = null!;
    private HSlider _goalSlider = null!;
    private HSlider _masterSlider = null!;
    private Label _monsterValueLabel = null!;
    private Label _footstepValueLabel = null!;
    private Label _goalValueLabel = null!;
    private Label _masterValueLabel = null!;
    private bool _isUpdating;

    public event Action<AudioSettings>? SettingsChanged;

    public override void _Ready()
    {
        _monsterSlider = GetNode<HSlider>("MonsterRow/MonsterSlider");
        _footstepSlider = GetNode<HSlider>("FootstepRow/FootstepSlider");
        _goalSlider = GetNode<HSlider>("GoalRow/GoalSlider");
        _masterSlider = GetNode<HSlider>("MasterRow/MasterSlider");
        _monsterValueLabel = GetNode<Label>("MonsterRow/MonsterValueLabel");
        _footstepValueLabel = GetNode<Label>("FootstepRow/FootstepValueLabel");
        _goalValueLabel = GetNode<Label>("GoalRow/GoalValueLabel");
        _masterValueLabel = GetNode<Label>("MasterRow/MasterValueLabel");

        ConfigureSlider(_monsterSlider, 1f);
        ConfigureSlider(_footstepSlider, 1f);
        ConfigureSlider(_goalSlider, 1f);
        ConfigureSlider(_masterSlider, 1f);

        _monsterSlider.ValueChanged += _ => OnSettingsEdited();
        _footstepSlider.ValueChanged += _ => OnSettingsEdited();
        _goalSlider.ValueChanged += _ => OnSettingsEdited();
        _masterSlider.ValueChanged += _ => OnSettingsEdited();

        UpdateValueLabels();
    }

    public void SetSettings(AudioSettings settings)
    {
        _isUpdating = true;
        _monsterSlider.SetValueNoSignal(settings.MonsterVolume);
        _footstepSlider.SetValueNoSignal(settings.FootstepVolume);
        _goalSlider.SetValueNoSignal(settings.GoalVolume);
        _masterSlider.SetValueNoSignal(settings.MasterVolume);
        UpdateValueLabels();
        _isUpdating = false;
    }

    private void ConfigureSlider(Godot.Range slider, float value)
    {
        slider.MinValue = 0f;
        slider.MaxValue = 1f;
        slider.Step = 0.05f;
        slider.Value = value;
        slider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    private void OnSettingsEdited()
    {
        UpdateValueLabels();

        if (_isUpdating)
        {
            return;
        }

        SettingsChanged?.Invoke(new AudioSettings
        {
            MonsterVolume = (float)_monsterSlider.Value,
            FootstepVolume = (float)_footstepSlider.Value,
            GoalVolume = (float)_goalSlider.Value,
            MasterVolume = (float)_masterSlider.Value
        });
    }

    private void UpdateValueLabels()
    {
        _monsterValueLabel.Text = $"{_monsterSlider.Value:P0}";
        _footstepValueLabel.Text = $"{_footstepSlider.Value:P0}";
        _goalValueLabel.Text = $"{_goalSlider.Value:P0}";
        _masterValueLabel.Text = $"{_masterSlider.Value:P0}";
    }
}