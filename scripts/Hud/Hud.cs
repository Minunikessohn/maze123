using Godot;

namespace Maze.UI;

/// <summary>
/// Bedienoberflaeche oben am Bildschirm.
/// Sendet typisierte Signale an Main, statt direkt Felder zu aendern.
/// </summary>
public partial class Hud : CanvasLayer
{
    [Signal] public delegate void GenerateRequestedEventHandler(int width, int height, string generatorId);
    [Signal] public delegate void SolveRequestedEventHandler(string solverId);
    [Signal] public delegate void SpeedChangedEventHandler(float stepsPerSecond);
    [Signal] public delegate void UnboundedModeChangedEventHandler(bool unbounded);
    [Signal] public delegate void ViewToggleRequestedEventHandler(bool use3D);
    [Signal] public delegate void HeatmapToggleEventHandler(bool enabled);
    [Signal] public delegate void FollowCamToggleEventHandler(bool enabled);
    [Signal] public delegate void ExploreModeToggleEventHandler(bool enabled);
    [Signal] public delegate void PauseToggleEventHandler(bool paused);
    [Signal] public delegate void StepRequestedEventHandler();
    [Signal] public delegate void ResetRequestedEventHandler();
    [Signal] public delegate void PlayManualToggleEventHandler(bool active);

    private HSlider _widthSlider = null!;
    private HSlider _heightSlider = null!;
    private HSlider _speedSlider = null!;
    private SpinBox _widthSpinBox = null!;
    private SpinBox _heightSpinBox = null!;
    private SpinBox _speedSpinBox = null!;
    private OptionButton _generatorChooser = null!;
    private OptionButton _solverChooser = null!;
    private Button _generateButton = null!;
    private Button _solveButton = null!;
    private Button _pauseButton = null!;
    private Button _stepButton = null!;
    private Button _resetButton = null!;
    private Button _playManualButton = null!;
    private CheckBox _viewToggle = null!;
    private CheckBox _heatmapToggle = null!;
    private CheckBox _followCamToggle = null!;
    private CheckBox _exploreModeToggle = null!;
    private CheckBox _unboundedToggle = null!;
    private Label _widthLabel = null!;
    private Label _heightLabel = null!;
    private Label _speedLabel = null!;
    private Label _victoryLabel = null!;

    public override void _Ready()
    {
        _widthSlider = GetNode<HSlider>("Root/Margin/VBox/Sizes/WidthSlider");
        _heightSlider = GetNode<HSlider>("Root/Margin/VBox/Sizes/HeightSlider");
        _speedSlider = GetNode<HSlider>("Root/Margin/VBox/SpeedRow/SpeedSlider");
        _widthSpinBox = GetNode<SpinBox>("Root/Margin/VBox/Sizes/WidthSpinBox");
        _heightSpinBox = GetNode<SpinBox>("Root/Margin/VBox/Sizes/HeightSpinBox");
        _speedSpinBox = GetNode<SpinBox>("Root/Margin/VBox/SpeedRow/SpeedSpinBox");
        _generatorChooser = GetNode<OptionButton>("Root/Margin/VBox/Algos/GeneratorChooser");
        _solverChooser = GetNode<OptionButton>("Root/Margin/VBox/Algos/SolverChooser");
        _generateButton = GetNode<Button>("Root/Margin/VBox/Buttons/GenerateButton");
        _solveButton = GetNode<Button>("Root/Margin/VBox/Buttons/SolveButton");
        _pauseButton = GetNode<Button>("Root/Margin/VBox/Buttons/PauseButton");
        _stepButton = GetNode<Button>("Root/Margin/VBox/Buttons/StepButton");
        _resetButton = GetNode<Button>("Root/Margin/VBox/Buttons/ResetButton");
        _playManualButton = GetNode<Button>("Root/Margin/VBox/Buttons/PlayManualButton");
        _viewToggle = GetNode<CheckBox>("Root/Margin/VBox/Algos/View3DToggle");
        _heatmapToggle = GetNode<CheckBox>("Root/Margin/VBox/Algos/HeatmapToggle");
        _followCamToggle = GetNode<CheckBox>("Root/Margin/VBox/Algos/FollowCamToggle");
        _exploreModeToggle = GetNode<CheckBox>("Root/Margin/VBox/Algos/ExploreModeToggle");
        _widthLabel = GetNode<Label>("Root/Margin/VBox/Sizes/WidthLabel");
        _heightLabel = GetNode<Label>("Root/Margin/VBox/Sizes/HeightLabel");
        _speedLabel = GetNode<Label>("Root/Margin/VBox/SpeedRow/SpeedLabel");
        _unboundedToggle = GetNode<CheckBox>("Root/Margin/VBox/SpeedRow/UnboundedToggle");
        _victoryLabel = GetNode<Label>("Root/Margin/VBox/VictoryLabel");

        _widthSlider.MinValue = 5;
        _widthSlider.MaxValue = 1000;
        _widthSlider.Step = 1;
        _widthSlider.Value = 25;

        _widthSpinBox.MinValue = 5;
        _widthSpinBox.MaxValue = 1000;
        _widthSpinBox.Step = 1;
        _widthSpinBox.Value = 25;

        _heightSlider.MinValue = 5;
        _heightSlider.MaxValue = 1000;
        _heightSlider.Step = 1;
        _heightSlider.Value = 25;

        _heightSpinBox.MinValue = 5;
        _heightSpinBox.MaxValue = 1000;
        _heightSpinBox.Step = 1;
        _heightSpinBox.Value = 25;

        _speedSlider.MinValue = 1;
        _speedSlider.MaxValue = 10001;
        _speedSlider.Step = 1;
        _speedSlider.Value = 30;

        _speedSpinBox.MinValue = 1;
        _speedSpinBox.MaxValue = 10001;
        _speedSpinBox.Step = 1;
        _speedSpinBox.Value = 30;

        UpdateLabels();

        _widthSlider.ValueChanged += value =>
        {
            _widthSpinBox.SetValueNoSignal(value);
            UpdateLabels();
        };
        _widthSpinBox.ValueChanged += value =>
        {
            _widthSlider.SetValueNoSignal(value);
            UpdateLabels();
        };
        _heightSlider.ValueChanged += value =>
        {
            _heightSpinBox.SetValueNoSignal(value);
            UpdateLabels();
        };
        _heightSpinBox.ValueChanged += value =>
        {
            _heightSlider.SetValueNoSignal(value);
            UpdateLabels();
        };
        _speedSlider.ValueChanged += value =>
        {
            _speedSpinBox.SetValueNoSignal(value);
            OnSpeedChanged(value);
        };
        _speedSpinBox.ValueChanged += value =>
        {
            _speedSlider.SetValueNoSignal(value);
            OnSpeedChanged(value);
        };
        _unboundedToggle.Toggled += OnUnboundedToggled;
        _generateButton.Pressed += OnGeneratePressed;
        _solveButton.Pressed += OnSolvePressed;
        _pauseButton.Toggled += OnPauseToggled;
        _stepButton.Pressed += OnStepPressed;
        _resetButton.Pressed += OnResetPressed;
        _playManualButton.Toggled += OnPlayManualToggled;
        _viewToggle.Toggled += OnViewToggled;
        _heatmapToggle.Toggled += OnHeatmapToggled;
        _followCamToggle.Toggled += OnFollowCamToggled;
        _exploreModeToggle.Toggled += OnExploreModeToggled;

        FillGeneratorChooser();
        FillSolverChooser();
    }

    private void UpdateLabels()
    {
        _widthLabel.Text = $"Breite:  {(int)_widthSlider.Value}";
        _heightLabel.Text = $"Hoehe:    {(int)_heightSlider.Value}";

        if (_unboundedToggle != null && _unboundedToggle.ButtonPressed)
        {
            _speedLabel.Text = "Tempo:  ungebremst";
        }
        else
        {
            _speedLabel.Text = $"Tempo:  {(int)_speedSlider.Value} Schritte/s";
        }
    }

    private void OnSpeedChanged(double value)
    {
        UpdateLabels();
        EmitSignal(SignalName.SpeedChanged, (float)value);
    }

    private void OnUnboundedToggled(bool pressed)
    {
        _speedSlider.Editable = !pressed;
        _speedSpinBox.Editable = !pressed;
        UpdateLabels();
        EmitSignal(SignalName.UnboundedModeChanged, pressed);
    }

    private void OnGeneratePressed()
    {
        int width = (int)_widthSlider.Value;
        int height = (int)_heightSlider.Value;
        string id = (string)_generatorChooser.GetItemMetadata(_generatorChooser.Selected);
        EmitSignal(SignalName.GenerateRequested, width, height, id);
    }

    private void OnSolvePressed()
    {
        string id = (string)_solverChooser.GetItemMetadata(_solverChooser.Selected);
        EmitSignal(SignalName.SolveRequested, id);
    }

    private void OnPauseToggled(bool pressed)
    {
        _pauseButton.Text = pressed ? "Fortsetzen" : "Pause";
        EmitSignal(SignalName.PauseToggle, pressed);
    }

    private void OnStepPressed() => EmitSignal(SignalName.StepRequested);

    private void OnResetPressed() => EmitSignal(SignalName.ResetRequested);

    private void OnPlayManualToggled(bool active)
    {
        _victoryLabel.Text = string.Empty;
        EmitSignal(SignalName.PlayManualToggle, active);
    }

    private void OnViewToggled(bool pressed) => EmitSignal(SignalName.ViewToggleRequested, pressed);

    private void OnHeatmapToggled(bool enabled) => EmitSignal(SignalName.HeatmapToggle, enabled);

    private void OnFollowCamToggled(bool enabled) => EmitSignal(SignalName.FollowCamToggle, enabled);

    private void OnExploreModeToggled(bool enabled) => EmitSignal(SignalName.ExploreModeToggle, enabled);

    private void FillGeneratorChooser()
    {
        _generatorChooser.Clear();
        AddGenerator("Recursive Backtracker", "recursive-backtracker");
        AddGenerator("Growing Tree (75% newest, 25% random)", "growing-tree");
        AddGenerator("Recursive Division", "recursive-division");
        AddGenerator("Cellular Automata (4-5)", "cellular-automata");
        _generatorChooser.Selected = 0;
    }

    private void FillSolverChooser()
    {
        _solverChooser.Clear();
        AddSolver("Breadth-First Search (BFS)", "bfs");
        AddSolver("Depth-First Search (DFS)", "dfs");
        AddSolver("A*", "a-star");
        AddSolver("Greedy Best-First", "greedy");
        AddSolver("Wall Follower (links)", "wall-follower");
        AddSolver("Dead-End Filling", "dead-end-filling");
        _solverChooser.Selected = 0;
    }

    private void AddGenerator(string label, string id)
    {
        int index = _generatorChooser.ItemCount;
        _generatorChooser.AddItem(label);
        _generatorChooser.SetItemMetadata(index, id);
    }

    private void AddSolver(string label, string id)
    {
        int index = _solverChooser.ItemCount;
        _solverChooser.AddItem(label);
        _solverChooser.SetItemMetadata(index, id);
    }

    public void ShowVictory(double seconds)
    {
        _victoryLabel.Text = $"Geschafft in {seconds:0.00} s!";
        _playManualButton.SetPressedNoSignal(false);
    }

    public void SetManualPlayActive(bool active) =>
        _playManualButton.SetPressedNoSignal(active);

    public void SetFollowCamActive(bool active) =>
        _followCamToggle.SetPressedNoSignal(active);

    public void SetExploreModeActive(bool active) =>
        _exploreModeToggle.SetPressedNoSignal(active);

    public void SetUse3DActive(bool active) =>
        _viewToggle.SetPressedNoSignal(active);
}