using Godot;

[Tool]
public partial class BoxSpawner : IslandFeatureSpawner
{
    [Export] public PackedScene BoxTemplate { get; set; }
    
    private static readonly string[] BoxContents = { "grain", "wood", "stone", "fish", "cloth" };

    protected override bool ValidateTemplates() => BoxTemplate != null;

    // Boxes run everywhere except pure cliff tiles (Sand or Grass)
    protected override bool IsValidSpawnTile(IslandTile tile) => true; 

    protected override PackedScene GetRandomTemplate(RandomNumberGenerator rng) => BoxTemplate;

    protected override void OnFeatureInstantiated(Node3D instance, Vector3I gridPos, RandomNumberGenerator rng)
    {
        instance.Name = $"Box_{gridPos.X}_{gridPos.Z}";

        if (instance is ItemBox itemBox)
        {
            itemBox.ContentType = BoxContents[rng.RandiRange(0, BoxContents.Length - 1)];
            itemBox.Quantity = rng.RandiRange(50, 150);
        }
    }

    protected override Vector3 CalculateSpawnPosition(Vector3I gridPos, Node3D instance)
    {
        return Generator.CalculateLocalPos(gridPos, instance, 0.25f); // Box height
    }


    
}