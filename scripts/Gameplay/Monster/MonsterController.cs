#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Maze.Model;

namespace Maze.Gameplay.Monster;

public partial class MonsterController : Node3D
{
    public enum MonsterState
    {
        Idle,
        Wander,
        Moving
    }

    [Export] public float HoverAmplitude { get; set; } = 0.08f;
    [Export] public float HoverSpeed { get; set; } = 1.8f;
    [Export] public float StandHeight { get; set; } = 0.28f;
    [Export] public float MoveSpeedCellsPerSecond { get; set; } = 1.35f;
    [Export] public float PauseBetweenMoves { get; set; } = 0.3f;

    private global::Maze.Model.Maze? _maze;
    private float _cellSize = 1f;
    private float _hoverTime;
    private float _pauseElapsed;
    private Vector3 _basePosition;
    private Vector3 _moveStartPosition;
    private Vector3 _moveTargetPosition;
    private float _moveElapsed;
    private float _moveDuration;
    private bool _isMoving;
    private Vector2I? _previousCell;

    public Vector2I SpawnCell { get; private set; }
    public Vector2I CurrentCell { get; private set; }
    public bool CanBeStunned { get; private set; }
    public MonsterState CurrentState { get; private set; } = MonsterState.Idle;
    public event Action<MonsterController, Vector2I>? CellChanged;

    public override void _Ready()
    {
        Visible = false;
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        _hoverTime += (float)delta * HoverSpeed;

        if (_isMoving)
        {
            UpdateMovement((float)delta);
        }
        else
        {
            _pauseElapsed += (float)delta;
            if (_pauseElapsed >= PauseBetweenMoves)
            {
                TryStartNextMove();
            }
        }

        ApplyHoverOffset();
    }

    public void Configure(global::Maze.Model.Maze maze, Vector2I spawnCell, float cellSize, bool canBeStunned)
    {
        _maze = maze;
        SpawnCell = spawnCell;
        CurrentCell = spawnCell;
        _cellSize = Mathf.Max(0.1f, cellSize);
        CanBeStunned = canBeStunned;
        CurrentState = MonsterState.Idle;
        _hoverTime = 0f;
        _pauseElapsed = 0f;
        _moveElapsed = 0f;
        _moveDuration = 0f;
        _isMoving = false;
        _previousCell = null;
        _basePosition = CellToWorld(spawnCell);
        Position = _basePosition;
    }

    public void ActivateMonster()
    {
        Visible = true;
        SetProcess(true);
        CurrentState = MonsterState.Wander;
        CellChanged?.Invoke(this, CurrentCell);
    }

    public void DeactivateMonster()
    {
        Visible = false;
        SetProcess(false);
        _isMoving = false;
        _pauseElapsed = 0f;
        CurrentState = MonsterState.Idle;
        Position = _basePosition;
    }

    private void UpdateMovement(float delta)
    {
        _moveElapsed += delta;
        float t = Mathf.Clamp(_moveElapsed / _moveDuration, 0f, 1f);
        _basePosition = _moveStartPosition.Lerp(_moveTargetPosition, t);

        if (t < 1f)
        {
            return;
        }

        _isMoving = false;
        _moveElapsed = 0f;
        _pauseElapsed = 0f;
        CurrentState = MonsterState.Wander;
        _basePosition = _moveTargetPosition;
        CellChanged?.Invoke(this, CurrentCell);
    }

    private void TryStartNextMove()
    {
        if (_maze is null || !_maze.IsInside(CurrentCell.X, CurrentCell.Y))
        {
            return;
        }

        Cell currentCell = _maze.GetCell(CurrentCell.X, CurrentCell.Y);
        List<Cell> reachableNeighbors = GetReachableNeighbors(_maze, currentCell);
        if (reachableNeighbors.Count == 0)
        {
            return;
        }

        Cell nextCell = SelectWanderNeighbor(reachableNeighbors);
        _previousCell = CurrentCell;
        CurrentCell = new Vector2I(nextCell.X, nextCell.Y);
        _moveStartPosition = _basePosition;
        _moveTargetPosition = CellToWorld(CurrentCell);
        _moveDuration = 1f / Mathf.Max(0.1f, MoveSpeedCellsPerSecond);
        _moveElapsed = 0f;
        _isMoving = true;
        CurrentState = MonsterState.Moving;
        FaceMovementDirection(_moveTargetPosition - _moveStartPosition);
    }

    private Cell SelectWanderNeighbor(List<Cell> reachableNeighbors)
    {
        if (_previousCell is not Vector2I previousCell)
        {
            return reachableNeighbors[GD.RandRange(0, reachableNeighbors.Count - 1)];
        }

        List<Cell> forwardNeighbors = new();
        foreach (Cell neighbor in reachableNeighbors)
        {
            if (neighbor.X == previousCell.X && neighbor.Y == previousCell.Y)
            {
                continue;
            }

            forwardNeighbors.Add(neighbor);
        }

        List<Cell> selectionPool = forwardNeighbors.Count > 0 ? forwardNeighbors : reachableNeighbors;
        return selectionPool[GD.RandRange(0, selectionPool.Count - 1)];
    }

    private void ApplyHoverOffset()
    {
        Vector3 position = _basePosition;
        position.Y += Mathf.Sin(_hoverTime) * HoverAmplitude;
        Position = position;
    }

    private static List<Cell> GetReachableNeighbors(global::Maze.Model.Maze maze, Cell cell)
    {
        List<Cell> neighbors = new();

        foreach (Direction direction in DirectionHelper.All)
        {
            if (cell.HasWall(direction))
            {
                continue;
            }

            Cell? neighbor = maze.GetNeighbor(cell, direction);
            if (neighbor is not null)
            {
                neighbors.Add(neighbor);
            }
        }

        return neighbors;
    }

    private Vector3 CellToWorld(Vector2I cell) =>
        new(cell.X * _cellSize + _cellSize / 2f, StandHeight, cell.Y * _cellSize + _cellSize / 2f);

    private void FaceMovementDirection(Vector3 movement)
    {
        Vector3 planarMovement = new(movement.X, 0f, movement.Z);
        if (planarMovement.LengthSquared() <= 0.0001f)
        {
            return;
        }

        Rotation = new Vector3(0f, Mathf.Atan2(planarMovement.X, planarMovement.Z), 0f);
    }
}