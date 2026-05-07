#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Maze.Audio;
using Maze.Game;
using Maze.Gameplay.Traps;
using Maze.Gameplay.Monster;
using Maze.Game.Settings;
using Maze.Generators;
using Maze.Model;
using Maze.Save;
using Maze.Solvers;
using Maze.UI;
using Maze.Views;
using Maze.World;
using static Maze.Model.DirectionHelper;

namespace Maze;

public partial class Main : Node
{
    private const float DefaultStepsPerSecond = 30f;
    private const float MaxSimulationSpeed = 100001f;
    private const float MonsterStunCollisionRadiusFactor = 0.45f;
    private const double TrapSpawnRate = 0.005d;
    private const int MinimumTrapStartDistance = 6;
    private const int MinimumTrapSpacing = 3;
    private const int MinimumTrapGoalDistance = 2;
    private const int PreferredTrapOpenNeighborCount = 3;
    private const int TrapSeedSalt = unchecked((int)0x5F3759DF);

    private MainMenu _mainMenu = null!;
    private PauseMenu _pauseMenu = null!;
    private Hud _hud = null!;
    private StatsPanel _stats = null!;
    private MazeView2D _view2D = null!;
    private MazeView3D _view3D = null!;
    private CameraController2D _camera2D = null!;
    private CameraController3D _camera3D = null!;
    private PlayerCharacter3D _player = null!;
    private HorrorAudioController _audioController = null!;
    private AlgorithmRunner _runner = null!;
    private DayNightController _dayNightController = null!;
    private MonsterManager _monsterManager = null!;
    private TrapManager _trapManager = null!;
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
    private bool _firstPersonEnabled;
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
        _camera2D = _view2D.GetNode<CameraController2D>("Camera2D");
        _camera3D = _view3D.GetNode<CameraController3D>("Camera3D");
        _player = GetNode<PlayerCharacter3D>("MazeView3D/Player");
        _audioController = GetNode<HorrorAudioController>("MazeView3D/HorrorAudioController");
        _runner = GetNode<AlgorithmRunner>("Runner");
        _dayNightController = GetNode<DayNightController>("DayNightController");
        _monsterManager = GetNode<MonsterManager>("MazeView3D/MonsterManager");
        _trapManager = GetNode<TrapManager>("MazeView3D/TrapManager");
        _saveGameService = new SaveGameService();

        _mainMenu.StartNewMazeRequested += OnStartNewMazeRequested;
        _mainMenu.LoadMazeRequested += OnLoadMazeRequested;
        _mainMenu.DeleteMazeRequested += OnDeleteMazeRequested;
        _mainMenu.SetGeneratorOptions(BuildGeneratorMenuItems());
        _pauseMenu.VisualSettingsChanged += OnVisualSettingsChanged;
        _pauseMenu.AudioSettingsChanged += OnAudioSettingsChanged;
        _pauseMenu.ReturnToMainMenuRequested += OnReturnToMainMenuRequested;
        _dayNightController.DayStarted += OnDayStarted;
        _dayNightController.NightStarted += OnNightStarted;
        _monsterManager.BindDayNightController(_dayNightController);
        _monsterManager.BindTrapManager(_trapManager);
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
        _hud.FirstPersonToggle += OnFirstPersonToggled;
        _hud.ExploreModeToggle += OnExploreModeToggled;
        _hud.UnboundedModeChanged += OnUnboundedModeChanged;
        _player.GoalReached += OnBotGoalReached;
        _player.CellVisited += OnPlayerCellVisited;
        _player.StaminaChanged += OnPlayerStaminaChanged;
        _audioController.BindPlayer(_player);

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
        if (_currentGameConfig is null)
        {
            return;
        }

        SyncDayNightState();
        SyncTrapState();
        UpdateMonsterStunCollision();
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
        InitializePerspectiveStateForGame();
        _random = new Random(sanitizedConfig.Seed);

        _tracker.Start();
        _runner.StopAll();
        _solverPath.Clear();
        _player.Hide();
        _view3D.ClearTrail();
        _view3D.ClearProximityEffects();
        _monsterManager.UpdatePlayerCell(null);
        ResetExploreMode();
        ClearPlayerCameraModes();
        _currentMaze = new global::Maze.Model.Maze(sanitizedConfig.Width, sanitizedConfig.Height);
        _sessionState.ResetForNewGame(sanitizedConfig, _currentMaze);
        ConfigureDayNightCycle(sanitizedConfig);
        _lastMazeBuiltFor3D = null;
        _view2D.SetMaze(_currentMaze);
        _view3D.ClearMaze();
        ConfigureTrapSystem();
        ConfigureMonsterSystem();
        SyncDayNightState();

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
        EnsureMonsterSpawnCells();
        EnsureTrapDefinitions();
        ConfigureTrapSystem();
        ConfigureMonsterSystem();
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
        _monsterManager.UpdatePlayerCell(null);
        ResetExploreMode();
        ClearPlayerCameraModes();
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
        ApplyPlayerCameraMode();
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

    private void OnDayStarted()
    {
        GD.Print("[Main] Tag gestartet.");
        SyncDayNightState();
    }

    private void OnNightStarted()
    {
        GD.Print("[Main] Nacht gestartet.");
        SyncDayNightState();
    }

    private void OnReturnToMainMenuRequested()
    {
        StopManualMode(force: true);
        _runner.StopAll();
        _player.Hide();
        _view3D.ClearTrail();
        _view3D.ClearProximityEffects();
        ClearTrapRuntimeState(clearDefinitions: true);
        _monsterManager.UpdatePlayerCell(null);
        ResetExploreMode();
        ClearPlayerCameraModes();
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
        _view3D.ClearProximityEffects();
        _monsterManager.UpdatePlayerCell(null);
        ResetExploreMode();
        ClearPlayerCameraModes();

        if (_currentMaze is null)
        {
            GD.Print("[Main] Reset ignoriert: Kein Maze geladen.");
            return;
        }

        _currentMaze.ResetSolverState();
        _sessionState.GoalReached = false;
        RebuildTrapRuntimeState();
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

        ApplyPlayerCameraMode();
        RefreshAudioGameplayState();

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

    private void OnFirstPersonToggled(bool enabled)
    {
        if (!IsSandboxMode())
        {
            _hud.SetFirstPersonActive(true);
            ApplyPlayerCameraMode(true);
            return;
        }

        _firstPersonEnabled = enabled;

        if (enabled && !_view3D.Visible)
        {
            _hud.SetUse3DActive(true);
            OnViewToggled(true);
            return;
        }

        ApplyPlayerCameraMode(true);
    }

    private void OnFollowCamToggled(bool enabled)
    {
        if (_isManualMode)
        {
            _followCamEnabled = true;
            _hud.SetFollowCamActive(true);
            ApplyPlayerCameraMode();
            return;
        }

        _followCamEnabled = enabled;
        ApplyPlayerCameraMode();
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
        Vector2I playerCell = new(x, y);
        _audioController.UpdatePlayerCell(playerCell);
        _trapManager.NotifyPlayerEnteredCell(playerCell);
        _monsterManager.UpdatePlayerCell(playerCell);
        _view3D.MarkTrailCell(x, y);
        _view3D.UpdateMonsterProximity(playerCell);
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
        _hud.SetStaminaVisible(true);
        ApplyPlayerCameraMode(true);
        RefreshAudioGameplayState();

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
        _audioController.UpdatePlayerCell(null);
        _view3D.ClearProximityEffects();
        _monsterManager.UpdatePlayerCell(null);

        ClearPlayerCameraModes();

        _followCamEnabled = _followCamEnabledBeforeManual;
        RefreshAudioGameplayState();
        ApplyEffectiveRunnerMode();
        _hud.SetFollowCamActive(_followCamEnabled);
        _hud.SetStaminaVisible(false);
        _hud.SetManualPlayActive(false);
        GD.Print("[Main] Selbst spielen beendet.");
    }

    private void ApplySimulationSpeed(float stepsPerSecond)
    {
        _runner.StepsPerSecond = stepsPerSecond;
        _player.PathMoveSpeed = Mathf.Clamp(stepsPerSecond, 0.5f, MaxSimulationSpeed);
    }

    private void OnPlayerStaminaChanged(float current, float maximum, bool sprinting)
    {
        _hud.SetStamina(current, maximum, sprinting);
        _audioController.SetPlayerStamina(current, maximum, sprinting);
    }

    private void ApplyVisualSettings(VisualSettings settings)
    {
        _view3D.ApplyBrightness(settings.Brightness);
        _view3D.ApplyEffectsIntensity(settings.EffectsIntensity);
        _hud.SetEffectsIntensity(settings.EffectsIntensity);
        _camera3D.SetFieldOfView(settings.FieldOfView);
        _pauseMenu.SetVisualSettings(settings);
        SyncDayNightState();
    }

    private void ApplyAudioSettings(AudioSettings settings)
    {
        _pauseMenu.SetAudioSettings(settings);
        _audioController.SetAudioSettings(settings);
    }

    private void ResetExploreMode()
    {
        _hud.SetExploreModeActive(false);
        _view3D.SetExploreMode(false);
    }

    private void InitializePerspectiveStateForGame()
    {
        _firstPersonEnabled = false;
        _hud.SetFirstPersonActive(ShouldUseFirstPersonCamera());
    }

    private void ClearPlayerCameraModes()
    {
        _player.SetFirstPersonActive(false);
        _camera3D.DisableFirstPerson();
        _camera3D.DisableFollow();
    }

    private void ApplyPlayerCameraMode(bool snapImmediately = false)
    {
        bool canTrackPlayer = _flowState == GameFlowState.Playing && _view3D.Visible && _player.Visible;
        bool firstPersonActive = canTrackPlayer && ShouldUseFirstPersonCamera();

        _player.SetFirstPersonActive(firstPersonActive);

        if (firstPersonActive)
        {
            _camera3D.EnableFirstPerson(_player, snapImmediately);
            return;
        }

        _camera3D.DisableFirstPerson();

        if (canTrackPlayer && (_isManualMode || _followCamEnabled))
        {
            _camera3D.EnableFollow(_player, snapImmediately);
            return;
        }

        _camera3D.DisableFollow();
    }

    private bool ShouldUseFirstPersonCamera() =>
        !IsSandboxMode() || _firstPersonEnabled;

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
                GetTrapDefinitionsForSave(),
                _sessionState.MonsterSpawnCells);

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
            ClearPlayerCameraModes();

            _currentGameConfig = saveData.Config.Clone().Sanitize();
            InitializePerspectiveStateForGame();
            _currentMaze = _mazeSerializer.DeserializeMaze(saveData);
            _lastMazeBuiltFor3D = null;
            _random = new Random(_currentGameConfig.Seed);

            _sessionState.ResetForNewGame(_currentGameConfig, _currentMaze);
            _sessionState.TrapDefinitions.AddRange(ConvertTrapDefinitions(saveData));
            _sessionState.ActiveTrapCells.AddRange(GetArmedTrapCells(_sessionState.TrapDefinitions));
            _sessionState.MonsterSpawnCells.AddRange(ConvertMonsterCells(saveData));
            _sessionState.StartCell = ResolveSavePoint(_currentMaze, saveData.StartCell, 0, 0);
            _sessionState.GoalCell = ResolveSavePoint(_currentMaze, saveData.GoalCell, _currentMaze.Width - 1, _currentMaze.Height - 1);
            _sessionState.IsRunning = true;
            EnsureMonsterSpawnCells();
            ConfigureDayNightCycle(_currentGameConfig);

            _view2D.SetMaze(_currentMaze);
            _view2D.ForceRefresh();
            _view3D.ClearProximityEffects();
            _view3D.SetMaze(_currentMaze);
            _lastMazeBuiltFor3D = _currentMaze;
            ConfigureTrapSystem();
            ConfigureMonsterSystem();
            SyncDayNightState();

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

    private List<TrapDefinition> GetTrapDefinitionsForSave()
    {
        if (_sessionState.TrapDefinitions.Count > 0)
        {
            HashSet<Vector2I> armedCells = new(_sessionState.ActiveTrapCells);
            List<TrapDefinition> definitions = new(_sessionState.TrapDefinitions.Count);

            foreach (TrapDefinition trap in _sessionState.TrapDefinitions)
            {
                definitions.Add(new TrapDefinition
                {
                    TrapId = trap.TrapId,
                    Cell = trap.Cell,
                    IsArmed = armedCells.Contains(trap.Cell)
                });
            }

            return definitions;
        }

        List<TrapDefinition> fallbackDefinitions = new(_sessionState.ActiveTrapCells.Count);

        foreach (Vector2I cell in _sessionState.ActiveTrapCells)
        {
            fallbackDefinitions.Add(new TrapDefinition
            {
                Cell = cell,
                IsArmed = true
            });
        }

        return fallbackDefinitions;
    }

    private static List<TrapDefinition> ConvertTrapDefinitions(MazeSaveData saveData)
    {
        List<TrapDefinition> definitions = new(saveData.Traps.Count);

        foreach (TrapSaveData trap in saveData.Traps)
        {
            definitions.Add(new TrapDefinition
            {
                TrapId = string.IsNullOrWhiteSpace(trap.TrapId) ? TrapDefinition.DefaultTrapId : trap.TrapId.Trim(),
                Cell = trap.Cell.ToVector2I(),
                IsArmed = trap.IsArmed
            });
        }

        return definitions;
    }

    private static List<Vector2I> GetArmedTrapCells(IEnumerable<TrapDefinition> trapDefinitions)
    {
        List<Vector2I> cells = new();

        foreach (TrapDefinition trap in trapDefinitions)
        {
            if (trap.IsArmed)
            {
                cells.Add(trap.Cell);
            }
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
        _hud.Visible = showGameplay;
        _hud.SetSandboxControlsVisible(sandboxMode);
        _hud.SetPauseActive(_flowState == GameFlowState.Paused);
        _hud.SetStaminaVisible(showGameplay && _isManualMode);

        if (!sandboxMode)
        {
            _hud.SetUse3DActive(true);
        }

        _hud.SetFirstPersonActive(ShouldUseFirstPersonCamera());

        bool sandboxUse3D = sandboxMode && (_view3D.Visible || ShouldUseFirstPersonCamera());
        _view2D.Visible = showGameplay && sandboxMode && !sandboxUse3D;
        _view3D.Visible = showGameplay && (!sandboxMode || sandboxUse3D);

        bool gameplayInputEnabled = _flowState == GameFlowState.Playing;
        bool view2DInputEnabled = gameplayInputEnabled && _view2D.Visible;
        _camera2D.Enabled = view2DInputEnabled;
        _camera2D.SetProcess(view2DInputEnabled);
        _camera2D.SetProcessUnhandledInput(view2DInputEnabled);

        if (view2DInputEnabled)
        {
            _camera2D.MakeCurrent();
        }

        _player.SetProcess(gameplayInputEnabled);
        _camera3D.SetProcess(gameplayInputEnabled && _view3D.Visible);
        _camera3D.SetProcessUnhandledInput(gameplayInputEnabled && _view3D.Visible);
        _runner.IsPaused = _flowState is GameFlowState.Boot or GameFlowState.MainMenu or GameFlowState.Paused;
        _dayNightController.SetPaused(_flowState != GameFlowState.Playing);
        RefreshAudioGameplayState();

        if (!gameplayInputEnabled)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        ApplyPlayerCameraMode();
        ApplyEffectiveRunnerMode();
        SyncDayNightState();
    }

    private void RefreshAudioGameplayState()
    {
        bool showGameplay = _flowState is GameFlowState.Loading or GameFlowState.Playing or GameFlowState.Paused;
        bool gameplayInputEnabled = _flowState == GameFlowState.Playing;
        _audioController.SetGameplayState(showGameplay && _view3D.Visible, gameplayInputEnabled && _isManualMode && _view3D.Visible);
    }

    private void ConfigureDayNightCycle(MazeGameConfig config)
    {
        _dayNightController.Configure(config.DayNightCycleEnabled, _sessionState.DayNightProgress);
        _dayNightController.SetPaused(_flowState != GameFlowState.Playing);
    }

    private void ConfigureTrapSystem()
    {
        _trapManager.Configure(_currentGameConfig, _currentMaze, _sessionState.TrapDefinitions, _view3D.CellSize);
        SyncTrapState();
    }

    private void ClearTrapRuntimeState(bool clearDefinitions)
    {
        _trapManager.Clear();
        _sessionState.ActiveTrapCells.Clear();

        if (clearDefinitions)
        {
            _sessionState.TrapDefinitions.Clear();
        }
    }

    private void RebuildTrapRuntimeState()
    {
        ClearTrapRuntimeState(clearDefinitions: false);
        ConfigureTrapSystem();
    }

    private void SyncTrapState()
    {
        HashSet<Vector2I> activeTrapCells = new(_trapManager.ActiveTrapCells);

        _sessionState.ActiveTrapCells.Clear();
        _sessionState.ActiveTrapCells.AddRange(activeTrapCells);

        foreach (TrapDefinition trap in _sessionState.TrapDefinitions)
        {
            trap.IsArmed = activeTrapCells.Contains(trap.Cell);
        }
    }

    private void ConfigureMonsterSystem() =>
        _monsterManager.Configure(_currentGameConfig, _currentMaze, _sessionState.MonsterSpawnCells, _view3D.CellSize);

    private void SyncDayNightState()
    {
        if (_currentGameConfig is null)
        {
            _monsterManager.Synchronize(MonsterSimulationMode.Inactive);
            _sessionState.ActiveMonsterCells.Clear();
            _view3D.SetMonsterCells(Array.Empty<Vector2I>());
            _audioController.SetMonsterCells(Array.Empty<Vector2I>());
            _view3D.ApplyDayNightState(false, false, MazeGameConfig.DefaultNightViewDistance, 0f, false);
            return;
        }

        _sessionState.DayNightProgress = _dayNightController.TimeOfDay;
        _monsterManager.Synchronize(GetMonsterSimulationMode());
        _sessionState.ActiveMonsterCells.Clear();
        _sessionState.ActiveMonsterCells.AddRange(_monsterManager.ActiveMonsterCells);
        _view3D.SetMonsterCells(_sessionState.ActiveMonsterCells);
        _audioController.SetMonsterCells(_sessionState.ActiveMonsterCells);
        _view3D.ApplyDayNightState(
            _currentGameConfig.DayNightCycleEnabled,
            _currentGameConfig.DarkModeEnabled,
            _currentGameConfig.NightViewDistance,
            _sessionState.DayNightProgress,
            _dayNightController.IsNight);
    }

    private void UpdateMonsterStunCollision()
    {
        if (_currentGameConfig is null
            || !_currentGameConfig.MonsterCanBeStunned
            || _flowState != GameFlowState.Playing
            || !_player.Visible)
        {
            _monsterManager.UpdateStunCollision(Vector3.Zero, 0f);
            return;
        }

        float collisionRadius = _view3D.CellSize * MonsterStunCollisionRadiusFactor;
        _monsterManager.UpdateStunCollision(_player.GlobalPosition, collisionRadius);
    }

    private MonsterSimulationMode GetMonsterSimulationMode()
    {
        if (!ShouldMonstersBePresent())
        {
            return MonsterSimulationMode.Inactive;
        }

        return _flowState == GameFlowState.Paused
            ? MonsterSimulationMode.Frozen
            : MonsterSimulationMode.Active;
    }

    private bool ShouldMonstersBePresent()
    {
        if (_currentGameConfig is null || !_currentGameConfig.MonsterGenerationEnabled)
        {
            return false;
        }

        if (_flowState is not GameFlowState.Playing and not GameFlowState.Paused)
        {
            return false;
        }

        if (!_currentGameConfig.MonstersOnlyAtNight)
        {
            return true;
        }

        return _currentGameConfig.DayNightCycleEnabled && _dayNightController.IsNight;
    }

    private void EnsureMonsterSpawnCells()
    {
        if (_currentMaze is null || _currentGameConfig is null || !_currentGameConfig.MonsterGenerationEnabled)
        {
            return;
        }

        if (_sessionState.MonsterSpawnCells.Count > 0)
        {
            return;
        }

        Cell startCell = ResolveStartCell(_currentMaze);
        Cell goalCell = ResolveGoalCell(_currentMaze);
        foreach (Vector2I spawnCell in ComputeMonsterSpawnCells(_currentMaze, startCell, goalCell))
        {
            _sessionState.MonsterSpawnCells.Add(spawnCell);
        }
    }

    private void EnsureTrapDefinitions()
    {
        _sessionState.TrapDefinitions.Clear();
        _sessionState.ActiveTrapCells.Clear();

        if (_currentMaze is null || _currentGameConfig is null || !_currentGameConfig.TrapGenerationEnabled)
        {
            return;
        }

        Cell startCell = ResolveStartCell(_currentMaze);
        Cell goalCell = ResolveGoalCell(_currentMaze);
        List<TrapDefinition> trapDefinitions = ComputeTrapDefinitions(
            _currentMaze,
            _currentGameConfig,
            startCell,
            goalCell,
            _sessionState.MonsterSpawnCells);

        _sessionState.TrapDefinitions.AddRange(trapDefinitions);
        _sessionState.ActiveTrapCells.AddRange(GetArmedTrapCells(trapDefinitions));
    }

    private static List<Vector2I> ComputeMonsterSpawnCells(global::Maze.Model.Maze maze, Cell startCell, Cell goalCell)
    {
        Dictionary<Vector2I, int> distances = ComputeDistancesFromStart(maze, startCell);
        List<(Vector2I Position, int Distance)> candidates = new();

        foreach (Cell cell in maze.AllCells())
        {
            if (!IsValidMonsterSpawnCell(maze, cell, startCell, goalCell))
            {
                continue;
            }

            Vector2I position = new(cell.X, cell.Y);
            if (!distances.TryGetValue(position, out int distance))
            {
                continue;
            }

            candidates.Add((position, distance));
        }

        if (candidates.Count == 0)
        {
            return new List<Vector2I>();
        }

        int totalMazeCells = maze.Width * maze.Height;
        int spawnCount = Math.Max(1, (int)Math.Round(totalMazeCells * 0.01d, MidpointRounding.AwayFromZero));
        spawnCount = Math.Min(spawnCount, candidates.Count);
        int minimumStartDistance = Math.Max(2, (int)Math.Round((maze.Width + maze.Height) * 0.2d, MidpointRounding.AwayFromZero));
        List<(Vector2I Position, int Distance)> preferredCandidates = candidates.FindAll(candidate => candidate.Distance >= minimumStartDistance);
        List<(Vector2I Position, int Distance)> source = preferredCandidates.Count >= spawnCount ? preferredCandidates : candidates;

        source.Sort(static (left, right) =>
        {
            int distanceComparison = right.Distance.CompareTo(left.Distance);
            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            int yComparison = right.Position.Y.CompareTo(left.Position.Y);
            return yComparison != 0 ? yComparison : right.Position.X.CompareTo(left.Position.X);
        });

        if (source.Count <= spawnCount)
        {
            List<Vector2I> allCells = new(source.Count);
            foreach ((Vector2I position, _) in source)
            {
                allCells.Add(position);
            }

            return allCells;
        }

        List<Vector2I> selectedSpawnCells = new(spawnCount);
        double stride = source.Count / (double)spawnCount;

        for (int index = 0; index < spawnCount; index++)
        {
            int candidateIndex = Math.Min(source.Count - 1, (int)Math.Floor(index * stride));
            selectedSpawnCells.Add(source[candidateIndex].Position);
        }

        return selectedSpawnCells;
    }

    private static List<TrapDefinition> ComputeTrapDefinitions(
        global::Maze.Model.Maze maze,
        MazeGameConfig config,
        Cell startCell,
        Cell goalCell,
        IReadOnlyCollection<Vector2I> monsterSpawnCells)
    {
        Dictionary<Vector2I, int> distancesFromStart = ComputeDistancesFromStart(maze, startCell);
        Dictionary<Vector2I, int> distancesFromGoal = ComputeDistancesFromStart(maze, goalCell);
        HashSet<Vector2I> forbiddenCells = BuildForbiddenTrapCells(maze, startCell, goalCell, monsterSpawnCells);
        List<(Vector2I Position, int DistanceFromStart, int DistanceFromGoal, int OpenNeighborCount)> candidates = new();

        foreach (Cell cell in maze.AllCells())
        {
            Vector2I position = new(cell.X, cell.Y);

            if (forbiddenCells.Contains(position) || !HasOpenNeighbor(maze, cell))
            {
                continue;
            }

            if (!distancesFromStart.TryGetValue(position, out int distanceFromStart)
                || !distancesFromGoal.TryGetValue(position, out int distanceFromGoal))
            {
                continue;
            }

            candidates.Add((position, distanceFromStart, distanceFromGoal, CountReachableNeighbors(maze, cell)));
        }

        if (candidates.Count == 0)
        {
            return new List<TrapDefinition>();
        }

        int trapCount = Math.Max(1, (int)Math.Round(maze.Width * maze.Height * TrapSpawnRate, MidpointRounding.AwayFromZero));
        trapCount = Math.Min(trapCount, candidates.Count);

        if (trapCount == 0)
        {
            return new List<TrapDefinition>();
        }

        Random trapRandom = new(unchecked(config.Seed ^ TrapSeedSalt));
        List<(Vector2I Position, int DistanceFromStart, int DistanceFromGoal, int OpenNeighborCount)> preferredCandidates =
            candidates.FindAll(candidate =>
                candidate.DistanceFromStart >= MinimumTrapStartDistance
                && candidate.DistanceFromGoal >= MinimumTrapGoalDistance
                && candidate.OpenNeighborCount >= PreferredTrapOpenNeighborCount);

        if (preferredCandidates.Count < trapCount)
        {
            preferredCandidates = candidates.FindAll(candidate =>
                candidate.DistanceFromStart >= MinimumTrapStartDistance
                && candidate.DistanceFromGoal >= MinimumTrapGoalDistance
                && candidate.OpenNeighborCount >= 2);
        }

        if (preferredCandidates.Count < trapCount)
        {
            preferredCandidates = candidates.FindAll(candidate =>
                candidate.DistanceFromStart >= MinimumTrapStartDistance
                && candidate.DistanceFromGoal >= MinimumTrapGoalDistance);
        }

        List<Vector2I> selectedCells = SelectTrapCells(preferredCandidates, candidates, trapCount, trapRandom);
        List<TrapDefinition> trapDefinitions = new(selectedCells.Count);

        foreach (Vector2I cell in selectedCells)
        {
            trapDefinitions.Add(new TrapDefinition
            {
                Cell = cell,
                IsArmed = true
            });
        }

        return trapDefinitions;
    }

    private static Dictionary<Vector2I, int> ComputeDistancesFromStart(global::Maze.Model.Maze maze, Cell startCell)
    {
        Dictionary<Vector2I, int> distances = new();
        Queue<Cell> frontier = new();
        Vector2I start = new(startCell.X, startCell.Y);
        distances[start] = 0;
        frontier.Enqueue(startCell);

        while (frontier.Count > 0)
        {
            Cell current = frontier.Dequeue();
            Vector2I currentPosition = new(current.X, current.Y);
            int nextDistance = distances[currentPosition] + 1;

            foreach (Cell neighbor in GetReachableNeighbors(maze, current))
            {
                Vector2I neighborPosition = new(neighbor.X, neighbor.Y);
                if (distances.ContainsKey(neighborPosition))
                {
                    continue;
                }

                distances[neighborPosition] = nextDistance;
                frontier.Enqueue(neighbor);
            }
        }

        return distances;
    }

    private static HashSet<Vector2I> BuildForbiddenTrapCells(
        global::Maze.Model.Maze maze,
        Cell startCell,
        Cell goalCell,
        IEnumerable<Vector2I> monsterSpawnCells)
    {
        HashSet<Vector2I> forbiddenCells = new();

        AddCellAndNeighbors(maze, startCell, forbiddenCells);
        AddCellAndNeighbors(maze, goalCell, forbiddenCells);

        foreach (Vector2I spawnCell in monsterSpawnCells)
        {
            forbiddenCells.Add(spawnCell);
        }

        return forbiddenCells;
    }

    private static void AddCellAndNeighbors(global::Maze.Model.Maze maze, Cell origin, ISet<Vector2I> target)
    {
        target.Add(new Vector2I(origin.X, origin.Y));

        foreach (Direction direction in All)
        {
            Cell? neighbor = maze.GetNeighbor(origin, direction);
            if (neighbor is not null)
            {
                target.Add(new Vector2I(neighbor.X, neighbor.Y));
            }
        }
    }

    private static List<Vector2I> SelectTrapCells(
        List<(Vector2I Position, int DistanceFromStart, int DistanceFromGoal, int OpenNeighborCount)> preferredCandidates,
        List<(Vector2I Position, int DistanceFromStart, int DistanceFromGoal, int OpenNeighborCount)> allCandidates,
        int trapCount,
        Random random)
    {
        List<Vector2I> selectedCells = new(trapCount);
        HashSet<Vector2I> selectedSet = new();

        TrySelectTrapCells(preferredCandidates, trapCount, random, selectedCells, selectedSet, enforceSpacing: true);
        TrySelectTrapCells(allCandidates, trapCount, random, selectedCells, selectedSet, enforceSpacing: true);
        TrySelectTrapCells(allCandidates, trapCount, random, selectedCells, selectedSet, enforceSpacing: false);

        return selectedCells;
    }

    private static void TrySelectTrapCells(
        List<(Vector2I Position, int DistanceFromStart, int DistanceFromGoal, int OpenNeighborCount)> sourceCandidates,
        int trapCount,
        Random random,
        List<Vector2I> selectedCells,
        HashSet<Vector2I> selectedSet,
        bool enforceSpacing)
    {
        if (selectedCells.Count >= trapCount || sourceCandidates.Count == 0)
        {
            return;
        }

        List<(Vector2I Position, int DistanceFromStart, int DistanceFromGoal, int OpenNeighborCount)> shuffledCandidates = new(sourceCandidates);
        ShuffleInPlace(shuffledCandidates, random);

        foreach ((Vector2I position, _, _, _) in shuffledCandidates)
        {
            if (selectedCells.Count >= trapCount || selectedSet.Contains(position))
            {
                continue;
            }

            if (enforceSpacing && !IsTrapSpacingValid(position, selectedCells))
            {
                continue;
            }

            selectedCells.Add(position);
            selectedSet.Add(position);
        }
    }

    private static bool IsTrapSpacingValid(Vector2I candidate, IEnumerable<Vector2I> selectedCells)
    {
        foreach (Vector2I selectedCell in selectedCells)
        {
            int distance = Math.Abs(candidate.X - selectedCell.X) + Math.Abs(candidate.Y - selectedCell.Y);
            if (distance < MinimumTrapSpacing)
            {
                return false;
            }
        }

        return true;
    }

    private static void ShuffleInPlace<T>(IList<T> items, Random random)
    {
        for (int index = items.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }
    }

    private static bool IsValidMonsterSpawnCell(global::Maze.Model.Maze maze, Cell candidate, Cell startCell, Cell goalCell)
    {
        bool isStart = candidate.X == startCell.X && candidate.Y == startCell.Y;
        bool isGoal = candidate.X == goalCell.X && candidate.Y == goalCell.Y;
        return !isStart && !isGoal && HasOpenNeighbor(maze, candidate);
    }

    private static bool HasOpenNeighbor(global::Maze.Model.Maze maze, Cell cell)
    {
        return CountReachableNeighbors(maze, cell) > 0;
    }

    private static int CountReachableNeighbors(global::Maze.Model.Maze maze, Cell cell)
    {
        int reachableNeighborCount = 0;

        foreach (Direction direction in All)
        {
            if (cell.HasWall(direction))
            {
                continue;
            }

            Cell? neighbor = maze.GetNeighbor(cell, direction);
            if (neighbor is not null)
            {
                reachableNeighborCount++;
            }
        }

        return reachableNeighborCount;
    }

    private static IEnumerable<Cell> GetReachableNeighbors(global::Maze.Model.Maze maze, Cell cell)
    {
        foreach (Direction direction in All)
        {
            if (cell.HasWall(direction))
            {
                continue;
            }

            Cell? neighbor = maze.GetNeighbor(cell, direction);
            if (neighbor is not null)
            {
                yield return neighbor;
            }
        }
    }

    private bool IsGameplayState() =>
        _flowState is GameFlowState.Loading or GameFlowState.Playing or GameFlowState.Paused;

    private bool IsSandboxMode() =>
        _currentGameConfig?.SandboxModeEnabled ?? true;

    private bool IsNormalMode() =>
        !IsSandboxMode();
}