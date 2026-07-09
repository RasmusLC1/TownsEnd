using Godot;
using System.Collections.Generic;

public enum TileType
{
    Grass,
    Sand,
    Stone
}


/// <summary>
/// Per-tile cached data. This is a plain C# class (not a Node/Resource) because
/// you'll likely have thousands of these -- keeping them lightweight matters.
/// Add whatever fields your game actually needs (resource yield, owner, etc.).
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
}