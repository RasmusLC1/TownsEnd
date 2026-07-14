using Godot;
using System;

public partial class Global : Node
{
    // 1. Static reference so other C# scripts can easily find this singleton
    public static Global Instance { get; private set; }

    public Vector3I DIMENSION = new Vector3I(16, 32, 16);

    public override void _EnterTree()
    {
        // Assign the static instance as soon as Godot instantiates the autoload
        Instance = this;
    }
}