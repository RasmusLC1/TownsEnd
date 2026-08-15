using Godot;
using System;

/// <summary>
/// Town-sim style camera: pans a ground-level focus point with WASD/edge-scroll,
/// orbits around that point on right-mouse drag, and dollies in/out on scroll wheel.
/// Unlike a free-fly cam, "forward" always means "toward the top of the screen"
/// regardless of the current tilt, which is what makes it feel natural for this genre.
/// </summary>
public partial class TownCamera : Camera3D
{
    [ExportGroup("Panning")]
    [Export] public float PanSpeed { get; set; } = 20.0f;
    [Export] public float PanSmoothing { get; set; } = 10.0f;
    [Export] public bool EdgeScrollEnabled { get; set; } = true;
    [Export] public float EdgeScrollMargin { get; set; } = 16.0f;

    [ExportGroup("Zoom")]
    [Export] public float ZoomStep { get; set; } = 3.0f;
    [Export] public float ZoomSmoothing { get; set; } = 10.0f;
    [Export] public float MinZoomDistance { get; set; } = 0.1f;
    [Export] public float MaxZoomDistance { get; set; } = 60.0f;

    [ExportGroup("Rotation")]
    [Export] public float RotateSensitivity { get; set; } = 0.006f;
    [Export] public float MinPitchDegrees { get; set; } = 30.0f;
    [Export] public float MaxPitchDegrees { get; set; } = 80.0f;

    [ExportGroup("Bounds (optional)")]
    [Export] public bool EnablePanBounds { get; set; } = false;
    [Export] public Vector2 PanBoundsMin { get; set; } = new Vector2(-100, -100);
    [Export] public Vector2 PanBoundsMax { get; set; } = new Vector2(100, 100);

    // The point on the ground the camera is orbiting/looking at.
    private Vector3 _pivotTarget;
    private Vector3 _pivotCurrent;

    private float _yaw;
    private float _pitch; // radians, measured up from horizontal
    private float _zoomTarget;
    private float _zoomCurrent;

    private bool _isRotating = false;

    public override void _Ready()
    {
        // Derive an initial pivot/yaw/pitch/zoom from wherever the camera was
        // placed in the editor, so scene setup still "just works".
        Vector3 forward = -GlobalTransform.Basis.Z;

        if (forward.Y < -0.01f)
        {
            float t = -GlobalPosition.Y / forward.Y;
            _pivotTarget = GlobalPosition + forward * t;
        }
        else
        {
            // Camera isn't tilted down at all; just pick a point ahead of it.
            _pivotTarget = GlobalPosition + forward * 20.0f;
            _pivotTarget.Y = 0.0f;
        }

        _pivotCurrent = _pivotTarget;

        _zoomTarget = GlobalPosition.DistanceTo(_pivotTarget);
        _zoomTarget = Mathf.Clamp(_zoomTarget, MinZoomDistance, MaxZoomDistance);
        _zoomCurrent = _zoomTarget;

        _yaw = Rotation.Y;

        float pitchDeg = Mathf.Clamp(-Mathf.RadToDeg(Rotation.X), MinPitchDegrees, MaxPitchDegrees);
        _pitch = Mathf.DegToRad(pitchDeg);

        ApplyCameraTransform(_pivotCurrent, _yaw, _pitch, _zoomCurrent);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                _isRotating = mouseButton.Pressed;
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelUp && mouseButton.Pressed)
            {
                _zoomTarget = Mathf.Clamp(_zoomTarget - ZoomStep, MinZoomDistance, MaxZoomDistance);
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown && mouseButton.Pressed)
            {
                _zoomTarget = Mathf.Clamp(_zoomTarget + ZoomStep, MinZoomDistance, MaxZoomDistance);
            }
        }

        // Orbit around the pivot while right-click is held. Cursor stays visible
        // and unlocked, matching the feel of most city-builder cameras.
        if (_isRotating && @event is InputEventMouseMotion mouseMotion)
        {
            _yaw -= mouseMotion.Relative.X * RotateSensitivity;

            float pitchDeg = Mathf.RadToDeg(_pitch) + mouseMotion.Relative.Y * RotateSensitivity * 60.0f;
            pitchDeg = Mathf.Clamp(pitchDeg, MinPitchDegrees, MaxPitchDegrees);
            _pitch = Mathf.DegToRad(pitchDeg);
        }
    }

    public override void _Process(double delta)
    {
        float fDelta = (float)delta;

        UpdatePivotFromInput(fDelta);

        // Smoothly interpolate toward the targets so pans/zooms have a bit of weight.
        _pivotCurrent = _pivotCurrent.Lerp(_pivotTarget, 1.0f - Mathf.Exp(-PanSmoothing * fDelta));
        _zoomCurrent = Mathf.Lerp(_zoomCurrent, _zoomTarget, 1.0f - Mathf.Exp(-ZoomSmoothing * fDelta));

        ApplyCameraTransform(_pivotCurrent, _yaw, _pitch, _zoomCurrent);
    }

    private void UpdatePivotFromInput(float fDelta)
    {
        Vector2 inputDir = Vector2.Zero;

        if (Input.IsKeyPressed(Key.W)) inputDir.Y -= 1;
        if (Input.IsKeyPressed(Key.S)) inputDir.Y += 1;
        if (Input.IsKeyPressed(Key.A)) inputDir.X -= 1;
        if (Input.IsKeyPressed(Key.D)) inputDir.X += 1;

        if (EdgeScrollEnabled)
        {
            Vector2 mousePos = GetViewport().GetMousePosition();
            Vector2 screenSize = GetViewport().GetVisibleRect().Size;

            if (mousePos.X <= EdgeScrollMargin) inputDir.X -= 1;
            if (mousePos.X >= screenSize.X - EdgeScrollMargin) inputDir.X += 1;
            if (mousePos.Y <= EdgeScrollMargin) inputDir.Y -= 1;
            if (mousePos.Y >= screenSize.Y - EdgeScrollMargin) inputDir.Y += 1;
        }

        if (inputDir == Vector2.Zero)
            return;

        inputDir = inputDir.Normalized();

        // Move along the ground plane relative to current yaw only -- pitch is
        // deliberately ignored so panning speed/direction doesn't change with zoom tilt.
        Vector3 forward = new Vector3(Mathf.Sin(_yaw), 0, Mathf.Cos(_yaw));
        Vector3 right = new Vector3(forward.Z, 0, -forward.X);

        // Zoom scales pan speed a bit so panning feels consistent whether you're
        // zoomed in close or looking at the whole town from far out.
        float speedScale = Mathf.Lerp(0.4f, 1.5f, Mathf.InverseLerp(MinZoomDistance, MaxZoomDistance, _zoomCurrent));

        Vector3 moveDir = (forward * inputDir.Y) + (right * inputDir.X);
        _pivotTarget += moveDir * PanSpeed * speedScale * fDelta;

        if (EnablePanBounds)
        {
            _pivotTarget.X = Mathf.Clamp(_pivotTarget.X, PanBoundsMin.X, PanBoundsMax.X);
            _pivotTarget.Z = Mathf.Clamp(_pivotTarget.Z, PanBoundsMin.Y, PanBoundsMax.Y);
        }
    }

    private void ApplyCameraTransform(Vector3 pivot, float yaw, float pitch, float zoom)
    {
        Basis yawBasis = new Basis(Vector3.Up, yaw);
        Basis pitchBasis = new Basis(Vector3.Right, -pitch);
        Basis combined = yawBasis * pitchBasis;

        // combined.Z is the direction from the pivot back to the camera.
        Vector3 offset = combined.Z * zoom;

        GlobalTransform = new Transform3D(combined, pivot + offset);
    }
}