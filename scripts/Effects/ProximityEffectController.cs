#nullable enable

using Godot;
using Maze.Views;

namespace Maze.Effects;

public partial class ProximityEffectController : CanvasLayer
{
    [Export] public float MaxDistanceCells = 6f;
    [Export] public float IntensityLerpSpeed = 6f;
    [Export] public float MaxOverlayAlpha = 0.32f;
    [Export] public float MaxShakeOffset = 0.12f;
    [Export] public float ShakeFrequency = 18f;

    private ColorRect _tint = null!;
    private CameraController3D? _camera;
    private float _targetIntensity;
    private float _currentIntensity;
    private float _effectsScale = 1f;
    private float _time;

    public override void _Ready()
    {
        _tint = GetNode<ColorRect>("Tint");
        ApplyVisuals(0f);
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        _currentIntensity = Mathf.MoveToward(_currentIntensity, _targetIntensity, IntensityLerpSpeed * (float)delta);
        ApplyVisuals(_currentIntensity);
    }

    public void SetCamera(CameraController3D camera)
    {
        _camera = camera;
    }

    public void SetEffectsScale(float effectsScale)
    {
        _effectsScale = Mathf.Clamp(effectsScale, 0f, 1.5f);
        ApplyVisuals(_currentIntensity);
    }

    public void ApplyNearestMonsterDistance(float nearestDistanceCells)
    {
        float proximity = 1f - Mathf.Clamp(nearestDistanceCells / Mathf.Max(0.01f, MaxDistanceCells), 0f, 1f);
        _targetIntensity = proximity * proximity;
    }

    public void Clear()
    {
        _targetIntensity = 0f;
        _currentIntensity = 0f;
        ApplyVisuals(0f);
    }

    private void ApplyVisuals(float intensity)
    {
        float scaledIntensity = Mathf.Clamp(intensity * _effectsScale, 0f, 1.5f);
        float overlayAlpha = MaxOverlayAlpha * Mathf.Clamp(scaledIntensity, 0f, 1f);

        Visible = overlayAlpha > 0.001f;
        _tint.Color = new Color(0.45f, 0.05f, 0.04f, overlayAlpha);

        if (_camera is null)
        {
            return;
        }

        float shakeStrength = MaxShakeOffset * Mathf.Clamp(scaledIntensity, 0f, 1f);
        Vector3 shakeOffset = shakeStrength <= 0.001f
            ? Vector3.Zero
            : new Vector3(
                Mathf.Sin(_time * ShakeFrequency) * shakeStrength,
                Mathf.Cos(_time * (ShakeFrequency * 1.37f)) * shakeStrength * 0.65f,
                0f);

        _camera.SetExternalShakeOffset(shakeOffset);
    }
}