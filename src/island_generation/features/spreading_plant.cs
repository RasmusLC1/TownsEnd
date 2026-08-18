using Godot;
using System;

[Tool]
public partial class SpreadingPlant : Plant, ISpreadable
{
    [Export] public double SpreadIntervalSeconds { get; set; } = 10.0;

    // ISpreadable implementation: plant can only spread if it has reached max size
    public bool CanSpread => Size >= MaxSize;

    private Timer _spreadTimer;

    public override void _Ready()
    {
        base._Ready();

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
        if (CanSpread)
        {
            Spread();
        }
    }

    // Explicit ISpreadable implementation
    public void Spread()
    {
        if (OccupiedTile == null || Scene == null || Generator == null) return;

        IslandTile newTile = OccupiedTile.GetRandomNeighbouringTile();
        if (newTile == null || newTile.IsOccupied || newTile.Type != TileType.Grass) return;

        Node3D newInstance = SpawnNewPlant(newTile);

        newTile.IsOccupied = true;
        newTile.IsWalkable = false;
        newTile.OccupyingObject = newInstance;
    }

    private Node3D SpawnNewPlant(IslandTile newTile)
    {
        Node3D newInstance = Scene.Instantiate<Node3D>();
        Generator.AddChild(newInstance);

        if (Engine.IsEditorHint())
        {
            newInstance.Owner = Generator.GetTree().EditedSceneRoot;
        }

        newInstance.Position = Generator.CalculateLocalPos(newTile.GridPosition, newInstance);

        if (newInstance is Plant newPlant)
        {
            newPlant.OccupiedTile = newTile;
            newPlant.Generator = Generator;
            newPlant.Scene = Scene;
            newPlant.Size = 1; // Spawns as a new baby plant
        }

        return newInstance;
    }

    protected override void OnGrowthTick()
    {
        base.OnGrowthTick();
        if (Size >= MaxSize && _spreadTimer.IsStopped())
        {
            _spreadTimer.Start();
        }
    }
}