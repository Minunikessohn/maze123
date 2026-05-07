#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Maze.Game;
using Maze.Gameplay.Traps;
using Maze.Model;
using Maze.World;

namespace Maze.Gameplay.Monster;

public enum MonsterSimulationMode
{
    Inactive,
    Frozen,
    Active
}

public partial class MonsterManager : Node3D
{
    private const string MonsterScenePath = "res://scenes/Monster.tscn";

    private readonly List<Vector2I> _spawnCells = new();
    private readonly List<Vector2I> _activeMonsterCells = new();
    private readonly List<MonsterController> _activeMonsters = new();
    private readonly Dictionary<MonsterController, int> _monsterIndices = new();
    private readonly HashSet<MonsterController> _stunOverlapMonsters = new();

    private PackedScene _monsterScene = null!;
    private DayNightController? _dayNightController;
    private TrapManager? _trapManager;
    private MazeGameConfig? _config;
    private global::Maze.Model.Maze? _maze;
    private Vector2I? _playerCell;
    private float _cellSize = 1f;
    private float _playerWalkSpeedCellsPerSecond = 2.2f;
    private bool _requiresSpawn = true;

    public IReadOnlyList<Vector2I> ActiveMonsterCells => _activeMonsterCells;
    public event Action<MonsterController>? PlayerSpotted;

    public override void _Ready()
    {
        _monsterScene = GD.Load<PackedScene>(MonsterScenePath);
    }

    public override void _ExitTree()
    {
        if (_dayNightController is null)
        {
            return;
        }

        _dayNightController.DayStarted -= OnDayStarted;
        _dayNightController.NightStarted -= OnNightStarted;
        _dayNightController = null;
    }

    public void BindDayNightController(DayNightController controller)
    {
        if (_dayNightController == controller)
        {
            return;
        }

        if (_dayNightController is not null)
        {
            _dayNightController.DayStarted -= OnDayStarted;
            _dayNightController.NightStarted -= OnNightStarted;
        }

        _dayNightController = controller;
        _dayNightController.DayStarted += OnDayStarted;
        _dayNightController.NightStarted += OnNightStarted;
    }

    public void BindTrapManager(TrapManager? trapManager)
    {
        _trapManager = trapManager;
    }

    public void Configure(MazeGameConfig? config, global::Maze.Model.Maze? maze, IEnumerable<Vector2I> spawnCells, float cellSize)
    {
        _config = config;
        _maze = maze;
        _cellSize = Mathf.Max(0.1f, cellSize);
        _spawnCells.Clear();

        HashSet<Vector2I> uniqueCells = new();
        foreach (Vector2I cell in spawnCells)
        {
            if (uniqueCells.Add(cell))
            {
                _spawnCells.Add(cell);
            }
        }

        DespawnAll();
        _requiresSpawn = true;
    }

    public void Synchronize(MonsterSimulationMode mode)
    {
        if (mode == MonsterSimulationMode.Inactive || !CanSpawnMonsters())
        {
            DespawnAll();
            _requiresSpawn = true;
            return;
        }

        if (_requiresSpawn)
        {
            SpawnAll();
        }

        bool shouldPauseSimulation = mode == MonsterSimulationMode.Frozen;
        foreach (MonsterController monster in _activeMonsters)
        {
            monster.SetSimulationPaused(shouldPauseSimulation);
        }
    }

    public void UpdatePlayerCell(Vector2I? playerCell)
    {
        _playerCell = playerCell;

        foreach (MonsterController monster in _activeMonsters)
        {
            monster.SetPlayerCell(playerCell);
        }
    }

    public void SetPlayerWalkSpeed(float playerWalkSpeedCellsPerSecond)
    {
        _playerWalkSpeedCellsPerSecond = Mathf.Max(0.1f, playerWalkSpeedCellsPerSecond);

        foreach (MonsterController monster in _activeMonsters)
        {
            monster.SetPlayerWalkSpeed(_playerWalkSpeedCellsPerSecond);
        }
    }

    public bool TryStunClosestMonsterInRadius(Vector3 worldPosition, float radius, float durationSeconds = -1f)
    {
        if (radius <= 0f)
        {
            return false;
        }

        MonsterController? closestMonster = null;
        float maxDistanceSquared = radius * radius;
        float closestDistanceSquared = maxDistanceSquared;

        foreach (MonsterController monster in _activeMonsters)
        {
            float distanceSquared = worldPosition.DistanceSquaredTo(monster.StunAnchorGlobalPosition);
            if (distanceSquared > closestDistanceSquared)
            {
                continue;
            }

            closestMonster = monster;
            closestDistanceSquared = distanceSquared;
        }

        if (closestMonster is null || !closestMonster.TryStun(durationSeconds))
        {
            return false;
        }

        OnMonsterCellChanged(closestMonster, closestMonster.CurrentCell);
        return true;
    }

    public int TryStunMonstersInRadius(Vector3 worldPosition, float radius, float durationSeconds = -1f)
    {
        if (radius <= 0f)
        {
            return 0;
        }

        int stunnedCount = 0;
        float maxDistanceSquared = radius * radius;

        for (int index = _activeMonsters.Count - 1; index >= 0; index--)
        {
            MonsterController monster = _activeMonsters[index];
            if (worldPosition.DistanceSquaredTo(monster.StunAnchorGlobalPosition) > maxDistanceSquared)
            {
                continue;
            }

            if (!monster.TryStun(durationSeconds))
            {
                continue;
            }

            OnMonsterCellChanged(monster, monster.CurrentCell);
            stunnedCount++;
        }

        return stunnedCount;
    }

    public int UpdateStunCollision(Vector3 worldPosition, float radius, float durationSeconds = -1f)
    {
        if (radius <= 0f)
        {
            _stunOverlapMonsters.Clear();
            return 0;
        }

        HashSet<MonsterController> overlappingMonsters = new();
        int stunnedCount = 0;
        float maxDistanceSquared = radius * radius;

        for (int index = _activeMonsters.Count - 1; index >= 0; index--)
        {
            MonsterController monster = _activeMonsters[index];
            if (worldPosition.DistanceSquaredTo(monster.StunAnchorGlobalPosition) > maxDistanceSquared)
            {
                continue;
            }

            overlappingMonsters.Add(monster);
            if (_stunOverlapMonsters.Contains(monster) || !monster.TryStun(durationSeconds))
            {
                continue;
            }

            OnMonsterCellChanged(monster, monster.CurrentCell);
            stunnedCount++;
        }

        _stunOverlapMonsters.Clear();
        foreach (MonsterController monster in overlappingMonsters)
        {
            _stunOverlapMonsters.Add(monster);
        }

        return stunnedCount;
    }

    private void OnDayStarted() => Synchronize(MonsterSimulationMode.Inactive);

    private void OnNightStarted() => Synchronize(MonsterSimulationMode.Active);

    private bool CanSpawnMonsters() =>
        _config is not null
        && _maze is not null
        && _config.MonsterGenerationEnabled
        && _spawnCells.Count > 0;

    private void SpawnAll()
    {
        DespawnAll();

        if (!CanSpawnMonsters())
        {
            return;
        }

        foreach (Vector2I spawnCell in _spawnCells)
        {
            MonsterController monster = _monsterScene.Instantiate<MonsterController>();
            AddChild(monster);
            monster.CellChanged += OnMonsterCellChanged;
            monster.PlayerSpotted += OnMonsterPlayerSpotted;
            monster.Configure(_maze!, spawnCell, _cellSize, _config?.MonsterCanBeStunned ?? false);
            monster.SetPlayerWalkSpeed(_playerWalkSpeedCellsPerSecond);
            monster.SetPlayerCell(_playerCell);
            _monsterIndices[monster] = _activeMonsterCells.Count;
            _activeMonsterCells.Add(monster.CurrentCell);
            monster.ActivateMonster();
            _activeMonsters.Add(monster);
        }

        _requiresSpawn = false;
    }

    private void DespawnAll()
    {
        for (int index = _activeMonsters.Count - 1; index >= 0; index--)
        {
            DespawnMonsterAtIndex(index);
        }

        _monsterIndices.Clear();
        _activeMonsters.Clear();
        _activeMonsterCells.Clear();
        _stunOverlapMonsters.Clear();
    }

    private void DespawnMonster(MonsterController monster)
    {
        if (!_monsterIndices.TryGetValue(monster, out int removedIndex))
        {
            return;
        }

        DespawnMonsterAtIndex(removedIndex);
        RebuildMonsterIndices();
    }

    private void DespawnMonsterAtIndex(int removedIndex)
    {
        if (removedIndex < 0 || removedIndex >= _activeMonsters.Count)
        {
            return;
        }

        MonsterController monster = _activeMonsters[removedIndex];

        monster.CellChanged -= OnMonsterCellChanged;
        monster.PlayerSpotted -= OnMonsterPlayerSpotted;
        _monsterIndices.Remove(monster);
        _stunOverlapMonsters.Remove(monster);

        _activeMonsters.RemoveAt(removedIndex);

        if (removedIndex < _activeMonsterCells.Count)
        {
            _activeMonsterCells.RemoveAt(removedIndex);
        }

        if (IsInstanceValid(monster))
        {
            monster.QueueFree();
        }
    }

    private void RebuildMonsterIndices()
    {
        _monsterIndices.Clear();

        for (int index = 0; index < _activeMonsters.Count; index++)
        {
            _monsterIndices[_activeMonsters[index]] = index;
        }
    }

    private void OnMonsterCellChanged(MonsterController monster, Vector2I cell)
    {
        if (!_monsterIndices.TryGetValue(monster, out int index) || index < 0 || index >= _activeMonsterCells.Count)
        {
            return;
        }

        _activeMonsterCells[index] = cell;

        if (_trapManager?.TryConsumeTrapAtCell(cell) == true)
        {
            DespawnMonster(monster);
        }
    }

    private void OnMonsterPlayerSpotted(MonsterController monster)
    {
        PlayerSpotted?.Invoke(monster);
    }
}