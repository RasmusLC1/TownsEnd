using Godot;
using System;

public partial class Profession : Node
{
	public int Salary = 0;
	public string WorkPlace = null; // Temp string needs to be replaced with the actual building later
	public int[] WorkHours = [8, 16];
	public WorkTool tool = null; 
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
