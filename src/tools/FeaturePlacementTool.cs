using Godot;

/// <summary>
/// Two modes in one tool, switched purely by whether a feature is currently
/// selected:
///
///   - Selected (_featureScene != null): clicking places that feature on the
///     tile under the click, same as before.
///   - Idle (_featureScene == null, the tool's starting state): clicking a
///     tile instead *inspects* whatever is occupying it, emitting
///     FeatureInspected so a UI element can show a name/description.
///
/// Call SelectFeature(scene) from your build-menu buttons to arm a
/// placement. Call ClearSelection() (or right-click, which
/// GridInputHandler wires up automatically via ISelectableGridTool) to go
/// back to idle/inspect mode.
/// </summary>
public partial class FeaturePlacementTool : Node, IGridTool, ISelectableGridTool
{
    [Export] private IslandGenerator _islandGenerator;

    [ExportGroup("Outline Colors")]
    [Export] public Color PlacementOutlineColor { get; set; } = new Color(0.45f, 1.0f, 0.55f, 0.9f);
    [Export] public Color InspectOutlineColor { get; set; } = new Color(0.45f, 0.75f, 1.0f, 0.9f);

    // Starts null on purpose -- the tool boots into "idle/inspect" mode
    // until something is picked from the build menu.
    private PackedScene _featureScene;

    public Color OutlineColor => _featureScene != null ? PlacementOutlineColor : InspectOutlineColor;
    public bool HasActiveSelection => _featureScene != null;

    // Fired when an inspected tile has a describable feature on it.
    [Signal] public delegate void FeatureInspectedEventHandler(string displayName, string description);
    // Fired when an inspected tile is empty (or has a non-describable object),
    // so any open info box can hide itself.
    [Signal] public delegate void FeatureInspectionClearedEventHandler();
    // Fired whenever the selection changes, so build-menu UI can update its
    // "currently selected" highlight (including on deselect via right-click).
    [Signal] public delegate void SelectionChangedEventHandler(bool hasSelection);

    public void OnAreaSelected(Vector2I start, Vector2I end)
    {
        IslandTile tile = _islandGenerator.GetSurfaceTileAt(start.X, start.Y);
        if (tile == null)
            return;

        if (_featureScene != null)
        {
            PlaceFeature(tile);
        }
        else
        {
            InspectFeature(tile);
        }
    }

    private void PlaceFeature(IslandTile tile)
    {
        if (tile.IsOccupied)
            return;

        Node3D instance = _featureScene.Instantiate<Node3D>();
        instance.Position = _islandGenerator.CalculateLocalPos(tile.GridPosition, instance);
        _islandGenerator.AddChild(instance);

        tile.IsOccupied = true;
        tile.IsWalkable = false;
        tile.OccupyingObject = instance;
    }

    private void InspectFeature(IslandTile tile)
    {
        if (tile.IsOccupied && tile.OccupyingObject is IFeatureInfo info)
        {
            EmitSignal(SignalName.FeatureInspected, info.DisplayName, info.Description);
        }
        else
        {
            EmitSignal(SignalName.FeatureInspectionCleared);
        }
    }

    /// <summary> Call from a build-menu button to arm placement of this feature. </summary>
    public void SelectFeature(PackedScene scene)
    {
        if (scene == null)
        {
            GD.PrintErr("[FeaturePlacementTool] SelectFeature called with a null scene -- use ClearSelection() to deselect instead.");
            return;
        }

        _featureScene = scene;
        EmitSignal(SignalName.SelectionChanged, true);
    }

    /// <summary> Returns to idle/inspect mode. Called automatically on right-click. </summary>
    public void ClearSelection()
    {
        if (_featureScene == null)
            return;

        _featureScene = null;
        EmitSignal(SignalName.SelectionChanged, false);
    }
}