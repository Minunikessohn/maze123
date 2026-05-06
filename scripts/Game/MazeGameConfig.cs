using System;

namespace Maze.Game;

public sealed class MazeGameConfig
{
    public const int MinimumMazeSize = 5;
    public const float MinimumNightViewDistance = 1f;
    public const float DefaultNightViewDistance = 6f;

    public int Width { get; set; } = 25;
    public int Height { get; set; } = 25;
    public string GeneratorId { get; set; } = "recursive-backtracker";
    public bool SandboxModeEnabled { get; set; }
    public bool PathGlowEnabled { get; set; } = true;
    public bool DarkModeEnabled { get; set; }
    public bool TrapGenerationEnabled { get; set; }
    public bool MonsterCanBeStunned { get; set; }
    public bool MonsterGenerationEnabled { get; set; }
    public bool DayNightCycleEnabled { get; set; }
    public bool MonstersOnlyAtNight { get; set; } = true;
    public float NightViewDistance { get; set; } = DefaultNightViewDistance;
    public int Seed { get; set; } = Random.Shared.Next(1, int.MaxValue);

    public static MazeGameConfig CreateDefault(int width, int height, string generatorId)
    {
        return new MazeGameConfig
        {
            Width = width,
            Height = height,
            GeneratorId = generatorId
        }.Sanitize();
    }

    public MazeGameConfig Clone()
    {
        return new MazeGameConfig
        {
            Width = Width,
            Height = Height,
            GeneratorId = GeneratorId,
            SandboxModeEnabled = SandboxModeEnabled,
            PathGlowEnabled = PathGlowEnabled,
            DarkModeEnabled = DarkModeEnabled,
            TrapGenerationEnabled = TrapGenerationEnabled,
            MonsterCanBeStunned = MonsterCanBeStunned,
            MonsterGenerationEnabled = MonsterGenerationEnabled,
            DayNightCycleEnabled = DayNightCycleEnabled,
            MonstersOnlyAtNight = MonstersOnlyAtNight,
            NightViewDistance = NightViewDistance,
            Seed = Seed
        };
    }

    public MazeGameConfig Sanitize()
    {
        Width = Math.Max(MinimumMazeSize, Width);
        Height = Math.Max(MinimumMazeSize, Height);
        GeneratorId = string.IsNullOrWhiteSpace(GeneratorId) ? "recursive-backtracker" : GeneratorId;
        NightViewDistance = Math.Max(MinimumNightViewDistance, NightViewDistance);

        // Monsters are globally restricted to nighttime even when later systems add more controls.
        MonstersOnlyAtNight = true;

        return this;
    }
}