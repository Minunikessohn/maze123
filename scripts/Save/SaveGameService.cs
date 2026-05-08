#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Godot;
using Maze.Game;

namespace Maze.Save;

public sealed class SaveGameService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _saveDirectoryPath;
    private readonly MazeSerializer _serializer = new();

    public SaveGameService(string? saveDirectoryPath = null)
    {
        _saveDirectoryPath = string.IsNullOrWhiteSpace(saveDirectoryPath)
            ? ProjectSettings.GlobalizePath("user://saves")
            : saveDirectoryPath;

        Directory.CreateDirectory(_saveDirectoryPath);
    }

    public void SaveMaze(MazeSaveData saveData)
    {
        if (saveData is null)
        {
            throw new ArgumentNullException(nameof(saveData));
        }

        if (saveData.SaveKind != MazeSaveKind.OfflineSave)
        {
            throw new InvalidOperationException("Nur Offline-Saves duerfen lokal persistiert werden.");
        }

        saveData.Config = saveData.Config.Clone().Sanitize();
        saveData.DisplayName = string.IsNullOrWhiteSpace(saveData.DisplayName) ? "maze-save" : saveData.DisplayName.Trim();
        saveData.SaveId = string.IsNullOrWhiteSpace(saveData.SaveId)
            ? GenerateUniqueSaveId(saveData.DisplayName)
            : SanitizeSaveId(saveData.SaveId);
        saveData.SourceSessionId = string.Empty;

        if (saveData.CreatedAtUtc == default)
        {
            saveData.CreatedAtUtc = DateTime.UtcNow;
        }

        string savePath = BuildSavePath(saveData.SaveId);
        string json = JsonSerializer.Serialize(saveData, JsonOptions);
        File.WriteAllText(savePath, json, Encoding.UTF8);
    }

    public MazeSaveData? LoadMaze(string saveId)
    {
        string savePath = BuildSavePath(saveId);
        if (!File.Exists(savePath))
        {
            return null;
        }

        string json = File.ReadAllText(savePath, Encoding.UTF8);
        MazeSaveData? saveData = JsonSerializer.Deserialize<MazeSaveData>(json, JsonOptions);
        if (saveData is null)
        {
            return null;
        }

        saveData.SaveId = SanitizeSaveId(string.IsNullOrWhiteSpace(saveData.SaveId) ? saveId : saveData.SaveId);
        saveData.DisplayName = string.IsNullOrWhiteSpace(saveData.DisplayName) ? saveData.SaveId : saveData.DisplayName.Trim();
        saveData.SaveKind = MazeSaveKind.OfflineSave;
        saveData.SourceSessionId = string.Empty;
        saveData.Config = saveData.Config.Clone().Sanitize();
        return saveData;
    }

    public bool DeleteMaze(string saveId)
    {
        string savePath = BuildSavePath(saveId);
        if (!File.Exists(savePath))
        {
            return false;
        }

        File.Delete(savePath);
        return true;
    }

    public IReadOnlyList<SaveSlotSummary> ListSaves()
    {
        List<SaveSlotSummary> saves = new();

        foreach (string filePath in Directory.GetFiles(_saveDirectoryPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                MazeSaveData? saveData = JsonSerializer.Deserialize<MazeSaveData>(json, JsonOptions);
                if (saveData is null)
                {
                    continue;
                }

                string fallbackId = Path.GetFileNameWithoutExtension(filePath);
                saveData.SaveId = SanitizeSaveId(string.IsNullOrWhiteSpace(saveData.SaveId) ? fallbackId : saveData.SaveId);
                saveData.DisplayName = string.IsNullOrWhiteSpace(saveData.DisplayName) ? saveData.SaveId : saveData.DisplayName.Trim();
                saveData.SaveKind = MazeSaveKind.OfflineSave;
                saveData.SourceSessionId = string.Empty;
                saveData.Config = saveData.Config.Clone().Sanitize();
                saves.Add(_serializer.CreateSummary(saveData));
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SaveGameService] Konnte Save-Datei nicht lesen: {filePath} | {ex.Message}");
            }
        }

        saves.Sort((left, right) => right.CreatedAtUtc.CompareTo(left.CreatedAtUtc));
        return saves;
    }

    private string BuildSavePath(string saveId)
    {
        string sanitizedId = SanitizeSaveId(saveId);
        return Path.Combine(_saveDirectoryPath, $"{sanitizedId}.json");
    }

    private string GenerateUniqueSaveId(string displayName)
    {
        string baseId = SanitizeSaveId(displayName);
        string candidate = baseId;
        int suffix = 1;

        while (File.Exists(BuildSavePath(candidate)))
        {
            suffix++;
            candidate = $"{baseId}-{suffix}";
        }

        return candidate;
    }

    private static string SanitizeSaveId(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
        {
            return "maze-save";
        }

        StringBuilder builder = new();
        bool lastWasDash = false;

        foreach (char character in saveId.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                lastWasDash = false;
                continue;
            }

            if (lastWasDash)
            {
                continue;
            }

            builder.Append('-');
            lastWasDash = true;
        }

        string sanitized = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "maze-save" : sanitized;
    }
}