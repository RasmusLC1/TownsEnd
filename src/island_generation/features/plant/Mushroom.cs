using Godot;

[Tool]
public partial class Mushroom : Plant
{
    public Mushroom()
    {
        Type = "mushroom";
        Size = 1;
        MaxSize = 10;
        GrowthStep = 2;
        GrowthIntervalSeconds = 20.0;
    }
}