using Godot;

[Tool]
public partial class PlantSpawner : IslandFeatureSpawner
{
    [Export] public Godot.Collections.Array<PackedScene> PlantTemplates { get; set; } = new();

    protected override bool ValidateTemplates() => PlantTemplates.Count > 0;

    protected override bool IsValidSpawnTile(IslandTile tile) => tile.Type == TileType.Grass;

    protected override PackedScene GetRandomTemplate(RandomNumberGenerator rng)
    {
        return PlantTemplates[rng.RandiRange(0, PlantTemplates.Count - 1)];
    }

    protected override void OnFeatureInstantiated(Node3D instance, IslandTile tile, PackedScene sourceScene, RandomNumberGenerator rng)
    {
        instance.Name = $"Plant_{tile.GridPosition.X}_{tile.GridPosition.Z}";
        if (instance is Plant plant)
        {
            plant.Size = rng.RandiRange(1, plant.MaxSize);
            plant.OccupiedTile = tile;
        }
    }

    protected override void PostPositionFeature(Node3D instance, IslandTile tile, RandomNumberGenerator rng)
    {
        instance.RotateY(rng.Randf() * Mathf.Pi * 2.0f);
    }
}