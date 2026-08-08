using Godot;
using System;
using System.Collections.Generic;

public abstract partial class IslandFeatureSpawner : Node
{
    [Export] public int SpawnCount { get; set; } = 10;
    
    protected IslandGenerator Generator;
    
    protected readonly List<Node3D> SpawnedFeatures = new();

    public virtual void Initialize(IslandGenerator generator)
    {
        Generator = generator;
    }

    /// <summary> Template method handling the core layout orchestrations. </summary>
    public virtual void ExecutionPlacement(RandomNumberGenerator rng)
    {
        ClearFeatures();

        if (!ValidateTemplates())
        {
            GD.PrintErr($"[{GetType().Name}] Template configuration validation failed!");
            return;
        }
        List<IslandTile> candidates = GenerateCandidates(rng);
        if (candidates.Count == 0) return;
        
        PlaceFeatures(rng, candidates);

        GD.Print($"[{GetType().Name}] Successfully generated {SpawnedFeatures.Count} features.");
    }

    private List<IslandTile> GenerateCandidates(RandomNumberGenerator rng)
    {
        List<IslandTile> candidates = new();
        foreach (IslandTile tile in Generator.GetAllSurfaceTiles())
        {
            if (!tile.IsOccupied && IsValidSpawnTile(tile))
            {
                candidates.Add(tile);
            }
        }

        if (candidates.Count == 0) return candidates;

        // 2. Fisher-Yates shuffle
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rng.RandiRange(0, i);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }
        return candidates;
    }

    private void PlaceFeatures(RandomNumberGenerator rng, List<IslandTile> candidates)
    {
        int actualCount = Mathf.Min(SpawnCount, candidates.Count);
        for (int i = 0; i < actualCount; i++)
        {
            IslandTile tile = candidates[i];

            Node3D featureInstance = GetFeatureScene(rng, tile);

            if (featureInstance == null) continue;

            PostPositionFeature(featureInstance, tile, rng);

            Generator.AddChild(featureInstance);

            if (Engine.IsEditorHint())
            {
                featureInstance.Owner = Generator.GetTree().EditedSceneRoot;
            }

            SpawnedFeatures.Add(featureInstance);

            tile.IsOccupied = true;
            tile.IsWalkable = false;
            tile.OccupyingObject = featureInstance;
        }
    }

    private Node3D GetFeatureScene(RandomNumberGenerator rng, IslandTile tile)
    {
        Vector3I targetGridPos = tile.GridPosition;

        PackedScene chosenScene = GetRandomTemplate(rng);
        if (chosenScene == null) return null;

        Node3D featureInstance = chosenScene.Instantiate<Node3D>();

        OnFeatureInstantiated(featureInstance, tile, chosenScene, rng); // pass chosenScene through

        featureInstance.Position = CalculateSpawnPosition(targetGridPos, featureInstance, rng);

        return featureInstance;
    }
    
    protected virtual Vector3 CalculateSpawnPosition(Vector3I gridPos, Node3D instance, RandomNumberGenerator rng)
    {
        Vector3 position = Generator.CalculateLocalPos(gridPos, instance);
        
        GD.Print(position);
        position.X += rng.Randf();
        position.Z += rng.Randf();
        GD.Print("UDPATED POS", position);

        return position;
    }

    public virtual void ClearFeatures()
    {
        foreach (var feature in SpawnedFeatures)
        {
            if (GodotObject.IsInstanceValid(feature))
            {
                // If running in the editor, free immediately to clean the scene tree right now
                if (Engine.IsEditorHint())
                {
                    feature.Free(); 
                }
                else
                {
                    feature.QueueFree();
                }
            }
        }
        SpawnedFeatures.Clear();
    }
    // --- Template Hooks to be implemented by child classes ---
    protected abstract bool ValidateTemplates();
    protected abstract bool IsValidSpawnTile(IslandTile tile);
    protected abstract PackedScene GetRandomTemplate(RandomNumberGenerator rng);
    protected virtual void OnFeatureInstantiated(Node3D instance, IslandTile tile, PackedScene sourceScene, RandomNumberGenerator rng) {}
    protected virtual void PostPositionFeature(Node3D instance, IslandTile tile, RandomNumberGenerator rng) {}
}
