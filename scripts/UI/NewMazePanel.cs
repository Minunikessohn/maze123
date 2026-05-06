#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Maze.Game;

namespace Maze.UI;

public partial class NewMazePanel : VBoxContainer
{
    private LineEdit _saveNameEdit = null!;
    private SpinBox _widthSpinBox = null!;
    private SpinBox _heightSpinBox = null!;
    private OptionButton _generatorChooser = null!;
    private SpinBox _seedSpinBox = null!;
    private CheckBox _sandboxModeToggle = null!;
    private CheckBox _pathGlowToggle = null!;
    private CheckBox _darkModeToggle = null!;
    private CheckBox _trapGenerationToggle = null!;
    private CheckBox _monsterStunToggle = null!;
    private CheckBox _monsterGenerationToggle = null!;
    private CheckBox _dayNightCycleToggle = null!;
    private SpinBox _nightViewDistanceSpinBox = null!;
    private Label _monsterNightRuleLabel = null!;

    public override void _Ready()
    {
        _saveNameEdit = GetNode<LineEdit>("Basics/SaveNameEdit");
        _widthSpinBox = GetNode<SpinBox>("Basics/WidthSpinBox");
        _heightSpinBox = GetNode<SpinBox>("Basics/HeightSpinBox");
        _generatorChooser = GetNode<OptionButton>("Basics/GeneratorChooser");
        _seedSpinBox = GetNode<SpinBox>("Basics/SeedSpinBox");
        _sandboxModeToggle = GetNode<CheckBox>("Options/SandboxModeToggle");
        _pathGlowToggle = GetNode<CheckBox>("Options/PathGlowToggle");
        _darkModeToggle = GetNode<CheckBox>("Options/DarkModeToggle");
        _trapGenerationToggle = GetNode<CheckBox>("Options/TrapGenerationToggle");
        _monsterStunToggle = GetNode<CheckBox>("Options/MonsterStunToggle");
        _monsterGenerationToggle = GetNode<CheckBox>("Options/MonsterGenerationToggle");
        _dayNightCycleToggle = GetNode<CheckBox>("Options/DayNightCycleToggle");
        _nightViewDistanceSpinBox = GetNode<SpinBox>("NightView/NightViewDistanceSpinBox");
        _monsterNightRuleLabel = GetNode<Label>("NightView/MonsterNightRuleLabel");

        _widthSpinBox.MinValue = MazeGameConfig.MinimumMazeSize;
        _widthSpinBox.MaxValue = 1000;
        _widthSpinBox.Step = 1;
        _widthSpinBox.Value = 25;

        _heightSpinBox.MinValue = MazeGameConfig.MinimumMazeSize;
        _heightSpinBox.MaxValue = 1000;
        _heightSpinBox.Step = 1;
        _heightSpinBox.Value = 25;

        _seedSpinBox.MinValue = 1;
        _seedSpinBox.MaxValue = int.MaxValue;
        _seedSpinBox.Step = 1;
        _seedSpinBox.Value = Random.Shared.Next(1, int.MaxValue);

        _nightViewDistanceSpinBox.MinValue = MazeGameConfig.MinimumNightViewDistance;
        _nightViewDistanceSpinBox.MaxValue = 50;
        _nightViewDistanceSpinBox.Step = 0.5;
        _nightViewDistanceSpinBox.Value = MazeGameConfig.DefaultNightViewDistance;

        _sandboxModeToggle.ButtonPressed = false;
        _pathGlowToggle.ButtonPressed = true;
        _monsterNightRuleLabel.Text = "Monster erscheinen spaeter nur nachts.";

        _monsterGenerationToggle.Toggled += _ => UpdateDependentControls();
        UpdateDependentControls();
    }

    public void SetGeneratorOptions(IEnumerable<KeyValuePair<string, string>> generators)
    {
        _generatorChooser.Clear();

        foreach (KeyValuePair<string, string> generator in generators)
        {
            int index = _generatorChooser.ItemCount;
            _generatorChooser.AddItem(generator.Value);
            _generatorChooser.SetItemMetadata(index, generator.Key);
        }

        if (_generatorChooser.ItemCount > 0)
        {
            _generatorChooser.Selected = 0;
        }
    }

    public MazeGameConfig BuildConfig()
    {
        string generatorId = _generatorChooser.ItemCount == 0
            ? "recursive-backtracker"
            : (string)_generatorChooser.GetItemMetadata(_generatorChooser.Selected);

        return new MazeGameConfig
        {
            Width = (int)_widthSpinBox.Value,
            Height = (int)_heightSpinBox.Value,
            GeneratorId = generatorId,
            SandboxModeEnabled = _sandboxModeToggle.ButtonPressed,
            PathGlowEnabled = _pathGlowToggle.ButtonPressed,
            DarkModeEnabled = _darkModeToggle.ButtonPressed,
            TrapGenerationEnabled = _trapGenerationToggle.ButtonPressed,
            MonsterCanBeStunned = _monsterStunToggle.ButtonPressed,
            MonsterGenerationEnabled = _monsterGenerationToggle.ButtonPressed,
            DayNightCycleEnabled = _dayNightCycleToggle.ButtonPressed,
            NightViewDistance = (float)_nightViewDistanceSpinBox.Value,
            Seed = (int)_seedSpinBox.Value
        }.Sanitize();
    }

    public string GetRequestedSaveName()
    {
        string saveName = _saveNameEdit.Text.Trim();
        return string.IsNullOrWhiteSpace(saveName) ? "maze-save" : saveName;
    }

    private void UpdateDependentControls()
    {
        bool monsterFeaturesEnabled = _monsterGenerationToggle.ButtonPressed;
        _monsterStunToggle.Disabled = !monsterFeaturesEnabled;

        if (!monsterFeaturesEnabled)
        {
            _monsterStunToggle.ButtonPressed = false;
        }
    }
}