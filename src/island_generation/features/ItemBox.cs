using Godot;
using System;

public partial class ItemBox : Node3D
{
    // The type of content inside the box (e.g., "grain", "stone", "wood")
    [Export] public string ContentType { get; set; } = "grain";
    
    // The amount of resources held inside the box
    [Export] public int Quantity { get; set; } = 100;
}