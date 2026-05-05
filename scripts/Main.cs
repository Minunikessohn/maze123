#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Maze.Generators;
using Maze.Model;
using Maze.Solvers;
using Maze.UI;
using Maze.Views;

namespace Maze;

public partial class Main : Node
{
    private const float DefaultStepsPerSecond = 30f;
    private const float MaxSimulationSpeed = 100001f;

    private Hud _hud = null!;
    private StatsPanel _stats = null!;
    private MazeView2D _view2D = null!;
    private MazeView3D _view3D = null!;
    private PlayerCharacter3D _player = null!;
    private AlgorithmRunner _runner = null!;
    private global::Maze.Model.Maze? _currentMaze;
    private global::Maze.Model.Maze? _lastMazeBuiltFor3D;
    private Cell _solverStart = null!;
    private Cell _solverGoal = null!;
    private readonly List<Cell> _solverPath = new();

    private readonly Dictionary<string, IMazeGenerator> _generators = new()
    {
        ["recursive-backtracker"] = new RecursiveBacktrackerGenerator(),
        ["growing-tree"] = new GrowingTreeGenerator(),
        ["recursive-division"] = new RecursiveDivisionGenerator(),
        ["cellular-automata"] = new CellularAutomataGenerator()
    };

    private readonly Dictionary<string, IMazeSolver> _solvers = new()
    {
        ["bfs"] = new BreadthFirstSolver(),
        ["dfs"] = new DepthFirstSolver(),
        ["a-star"] = new AStarSolver(),
        ["greedy"] = new GreedyBestFirstSolver(),
        ["wall-follower"] = new WallFollowerSolver(),
        ["dead-end-filling"] = new DeadEndFillingSolver()
    };

    private readonly Random _random = new();
    private readonly PerformanceTracker _tracker = new();
    private bool _suppressViewRefresh;
    private bool _userRequestedUnboundedMode;
    private bool _followCamEnabled;
    private bool _followCamEnabledBeforeManual;
    private bool _isManualMode;
    private double _manualStartTimeSeconds;

    public override void _Ready()
    {
        _hud = GetNode<Hud>("Hud");
        _stats = GetNode<StatsPanel>("Hud/StatsPanel");
        _view2D = GetNode<MazeView2D>("MazeView2D");
        _view3D = GetNode<MazeView3D>("MazeView3D");
        _player = GetNode<PlayerCharacter3D>("MazeView3D/Player");
        _runner = GetNode<AlgorithmRunner>("Runner");

        _view2D.Visible = true;
        _view3D.Visible = false;

        _hud.GenerateRequested += OnGenerateRequested;
        _hud.SolveRequested += OnSolveRequested;
        _hud.SpeedChanged += OnSpeedChanged;
        _hud.PauseToggle += OnPauseToggled;
        _hud.StepRequested += OnStepRequested;
        _hud.ResetRequested += OnResetRequested;
        _hud.PlayManualToggle += OnPlayManualToggle;
        _hud.ViewToggleRequested += OnViewToggled;
        _hud.HeatmapToggle += OnHeatmapToggled;
        _hud.FollowCamToggle += OnFollowCamToggled;
        _hud.ExploreModeToggle += OnExploreModeToggled;
        _hud.UnboundedModeChanged += OnUnboundedModeChanged;
        _player.GoalReached += OnBotGoalReached;
        _player.CellVisited += OnPlayerCellVisited;

        _runner.GenerationStepProduced += OnGenerationStepProduced;
        _runner.GenerationFinished += OnGenerationFinished;
        _runner.SolverStepProduced += OnSolverStepProduced;
        _runner.SolverFinished += OnSolverFinished;
        ApplySimulationSpeed(DefaultStepsPerSecond);
        ApplyEffectiveRunnerMode();

        GD.Print("[Main] HUD, 2D-View und 3D-View verbunden.");
    }

    public override void _Process(double delta)
    {
    }

    public override void _PhysicsProcess(double delta)
    {
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Space } && _runner.IsPaused)
        {
            _runner.ForceSingleStep();
        }
    }

    public override void _ExitTree() => GD.Print("[Main] _ExitTree.");

    private void OnGenerateRequested(int width, int height, string generatorId)
    {
        OnStopManualRequested();

        if (!_generators.TryGetValue(generatorId, out IMazeGenerator? generator))
        {
            GD.PrintErr($"Unbekannter Generator: {generatorId}");
            return;
        }

        _tracker.Start();
        _runner.StopAll();
        _solverPath.Clear();
        _player.Hide();
        _view3D.ClearTrail();
        ResetExploreMode();
        _view3D.GetNode<CameraController3D>("Camera3D").DisableFollow();
        _currentMaze = new global::Maze.Model.Maze(width, height);
        _lastMazeBuiltFor3D = null;
        _view2D.SetMaze(_currentMaze);
        _view3D.ClearMaze();

        _runner.StartGeneration(generator.Generate(_currentMaze, _random));
        GD.Print($"[Main] Generator {generator.Name} gestartet.");
    }

    private void OnGenerationStepProduced()
    {
        GenerationStep? step = _runner.LastGenerationStep;
        if (step is null || _currentMaze is null)
        {
            return;
        }

        step.Cell.State = step.NewState;
        _tracker.TickStep();
        _tracker.IncrementVisited();

        if (!ShouldRefreshStepViews())
        {
            return;
        }

        _stats.UpdateStats(_tracker.Elapsed, _tracker.Steps, _tracker.VisitedCells, _tracker.PathLength, 0);
        _view2D.Refresh();
    }

    private void OnGenerationFinished()
    {
        if (_currentMaze is null)
        {
            return;
        }

        foreach (Cell cell in _currentMaze.AllCells())
        {
            cell.State = CellState.Open;
        }

        _view2D.ForceRefresh();
        _view3D.SetMaze(_currentMaze);
        _lastMazeBuiltFor3D = _currentMaze;
        _tracker.Stop();
        _stats.UpdateStats(_tracker.Elapsed, _tracker.Steps, _tracker.VisitedCells, 0, _tracker.ManagedMemoryDeltaBytes);
        GD.Print("[Main] Generator fertig.");
    }

    private void OnSolveRequested(string solverId)
    {
        OnStopManualRequested();

        if (_currentMaze is null)
        {
            GD.PrintErr("Kein Maze.");
            return;
        }

        if (!_solvers.TryGetValue(solverId, out IMazeSolver? solver))
        {
            GD.PrintErr($"Unbekannter Solver: {solverId}");
            return;
        }

        _tracker.Start();
        _currentMaze.ResetSolverState();
        _solverPath.Clear();
        _player.Hide();
        _view3D.ClearTrail();
        ResetExploreMode();
        _view3D.GetNode<CameraController3D>("Camera3D").DisableFollow();
        _solverStart = _currentMaze.GetCell(0, 0);
        _solverGoal = _currentMaze.GetCell(_currentMaze.Width - 1, _currentMaze.Height - 1);
        _solverStart.State = CellState.Start;
        _solverGoal.State = CellState.Goal;
        _view2D.Refresh();
        _view3D.Refresh();

        _runner.StopAll();
        _runner.StartSolver(solver.Solve(_currentMaze, _solverStart, _solverGoal));
    }

    private void OnSolverStepProduced()
    {
        SolverStep? step = _runner.LastSolverStep;
        if (step is null)
        {
            return;
        }

        if (step.Cell == _solverStart)
        {
            step.Cell.State = CellState.Start;
        }
        else if (step.Cell == _solverGoal)
        {
            step.Cell.State = CellState.Goal;
        }
        else
        {
            step.Cell.State = step.NewState;
        }

        _tracker.TickStep();
        if (step.NewState == CellState.Visited)
        {
            _tracker.IncrementVisited();
        }

        if (step.NewState == CellState.Path)
        {
            _tracker.SetPathLength(step.Distance + 1);
        }

        step.Cell.Distance = step.Distance;

        if (step.NewState == CellState.Path)
        {
            _solverPath.Add(step.Cell);
        }

        if (!ShouldRefreshStepViews())
        {
            return;
        }

        _stats.UpdateStats(_tracker.Elapsed, _tracker.Steps, _tracker.VisitedCells, _tracker.PathLength, 0);
        _view2D.Refresh();
    }

    private void OnSolverFinished()
    {
        _view2D.ForceRefresh();
        _tracker.Stop();
        _stats.UpdateStats(_tracker.Elapsed, _tracker.Steps, _tracker.VisitedCells, _tracker.PathLength, _tracker.ManagedMemoryDeltaBytes);
        GD.Print("[Main] Solver fertig.");

        _solverPath.Sort((left, right) => left.Distance.CompareTo(right.Distance));

        if (_solverPath.Count == 0 && !AreNeighbors(_solverStart, _solverGoal))
        {
            GD.Print("[Main] Kein Pfad zum Loesen vorhanden - Bot bleibt versteckt.");
            return;
        }

        List<Cell> fullPath = new(_solverPath.Count + 2) { _solverStart };
        fullPath.AddRange(_solverPath);
        fullPath.Add(_solverGoal);

        _player.StartFollowingPath(fullPath, _view3D.CellSize);
        if (_followCamEnabled)
        {
            _view3D.GetNode<CameraController3D>("Camera3D").EnableFollow(_player);
        }
    }

    private void OnSpeedChanged(float stepsPerSecond) =>
        ApplySimulationSpeed(stepsPerSecond);

    private void OnPauseToggled(bool paused) =>
        _runner.IsPaused = paused;

    private void OnStepRequested() =>
        _runner.ForceSingleStep();

    private void OnResetRequested()
    {
        OnStopManualRequested();
        _runner.StopAll();
        _solverPath.Clear();
        _player.Hide();
        _view3D.ClearTrail();
        ResetExploreMode();
        _view3D.GetNode<CameraController3D>("Camera3D").DisableFollow();

        if (_currentMaze is null)
        {
            GD.Print("[Main] Reset ignoriert: Kein Maze geladen.");
            return;
        }

        _currentMaze.ResetSolverState();
        _view2D.ForceRefresh();
        _view3D.Refresh();
        _stats.UpdateStats(TimeSpan.Zero, 0, 0, 0, 0);
        GD.Print("[Main] Solver-Zustand zurueckgesetzt.");
    }

    private void OnViewToggled(bool use3D)
    {
        if (_isManualMode && !use3D)
        {
            _hud.SetUse3DActive(true);
            return;
        }

        _view2D.Visible = !use3D;
        _view3D.Visible = use3D;

        if (use3D && _currentMaze is not null && !ReferenceEquals(_lastMazeBuiltFor3D, _currentMaze))
        {
            _view3D.SetMaze(_currentMaze);
            _lastMazeBuiltFor3D = _currentMaze;
        }

        ApplyEffectiveRunnerMode();

        GD.Print($"[Main] 3D-Ansicht = {use3D}");
    }

    private void OnHeatmapToggled(bool enabled)
    {
        _view2D.ShowDistances = enabled;
        _view2D.Refresh();
    }

    private void OnExploreModeToggled(bool enabled)
    {
        _view3D.SetExploreMode(enabled);
    }

    private void OnFollowCamToggled(bool enabled)
    {
        if (_isManualMode)
        {
            _followCamEnabled = true;
            _hud.SetFollowCamActive(true);
            _view3D.GetNode<CameraController3D>("Camera3D").EnableFollow(_player);
            return;
        }

        _followCamEnabled = enabled;

        CameraController3D camera = _view3D.GetNode<CameraController3D>("Camera3D");
        if (enabled && _player.Visible)
        {
            camera.EnableFollow(_player);
            return;
        }

        camera.DisableFollow();
    }

    private void OnUnboundedModeChanged(bool unbounded)
    {
        _userRequestedUnboundedMode = unbounded;
        _suppressViewRefresh = unbounded;
        ApplyEffectiveRunnerMode();
    }

    private void OnBotGoalReached()
    {
        if (_isManualMode)
        {
            double elapsed = Time.GetTicksMsec() / 1000.0 - _manualStartTimeSeconds;
            _hud.ShowVictory(elapsed);
            OnStopManualRequested();
            return;
        }

        GD.Print("[Main] Bot ist am Ziel angekommen.");
    }

    private void OnPlayerCellVisited(int x, int y)
    {
        _view3D.MarkTrailCell(x, y);
    }

    private void OnPlayManualToggle(bool active)
    {
        if (active)
        {
            OnPlayManualRequested();
            return;
        }

        OnStopManualRequested();
    }

    private void OnPlayManualRequested()
    {
        if (_currentMaze is null)
        {
            GD.PrintErr("[Main] Kein Maze - bitte erst Erstellen.");
            _hud.SetManualPlayActive(false);
            return;
        }

        _runner.StopAll();
        _solverPath.Clear();
        _currentMaze.ResetSolverState();
        _view3D.ClearTrail();
        _solverStart = _currentMaze.GetCell(0, 0);
        _solverGoal = _currentMaze.GetCell(_currentMaze.Width - 1, _currentMaze.Height - 1);
        _solverStart.State = CellState.Start;
        _solverGoal.State = CellState.Goal;
        _view2D.ForceRefresh();
        _view3D.SetMaze(_currentMaze);
        _lastMazeBuiltFor3D = _currentMaze;

        _hud.SetUse3DActive(true);
        OnViewToggled(true);

        CameraController3D camera = _view3D.GetNode<CameraController3D>("Camera3D");
        _player.EnableManualMode(_currentMaze, _solverStart, _solverGoal, _view3D.CellSize, camera);
        _isManualMode = true;
        _manualStartTimeSeconds = Time.GetTicksMsec() / 1000.0;
        ApplyEffectiveRunnerMode();

        _followCamEnabledBeforeManual = _followCamEnabled;
        _followCamEnabled = true;
        _hud.SetFollowCamActive(true);
        camera.EnableFollow(_player, true);

        GD.Print("[Main] Selbst spielen aktiviert.");
    }

    private void OnStopManualRequested()
    {
        if (!_isManualMode)
        {
            _hud.SetManualPlayActive(false);
            return;
        }

        _player.DisableManualMode();
        _isManualMode = false;

        CameraController3D camera = _view3D.GetNode<CameraController3D>("Camera3D");
        camera.DisableFollow();

        _followCamEnabled = _followCamEnabledBeforeManual;
        ApplyEffectiveRunnerMode();
        _hud.SetFollowCamActive(_followCamEnabled);
        _hud.SetManualPlayActive(false);
        GD.Print("[Main] Selbst spielen beendet.");
    }

    private void ApplySimulationSpeed(float stepsPerSecond)
    {
        _runner.StepsPerSecond = stepsPerSecond;
        _player.MoveSpeed = Mathf.Clamp(stepsPerSecond, 0.5f, MaxSimulationSpeed);
    }

    private void ResetExploreMode()
    {
        _hud.SetExploreModeActive(false);
        _view3D.SetExploreMode(false);
    }

    private bool ShouldRefreshStepViews() =>
        !_suppressViewRefresh && !ShouldSkipInvisibleStepVisualization();

    private bool ShouldSkipInvisibleStepVisualization() =>
        _view3D.Visible && !_isManualMode;

    private void ApplyEffectiveRunnerMode()
    {
        bool runUnbounded = _userRequestedUnboundedMode || ShouldSkipInvisibleStepVisualization();
        _runner.Mode = runUnbounded ? AlgorithmRunner.RunMode.Unbounded : AlgorithmRunner.RunMode.Throttled;
    }

    private static bool AreNeighbors(Cell a, Cell b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;
}