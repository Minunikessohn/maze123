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
        Chase,
        Search,
        Moving
    }

    [Export] public float HoverAmplitude { get; set; } = 0.08f;
    [Export] public float HoverSpeed { get; set; } = 1.8f;
    [Export] public float StandHeight { get; set; } = 0.28f;
    [Export] public float MoveSpeedCellsPerSecond { get; set; } = 1.35f;
    [Export] public float PauseBetweenMoves { get; set; } = 0.3f;
    [Export] public int MaxSightRangeCells { get; set; } = 13;
    [Export] public float SearchDurationSeconds { get; set; } = 1.6f;

    private global::Maze.Model.Maze? _maze;
    private Vector2I? _playerCell;
    private float _cellSize = 1f;
    private float _hoverTime;
    private float _pauseElapsed;
    private Vector3 _basePosition;
    private Vector3 _moveStartPosition;
    private Vector3 _moveTargetPosition;
    private float _moveElapsed;
    private float _moveDuration;
    private bool _isMoving;
    private float _searchElapsed;
    private Vector2I? _previousCell;

    public Vector2I SpawnCell { get; private set; }
    public Vector2I CurrentCell { get; private set; }
    public bool CanBeStunned { get; private set; }
    public bool CanSeePlayerNow { get; private set; }
    public Vector2I? LastSeenPlayerCell { get; private set; }
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
        UpdatePlayerVisibility();
        UpdateBehaviorState((float)delta);

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
        _searchElapsed = 0f;
        _previousCell = null;
        CanSeePlayerNow = false;
        LastSeenPlayerCell = null;
        _basePosition = CellToWorld(spawnCell);
        Position = _basePosition;
    }

    public void SetPlayerCell(Vector2I? playerCell)
    {
        _playerCell = playerCell;
        UpdatePlayerVisibility();
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
        _searchElapsed = 0f;
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
        _basePosition = _moveTargetPosition;
        UpdatePlayerVisibility();
        UpdateBehaviorState(0f);
        CellChanged?.Invoke(this, CurrentCell);
    }

    private void TryStartNextMove()
    {
        if (_maze is null || !_maze.IsInside(CurrentCell.X, CurrentCell.Y))
        {
            return;
        }

        Cell currentCell = _maze.GetCell(CurrentCell.X, CurrentCell.Y);
        if (CanSeePlayerNow && _playerCell is Vector2I playerCell && AdvanceAlongPath(playerCell, MonsterState.Chase))
        {
            return;
        }

        if (!CanSeePlayerNow
            && LastSeenPlayerCell is Vector2I lastSeenPlayerCell
            && _searchElapsed > 0f
            && lastSeenPlayerCell != CurrentCell
            && AdvanceAlongPath(lastSeenPlayerCell, MonsterState.Search))
        {
            return;
        }

        List<Cell> reachableNeighbors = GetReachableNeighbors(_maze, currentCell);
        if (reachableNeighbors.Count == 0)
        {
            return;
        }

        Cell nextCell = SelectWanderNeighbor(reachableNeighbors);
        StartMove(nextCell, MonsterState.Wander);
    }

    private void StartMove(Cell nextCell, MonsterState nextState)
    {
        _previousCell = CurrentCell;
        CurrentCell = new Vector2I(nextCell.X, nextCell.Y);
        _moveStartPosition = _basePosition;
        _moveTargetPosition = CellToWorld(CurrentCell);
        _moveDuration = 1f / Mathf.Max(0.1f, MoveSpeedCellsPerSecond);
        _moveElapsed = 0f;
        _isMoving = true;
        CurrentState = nextState;
        FaceMovementDirection(_moveTargetPosition - _moveStartPosition);
    }

    private bool AdvanceAlongPath(Vector2I targetCell, MonsterState nextState)
    {
        List<Vector2I> path = FindPathToPlayer(CurrentCell, targetCell);
        if (path.Count < 2)
        {
            return false;
        }

        Vector2I nextCell = path[1];
        StartMove(_maze!.GetCell(nextCell.X, nextCell.Y), nextState);
        return true;
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

    private void UpdatePlayerVisibility()
    {
        if (_playerCell is not Vector2I playerCell || _maze is null)
        {
            CanSeePlayerNow = false;
            return;
        }

        CanSeePlayerNow = CanSeePlayer(CurrentCell, playerCell, MaxSightRangeCells);
        if (!CanSeePlayerNow)
        {
            return;
        }

        LastSeenPlayerCell = playerCell;
        _searchElapsed = SearchDurationSeconds;
        if (!_isMoving)
        {
            FaceMovementDirection(CellToWorld(playerCell) - _basePosition);
        }
    }

    private void UpdateBehaviorState(float delta)
    {
        if (CanSeePlayerNow)
        {
            CurrentState = MonsterState.Chase;
            return;
        }

        if (LastSeenPlayerCell is not Vector2I lastSeenPlayerCell)
        {
            CurrentState = MonsterState.Wander;
            return;
        }

        if (lastSeenPlayerCell == CurrentCell)
        {
            LastSeenPlayerCell = null;
            _searchElapsed = 0f;
            CurrentState = MonsterState.Wander;
            return;
        }

        if (_searchElapsed > 0f)
        {
            _searchElapsed = Mathf.Max(0f, _searchElapsed - delta);
            CurrentState = MonsterState.Search;
            return;
        }

        LastSeenPlayerCell = null;
        CurrentState = MonsterState.Wander;
    }

    private bool CanSeePlayer(Vector2I monsterCell, Vector2I playerCell, int maxRangeCells)
    {
        if (_maze is null)
        {
            return false;
        }

        return CanSeePlayer(_maze, monsterCell, playerCell, maxRangeCells);
    }

    private List<Vector2I> FindPathToPlayer(Vector2I startCell, Vector2I playerCell)
    {
        if (_maze is null || !_maze.IsInside(startCell.X, startCell.Y) || !_maze.IsInside(playerCell.X, playerCell.Y))
        {
            return new List<Vector2I>();
        }

        if (startCell == playerCell)
        {
            return new List<Vector2I> { startCell };
        }

        PriorityQueue<Vector2I, int> frontier = new();
        Dictionary<Vector2I, Vector2I> cameFrom = new();
        Dictionary<Vector2I, int> costSoFar = new() { [startCell] = 0 };

        frontier.Enqueue(startCell, 0);
        cameFrom[startCell] = startCell;

        while (frontier.Count > 0)
        {
            Vector2I current = frontier.Dequeue();
            if (current == playerCell)
            {
                return ReconstructPath(cameFrom, startCell, playerCell);
            }

            Cell currentCell = _maze.GetCell(current.X, current.Y);
            foreach (Cell neighbor in GetReachableNeighbors(_maze, currentCell))
            {
                Vector2I neighborCell = new(neighbor.X, neighbor.Y);
                int nextCost = costSoFar[current] + 1;
                if (costSoFar.TryGetValue(neighborCell, out int existingCost) && existingCost <= nextCost)
                {
                    continue;
                }

                costSoFar[neighborCell] = nextCost;
                cameFrom[neighborCell] = current;
                frontier.Enqueue(neighborCell, nextCost + GetHeuristicCost(neighborCell, playerCell));
            }
        }

        return new List<Vector2I>();
    }

    private static bool CanSeePlayer(global::Maze.Model.Maze maze, Vector2I monsterCell, Vector2I playerCell, int maxRangeCells)
    {
        if (!maze.IsInside(monsterCell.X, monsterCell.Y) || !maze.IsInside(playerCell.X, playerCell.Y))
        {
            return false;
        }

        if (monsterCell == playerCell)
        {
            return true;
        }

        int clampedRange = Math.Max(0, maxRangeCells);
        Dictionary<Vector2I, int> distances = new() { [monsterCell] = 0 };
        Queue<Vector2I> frontier = new();
        frontier.Enqueue(monsterCell);

        while (frontier.Count > 0)
        {
            Vector2I current = frontier.Dequeue();
            int currentDistance = distances[current];
            if (currentDistance >= clampedRange)
            {
                continue;
            }

            Cell currentCell = maze.GetCell(current.X, current.Y);
            foreach (Cell neighbor in GetReachableNeighbors(maze, currentCell))
            {
                Vector2I neighborCell = new(neighbor.X, neighbor.Y);
                if (distances.ContainsKey(neighborCell))
                {
                    continue;
                }

                int nextDistance = currentDistance + 1;
                if (neighborCell == playerCell)
                {
                    return nextDistance <= clampedRange;
                }

                distances[neighborCell] = nextDistance;
                frontier.Enqueue(neighborCell);
            }
        }

        return false;
    }

    private static List<Vector2I> ReconstructPath(Dictionary<Vector2I, Vector2I> cameFrom, Vector2I startCell, Vector2I goalCell)
    {
        if (!cameFrom.ContainsKey(goalCell))
        {
            return new List<Vector2I>();
        }

        List<Vector2I> path = new();
        Vector2I current = goalCell;
        path.Add(current);

        while (current != startCell)
        {
            current = cameFrom[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private static int GetHeuristicCost(Vector2I from, Vector2I to) =>
        Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y);

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