using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class RiverGenerator : Node
{
    [Export] public int RiverMeshStartId = 294;

    public int TileRiverCorner      => RiverMeshStartId + 0;
    public int TileRiverCornerSmall => RiverMeshStartId + 1;
    public int TileRiverCross       => RiverMeshStartId + 2;
    public int TileRiverEnd         => RiverMeshStartId + 3;
    public int TileRiverEndClosed   => RiverMeshStartId + 4;
    public int TileRiverOpen        => RiverMeshStartId + 5;
    public int TileRiverRocks       => RiverMeshStartId + 6;
    public int TileRiverSide        => RiverMeshStartId + 7;
    public int TileRiverSideOpen    => RiverMeshStartId + 8;
    public int TileRiverSplit       => RiverMeshStartId + 9;
    public int TileRiverStraight    => RiverMeshStartId + 10;
    public int TileRiverTile        => RiverMeshStartId + 11;

    // How strongly the river favors the single steepest downhill neighbor.
    // 1.0 = always steepest (rigid, jagged). Lower values let it wander more,
    // producing natural-looking curves. Try 0.5-0.7 as a starting point.
    [Export(PropertyHint.Range, "0,1")] public float SteepnessBias = 0.65f;

    // Cardinal directions on the XZ plane. Order matters -- everything below
    // (yaw angles, corner selection) is built around this fixed indexing.
    private static readonly Vector2I[] Directions =
    {
        new Vector2I(0, -1), // 0: North
        new Vector2I(1, 0),  // 1: East
        new Vector2I(0, 1),  // 2: South
        new Vector2I(-1, 0), // 3: West
    };

    public void GenerateRiver(IslandGenerator generator, RandomNumberGenerator rng)
    {
        GD.Print("[RiverGenerator] Carving river pathway...");

        IslandTile source = generator.GetTallestUnoccupiedTile();
        if (source == null)
        {
            GD.PrintErr("[RiverGenerator] No valid source tile found.");
            return;
        }

        List<IslandTile> path = TraceDownhillPath(source, generator, rng);

        if (path.Count < 2)
        {
            GD.PrintErr("[RiverGenerator] River path too short to place -- source may be landlocked or sitting in a local pit.");
            return;
        }

        PlaceRiverTiles(path, generator);

        GD.Print($"[RiverGenerator] River generated across {path.Count} tiles.");
    }

    /// <summary>
    /// Walks downhill from the source, always moving to strictly lower or
    /// equal ground (never uphill), biased toward the steepest drop but with
    /// enough randomness and directional momentum to curve naturally instead
    /// of being a rigid straight line or a jittery zig-zag.
    /// </summary>
    private List<IslandTile> TraceDownhillPath(IslandTile source, IslandGenerator generator, RandomNumberGenerator rng)
    {
        var path = new List<IslandTile> { source };
        var visited = new HashSet<Vector3I> { source.GridPosition };

        IslandTile current = source;
        Vector2I lastDirection = Vector2I.Zero;

        int maxSteps = (int)generator.IslandRadius + 40;

        for (int step = 0; step < maxSteps; step++)
        {
            IslandTile next = PickNextTile(current, visited, lastDirection, rng);
            if (next == null)
                break; // Dead end: coastline, a local pit, or no unvisited downhill tiles left.

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
        // Only ever flow downhill or across flat ground -- this is the fix
        // for the "flows uphill" bug: compare against the CURRENT tile,
        // not just against other candidates.
        var candidates = new List<IslandTile>();
        foreach (IslandTile neighbor in current.NeighbouringTiles)
        {
            if (visited.Contains(neighbor.GridPosition))
                continue;
            if (neighbor.GridPosition.Y <= current.GridPosition.Y)
                candidates.Add(neighbor);
        }

        if (candidates.Count == 0)
            return null;

        // Weighted pick: steeper drop = more weight (flows downhill),
        // continuing the same direction as the last step = more weight
        // (reduces jaggedness), small random jitter so it isn't perfectly
        // deterministic. This combination is what produces natural curves.
        float totalWeight = 0f;
        var weights = new float[candidates.Count];

        for (int i = 0; i < candidates.Count; i++)
        {
            IslandTile candidate = candidates[i];
            int drop = current.GridPosition.Y - candidate.GridPosition.Y;

            Vector2I dir = new Vector2I(
                Mathf.Sign(candidate.GridPosition.X - current.GridPosition.X),
                Mathf.Sign(candidate.GridPosition.Z - current.GridPosition.Z));

            float directionBonus = (lastDirection != Vector2I.Zero && dir == lastDirection) ? 1.0f : 0.0f;

            float weight = (drop * SteepnessBias) + (directionBonus * (1.0f - SteepnessBias)) + 0.05f;
            weights[i] = Mathf.Max(weight, 0.01f); // keep every candidate reachable, never fully zero

            totalWeight += weights[i];
        }

        float roll = rng.RandfRange(0f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
                return candidates[i];
        }

        return candidates[candidates.Count - 1];
    }

    /// <summary>
    /// Second pass over the finished path: for each tile, works out which
    /// directions it connects to (previous/next tile) and places the
    /// matching river piece -- end, straight, or corner -- at the right
    /// rotation, instead of stamping the same "open" tile everywhere.
    /// Replaces the existing ground tile in-place, so the river sits AT
    /// terrain height rather than floating above it on an offset.
    /// </summary>
    private void PlaceRiverTiles(List<IslandTile> path, IslandGenerator generator)
    {
        for (int i = 0; i < path.Count; i++)
        {
            IslandTile tile = path[i];

            var connections = new List<int>();
            if (i > 0) connections.Add(DirectionIndexTo(path[i - 1].GridPosition, tile.GridPosition));
            if (i < path.Count - 1) connections.Add(DirectionIndexTo(path[i + 1].GridPosition, tile.GridPosition));

            (int meshId, float yawDegrees) = ResolveRiverPiece(connections);

            generator.SetCellItem(tile.GridPosition, meshId, YawToOrientation(yawDegrees, generator));

            tile.IsWalkable = false;
            tile.IsOccupied = true;
        }
    }

    /// <summary> Direction index (matching Directions[]) pointing FROM `to`'s position TOWARD `from`'s position. </summary>
    private int DirectionIndexTo(Vector3I from, Vector3I to)
    {
        Vector2I delta = new Vector2I(from.X - to.X, from.Z - to.Z);
        for (int i = 0; i < Directions.Length; i++)
        {
            if (Directions[i] == delta)
                return i;
        }
        return 0; // Shouldn't happen for orthogonally-adjacent path tiles.
    }

    /// <summary>
    /// Given 1 or 2 connection directions, picks the matching mesh (end,
    /// straight, or corner) and the yaw needed to orient it.
    /// IMPORTANT: the yaw values below assume a specific default facing for
    /// each source mesh (e.g. the corner piece modeled as connecting
    /// North+East at 0 degrees). I can't verify that against your actual
    /// assets -- if pieces come out rotated wrong in the editor, it's a
    /// fixed offset issue: add/subtract a constant (usually a multiple of
    /// 90) to the returned yaw until they line up.
    /// </summary>
    private (int meshId, float yawDegrees) ResolveRiverPiece(List<int> connections)
    {
        if (connections.Count == 1)
        {
            // A single connection = the source spring or the river's mouth.
            return (TileRiverEnd, DirectionIndexToYaw(connections[0]));
        }

        int a = Mathf.Min(connections[0], connections[1]);
        int b = Mathf.Max(connections[0], connections[1]);

        // Opposite directions = straight segment.
        if (a == 0 && b == 2) return (TileRiverStraight, 0f);   // North-South
        if (a == 1 && b == 3) return (TileRiverStraight, 90f);  // East-West

        // Adjacent directions = corner. Each remaining pair is exactly one
        // of the 4 possible corners.
        if (a == 0 && b == 1) return (TileRiverCorner, 0f);    // North-East
        if (a == 1 && b == 2) return (TileRiverCorner, 90f);   // East-South
        if (a == 2 && b == 3) return (TileRiverCorner, 180f);  // South-West
        if (a == 0 && b == 3) return (TileRiverCorner, 270f);  // North-West

        // Shouldn't be reachable, but fail soft instead of crashing.
        return (TileRiverOpen, 0f);
    }

    private float DirectionIndexToYaw(int directionIndex) => directionIndex * 90f;

    /// <summary>
    /// Converts a yaw in degrees to one of GridMap's 24 valid orthogonal
    /// cell orientations, so the mesh's pre-baked rotation is used correctly
    /// instead of every piece defaulting to identity rotation.
    /// </summary>
    private int YawToOrientation(float yawDegrees, GridMap gridMap)
    {
        Basis basis = new Basis(Vector3.Up, Mathf.DegToRad(yawDegrees));
        return gridMap.GetOrthogonalIndexFromBasis(basis);
    }
}