using Godot;
using System;

public partial class Settler : Node3D
{
	// The type of content inside the box (e.g., "grain", "stone", "wood")
    [Export] public Home Home { get; set; } = null;
    [Export] public string Profession { get; set; } = null;
    
    // The amount of resources held inside the box
    [Export] public Settler Partner { get; set; } = null;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
