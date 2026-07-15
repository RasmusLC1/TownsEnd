using Godot;
using System;

public enum TileType
{
    Air, // represents an empty voxel cell -- distinct from "no data yet"
    Grass,
    Sand,
    Stone,
    RockUnderground,
    Snow
}

/// <summary>
/// Maps each TileType to its UV rect in the shared texture atlas, so every
/// tile in the world can be drawn from ONE mesh + ONE material, and the
/// only thing that changes per-tile is which UV sub-rect gets baked into
/// its quads.
///
/// Assumes the atlas is a single horizontal strip: one square cell per
/// TileType, in enum declaration order. Swap in a real atlas later as long
/// as it keeps this layout, or update CellCount/GetUVRect if you move to a
/// grid layout instead of a strip.
/// </summary>
public static class TileTexturer
{
    private static readonly TileType[] AtlasOrder = (TileType[])Enum.GetValues(typeof(TileType));
    private static readonly int CellCount = AtlasOrder.Length;

    /// <summary>
    /// Returns this tile type's UV sub-rect (0-1 space) within the atlas.
    /// Currently the same texture is used on all 6 faces of a tile; see
    /// GetUVRect(TileType, TileFace) below if you want top/side/bottom
    /// variation later (e.g. grass top vs. dirt sides).
    /// </summary>
    public static Rect2 GetUVRect(TileType type)
    {
        int index = Array.IndexOf(AtlasOrder, type);
        float cellWidth = 1.0f / CellCount;
        return new Rect2(index * cellWidth, 0f, cellWidth, 1f);
    }
}

public enum TileFace
{
    Top,
    Bottom,
    North, // -Z
    South, // +Z
    East,  // +X
    West   // -X
}