using Godot;
using System;

public partial class Home : Node
{
    public Settler[] Residents { get; set; } = Array.Empty<Settler>(); // The family that lives there
    public int ResidentsMax { get; set; } = 4; // Number of residents a house can hold
    public IslandTile Tile { get; set; } = null; // Central Tile the house is placed on
    public int Food { get; set; } = 10; // Settlers eat 1 food per day

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
