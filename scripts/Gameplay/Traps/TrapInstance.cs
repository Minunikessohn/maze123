#nullable enable

using Godot;
using Maze.Game;

namespace Maze.Gameplay.Traps;

public partial class TrapInstance : Node3D
{
    private static readonly Color ArmedPlateColor = new("#5f2320");
    private static readonly Color ArmedCoreColor = new("#ff7043");
    private static readonly Color ConsumedPlateColor = new("#2a2d31");
    private static readonly Color ConsumedCoreColor = new("#4d555f");

    [Export] public float HoverHeight { get; set; } = 0.025f;

    private MeshInstance3D? _plate;
    private MeshInstance3D? _core;
    private OmniLight3D? _glow;
    private StandardMaterial3D? _plateMaterial;
    private StandardMaterial3D? _coreMaterial;

    public string TrapId { get; private set; } = TrapDefinition.DefaultTrapId;
    public Vector2I Cell { get; private set; }
    public bool IsArmed { get; private set; } = true;

    public override void _Ready()
    {
        _plate = GetNodeOrNull<MeshInstance3D>("Plate");
        _core = GetNodeOrNull<MeshInstance3D>("Core");
        _glow = GetNodeOrNull<OmniLight3D>("Glow");

        if (_plate?.GetActiveMaterial(0) is StandardMaterial3D plateMaterial)
        {
            _plateMaterial = (StandardMaterial3D)plateMaterial.Duplicate();
            _plate.SetSurfaceOverrideMaterial(0, _plateMaterial);
        }

        if (_core?.GetActiveMaterial(0) is StandardMaterial3D coreMaterial)
        {
            _coreMaterial = (StandardMaterial3D)coreMaterial.Duplicate();
            _core.SetSurfaceOverrideMaterial(0, _coreMaterial);
        }

        ApplyVisualState();
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
        ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        if (_plateMaterial is not null)
        {
            Color plateColor = IsArmed ? ArmedPlateColor : ConsumedPlateColor;
            _plateMaterial.AlbedoColor = plateColor;
            _plateMaterial.Metallic = IsArmed ? 0.35f : 0.12f;
            _plateMaterial.Roughness = IsArmed ? 0.22f : 0.65f;
        }

        if (_coreMaterial is not null)
        {
            Color coreColor = IsArmed ? ArmedCoreColor : ConsumedCoreColor;
            _coreMaterial.AlbedoColor = coreColor;
            _coreMaterial.EmissionEnabled = true;
            _coreMaterial.Emission = coreColor;
            _coreMaterial.EmissionEnergyMultiplier = IsArmed ? 1.35f : 0.15f;
            _coreMaterial.Roughness = IsArmed ? 0.14f : 0.58f;
        }

        if (_glow is not null)
        {
            _glow.Visible = IsArmed;
            _glow.LightColor = IsArmed ? ArmedCoreColor : ConsumedCoreColor;
            _glow.LightEnergy = IsArmed ? 0.45f : 0.05f;
        }
    }
}