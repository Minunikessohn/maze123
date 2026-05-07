#nullable enable

using System.Collections.Generic;
using Godot;
using Maze.Model;

namespace Maze.Views;

/// <summary>
/// 2D-Visualisierung des Labyrinths. Zeichnet Zellfarben und Waende auf Basis des Modells.
/// </summary>
public partial class MazeView2D : Node2D
{
    [Export] public int CellSizePx = 24;
    [Export] public int WallThicknessPx = 2;
    [Export] public bool ShowDistances = false;
    [Export] public bool FitToViewport = false;
    [Export] public bool RevealVisitedOnly = false;
    [Export] public bool ShowPlayerMarker = false;
    [Export] public int ViewportPaddingPx = 28;

    public static readonly Dictionary<CellState, Color> StateColors = new()
    {
        { CellState.Untouched, new Color("#1e1e1e") },
        { CellState.Carving, new Color("#ffaa00") },
        { CellState.Open, new Color("#2c2c2c") },
        { CellState.Frontier, new Color("#8ab4f8") },
        { CellState.Visited, new Color("#3d5a80") },
        { CellState.Path, new Color("#f6c177") },
        { CellState.Start, new Color("#a3be8c") },
        { CellState.Goal, new Color("#bf616a") },
        { CellState.Filled, new Color("#000000") }
    };

    private static readonly Color WallColor = new("#dcdcdc");
    private static readonly Color HeatmapMin = new("#003366");
    private static readonly Color HeatmapMax = new("#ff6f3c");
    private static readonly Color HiddenCellColor = new("#080a0d");
    private static readonly Color OverlayBackdropColor = new(0.04f, 0.05f, 0.08f, 0.9f);
    private static readonly Color PlayerMarkerColor = new("#ffef7a");

    private const int ThrottleThreshold = 250;
    private const double ThrottledRefreshHz = 30.0;

    private readonly HashSet<Vector2I> _visitedCells = new();
    private CameraController2D? _camera;
    private global::Maze.Model.Maze? _maze;
    private Vector2I? _playerCell;
    private bool _refreshDirty;
    private double _refreshAccumulator;

    public override void _Ready()
    {
        _camera = GetNodeOrNull<CameraController2D>("Camera2D");
        SetCameraEnabled(!FitToViewport);
    }

    public void SetMaze(global::Maze.Model.Maze maze)
    {
        _maze = maze;
        _visitedCells.Clear();
        _playerCell = null;
        _refreshDirty = false;
        _refreshAccumulator = 0;
        QueueRedraw();
        if (!FitToViewport)
        {
            _camera?.FitToMaze(maze);
        }
    }

    public void SetCameraEnabled(bool enabled)
    {
        if (_camera is null)
        {
            return;
        }

        _camera.Enabled = enabled && !FitToViewport;
        if (_camera.Enabled && _maze is not null)
        {
            _camera.FitToMaze(_maze);
        }
    }

    public void ClearVisited()
    {
        _visitedCells.Clear();
        _playerCell = null;
        Refresh();
    }

    public void MarkVisited(Vector2I cell)
    {
        if (_maze is null || !_maze.IsInside(cell.X, cell.Y) || !_visitedCells.Add(cell))
        {
            return;
        }

        Refresh();
    }

    public void SetPlayerCell(Vector2I? cell)
    {
        _playerCell = cell;
        Refresh();
    }

    public void Refresh()
    {
        if (_maze is null)
        {
            return;
        }

        if (_maze.Width <= ThrottleThreshold && _maze.Height <= ThrottleThreshold)
        {
            QueueRedraw();
            return;
        }

        _refreshDirty = true;
    }

    public void ForceRefresh()
    {
        _refreshDirty = false;
        _refreshAccumulator = 0;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!_refreshDirty)
        {
            return;
        }

        _refreshAccumulator += delta;
        if (_refreshAccumulator >= 1.0 / ThrottledRefreshHz)
        {
            _refreshAccumulator = 0;
            _refreshDirty = false;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (_maze is null)
        {
            return;
        }

        int maxDistance = ComputeMaxDistance(_maze);
        Vector2 cellSize = ResolveCellSize(_maze);
        Vector2 origin = ResolveOrigin(_maze, cellSize);

        if (FitToViewport && RevealVisitedOnly)
        {
            Vector2 mazePixelSize = new(_maze.Width * cellSize.X, _maze.Height * cellSize.Y);
            Rect2 backdropRect = new(origin - new Vector2(18f, 18f), mazePixelSize + new Vector2(36f, 36f));
            DrawRect(backdropRect, OverlayBackdropColor, true);
        }

        foreach (Cell cell in _maze.AllCells())
        {
            bool isDiscovered = IsDiscovered(cell.X, cell.Y);
            Rect2 rect = new(
                origin.X + cell.X * cellSize.X,
                origin.Y + cell.Y * cellSize.Y,
                cellSize.X,
                cellSize.Y);

            Color fill = ShowDistances && cell.Distance >= 0 && isDiscovered
                ? Heatmap(cell.Distance, maxDistance)
                : isDiscovered
                    ? StateColors[cell.State]
                    : HiddenCellColor;

            DrawRect(rect, fill, true);
        }

        float wallThickness = ResolveWallThickness(cellSize);

        foreach (Cell cell in _maze.AllCells())
        {
            if (RevealVisitedOnly && !IsDiscovered(cell.X, cell.Y))
            {
                continue;
            }

            float x0 = origin.X + cell.X * cellSize.X;
            float y0 = origin.Y + cell.Y * cellSize.Y;
            float x1 = x0 + cellSize.X;
            float y1 = y0 + cellSize.Y;

            if (cell.HasWall(Direction.North))
            {
                DrawLine(new Vector2(x0, y0), new Vector2(x1, y0), WallColor, wallThickness);
            }

            if (cell.HasWall(Direction.West))
            {
                DrawLine(new Vector2(x0, y0), new Vector2(x0, y1), WallColor, wallThickness);
            }

            if (cell.Y == _maze.Height - 1 && cell.HasWall(Direction.South))
            {
                DrawLine(new Vector2(x0, y1), new Vector2(x1, y1), WallColor, wallThickness);
            }

            if (cell.X == _maze.Width - 1 && cell.HasWall(Direction.East))
            {
                DrawLine(new Vector2(x1, y0), new Vector2(x1, y1), WallColor, wallThickness);
            }
        }

        if (ShowPlayerMarker && _playerCell is Vector2I playerCell && _maze.IsInside(playerCell.X, playerCell.Y))
        {
            Vector2 center = new(
                origin.X + (playerCell.X + 0.5f) * cellSize.X,
                origin.Y + (playerCell.Y + 0.5f) * cellSize.Y);
            DrawCircle(center, Mathf.Max(3f, Mathf.Min(cellSize.X, cellSize.Y) * 0.22f), PlayerMarkerColor);
        }
    }

    private bool IsDiscovered(int x, int y)
    {
        if (!RevealVisitedOnly)
        {
            return true;
        }

        Vector2I cell = new(x, y);
        return _visitedCells.Contains(cell) || _playerCell == cell;
    }

    private Vector2 ResolveCellSize(global::Maze.Model.Maze maze)
    {
        if (!FitToViewport)
        {
            return new Vector2(CellSizePx, CellSizePx);
        }

        Vector2 viewport = GetViewportRect().Size;
        float availableWidth = Mathf.Max(1f, viewport.X - ViewportPaddingPx * 2f);
        float availableHeight = Mathf.Max(1f, viewport.Y - ViewportPaddingPx * 2f);
        float size = Mathf.Max(4f, Mathf.Min(availableWidth / maze.Width, availableHeight / maze.Height));
        return new Vector2(size, size);
    }

    private Vector2 ResolveOrigin(global::Maze.Model.Maze maze, Vector2 cellSize)
    {
        if (!FitToViewport)
        {
            return Vector2.Zero;
        }

        Vector2 viewport = GetViewportRect().Size;
        Vector2 mazePixelSize = new(maze.Width * cellSize.X, maze.Height * cellSize.Y);
        return (viewport - mazePixelSize) * 0.5f;
    }

    private float ResolveWallThickness(Vector2 cellSize)
    {
        if (!FitToViewport)
        {
            return WallThicknessPx;
        }

        return Mathf.Max(1f, Mathf.Min(cellSize.X, cellSize.Y) * 0.08f);
    }

    private static int ComputeMaxDistance(global::Maze.Model.Maze maze)
    {
        int max = 0;
        foreach (Cell cell in maze.AllCells())
        {
            if (cell.Distance > max)
            {
                max = cell.Distance;
            }
        }

        return max;
    }

    private static Color Heatmap(int distance, int maxDistance)
    {
        if (maxDistance <= 0)
        {
            return HeatmapMin;
        }

        float t = (float)distance / maxDistance;
        return HeatmapMin.Lerp(HeatmapMax, t);
    }
}