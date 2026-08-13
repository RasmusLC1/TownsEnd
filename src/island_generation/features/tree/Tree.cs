using Godot;

[Tool]
public partial class Tree : SpreadingPlant
{
    public Tree()
    {
        Type = "oak";
        Size = 1;
        MaxSize = 100;
        GrowthStep = 10;
        GrowthIntervalSeconds = 10.0;
        SpreadIntervalSeconds = GD.RandRange(60.0, 100.0);
    }
}