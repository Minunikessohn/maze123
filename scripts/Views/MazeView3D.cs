#nullable enable

using System.Collections.Generic;
using Godot;
using Maze.Effects;
using Maze.Game;
using Maze.Model;

namespace Maze.Views;

/// <summary>
/// 3D-Visualisierung des Labyrinths. Nutzt MultiMeshes fuer grosse Wandmengen,
/// damit auch sehr grosse Labyrinthe ohne Node-Flut aufgebaut werden koennen.
/// </summary>
public partial class MazeView3D : Node3D
{
    [Export] public float CellSize = 3.6f;
    [Export] public float WallHeight = 3.4f;
    [Export] public float WallThickness = 0.18f;
    [Export] public float ExploreSunEnergy = 0.05f;
    [Export] public float ExploreAmbientEnergy = 0.05f;
    [Export] public float ExploreFogDensity = 0.06f;
    [Export] public float ExplorePlayerLightEnergy = 2.1f;

    private CameraController3D _camera = null!;
    private DirectionalLight3D _sun = null!;
    private OmniLight3D _playerLight = null!;
    private WorldEnvironment _worldEnvironment = null!;
    private ProximityEffectController _proximityEffects = null!;
    private Node3D _wallContainer = null!;
    private Node3D _markerContainer = null!;
    private MeshInstance3D _floor = null!;
    private MultiMeshInstance3D _floorDetails = null!;
    private MultiMeshInstance3D _floorAccents = null!;
    private MultiMeshInstance3D _wallsHorizontal = null!;
    private MultiMeshInstance3D _wallsVertical = null!;
    private MultiMeshInstance3D _trail = null!;
    private Node3D _startMarker = null!;
    private Node3D _goalMarker = null!;
    private Node3D _startMarkerAccent = null!;
    private Node3D _goalMarkerAccent = null!;
    private OmniLight3D _startMarkerLight = null!;
    private OmniLight3D _goalMarkerLight = null!;
    private bool _exploreTarget;
    private float _exploreFactor;
    private float _brightnessMultiplier = 1f;
    private float _effectsIntensity = 1f;
    private float _dayNightFactor;
    private float _nightViewDistance = MazeGameConfig.DefaultNightViewDistance;
    private bool _isNight;
    private float _cameraDefaultFar;
    private float _markerAnimationTime;
    private float _playerLightDefaultRange;
    private double _atmosphereTime;
    private readonly List<Vector2I> _trailCells = new();
    private readonly List<Vector2I> _monsterCells = new();
    private readonly HashSet<Vector2I> _trailCellSet = new();
    private Vector2I? _playerCell;

    private const float DaySunEnergy = 1.0f;
    private const float DayAmbientEnergy = 0.4f;
    private const float NightSunEnergy = 0.14f;
    private const float NightAmbientEnergy = 0.08f;
    private const float NightFogDensity = 0.98f;
    private const float ExploreFogDepthDensity = 0.96f;
    private const float FogDistanceDensityMultiplier = 1.3f;
    private const float FogBeginDistanceRatio = 0.28f;
    private const float FogDepthCurve = 3.2f;
    private const float NightPlayerLightEnergy = 1.05f;
    private const float DarkModeFactor = 0.45f;
    private const float StartMarkerBaseEmission = 1.7f;
    private const float GoalMarkerBaseEmission = 1.95f;
    private const float TrailBaseEmission = 0.65f;
    private const float MaxFirstPersonFovPenalty = 10f;
    private static readonly Color DayFogColor = new("#cfd6df");
    private static readonly Color NightFogColor = new("#080a0e");
    private static readonly Color ExploreFogColor = new("#050608");
    private static readonly Color PlayerLightDayColor = new("#ffe8cc");
    private static readonly Color PlayerLightNightColor = new("#ffc18f");
    private const float ExploreLerpSpeed = 1.6f;
    private static readonly Color StartMarkerColor = new("#a3be8c");
    private static readonly Color GoalMarkerColor = new("#bf616a");
    private static readonly StandardMaterial3D MarkerBaseMaterial = new()
    {
        AlbedoColor = new Color("#20252b"),
        Metallic = 0.08f,
        Roughness = 0.32f
    };
    private static readonly StandardMaterial3D MarkerTrimMaterial = new()
    {
        AlbedoColor = new Color("#3f4953"),
        Metallic = 0.25f,
        Roughness = 0.18f
    };
    private static readonly StandardMaterial3D StartMarkerMaterial = new()
    {
        AlbedoColor = StartMarkerColor,
        EmissionEnabled = true,
        Emission = StartMarkerColor,
        EmissionEnergyMultiplier = StartMarkerBaseEmission,
        Metallic = 0.12f,
        Roughness = 0.14f
    };
    private static readonly StandardMaterial3D GoalMarkerMaterial = new()
    {
        AlbedoColor = GoalMarkerColor,
        EmissionEnabled = true,
        Emission = GoalMarkerColor,
        EmissionEnergyMultiplier = GoalMarkerBaseEmission,
        Metallic = 0.1f,
        Roughness = 0.12f
    };

    private static readonly StandardMaterial3D WallMaterial = new()
    {
        AlbedoColor = new Color("#dcdcdc")
    };

    private static readonly StandardMaterial3D FloorMaterial = new()
    {
        AlbedoColor = new Color("#2c2c2c")
    };

    private static readonly StandardMaterial3D FloorDetailMaterial = new()
    {
        AlbedoColor = new Color("#1a1d21"),
        Metallic = 0.04f,
        Roughness = 0.85f
    };

    private static readonly StandardMaterial3D FloorAccentMaterial = new()
    {
        AlbedoColor = new Color("#111316"),
        Metallic = 0.02f,
        Roughness = 0.92f
    };

    private static readonly StandardMaterial3D TrailMaterial = new()
    {
        AlbedoColor = new Color("#4ecdc4"),
        EmissionEnabled = true,
        Emission = new Color("#4ecdc4"),
        EmissionEnergyMultiplier = TrailBaseEmission,
        Metallic = 0.05f,
        Roughness = 0.2f
    };

    private global::Maze.Model.Maze? _maze;

    public override void _Ready()
    {
        _camera = GetNode<CameraController3D>("Camera3D");
        _sun = GetNode<DirectionalLight3D>("Sun");
        _playerLight = GetNode<OmniLight3D>("Player/PlayerLight");
        _worldEnvironment = GetNode<WorldEnvironment>("WorldEnvironment");
        _proximityEffects = GetNode<ProximityEffectController>("MonsterProximityOverlay");
        _wallContainer = GetNode<Node3D>("WallContainer");
        _floor = GetNode<MeshInstance3D>("Floor");
        _floorDetails = GetNode<MultiMeshInstance3D>("FloorDetails");
        _wallsHorizontal = GetNode<MultiMeshInstance3D>("WallContainer/WallsHorizontal");
        _wallsVertical = GetNode<MultiMeshInstance3D>("WallContainer/WallsVertical");
        InitializeAtmosphereDetails();
        InitializeTrail();
        InitializeMarkers();

        _floorDetails.MaterialOverride = FloorDetailMaterial;
        _floorAccents.MaterialOverride = FloorAccentMaterial;
        _wallsHorizontal.MaterialOverride = WallMaterial;
        _wallsVertical.MaterialOverride = WallMaterial;

        if (_worldEnvironment.Environment is not null)
        {
            _worldEnvironment.Environment = (Environment)_worldEnvironment.Environment.Duplicate();
        }

        _cameraDefaultFar = _camera.Far;
        _playerLightDefaultRange = _playerLight.OmniRange;

        _proximityEffects.SetCamera(_camera);
        _proximityEffects.SetEffectsScale(_effectsIntensity);
        ApplyExploreFactor(0f);
    }

    public override void _Process(double delta)
    {
        _atmosphereTime += delta;
        float target = _exploreTarget ? 1f : 0f;
        if (!Mathf.IsEqualApprox(_exploreFactor, target))
        {
            _exploreFactor = Mathf.MoveToward(_exploreFactor, target, ExploreLerpSpeed * (float)delta);
        }

        ApplyExploreFactor(_exploreFactor);
        AnimateMarkers((float)delta);
    }

    public void SetMaze(global::Maze.Model.Maze maze)
    {
        _maze = maze;
        Rebuild();
        _camera.FitToMaze(maze, CellSize);
    }

    public void ClearMaze()
    {
        _maze = null;
        _floor.Mesh = null;
        ResetMultiMeshes();
        ClearTrail();
        _monsterCells.Clear();
        _playerCell = null;
        _proximityEffects.Clear();
        _startMarker.Visible = false;
        _goalMarker.Visible = false;
    }

    public void Refresh()
    {
        // In dieser einfachen Variante reicht ein vollstaendiger Neubau.
        if (_maze is not null)
        {
            Rebuild();
        }
    }

    public void MarkTrailCell(int x, int y)
    {
        if (_maze is null || x < 0 || y < 0 || x >= _maze.Width || y >= _maze.Height)
        {
            return;
        }

        Vector2I cell = new(x, y);
        if (!_trailCellSet.Add(cell))
        {
            return;
        }

        _trailCells.Add(cell);
        RebuildTrail();
    }

    public void ClearTrail()
    {
        _trailCells.Clear();
        _trailCellSet.Clear();
        RebuildTrail();
    }

    public void SetMonsterCells(IEnumerable<Vector2I> monsterCells)
    {
        _monsterCells.Clear();
        _monsterCells.AddRange(monsterCells);
        RefreshMonsterProximity();
    }

    public void UpdateMonsterProximity(Vector2I playerCell)
    {
        _playerCell = playerCell;
        RefreshMonsterProximity();
    }

    public void ClearProximityEffects()
    {
        _playerCell = null;
        _proximityEffects.Clear();
    }

    private void Rebuild()
    {
        if (_maze is null)
        {
            _floor.Mesh = null;
            ResetMultiMeshes();
            return;
        }

        BuildFloor(_maze);
        BuildFloorDetails(_maze);
        BuildFloorAccents(_maze);
        BuildWalls(_maze);
        UpdateMarkers(_maze);
        RebuildTrail();
    }

    private void ResetMultiMeshes()
    {
        _floorDetails.Multimesh.InstanceCount = 0;
        _floorDetails.Multimesh.VisibleInstanceCount = 0;
        _floorAccents.Multimesh.InstanceCount = 0;
        _floorAccents.Multimesh.VisibleInstanceCount = 0;
        _wallsHorizontal.Multimesh.InstanceCount = 0;
        _wallsHorizontal.Multimesh.VisibleInstanceCount = 0;
        _wallsVertical.Multimesh.InstanceCount = 0;
        _wallsVertical.Multimesh.VisibleInstanceCount = 0;
    }

    private void BuildFloor(global::Maze.Model.Maze maze)
    {
        float floorThickness = Mathf.Max(0.05f, CellSize * 0.06f);
        Vector3 size = new(maze.Width * CellSize, floorThickness, maze.Height * CellSize);
        _floor.Mesh = new BoxMesh { Size = size };
        _floor.MaterialOverride = FloorMaterial;
        _floor.Position = new Vector3(maze.Width * CellSize / 2f, -floorThickness * 0.5f, maze.Height * CellSize / 2f);
    }

    private void BuildFloorDetails(global::Maze.Model.Maze maze)
    {
        MultiMesh floorDetails = _floorDetails.Multimesh;
        floorDetails.Mesh = new BoxMesh
        {
            Size = new Vector3(CellSize * 0.72f, Mathf.Max(0.02f, CellSize * 0.018f), CellSize * 0.72f)
        };
        floorDetails.InstanceCount = maze.Width * maze.Height;
        floorDetails.VisibleInstanceCount = maze.Width * maze.Height;

        int instanceIndex = 0;
        for (int y = 0; y < maze.Height; y++)
        {
            for (int x = 0; x < maze.Width; x++)
            {
                Vector3 center = new(x * CellSize + CellSize / 2f, 0.012f, y * CellSize + CellSize / 2f);
                float scale = 0.84f + CellNoise(x, y) * 0.14f;
                Basis basis = Basis.Identity.Scaled(new Vector3(scale, 1f, scale));
                floorDetails.SetInstanceTransform(instanceIndex++, new Transform3D(basis, center));
            }
        }
    }

    private void BuildFloorAccents(global::Maze.Model.Maze maze)
    {
        MultiMesh floorAccents = _floorAccents.Multimesh;
        floorAccents.Mesh = new BoxMesh
        {
            Size = new Vector3(CellSize * 0.54f, Mathf.Max(0.03f, CellSize * 0.024f), CellSize * 0.54f)
        };

        List<Transform3D> accentTransforms = new();
        for (int y = 0; y < maze.Height; y++)
        {
            for (int x = 0; x < maze.Width; x++)
            {
                Cell cell = maze.GetCell(x, y);
                int openNeighborCount = CountOpenNeighbors(cell);
                float noise = CellNoise(x + 17, y + 41);
                bool emphasizeCrossing = openNeighborCount >= 3;
                bool addPlate = openNeighborCount >= 2 && noise > 0.72f;

                if (!emphasizeCrossing && !addPlate)
                {
                    continue;
                }

                Vector3 center = global::Maze.MazeWorldGrid.CellToWorldCenter(new Vector2I(x, y), CellSize, 0.018f);
                float scale = emphasizeCrossing
                    ? Mathf.Lerp(0.92f, 1.08f, noise)
                    : Mathf.Lerp(0.7f, 0.9f, noise);
                Basis basis = Basis.Identity.Scaled(new Vector3(scale, 1f, scale));
                accentTransforms.Add(new Transform3D(basis, center));
            }
        }

        floorAccents.InstanceCount = accentTransforms.Count;
        floorAccents.VisibleInstanceCount = accentTransforms.Count;
        for (int index = 0; index < accentTransforms.Count; index++)
        {
            floorAccents.SetInstanceTransform(index, accentTransforms[index]);
        }
    }

    private void BuildWalls(global::Maze.Model.Maze maze)
    {
        ConfigureWallMeshes();

        int maxHorizontal = maze.Width * maze.Height + maze.Width;
        int maxVertical = maze.Width * maze.Height + maze.Height;

        MultiMesh horizontal = _wallsHorizontal.Multimesh;
        MultiMesh vertical = _wallsVertical.Multimesh;

        horizontal.InstanceCount = maxHorizontal;
        vertical.InstanceCount = maxVertical;

        int horizontalIndex = 0;
        int verticalIndex = 0;

        for (int y = 0; y < maze.Height; y++)
        {
            for (int x = 0; x < maze.Width; x++)
            {
                Cell cell = maze.GetCell(x, y);

                if (cell.HasWall(Direction.North))
                {
                    horizontal.SetInstanceTransform(horizontalIndex++, HorizontalWallTransform(x * CellSize + CellSize / 2f, y * CellSize));
                }

                if (cell.HasWall(Direction.West))
                {
                    vertical.SetInstanceTransform(verticalIndex++, VerticalWallTransform(x * CellSize, y * CellSize + CellSize / 2f));
                }

                if (y == maze.Height - 1 && cell.HasWall(Direction.South))
                {
                    horizontal.SetInstanceTransform(horizontalIndex++, HorizontalWallTransform(x * CellSize + CellSize / 2f, (y + 1) * CellSize));
                }

                if (x == maze.Width - 1 && cell.HasWall(Direction.East))
                {
                    vertical.SetInstanceTransform(verticalIndex++, VerticalWallTransform((x + 1) * CellSize, y * CellSize + CellSize / 2f));
                }
            }
        }

        horizontal.VisibleInstanceCount = horizontalIndex;
        vertical.VisibleInstanceCount = verticalIndex;
    }

    private void ConfigureWallMeshes()
    {
        _wallsHorizontal.Multimesh.Mesh = new BoxMesh { Size = new Vector3(CellSize, WallHeight, WallThickness) };
        _wallsVertical.Multimesh.Mesh = new BoxMesh { Size = new Vector3(WallThickness, WallHeight, CellSize) };
    }

    private void InitializeAtmosphereDetails()
    {
        _floorAccents = new MultiMeshInstance3D
        {
            Name = "FloorAccents",
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _floorAccents.Multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D
        };
        AddChild(_floorAccents);
    }

    private void InitializeTrail()
    {
        _trail = new MultiMeshInstance3D
        {
            Name = "Trail"
        };
        _trail.Multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D
        };
        _trail.MaterialOverride = TrailMaterial;
        AddChild(_trail);
        MoveChild(_trail, GetChildCount() - 1);
        RebuildTrail();
    }

    private void InitializeMarkers()
    {
        _markerContainer = new Node3D { Name = "MarkerContainer" };
        AddChild(_markerContainer);

        (_startMarker, _startMarkerAccent, _startMarkerLight) = CreateStartMarker();
        (_goalMarker, _goalMarkerAccent, _goalMarkerLight) = CreateGoalMarker();

        _markerContainer.AddChild(_startMarker);
        _markerContainer.AddChild(_goalMarker);
        _startMarker.Visible = false;
        _goalMarker.Visible = false;
    }

    private (Node3D Root, Node3D Accent, OmniLight3D Light) CreateStartMarker()
    {
        Node3D root = new() { Name = "StartMarker" };
        root.AddChild(CreateMeshInstance(new CylinderMesh
        {
            Height = 0.12f,
            TopRadius = 0.34f,
            BottomRadius = 0.38f,
            RadialSegments = 24
        }, MarkerBaseMaterial, new Vector3(0f, 0.06f, 0f)));
        root.AddChild(CreateMeshInstance(new CylinderMesh
        {
            Height = 0.025f,
            TopRadius = 0.24f,
            BottomRadius = 0.28f,
            RadialSegments = 24
        }, StartMarkerMaterial, new Vector3(0f, 0.135f, 0f)));

        Node3D accent = new() { Name = "Accent" };
        accent.AddChild(CreateMeshInstance(new CylinderMesh
        {
            Height = 0.42f,
            TopRadius = 0.07f,
            BottomRadius = 0.09f,
            RadialSegments = 20
        }, MarkerTrimMaterial, new Vector3(0f, 0.35f, 0f)));
        accent.AddChild(CreateMeshInstance(new SphereMesh
        {
            Radius = 0.13f,
            Height = 0.26f,
            RadialSegments = 20,
            Rings = 10
        }, StartMarkerMaterial, new Vector3(0f, 0.63f, 0f)));

        for (int index = 0; index < 4; index++)
        {
            float angle = Mathf.Pi * 0.5f * index;
            Vector3 finPosition = new(Mathf.Cos(angle) * 0.16f, 0.36f, Mathf.Sin(angle) * 0.16f);
            BoxMesh finMesh = new()
            {
                Size = new Vector3(0.05f, 0.18f, 0.2f)
            };

            MeshInstance3D fin = CreateMeshInstance(finMesh, StartMarkerMaterial, finPosition);
            fin.Rotation = new Vector3(0f, angle, 0f);
            accent.AddChild(fin);
        }

        root.AddChild(accent);

        OmniLight3D light = new()
        {
            Name = "Glow",
            Position = new Vector3(0f, 0.72f, 0f),
            OmniRange = 2.4f,
            LightEnergy = 0.85f,
            LightColor = StartMarkerColor,
            ShadowEnabled = false
        };
        root.AddChild(light);

        return (root, accent, light);
    }

    private (Node3D Root, Node3D Accent, OmniLight3D Light) CreateGoalMarker()
    {
        Node3D root = new() { Name = "GoalMarker" };
        root.AddChild(CreateMeshInstance(new CylinderMesh
        {
            Height = 0.14f,
            TopRadius = 0.36f,
            BottomRadius = 0.4f,
            RadialSegments = 24
        }, MarkerBaseMaterial, new Vector3(0f, 0.07f, 0f)));
        root.AddChild(CreateMeshInstance(new CylinderMesh
        {
            Height = 0.03f,
            TopRadius = 0.19f,
            BottomRadius = 0.24f,
            RadialSegments = 24
        }, GoalMarkerMaterial, new Vector3(0f, 0.155f, 0f)));

        Node3D accent = new() { Name = "Accent" };
        accent.AddChild(CreateMeshInstance(new CylinderMesh
        {
            Height = 0.45f,
            TopRadius = 0.035f,
            BottomRadius = 0.055f,
            RadialSegments = 18
        }, MarkerTrimMaterial, new Vector3(0f, 0.42f, 0f)));

        for (int index = 0; index < 3; index++)
        {
            float angle = index * Mathf.Tau / 3f;
            Vector3 pillarPosition = new(Mathf.Cos(angle) * 0.19f, 0.34f, Mathf.Sin(angle) * 0.19f);
            accent.AddChild(CreateMeshInstance(new CylinderMesh
            {
                Height = 0.3f,
                TopRadius = 0.045f,
                BottomRadius = 0.06f,
                RadialSegments = 18
            }, GoalMarkerMaterial, pillarPosition));
        }

        accent.AddChild(CreateMeshInstance(new SphereMesh
        {
            Radius = 0.12f,
            Height = 0.26f,
            RadialSegments = 20,
            Rings = 10
        }, GoalMarkerMaterial, new Vector3(0f, 0.74f, 0f)));
        root.AddChild(accent);

        OmniLight3D light = new()
        {
            Name = "Glow",
            Position = new Vector3(0f, 0.8f, 0f),
            OmniRange = 2.9f,
            LightEnergy = 1.05f,
            LightColor = GoalMarkerColor,
            ShadowEnabled = false
        };
        root.AddChild(light);

        return (root, accent, light);
    }

    private MeshInstance3D CreateMeshInstance(Mesh mesh, Material material, Vector3 position)
    {
        MeshInstance3D meshInstance = new()
        {
            Mesh = mesh,
            MaterialOverride = material,
            Position = position,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        return meshInstance;
    }

    private void UpdateMarkers(global::Maze.Model.Maze maze)
    {
        Cell startCell = maze.GetCell(0, 0);
        Cell goalCell = maze.GetCell(maze.Width - 1, maze.Height - 1);
        float markerScale = CellSize;

        _startMarker.Scale = Vector3.One * markerScale;
        _goalMarker.Scale = Vector3.One * markerScale;
        _startMarker.Position = CellCenter(startCell);
        _goalMarker.Position = CellCenter(goalCell);
        _startMarkerLight.OmniRange = Mathf.Max(2f, CellSize * 2.4f);
        _goalMarkerLight.OmniRange = Mathf.Max(2.4f, CellSize * 2.9f);
        _startMarker.Visible = true;
        _goalMarker.Visible = true;
    }

    private Vector3 CellCenter(Cell cell) =>
        global::Maze.MazeWorldGrid.CellToWorldCenter(cell, CellSize);

    private void RebuildTrail()
    {
        if (_trail is null)
        {
            return;
        }

        MultiMesh trailMesh = _trail.Multimesh;
        if (_maze is null || _trailCells.Count == 0)
        {
            trailMesh.InstanceCount = 0;
            trailMesh.VisibleInstanceCount = 0;
            return;
        }

        float trailThickness = Mathf.Max(0.028f, CellSize * 0.01f);
        float trailHeight = Mathf.Max(0.07f, CellSize * 0.02f);
        trailMesh.Mesh = new BoxMesh
        {
            Size = new Vector3(CellSize * 0.42f, trailThickness, CellSize * 0.42f)
        };
        trailMesh.InstanceCount = _trailCells.Count;
        trailMesh.VisibleInstanceCount = _trailCells.Count;

        for (int index = 0; index < _trailCells.Count; index++)
        {
            Vector2I cell = _trailCells[index];
            Vector3 center = global::Maze.MazeWorldGrid.CellToWorldCenter(cell, CellSize, trailHeight);
            trailMesh.SetInstanceTransform(index, new Transform3D(Basis.Identity, center));
        }
    }

    private void AnimateMarkers(float delta)
    {
        if (!_startMarker.Visible && !_goalMarker.Visible)
        {
            return;
        }

        _markerAnimationTime += delta;

        float startBob = 0.04f * Mathf.Sin(_markerAnimationTime * 1.9f);
        startBob *= Mathf.Max(1f, CellSize * 0.35f);
        _startMarkerAccent.Position = new Vector3(0f, startBob, 0f);
        _startMarkerAccent.RotateY(delta * 0.75f);

        float goalBob = 0.06f * Mathf.Sin(_markerAnimationTime * 2.4f + 0.8f);
        goalBob *= Mathf.Max(1f, CellSize * 0.35f);
        _goalMarkerAccent.Position = new Vector3(0f, goalBob, 0f);
        _goalMarkerAccent.RotateY(-delta * 1.1f);
    }

    private static float CellNoise(int x, int y)
    {
        unchecked
        {
            int hash = x * 73856093 ^ y * 19349663;
            hash = (hash << 13) ^ hash;
            int value = hash * (hash * hash * 15731 + 789221) + 1376312589;
            value &= 0x7fffffff;
            return value / (float)int.MaxValue;
        }
    }

    private static int CountOpenNeighbors(Cell cell)
    {
        int openCount = 0;
        if (!cell.HasWall(Direction.North))
        {
            openCount++;
        }

        if (!cell.HasWall(Direction.East))
        {
            openCount++;
        }

        if (!cell.HasWall(Direction.South))
        {
            openCount++;
        }

        if (!cell.HasWall(Direction.West))
        {
            openCount++;
        }

        return openCount;
    }

    private Transform3D HorizontalWallTransform(float centerX, float centerZ) =>
        new(Basis.Identity, new Vector3(centerX, WallHeight / 2f, centerZ));

    private Transform3D VerticalWallTransform(float centerX, float centerZ) =>
        new(Basis.Identity, new Vector3(centerX, WallHeight / 2f, centerZ));

    public void SetExploreMode(bool enabled) => _exploreTarget = enabled;

    public void ApplyBrightness(float brightness)
    {
        _brightnessMultiplier = Mathf.Clamp(brightness, 0.4f, 1.8f);
        ApplyExploreFactor(_exploreFactor);
    }

    public void ApplyEffectsIntensity(float effectsIntensity)
    {
        _effectsIntensity = Mathf.Clamp(effectsIntensity, 0f, 1.5f);
        _proximityEffects.SetEffectsScale(_effectsIntensity);
        RefreshMonsterProximity();
        ApplyExploreFactor(_exploreFactor);
    }

    public void ApplyDayNightState(bool cycleEnabled, bool darkModeEnabled, float nightViewDistance, float timeOfDay, bool isNight)
    {
        _nightViewDistance = Mathf.Max(2f, nightViewDistance);
        _isNight = isNight;

        if (cycleEnabled)
        {
            float daylight = 0.5f + 0.5f * Mathf.Cos(Mathf.Tau * Mathf.PosMod(timeOfDay, 1f));
            _dayNightFactor = 1f - daylight;

            if (darkModeEnabled)
            {
                _dayNightFactor = Mathf.Clamp(_dayNightFactor * 1.15f + 0.08f, 0f, 1f);
            }
        }
        else
        {
            _dayNightFactor = darkModeEnabled ? DarkModeFactor : 0f;
        }

        ApplyExploreFactor(_exploreFactor);
    }

    private void ApplyExploreFactor(float factor)
    {
        Environment? environment = _worldEnvironment.Environment;
        if (environment is null)
        {
            return;
        }

        _exploreFactor = Mathf.Clamp(factor, 0f, 1f);
        float baseSunEnergy = Mathf.Lerp(DaySunEnergy, NightSunEnergy, _dayNightFactor);
        float baseAmbientEnergy = Mathf.Lerp(DayAmbientEnergy, NightAmbientEnergy, _dayNightFactor);
        float basePlayerLightEnergy = Mathf.Lerp(0f, NightPlayerLightEnergy, _dayNightFactor);
        float fogDensity = Mathf.Max(
            Mathf.Lerp(0f, NightFogDensity, _dayNightFactor),
            Mathf.Lerp(0f, ExploreFogDepthDensity, _exploreFactor)) * FogDistanceDensityMultiplier;
        float effectiveNightFactor = Mathf.Max(_dayNightFactor, _exploreFactor);
        float stressFactor = Mathf.Clamp(effectiveNightFactor * Mathf.Max(0.45f, _effectsIntensity), 0f, 1f);
        bool fogActive = effectiveNightFactor > 0.01f;
        float fogEndDistance = Mathf.Max(2f, Mathf.Lerp(_cameraDefaultFar, _nightViewDistance, effectiveNightFactor));
        float fogBeginDistance = Mathf.Max(0.1f, fogEndDistance * FogBeginDistanceRatio);
        Color fogColor = DayFogColor.Lerp(NightFogColor, _dayNightFactor).Lerp(ExploreFogColor, _exploreFactor);
        float lightPulse = 1f
            + Mathf.Sin((float)_atmosphereTime * 5.4f) * 0.07f * stressFactor
            + Mathf.Sin((float)_atmosphereTime * 11.1f + 0.9f) * 0.03f * stressFactor;
        float playerLightRange = Mathf.Lerp(_playerLightDefaultRange, Mathf.Max(CellSize * 1.8f, 3.8f), effectiveNightFactor);
        float goalRevealFactor = ComputeGoalRevealFactor(effectiveNightFactor);
        float startMarkerFactor = Mathf.Lerp(1f, 0.55f, effectiveNightFactor) * Mathf.Max(0.15f, _effectsIntensity);
        float goalMarkerFactor = Mathf.Lerp(1f, 0.32f, effectiveNightFactor) * goalRevealFactor * Mathf.Max(0.15f, _effectsIntensity);

        _sun.LightEnergy = Mathf.Lerp(baseSunEnergy, ExploreSunEnergy, _exploreFactor) * _brightnessMultiplier;
        environment.AmbientLightEnergy = Mathf.Lerp(baseAmbientEnergy, ExploreAmbientEnergy, _exploreFactor) * _brightnessMultiplier;
        _playerLight.LightEnergy = Mathf.Lerp(basePlayerLightEnergy, ExplorePlayerLightEnergy, _exploreFactor) * _brightnessMultiplier * lightPulse * Mathf.Max(0.1f, _effectsIntensity);
        _playerLight.OmniRange = playerLightRange;
        _playerLight.LightColor = PlayerLightDayColor.Lerp(PlayerLightNightColor, effectiveNightFactor);
        _playerLight.Visible = effectiveNightFactor > 0.01f;
        environment.FogEnabled = fogActive;
        environment.FogMode = fogActive ? Environment.FogModeEnum.Depth : Environment.FogModeEnum.Exponential;
        environment.FogLightColor = fogColor;
        environment.FogLightEnergy = fogActive ? 0.8f : 1f;
        environment.FogDensity = fogDensity;
        environment.FogSkyAffect = fogActive ? 1f : 0f;
        environment.FogAerialPerspective = 0f;
        environment.BackgroundColor = fogActive ? fogColor : DayFogColor;
        environment.FogDepthBegin = fogBeginDistance;
        environment.FogDepthEnd = fogEndDistance;
        environment.FogDepthCurve = FogDepthCurve;
        _camera.Far = fogActive ? Mathf.Min(_cameraDefaultFar, fogEndDistance + Mathf.Max(CellSize * 1.5f, 3f)) : _cameraDefaultFar;
        _camera.SetFirstPersonFieldOfViewOffset(-MaxFirstPersonFovPenalty * effectiveNightFactor);
        StartMarkerMaterial.EmissionEnergyMultiplier = StartMarkerBaseEmission * startMarkerFactor;
        GoalMarkerMaterial.EmissionEnergyMultiplier = GoalMarkerBaseEmission * goalMarkerFactor;
        TrailMaterial.EmissionEnergyMultiplier = TrailBaseEmission * Mathf.Lerp(1f, 0.78f, effectiveNightFactor) * Mathf.Max(0.15f, _effectsIntensity);
        _startMarkerLight.LightEnergy = 0.85f * startMarkerFactor;
        _goalMarkerLight.LightEnergy = 1.05f * goalMarkerFactor;
    }

    private float ComputeGoalRevealFactor(float effectiveNightFactor)
    {
        float baseReveal = Mathf.Lerp(1f, 0.3f, effectiveNightFactor);
        if (_maze is null || _playerCell is null)
        {
            return baseReveal;
        }

        Vector2I goalCell = new(_maze.Width - 1, _maze.Height - 1);
        float distanceToGoal = _playerCell.Value.DistanceTo(goalCell);
        float distanceReveal = Mathf.InverseLerp(12f, 3f, distanceToGoal);
        return Mathf.Clamp(Mathf.Lerp(baseReveal, 1f, distanceReveal), 0.18f, 1f);
    }

    private void RefreshMonsterProximity()
    {
        if (_playerCell is null || _monsterCells.Count == 0 || (!_isNight && _dayNightFactor > 0.01f))
        {
            _proximityEffects.Clear();
            return;
        }

        float nearestDistance = float.MaxValue;
        Vector2 playerCell = _playerCell.Value;

        foreach (Vector2I monsterCell in _monsterCells)
        {
            float distance = playerCell.DistanceTo(monsterCell);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
            }
        }

        _proximityEffects.ApplyNearestMonsterDistance(nearestDistance);
    }
}