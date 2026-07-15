using Godot;
using System.Collections.Generic;

/// <summary>
/// Pure renderer + collider for one chunk's worth of voxel data. Does NOT
/// generate its own terrain -- IslandGenerator computes the full island
/// (noise, radial falloff, BFS coastline, rivers) globally as before, then
/// IslandChunkSpawner slices that result into per-chunk TileType[,,] arrays
/// and hands them to SetBlocks(). This chunk never needs to know about
/// islands, noise, or coastlines -- it only knows how to mesh a 3D array.
/// </summary>
[Tool]
public partial class Chunk : StaticBody3D
{
    public static readonly Vector3I[] vertices = new Vector3I[]
    {
        new Vector3I(0, 0, 0), new Vector3I(1, 0, 0),
        new Vector3I(0, 1, 0), new Vector3I(1, 1, 0),
        new Vector3I(0, 0, 1), new Vector3I(1, 0, 1),
        new Vector3I(0, 1, 1), new Vector3I(1, 1, 1),
    };

    public static readonly int[] TOP = { 2, 3, 7, 6 };
    public static readonly int[] BOTTOM = { 0, 4, 5, 1 };
    public static readonly int[] LEFT = { 6, 4, 0, 2 };
    public static readonly int[] RIGHT = { 3, 1, 5, 7 };
    public static readonly int[] FRONT = { 7, 5, 4, 6 };
    public static readonly int[] BACK = { 2, 0, 1, 3 };

    private TileType[,,] _blocks;
    private readonly SurfaceTool _st = new();

    private ArrayMesh _mesh;
    private MeshInstance3D _meshInstance;
    private readonly StandardMaterial3D _material =
        GD.Load<StandardMaterial3D>("res://terrain/voxel/new_spatialmaterial.tres");

    private Vector2 _chunkPosition = Vector2.Zero;
    public Vector2 ChunkPosition
    {
        get => _chunkPosition;
        set => SetChunkPosition(value);
    }

    public override void _Ready()
    {
        if (_material != null)
            _material.TextureFilter = BaseMaterial3D.TextureFilterEnum.NearestWithMipmaps;
    }

    /// <summary>
    /// Supplies this chunk's voxel data and immediately rebuilds its mesh +
    /// collision. Call this instead of the old Generate() -- terrain
    /// generation lives in IslandGenerator/IslandChunkSpawner now.
    /// blocks must be sized exactly Global.DIMENSION (x, y, z).
    /// </summary>
    public void SetBlocks(TileType[,,] blocks)
    {
        _blocks = blocks;
        Update();
    }

    public void Update()
    {
        if (_blocks == null)
            return; // no data yet -- nothing to build

        if (_meshInstance != null)
        {
            _meshInstance.CallDeferred(Node.MethodName.QueueFree);
            _meshInstance = null;
        }

        _mesh = new ArrayMesh();
        _meshInstance = new MeshInstance3D();
        _st.Begin(Mesh.PrimitiveType.Triangles);

        Vector3I dimension = Global.DIMENSION;
        for (int x = 0; x < dimension.X; x++)
            for (int y = 0; y < dimension.Y; y++)
                for (int z = 0; z < dimension.Z; z++)
                    CreateBlock(x, y, z);

        _st.GenerateNormals(false);
        _st.SetMaterial(_material);
        _st.Commit(_mesh);
        _meshInstance.Mesh = _mesh;

        AddChild(_meshInstance);

        // Skip collision baking entirely for an empty chunk (e.g. all-air,
        // fully carved by a river) -- CreateTrimeshCollision on zero
        // triangles is wasted work and can warn in the console.
        if (_mesh.GetSurfaceCount() > 0)
            _meshInstance.CreateTrimeshCollision();

        Visible = true;
    }

    public void CreateBlock(int x, int y, int z)
    {
        TileType block = _blocks[x, y, z];
        if (block == TileType.Air)
            return;

        Global.BlockInfo blockInfo = Global.Types[block];

        if (CheckTransparent(x, y + 1, z)) CreateFace(TOP, x, y, z, blockInfo.FaceTextures[Global.Face.Top]);
        if (CheckTransparent(x, y - 1, z)) CreateFace(BOTTOM, x, y, z, blockInfo.FaceTextures[Global.Face.Bottom]);
        if (CheckTransparent(x - 1, y, z)) CreateFace(LEFT, x, y, z, blockInfo.FaceTextures[Global.Face.Left]);
        if (CheckTransparent(x + 1, y, z)) CreateFace(RIGHT, x, y, z, blockInfo.FaceTextures[Global.Face.Right]);
        if (CheckTransparent(x, y, z - 1)) CreateFace(BACK, x, y, z, blockInfo.FaceTextures[Global.Face.Back]);
        if (CheckTransparent(x, y, z + 1)) CreateFace(FRONT, x, y, z, blockInfo.FaceTextures[Global.Face.Front]);
    }

    public void CreateFace(int[] indices, int x, int y, int z, Vector2 textureAtlasOffset)
    {
        Vector3 offset = new Vector3(x, y, z);

        Vector3 a = (Vector3)vertices[indices[0]] + offset;
        Vector3 b = (Vector3)vertices[indices[1]] + offset;
        Vector3 c = (Vector3)vertices[indices[2]] + offset;
        Vector3 d = (Vector3)vertices[indices[3]] + offset;

        Vector2 uvOffset = textureAtlasOffset / Global.TEXTURE_ATLAS_SIZE;
        float height = 1.0f / Global.TEXTURE_ATLAS_SIZE.Y;
        float width = 1.0f / Global.TEXTURE_ATLAS_SIZE.X;

        Vector2 uvA = uvOffset + new Vector2(0, 0);
        Vector2 uvB = uvOffset + new Vector2(0, height);
        Vector2 uvC = uvOffset + new Vector2(width, height);
        Vector2 uvD = uvOffset + new Vector2(width, 0);

        _st.AddTriangleFan(new Vector3[] { a, b, c }, new Vector2[] { uvA, uvB, uvC });
        _st.AddTriangleFan(new Vector3[] { a, c, d }, new Vector2[] { uvA, uvC, uvD });
    }

    /// <summary>
    /// NOTE: only checks this chunk's own local array. A block sitting
    /// exactly on a chunk boundary can't see into the neighbor chunk, so it
    /// always treats its outward-facing edge as transparent and draws that
    /// face -- even when the neighbor chunk has a solid block there. This
    /// produces a thin strip of harmless-but-unnecessary geometry at every
    /// chunk seam. Fine to ignore for now; fixable later by having
    /// IslandChunkSpawner pass each chunk a 1-cell border read from its
    /// neighbors if it's ever worth the complexity.
    /// </summary>
    private bool CheckTransparent(int x, int y, int z)
    {
        if (x >= 0 && x < Global.DIMENSION.X &&
            y >= 0 && y < Global.DIMENSION.Y &&
            z >= 0 && z < Global.DIMENSION.Z)
        {
            TileType block = _blocks[x, y, z];
            Global.BlockInfo blockInfo = Global.Types[block];
            return !blockInfo.IsSolid;
        }

        return true;
    }

    private void SetChunkPosition(Vector2 pos)
    {
        _chunkPosition = pos;
        Position = new Vector3(pos.X, 0, pos.Y) * Global.DIMENSION;
        Visible = false; // hidden until SetBlocks()/Update() populates it
    }
}