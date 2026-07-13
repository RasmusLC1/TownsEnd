using System;

public class WorkTool
{
    public int Health { get; set; } = 100;
    public int MaxHealth { get; set; } = 100;
    public int Price { get; set; } = 10;
    public string ToolName { get; set; } = string.Empty;

    // Optional: A quick constructor to make creating tools even easier
    public WorkTool(string name, int price)
    {
        ToolName = name;
        Price = price;
    }

    public WorkTool() { } // Keeps the parameterless creator working too
}