#nullable enable

using System.Collections.Generic;
using Godot;
using Maze.Game;

namespace Maze.Gameplay.Traps;

public partial class TrapManager : Node3D
{
    private const string TrapScenePath = "res://scenes/MonsterTrap.tscn";

    private readonly List<Vector2I> _activeTrapCells = new();
    private readonly Dictionary<Vector2I, TrapInstance> _trapInstances = new();

    private PackedScene _trapScene = null!;
    private MazeGameConfig? _config;
    private global::Maze.Model.Maze? _maze;
    private float _cellSize = 1f;

    public IReadOnlyList<Vector2I> ActiveTrapCells => _activeTrapCells;

    public override void _Ready()
    {
        _trapScene = GD.Load<PackedScene>(TrapScenePath);
    }

    public void Configure(MazeGameConfig? config, global::Maze.Model.Maze? maze, IEnumerable<TrapDefinition> trapDefinitions, float cellSize)
    {
        _config = config;
        _maze = maze;
        _cellSize = Mathf.Max(0.1f, cellSize);

        Clear();

        if (!CanSpawnTraps())
        {
            return;
        }

        foreach (TrapDefinition definition in trapDefinitions)
        {
            if (!_maze!.IsInside(definition.Cell.X, definition.Cell.Y) || _trapInstances.ContainsKey(definition.Cell))
            {
                continue;
            }

            TrapInstance trap = _trapScene.Instantiate<TrapInstance>();
            AddChild(trap);
            trap.Configure(definition, _cellSize);
            _trapInstances.Add(definition.Cell, trap);

            if (definition.IsArmed)
            {
                _activeTrapCells.Add(definition.Cell);
            }
        }
    }

    public void Clear()
    {
        foreach (TrapInstance trap in _trapInstances.Values)
        {
            if (IsInstanceValid(trap))
            {
                trap.QueueFree();
            }
        }

        _trapInstances.Clear();
        _activeTrapCells.Clear();
    }

    public bool TryConsumeTrapAtCell(Vector2I cell)
    {
        if (!_trapInstances.TryGetValue(cell, out TrapInstance? trap) || !trap.IsArmed)
        {
            return false;
        }

        trap.SetArmed(false);
        _activeTrapCells.Remove(cell);
        return true;
    }

    public void NotifyPlayerEnteredCell(Vector2I cell)
    {
        if (!_trapInstances.ContainsKey(cell))
        {
            return;
        }

        // Version 1 rule: traps are visible to the player but never affect them.
    }

    private bool CanSpawnTraps() =>
        _config is not null
        && _maze is not null
        && _config.TrapGenerationEnabled;
}