using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class IslandGenerator : GridMap
{
    [Export] public int MapWidth { get; set; } = 60;
    [Export] public int MapDepth { get; set; } = 60;
    [Export] public int MaxHeight { get; set; } = 10;

    [Export] public float NoiseFrequency { get; set; } = 0.05f;
    [Export] public int NoiseSeed { get; set; } = 0;

    // Drag and drop your VisualBox.tscn file into this slot inside the Godot Inspector
    [Export] public PackedScene BoxScene { get; set; }

    [Export] public int BoxSpawnCount { get; set; } = 5;

    // Mesh library IDs (Verify these in your MeshLibrary tab)
    private const int TileGrass = 83;
    private const int TileSand = 41;
    private const int TileStone = 85;

    private static readonly string[] BoxContents = { "grain", "wood", "stone", "fish", "cloth" };

    // VisualBox's source mesh is roughly a 1m cube -- this scales it down to
    // sit proportionally on a tile footprint instead of towering over it.
    // Tweak freely; it doesn't need to match CellSize exactly.

    private FastNoiseLite _noise;
    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    // The cache: every placed cell gets an entry here, keyed by the same
    // Vector3I grid coordinate you pass to SetCellItem.
    private Dictionary<Vector3I, IslandTile> _tileData = new();

    // Top Y of each (x, z) column, used to avoid spawning boxes in spots
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

        _tileData.Clear();
        _columnTopY.Clear();
        ClearSpawnedBoxes(); // Wipe boxes from any previous run before regenerating

        _noise = new FastNoiseLite();
        _noise.Seed = NoiseSeed != 0 ? NoiseSeed : (int)GD.Randi();
        _noise.Frequency = NoiseFrequency;
        _noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;

        // Seed the RNG the same way as the noise so a given NoiseSeed always
        // reproduces the same island AND the same box placement.
        _rng.Seed = (ulong)_noise.Seed;

        GD.Print($"Using Noise Seed: {_noise.Seed}");

        float centerX = MapWidth / 2.0f;
        float centerZ = MapDepth / 2.0f;
        float maxDistance = new Vector2(centerX, centerZ).Length();

        int totalTilesPlaced = 0;

        for (int x = 0; x < MapWidth; x++)
        {
            for (int z = 0; z < MapDepth; z++)
            {
                float noiseVal = _noise.GetNoise2D(x, z);
                noiseVal = (noiseVal + 1.0f) / 2.0f;

                float distFromCenter = new Vector2(x - centerX, z - centerZ).Length();
                float mask = 1.0f - (distFromCenter / maxDistance);
                mask = MathF.Max(0.0f, MathF.Min(mask, 1.0f));

                float finalHeightFactor = noiseVal * (mask * mask);
                int calculatedHeight = (int)(finalHeightFactor * MaxHeight);

                if (calculatedHeight <= 0)
                    continue;

                for (int y = 0; y < calculatedHeight; y++)
                {
                    int tileToPlace;
                    TileType tileType;

                    if (y == calculatedHeight - 1)
                    {
                        bool isSand = y <= 1;
                        tileToPlace = isSand ? TileSand : TileGrass;
                        tileType = isSand ? TileType.Sand : TileType.Grass;
                    }
                    else
                    {
                        tileToPlace = TileStone;
                        tileType = TileType.Stone;
                    }

                    int centeredX = x - (MapWidth / 2);
                    int centeredZ = z - (MapDepth / 2);
                    var gridPos = new Vector3I(centeredX, y, centeredZ);

                    SetCellItem(gridPos, tileToPlace);

                    _tileData[gridPos] = new IslandTile
                    {
                        GridPosition = gridPos,
                        MeshId = tileToPlace,
                        Type = tileType,
                        IsWalkable = (y == calculatedHeight - 1)
                    };

                    if (y == calculatedHeight - 1)
                        _columnTopY[new Vector2I(centeredX, centeredZ)] = y;

                    totalTilesPlaced++;
                }
            }
        }

        GD.Print($"Generation Complete! Placed {totalTilesPlaced} tiles across the grid.");

        SpawnRandomBoxes(BoxSpawnCount);
    }

    /// <summary>
    /// Spawns boxes on N distinct, randomly chosen walkable/unoccupied tiles.
    /// </summary>
    public void SpawnRandomBoxes(int count)
    {
        if (BoxScene == null)
        {
            GD.PrintErr("BoxScene is unassigned in the IslandGenerator inspector -- no boxes will spawn.");
            return;
        }

        // Gather every tile that's actually a valid spawn target.
        var candidates = new List<Vector3I>();
        foreach (var kvp in _tileData)
        {
            // Fix: Pass 0 to maxStepUp so it refuses to spawn if any adjacent block is higher than this tile!
            if (kvp.Value.IsWalkable && !kvp.Value.IsOccupied && IsTopmostTile(kvp.Key) && IsExposedSpawnSpot(kvp.Key, 0))
            {
                candidates.Add(kvp.Key);
            }
        }

        if (candidates.Count == 0)
        {
            GD.PrintErr("No walkable tiles available to spawn boxes on.");
            return;
        }

        // Fisher-Yates shuffle, then take the first `count` -- guarantees
        // distinct positions with no repeats, unlike calling the same
        // coordinate multiple times.
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = _rng.RandiRange(0, i);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        int spawnCount = Mathf.Min(count, candidates.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            string contents = BoxContents[_rng.RandiRange(0, BoxContents.Length - 1)];
            int amount = _rng.RandiRange(50, 150);
            SpawnBoxAt(candidates[i], contents, amount);
        }

        GD.Print($"Spawned {spawnCount} boxes across the island.");
    }

    /// <summary>
    /// Checks if a tile position has any other tile directly above it inside the GridMap.
    /// Returns true if it is completely open to the sky.
    /// </summary>
    public bool IsTopmostTile(Vector3I gridPos)
    {
        // Look exactly 1 unit higher on the Y axis
        Vector3I abovePos = new Vector3I(gridPos.X, gridPos.Y + 1, gridPos.Z);
        
        // GetCellItem returns -1 if the cell is completely empty air
        return GetCellItem(abovePos) == InvalidCellItem;
    }

    /// <summary>
    /// Strictly rejects spots that sit flat against higher terrain faces.
    /// </summary>
    private bool IsExposedSpawnSpot(Vector3I gridPos, int maxStepUp = 0)
    {
        // Ensure the coordinate space directly above the box is 100% empty space
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
            // If a neighbor cell at our height + 1 contains a mesh block, it's a cliffside wall.
            // We reject it to prevent the box from spawning inside the mesh cliff visual boundary.
            if (GetCellItem(new Vector3I(n.X, gridPos.Y + 1, n.Z)) != InvalidCellItem)
                return false;
        }

        return true;
    }
    /// <summary> Spawns your custom .tscn Crate Scene on top of a tile and updates the data cache. </summary>
    public void SpawnBoxAt(Vector3I gridPos, string contents = "grain", int amount = 100)
    {
        IslandTile tile = GetTileAt(gridPos);
        if (tile == null || !tile.IsWalkable || tile.IsOccupied)
        {
            GD.Print($"Cannot spawn box at {gridPos}: Tile is invalid or occupied.");
            return;
        }

        Node3D boxInstance = BoxScene.Instantiate<Node3D>();
        boxInstance.Name = $"Box_{gridPos.X}_{gridPos.Z}";

        if (boxInstance is ItemBox itemBox)
        {
            itemBox.ContentType = contents;
            itemBox.Quantity = amount;
        }

        
        boxInstance.Position = CalculateLocalPos(gridPos, boxInstance);


        AddChild(boxInstance);

        tile.IsOccupied = true;
        tile.IsWalkable = false;
        tile.OccupyingObject = boxInstance;

        GD.Print($"Spawned crate with {amount} x {contents} at: {gridPos}, Topmost Tile {IsTopmostTile(gridPos)}");
    }

    private Vector3 CalculateLocalPos(Vector3I gridPos, Node3D entity)
    {
        // 1. Get the center of the cell from Godot
        Vector3 localPos = MapToLocal(gridPos);
    
        // 2. FORCE the height step to match a standard 1m or 2m tile if your CellSize is tiny.
        // Let's explicitly lift it by half a full unit, or a multiplier that matches your real grid scale.
        // Try changing 0.5f here to 1.0f if it's still slightly too low!
        float realCellHeight = 1.0f; 
        float tileTopY = localPos.Y + (realCellHeight / 2.0f);

        // 3. Add the asset's vertical offset
        float verticalOffset = 0.0f;
        Aabb? boxBounds = GetVisualAabb(entity);
        verticalOffset = boxBounds.Value.Size.Y + boxBounds.Value.Position.Y;


        // 4. Set final position
        localPos.Y = tileTopY + verticalOffset;
        return localPos;        
    }

    private void ClearSpawnedBoxes()
    {
        foreach (var child in GetChildren())
        {
            if (child.Name.ToString().StartsWith("Box_"))
                child.QueueFree();
        }
    }

    /// <summary>
    /// Finds the first mesh (directly or nested in children) under a node and
    /// returns its object-space AABB, so props can be sized/positioned based
    /// on their real geometry instead of assumptions about the source mesh.
    /// </summary>
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

    public IslandTile GetTileAt(Vector3I gridPos) => _tileData.TryGetValue(gridPos, out var tile) ? tile : null;

    public IslandTile GetTileAtWorld(Vector3 worldPos) => GetTileAt(LocalToMap(ToLocal(worldPos)));

    /// <summary>
    /// Safely retrieves a tile from the cache. If it doesn't exist, returns null.
    /// </summary>
    public IslandTile GetTileAtCoordinate(Vector3I gridPos)
    {
        return _tileData.TryGetValue(gridPos, out var tile) ? tile : null;
    }
    
}