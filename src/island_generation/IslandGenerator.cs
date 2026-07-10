using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

[Tool]
public partial class IslandGenerator : GridMap
{
    // Backing fields for tracked editor properties
    private int _maxHeight = 10;
    private float _noiseFrequency = 0.05f;
    private int _noiseSeed = 0;
    private float _islandRadius = 60.0f;


    [Export] 
    public int MaxHeight 
    { 
        get => _maxHeight; 
        set { _maxHeight = value; UpdateEditorGeneration(); } 
    }

    [Export] 
    public float NoiseFrequency 
    { 
        get => _noiseFrequency; 
        set { _noiseFrequency = value; UpdateEditorGeneration(); } 
    }

    [Export] 
    public int NoiseSeed 
    { 
        get => _noiseSeed; 
        set { _noiseSeed = value; UpdateEditorGeneration(); } 
    }

    [Export] 
    public float IslandRadius 
    { 
        get => _islandRadius; 
        set { _islandRadius = value; UpdateEditorGeneration(); } 
    }

    /// <summary>
    /// Activating this checkbox in the Inspector acts as a button to manually force a total rebuild.
    /// </summary>
    [Export]
    public bool ForceRebuildIsland
    {
        get => false;
        set { if (value) { Clear(); GenerateIsland(); } }
    }

    // Mesh library IDs
    private const int TileGrass = 41;
    private const int TileSand = 83;
    private const int TileStone = 85;

    private FastNoiseLite _noise;
    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    // EVERY placed cell, including buried stone layers -- keep using this
    // for anything that needs the full column (cliff faces, caves, etc).
    private Dictionary<Vector3I, IslandTile> _tileData = new();

    // ONLY the topmost tile of each (x, z) column. This is the single
    // source of truth for "where is the ground here" -- rivers, spawners,
    // and neighbor-linking should all go through this, not _tileData.
    private Dictionary<Vector2I, IslandTile> _surfaceTiles = new();

    public override void _Ready()
    {
        Clear(); // Clear manual tiles
        GenerateIsland();
    }

    public void GenerateIsland()
    {
        GD.Print("Initializing Island Generation...");
        ResetGeneratorState();
        ConfigureNoiseAndRandomness();

        BuildColumns();
        LinkTileNeighborhood();

        ExecuteRiverCarving();
        // Dynamically execute all attached feature spawner decorators (Trees, Boxes, etc.)
        ExecuteFeatureSpawners();
    }

    private void BuildColumns()
    {
        int totalTilesPlaced = 0;
        int halfWidth = (int)IslandRadius + 10;
        int halfDepth = (int)IslandRadius + 10;

        for (int x = -halfWidth; x <= halfWidth; x++)
        {
            for (int z = -halfDepth; z <= halfDepth; z++)
            {
                int calculatedHeight = CalculateCellHeight(x, z);
                if (calculatedHeight <= 0) 
                    continue;

                totalTilesPlaced += BuildTileColumn(x, z, calculatedHeight);
            }
        }
        GD.Print($"Generation Complete! Placed {totalTilesPlaced} tiles across the grid ({_surfaceTiles.Count} surface tiles).");
    }

    

    private void ResetGeneratorState()
    {
        _tileData.Clear();
        _surfaceTiles.Clear();
        
        // Clear out any older manual decorators or box assets hanging in the tree
        foreach (Node child in GetChildren())
        {
            if (child is IslandFeatureSpawner spawner)
            {
                spawner.ClearFeatures();
            }
        }
    }

    private void ConfigureNoiseAndRandomness()
    {
        _noise = new FastNoiseLite
        {
            Seed = NoiseSeed != 0 ? NoiseSeed : (int)GD.Randi(),
            Frequency = NoiseFrequency,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex
        };

        _rng.Seed = (ulong)_noise.Seed;
        GD.Print($"Using Noise Seed: {_noise.Seed}");
    }

    private int CalculateCellHeight(int x, int z)
    {
        float noiseVal = (_noise.GetNoise2D(x, z) + 1.0f) / 2.0f;
        float distFromCenter = new Vector2(x, z).Length();
        float mask = MathF.Max(0.0f, MathF.Min(1.0f - (distFromCenter / IslandRadius), 1.0f));
        float finalHeightFactor = noiseVal * (mask * mask);
        return (int)(finalHeightFactor * MaxHeight);
    }

    private int BuildTileColumn(int centeredX, int centeredZ, int calculatedHeight)
    {
        int tilesPlacedInColumn = 0;

        for (int y = 0; y < calculatedHeight; y++)
        {
            bool isSurface = (y == calculatedHeight - 1);

            int tileToPlace = TileStone;
            TileType tileType = TileType.Stone;

            if (isSurface)
            {
                bool isSand = y <= 1;
                tileToPlace = isSand ? TileSand : TileGrass;
                tileType = isSand ? TileType.Sand : TileType.Grass;
            }

            var gridPos = new Vector3I(centeredX, y, centeredZ);
            SetCellItem(gridPos, tileToPlace);

            var tile = new IslandTile
            {
                GridPosition = gridPos,
                MeshId = tileToPlace,
                Type = tileType,
                IsWalkable = isSurface
            };

            // Every layer goes in _tileData (needed for cliffs/caves/etc).
            _tileData[gridPos] = tile;

            // Only the top layer of the column also goes in _surfaceTiles --
            // this is the fast path everything else should use.
            if (isSurface)
            {
                _surfaceTiles[new Vector2I(centeredX, centeredZ)] = tile;
            }

            tilesPlacedInColumn++;
        }

        return tilesPlacedInColumn;
    }

    private void ExecuteFeatureSpawners()
    {
        foreach (Node child in GetChildren())
        {
            if (child is IslandFeatureSpawner spawner)
            {
                spawner.Initialize(this);
                spawner.ExecutionPlacement(_rng);
            }
        }
    }

    private void UpdateEditorGeneration()
    {
        if (Engine.IsEditorHint())
        {
            Clear();
            GenerateIsland();
        }
    }

    /// <summary>
    /// Checks if a tile position has any other tile directly above it inside the GridMap.
    /// Still uses the full GridMap query since this is about a specific Y layer,
    /// which is exactly the kind of sub-surface question _surfaceTiles can't answer.
    /// </summary>
    public bool IsTopmostTile(Vector3I gridPos)
    {
        Vector3I abovePos = new Vector3I(gridPos.X, gridPos.Y + 1, gridPos.Z);
        return GetCellItem(abovePos) == InvalidCellItem;
    }

    /// <summary>
    /// Strictly rejects spots that sit flat against higher terrain faces.
    /// Kept working off the full GridMap/_tileData -- this is a cliff-face
    /// check, which is exactly the case you still need the sub-surface data for.
    /// </summary>
    private bool IsExposedSpawnSpot(Vector3I gridPos, int maxStepUp = 0)
    {
        if (GetCellItem(new Vector3I(gridPos.X, gridPos.Y + 1, gridPos.Z)) != InvalidCellItem)
            return false;

        Span<Vector3I> neighbors = stackalloc Vector3I[]
        {
            new Vector3I(gridPos.X + 1, gridPos.Y, gridPos.Z),
            new Vector3I(gridPos.X - 1, gridPos.Y, gridPos.Z),
            new Vector3I(gridPos.X,     gridPos.Y, gridPos.Z + 1),
            new Vector3I(gridPos.X,     gridPos.Y, gridPos.Z - 1),
        };

        foreach (var n in neighbors)
        {
            if (GetCellItem(new Vector3I(n.X, gridPos.Y + 1, n.Z)) != InvalidCellItem)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Computes where to place `entity` so it sits correctly on top of the
    /// tile at `gridPos`. Always uses the entity's real AABB to work out the
    /// lift needed -- never guessed from scale alone. `extraNudgeY` is an
    /// OPTIONAL adjustment on top of the correct base placement (e.g. -0.05
    /// to sink a trunk slightly into the ground), not a replacement for it.
    /// </summary>
    public Vector3 CalculateLocalPos(Vector3I gridPos, Node3D entity)
    {
        Vector3 localPos = MapToLocal(gridPos);
        float tileTopY = localPos.Y + (CellSize.Y / 2.0f);

        Aabb? entityBounds = GetVisualAabb(entity);
        float baseLift = entityBounds.HasValue ? -entityBounds.Value.Position.Y: 0.0f;

        localPos.Y = tileTopY + baseLift + 1; // Offset by 1 for tile size
        return localPos;
    }

    private Aabb? GetVisualAabb(Node3D root)
    {
        Aabb? result = null;
        AccumulateAabb(root, Transform3D.Identity, ref result);
        return result;
    }

    /// <summary>
    /// Walks the entire subtree, merging the AABB of every mesh found into
    /// one combined bounding box expressed in `root`'s local space, so a
    /// model made of several parts (trunk, canopy, etc.) gets one bounding
    /// box covering the whole thing rather than just whichever mesh happens
    /// to be found first.
    /// </summary>
    private void AccumulateAabb(Node3D node, Transform3D transformToRoot, ref Aabb? result)
    {
        if (node is VisualInstance3D visual)
        {
            Aabb localAabb = visual.GetAabb();
            Aabb rootSpaceAabb = TransformAabb(transformToRoot, localAabb);
            result = result.HasValue ? result.Value.Merge(rootSpaceAabb) : rootSpaceAabb;
        }

        foreach (Node child in node.GetChildren())
        {
            if (child is Node3D child3D)
            {
                Transform3D childTransformToRoot = transformToRoot * child3D.Transform;
                AccumulateAabb(child3D, childTransformToRoot, ref result);
            }
        }
    }

    private static Aabb TransformAabb(Transform3D transform, Aabb aabb)
    {
        Vector3 min = aabb.Position;
        Vector3 max = aabb.Position + aabb.Size;

        Aabb result = new Aabb(transform * min, Vector3.Zero);
        for (int i = 1; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) != 0 ? max.X : min.X,
                (i & 2) != 0 ? max.Y : min.Y,
                (i & 4) != 0 ? max.Z : min.Z);
            result = result.Expand(transform * corner);
        }

        return result;
    }

    /// <summary>
    /// Links each SURFACE tile to its 4 surface neighbors, deeper tiles do not
    /// matter
    /// </summary>
    public void LinkTileNeighborhood()
    {
        Vector2I[] cardinalDirections =
        {
            new Vector2I(1, 0),  // East
            new Vector2I(-1, 0), // West
            new Vector2I(0, 1),  // South
            new Vector2I(0, -1)  // North
        };

        foreach (IslandTile tile in _surfaceTiles.Values)
        {
            List<IslandTile> validNeighbors = new();

            foreach (Vector2I dir in cardinalDirections)
            {
                var neighborKey = new Vector2I(tile.GridPosition.X + dir.X, tile.GridPosition.Z + dir.Y);
                if (_surfaceTiles.TryGetValue(neighborKey, out IslandTile neighborTile))
                {
                    validNeighbors.Add(neighborTile);
                }
            }

            tile.NeighbouringTiles = validNeighbors.ToArray();
        }
    }

    private void ExecuteRiverCarving()
    {
        RiverGenerator riverGen = null;

        foreach (Node child in GetChildren())
        {
            if (child is RiverGenerator foundGen)
            {
                riverGen = foundGen;
                break;
            }
        }

        if (riverGen == null)
        {
            riverGen = new RiverGenerator(); 
        }

            // 1. Calculate the full path sequence while the neighborhood mapping is completely intact
            List<Vector3I> carveTargets = riverGen.GetRiverCarvePath(this, _rng);
            
            GD.Print($"[IslandGenerator] Total river positions calculated to clear: {carveTargets.Count}");

            // 2. Batch execute the destruction now that the path sequence is locked in
            foreach (Vector3I pos in carveTargets)
            {
                RemoveColumn(pos);
            }
    }
    public void RemoveColumn(Vector3I gridPos)
    {
        Vector2I xzCoord = new Vector2I(gridPos.X, gridPos.Z);
        _surfaceTiles.Remove(xzCoord);

        for (int y = 0; y <= MaxHeight + 5; y++)
        {
            // Create the specific position for this Y layer
            Vector3I currentPos = new Vector3I(gridPos.X, y, gridPos.Z);

            SetCellItem(currentPos, -1); 

            _tileData.Remove(currentPos);
        }
        
        // Force the editor to redraw this grid section
        NotifyPropertyListChanged();
    }

    public void RemoveTile(Vector3I gridPos)
    {
        // 1. Visually erase the 3D block from the GridMap scene
        SetCellItem(gridPos, -1);

        // 2. Clear the internal backend tracking data record
        if (!_tileData.Remove(gridPos))
        {
            return; // The tile didn't exist in our backend data structure anyway
        }

        // 3. Handle surface validation exactly like a precise column step
        Vector2I xzCoord = new Vector2I(gridPos.X, gridPos.Z);
        
        if (_surfaceTiles.TryGetValue(xzCoord, out IslandTile surfaceTile))
        {
            // Only drop the surface tracking entry if this specific vertical block was the surface layer
            if (surfaceTile.GridPosition == gridPos)
            {
                _surfaceTiles.Remove(xzCoord);
            }
        }

        // 4. Force the editor to update and redraw this grid section
        NotifyPropertyListChanged();
    }

    /// <summary> Full-column lookup -- includes buried/stone layers. Use for cliffs/caves. </summary>
    public IslandTile GetTileAt(Vector3I gridPos) => _tileData.TryGetValue(gridPos, out var tile) ? tile : null;

    /// <summary> Surface-only lookup -- the tile actually visible/walkable at this (x, z). </summary>
    public IslandTile GetSurfaceTileAt(Vector2I xz) => _surfaceTiles.TryGetValue(xz, out var tile) ? tile : null;

    public IslandTile GetSurfaceTileAt(int x, int z) => GetSurfaceTileAt(new Vector2I(x, z));

    /// <summary> All surface tiles -- the set anything doing rivers/spawning/pathing should iterate over. </summary>
    public IEnumerable<IslandTile> GetAllSurfaceTiles() => _surfaceTiles.Values;

    public IslandTile GetTallestUnoccupiedTile()
    {
        if (_surfaceTiles.Count == 0) return null;

        // Now correctly restricted to surface tiles only -- previously this
        // searched _tileData directly and could return a buried stone tile
        // from a tall hill instead of an actual walkable peak.
        return _surfaceTiles.Values
            .Where(tile => !tile.IsOccupied)
            .MaxBy(tile => tile.GridPosition.Y);
    }
    
    public IEnumerable<Vector3I> GetTileCacheKeys() => _tileData.Keys;

    /// <summary> Fast O(1) lookup of the surface Y for a column. Returns -1 if the column is empty/ocean. </summary>
    public int GetSurfaceYAt(int x, int z)
    {
        return _surfaceTiles.TryGetValue(new Vector2I(x, z), out var tile) ? tile.GridPosition.Y : -1;
    }
}
