using Godot;
using Maze.Model;

namespace Maze;

public static class MazeWorldGrid
{
    public static Vector3 CellToWorldCenter(Cell cell, float cellSize, float y = 0f) =>
        CellToWorldCenter(new Vector2I(cell.X, cell.Y), cellSize, y);

    public static Vector3 CellToWorldCenter(Vector2I cell, float cellSize, float y = 0f)
    {
        float normalizedCellSize = NormalizeCellSize(cellSize);
        return new Vector3(
            cell.X * normalizedCellSize + normalizedCellSize * 0.5f,
            y,
            cell.Y * normalizedCellSize + normalizedCellSize * 0.5f);
    }

    public static Vector2I WorldToCell(Vector3 worldPosition, float cellSize, int mazeWidth = 0, int mazeHeight = 0)
    {
        float normalizedCellSize = NormalizeCellSize(cellSize);
        Vector2I cell = new(
            Mathf.FloorToInt(worldPosition.X / normalizedCellSize),
            Mathf.FloorToInt(worldPosition.Z / normalizedCellSize));

        if (mazeWidth > 0)
        {
            cell.X = Mathf.Clamp(cell.X, 0, mazeWidth - 1);
        }

        if (mazeHeight > 0)
        {
            cell.Y = Mathf.Clamp(cell.Y, 0, mazeHeight - 1);
        }

        return cell;
    }

    public static Aabb GetCellBounds(Vector2I cell, float cellSize, float minY = 0f, float height = 0f)
    {
        float normalizedCellSize = NormalizeCellSize(cellSize);
        return new Aabb(
            new Vector3(cell.X * normalizedCellSize, minY, cell.Y * normalizedCellSize),
            new Vector3(normalizedCellSize, height, normalizedCellSize));
    }

    private static float NormalizeCellSize(float cellSize) => Mathf.Max(0.001f, cellSize);
}