using Godot;

[Tool]
public partial class MeshLibraryCollisionGenerator : Node
{
    [Export] public string MeshLibraryPath { get; set; } = "res://tiles.tres";

    /// <summary>
    /// Same pattern as ForceRebuildIsland on IslandGenerator: this isn't a
    /// real persisted setting, it's a checkbox that acts as a button.
    /// Ticking it in the Inspector runs the generation once, immediately.
    /// </summary>
    [Export]
    public bool GenerateCollisions
    {
        get => false;
        set { if (value) Run(); }
    }

    private void Run()
    {
        var meshLib = ResourceLoader.Load<MeshLibrary>(MeshLibraryPath);
        if (meshLib == null)
        {
            GD.PrintErr($"Could not load MeshLibrary at {MeshLibraryPath}");
            return;
        }

        int[] itemIds = meshLib.GetItemList();
        int updated = 0;

        foreach (int id in itemIds)
        {
            Mesh mesh = meshLib.GetItemMesh(id);
            if (mesh == null)
                continue;

            // Matches exactly what the editor's "Create Trimesh Static Body"
            // does -- a collision shape that hugs the mesh's real geometry.
            Shape3D shape = mesh.CreateTrimeshShape();
            if (shape == null)
            {
                GD.PrintErr($"Could not generate a shape for item {id} ({meshLib.GetItemName(id)})");
                continue;
            }

            var shapesArray = new Godot.Collections.Array { shape, Transform3D.Identity };
            meshLib.SetItemShapes(id, shapesArray);
            updated++;
        }

        Error err = ResourceSaver.Save(meshLib, MeshLibraryPath);
        if (err != Error.Ok)
        {
            GD.PrintErr($"Failed to save MeshLibrary: {err}");
            return;
        }

        GD.Print($"Generated trimesh collision for {updated}/{itemIds.Length} items and saved to {MeshLibraryPath}.");
    }
}