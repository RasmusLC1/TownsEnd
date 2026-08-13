using Godot;

[Tool]
public partial class Bush : SpreadingPlant
{
    public Bush()
    {
        Type = "Bush";
        Size = 1;
        MaxSize = 30;
        GrowthStep = 3;
        GrowthIntervalSeconds = 10.0;
        SpreadIntervalSeconds = GD.RandRange(70.0, 90.0);
    }
}