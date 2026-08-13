using Godot;

[Tool]
public partial class TreeSpawner : IslandFeatureSpawner
{
    [Export] public Godot.Collections.Array<PackedScene> TreeTemplates { get; set; } = new();

    protected override bool ValidateTemplates() => TreeTemplates.Count > 0;

    protected override bool IsValidSpawnTile(IslandTile tile) => tile.Type == TileType.Grass;

    protected override PackedScene GetRandomTemplate(RandomNumberGenerator rng)
    {
        return TreeTemplates[rng.RandiRange(0, TreeTemplates.Count - 1)];
    }

    protected override void OnFeatureInstantiated(Node3D instance, IslandTile tile, PackedScene sourceScene, RandomNumberGenerator rng)
    {
        instance.Name = $"Tree_{tile.GridPosition.X}_{tile.GridPosition.Z}";

        if (instance is Tree tree)
        {
            tree.Size = rng.RandiRange(10, tree.MaxSize);
            tree.OccupiedTile = tile;
            tree.Generator = Generator;
            tree.Scene = sourceScene;
        }
    }

    protected override void PostPositionFeature(Node3D instance, IslandTile tile, RandomNumberGenerator rng)
    {
        instance.RotateY(rng.Randf() * Mathf.Pi * 2.0f);
    }
}