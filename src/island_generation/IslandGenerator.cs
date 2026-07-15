using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// No longer extends GridMap -- GridMap has been fully replaced by
/// IslandChunkSpawner/Chunk for both rendering AND collision
/// (Chunk.Update() calls CreateTrimeshCollision() itself). If any other
/// script in the project references this node expecting GridMap-specific
/// members (SetCellItem, GetCellItem, MapToLocal, CellSize, Clear), it will
/// no longer compile -- worth a project-wide search for those before you
/// build.
/// </summary>
[Tool]
public partial class IslandGenerator : Node3D
{
    // Unit size of one tile/voxel in world space. Chunk's cube vertices are
    // exactly 1x1x1, so this should stay 1.0 unless you change Chunk too.
    public const float TileSize = 1.0f;

    // Backing fields for tracked editor properties
    private int _maxHeight = 10;
    private float _noiseFrequency = 0.05f;
    private int _noiseSeed = 0;
    private float _islandRadius = 60.0f;
    private IslandChunkSpawner _chunkSpawner;

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
        set { if (value) { _chunkSpawner?.ClearAll(); GenerateIsland(); } }
    }

    private FastNoiseLite _noise;
    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    // EVERY placed cell, including buried stone layers -- keep using this
    // for anything that needs the full column (cliff faces, caves, etc).
    private Dictionary<Vector3I, IslandTile> _tileData = new();

    // ONLY the topmost tile of each (x, z) column. This is the single
    // source of truth for "where is the ground here" -- rivers, spawners,
    // and neighbor-linking should all go through this, not _tileData.
    private Dictionary<Vector2I, IslandTile> _surfaceTiles = new();

    /// <summary>
    /// Converts a grid cell coordinate to this node's local space, returning
    /// the MIN corner of that cell -- NOT its center. Chunk places a block
    /// at gridPos spanning [gridPos, gridPos + TileSize) on each axis, unlike
    /// GridMap's old convention of centering cells on integer coordinates.
    /// Anything that used to call MapToLocal() should call this instead, but
    /// double check whether it wanted the center or a corner -- see
    /// CalculateLocalPos below for an example that adds TileSize/2 back in.
    /// </summary>
    public static Vector3 GridToLocal(Vector3I gridPos) => new Vector3(gridPos.X, gridPos.Y, gridPos.Z) * TileSize;

    /// <summary>
    /// Inverse of GridToLocal. Floor-divides (not rounds) so each cell's
    /// [gridPos, gridPos + TileSize) range maps back to gridPos correctly --
    /// a plain round, which is what GridMap's old LocalToMap effectively did
    /// for a centered grid, would be wrong here.
    /// </summary>
    public static Vector3I LocalToGrid(Vector3 localPos) => new Vector3I(
        Mathf.FloorToInt(localPos.X / TileSize),
        Mathf.FloorToInt(localPos.Y / TileSize),
        Mathf.FloorToInt(localPos.Z / TileSize));

    public override void _Ready()
    {
        _chunkSpawner ??= new IslandChunkSpawner(this);
        _chunkSpawner.ClearAll(); // clear any manual/leftover chunks
        GenerateIsland();
    }

    public void GenerateIsland()
    {
        GD.Print("Initializing Island Generation...");
        ResetGeneratorState();
        ConfigureNoiseAndRandomness();

        // 1. Build the physical data map first
        BuildColumns();
        LinkTileNeighborhood();
        PaintSandCoastline();
        ExecuteRiverCarving(); 

        // 2. Spawn the features ON TOP of the finalized data map
        ExecuteFeatureSpawners();

        // 3. Finally, build/mesh the chunks
        _chunkSpawner ??= new IslandChunkSpawner(this);
        _chunkSpawner.BuildAll(_tileData);
    }

    private void PaintSandCoastline()
    {
        var (sandQueue, visitedCoords) = FindCoastTiles();

        // 2. Flood fill inward to paint the sand ring
        while (sandQueue.Count > 0)
        {
            var (currentTile, remainingDepth) = sandQueue.Dequeue();

            // Convert the tile to sand -- purely a data change now, the
            // chunk spawner picks this up when BuildAll(_tileData) runs
            // at the end of GenerateIsland.
            currentTile.Type = TileType.Sand;

            // If we can still push deeper inland, check neighbors
            if (remainingDepth > 1)
            {
                foreach (IslandTile neighbor in currentTile.NeighbouringTiles)
                {
                    Vector2I neighborCoord = new Vector2I(neighbor.GridPosition.X, neighbor.GridPosition.Z);

                    if (!visitedCoords.Contains(neighborCoord))
                    {
                        visitedCoords.Add(neighborCoord);
                        sandQueue.Enqueue((neighbor, remainingDepth - 1));
                    }
                }
            }
        }
    }

    // 1. Identify initial coastline tiles (any surface tile with fewer than 4 neighbors)
    private (Queue<(IslandTile Tile, int Distance)> Queue, HashSet<Vector2I> Visited) FindCoastTiles()
    {
        var queue = new Queue<(IslandTile Tile, int Distance)>();
        var visited = new HashSet<Vector2I>();

        foreach (IslandTile tile in _surfaceTiles.Values)
        {
            if (tile.NeighbouringTiles.Length < 4)
            {
                int maxSandDepth = _rng.RandiRange(2, 4);

                queue.Enqueue((tile, maxSandDepth));
                Vector2I coord = new Vector2I(tile.GridPosition.X, tile.GridPosition.Z);
                visited.Add(coord);
            }
        }

        return (queue, visited);
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

        // 1. Tell spawners to clear their tracked lists
        foreach (Node child in GetChildren())
        {
            if (child is IslandFeatureSpawner spawner)
            {
                spawner.ClearFeatures();
            }
        }

        // 2. Hard sweep: Catch any orphaned nodes under the Generator that are not Spawners themselves
        // (This guarantees no stray trees are left behind in the editor scene dock)
        var childrenToClean = GetChildren();
        for (int i = childrenToClean.Count - 1; i >= 0; i--)
        {
            Node child = childrenToClean[i];
            if (child is not IslandFeatureSpawner && child is not RiverGenerator)
            {
                if (Engine.IsEditorHint())
                {
                    child.Free();
                }
                else
                {
                    child.QueueFree();
                }
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

            // Default everything below the surface to stone; surface
            // defaults to grass and gets overwritten by PaintSandCoastline
            // where applicable.
            TileType tileType = isSurface ? TileType.Grass : TileType.Stone;

            var gridPos = new Vector3I(centeredX, y, centeredZ);

            var tile = new IslandTile
            {
                GridPosition = gridPos,
                Type = tileType,
                IsWalkable = isSurface
            };

            // Every layer goes in _tileData (needed for cliffs/caves/etc,
            // and now also what the chunk spawner reads directly).
            _tileData[gridPos] = tile;

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
                // Force-refresh the generator reference to ensure it points to this active instance
                spawner.Initialize(this);
                spawner.ExecutionPlacement(_rng);
            }
        }
    }

    private void UpdateEditorGeneration()
    {
        if (Engine.IsEditorHint())
        {
            _chunkSpawner?.ClearAll();
            GenerateIsland();
        }
    }

    /// <summary>
    /// Checks if a tile position has any other tile directly above it.
    /// Was a GridMap query; now a direct dictionary lookup against the same
    /// source of truth the chunk spawner reads from.
    /// </summary>
    public bool IsTopmostTile(Vector3I gridPos)
    {
        Vector3I abovePos = new Vector3I(gridPos.X, gridPos.Y + 1, gridPos.Z);
        return !_tileData.ContainsKey(abovePos);
    }

    /// <summary>
    /// Strictly rejects spots that sit flat against higher terrain faces.
    /// Same logic as before, just reading _tileData instead of GetCellItem.
    /// </summary>
    private bool IsExposedSpawnSpot(Vector3I gridPos, int maxStepUp = 0)
    {
        if (_tileData.ContainsKey(new Vector3I(gridPos.X, gridPos.Y + 1, gridPos.Z)))
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
            if (_tileData.ContainsKey(new Vector3I(n.X, gridPos.Y + 1, n.Z)))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Computes where to place `entity` so it sits correctly on top of the
    /// tile at `gridPos`. MapToLocal/CellSize (GridMap API) replaced with
    /// plain grid-to-world math -- TileSize is always 1.0 to match Chunk's
    /// unit cube vertices.
    /// </summary>
    public Vector3 CalculateLocalPos(Vector3I gridPos, Node3D entity, float extraNodge = 0f)
    {
        Vector3 localPos = GridToLocal(gridPos);
        float tileTopY = localPos.Y + TileSize; // corner convention: gridPos.Y is the tile's bottom, so top = bottom + full tile height

        localPos.Y = tileTopY;
        return localPos;
    }

    private Aabb? GetVisualAabb(Node3D root)
    {
        Aabb? result = null;
        AccumulateAabb(root, Transform3D.Identity, ref result);
        return result;
    }

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
    /// Links each SURFACE tile to its 4 surface neighbors, deeper tiles do not matter
    /// </summary>
    public void LinkTileNeighborhood()
    {
        Vector2I[] cardinalDirections =
        {
            new Vector2I(1, 0),
            new Vector2I(-1, 0),
            new Vector2I(0, 1),
            new Vector2I(0, -1)
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

        riverGen ??= new RiverGenerator();

        List<Vector3I> carveTargets = riverGen.GetRiverCarvePath(this, _rng);

        GD.Print($"[IslandGenerator] Total river positions calculated to clear: {carveTargets.Count}");

        foreach (Vector3I pos in carveTargets)
        {
            RemoveColumn(pos);
        }

        // Redundant with the full BuildAll() at the end of GenerateIsland()
        // during initial generation, but keeps ExecuteRiverCarving safe to
        // call standalone later (e.g. a "reroll river" editor action)
        // without needing to know about that detail.
        _chunkSpawner?.RebuildDirty(_tileData);
    }

    public void RemoveColumn(Vector3I gridPos)
    {
        Vector2I xzCoord = new Vector2I(gridPos.X, gridPos.Z);
        _surfaceTiles.Remove(xzCoord);

        for (int y = 0; y <= MaxHeight + 5; y++)
        {
            Vector3I currentPos = new Vector3I(gridPos.X, y, gridPos.Z);

            _tileData.Remove(currentPos);
            _chunkSpawner?.MarkDirty(currentPos);
        }

        NotifyPropertyListChanged();
    }

    public void RemoveTile(Vector3I gridPos)
    {
        if (!_tileData.Remove(gridPos))
        {
            return; // didn't exist, nothing to do
        }

        Vector2I xzCoord = new Vector2I(gridPos.X, gridPos.Z);

        if (_surfaceTiles.TryGetValue(xzCoord, out IslandTile surfaceTile))
        {
            if (surfaceTile.GridPosition == gridPos)
            {
                _surfaceTiles.Remove(xzCoord);
            }
        }

        // Unlike RemoveColumn (called in a tight batch during river
        // carving), this is assumed to be a single standalone removal --
        // e.g. a player mining one block at runtime -- so it rebuilds
        // immediately instead of waiting for a caller to batch it.
        _chunkSpawner?.MarkDirty(gridPos);
        _chunkSpawner?.RebuildDirty(_tileData);

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