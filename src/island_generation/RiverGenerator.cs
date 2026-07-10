using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

[Tool]
public partial class RiverGenerator : Node
{
    [Export(PropertyHint.Range, "0,1")] public float MomentumBias { get; set; } = 0.4f;
    [Export(PropertyHint.Range, "0,3")] public float OutwardBias { get; set; } = 1.8f;
    [Export(PropertyHint.Range, "1,5")] public int BaseRiverWidth { get; set; } = 2;
    [Export] public int SpawnCount { get; set; } = 4; // 3-4 distinct rivers usually look best!

    public List<Vector3I> GetRiverCarvePath(IslandGenerator generator, RandomNumberGenerator rng)
    {
        GD.Print("[RiverGenerator] Running multi-river pathway distribution...");

        var surfaceTiles = generator.GetAllSurfaceTiles().Where(t => !t.IsOccupied).ToList();
        if (!surfaceTiles.Any()) return new List<Vector3I>();

        HashSet<Vector3I> uniqueCarvePositions = new();

        // Find high-altitude tiles near the center to act as a source mountain range
        var candidateSources = surfaceTiles
            .OrderBy(tile => new Vector2(tile.GridPosition.X, tile.GridPosition.Z).LengthSquared())
            .Take(50) // Grab the top 50 closest to center
            .OrderByDescending(tile => tile.GridPosition.Y) // Sort by highest elevation
            .ToList();

        int activeStreams = Mathf.Min(SpawnCount, candidateSources.Count);

        for (int i = 0; i < activeStreams; i++)
        {
            // Pick distinct starting spots so they don't choke the center point
            int sourceIndex = (i * 7) % candidateSources.Count;
            IslandTile source = candidateSources[sourceIndex];

            List<IslandTile> tilePath = TraceRiverPath(source, generator, rng);
            
            // Expand each distinct path organically
            foreach (var tile in tilePath)
            {   
                int radius = BaseRiverWidth - 1; 
                Vector3I centerPos = tile.GridPosition;

                for (int xOffset = -radius; xOffset <= radius; xOffset++)
                {
                    for (int zOffset = -radius; zOffset <= radius; zOffset++)
                    {
                        // Direct circular brush validation
                        if ((xOffset * xOffset) + (zOffset * zOffset) <= radius * radius)
                        {
                            Vector3I neighborPos = new Vector3I(
                                centerPos.X + xOffset,
                                centerPos.Y,
                                centerPos.Z + zOffset
                            );
                            uniqueCarvePositions.Add(neighborPos);
                        }
                    }
                }
            }
        }

        return uniqueCarvePositions.ToList();
    }

    private List<IslandTile> TraceRiverPath(IslandTile source, IslandGenerator generator, RandomNumberGenerator rng)
    {
        var path = new List<IslandTile> { source };
        var visited = new HashSet<Vector3I> { source.GridPosition };

        IslandTile current = source;
        Vector2I lastDirection = Vector2I.Zero;

        int maxSteps = (int)generator.IslandRadius * 3;

        for (int step = 0; step < maxSteps; step++)
        {
            // Coastline detection rule: less than 4 neighbors means we hit the map ocean boundary
            if (current.NeighbouringTiles == null || current.NeighbouringTiles.Length < 4)
            {
                GD.Print($"[RiverGenerator] Coastline reached at {current.GridPosition} after {step} steps.");
                break;
            }

            IslandTile next = PickNextTile(current, visited, lastDirection, rng);
            if (next == null)
            {
                // Force a lenient fallback step to prevent early locking
                next = current.NeighbouringTiles.FirstOrDefault(n => !visited.Contains(n.GridPosition));
                if (next == null) break; 
            }

            lastDirection = new Vector2I(
                Mathf.Sign(next.GridPosition.X - current.GridPosition.X),
                Mathf.Sign(next.GridPosition.Z - current.GridPosition.Z));

            path.Add(next);
            visited.Add(next.GridPosition);
            current = next;
        }

        return path;
    }

    private IslandTile PickNextTile(IslandTile current, HashSet<Vector3I> visited, Vector2I lastDirection, RandomNumberGenerator rng)
    {
        var candidates = new List<IslandTile>();
        var weights = new List<float>();
        float totalWeight = 0f;

        float currentDistSq = new Vector2(current.GridPosition.X, current.GridPosition.Z).LengthSquared();

        foreach (IslandTile neighbor in current.NeighbouringTiles)
        {   
            if (visited.Contains(neighbor.GridPosition)) 
                continue;

            Vector2I dir = new Vector2I(
                neighbor.GridPosition.X - current.GridPosition.X,
                neighbor.GridPosition.Z - current.GridPosition.Z
            );

            if (lastDirection != Vector2I.Zero && (dir.X == -lastDirection.X && dir.Y == -lastDirection.Y))
                continue;

            float weight = 0.1f; // Base random wiggle weight

            // 1. Momentum Check
            if (lastDirection != Vector2I.Zero && dir == lastDirection)
            {
                weight += MomentumBias; 
            }

            // 2. Outward Push Logic (Forces tiles moving away from center to win out)
            float neighborDistSq = new Vector2(neighbor.GridPosition.X, neighbor.GridPosition.Z).LengthSquared();
            if (neighborDistSq > currentDistSq)
            {
                weight += OutwardBias;
            }

            candidates.Add(neighbor);
            weights.Add(weight);
            totalWeight += weight;
        }

        if (candidates.Count == 0)
            return null;

        float roll = rng.RandfRange(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
                return candidates[i];
        }

        return candidates[^1];
    }
}