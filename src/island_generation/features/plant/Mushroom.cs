using Godot;

[Tool]
public partial class Mushroom : SpreadingPlant
{
    public Mushroom()
    {
        Type = "Mushroom";
        Size = 1;
        MaxSize = 10;
        GrowthStep = 1;
        GrowthIntervalSeconds = 15.0;
        SpreadIntervalSeconds = GD.RandRange(40.0, 60.0);
    }
}