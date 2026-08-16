using Godot;

/// <summary>
/// Self-contained "click a feature, see a little textbox" UI. Builds its
/// own Panel + Labels in code, so there's nothing to wire up in the editor
/// beyond adding this node to the scene and pointing _featurePlacementTool
/// at your FeaturePlacementTool.
/// </summary>
public partial class FeatureInfoPanel : CanvasLayer
{
    [Export] private FeaturePlacementTool _featurePlacementTool;

    // Optional. If set, the panel positions itself just above this
    // control's top edge instead of a fixed distance from the screen
    // bottom -- so it won't overlap a build menu docked at the bottom.
    [Export] private Control _placementMenu;

    [Export] private float _panelWidth = 224f;
    [Export] private float _panelHeight = 64f;
    [Export] private float _margin = 16f;

    [ExportGroup("Debug")]
    // Handy for a standalone test scene (run with F6) that has no
    // FeaturePlacementTool in it -- check this to see the panel immediately
    // on _Ready with sample text, so you can iterate on layout/styling
    // without needing the full game running.
    [Export] private bool _debugShowOnReady = false;
    [Export] private string _debugName = "Oak Tree";
    [Export] private string _debugDescription = "Food: 42/100";

    private PanelContainer _panel;
    private Label _nameLabel;
    private Label _foodLabel;

    public override void _Ready()
    {
        BuildUi();
        PositionPanel();

        // The menu's Size may not be finalized yet this frame (it might be
        // a Container still laying out its children), so reposition once
        // more after everything settles -- and again any time it resizes.
        if (_placementMenu != null)
        {
            _placementMenu.Resized += PositionPanel;
            Callable.From(PositionPanel).CallDeferred();
        }

        if (_featurePlacementTool != null)
        {
            _featurePlacementTool.FeatureInspected += OnFeatureInspected;
            _featurePlacementTool.FeatureInspectionCleared += OnFeatureInspectionCleared;
        }
        else
        {
            GD.PrintErr("[FeatureInfoPanel] No FeaturePlacementTool assigned.");
        }

        if (_debugShowOnReady)
        {
            OnFeatureInspected(_debugName, _debugDescription);
        }
    }

    private void PositionPanel()
    {
        float bottomMargin = _margin + (_placementMenu?.Size.Y ?? 0f);

        _panel.OffsetRight = -_margin;
        _panel.OffsetLeft = -_margin - _panelWidth;
        _panel.OffsetBottom = -bottomMargin;
        _panel.OffsetTop = -bottomMargin - _panelHeight;
    }

    private void BuildUi()
    {
        _panel = new PanelContainer
        {
            Visible = false,
        };
        _panel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        AddChild(_panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        _panel.AddChild(margin);

        var vbox = new VBoxContainer();
        margin.AddChild(vbox);

        _nameLabel = new Label();
        _nameLabel.AddThemeFontSizeOverride("font_size", 18);
        vbox.AddChild(_nameLabel);

        _foodLabel = new Label();
        vbox.AddChild(_foodLabel);
    }

    private void OnFeatureInspected(string displayName, string description)
    {
        _nameLabel.Text = displayName;
        _foodLabel.Text = description;
        _panel.Visible = true;
    }

    private void OnFeatureInspectionCleared()
    {
        _panel.Visible = false;
    }
}