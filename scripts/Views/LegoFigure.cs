#nullable enable

using Godot;

namespace Maze.Views;

/// <summary>
/// Blockige Spielfigur mit sechs getexturierten Quadern und einfacher Laufanimation.
/// </summary>
public partial class LegoFigure : Node3D
{
    [Export] public Texture2D? AtlasTexture;
    [Export] public float WalkSpeedScale = 8f;
    [Export] public float HeadTurn = 0f;

    public Node3D HeadPivot { get; private set; } = null!;
    public Node3D LeftShoulder { get; private set; } = null!;
    public Node3D RightShoulder { get; private set; } = null!;
    public Node3D LeftHip { get; private set; } = null!;
    public Node3D RightHip { get; private set; } = null!;

    private float _walkPhase;
    private bool _isWalking;

    public override void _Ready()
    {
        StandardMaterial3D material = new()
        {
            AlbedoTexture = AtlasTexture,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
        };

        Node3D hip = new() { Name = "Hip", Position = new Vector3(0f, 12f, 0f) };
        AddChild(hip);

        AddCuboid(hip, "BodyMesh", new Vector3(-4f, 0f, -2f), 8f, 12f, 4f, BodyUvs(), material);

        HeadPivot = new Node3D { Name = "HeadPivot", Position = new Vector3(0f, 12f, 2f) };
        hip.AddChild(HeadPivot);
        AddCuboid(HeadPivot, "HeadMesh", new Vector3(-4f, 0f, -4f), 8f, 8f, 8f, HeadUvs(), material);

        LeftShoulder = new Node3D { Name = "LeftShoulder", Position = new Vector3(-6f, 10f, 0f) };
        hip.AddChild(LeftShoulder);
        AddCuboid(LeftShoulder, "LeftArmMesh", new Vector3(-2f, -10f, -2f), 4f, 12f, 4f, ArmLeftUvs(), material);

        RightShoulder = new Node3D { Name = "RightShoulder", Position = new Vector3(6f, 10f, 0f) };
        hip.AddChild(RightShoulder);
        AddCuboid(RightShoulder, "RightArmMesh", new Vector3(-2f, -10f, -2f), 4f, 12f, 4f, ArmRightUvs(), material);

        LeftHip = new Node3D { Name = "LeftHip", Position = new Vector3(-2f, 0f, 0f) };
        hip.AddChild(LeftHip);
        AddCuboid(LeftHip, "LeftLegMesh", new Vector3(-2f, -12f, -2f), 4f, 12f, 4f, LegLeftUvs(), material);

        RightHip = new Node3D { Name = "RightHip", Position = new Vector3(2f, 0f, 0f) };
        hip.AddChild(RightHip);
        AddCuboid(RightHip, "RightLegMesh", new Vector3(-2f, -12f, -2f), 4f, 12f, 4f, LegRightUvs(), material);

        ApplyPose();
    }

    public override void _Process(double delta)
    {
        if (_isWalking)
        {
            _walkPhase += (float)delta * WalkSpeedScale;
        }

        ApplyPose();
    }

    public void SetWalking(bool walking)
    {
        _isWalking = walking;
        if (!walking)
        {
            ApplyIdlePose();
        }
    }

    private void ApplyPose()
    {
        if (!_isWalking)
        {
            ApplyIdlePose();
            return;
        }

        float phase = _walkPhase;
        HeadPivot.Rotation = new Vector3(Mathf.Sin(phase) / 10f, HeadTurn, 0f);
        LeftShoulder.Rotation = new Vector3(
            Mathf.Sin(phase * 5f / 8f) / 2f,
            0f,
            Mathf.Sin(phase * 9f / 8f) / 8f - 1f / 8f);
        RightShoulder.Rotation = new Vector3(
            Mathf.Sin(phase * 5f / 8f - Mathf.Pi) / 2f,
            0f,
            Mathf.Sin(phase * 9f / 8f - Mathf.Pi) / 8f + 1f / 8f);
        LeftHip.Rotation = new Vector3(Mathf.Sin(phase * 7f / 8f), 0f, 0f);
        RightHip.Rotation = new Vector3(Mathf.Sin(phase * 7f / 8f - Mathf.Pi), 0f, 0f);
    }

    private void ApplyIdlePose()
    {
        HeadPivot.Rotation = new Vector3(0f, HeadTurn, 0f);
        LeftShoulder.Rotation = Vector3.Zero;
        RightShoulder.Rotation = Vector3.Zero;
        LeftHip.Rotation = Vector3.Zero;
        RightHip.Rotation = Vector3.Zero;
    }

    private static void AddCuboid(
        Node3D parent,
        string name,
        Vector3 meshOffset,
        float width,
        float height,
        float depth,
        TexturedCuboid.FaceUvs uvs,
        Material material)
    {
        MeshInstance3D instance = new()
        {
            Name = name,
            Mesh = TexturedCuboid.Build(width, height, depth, uvs),
            Position = meshOffset,
            MaterialOverride = material,
        };
        parent.AddChild(instance);
    }

    private static TexturedCuboid.FaceUvs HeadUvs() => new(
        Front: new TexturedCuboid.UvRect(8, 8, 8, 8),
        Right: new TexturedCuboid.UvRect(16, 8, 8, 8),
        Rear: new TexturedCuboid.UvRect(24, 8, 8, 8),
        Left: new TexturedCuboid.UvRect(0, 8, 8, 8),
        Top: new TexturedCuboid.UvRect(8, 0, 8, 8),
        Bottom: new TexturedCuboid.UvRect(16, 0, 8, 8));

    private static TexturedCuboid.FaceUvs BodyUvs() => new(
        Front: new TexturedCuboid.UvRect(20, 20, 8, 12),
        Right: new TexturedCuboid.UvRect(28, 20, 4, 12),
        Rear: new TexturedCuboid.UvRect(32, 20, 8, 12),
        Left: new TexturedCuboid.UvRect(16, 20, 4, 12),
        Top: new TexturedCuboid.UvRect(20, 16, 8, 4),
        Bottom: new TexturedCuboid.UvRect(28, 16, 8, 4));

    private static TexturedCuboid.FaceUvs ArmLeftUvs() => new(
        Front: new TexturedCuboid.UvRect(44, 20, 4, 12),
        Right: new TexturedCuboid.UvRect(48, 20, 4, 12),
        Rear: new TexturedCuboid.UvRect(52, 20, 4, 12),
        Left: new TexturedCuboid.UvRect(40, 20, 4, 12),
        Top: new TexturedCuboid.UvRect(44, 16, 4, 4),
        Bottom: new TexturedCuboid.UvRect(48, 16, 4, 4));

    private static TexturedCuboid.FaceUvs ArmRightUvs() => new(
        Front: new TexturedCuboid.UvRect(48, 20, -4, 12),
        Right: new TexturedCuboid.UvRect(44, 20, -4, 12),
        Rear: new TexturedCuboid.UvRect(56, 20, -4, 12),
        Left: new TexturedCuboid.UvRect(52, 20, -4, 12),
        Top: new TexturedCuboid.UvRect(48, 16, -4, 4),
        Bottom: new TexturedCuboid.UvRect(52, 16, -4, 4));

    private static TexturedCuboid.FaceUvs LegLeftUvs() => new(
        Front: new TexturedCuboid.UvRect(4, 20, 4, 12),
        Right: new TexturedCuboid.UvRect(8, 20, 4, 12),
        Rear: new TexturedCuboid.UvRect(12, 20, 4, 12),
        Left: new TexturedCuboid.UvRect(0, 20, 4, 12),
        Top: new TexturedCuboid.UvRect(4, 16, 4, 4),
        Bottom: new TexturedCuboid.UvRect(8, 16, 4, 4));

    private static TexturedCuboid.FaceUvs LegRightUvs() => new(
        Front: new TexturedCuboid.UvRect(8, 20, -4, 12),
        Right: new TexturedCuboid.UvRect(4, 20, -4, 12),
        Rear: new TexturedCuboid.UvRect(16, 20, -4, 12),
        Left: new TexturedCuboid.UvRect(12, 20, -4, 12),
        Top: new TexturedCuboid.UvRect(8, 16, -4, 4),
        Bottom: new TexturedCuboid.UvRect(12, 16, -4, 4));
}