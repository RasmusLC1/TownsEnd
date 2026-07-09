using Godot;
using System;
using System.Collections.Generic;

public abstract partial class IslandFeatureSpawner : Node
{
    [Export] public int SpawnCount { get; set; } = 10;
    
    protected IslandGenerator Generator;
    
    // Kept protected so children can read if needed, managed cleanly by base class
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

        // 1. Gather filtered candidates
        List<Vector3I> candidates = new();
        foreach (var coordinate in Generator.GetTileCacheKeys())
        {
            IslandTile tile = Generator.GetTileAt(coordinate);
            if (tile != null && tile.IsWalkable && !tile.IsOccupied && Generator.IsTopmostTile(coordinate))
            {
                if (IsValidSpawnTile(tile))
                {
                    candidates.Add(coordinate);
                }
            }
        }

        if (candidates.Count == 0) return;

        // 2. Fisher-Yates shuffle
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rng.RandiRange(0, i);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        // 3. Spawning & placement execution
        int actualCount = Mathf.Min(SpawnCount, candidates.Count);
        for (int i = 0; i < actualCount; i++)
        {
            Vector3I targetGridPos = candidates[i];
            IslandTile tile = Generator.GetTileAt(targetGridPos);

            PackedScene chosenScene = GetRandomTemplate(rng);
            if (chosenScene == null) continue;

            Node3D featureInstance = chosenScene.Instantiate<Node3D>();
            
            // Allow child customization hooks before position calculations
            OnFeatureInstantiated(featureInstance, targetGridPos, rng);

            // Dynamically query calculated placement matrix position
            featureInstance.Position = CalculateSpawnPosition(targetGridPos, featureInstance);

            // Let children execute modifications after positioning (like rotations)
            PostPositionFeature(featureInstance, rng);

            Generator.AddChild(featureInstance);
            SpawnedFeatures.Add(featureInstance);

            // Safely lock down the structural cells
            tile.IsOccupied = true;
            tile.IsWalkable = false;
            tile.OccupyingObject = featureInstance;
        }

        GD.Print($"[{GetType().Name}] Successfully generated {SpawnedFeatures.Count} features.");
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
    protected abstract Vector3 CalculateSpawnPosition(Vector3I gridPos, Node3D instance);
}