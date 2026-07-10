using Godot;
using System;

public partial class GridInputHandler : Node
{
    // Assign these in the Godot Inspector
    [Export] private Camera3D _camera;
    [Export] private IslandGenerator _islandGenerator; 

    public override void _UnhandledInput(InputEvent @event)
    {
        // Only run if our dependencies are set up in the inspector
        if (_camera == null || _islandGenerator == null) return;

        // Check for Left Mouse Button Press
        if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed && mouseBtn.ButtonIndex == MouseButton.Left)
        {
            Vector3I? hitTile = RaycastFromMouse(mouseBtn.Position);

            if (hitTile.HasValue)
            {
                GD.Print($"[GridInputHandler] Detected click on tile: {hitTile.Value}");
                
                // Keep IslandGenerator pure: we just pass the coordinate to its public method
                _islandGenerator.RemoveTile(hitTile.Value);
            }
        }
    }

    private Vector3I? RaycastFromMouse(Vector2 mousePosition)
    {
        Vector3 rayOrigin = _camera.ProjectRayOrigin(mousePosition);
        Vector3 rayNormal = _camera.ProjectRayNormal(mousePosition);
        Vector3 rayEnd = rayOrigin + (rayNormal * 2000.0f);

        var spaceState = _camera.GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(rayOrigin, rayEnd);
        var result = spaceState.IntersectRay(query);

        if (result.Count == 0)
            return null;

        Vector3 globalHitPosition = (Vector3)result["position"];
        Vector3 globalHitNormal = (Vector3)result["normal"];

        // Nudge inside the collided block -- avoids ambiguity when a click
        // lands exactly on a boundary between two columns.
        Vector3 internalGlobalPoint = globalHitPosition - (globalHitNormal * 0.1f);
        Vector3 localHitPoint = _islandGenerator.ToLocal(internalGlobalPoint);

        // Only trust X/Z from the raycast. Some of your tile meshes (the
        // "overhang" pieces) deliberately bulge outside their own cell's
        // vertical bounds to create smooth terrain, so a raycast can hit
        // real collision geometry at a Y that LocalToMap resolves to a
        // layer that was never actually placed there. Go straight to the
        // cached surface tile for that column instead -- guarantees this
        // always resolves to the true topmost tile, with no room for the
        // returned coordinate to ever drift from what's actually cached.
        Vector3I approxCell = _islandGenerator.LocalToMap(localHitPoint);
        IslandTile surfaceTile = _islandGenerator.GetSurfaceTileAt(approxCell.X, approxCell.Z);

        if (surfaceTile == null)
        {
            GD.Print($"[GridInputHandler] Click resolved to column ({approxCell.X}, {approxCell.Z}) with no terrain -- likely clicked past the coastline.");
            return null;
        }

        return surfaceTile.GridPosition;
    }
}