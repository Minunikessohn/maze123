#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Maze.Model;
using Maze.Network;

namespace Maze.Views;

/// <summary>
/// Die im 3D-Maze sichtbare Spielfigur. Haelt eine Liste von Wegpunkten
/// (in Welt-Koordinaten) und interpoliert pro Frame zwischen ihnen.
/// </summary>
public partial class PlayerCharacter3D : CharacterBody3D
{
    [Signal] public delegate void GoalReachedEventHandler(long peerId);
    [Signal] public delegate void CellVisitedEventHandler(long peerId, int x, int y);
    [Signal] public delegate void StaminaChangedEventHandler(long peerId, float current, float maximum, bool sprinting);

    [Export] public float PathMoveSpeed = 4f;
    [Export] public float ManualMoveSpeed = 2.2f;
    [Export] public float SprintMultiplier = 1.75f;
    [Export] public float MaxStamina = 5f;
    [Export] public float StaminaDrainPerSecond = 1.1f;
    [Export] public float StaminaRecoveryPerSecond = 0.8f;
    [Export] public float StaminaRecoveryDelaySeconds = 0.85f;
    [Export] public float StandHeight = 0f;
    [Export] public float CollisionRadius = 0.42f;
    [Export] public float FigureHeightFactor = 0.58f;

    public enum Mode
    {
        Idle,
        FollowingPath,
        Manual
    }

    public enum ControlAuthority
    {
        Scripted,
        LocalInput,
        Replicated
    }

    private readonly List<Vector3> _waypoints = new();
    private int _currentIndex;
    private bool _isMoving;
    private float _cellSize = 1f;
    private global::Maze.Model.Maze? _manualMaze;
    private Vector2I? _manualGoalCell;
    private CameraController3D? _manualCamera;
    private Vector2I? _currentPlayerCell;
    private LegoFigure? _figure;
    private bool _firstPersonActive;
    private float _currentStamina;
    private float _effectiveMaximumStamina;
    private float _staminaRecoveryDelayRemaining;
    private bool _isSprinting;

    public bool IsMoving => _isMoving;
    public Vector2I? CurrentPlayerCell => _currentPlayerCell;
    public float CurrentStamina => _currentStamina;
    public float MaximumStamina => _effectiveMaximumStamina;
    public bool IsSprinting => _isSprinting;
    public long PeerId { get; private set; }
    public ControlAuthority Authority { get; private set; } = ControlAuthority.Scripted;
    public Mode CurrentMode { get; private set; } = Mode.Idle;
    public bool IsManualModeActive => CurrentMode == Mode.Manual;
    public bool UsesLocalInput => CurrentMode == Mode.Manual && Authority == ControlAuthority.LocalInput;
    public bool IsReplicatedAvatar => CurrentMode == Mode.Manual && Authority == ControlAuthority.Replicated;

    public override void _Ready()
    {
        _figure = GetNodeOrNull<LegoFigure>("Figure");
        _effectiveMaximumStamina = MaxStamina;
        ResetStamina(emitSignal: false);
        ApplyVisualScale();
    }

    public void AssignPeerId(long peerId)
    {
        PeerId = peerId;
    }

    public new void Hide()
    {
        _waypoints.Clear();
        _currentIndex = 0;
        _isMoving = false;
        Velocity = Vector3.Zero;
        _figure?.SetWalking(false);
        _manualMaze = null;
        _manualGoalCell = null;
        _currentPlayerCell = null;
        _isSprinting = false;
        _isMoving = false;
        ResetStamina();
        Authority = ControlAuthority.Scripted;
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
        Authority = ControlAuthority.Scripted;

        if (_waypoints.Count == 0)
        {
            Hide();
            return;
        }

        _currentPlayerCell = null;
        ApplyVisualScale();
        Position = _waypoints[0];
        Visible = true;
        UpdateCurrentPlayerCell(forceEmit: true);
        _currentIndex = 1;
        _isMoving = _waypoints.Count > 1;
        Velocity = Vector3.Zero;

        if (!_isMoving)
        {
            CurrentMode = Mode.Idle;
            EmitSignal(SignalName.GoalReached, PeerId);
        }
    }

    public void EnableManualMode(
        global::Maze.Model.Maze maze,
        Cell start,
        Cell goal,
        float cellSize,
        CameraController3D? camera,
        ControlAuthority authority = ControlAuthority.LocalInput)
    {
        if (authority == ControlAuthority.LocalInput && camera is null)
        {
            throw new ArgumentNullException(nameof(camera));
        }

        _cellSize = cellSize;
        _manualMaze = maze;
        _manualGoalCell = new Vector2I(goal.X, goal.Y);
        _manualCamera = camera;
        Authority = authority;
        _waypoints.Clear();
        _currentIndex = 0;
        _isMoving = false;
        _isSprinting = false;
        Velocity = Vector3.Zero;
        ResetStamina();

        ApplyVisualScale();
        Position = CellToWorld(start);
        Visible = true;
        CurrentMode = Mode.Manual;
        _currentPlayerCell = null;
        UpdateCurrentPlayerCell(forceEmit: true);
    }

    public void ApplyReplicatedRuntimeState(global::Maze.Model.Maze maze, Cell goal, float cellSize, PlayerRuntimeState runtimeState)
    {
        _cellSize = cellSize;
        _manualMaze = maze;
        _manualGoalCell = new Vector2I(goal.X, goal.Y);
        _manualCamera = null;
        _waypoints.Clear();
        _currentIndex = 0;
        _staminaRecoveryDelayRemaining = 0f;
        _currentStamina = Mathf.Max(0f, runtimeState.CurrentStamina);
        _effectiveMaximumStamina = runtimeState.MaximumStamina > 0f ? runtimeState.MaximumStamina : MaxStamina;
        _isSprinting = false;
        _isMoving = false;
        Velocity = Vector3.Zero;
        Authority = runtimeState.IsManualMode ? ControlAuthority.Replicated : ControlAuthority.Scripted;
        CurrentMode = runtimeState.IsManualMode ? Mode.Manual : Mode.Idle;

        Vector2I currentCell = runtimeState.CurrentCell.ToVector2I();
        GlobalPosition = global::Maze.MazeWorldGrid.CellToWorldCenter(currentCell, _cellSize, StandHeight);
        _currentPlayerCell = currentCell;
        ApplyVisualScale();
        _figure?.SetWalking(false);
        Visible = true;
        EmitStaminaChanged();
    }

    public void DisableManualMode()
    {
        _manualMaze = null;
        _manualGoalCell = null;
        _manualCamera = null;
        _currentPlayerCell = null;
        _figure?.SetWalking(false);
        _waypoints.Clear();
        _currentIndex = 0;
        _isMoving = false;
        _isSprinting = false;
        Velocity = Vector3.Zero;
        ResetStamina();
        Authority = ControlAuthority.Scripted;
        Visible = false;
        CurrentMode = Mode.Idle;
    }

    public void ResetManualPosition(Cell start)
    {
        if (CurrentMode != Mode.Manual)
        {
            return;
        }

        Position = CellToWorld(start);
        Velocity = Vector3.Zero;
        _isMoving = false;
        _isSprinting = false;
        _figure?.SetWalking(false);
        ResetStamina();
        _currentPlayerCell = null;
        UpdateCurrentPlayerCell(forceEmit: true);
    }

    public void SetFirstPersonActive(bool active)
    {
        _firstPersonActive = active;

        if (_figure is not null)
        {
            _figure.Visible = !active;
        }
    }

    public Vector3 GetEyeWorldPosition()
    {
        if (_figure?.HeadPivot is not null)
        {
            return _figure.HeadPivot.GlobalPosition;
        }

        return GlobalPosition + new Vector3(0f, 0.45f, 0f);
    }

    public override void _Process(double delta)
    {
        if (CurrentMode == Mode.FollowingPath)
        {
            ProcessFollowPath(delta);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (CurrentMode == Mode.Manual)
        {
            ProcessManual(delta);
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
        float step = PathMoveSpeed * _cellSize * (float)delta;

        if (step >= remaining)
        {
            Position = target;
            UpdateCurrentPlayerCell();
            _currentIndex++;
            if (_currentIndex >= _waypoints.Count)
            {
                _isMoving = false;
                _figure?.SetWalking(false);
                CurrentMode = Mode.Idle;
                EmitSignal(SignalName.GoalReached, PeerId);
            }

            return;
        }

        Position += toTarget.Normalized() * step;
    }

    private void ProcessManual(double delta)
    {
        if (_manualMaze is null || _manualGoalCell is null)
        {
            _figure?.SetWalking(false);
            _isMoving = false;
            _isSprinting = false;
            Velocity = Vector3.Zero;
            CurrentMode = Mode.Idle;
            return;
        }

        if (Authority != ControlAuthority.LocalInput || _manualCamera is null)
        {
            _figure?.SetWalking(false);
            _isMoving = false;
            _isSprinting = false;
            Velocity = Vector3.Zero;
            UpdateCurrentPlayerCell();
            return;
        }

        float deltaSeconds = (float)delta;
        Vector3 moveDirection = _manualCamera.GetGroundMoveDirectionForInput();
        bool wantsToMove = moveDirection != Vector3.Zero;
        bool wantsSprint = wantsToMove && CanSprintNow();
        float movementSpeed = GetCurrentManualSpeed(wantsSprint);
        Vector3 desiredMotion = wantsToMove ? moveDirection * movementSpeed * deltaSeconds : Vector3.Zero;
        Vector3 actualMotion = ResolveManualMotion(desiredMotion);

        if (deltaSeconds > 0f)
        {
            Velocity = new Vector3(actualMotion.X / deltaSeconds, 0f, actualMotion.Z / deltaSeconds);
        }
        else
        {
            Velocity = Vector3.Zero;
        }

        if (actualMotion.LengthSquared() > 0.000001f)
        {
            GlobalPosition += actualMotion;
            _isMoving = true;
            FaceMovementDirection(actualMotion);
        }
        else
        {
            _isMoving = false;
            Velocity = Vector3.Zero;
        }

        _isSprinting = wantsSprint && _isMoving;
        _figure?.SetWalking(_isMoving);
        UpdateStamina(delta);

        if (!UpdateCurrentPlayerCell() || _currentPlayerCell != _manualGoalCell)
        {
            return;
        }

        _isMoving = false;
        _isSprinting = false;
        Velocity = Vector3.Zero;
        _figure?.SetWalking(false);
        CurrentMode = Mode.Idle;
        EmitSignal(SignalName.GoalReached, PeerId);
    }

    private float GetCurrentManualSpeed(bool sprinting) =>
        ManualMoveSpeed * _cellSize * (sprinting ? SprintMultiplier : 1f);

    private bool CanSprintNow() =>
        Input.IsPhysicalKeyPressed(Key.Shift) && _currentStamina > 0.05f;

    private void UpdateStamina(double delta)
    {
        float previousStamina = _currentStamina;
        bool previousSprintState = _isSprinting;
        float deltaSeconds = (float)delta;

        if (_isMoving && _isSprinting)
        {
            _currentStamina = Mathf.Max(0f, _currentStamina - StaminaDrainPerSecond * deltaSeconds);
            _staminaRecoveryDelayRemaining = StaminaRecoveryDelaySeconds;

            if (_currentStamina <= 0.001f)
            {
                _currentStamina = 0f;
                _isSprinting = false;
            }
        }
        else if (_staminaRecoveryDelayRemaining > 0f)
        {
            _staminaRecoveryDelayRemaining = Mathf.Max(0f, _staminaRecoveryDelayRemaining - deltaSeconds);
        }
        else if (_currentStamina < MaxStamina)
        {
            _currentStamina = Mathf.Min(MaxStamina, _currentStamina + StaminaRecoveryPerSecond * deltaSeconds);
        }

        if (!Mathf.IsEqualApprox(previousStamina, _currentStamina) || previousSprintState != _isSprinting)
        {
            EmitStaminaChanged();
        }
    }

    private void ResetStamina(bool emitSignal = true)
    {
        _effectiveMaximumStamina = MaxStamina;
        _currentStamina = _effectiveMaximumStamina;
        _staminaRecoveryDelayRemaining = 0f;
        _isSprinting = false;

        if (emitSignal)
        {
            EmitStaminaChanged();
        }
    }

    private void EmitStaminaChanged() =>
        EmitSignal(SignalName.StaminaChanged, PeerId, _currentStamina, _effectiveMaximumStamina, _isSprinting);

    private void ApplyVisualScale()
    {
        if (_figure is null)
        {
            return;
        }

        float targetHeight = Mathf.Max(0.6f, _cellSize * FigureHeightFactor);
        float baseFigureHeight = 32f;
        float figureScale = targetHeight / baseFigureHeight;
        _figure.Scale = Vector3.One * figureScale;
    }

    private Vector3 ResolveManualMotion(Vector3 desiredMotion)
    {
        if (_manualMaze is null || desiredMotion == Vector3.Zero)
        {
            return Vector3.Zero;
        }

        Vector3 resolvedPosition = GlobalPosition;
        resolvedPosition.Y = StandHeight;

        if (!Mathf.IsZeroApprox(desiredMotion.X))
        {
            resolvedPosition.X = ResolveAxisMotion(resolvedPosition, desiredMotion.X, horizontal: true);
        }

        if (!Mathf.IsZeroApprox(desiredMotion.Z))
        {
            resolvedPosition.Z = ResolveAxisMotion(resolvedPosition, desiredMotion.Z, horizontal: false);
        }

        resolvedPosition.Y = StandHeight;
        return resolvedPosition - GlobalPosition;
    }

    private float ResolveAxisMotion(Vector3 position, float delta, bool horizontal)
    {
        if (_manualMaze is null)
        {
            return (horizontal ? position.X : position.Z) + delta;
        }

        float cellSize = Mathf.Max(0.001f, _cellSize);
        float radius = Mathf.Clamp(CollisionRadius, 0.01f, cellSize * 0.49f);
        Vector2I cell = WorldToCell(position);
        Cell mazeCell = _manualMaze.GetCell(cell.X, cell.Y);
        Aabb cellBounds = global::Maze.MazeWorldGrid.GetCellBounds(cell, cellSize, StandHeight);

        float nextValue = (horizontal ? position.X : position.Z) + delta;
        float minValue = radius;
        float maxValue = (horizontal ? _manualMaze.Width : _manualMaze.Height) * cellSize - radius;

        if (horizontal)
        {
            if (mazeCell.HasWall(Direction.West))
            {
                minValue = Mathf.Max(minValue, cellBounds.Position.X + radius);
            }

            if (mazeCell.HasWall(Direction.East))
            {
                maxValue = Mathf.Min(maxValue, cellBounds.Position.X + cellBounds.Size.X - radius);
            }
        }
        else
        {
            if (mazeCell.HasWall(Direction.North))
            {
                minValue = Mathf.Max(minValue, cellBounds.Position.Z + radius);
            }

            if (mazeCell.HasWall(Direction.South))
            {
                maxValue = Mathf.Min(maxValue, cellBounds.Position.Z + cellBounds.Size.Z - radius);
            }
        }

        return Mathf.Clamp(nextValue, minValue, maxValue);
    }

    private bool UpdateCurrentPlayerCell(bool forceEmit = false)
    {
        Vector2I cell = WorldToCell(GlobalPosition);
        if (!forceEmit && _currentPlayerCell == cell)
        {
            return false;
        }

        _currentPlayerCell = cell;
        EmitSignal(SignalName.CellVisited, PeerId, cell.X, cell.Y);
        return true;
    }

    private Vector3 CellToWorld(Cell cell) =>
        global::Maze.MazeWorldGrid.CellToWorldCenter(cell, _cellSize, StandHeight);

    private Vector2I WorldToCell(Vector3 position) =>
        _manualMaze is null
            ? global::Maze.MazeWorldGrid.WorldToCell(position, _cellSize)
            : global::Maze.MazeWorldGrid.WorldToCell(position, _cellSize, _manualMaze.Width, _manualMaze.Height);

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