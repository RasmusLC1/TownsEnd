using Godot;
using System;

public partial class SpectatorCamera : Camera3D
{
    [Export] public float MoveSpeed { get; set; } = 20.0f;
    [Export] public float LookSensitivity { get; set; } = 0.003f;

    private Vector3 _rotation = Vector3.Zero;
    private bool _isLooking = false;

    public override void _Ready()
    {
        // Capture initial rotation angles from the editor setup
        _rotation.Y = Rotation.Y;
        _rotation.X = Rotation.X;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Toggle camera look when holding Right Mouse Button
        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Right)
        {
            _isLooking = mouseButton.Pressed;
            Input.MouseMode = _isLooking ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
        }

        // Handle mouse movement for looking around
        if (_isLooking && @event is InputEventMouseMotion mouseMotion)
        {
            _rotation.Y -= mouseMotion.Relative.X * LookSensitivity;
            _rotation.X -= mouseMotion.Relative.Y * LookSensitivity;
            _rotation.X = Mathf.Clamp(_rotation.X, Mathf.DegToRad(-85), Mathf.DegToRad(85)); // Prevent flipping upside down

            Rotation = new Vector3(_rotation.X, _rotation.Y, 0);
        }
    }

    public override void _Process(double delta)
    {
        float fDelta = (float)delta;
        Vector3 inputDir = Vector3.Zero;

        // Standard fly-cam controls
        if (Input.IsKeyPressed(Key.W)) inputDir.Z -= 1;
        if (Input.IsKeyPressed(Key.S)) inputDir.Z += 1;
        if (Input.IsKeyPressed(Key.A)) inputDir.X -= 1;
        if (Input.IsKeyPressed(Key.D)) inputDir.X += 1;
        if (Input.IsKeyPressed(Key.E)) inputDir.Y += 1; // Fly Up
        if (Input.IsKeyPressed(Key.Q)) inputDir.Y -= 1; // Fly Down

        if (inputDir != Vector3.Zero)
        {
            inputDir = inputDir.Normalized();
            
            // Translate directional input relative to where the camera is facing
            Vector3 forward = Transform.Basis.Z;
            Vector3 right = Transform.Basis.X;
            Vector3 up = Vector3.Up;

            Vector3 moveDir = (forward * inputDir.Z) + (right * inputDir.X) + (up * inputDir.Y);
            GlobalPosition += moveDir * MoveSpeed * fDelta;
        }
    }
}