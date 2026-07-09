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

    protected override void OnFeatureInstantiated(Node3D instance, Vector3I gridPos, RandomNumberGenerator rng)
    {
        instance.Name = $"Rock_{gridPos.X}_{gridPos.Z}";
    }

    protected override Vector3 CalculateSpawnPosition(Vector3I gridPos, Node3D instance)
    {
        // Maintains your custom scale factor alignment logic
        return Generator.CalculateLocalPos(gridPos, instance, instance.Scale.Y / 2);
    }

    protected override void PostPositionFeature(Node3D instance, RandomNumberGenerator rng)
    {
        instance.RotateY(rng.Randf() * Mathf.Pi * 2.0f);
    }
}