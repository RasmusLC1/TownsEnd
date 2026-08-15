using Godot;

/// <summary>
/// EXAMPLE wiring, not required -- shows the minimal way to turn
/// FeaturePlacementTool's FeatureInspected / FeatureInspectionCleared
/// signals into a visible text box. Attach this to a Control (e.g. a
/// PanelContainer with a Label child) in your HUD scene, point
/// _titleLabel/_descriptionLabel at that Label (or use one multiline Label
/// and drop _descriptionLabel), and connect _featurePlacementTool to the
/// same FeaturePlacementTool node GridInputHandler uses.
///
/// Feel free to replace this with your own popup/tooltip system -- the
/// important part is just connecting to the two signals below.
/// </summary>
public partial class FeatureInfoPanel : Control
{
    [Export] private FeaturePlacementTool _featurePlacementTool;
    [Export] private Label _titleLabel;
    [Export] private Label _descriptionLabel;

    public override void _Ready()
    {
        Visible = false;

        if (_featurePlacementTool == null)
        {
            GD.PrintErr("[FeatureInfoPanel] No FeaturePlacementTool assigned.");
            return;
        }

        _featurePlacementTool.FeatureInspected += OnFeatureInspected;
        _featurePlacementTool.FeatureInspectionCleared += OnFeatureInspectionCleared;
    }

    private void OnFeatureInspected(string displayName, string description)
    {
        if (_titleLabel != null) _titleLabel.Text = displayName;
        if (_descriptionLabel != null) _descriptionLabel.Text = description;
        Visible = true;
    }

    private void OnFeatureInspectionCleared()
    {
        Visible = false;
    }
}