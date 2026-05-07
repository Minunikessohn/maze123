#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Maze.Model;

namespace Maze.Gameplay.Monster;

public partial class MonsterController : Node3D
{
    private const string DefaultImportedModelScenePath = "res://assets/monsters/Slenderman Model 3.fbx";

    public enum MonsterState
    {
        Idle,
        Wander,
        Chase,
        Search,
        Stunned
    }

    private static readonly Color NormalBodyColor = new(0.75f, 0.22f, 0.22f, 1f);
    private static readonly Color NormalEmissionColor = new(0.58f, 0.08f, 0.08f, 1f);
    private static readonly Color ChaseBodyColor = new(0.92f, 0.36f, 0.18f, 1f);
    private static readonly Color ChaseEmissionColor = new(0.85f, 0.26f, 0.08f, 1f);
    private static readonly Color SearchBodyColor = new(0.78f, 0.5f, 0.18f, 1f);
    private static readonly Color SearchEmissionColor = new(0.62f, 0.38f, 0.08f, 1f);
    private static readonly Color StunnedBodyColor = new(0.3f, 0.62f, 0.95f, 1f);
    private static readonly Color StunnedEmissionColor = new(0.2f, 0.5f, 0.9f, 1f);

    [Export] public float HoverAmplitude { get; set; } = 0.08f;
    [Export] public float HoverSpeed { get; set; } = 1.8f;
    [Export] public float StandHeight { get; set; } = 0.28f;
    [Export] public float MoveSpeedCellsPerSecond { get; set; } = 1.35f;
    [Export] public float PauseBetweenMoves { get; set; } = 0.3f;
    [Export] public int MaxSightRangeCells { get; set; } = 13;
    [Export] public float IdleDurationSeconds { get; set; } = 0.35f;
    [Export] public float ChaseMemoryDurationSeconds { get; set; } = 5f;
    [Export] public float SearchDurationSeconds { get; set; } = 1.6f;
    [Export] public float DefaultStunDurationSeconds { get; set; } = 2.4f;
    [Export] public float VisualScaleFactor { get; set; } = 1.05f;
    [Export] public float RevealDistanceCells { get; set; } = 1.35f;
    [Export] public string ImportedModelScenePath { get; set; } = DefaultImportedModelScenePath;

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
    private bool _isActive;
    private bool _hasBeenRevealed;
    private bool _simulationPaused;
    private float _idleElapsed;
    private float _chaseMemoryRemaining;
    private float _searchElapsed;
    private float _stunElapsed;
    private Vector2I? _previousCell;
    private Node3D? _bodyRoot;
    private Node3D? _modelAnchor;
    private readonly List<StandardMaterial3D> _bodyMaterials = new();
    private MeshInstance3D? _fallbackBody;
    private bool _hasImportedVisuals;
    private OmniLight3D? _glowLight;

    public Vector2I SpawnCell { get; private set; }
    public Vector2I CurrentCell { get; private set; }
    public bool CanBeStunned { get; private set; }
    public bool CanSeePlayerNow { get; private set; }
    public Vector2I? LastSeenPlayerCell { get; private set; }
    public MonsterState CurrentState { get; private set; } = MonsterState.Idle;
    public event Action<MonsterController, Vector2I>? CellChanged;

    public Vector3 StunAnchorGlobalPosition => GlobalPosition;

    public override void _Ready()
    {
        _bodyRoot = GetNodeOrNull<Node3D>("Body");
        _modelAnchor = GetNodeOrNull<Node3D>("Body/ModelAnchor");
        _fallbackBody = GetNodeOrNull<MeshInstance3D>("Body/FallbackBody");
        _bodyMaterials.Clear();
        _hasImportedVisuals = false;
        if (_bodyRoot is not null)
        {
            TryAttachImportedModel();
            _hasImportedVisuals = HasImportedVisuals(_bodyRoot);
            CollectBodyMaterials(_bodyRoot);
        }

        if (_fallbackBody is not null)
        {
            _fallbackBody.Visible = !_hasImportedVisuals;
        }

        _glowLight = GetNodeOrNull<OmniLight3D>("Glow");
        Visible = false;
        SetProcess(false);
        ApplyStateVisuals(MonsterState.Idle);
    }

    public override void _Process(double delta)
    {
        float deltaSeconds = (float)delta;
        _hoverTime += deltaSeconds * HoverSpeed;

        if (CurrentState == MonsterState.Stunned)
        {
            UpdateStun(deltaSeconds);
            RefreshVisibility();
            ApplyHoverOffset();
            return;
        }

        UpdatePlayerVisibility();
        UpdateBehaviorState(deltaSeconds);

        if (CurrentState == MonsterState.Idle)
        {
            _pauseElapsed = 0f;
            RefreshVisibility();
            ApplyHoverOffset();
            return;
        }

        if (_isMoving)
        {
            UpdateMovement(deltaSeconds);
        }
        else
        {
            _pauseElapsed += deltaSeconds;
            if (_pauseElapsed >= PauseBetweenMoves)
            {
                TryStartNextMove();
            }
        }

        RefreshVisibility();
        ApplyHoverOffset();
    }

    public void Configure(global::Maze.Model.Maze maze, Vector2I spawnCell, float cellSize, bool canBeStunned)
    {
        _maze = maze;
        SpawnCell = spawnCell;
        CurrentCell = spawnCell;
        _cellSize = Mathf.Max(0.1f, cellSize);
        CanBeStunned = canBeStunned;
        _hoverTime = 0f;
        _pauseElapsed = 0f;
        _moveElapsed = 0f;
        _moveDuration = 0f;
        _isMoving = false;
        _isActive = false;
        _hasBeenRevealed = false;
        _simulationPaused = false;
        _idleElapsed = IdleDurationSeconds;
        _chaseMemoryRemaining = 0f;
        _searchElapsed = 0f;
        _stunElapsed = 0f;
        _previousCell = null;
        CanSeePlayerNow = false;
        LastSeenPlayerCell = null;
        ApplyVisualScale();
        _basePosition = CellToWorld(spawnCell);
        Position = _basePosition;
        SetCurrentState(MonsterState.Idle);
    }

    public void SetPlayerCell(Vector2I? playerCell)
    {
        _playerCell = playerCell;

        if (CurrentState == MonsterState.Stunned || _simulationPaused)
        {
            return;
        }

        UpdatePlayerVisibility();
    }

    public void ActivateMonster()
    {
        _isActive = true;
        Visible = false;
        _hasBeenRevealed = false;
        _simulationPaused = false;
        SetProcess(true);
        _idleElapsed = IdleDurationSeconds;
        _pauseElapsed = 0f;
        _stunElapsed = 0f;
        SetCurrentState(MonsterState.Idle);
        RefreshVisibility();
        CellChanged?.Invoke(this, CurrentCell);
    }

    public void DeactivateMonster()
    {
        _isActive = false;
        Visible = false;
        _hasBeenRevealed = false;
        _simulationPaused = false;
        SetProcess(false);
        _isMoving = false;
        _pauseElapsed = 0f;
        _idleElapsed = 0f;
        _chaseMemoryRemaining = 0f;
        _searchElapsed = 0f;
        _stunElapsed = 0f;
        SetCurrentState(MonsterState.Idle);
        Position = _basePosition;
    }

    public void SetSimulationPaused(bool paused)
    {
        _simulationPaused = paused;
        SetProcess(_isActive && !paused);
    }

    public bool TryStun(float durationSeconds = -1f)
    {
        if (!CanBeStunned || !_isActive)
        {
            return false;
        }

        float effectiveDuration = durationSeconds > 0f ? durationSeconds : DefaultStunDurationSeconds;
        if (effectiveDuration <= 0f)
        {
            return false;
        }

        bool wasMoving = _isMoving;
        _isMoving = false;
        _moveElapsed = 0f;
        _moveDuration = 0f;
        _pauseElapsed = 0f;
        _idleElapsed = 0f;
        _chaseMemoryRemaining = 0f;
        _searchElapsed = 0f;
        _stunElapsed = effectiveDuration;
        _hasBeenRevealed = true;
        CanSeePlayerNow = false;
        LastSeenPlayerCell = null;
        CurrentCell = WorldToCell(Position);
        _basePosition = CellToWorld(CurrentCell);
        Position = _basePosition;
        SetCurrentState(MonsterState.Stunned);

        if (wasMoving)
        {
            CellChanged?.Invoke(this, CurrentCell);
        }

        return true;
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

    private void ApplyVisualScale()
    {
        float visualScale = _hasImportedVisuals
            ? Mathf.Max(1.2f, _cellSize * VisualScaleFactor * 0.9f)
            : Mathf.Max(0.7f, _cellSize * VisualScaleFactor * 0.38f);

        if (_bodyRoot is not null)
        {
            _bodyRoot.Scale = Vector3.One * visualScale;
        }

        if (_glowLight is not null)
        {
            _glowLight.OmniRange = Mathf.Max(1.9f, _cellSize * 1.55f);
        }
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
            && LastSeenPlayerCell is Vector2I rememberedPlayerCell
            && _chaseMemoryRemaining > 0f
            && rememberedPlayerCell != CurrentCell
            && AdvanceAlongPath(rememberedPlayerCell, MonsterState.Chase))
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
        SetCurrentState(nextState);
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
            _chaseMemoryRemaining = 0f;
            LastSeenPlayerCell = null;
            _searchElapsed = 0f;
            RefreshVisibility();
            return;
        }

        CanSeePlayerNow = CanSeePlayer(CurrentCell, playerCell, MaxSightRangeCells);
        if (!CanSeePlayerNow)
        {
            RefreshVisibility();
            return;
        }

        LastSeenPlayerCell = playerCell;
        _chaseMemoryRemaining = Mathf.Max(0f, ChaseMemoryDurationSeconds);
        _searchElapsed = SearchDurationSeconds;
        _hasBeenRevealed = true;
        if (!_isMoving)
        {
            FaceMovementDirection(CellToWorld(playerCell) - _basePosition);
        }

        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        if (!_isActive)
        {
            Visible = false;
            return;
        }

        if (ShouldBeVisibleNow())
        {
            _hasBeenRevealed = true;
        }

        Visible = _hasBeenRevealed;
    }

    private bool ShouldBeVisibleNow()
    {
        if (CurrentState == MonsterState.Stunned || CurrentState == MonsterState.Chase)
        {
            return true;
        }

        if (CanSeePlayerNow || CurrentState == MonsterState.Search)
        {
            return true;
        }

        if (_playerCell is not Vector2I playerCell)
        {
            return false;
        }

        Vector2 cellOffset = new(CurrentCell.X - playerCell.X, CurrentCell.Y - playerCell.Y);
        return cellOffset.Length() <= Mathf.Max(0.1f, RevealDistanceCells);
    }

    private void UpdateBehaviorState(float delta)
    {
        if (CurrentState == MonsterState.Stunned)
        {
            return;
        }

        if (_idleElapsed > 0f)
        {
            _idleElapsed = Mathf.Max(0f, _idleElapsed - delta);
            SetCurrentState(MonsterState.Idle);
            return;
        }

        if (CanSeePlayerNow)
        {
            SetCurrentState(MonsterState.Chase);
            return;
        }

        if (LastSeenPlayerCell is not Vector2I lastSeenPlayerCell)
        {
            SetCurrentState(MonsterState.Wander);
            return;
        }

        if (lastSeenPlayerCell != CurrentCell && _chaseMemoryRemaining > 0f)
        {
            _chaseMemoryRemaining = Mathf.Max(0f, _chaseMemoryRemaining - delta);
            SetCurrentState(MonsterState.Chase);
            return;
        }

        if (lastSeenPlayerCell == CurrentCell)
        {
            _chaseMemoryRemaining = 0f;
        }

        if (_searchElapsed > 0f)
        {
            _searchElapsed = Mathf.Max(0f, _searchElapsed - delta);
            SetCurrentState(MonsterState.Search);
            return;
        }

        _chaseMemoryRemaining = 0f;
        LastSeenPlayerCell = null;
        SetCurrentState(MonsterState.Wander);
    }

    private void UpdateStun(float delta)
    {
        if (_stunElapsed <= 0f)
        {
            RecoverFromStun();
            return;
        }

        _stunElapsed = Mathf.Max(0f, _stunElapsed - delta);
        if (_stunElapsed <= 0f)
        {
            RecoverFromStun();
        }
    }

    private void RecoverFromStun()
    {
        _stunElapsed = 0f;
        _idleElapsed = IdleDurationSeconds;
        SetCurrentState(MonsterState.Idle);
    }

    private void SetCurrentState(MonsterState nextState)
    {
        if (CurrentState == nextState)
        {
            return;
        }

        CurrentState = nextState;
        ApplyStateVisuals(nextState);
    }

    private void ApplyStateVisuals(MonsterState state)
    {
        Color bodyColor = NormalBodyColor;
        Color emissionColor = NormalEmissionColor;
        float emissionEnergy = 1.4f;
        Color glowColor = new(0.95f, 0.25f, 0.2f, 1f);
        float glowEnergy = 0.6f;

        switch (state)
        {
            case MonsterState.Chase:
                bodyColor = ChaseBodyColor;
                emissionColor = ChaseEmissionColor;
                emissionEnergy = 1.9f;
                glowColor = new Color(1f, 0.35f, 0.1f, 1f);
                glowEnergy = 1.05f;
                break;
            case MonsterState.Search:
                bodyColor = SearchBodyColor;
                emissionColor = SearchEmissionColor;
                emissionEnergy = 1.55f;
                glowColor = new Color(0.95f, 0.62f, 0.2f, 1f);
                glowEnergy = 0.82f;
                break;
            case MonsterState.Stunned:
                bodyColor = StunnedBodyColor;
                emissionColor = StunnedEmissionColor;
                emissionEnergy = 1.15f;
                glowColor = new Color(0.45f, 0.75f, 1f, 1f);
                glowEnergy = 0.42f;
                break;
        }

        foreach (StandardMaterial3D bodyMaterial in _bodyMaterials)
        {
            bodyMaterial.AlbedoColor = bodyColor;
            bodyMaterial.Emission = emissionColor;
            bodyMaterial.EmissionEnergyMultiplier = emissionEnergy;
            bodyMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Disabled;
        }

        if (_glowLight is not null)
        {
            _glowLight.LightColor = glowColor;
            _glowLight.LightEnergy = glowEnergy;
        }
    }

    private void CollectBodyMaterials(Node node)
    {
        if (node is MeshInstance3D meshInstance)
        {
            int surfaceCount = meshInstance.Mesh?.GetSurfaceCount() ?? 0;
            for (int surfaceIndex = 0; surfaceIndex < surfaceCount; surfaceIndex++)
            {
                if (meshInstance.GetActiveMaterial(surfaceIndex) is not StandardMaterial3D bodyMaterial)
                {
                    continue;
                }

                StandardMaterial3D duplicate = (StandardMaterial3D)bodyMaterial.Duplicate();
                ForceOpaqueMaterial(duplicate);
                meshInstance.SetSurfaceOverrideMaterial(surfaceIndex, duplicate);
                _bodyMaterials.Add(duplicate);
            }
        }

        foreach (Node child in node.GetChildren())
        {
            CollectBodyMaterials(child);
        }
    }

    private bool HasImportedVisuals(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child == _fallbackBody)
            {
                continue;
            }

            if (child is MeshInstance3D meshInstance && meshInstance.Mesh is not null)
            {
                return true;
            }

            if (HasImportedVisuals(child))
            {
                return true;
            }
        }

        return false;
    }

    private void TryAttachImportedModel()
    {
        if (_modelAnchor is null)
        {
            return;
        }

        foreach (Node child in _modelAnchor.GetChildren())
        {
            child.QueueFree();
        }

        if (string.IsNullOrWhiteSpace(ImportedModelScenePath))
        {
            return;
        }

        PackedScene? modelScene = GD.Load<PackedScene>(ImportedModelScenePath);
        if (modelScene is null)
        {
            GD.PushWarning($"Monster model could not be loaded: {ImportedModelScenePath}");
            return;
        }

        Node? instance = modelScene.InstantiateOrNull<Node>();
        if (instance is null)
        {
            GD.PushWarning($"Monster model could not be instantiated: {ImportedModelScenePath}");
            return;
        }

        instance.Name = "ImportedModel";
        _modelAnchor.AddChild(instance);
    }

    private static void ForceOpaqueMaterial(StandardMaterial3D material)
    {
        Color albedoColor = material.AlbedoColor;
        albedoColor.A = 1f;
        material.AlbedoColor = albedoColor;
        material.Transparency = BaseMaterial3D.TransparencyEnum.Disabled;
        material.NoDepthTest = false;
        material.DistanceFadeMode = BaseMaterial3D.DistanceFadeModeEnum.Disabled;
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
        if (monsterCell.X == playerCell.X)
        {
            int distance = Math.Abs(playerCell.Y - monsterCell.Y);
            if (distance > clampedRange)
            {
                return false;
            }

            Direction direction = playerCell.Y > monsterCell.Y ? Direction.South : Direction.North;
            return HasClearSightLine(maze, monsterCell, direction, distance);
        }

        if (monsterCell.Y == playerCell.Y)
        {
            int distance = Math.Abs(playerCell.X - monsterCell.X);
            if (distance > clampedRange)
            {
                return false;
            }

            Direction direction = playerCell.X > monsterCell.X ? Direction.East : Direction.West;
            return HasClearSightLine(maze, monsterCell, direction, distance);
        }

        return false;
    }

    private static bool HasClearSightLine(global::Maze.Model.Maze maze, Vector2I origin, Direction direction, int distance)
    {
        Cell currentCell = maze.GetCell(origin.X, origin.Y);

        for (int step = 0; step < distance; step++)
        {
            if (currentCell.HasWall(direction))
            {
                return false;
            }

            Cell? neighbor = maze.GetNeighbor(currentCell, direction);
            if (neighbor is null)
            {
                return false;
            }

            currentCell = neighbor;
        }

        return true;
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
        global::Maze.MazeWorldGrid.CellToWorldCenter(cell, _cellSize, StandHeight);

    private Vector2I WorldToCell(Vector3 position) =>
        _maze is null
            ? global::Maze.MazeWorldGrid.WorldToCell(position, _cellSize)
            : global::Maze.MazeWorldGrid.WorldToCell(position, _cellSize, _maze.Width, _maze.Height);

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