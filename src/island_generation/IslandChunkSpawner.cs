using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Converts the island's global tile data (from IslandGenerator's
/// _tileData) into per-chunk TileType[,,] arrays and spawns/updates one
/// Chunk StaticBody3D per chunk. IslandGenerator still owns terrain shape,
/// coastline, and rivers entirely as before -- this class only slices the
/// finished result into chunk-sized buckets for rendering.
///
/// ASSUMPTION: island height (MaxHeight) must fit within Global.DIMENSION.Y
/// (16) since this only spawns a single vertical layer of chunks. If you
/// raise MaxHeight above ~15, you'll need to stack chunks vertically too --
/// flag it if you get there and we can extend this.
/// </summary>
public partial class IslandChunkSpawner : RefCounted
{
    private readonly Node3D _parent;
    private readonly Dictionary<Vector2I, Chunk> _chunks = new();
    private readonly HashSet<Vector2I> _dirtyChunks = new();

    public IslandChunkSpawner(Node3D parent)
    {
        _parent = parent;
    }

    private static Vector2I ChunkCoordOf(Vector3I tilePos)
    {
        int cx = (int)MathF.Floor((float)tilePos.X / Global.DIMENSION.X);
        int cz = (int)MathF.Floor((float)tilePos.Z / Global.DIMENSION.Z);
        return new Vector2I(cx, cz);
    }

    /// <summary> Full rebuild -- call once after generation finishes. </summary>
    public void BuildAll(Dictionary<Vector3I, IslandTile> tileData)
    {
        ClearAll();

        var grouped = tileData.Keys.GroupBy(ChunkCoordOf);
        foreach (var group in grouped)
        {
            RebuildChunk(group.Key, tileData);
        }

        GD.Print($"[IslandChunkSpawner] Spawned {_chunks.Count} chunks.");
    }

    /// <summary> Marks the owning chunk (and boundary neighbors) dirty for a changed tile. </summary>
    public void MarkDirty(Vector3I tilePos)
    {
        Vector2I owner = ChunkCoordOf(tilePos);
        _dirtyChunks.Add(owner);

        int localX = tilePos.X - owner.X * Global.DIMENSION.X;
        int localZ = tilePos.Z - owner.Y * Global.DIMENSION.Z;

        if (localX == 0) _dirtyChunks.Add(owner + new Vector2I(-1, 0));
        if (localX == Global.DIMENSION.X - 1) _dirtyChunks.Add(owner + new Vector2I(1, 0));
        if (localZ == 0) _dirtyChunks.Add(owner + new Vector2I(0, -1));
        if (localZ == Global.DIMENSION.Z - 1) _dirtyChunks.Add(owner + new Vector2I(0, 1));
    }

    /// <summary> Rebuilds every chunk marked dirty since the last call. Call once per batch, not per-tile. </summary>
    public void RebuildDirty(Dictionary<Vector3I, IslandTile> tileData)
    {
        if (_dirtyChunks.Count == 0)
            return;

        foreach (Vector2I coord in _dirtyChunks)
            RebuildChunk(coord, tileData);

        GD.Print($"[IslandChunkSpawner] Rebuilt {_dirtyChunks.Count} dirty chunk(s).");
        _dirtyChunks.Clear();
    }

    private void RebuildChunk(Vector2I coord, Dictionary<Vector3I, IslandTile> tileData)
    {
        var blocks = new TileType[Global.DIMENSION.X, Global.DIMENSION.Y, Global.DIMENSION.Z];
        // Defaults to TileType.Air (enum value 0) automatically -- no need to fill explicitly.

        bool anySolid = false;
        int baseX = coord.X * Global.DIMENSION.X;
        int baseZ = coord.Y * Global.DIMENSION.Z;

        for (int lx = 0; lx < Global.DIMENSION.X; lx++)
        {
            for (int lz = 0; lz < Global.DIMENSION.Z; lz++)
            {
                for (int ly = 0; ly < Global.DIMENSION.Y; ly++)
                {
                    var worldPos = new Vector3I(baseX + lx, ly, baseZ + lz);
                    if (tileData.TryGetValue(worldPos, out IslandTile tile))
                    {
                        blocks[lx, ly, lz] = tile.Type;
                        anySolid = true;
                    }
                }
            }
        }

        if (!anySolid)
        {
            // Chunk is now fully empty (e.g. carved out by a river) -- remove it entirely.
            if (_chunks.TryGetValue(coord, out Chunk emptyChunk))
            {
                emptyChunk.QueueFree();
                _chunks.Remove(coord);
            }
            return;
        }

        if (!_chunks.TryGetValue(coord, out Chunk chunk))
        {
            chunk = new Chunk { Name = $"Chunk_{coord.X}_{coord.Y}" };
            _parent.AddChild(chunk);
            // Loads island in editor
            if (Engine.IsEditorHint())
                chunk.Owner = _parent.GetTree().EditedSceneRoot;
            chunk.ChunkPosition = new Vector2(coord.X, coord.Y);
            _chunks[coord] = chunk;
        }

        chunk.SetBlocks(blocks);
    }

    public void ClearAll()
    {
        foreach (var chunk in _chunks.Values)
            chunk.QueueFree();

        _chunks.Clear();
        _dirtyChunks.Clear();
    }
}