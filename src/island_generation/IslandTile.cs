using Godot;
using System.Collections.Generic;
using System.Diagnostics.Tracing;

public enum TileType
{
    Grass,
    Sand,
    Stone
}


/// <summary>
/// Per-tile cached data for quick lookups, pure C# instead of Nodes for performance
/// </summary>
public class IslandTile
{
    public Vector3I GridPosition;
    public int MeshId;
    public TileType Type;

    public bool IsWalkable = true;
    public bool IsOccupied = false;
    
    // Store a reference to the actual instance in the world
    public Node3D OccupyingObject = null; 

    public Dictionary<string, Variant> Metadata = new();

    public IslandTile[] NeighbouringTiles = new IslandTile[]{};

}



