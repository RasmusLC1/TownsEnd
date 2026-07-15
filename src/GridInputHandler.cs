using Godot;
using System;

/// <summary>
/// Resolves mouse drags/clicks into grid selections and renders the
/// selection outline. Deliberately knows NOTHING about what a selection
/// does -- that's entirely delegated to whichever IGridTool is assigned to
/// each mouse button in the Inspector. Adding a new mouse-driven action
/// means writing a new IGridTool, not touching this file.
/// </summary>
public partial class GridInputHandler : Node
{
    [Export] private Camera3D _camera;
    [Export] private IslandGenerator _islandGenerator;

    // Assign any Node that implements IGridTool (e.g. TileRemovalTool,
    // FeaturePlacementTool). Leave empty to disable that action entirely.
    [Export] private Node _leftClickToolNode;    // Plain left click
    [Export] private Node _leftShiftClickToolNode;  // Shift + left click

    [Export] private float _fallbackPlaneHeight = 0.0f;
    [Export] private float _outlineHeightMargin = 0.05f;

    private IGridTool _primaryTool;
    private IGridTool _secondaryTool;

    private Vector2I? _dragStartColumn;
    private bool _isDragging = false;
    private IGridTool _activeDragTool;

    private MeshInstance3D _outlineInstance;
    private ImmediateMesh _outlineMesh;
    private StandardMaterial3D _outlineMaterial;

    public override void _Ready()
    {
        _primaryTool = ResolveTool(_leftClickToolNode);
        _secondaryTool = ResolveTool(_leftShiftClickToolNode);

        CreateDragMesh();
        _islandGenerator.AddChild(_outlineInstance);
    }

    private IGridTool ResolveTool(Node node)
    {
        if (node == null) return null;

        if (node is IGridTool tool) return tool;

        GD.PrintErr($"[GridInputHandler] '{node.Name}' is assigned as a tool but doesn't implement IGridTool.");
        return null;
    }

    private void CreateDragMesh()
    {
        _outlineMesh = new ImmediateMesh();
        _outlineInstance = new MeshInstance3D { Mesh = _outlineMesh, Visible = false };

        _outlineMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            NoDepthTest = true
        };
        _outlineInstance.MaterialOverride = _outlineMaterial;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_camera == null || _islandGenerator == null) return;

        if (@event is InputEventMouseButton mouseBtn && mouseBtn.ButtonIndex == MouseButton.Left)
        {
            if (mouseBtn.Pressed)
            {
                IGridTool tool = mouseBtn.ShiftPressed ? _secondaryTool : _primaryTool;
                StartDrag(mouseBtn, tool);
            }
            else
            {
                EndDrag(mouseBtn);
            }
        }
    }

    private void StartDrag(InputEventMouseButton mouseBtn, IGridTool tool)
    {
        if (tool == null) return;

        _dragStartColumn = GetGridColumnUnderMouse(mouseBtn.Position);
        _isDragging = _dragStartColumn.HasValue;
        _activeDragTool = tool;

        _outlineMaterial.AlbedoColor = tool.OutlineColor;
        _outlineInstance.Visible = _isDragging;
    }

    private void EndDrag(InputEventMouseButton mouseBtn)
    {
        if (!_isDragging || _activeDragTool == null || !_dragStartColumn.HasValue) return;

        Vector2I? dragEndColumn = GetGridColumnUnderMouse(mouseBtn.Position);
        if (dragEndColumn.HasValue)
        {
            _activeDragTool.OnAreaSelected(_dragStartColumn.Value, dragEndColumn.Value);
        }

        _isDragging = false;
        _dragStartColumn = null;
        _activeDragTool = null;
        _outlineInstance.Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!_isDragging || !_dragStartColumn.HasValue) return;

        Vector2I? currentColumn = GetGridColumnUnderMouse(GetViewport().GetMousePosition());
        if (currentColumn.HasValue)
        {
            UpdateSelectionOutline(_dragStartColumn.Value, currentColumn.Value);
        }
    }

    private void UpdateSelectionOutline(Vector2I a, Vector2I b)
    {
        int minX = Math.Min(a.X, b.X);
        int maxX = Math.Max(a.X, b.X);
        int minZ = Math.Min(a.Y, b.Y);
        int maxZ = Math.Max(a.Y, b.Y);

        int maxSurfaceY = 0;
        bool foundAny = false;
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                int y = _islandGenerator.GetSurfaceYAt(x, z);
                if (y == -1) continue;
                if (!foundAny || y > maxSurfaceY) maxSurfaceY = y;
                foundAny = true;
            }
        }

        // Corner convention: a tile at gridPos.Y occupies [gridPos.Y, gridPos.Y + TileSize),
        // so its top face is gridPos.Y + TileSize, not gridPos.Y + TileSize/2 like the old
        // GridMap-centered math computed.
        float outlineY = foundAny
            ? IslandGenerator.GridToLocal(new Vector3I(0, maxSurfaceY, 0)).Y + IslandGenerator.TileSize + _outlineHeightMargin
            : IslandGenerator.GridToLocal(Vector3I.Zero).Y + _outlineHeightMargin;

        // gridPos IS the near corner already under this convention, and the
        // far corner is exactly one more tile out -- no half-cellSize
        // offsetting needed like the old centered-cell GridMap math required.
        float x0 = minX * IslandGenerator.TileSize;
        float x1 = (maxX + 1) * IslandGenerator.TileSize;
        float z0 = minZ * IslandGenerator.TileSize;
        float z1 = (maxZ + 1) * IslandGenerator.TileSize;

        Vector3 c0 = new Vector3(x0, outlineY, z0);
        Vector3 c1 = new Vector3(x1, outlineY, z0);
        Vector3 c2 = new Vector3(x1, outlineY, z1);
        Vector3 c3 = new Vector3(x0, outlineY, z1);

        _outlineMesh.ClearSurfaces();
        _outlineMesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
        _outlineMesh.SurfaceAddVertex(c0);
        _outlineMesh.SurfaceAddVertex(c1);
        _outlineMesh.SurfaceAddVertex(c2);
        _outlineMesh.SurfaceAddVertex(c3);
        _outlineMesh.SurfaceAddVertex(c0);
        _outlineMesh.SurfaceEnd();
    }

    private Vector2I? GetGridColumnUnderMouse(Vector2 mousePosition)
    {
        Vector3 rayOrigin = _camera.ProjectRayOrigin(mousePosition);
        Vector3 rayDirection = _camera.ProjectRayNormal(mousePosition);

        var spaceState = _camera.GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(rayOrigin, rayOrigin + rayDirection * 2000.0f);
        var result = spaceState.IntersectRay(query);

        Vector3 globalPoint;

        if (result.Count > 0)
        {
            Vector3 hitPos = (Vector3)result["position"];
            Vector3 hitNormal = (Vector3)result["normal"];
            globalPoint = hitPos - (hitNormal * 0.1f);
        }
        else
        {
            if (Mathf.Abs(rayDirection.Y) < 0.0001f)
                return null;

            float t = (_fallbackPlaneHeight - rayOrigin.Y) / rayDirection.Y;
            if (t < 0)
                return null;

            globalPoint = rayOrigin + rayDirection * t;
        }

        Vector3 localPoint = _islandGenerator.ToLocal(globalPoint);
        Vector3I cell = IslandGenerator.LocalToGrid(localPoint);
        return new Vector2I(cell.X, cell.Z);
    }
}