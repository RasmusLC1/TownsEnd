using Godot;
using System;

public partial class PlacementMenu : Control
{
    // A reference to the placement tool running your grid logic
    [Export] private FeaturePlacementTool _placementTool;

    // Add your different buildable items here in the inspector
    [Export] private PackedScene _pathScene;
    [Export] private PackedScene _treeScene;
    [Export] private PackedScene _rockScene;

    // Connected via Godot's node signals in the editor
    public void OnPathButtonClicked()
    {
        SelectFeature(_pathScene);
    }

    public void OnTreeplaceButtonClicked()
    {
        SelectFeature(_treeScene);
    }

    public void OnRockButtonClicked()
    {
        SelectFeature(_rockScene);
    }

    private void SelectFeature(PackedScene scene)
    {
        if (_placementTool == null || scene == null) return;
        
        // 1. Update the scene the tool will drop
        _placementTool.SetFeatureScene(scene);
        
        // 2. Tell your GridInputHandler to make this tool active if it isn't already
        // (e.g., GridInputHandler.SetActiveTool(_placementTool);)
        
        GD.Print($"Selected new feature to place: {scene.ResourcePath}");
    }
}