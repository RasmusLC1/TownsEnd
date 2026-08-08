using Godot;

[Tool]
public partial class RockSpawner : IslandFeatureSpawner
{
    [Export] public Godot.Collections.Array<PackedScene> RockTemplates { get; set; } = new();

    protected override bool ValidateTemplates() => RockTemplates.Count > 0;

    protected override bool IsValidSpawnTile(IslandTile tile) => tile.Type == TileType.Grass;

    protected override PackedScene GetRandomTemplate(RandomNumberGenerator rng)
    {
        return RockTemplates[rng.RandiRange(0, RockTemplates.Count - 1)];
    }

    protected override void OnFeatureInstantiated(Node3D instance, IslandTile tile, PackedScene sourceScene, RandomNumberGenerator rng)
    {
        instance.Name = $"Rock_{tile.GridPosition.X}_{tile.GridPosition.Z}";
        // if (instance is Plant plant)
        // {
        //     plant.Size = rng.RandiRange(1, plant.MaxSize);
        //     plant.OccupiedTile = tile;
        // }
    }



    protected override void PostPositionFeature(Node3D instance, IslandTile tile, RandomNumberGenerator rng)
    {
        instance.RotateY(rng.Randf() * Mathf.Pi * 2.0f);
    }
}