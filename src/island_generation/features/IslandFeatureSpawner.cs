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
         // 3. Spawning & placement execution
        int actualCount = Mathf.Min(SpawnCount, candidates.Count);
        for (int i = 0; i < actualCount; i++)
        {
            IslandTile tile = candidates[i];
            Vector3I targetGridPos = tile.GridPosition;

            PackedScene chosenScene = GetRandomTemplate(rng);
            if (chosenScene == null) continue;

            Node3D featureInstance = chosenScene.Instantiate<Node3D>();
            
            OnFeatureInstantiated(featureInstance, targetGridPos, rng);

            featureInstance.Position = CalculateSpawnPosition(targetGridPos, featureInstance);

            PostPositionFeature(featureInstance, rng);

            Generator.AddChild(featureInstance);
            SpawnedFeatures.Add(featureInstance);

            tile.IsOccupied = true;
            tile.IsWalkable = false;
            tile.OccupyingObject = featureInstance;
        }
    }

    protected virtual Vector3 CalculateSpawnPosition(Vector3I gridPos, Node3D instance)
    {
        return Generator.CalculateLocalPos(gridPos, instance);
    }

    public virtual void ClearFeatures()
    {
        foreach (var feature in SpawnedFeatures)
        {
            if (GodotObject.IsInstanceValid(feature))
                feature.QueueFree();
        }
        SpawnedFeatures.Clear();
    }

    // --- Template Hooks to be implemented by child classes ---
    protected abstract bool ValidateTemplates();
    protected abstract bool IsValidSpawnTile(IslandTile tile);
    protected abstract PackedScene GetRandomTemplate(RandomNumberGenerator rng);
    protected virtual void OnFeatureInstantiated(Node3D instance, Vector3I gridPos, RandomNumberGenerator rng) {}
    protected virtual void PostPositionFeature(Node3D instance, RandomNumberGenerator rng) {}
}
