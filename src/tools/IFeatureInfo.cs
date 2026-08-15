/// <summary>
/// Implement this on the root script of any feature scene (tree, rock,
/// crate, building, etc.) to make it inspectable -- FeaturePlacementTool
/// looks for this interface on OccupyingObject when the player clicks a
/// tile with nothing selected to place.
/// </summary>
public interface IFeatureInfo
{
    string DisplayName { get; }
    string Description { get; }
}