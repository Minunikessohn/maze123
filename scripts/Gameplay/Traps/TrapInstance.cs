#nullable enable

using Godot;
using Maze.Game;

namespace Maze.Gameplay.Traps;

public partial class TrapInstance : Node3D
{
    private static readonly Color ArmedPlateColor = new("#5f2320");
    private static readonly Color ArmedCoreColor = new("#ff7043");
    private static readonly Color ArmedRingColor = new("#ffb067");
    private static readonly Color ConsumedPlateColor = new("#2a2d31");
    private static readonly Color ConsumedCoreColor = new("#4d555f");
    private static readonly Color ConsumedRingColor = new("#3b424b");

    [Export] public float HoverHeight { get; set; } = 0.025f;
    [Export] public float PulseSpeed { get; set; } = 2.6f;
    [Export] public float PulseTravel { get; set; } = 0.016f;
    [Export] public float ConsumedFadeDelaySeconds { get; set; } = 0.2f;
    [Export] public float ConsumedFadeDurationSeconds { get; set; } = 0.45f;

    private MeshInstance3D? _plate;
    private MeshInstance3D? _ring;
    private MeshInstance3D? _core;
    private OmniLight3D? _glow;
    private StandardMaterial3D? _plateMaterial;
    private StandardMaterial3D? _ringMaterial;
    private StandardMaterial3D? _coreMaterial;
    private float _pulseTime;
    private float _baseCoreHeight;
    private float _baseGlowHeight;
    private bool _isConsumedFading;
    private double _consumedFadeElapsed;

    public string TrapId { get; private set; } = TrapDefinition.DefaultTrapId;
    public Vector2I Cell { get; private set; }
    public bool IsArmed { get; private set; } = true;

    public override void _Ready()
    {
        _plate = GetNodeOrNull<MeshInstance3D>("Plate");
        _ring = GetNodeOrNull<MeshInstance3D>("WarningRing");
        _core = GetNodeOrNull<MeshInstance3D>("Core");
        _glow = GetNodeOrNull<OmniLight3D>("Glow");
        _baseCoreHeight = _core?.Position.Y ?? 0.028f;
        _baseGlowHeight = _glow?.Position.Y ?? 0.09f;

        if (_plate?.GetActiveMaterial(0) is StandardMaterial3D plateMaterial)
        {
            _plateMaterial = (StandardMaterial3D)plateMaterial.Duplicate();
            _plate.SetSurfaceOverrideMaterial(0, _plateMaterial);
        }

        if (_ring?.GetActiveMaterial(0) is StandardMaterial3D ringMaterial)
        {
            _ringMaterial = (StandardMaterial3D)ringMaterial.Duplicate();
            _ring.SetSurfaceOverrideMaterial(0, _ringMaterial);
        }

        if (_core?.GetActiveMaterial(0) is StandardMaterial3D coreMaterial)
        {
            _coreMaterial = (StandardMaterial3D)coreMaterial.Duplicate();
            _core.SetSurfaceOverrideMaterial(0, _coreMaterial);
        }

        ApplyVisualState();
    }

    public override void _Process(double delta)
    {
        if (_isConsumedFading)
        {
            ProcessConsumedFade(delta);
            return;
        }

        if (!Visible || !IsArmed)
        {
            return;
        }

        _pulseTime += (float)delta * PulseSpeed;
        float pulse = (Mathf.Sin(_pulseTime) + 1f) * 0.5f;

        if (_core is not null)
        {
            Vector3 corePosition = _core.Position;
            corePosition.Y = _baseCoreHeight + pulse * PulseTravel;
            _core.Position = corePosition;

            float coreScale = 0.95f + pulse * 0.18f;
            _core.Scale = new Vector3(coreScale, 1f, coreScale);
        }

        if (_ring is not null)
        {
            float ringScale = 0.96f + pulse * 0.12f;
            _ring.Scale = new Vector3(ringScale, 1f, ringScale);
        }

        if (_ringMaterial is not null)
        {
            _ringMaterial.EmissionEnergyMultiplier = 0.7f + pulse * 1.4f;
            _ringMaterial.AlbedoColor = ArmedRingColor.Lerp(ArmedCoreColor, pulse * 0.35f);
        }

        if (_coreMaterial is not null)
        {
            _coreMaterial.EmissionEnergyMultiplier = 1.2f + pulse * 1.55f;
        }

        if (_glow is not null)
        {
            _glow.Position = new Vector3(_glow.Position.X, _baseGlowHeight + pulse * PulseTravel * 1.6f, _glow.Position.Z);
            _glow.LightEnergy = 0.45f + pulse * 0.55f;
        }
    }

    public void Configure(TrapDefinition definition, float cellSize)
    {
        TrapId = string.IsNullOrWhiteSpace(definition.TrapId) ? TrapDefinition.DefaultTrapId : definition.TrapId.Trim();
        Cell = definition.Cell;
        IsArmed = definition.IsArmed;
        Position = new Vector3(
            Cell.X * cellSize + cellSize / 2f,
            HoverHeight,
            Cell.Y * cellSize + cellSize / 2f);

        float scaleFactor = Mathf.Max(0.35f, cellSize);
        Scale = new Vector3(scaleFactor, 1f, scaleFactor);
        ApplyVisualState();
    }

    public void SetArmed(bool armed)
    {
        if (IsArmed == armed)
        {
            return;
        }

        IsArmed = armed;
        if (!armed)
        {
            _isConsumedFading = true;
            _consumedFadeElapsed = 0d;
        }
        else
        {
            Visible = true;
            _isConsumedFading = false;
            _consumedFadeElapsed = 0d;
        }

        ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        Visible = true;

        if (_plateMaterial is not null)
        {
            Color plateColor = IsArmed ? ArmedPlateColor : ConsumedPlateColor;
            _plateMaterial.AlbedoColor = plateColor;
            _plateMaterial.Metallic = IsArmed ? 0.35f : 0.12f;
            _plateMaterial.Roughness = IsArmed ? 0.22f : 0.65f;
            _plateMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            _plateMaterial.AlbedoColor = new Color(plateColor, 1f);
        }

        if (_ringMaterial is not null)
        {
            Color ringColor = IsArmed ? ArmedRingColor : ConsumedRingColor;
            _ringMaterial.AlbedoColor = ringColor;
            _ringMaterial.EmissionEnabled = true;
            _ringMaterial.Emission = ringColor;
            _ringMaterial.EmissionEnergyMultiplier = IsArmed ? 1.45f : 0.08f;
            _ringMaterial.Metallic = 0.05f;
            _ringMaterial.Roughness = IsArmed ? 0.2f : 0.7f;
            _ringMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            _ringMaterial.AlbedoColor = new Color(ringColor, IsArmed ? 0.85f : 0.3f);
        }

        if (_coreMaterial is not null)
        {
            Color coreColor = IsArmed ? ArmedCoreColor : ConsumedCoreColor;
            _coreMaterial.AlbedoColor = coreColor;
            _coreMaterial.EmissionEnabled = true;
            _coreMaterial.Emission = coreColor;
            _coreMaterial.EmissionEnergyMultiplier = IsArmed ? 1.35f : 0.15f;
            _coreMaterial.Roughness = IsArmed ? 0.14f : 0.58f;
            _coreMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            _coreMaterial.AlbedoColor = new Color(coreColor, 1f);
        }

        if (_glow is not null)
        {
            _glow.Visible = IsArmed;
            _glow.LightColor = IsArmed ? ArmedCoreColor : ConsumedCoreColor;
            _glow.LightEnergy = IsArmed ? 0.45f : 0.05f;
        }

        if (_core is not null)
        {
            _core.Position = new Vector3(_core.Position.X, _baseCoreHeight, _core.Position.Z);
            _core.Scale = Vector3.One;
        }

        if (_ring is not null)
        {
            _ring.Scale = Vector3.One;
        }

        if (_glow is not null)
        {
            _glow.Position = new Vector3(_glow.Position.X, _baseGlowHeight, _glow.Position.Z);
        }
    }

    private void ProcessConsumedFade(double delta)
    {
        _consumedFadeElapsed += delta;
        double fadeTime = _consumedFadeElapsed - ConsumedFadeDelaySeconds;

        if (fadeTime <= 0d)
        {
            return;
        }

        float fadeProgress = Mathf.Clamp((float)(fadeTime / Mathf.Max(0.01f, ConsumedFadeDurationSeconds)), 0f, 1f);
        float alpha = 1f - fadeProgress;

        if (_plateMaterial is not null)
        {
            Color plateColor = _plateMaterial.AlbedoColor;
            _plateMaterial.AlbedoColor = new Color(plateColor, alpha);
        }

        if (_ringMaterial is not null)
        {
            Color ringColor = _ringMaterial.AlbedoColor;
            _ringMaterial.AlbedoColor = new Color(ringColor, alpha * 0.3f);
            _ringMaterial.EmissionEnergyMultiplier = 0.08f * alpha;
        }

        if (_coreMaterial is not null)
        {
            Color coreColor = _coreMaterial.AlbedoColor;
            _coreMaterial.AlbedoColor = new Color(coreColor, alpha);
            _coreMaterial.EmissionEnergyMultiplier = 0.15f * alpha;
        }

        if (_glow is not null)
        {
            _glow.Visible = alpha > 0.02f;
            _glow.LightEnergy = 0.05f * alpha;
        }

        if (fadeProgress < 1f)
        {
            return;
        }

        _isConsumedFading = false;
        Visible = false;
    }
}