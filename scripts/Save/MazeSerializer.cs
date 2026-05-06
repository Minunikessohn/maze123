#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Maze.Game;
using Maze.Model;

namespace Maze.Save;

public sealed class MazeSerializer
{
    public MazeSaveData CreateSaveData(
        string displayName,
        MazeGameConfig config,
        global::Maze.Model.Maze maze,
        Cell? startCell = null,
        Cell? goalCell = null,
        IEnumerable<Vector2I>? trapCells = null,
        IEnumerable<Vector2I>? monsterSpawnCells = null)
    {
        Cell resolvedStart = startCell ?? maze.GetCell(0, 0);
        Cell resolvedGoal = goalCell ?? maze.GetCell(maze.Width - 1, maze.Height - 1);

        MazeSaveData saveData = new()
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "maze-save" : displayName.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            Config = config.Clone().Sanitize(),
            StartCell = new MazePointSaveData(resolvedStart.X, resolvedStart.Y),
            GoalCell = new MazePointSaveData(resolvedGoal.X, resolvedGoal.Y)
        };

        foreach (Cell cell in maze.AllCells())
        {
            saveData.Cells.Add(new MazeCellSaveData
            {
                X = cell.X,
                Y = cell.Y,
                NorthWall = cell.HasWall(Direction.North),
                EastWall = cell.HasWall(Direction.East),
                SouthWall = cell.HasWall(Direction.South),
                WestWall = cell.HasWall(Direction.West)
            });
        }

        if (trapCells is not null)
        {
            foreach (Vector2I cell in trapCells)
            {
                saveData.Traps.Add(new TrapSaveData
                {
                    Cell = MazePointSaveData.FromVector2I(cell)
                });
            }
        }

        if (monsterSpawnCells is not null)
        {
            foreach (Vector2I cell in monsterSpawnCells)
            {
                saveData.MonsterSpawnCells.Add(MazePointSaveData.FromVector2I(cell));
            }
        }

        return saveData;
    }

    public global::Maze.Model.Maze DeserializeMaze(MazeSaveData saveData)
    {
        MazeGameConfig config = saveData.Config.Clone().Sanitize();
        global::Maze.Model.Maze maze = new(config.Width, config.Height);

        if (saveData.Cells.Count != config.Width * config.Height)
        {
            throw new InvalidOperationException(
                $"Save enthaelt {saveData.Cells.Count} Zellen, erwartet werden {config.Width * config.Height}.");
        }

        foreach (MazeCellSaveData cellData in saveData.Cells)
        {
            if (!maze.IsInside(cellData.X, cellData.Y))
            {
                throw new InvalidOperationException($"Ungueltige Zellkoordinate im Save: ({cellData.X},{cellData.Y}).");
            }

            Cell cell = maze.GetCell(cellData.X, cellData.Y);
            cell.SetWall(Direction.North, cellData.NorthWall);
            cell.SetWall(Direction.East, cellData.EastWall);
            cell.SetWall(Direction.South, cellData.SouthWall);
            cell.SetWall(Direction.West, cellData.WestWall);
            cell.State = CellState.Open;
            cell.Distance = -1;
        }

        return maze;
    }

    public SaveSlotSummary CreateSummary(MazeSaveData saveData)
    {
        MazeGameConfig config = saveData.Config.Clone().Sanitize();

        return new SaveSlotSummary
        {
            SaveId = saveData.SaveId,
            DisplayName = saveData.DisplayName,
            CreatedAtUtc = saveData.CreatedAtUtc,
            Width = config.Width,
            Height = config.Height,
            GeneratorId = config.GeneratorId
        };
    }
}