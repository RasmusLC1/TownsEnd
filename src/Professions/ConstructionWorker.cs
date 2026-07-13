using Godot;
using System;

public partial class ConstructionWorker : Profession
{
    public ConstructionWorker()
    {
        Salary = 25;                       // Specific pay for construction per day
        WorkHours = [7, 15];               // Early shift example: 07:00 to 15:00
        
        // Setting up their specific default tool
        tool = new WorkTool {
            ToolName = "Hammer",
            Price = 10,
            Health = 100,
            MaxHealth = 100
        };
    }
}