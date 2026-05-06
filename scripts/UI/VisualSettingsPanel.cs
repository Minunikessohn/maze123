#nullable enable

using System;
using Godot;
using Maze.Game.Settings;

namespace Maze.UI;

public partial class VisualSettingsPanel : VBoxContainer
{
    private HSlider _brightnessSlider = null!;
    private HSlider _fieldOfViewSlider = null!;
    private HSlider _effectsSlider = null!;
    private Label _brightnessValueLabel = null!;
    private Label _fieldOfViewValueLabel = null!;
    private Label _effectsValueLabel = null!;
    private bool _isUpdating;

    public event Action<VisualSettings>? SettingsChanged;

    public override void _Ready()
    {
        _brightnessSlider = GetNode<HSlider>("BrightnessRow/BrightnessSlider");
        _fieldOfViewSlider = GetNode<HSlider>("FieldOfViewRow/FieldOfViewSlider");
        _effectsSlider = GetNode<HSlider>("EffectsRow/EffectsSlider");
        _brightnessValueLabel = GetNode<Label>("BrightnessRow/BrightnessValueLabel");
        _fieldOfViewValueLabel = GetNode<Label>("FieldOfViewRow/FieldOfViewValueLabel");
        _effectsValueLabel = GetNode<Label>("EffectsRow/EffectsValueLabel");

        ConfigureSlider(_brightnessSlider, 0.4f, 1.8f, 0.05f, 1f);
        ConfigureSlider(_fieldOfViewSlider, 55f, 100f, 1f, 75f);
        ConfigureSlider(_effectsSlider, 0f, 1.5f, 0.05f, 1f);

        _brightnessSlider.ValueChanged += _ => OnSettingsEdited();
        _fieldOfViewSlider.ValueChanged += _ => OnSettingsEdited();
        _effectsSlider.ValueChanged += _ => OnSettingsEdited();

        UpdateValueLabels();
    }

    public void SetSettings(VisualSettings settings)
    {
        _isUpdating = true;
        _brightnessSlider.SetValueNoSignal(settings.Brightness);
        _fieldOfViewSlider.SetValueNoSignal(settings.FieldOfView);
        _effectsSlider.SetValueNoSignal(settings.EffectsIntensity);
        UpdateValueLabels();
        _isUpdating = false;
    }

    private void ConfigureSlider(Godot.Range slider, float minValue, float maxValue, float step, float value)
    {
        slider.MinValue = minValue;
        slider.MaxValue = maxValue;
        slider.Step = step;
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

        SettingsChanged?.Invoke(new VisualSettings
        {
            Brightness = (float)_brightnessSlider.Value,
            FieldOfView = (float)_fieldOfViewSlider.Value,
            EffectsIntensity = (float)_effectsSlider.Value
        });
    }

    private void UpdateValueLabels()
    {
        _brightnessValueLabel.Text = $"{_brightnessSlider.Value:0.00}x";
        _fieldOfViewValueLabel.Text = $"{_fieldOfViewSlider.Value:0}";
        _effectsValueLabel.Text = $"{_effectsSlider.Value:0.00}x";
    }
}