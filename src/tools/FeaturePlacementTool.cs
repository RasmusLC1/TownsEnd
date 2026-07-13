using Godot;

/// <summary>
/// Places a single feature scene on the tile where the drag/click started.
/// A minimal example of a second tool -- extend OnAreaSelected if you want
/// it to place across the whole dragged rectangle instead of just the start.
/// </summary>
public partial class FeaturePlacementTool : Node, IGridTool
{
    [Export] private IslandGenerator _islandGenerator;
    [Export] private PackedScene _featureScene;
    [Export] public Color OutlineColor { get; set; } = new Color(0.45f, 1.0f, 0.55f, 0.9f);

    public void OnAreaSelected(Vector2I start, Vector2I end)
    {
        if (_featureScene == null)
        {
            GD.PrintErr("[FeaturePlacementTool] No feature scene assigned.");
            return;
        }

        IslandTile tile = _islandGenerator.GetSurfaceTileAt(start.X, start.Y);
        if (tile == null || tile.IsOccupied)
            return;

        Node3D instance = _featureScene.Instantiate<Node3D>();
        instance.Position = _islandGenerator.CalculateLocalPos(tile.GridPosition, instance);
        _islandGenerator.AddChild(instance);

        tile.IsOccupied = true;
        tile.IsWalkable = false;
        tile.OccupyingObject = instance;
    }

    public void SetFeatureScene(PackedScene newScene)
    {
        _featureScene = newScene;
    }
}