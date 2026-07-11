using Godot;
using System;

public partial class GridInputHandler : Node
{
    // Assign these in the Godot Inspector
    [Export] private Camera3D _camera;
    [Export] private IslandGenerator _islandGenerator;

    [Export] private float _fallbackPlaneHeight = 0.0f;
    [Export] private Color _outlineColor = new Color(0.4f, 0.8f, 1.0f, 0.9f);
    [Export] private float _outlineHeightMargin = 0.05f;

    private Vector2I? _dragStartColumn;
    private bool _isDragging = false;

    private MeshInstance3D _outlineInstance;
    private ImmediateMesh _outlineMesh;

    public override void _Ready()
    {
        CreateDragMesh();
        // Parented directly under the GridMap so MapToLocal's output can be
        // used as-is for the outline's own local-space positions.
        _islandGenerator.AddChild(_outlineInstance);
    }


    /// <summary>
    /// Creates a ImmediateMesh -> MeshInstance3D drag box with a
    /// blue outline that can be used for drag functionality
    /// Depth disabled
    /// </summary>
    public void CreateDragMesh()
    {
        _outlineMesh = new ImmediateMesh();
        _outlineInstance = new MeshInstance3D { Mesh = _outlineMesh, Visible = false };

        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = _outlineColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            // Keeps the outline visible even when it's nearly flush with the
            // terrain surface, instead of z-fighting/disappearing into it.
            NoDepthTest = true
        };
        _outlineInstance.MaterialOverride = material;

    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_camera == null || _islandGenerator == null) return;

        if (@event is InputEventMouseButton mouseBtn && mouseBtn.ButtonIndex == MouseButton.Left)
        {
            Left_Mouse_Click(mouseBtn);
        }
    }

    private void Left_Mouse_Click(InputEventMouseButton mouseBtn)
    {
            if (mouseBtn.Pressed)
            {
                Handle_Left_Click(mouseBtn);
                
            }
            else if (_isDragging && _dragStartColumn.HasValue)
            {
                Handle_Left_Drag(mouseBtn);
            }
    }

    private void Handle_Left_Click(InputEventMouseButton mouseBtn)
    {
        _dragStartColumn = GetGridColumnUnderMouse(mouseBtn.Position);
        _isDragging = _dragStartColumn.HasValue;
        _outlineInstance.Visible = _isDragging;
    }

    private void Handle_Left_Drag(InputEventMouseButton mouseBtn)
    {
        Vector2I? dragEndColumn = GetGridColumnUnderMouse(mouseBtn.Position);
        if (dragEndColumn.HasValue)
        {
            RemoveTilesInRectangle(_dragStartColumn.Value, dragEndColumn.Value);
        }

        _isDragging = false;
        _dragStartColumn = null;
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

        // Sit slightly above the tallest tile currently in range so the
        // outline doesn't clip into hills as the selection grows/shrinks.
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

        Vector3 cellSize = _islandGenerator.CellSize;
        float outlineY = foundAny
            ? _islandGenerator.MapToLocal(new Vector3I(0, maxSurfaceY, 0)).Y + (cellSize.Y / 2.0f) + _outlineHeightMargin
            : _islandGenerator.MapToLocal(Vector3I.Zero).Y + _outlineHeightMargin;

        // MapToLocal gives cell CENTERS -- push outward by half a cell on
        // each side so the outline traces the selection's true outer edge.
        Vector3 innerMin = _islandGenerator.MapToLocal(new Vector3I(minX, 0, minZ));
        Vector3 innerMax = _islandGenerator.MapToLocal(new Vector3I(maxX, 0, maxZ));

        float x0 = innerMin.X - cellSize.X / 2.0f;
        float x1 = innerMax.X + cellSize.X / 2.0f;
        float z0 = innerMin.Z - cellSize.Z / 2.0f;
        float z1 = innerMax.Z + cellSize.Z / 2.0f;

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
        _outlineMesh.SurfaceAddVertex(c0); // close the loop
        _outlineMesh.SurfaceEnd();
    }

    /// <summary>
    /// Removes every surface tile whose column falls within the rectangle
    /// spanning `a` and `b` (inclusive). When a == b, this removes exactly
    /// one tile -- the same behavior as a plain click.
    /// </summary>
    private void RemoveTilesInRectangle(Vector2I a, Vector2I b)
    {
        int minX = Math.Min(a.X, b.X);
        int maxX = Math.Max(a.X, b.X);
        int minZ = Math.Min(a.Y, b.Y);
        int maxZ = Math.Max(a.Y, b.Y);

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

        GD.Print($"[GridInputHandler] Removed {removed} tile(s) in selection ({minX},{minZ}) to ({maxX},{maxZ}).");
    }

    /// <summary>
    /// Resolves the grid column under the mouse. Prefers an actual raycast
    /// hit against terrain collision (matches the true topmost tile via
    /// GetSurfaceTileAt, same as a normal click). Falls back to a flat
    /// plane intersection if nothing was hit, so dragging across water or
    /// gaps still produces a usable column for the selection rectangle.
    /// </summary>
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
        Vector3I cell = _islandGenerator.LocalToMap(localPoint);
        return new Vector2I(cell.X, cell.Z);
    }
}