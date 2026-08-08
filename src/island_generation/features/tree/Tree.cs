using Godot;
using System;


[Tool]
public partial class Tree : Plant
{
    [Export] public double SpreadIntervalSeconds { get; set; } = 10.0; // time to spread another tree
    [Export] public PackedScene TreeScene { get; set; } // assign the Tree's own scene in the inspector
    public IslandGenerator Generator; // set by TreeSpawner when this tree is spawned, like OccupiedTile
    public Tree()
    {
        Type = "oak";
        Size = 1;
        MaxSize = 100;
        GrowthStep = 10;
        GrowthIntervalSeconds = 1.0;
    }
    private Timer _spreadTimer;


    public override void _Ready()
    {
        base._Ready(); // runs Plant's model setup, scaling, and growth timer
        SpreadIntervalSeconds = GD.RandRange(9.0, 11.0);

        // your extra hook logic here
        _spreadTimer = new Timer
        {
            WaitTime = SpreadIntervalSeconds,
            Autostart = false,
            OneShot = false
        };
        AddChild(_spreadTimer);
        _spreadTimer.Timeout += OnSpreadTick;
    }

    private void OnSpreadTick()
    {
        if (OccupiedTile == null || TreeScene == null || Generator == null) return;

        IslandTile newTile = OccupiedTile.GetRandomNeighbouringTile();
        if (newTile == null || newTile.IsOccupied || newTile.Type != TileType.Grass) return;

        
        Node3D newInstance = SpawnNewTree(newTile);

        newTile.IsOccupied = true;
        newTile.IsWalkable = false;
        newTile.OccupyingObject = newInstance;
    }

    private Node3D SpawnNewTree(IslandTile newTile)
    {
        Node3D newInstance = TreeScene.Instantiate<Node3D>();
        Generator.AddChild(newInstance);


        if (Engine.IsEditorHint())
        {
            newInstance.Owner = Generator.GetTree().EditedSceneRoot;
        }

        newInstance.Position = Generator.CalculateLocalPos(newTile.GridPosition, newInstance);

        if (newInstance is Tree newTree)
        {
            newTree.OccupiedTile = newTile;
            newTree.Generator = Generator;
            newTree.TreeScene = TreeScene;
            newTree.Size = 80;

        }
        return newInstance;
    }

    protected override void OnGrowthTick()
    {
        base.OnGrowthTick();
        if (Size >= MaxSize)
        {
            _spreadTimer.Start();
        }
    }

}