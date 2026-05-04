#nullable enable

using System.Collections.Generic;
using Godot;
using Maze.Model;

namespace Maze.Views;

/// <summary>
/// Die im 3D-Maze sichtbare Spielfigur. Haelt eine Liste von Wegpunkten
/// (in Welt-Koordinaten) und interpoliert pro Frame zwischen ihnen.
/// </summary>
public partial class PlayerCharacter3D : Node3D
{
    [Signal] public delegate void GoalReachedEventHandler();

    [Export] public float MoveSpeed = 4f;
    [Export] public float StandHeight = 0f;

    public enum Mode
    {
        Idle,
        FollowingPath,
        Manual
    }

    private readonly List<Vector3> _waypoints = new();
    private int _currentIndex;
    private bool _isMoving;
    private float _cellSize = 1f;
    private global::Maze.Model.Maze? _manualMaze;
    private Cell? _manualCell;
    private Cell? _manualGoal;
    private CameraController3D? _manualCamera;
    private bool _isAnimatingCell;
    private Vector3 _animFrom;
    private Vector3 _animTo;
    private float _animElapsed;
    private float _animDuration;
    private LegoFigure? _figure;

    public bool IsMoving => _isMoving;
    public Mode CurrentMode { get; private set; } = Mode.Idle;

    public override void _Ready()
    {
        _figure = GetNodeOrNull<LegoFigure>("Figure");
    }

    public new void Hide()
    {
        _waypoints.Clear();
        _currentIndex = 0;
        _isMoving = false;
        _figure?.SetWalking(false);
        _manualMaze = null;
        _manualCell = null;
        _manualGoal = null;
        _isAnimatingCell = false;
        CurrentMode = Mode.Idle;
        Visible = false;
    }

    public void StartFollowingPath(List<Cell> path, float cellSize)
    {
        _cellSize = cellSize;
        _waypoints.Clear();

        foreach (Cell cell in path)
        {
            _waypoints.Add(CellToWorld(cell));
        }

        CurrentMode = Mode.FollowingPath;

        if (_waypoints.Count == 0)
        {
            Hide();
            return;
        }

        Position = _waypoints[0];
        Visible = true;
        _currentIndex = 1;
        _isMoving = _waypoints.Count > 1;

        if (!_isMoving)
        {
            CurrentMode = Mode.Idle;
            EmitSignal(SignalName.GoalReached);
        }
    }

    public void EnableManualMode(global::Maze.Model.Maze maze, Cell start, Cell goal, float cellSize, CameraController3D camera)
    {
        _cellSize = cellSize;
        _manualMaze = maze;
        _manualCell = start;
        _manualGoal = goal;
        _manualCamera = camera;
        _isAnimatingCell = false;
        _waypoints.Clear();
        _currentIndex = 0;
        _isMoving = false;

        Position = CellToWorld(start);
        Visible = true;
        CurrentMode = Mode.Manual;
    }

    public void DisableManualMode()
    {
        _manualMaze = null;
        _manualCell = null;
        _manualGoal = null;
        _manualCamera = null;
        _isAnimatingCell = false;
        _figure?.SetWalking(false);
        _waypoints.Clear();
        _currentIndex = 0;
        _isMoving = false;
        Visible = false;
        CurrentMode = Mode.Idle;
    }

    public override void _Process(double delta)
    {
        switch (CurrentMode)
        {
            case Mode.FollowingPath:
                ProcessFollowPath(delta);
                break;
            case Mode.Manual:
                ProcessManual(delta);
                break;
        }
    }

    private void ProcessFollowPath(double delta)
    {
        if (!_isMoving)
        {
            _figure?.SetWalking(false);
            return;
        }

        _figure?.SetWalking(true);

        Vector3 target = _waypoints[_currentIndex];
        Vector3 toTarget = target - Position;
    FaceMovementDirection(toTarget);
        float remaining = toTarget.Length();
        float step = MoveSpeed * _cellSize * (float)delta;

        if (step >= remaining)
        {
            Position = target;
            _currentIndex++;
            if (_currentIndex >= _waypoints.Count)
            {
                _isMoving = false;
                _figure?.SetWalking(false);
                CurrentMode = Mode.Idle;
                EmitSignal(SignalName.GoalReached);
            }

            return;
        }

        Position += toTarget.Normalized() * step;
    }

    private void ProcessManual(double delta)
    {
        if (_manualMaze is null || _manualCell is null || _manualGoal is null || _manualCamera is null)
        {
            _figure?.SetWalking(false);
            CurrentMode = Mode.Idle;
            return;
        }

        _figure?.SetWalking(_isAnimatingCell);

        if (_isAnimatingCell)
        {
            _animElapsed += (float)delta;
            float t = Mathf.Clamp(_animElapsed / _animDuration, 0f, 1f);
            Position = _animFrom.Lerp(_animTo, t);

            if (t >= 1f)
            {
                _isAnimatingCell = false;
                _figure?.SetWalking(false);
                Position = _animTo;
                if (_manualCell == _manualGoal)
                {
                    CurrentMode = Mode.Idle;
                    EmitSignal(SignalName.GoalReached);
                }
            }

            return;
        }

        Direction? direction = null;
        if (Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.D))
        {
            direction = _manualCamera.GetFacingDirectionForInput();
        }

        if (direction is null || _manualCell.HasWall(direction.Value))
        {
            return;
        }

        Cell? next = _manualMaze.GetNeighbor(_manualCell, direction.Value);
        if (next is null)
        {
            return;
        }

        _animFrom = Position;
        _animTo = CellToWorld(next);
        _animElapsed = 0f;
        _animDuration = 1f / Mathf.Max(0.5f, MoveSpeed);
        _isAnimatingCell = true;
        _manualCell = next;
        FaceMovementDirection(_animTo - _animFrom);
    }

    private Vector3 CellToWorld(Cell cell) =>
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