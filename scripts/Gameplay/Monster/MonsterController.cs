#nullable enable

using Godot;

namespace Maze.Gameplay.Monster;

public partial class MonsterController : Node3D
{
    public enum MonsterState
    {
        Idle
    }

    [Export] public float HoverAmplitude { get; set; } = 0.08f;
    [Export] public float HoverSpeed { get; set; } = 1.8f;
    [Export] public float StandHeight { get; set; } = 0.28f;

    private float _cellSize = 1f;
    private float _hoverTime;
    private Vector3 _basePosition;

    public Vector2I SpawnCell { get; private set; }
    public bool CanBeStunned { get; private set; }
    public MonsterState CurrentState { get; private set; } = MonsterState.Idle;

    public override void _Ready()
    {
        Visible = false;
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        _hoverTime += (float)delta * HoverSpeed;
        Vector3 position = _basePosition;
        position.Y += Mathf.Sin(_hoverTime) * HoverAmplitude;
        Position = position;
    }

    public void Configure(Vector2I spawnCell, float cellSize, bool canBeStunned)
    {
        SpawnCell = spawnCell;
        _cellSize = Mathf.Max(0.1f, cellSize);
        CanBeStunned = canBeStunned;
        CurrentState = MonsterState.Idle;
        _hoverTime = 0f;
        _basePosition = CellToWorld(spawnCell);
        Position = _basePosition;
    }

    public void ActivateMonster()
    {
        Visible = true;
        SetProcess(true);
    }

    public void DeactivateMonster()
    {
        Visible = false;
        SetProcess(false);
        Position = _basePosition;
    }

    private Vector3 CellToWorld(Vector2I cell) =>
        new(cell.X * _cellSize + _cellSize / 2f, StandHeight, cell.Y * _cellSize + _cellSize / 2f);
}