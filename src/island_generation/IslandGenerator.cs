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

    // Mesh library IDs (Verify these in your MeshLibrary tab)
    private const int TileGrass = 83;
    private const int TileSand = 41;
    private const int TileStone = 85;

    private FastNoiseLite _noise;
    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    // The cache: every placed cell gets an entry here, keyed by the same
    // Vector3I grid coordinate you pass to SetCellItem.
    private Dictionary<Vector3I, IslandTile> _tileData = new();

    // Top Y of each (x, z) column, used to avoid spawning features in spots
    // pinned right against a much taller neighboring column.
    private Dictionary<Vector2I, int> _columnTopY = new();

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

        int totalTilesPlaced = 0;
        
        // Calculate boundaries dynamically based on your desired radius 
        // adding a small padding buffer so edges transition cleanly
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
        LinkTileNeighborhood();
        GD.Print($"Generation Complete! Placed {totalTilesPlaced} tiles across the grid.");

        ExecuteRiverGenerators();
        // Dynamically execute all attached feature spawner decorators (Trees, Boxes, etc.)
        ExecuteFeatureSpawners();
    }

    private void ExecuteRiverGenerators()
    {
        foreach (Node child in GetChildren())
        {
            if (child is RiverGenerator riverGen)
            {
                riverGen.GenerateRiver(this, _rng);
            }
        }
    }

    private void ResetGeneratorState()
    {
        _tileData.Clear();
        _columnTopY.Clear();
        
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
        // Keep your raw noise sampling frequency configuration intact
        float noiseVal = (_noise.GetNoise2D(x, z) + 1.0f) / 2.0f;

        // Calculate distance directly from the local origin point (0,0)
        float distFromCenter = new Vector2(x, z).Length();
        
        // Normalize falloff strictly using your IslandRadius parameter
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
            
            // Determine tile asset lookups
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

            // Cache metadata profile for routing AI pathfinding and spawner rules
            _tileData[gridPos] = new IslandTile
            {
                GridPosition = gridPos,
                MeshId = tileToPlace,
                Type = tileType,
                IsWalkable = isSurface
            };

            if (isSurface)
            {
                _columnTopY[new Vector2I(centeredX, centeredZ)] = y;
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
    /// Returns true if it is completely open to the sky.
    /// </summary>
    public bool IsTopmostTile(Vector3I gridPos)
    {
        Vector3I abovePos = new Vector3I(gridPos.X, gridPos.Y + 1, gridPos.Z);
        return GetCellItem(abovePos) == InvalidCellItem;
    }

    /// <summary>
    /// Strictly rejects spots that sit flat against higher terrain faces.
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

    public Vector3 CalculateLocalPos(Vector3I gridPos, Node3D entity, float baseOffsetY = 0.0f)
    {
        Vector3 localPos = MapToLocal(gridPos);

        float realCellHeight = 1.0f; 
        float tileTopY = localPos.Y + (realCellHeight / 2.0f);

        float verticalOffset = 0.0f;
        if (baseOffsetY == 0.0f)
        {
            Aabb? boxBounds = GetVisualAabb(entity);
            if (boxBounds.HasValue)
            {
                verticalOffset = boxBounds.Value.Size.Y + boxBounds.Value.Position.Y;
            }
        }
        else
        {
            verticalOffset = baseOffsetY;
        }

        localPos.Y = tileTopY + verticalOffset;
        return localPos;        
    }

    private Aabb? GetVisualAabb(Node3D node)
    {
        if (node is VisualInstance3D visual)
            return visual.GetAabb();

        foreach (Node child in node.GetChildren())
        {
            if (child is Node3D child3D)
            {
                Aabb? result = GetVisualAabb(child3D);
                if (result.HasValue)
                    return result;
            }
        }

        return null;
    }

    public void LinkTileNeighborhood()
    {
        // A quick lookup helper for relative 2D directions
        Vector2I[] cardinalDirections = new Vector2I[]
        {
            new Vector2I(1, 0),  // East
            new Vector2I(-1, 0), // West
            new Vector2I(0, 1),  // South
            new Vector2I(0, -1)  // North
        };

        foreach (KeyValuePair<Vector3I, IslandTile> entry in _tileData)
        {
            IslandTile tile = entry.Value;
            List<IslandTile> validNeighbors = new();

            foreach (Vector2I dir in cardinalDirections)
            {
                // Calculate the target coordinate for the neighbor column
                int neighborX = tile.GridPosition.X + dir.X;
                int neighborZ = tile.GridPosition.Z + dir.Y;

                // Grab the top surface height profile for that column coordinate
                int neighborTopY = GetSurfaceYAt(neighborX, neighborZ);

                if (neighborTopY != -1)
                {
                    Vector3I neighborTargetKey = new Vector3I(neighborX, neighborTopY, neighborZ);
                    IslandTile neighborTile = GetTileAt(neighborTargetKey);

                    if (neighborTile != null)
                    {
                        validNeighbors.Add(neighborTile);
                    }
                }
            }

            // Cache the list cleanly as an array onto your class object
            tile.NeighbouringTiles = validNeighbors.ToArray();
        }
    }

    // Returns the tile at a given grid position
    public IslandTile GetTileAt(Vector3I gridPos) => _tileData.TryGetValue(gridPos, out var tile) ? tile : null;

    public IslandTile GetTallestUnoccupiedTile()
    {
        if (_tileData.Count == 0) return null;
        
        // Filter out occupied tiles first, then find the one with the maximum Y
        return _tileData.Values
            .Where(tile => !tile.IsOccupied)
            .MaxBy(tile => tile.GridPosition.Y);
    }
    
    public System.Collections.Generic.IEnumerable<Vector3I> GetTileCacheKeys()
    {
        return _tileData.Keys;
    }

    /// <summary>
    /// Fast O(1) lookup to get the highest surface Y position for a given (x, z) coordinate.
    /// Returns -1 if the column is empty/ocean.
    /// </summary>
    public int GetSurfaceYAt(int x, int z)
    {
        Vector2I key = new Vector2I(x, z);
        return _columnTopY.TryGetValue(key, out int y) ? y : -1;
    }

    
}