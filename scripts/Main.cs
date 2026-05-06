#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Maze.Game;
using Maze.Game.Settings;
using Maze.Generators;
using Maze.Model;
using Maze.Save;
using Maze.Solvers;
using Maze.UI;
using Maze.Views;

namespace Maze;

public partial class Main : Node
{
    private const float DefaultStepsPerSecond = 30f;
    private const float MaxSimulationSpeed = 100001f;

    private MainMenu _mainMenu = null!;
    private PauseMenu _pauseMenu = null!;
    private Hud _hud = null!;
    private StatsPanel _stats = null!;
    private MazeView2D _view2D = null!;
    private MazeView3D _view3D = null!;
    private CameraController3D _camera3D = null!;
    private PlayerCharacter3D _player = null!;
    private AlgorithmRunner _runner = null!;
    private SaveGameService _saveGameService = null!;
    private global::Maze.Model.Maze? _currentMaze;
    private global::Maze.Model.Maze? _lastMazeBuiltFor3D;
    private Cell _solverStart = null!;
    private Cell _solverGoal = null!;
    private readonly List<Cell> _solverPath = new();
    private readonly MazeSerializer _mazeSerializer = new();

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

    private Random _random = new();
    private readonly PerformanceTracker _tracker = new();
    private MazeGameConfig? _currentGameConfig;
    private readonly GameSessionState _sessionState = new();
    private GameFlowState _flowState = GameFlowState.Boot;
    private bool _suppressViewRefresh;
    private bool _userRequestedUnboundedMode;
    private bool _followCamEnabled;
    private bool _followCamEnabledBeforeManual;
    private bool _isManualMode;
    private double _manualStartTimeSeconds;
    private string _pendingSaveDisplayName = string.Empty;

    public override void _Ready()
    {
        _mainMenu = GetNode<MainMenu>("MainMenu");
        _pauseMenu = GetNode<PauseMenu>("PauseMenu");
        _hud = GetNode<Hud>("Hud");
        _stats = GetNode<StatsPanel>("Hud/StatsPanel");
        _view2D = GetNode<MazeView2D>("MazeView2D");
        _view3D = GetNode<MazeView3D>("MazeView3D");
        _camera3D = _view3D.GetNode<CameraController3D>("Camera3D");
        _player = GetNode<PlayerCharacter3D>("MazeView3D/Player");
        _runner = GetNode<AlgorithmRunner>("Runner");
        _saveGameService = new SaveGameService();

        _mainMenu.StartNewMazeRequested += OnStartNewMazeRequested;
        _mainMenu.LoadMazeRequested += OnLoadMazeRequested;
        _mainMenu.DeleteMazeRequested += OnDeleteMazeRequested;
        _mainMenu.SetGeneratorOptions(BuildGeneratorMenuItems());
        _pauseMenu.VisualSettingsChanged += OnVisualSettingsChanged;
        _pauseMenu.AudioSettingsChanged += OnAudioSettingsChanged;
        _pauseMenu.ReturnToMainMenuRequested += OnReturnToMainMenuRequested;
        _pauseMenu.SetVisualSettings(_sessionState.VisualSettings);
        _pauseMenu.SetAudioSettings(_sessionState.AudioSettings);
        RefreshSaveSlots();

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
        ApplyVisualSettings(_sessionState.VisualSettings);
        ApplyAudioSettings(_sessionState.AudioSettings);
        TransitionToState(GameFlowState.MainMenu);

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
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }
            && _flowState is GameFlowState.Playing or GameFlowState.Paused)
        {
            TogglePauseMenu();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_flowState == GameFlowState.Paused
            && @event is InputEventKey { Pressed: true, Keycode: Key.Space }
            && _runner.IsPaused)
        {
            _runner.ForceSingleStep();
        }
    }

    public override void _ExitTree() => GD.Print("[Main] _ExitTree.");

    private void OnGenerateRequested(int width, int height, string generatorId)
    {
        _pendingSaveDisplayName = string.Empty;

        if (StartNewGame(MazeGameConfig.CreateDefault(width, height, generatorId)))
        {
            TransitionToState(GameFlowState.Loading);
        }
    }

    private bool StartNewGame(MazeGameConfig config)
    {
        StopManualMode(force: true);

        MazeGameConfig sanitizedConfig = config.Clone().Sanitize();

        if (!_generators.TryGetValue(sanitizedConfig.GeneratorId, out IMazeGenerator? generator))
        {
            GD.PrintErr($"Unbekannter Generator: {sanitizedConfig.GeneratorId}");
            return false;
        }

        _currentGameConfig = sanitizedConfig;
        _random = new Random(sanitizedConfig.Seed);

        _tracker.Start();
        _runner.StopAll();
        _solverPath.Clear();
        _player.Hide();
        _view3D.ClearTrail();
        ResetExploreMode();
        _camera3D.DisableFollow();
        _currentMaze = new global::Maze.Model.Maze(sanitizedConfig.Width, sanitizedConfig.Height);
        _sessionState.ResetForNewGame(sanitizedConfig, _currentMaze);
        _lastMazeBuiltFor3D = null;
        _view2D.SetMaze(_currentMaze);
        _view3D.ClearMaze();

        _runner.StartGeneration(generator.Generate(_currentMaze, _random));
        GD.Print($"[Main] Generator {generator.Name} gestartet.");
        return true;
    }

    private void OnStartNewMazeRequested(string saveName, MazeGameConfig config)
    {
        _pendingSaveDisplayName = saveName;

        if (!StartNewGame(config))
        {
            _pendingSaveDisplayName = string.Empty;
            return;
        }

        TransitionToState(GameFlowState.Loading);
    }

    private void OnLoadMazeRequested(string saveId)
    {
        TransitionToState(GameFlowState.Loading);

        MazeSaveData? saveData = _saveGameService.LoadMaze(saveId);
        if (saveData is null)
        {
            GD.PrintErr($"[Main] Save konnte nicht geladen werden: {saveId}");
            TransitionToState(GameFlowState.MainMenu);
            RefreshSaveSlots();
            return;
        }

        if (!TryLoadMaze(saveData))
        {
            TransitionToState(GameFlowState.MainMenu);
            return;
        }

        TransitionToState(GameFlowState.Playing);
    }

    private void OnDeleteMazeRequested(string saveId)
    {
        if (!_saveGameService.DeleteMaze(saveId))
        {
            GD.PrintErr($"[Main] Save konnte nicht geloescht werden: {saveId}");
            return;
        }

        RefreshSaveSlots();
        GD.Print($"[Main] Save geloescht: {saveId}");
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
        _sessionState.IsRunning = true;
        _sessionState.GoalReached = false;
        _sessionState.StartCell = _currentMaze.GetCell(0, 0);
        _sessionState.GoalCell = _currentMaze.GetCell(_currentMaze.Width - 1, _currentMaze.Height - 1);
        _tracker.Stop();
        _stats.UpdateStats(_tracker.Elapsed, _tracker.Steps, _tracker.VisitedCells, 0, _tracker.ManagedMemoryDeltaBytes);
        TrySaveCurrentMaze();

        TransitionToState(GameFlowState.Playing);

        if (!IsSandboxMode())
        {
            OnPlayManualRequested();
        }

        GD.Print("[Main] Generator fertig.");
    }

    private void OnSolveRequested(string solverId)
    {
        if (!IsSandboxMode())
        {
            GD.Print("[Main] Solver im normalen Modus deaktiviert.");
            return;
        }

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
        _camera3D.DisableFollow();
        _solverStart = ResolveStartCell(_currentMaze);
        _solverGoal = ResolveGoalCell(_currentMaze);
        _sessionState.StartCell = _solverStart;
        _sessionState.GoalCell = _solverGoal;
        _sessionState.GoalReached = false;
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
            _camera3D.EnableFollow(_player);
        }
    }

    private void OnSpeedChanged(float stepsPerSecond) =>
        ApplySimulationSpeed(stepsPerSecond);

    private void OnPauseToggled(bool paused)
    {
        if (!IsGameplayState())
        {
            _hud.SetPauseActive(false);
            return;
        }

        TransitionToState(paused ? GameFlowState.Paused : GameFlowState.Playing);
    }

    private void OnVisualSettingsChanged(VisualSettings settings)
    {
        _sessionState.VisualSettings.Brightness = settings.Brightness;
        _sessionState.VisualSettings.FieldOfView = settings.FieldOfView;
        _sessionState.VisualSettings.EffectsIntensity = settings.EffectsIntensity;
        ApplyVisualSettings(_sessionState.VisualSettings);
    }

    private void OnAudioSettingsChanged(AudioSettings settings)
    {
        _sessionState.AudioSettings.MonsterVolume = settings.MonsterVolume;
        _sessionState.AudioSettings.FootstepVolume = settings.FootstepVolume;
        _sessionState.AudioSettings.GoalVolume = settings.GoalVolume;
        _sessionState.AudioSettings.MasterVolume = settings.MasterVolume;
        ApplyAudioSettings(_sessionState.AudioSettings);
    }

    private void OnReturnToMainMenuRequested()
    {
        StopManualMode(force: true);
        _runner.StopAll();
        _player.Hide();
        _view3D.ClearTrail();
        ResetExploreMode();
        _camera3D.DisableFollow();
        RefreshSaveSlots();
        TransitionToState(GameFlowState.MainMenu);
    }

    private void OnStepRequested() =>
        _runner.ForceSingleStep();

    private void OnResetRequested()
    {
        if (!IsSandboxMode())
        {
            GD.Print("[Main] Reset ueber HUD ist im normalen Modus deaktiviert.");
            return;
        }

        OnStopManualRequested();
        _runner.StopAll();
        _solverPath.Clear();
        _player.Hide();
        _view3D.ClearTrail();
        ResetExploreMode();
        _camera3D.DisableFollow();

        if (_currentMaze is null)
        {
            GD.Print("[Main] Reset ignoriert: Kein Maze geladen.");
            return;
        }

        _currentMaze.ResetSolverState();
        _sessionState.GoalReached = false;
        TransitionToState(GameFlowState.Playing);
        _view2D.ForceRefresh();
        _view3D.Refresh();
        _stats.UpdateStats(TimeSpan.Zero, 0, 0, 0, 0);
        GD.Print("[Main] Solver-Zustand zurueckgesetzt.");
    }

    private void OnViewToggled(bool use3D)
    {
        if (!IsSandboxMode())
        {
            use3D = true;
        }

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
        if (!IsSandboxMode())
        {
            return;
        }

        _view2D.ShowDistances = enabled;
        _view2D.Refresh();
    }

    private void OnExploreModeToggled(bool enabled)
    {
        if (!IsSandboxMode())
        {
            return;
        }

        _view3D.SetExploreMode(enabled);
    }

    private void OnFollowCamToggled(bool enabled)
    {
        if (_isManualMode)
        {
            _followCamEnabled = true;
            _hud.SetFollowCamActive(true);
            _camera3D.EnableFollow(_player);
            return;
        }

        _followCamEnabled = enabled;

        if (enabled && _player.Visible)
        {
            _camera3D.EnableFollow(_player);
            return;
        }

        _camera3D.DisableFollow();
    }

    private void OnUnboundedModeChanged(bool unbounded)
    {
        if (!IsSandboxMode())
        {
            _userRequestedUnboundedMode = false;
            _suppressViewRefresh = false;
            ApplyEffectiveRunnerMode();
            return;
        }

        _userRequestedUnboundedMode = unbounded;
        _suppressViewRefresh = unbounded;
        ApplyEffectiveRunnerMode();
    }

    private void OnBotGoalReached()
    {
        _sessionState.GoalReached = true;

        if (_isManualMode)
        {
            double elapsed = Time.GetTicksMsec() / 1000.0 - _manualStartTimeSeconds;

            if (IsSandboxMode())
            {
                _hud.ShowVictory(elapsed);
                OnStopManualRequested();
            }
            else
            {
                GD.Print($"[Main] Ziel im normalen Modus erreicht nach {elapsed:0.00} s.");
            }

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
        if (!IsSandboxMode())
        {
            if (!_isManualMode)
            {
                OnPlayManualRequested();
            }

            return;
        }

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
        _solverStart = ResolveStartCell(_currentMaze);
        _solverGoal = ResolveGoalCell(_currentMaze);
        _sessionState.StartCell = _solverStart;
        _sessionState.GoalCell = _solverGoal;
        _sessionState.GoalReached = false;
        _solverStart.State = CellState.Start;
        _solverGoal.State = CellState.Goal;
        _view2D.ForceRefresh();
        _view3D.SetMaze(_currentMaze);
        _lastMazeBuiltFor3D = _currentMaze;

        _hud.SetUse3DActive(true);
        OnViewToggled(true);

        _player.EnableManualMode(_currentMaze, _solverStart, _solverGoal, _view3D.CellSize, _camera3D);
        _isManualMode = true;
        _sessionState.IsManualMode = true;
        _manualStartTimeSeconds = Time.GetTicksMsec() / 1000.0;
        ApplyEffectiveRunnerMode();

        _followCamEnabledBeforeManual = _followCamEnabled;
        _followCamEnabled = true;
        _hud.SetFollowCamActive(true);
        _camera3D.EnableFollow(_player, true);

        GD.Print("[Main] Selbst spielen aktiviert.");
    }

    private void OnStopManualRequested()
    {
        StopManualMode(force: false);
    }

    private void StopManualMode(bool force)
    {
        if (!force && !IsSandboxMode() && _isManualMode)
        {
            return;
        }

        if (!_isManualMode)
        {
            _hud.SetManualPlayActive(false);
            return;
        }

        _player.DisableManualMode();
        _isManualMode = false;
        _sessionState.IsManualMode = false;

        _camera3D.DisableFollow();

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

    private void ApplyVisualSettings(VisualSettings settings)
    {
        _view3D.ApplyBrightness(settings.Brightness);
        _view3D.ApplyEffectsIntensity(settings.EffectsIntensity);
        _camera3D.SetFieldOfView(settings.FieldOfView);
        _pauseMenu.SetVisualSettings(settings);
    }

    private void ApplyAudioSettings(AudioSettings settings)
    {
        _pauseMenu.SetAudioSettings(settings);
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

    private void TogglePauseMenu()
    {
        if (_flowState == GameFlowState.Playing)
        {
            TransitionToState(GameFlowState.Paused);
            return;
        }

        if (_flowState == GameFlowState.Paused)
        {
            TransitionToState(GameFlowState.Playing);
        }
    }

    private static bool AreNeighbors(Cell a, Cell b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;

    private void TrySaveCurrentMaze()
    {
        if (_currentMaze is null || _currentGameConfig is null || string.IsNullOrWhiteSpace(_pendingSaveDisplayName))
        {
            return;
        }

        try
        {
            MazeSaveData saveData = _mazeSerializer.CreateSaveData(
                _pendingSaveDisplayName,
                _currentGameConfig,
                _currentMaze,
                ResolveStartCell(_currentMaze),
                ResolveGoalCell(_currentMaze),
                _sessionState.ActiveTrapCells,
                _sessionState.ActiveMonsterCells);

            _saveGameService.SaveMaze(saveData);
            RefreshSaveSlots();
            GD.Print($"[Main] Save erstellt: {saveData.SaveId}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Main] Save konnte nicht erstellt werden: {ex.Message}");
        }
        finally
        {
            _pendingSaveDisplayName = string.Empty;
        }
    }

    private bool TryLoadMaze(MazeSaveData saveData)
    {
        try
        {
            StopManualMode(force: true);
            _runner.StopAll();
            _solverPath.Clear();
            _player.Hide();
            _view3D.ClearTrail();
            ResetExploreMode();
            _camera3D.DisableFollow();

            _currentGameConfig = saveData.Config.Clone().Sanitize();
            _currentMaze = _mazeSerializer.DeserializeMaze(saveData);
            _lastMazeBuiltFor3D = null;
            _random = new Random(_currentGameConfig.Seed);

            _sessionState.ResetForNewGame(_currentGameConfig, _currentMaze);
            _sessionState.ActiveTrapCells.AddRange(ConvertTrapCells(saveData));
            _sessionState.ActiveMonsterCells.AddRange(ConvertMonsterCells(saveData));
            _sessionState.StartCell = ResolveSavePoint(_currentMaze, saveData.StartCell, 0, 0);
            _sessionState.GoalCell = ResolveSavePoint(_currentMaze, saveData.GoalCell, _currentMaze.Width - 1, _currentMaze.Height - 1);
            _sessionState.IsRunning = true;

            _view2D.SetMaze(_currentMaze);
            _view2D.ForceRefresh();
            _view3D.SetMaze(_currentMaze);
            _lastMazeBuiltFor3D = _currentMaze;

            if (!IsSandboxMode())
            {
                OnPlayManualRequested();
            }

            GD.Print($"[Main] Save geladen: {saveData.SaveId}");
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Main] Save konnte nicht geladen werden: {ex.Message}");
            return false;
        }
    }

    private void RefreshSaveSlots() =>
        _mainMenu.SetSaveSlots(_saveGameService.ListSaves());

    private Cell ResolveStartCell(global::Maze.Model.Maze maze) =>
        ResolveSessionPoint(maze, _sessionState.StartCell, 0, 0);

    private Cell ResolveGoalCell(global::Maze.Model.Maze maze) =>
        ResolveSessionPoint(maze, _sessionState.GoalCell, maze.Width - 1, maze.Height - 1);

    private static Cell ResolveSessionPoint(global::Maze.Model.Maze maze, Cell? sessionCell, int fallbackX, int fallbackY)
    {
        if (sessionCell is not null && maze.IsInside(sessionCell.X, sessionCell.Y))
        {
            return maze.GetCell(sessionCell.X, sessionCell.Y);
        }

        return maze.GetCell(fallbackX, fallbackY);
    }

    private static Cell ResolveSavePoint(global::Maze.Model.Maze maze, MazePointSaveData point, int fallbackX, int fallbackY)
    {
        if (maze.IsInside(point.X, point.Y))
        {
            return maze.GetCell(point.X, point.Y);
        }

        return maze.GetCell(fallbackX, fallbackY);
    }

    private static List<Vector2I> ConvertTrapCells(MazeSaveData saveData)
    {
        List<Vector2I> cells = new(saveData.Traps.Count);

        foreach (TrapSaveData trap in saveData.Traps)
        {
            cells.Add(trap.Cell.ToVector2I());
        }

        return cells;
    }

    private static List<Vector2I> ConvertMonsterCells(MazeSaveData saveData)
    {
        List<Vector2I> cells = new(saveData.MonsterSpawnCells.Count);

        foreach (MazePointSaveData point in saveData.MonsterSpawnCells)
        {
            cells.Add(point.ToVector2I());
        }

        return cells;
    }

    private List<KeyValuePair<string, string>> BuildGeneratorMenuItems()
    {
        List<KeyValuePair<string, string>> items = new(_generators.Count);

        foreach (KeyValuePair<string, IMazeGenerator> generator in _generators)
        {
            items.Add(new KeyValuePair<string, string>(generator.Key, generator.Value.Name));
        }

        return items;
    }

    private void TransitionToState(GameFlowState newState)
    {
        if (_flowState == newState)
        {
            ApplyStatePresentation();
            return;
        }

        _flowState = newState;
        _sessionState.FlowState = newState;
        _sessionState.IsPaused = newState == GameFlowState.Paused;

        if (newState is GameFlowState.Boot or GameFlowState.MainMenu)
        {
            _sessionState.IsRunning = false;
        }

        ApplyStatePresentation();
        GD.Print($"[Main] Zustand gewechselt zu {newState}.");
    }

    private void ApplyStatePresentation()
    {
        bool showMainMenu = _flowState == GameFlowState.MainMenu;
        bool showGameplay = _flowState is GameFlowState.Loading or GameFlowState.Playing or GameFlowState.Paused;
        bool showPauseMenu = _flowState == GameFlowState.Paused;
        bool sandboxMode = IsSandboxMode();

        _mainMenu.Visible = showMainMenu;
        _pauseMenu.Visible = showPauseMenu;
        _hud.Visible = showGameplay && sandboxMode;
        _hud.SetPauseActive(_flowState == GameFlowState.Paused);

        if (!sandboxMode)
        {
            _hud.SetUse3DActive(true);
        }

        _view2D.Visible = showGameplay && sandboxMode;
        _view3D.Visible = showGameplay && !sandboxMode;

        bool gameplayInputEnabled = _flowState == GameFlowState.Playing;
        _player.SetProcess(gameplayInputEnabled);
        _camera3D.SetProcess(gameplayInputEnabled && _view3D.Visible);
        _camera3D.SetProcessUnhandledInput(gameplayInputEnabled && _view3D.Visible);
        _runner.IsPaused = _flowState is GameFlowState.Boot or GameFlowState.MainMenu or GameFlowState.Paused;

        if (!gameplayInputEnabled)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        ApplyEffectiveRunnerMode();
    }

    private bool IsGameplayState() =>
        _flowState is GameFlowState.Loading or GameFlowState.Playing or GameFlowState.Paused;

    private bool IsSandboxMode() =>
        _currentGameConfig?.SandboxModeEnabled ?? true;

    private bool IsNormalMode() =>
        !IsSandboxMode();
}