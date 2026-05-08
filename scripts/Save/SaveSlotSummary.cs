#nullable enable

using System;
using Maze.Game;

namespace Maze.Save;

public sealed class SaveSlotSummary
{
    public string SaveId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public MazeSaveKind SaveKind { get; init; } = MazeSaveKind.OfflineSave;
    public int Width { get; init; }
    public int Height { get; init; }
    public string GeneratorId { get; init; } = string.Empty;

    public string ToDisplayLabel()
    {
        string label = string.IsNullOrWhiteSpace(DisplayName) ? SaveId : DisplayName;
        string created = CreatedAtUtc == default
            ? "unbekannt"
            : CreatedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
        string kindLabel = SaveKind == MazeSaveKind.HostSessionSnapshot ? "session" : "offline";

        return $"{label} | {kindLabel} | {Width}x{Height} | {GeneratorId} | {created}";
    }
}