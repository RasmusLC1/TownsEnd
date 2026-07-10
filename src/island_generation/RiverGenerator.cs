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
    [Export] public int SpawnCount { get; set; } = 3; // 3-4 distinct rivers usually look best!

    public List<Vector3I> GetRiverCarvePath(IslandGenerator generator, RandomNumberGenerator rng)
    {
        GD.Print("[RiverGenerator] Running multi-river pathway distribution...");

        var surfaceTiles = generator.GetAllSurfaceTiles().Where(t => !t.IsOccupied).ToList();
        if (!surfaceTiles.Any()) return new List<Vector3I>();

        var candidateSources = FindSourceTile(surfaceTiles);
        
        HashSet<Vector3I> uniqueCarvePositions = new();
        HashSet<Vector3I> globalSharedVisited = new();

        int activeStreams = Mathf.Min(SpawnCount, candidateSources.Count);
        int successfulRivers = 0;

        for (int i = 0; i < candidateSources.Count; i++)
        {
            if (successfulRivers >= activeStreams) break;

            IslandTile source = candidateSources[i];
            if (globalSharedVisited.Contains(source.GridPosition)) continue;

            // Trace the single backbone line
            List<IslandTile> tilePath = TraceRiverPath(source, generator, globalSharedVisited, rng);
            if (tilePath.Count < 5) continue; 

            successfulRivers++;

            // Expand the core line into a wider river channel
            ApplyRiverWidthExpansion(tilePath, uniqueCarvePositions, globalSharedVisited);
        }

        return uniqueCarvePositions.ToList();
    }

    private List<IslandTile> FindSourceTile(List<IslandTile> surfaceTiles)
    {
        return surfaceTiles
            .OrderBy(tile => new Vector2(tile.GridPosition.X, tile.GridPosition.Z).LengthSquared())
            .Take(60) // Grab central group
            .ToList();
    }

    private void ApplyRiverWidthExpansion(List<IslandTile> tilePath, HashSet<Vector3I> uniqueCarvePositions, HashSet<Vector3I> globalSharedVisited)
    {
        int radius = BaseRiverWidth - 1;

        foreach (var tile in tilePath)
        {
            var brushPositions = GetCircularBrushPositions(tile.GridPosition, radius);
            
            foreach (Vector3I pos in brushPositions)
            {
                uniqueCarvePositions.Add(pos);
                globalSharedVisited.Add(pos);
            }
        }
    }

    private List<Vector3I> GetCircularBrushPositions(Vector3I centerPos, int radius)
    {
        var positions = new List<Vector3I>();

        for (int xOffset = -radius; xOffset <= radius; xOffset++)
        {
            for (int zOffset = -radius; zOffset <= radius; zOffset++)
            {
                // Validate if coordinate lands inside the circular radius
                if ((xOffset * xOffset) + (zOffset * zOffset) <= radius * radius)
                {
                    positions.Add(new Vector3I(
                        centerPos.X + xOffset,
                        centerPos.Y, 
                        centerPos.Z + zOffset
                    ));
                }
            }
        }

        return positions;
    }

    private List<IslandTile> TraceRiverPath(IslandTile source, IslandGenerator generator, HashSet<Vector3I> globalSharedVisited, RandomNumberGenerator rng)
    {
        var path = new List<IslandTile> { source };
        
        // Local path tracking so a single river doesn't cross its own tail
        var localVisited = new HashSet<Vector3I> { source.GridPosition };

        IslandTile current = source;
        Vector2I lastDirection = Vector2I.Zero;

        int maxSteps = (int)generator.IslandRadius * 3;

        for (int step = 0; step < maxSteps; step++)
        {
            // Coastline detection rule
            if (current.NeighbouringTiles == null || current.NeighbouringTiles.Length < 4)
            {
                GD.Print($"[RiverGenerator] Coastline reached at {current.GridPosition} after {step} steps.");
                break; 
            }

            // FIX: Pass both localVisited and globalSharedVisited into the tile selector
            IslandTile next = PickNextTile(current, localVisited, globalSharedVisited, lastDirection, rng);
            
            if (next == null)
            {
                // Fallback: If completely blocked by other rivers, allow a temporary breakthrough 
                // to reach the coast instead of leaving an awkward dead-end inland pool
                next = current.NeighbouringTiles.FirstOrDefault(n => !localVisited.Contains(n.GridPosition));
                if (next == null) break; 
            }

            lastDirection = new Vector2I(
                Mathf.Sign(next.GridPosition.X - current.GridPosition.X),
                Mathf.Sign(next.GridPosition.Z - current.GridPosition.Z));

            path.Add(next);
            localVisited.Add(next.GridPosition);
            current = next;
        }

        return path;
    }
    private IslandTile PickNextTile(IslandTile current, HashSet<Vector3I> localVisited, HashSet<Vector3I> globalSharedVisited, Vector2I lastDirection, RandomNumberGenerator rng)
    {
        var candidates = new List<IslandTile>();
        var weights = new List<float>();
        float totalWeight = 0f;

        foreach (IslandTile neighbor in current.NeighbouringTiles)
        {   
            // Fix: Use the correct parameter names to check both self-intersection and other rivers
            if (localVisited.Contains(neighbor.GridPosition) || globalSharedVisited.Contains(neighbor.GridPosition)) 
                continue;

            if (IsUTurn(current.GridPosition, neighbor.GridPosition, lastDirection))
                continue;

            float weight = CalculateTileWeight(current, neighbor, lastDirection);

            candidates.Add(neighbor);
            weights.Add(weight);
            totalWeight += weight;
        }

        if (candidates.Count == 0)
            return null;

        return SelectWeightedCandidate(candidates, weights, totalWeight, rng);
    }
    
    private float CalculateTileWeight(IslandTile current, IslandTile neighbor, Vector2I lastDirection)
    {
        float weight = 0.1f; // Base random wiggle weight

        Vector2I dir = new Vector2I(
            neighbor.GridPosition.X - current.GridPosition.X,
            neighbor.GridPosition.Z - current.GridPosition.Z
        );

        // 1. Momentum Check
        if (lastDirection != Vector2I.Zero && dir == lastDirection)
        {
            weight += MomentumBias; 
        }

        // 2. Outward Push Logic (Forces tiles moving away from center to win out)
        float currentDistSq = new Vector2(current.GridPosition.X, current.GridPosition.Z).LengthSquared();
        float neighborDistSq = new Vector2(neighbor.GridPosition.X, neighbor.GridPosition.Z).LengthSquared();
        
        if (neighborDistSq > currentDistSq)
        {
            weight += OutwardBias;
        }

        return weight;
    }

    private bool IsUTurn(Vector3I currentPos, Vector3I neighborPos, Vector2I lastDirection)
    {
        if (lastDirection == Vector2I.Zero) return false;

        Vector2I dir = new Vector2I(
            neighborPos.X - currentPos.X,
            neighborPos.Z - currentPos.Z
        );

        return dir.X == -lastDirection.X && dir.Y == -lastDirection.Y;
    }

    private IslandTile SelectWeightedCandidate(List<IslandTile> candidates, List<float> weights, float totalWeight, RandomNumberGenerator rng)
    {
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