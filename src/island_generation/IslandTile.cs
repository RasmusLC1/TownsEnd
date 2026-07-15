using Godot;
using System.Collections.Generic;

// TileType now lives in TileType.cs (shared with Chunk/Global for voxel
// rendering) -- the local 3-value enum that used to live here has been
// removed to avoid two conflicting definitions of the same concept.

/// <summary>
/// Per-tile cached data for quick lookups, pure C# instead of Nodes for performance
/// </summary>
public class IslandTile
{
    public Vector3I GridPosition;
    public int MeshId; // vestigial from the GridMap/MeshLibrary days -- no longer read by the chunk renderer, safe to remove once you confirm nothing else uses it
    public TileType Type;

    public bool IsWalkable = true;
    public bool IsOccupied = false;

    // Store a reference to the actual instance in the world
    public Node3D OccupyingObject = null;

    public Dictionary<string, Variant> Metadata = new();

    public IslandTile[] NeighbouringTiles = new IslandTile[]{};
}