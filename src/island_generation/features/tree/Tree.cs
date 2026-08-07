[Tool]
public partial class Tree : Plant
{
    public Tree()
    {
        Type = "oak";
        Size = 1;
        Maxsize = 100;
        GrowthStep = 10;
        GrowthIntervalSeconds = 10.0;
    }
}