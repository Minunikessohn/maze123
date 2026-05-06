#nullable enable

using System;
using Godot;

namespace Maze.World;

public partial class DayNightController : Node
{
    public const float DefaultCycleDurationSeconds = 90f;
    public const float NightStartsAt = 0.5f;

    [Export(PropertyHint.Range, "15,600,1,or_greater")]
    public float CycleDurationSeconds { get; set; } = DefaultCycleDurationSeconds;

    public float TimeOfDay { get; private set; }
    public bool IsNight { get; private set; }

    private bool _cycleEnabled;
    private bool _isPaused = true;

    public event Action? DayStarted;
    public event Action? NightStarted;

    public override void _Process(double delta)
    {
        if (!_cycleEnabled || _isPaused || CycleDurationSeconds <= 0.001f)
        {
            return;
        }

        SetTimeOfDay(TimeOfDay + (float)delta / CycleDurationSeconds);
    }

    public void Configure(bool cycleEnabled, float initialTimeOfDay = 0f)
    {
        _cycleEnabled = cycleEnabled;
        SetTimeOfDay(cycleEnabled ? initialTimeOfDay : 0f, emitSignals: false);
    }

    public void SetPaused(bool paused) => _isPaused = paused;

    public void Reset(float initialTimeOfDay = 0f) =>
        SetTimeOfDay(initialTimeOfDay, emitSignals: false);

    private void SetTimeOfDay(float timeOfDay, bool emitSignals = true)
    {
        bool wasNight = IsNight;
        TimeOfDay = Mathf.PosMod(timeOfDay, 1f);
        IsNight = TimeOfDay >= NightStartsAt;

        if (!emitSignals || wasNight == IsNight)
        {
            return;
        }

        if (IsNight)
        {
            NightStarted?.Invoke();
            return;
        }

        DayStarted?.Invoke();
    }
}