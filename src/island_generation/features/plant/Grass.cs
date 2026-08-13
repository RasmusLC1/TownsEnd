using Godot;

[Tool]
public partial class Grass : SpreadingPlant
{
    public Grass()
    {
        Type = "Bush";
        Size = 1;
        MaxSize = 5;
        GrowthStep = 1;
        GrowthIntervalSeconds = 5.0;
        SpreadIntervalSeconds = GD.RandRange(60.0, 70.0);
    }
}