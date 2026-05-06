#nullable enable

using System.Collections.Generic;
using Godot;
using Maze.Game;
using Maze.Model;
using Maze.World;

namespace Maze.Gameplay.Monster;

public partial class MonsterManager : Node3D
{
    private const string MonsterScenePath = "res://scenes/Monster.tscn";

    private readonly List<Vector2I> _spawnCells = new();
    private readonly List<Vector2I> _activeMonsterCells = new();
    private readonly List<MonsterController> _activeMonsters = new();
    private readonly Dictionary<MonsterController, int> _monsterIndices = new();

    private PackedScene _monsterScene = null!;
    private DayNightController? _dayNightController;
    private MazeGameConfig? _config;
    private global::Maze.Model.Maze? _maze;
    private Vector2I? _playerCell;
    private float _cellSize = 1f;

    public IReadOnlyList<Vector2I> ActiveMonsterCells => _activeMonsterCells;

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
    }

    public void Synchronize(bool shouldBeActive)
    {
        if (!shouldBeActive || !CanSpawnMonsters())
        {
            DespawnAll();
            return;
        }

        if (_activeMonsters.Count == _spawnCells.Count)
        {
            return;
        }

        SpawnAll();
    }

    public void UpdatePlayerCell(Vector2I? playerCell)
    {
        _playerCell = playerCell;

        foreach (MonsterController monster in _activeMonsters)
        {
            monster.SetPlayerCell(playerCell);
        }
    }

    private void OnDayStarted() => Synchronize(false);

    private void OnNightStarted() => Synchronize(true);

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
            monster.Configure(_maze!, spawnCell, _cellSize, _config?.MonsterCanBeStunned ?? false);
            monster.SetPlayerCell(_playerCell);
            _monsterIndices[monster] = _activeMonsterCells.Count;
            _activeMonsterCells.Add(monster.CurrentCell);
            monster.ActivateMonster();
            _activeMonsters.Add(monster);
        }
    }

    private void DespawnAll()
    {
        foreach (MonsterController monster in _activeMonsters)
        {
            if (IsInstanceValid(monster))
            {
                monster.CellChanged -= OnMonsterCellChanged;
                monster.QueueFree();
            }
        }

        _monsterIndices.Clear();
        _activeMonsters.Clear();
        _activeMonsterCells.Clear();
    }

    private void OnMonsterCellChanged(MonsterController monster, Vector2I cell)
    {
        if (!_monsterIndices.TryGetValue(monster, out int index) || index < 0 || index >= _activeMonsterCells.Count)
        {
            return;
        }

        _activeMonsterCells[index] = cell;
    }
}