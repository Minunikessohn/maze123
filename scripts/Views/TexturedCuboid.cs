using Godot;
using GodotArray = Godot.Collections.Array;

namespace Maze.Views;

/// <summary>
/// Baut ein ArrayMesh fuer einen getexturierten Quader mit pro Seite eigenen UV-Koordinaten.
/// </summary>
public static class TexturedCuboid
{
    public readonly record struct UvRect(int X, int Y, int Width, int Height);

    public readonly record struct FaceUvs(
        UvRect Front,
        UvRect Right,
        UvRect Rear,
        UvRect Left,
        UvRect Top,
        UvRect Bottom);

    private const float AtlasWidth = 64f;
    private const float AtlasHeight = 32f;

    public static ArrayMesh Build(float width, float height, float depth, FaceUvs uvs)
    {
        Vector3[] vertices = new Vector3[24];
        Vector3[] normals = new Vector3[24];
        Vector2[] texUvs = new Vector2[24];
        int[] indices = new int[36];

        SetFace(vertices, normals, texUvs, 0, new Vector3(0, 0, 1),
            new Vector3(0, height, depth), new Vector3(width, height, depth),
            new Vector3(0, 0, depth), new Vector3(width, 0, depth),
            uvs.Front);

        SetFace(vertices, normals, texUvs, 4, new Vector3(1, 0, 0),
            new Vector3(width, height, depth), new Vector3(width, height, 0),
            new Vector3(width, 0, depth), new Vector3(width, 0, 0),
            uvs.Right);

        SetFace(vertices, normals, texUvs, 8, new Vector3(0, 0, -1),
            new Vector3(width, height, 0), new Vector3(0, height, 0),
            new Vector3(width, 0, 0), new Vector3(0, 0, 0),
            uvs.Rear);

        SetFace(vertices, normals, texUvs, 12, new Vector3(-1, 0, 0),
            new Vector3(0, height, 0), new Vector3(0, height, depth),
            new Vector3(0, 0, 0), new Vector3(0, 0, depth),
            uvs.Left);

        SetFace(vertices, normals, texUvs, 16, new Vector3(0, 1, 0),
            new Vector3(0, height, 0), new Vector3(width, height, 0),
            new Vector3(0, height, depth), new Vector3(width, height, depth),
            uvs.Top);

        SetFace(vertices, normals, texUvs, 20, new Vector3(0, -1, 0),
            new Vector3(0, 0, depth), new Vector3(width, 0, depth),
            new Vector3(0, 0, 0), new Vector3(width, 0, 0),
            uvs.Bottom);

        int indexCursor = 0;
        for (int faceOffset = 0; faceOffset < 24; faceOffset += 4)
        {
            indices[indexCursor++] = faceOffset;
            indices[indexCursor++] = faceOffset + 1;
            indices[indexCursor++] = faceOffset + 2;
            indices[indexCursor++] = faceOffset + 1;
            indices[indexCursor++] = faceOffset + 3;
            indices[indexCursor++] = faceOffset + 2;
        }

        GodotArray arrays = new();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.TexUV] = texUvs;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        ArrayMesh mesh = new();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private static void SetFace(
        Vector3[] vertices,
        Vector3[] normals,
        Vector2[] texUvs,
        int offset,
        Vector3 normal,
        Vector3 topLeft,
        Vector3 topRight,
        Vector3 bottomLeft,
        Vector3 bottomRight,
        UvRect rect)
    {
        vertices[offset] = topLeft;
        vertices[offset + 1] = topRight;
        vertices[offset + 2] = bottomLeft;
        vertices[offset + 3] = bottomRight;

        normals[offset] = normal;
        normals[offset + 1] = normal;
        normals[offset + 2] = normal;
        normals[offset + 3] = normal;

        float u0 = rect.X / AtlasWidth;
        float v0 = rect.Y / AtlasHeight;
        float u1 = (rect.X + rect.Width) / AtlasWidth;
        float v1 = (rect.Y + rect.Height) / AtlasHeight;

        texUvs[offset] = new Vector2(u0, v0);
        texUvs[offset + 1] = new Vector2(u1, v0);
        texUvs[offset + 2] = new Vector2(u0, v1);
        texUvs[offset + 3] = new Vector2(u1, v1);
    }
}