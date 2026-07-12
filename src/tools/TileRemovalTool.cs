using Godot;
using System;

/// <summary>
/// Removes every surface tile within the selected rectangle. This is the
/// exact logic that used to live inside GridInputHandler -- moved here
/// unchanged, just no longer coupled to input handling.
/// </summary>
public partial class TileRemovalTool : Node, IGridTool
{
    [Export] private IslandGenerator _islandGenerator;
    [Export] public Color OutlineColor { get; set; } = new Color(1.0f, 0.35f, 0.35f, 0.9f);

    public void OnAreaSelected(Vector2I start, Vector2I end)
    {
        int minX = Math.Min(start.X, end.X);
        int maxX = Math.Max(start.X, end.X);
        int minZ = Math.Min(start.Y, end.Y);
        int maxZ = Math.Max(start.Y, end.Y);

        int removed = 0;
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                IslandTile tile = _islandGenerator.GetSurfaceTileAt(x, z);
                if (tile != null)
                {
                    _islandGenerator.RemoveTile(tile.GridPosition);
                    removed++;
                }
            }
        }

        GD.Print($"[TileRemovalTool] Removed {removed} tile(s) in ({minX},{minZ}) to ({maxX},{maxZ}).");
    }
}