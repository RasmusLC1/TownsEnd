using Godot;
using System.Collections.Generic;
using System.Security.Principal;
[Tool]
public partial class Chunk : StaticBody3D
{
	public static Vector3I[] vertices = new Vector3I[]
	{
		new Vector3I(0, 0, 0), // 1
		new Vector3I(1, 0, 0), // 2
		new Vector3I(0, 1, 0), // 3
		new Vector3I(1, 1, 0), // 4
		new Vector3I(0, 0, 1), // 5
		new Vector3I(1, 0, 1), // 6
		new Vector3I(0, 1, 1), // 7
		new Vector3I(1, 1, 1), // 8
	};

	public static int[] TOP = [2, 3, 7, 6]; // vertices 
	public static int[] BOTTOM = [0, 4, 5, 1];
	public static int[] LEFT = [6, 4, 0, 2];
	public static int[] RIGHT = [3, 1, 5, 7];
	public static int[] FRONT = [7, 5, 4, 6];
	public static int[] BACK = [2, 0, 1, 3];


	// 1. A dynamic list
	private Global.BlockType[,,] _blocks;
	SurfaceTool _st = new();

	MeshInstance3D meshInstance = null;
    private ArrayMesh _mesh = null;
    private MeshInstance3D _meshInstance = null;
	private StandardMaterial3D _material = GD.Load<StandardMaterial3D>("res://terrain/voxel/new_spatialmaterial.tres");
	public bool _visible = false;

	private Vector2 _chunkPosition = Vector2.Zero;

	public Vector2 ChunkPosition
	{
		get => _chunkPosition;
		set => SetChunkPosition(value);
	}
    // 3. Use FastNoiseLite (Godot 4's replacement for OpenSimplexNoise)
    private FastNoiseLite _noise = new();

    public override void _Ready()
    {
        // 4. Set texture filtering to Nearest (this replaces texture.set_flags(2) / MIPMAPS)
        if (_material != null)
        {
            _material.TextureFilter = BaseMaterial3D.TextureFilterEnum.NearestWithMipmaps;
        }

        Generate();
        Update();
    }
	private void Generate()
	{
		// 1. Initialize the 3D array dimensions directly matching Global.DIMENSION
		Vector3I dimension = Global.DIMENSION;
		_blocks = new Global.BlockType[dimension.X, dimension.Y, dimension.Z];

		for (int i = 0; i < dimension.X; i++)
		{
			for (int j = 0; j < dimension.Y; j++)
			{
				for (int k = 0; k < dimension.Z; k++)
				{
					SpawnRules(dimension, i, j, k);
				}
			}
		}
	}

	public void SpawnRules(Vector3I dimension, int i, int j, int k)
	{
		// 2. Calculate global block position for 2D noise generation
		Vector2 globalPos = _chunkPosition * new Vector2(dimension.X, dimension.Z) 
							+ new Vector2(i, k);

		// 3. Get noise value (normalized to 0-1 range) and scale to dimension.Y
		// Note: get_noise_2dv in Godot 4 C# uses GetNoise2Dv
		float noiseVal = (_noise.GetNoise2Dv(globalPos) + 1.0f) / 2.0f;
		int height = (int)(noiseVal * dimension.Y);

		// 4. Default block type
		Global.BlockType block = Global.BlockType.AIR;

		// 5. Build terrain layers based on block height
		if (j < height / 2)
		{
			block = Global.BlockType.STONE;
		}
		else if (j < height)
		{
			block = Global.BlockType.DIRT;
		}
		else if (j == height)
		{
			block = Global.BlockType.GRASS;
		}

		// 6. Assign the block type to the 3D coordinate
		_blocks[i, j, k] = block;
	}

	public void Update()
    {
        // 1. Unload old mesh instance
        if (_meshInstance != null)
        {
            _meshInstance.CallDeferred(Node.MethodName.QueueFree);
            _meshInstance = null;
        }

        // 2. Initialize new mesh components
        _mesh = new ArrayMesh();
        _meshInstance = new MeshInstance3D();
        _st.Begin(Mesh.PrimitiveType.Triangles);

        // 3. Loop through your 3D grid dimensions
        // Note: Assuming "Global" is an autoload singleton in your project
        Vector3I dimension = Global.DIMENSION; 

        for (int x = 0; x < dimension.X; x++)
        {
            for (int y = 0; y < dimension.Y; y++)
            {
                for (int z = 0; z < dimension.Z; z++)
                {
                    CreateBlock(x, y, z);
                }
            }
        }

        // 4. Generate mesh, apply materials, and commit
        _st.GenerateNormals(false);
        _st.SetMaterial(_material);
        _st.Commit(_mesh);
        _meshInstance.Mesh = _mesh;

        // 5. Add to scene tree and generate static collision
        AddChild(_meshInstance);
        _meshInstance.CreateTrimeshCollision();

        _visible = true;
    }

	public void CreateBlock(int x, int y, int z)
	{
		Global.BlockType block = _blocks[x, y, z];
		if (block == Global.BlockType.AIR)
		{
			return;
		}

		Global.BlockInfo blockInfo = Global.Instance.Types[block];

		// 4. Render faces only if the neighboring block coordinate is transparent
		if (CheckTransparent(x, y + 1, z))
		{
			CreateFace(TOP, x, y, z, blockInfo.FaceTextures[Global.Face.Top]);
		}

		if (CheckTransparent(x, y - 1, z))
		{
			CreateFace(BOTTOM, x, y, z, blockInfo.FaceTextures[Global.Face.Bottom]);
		}

		if (CheckTransparent(x - 1, y, z))
		{
			CreateFace(LEFT, x, y, z, blockInfo.FaceTextures[Global.Face.Left]);
		}

		if (CheckTransparent(x + 1, y, z))
		{
			CreateFace(RIGHT, x, y, z, blockInfo.FaceTextures[Global.Face.Right]);
		}

		if (CheckTransparent(x, y, z - 1))
		{
			CreateFace(BACK, x, y, z, blockInfo.FaceTextures[Global.Face.Back]);
		}

		if (CheckTransparent(x, y, z + 1))
		{
			CreateFace(FRONT, x, y, z, blockInfo.FaceTextures[Global.Face.Front]);
		}
	}


	public void CreateFace(int[] indices, int x, int y, int z, Vector2 textureAtlasOffset)
	{
		// 1. Position offset for the current voxel
		Vector3 offset = new Vector3(x, y, z);

		// 2. Fetch the 4 corner vertices for this face and apply the offset
		// (Cast the Vector3I from 'vertices' to Vector3 so we can add the float offset)
		Vector3 a = (Vector3)vertices[indices[0]] + offset;
		Vector3 b = (Vector3)vertices[indices[1]] + offset;
		Vector3 c = (Vector3)vertices[indices[2]] + offset;
		Vector3 d = (Vector3)vertices[indices[3]] + offset;

		// 3. Calculate UV offsets and sizes based on your static Global atlas dimensions
		Vector2 uvOffset = textureAtlasOffset / Global.TEXTURE_ATLAS_SIZE;
		float height = 1.0f / Global.TEXTURE_ATLAS_SIZE.Y;
		float width = 1.0f / Global.TEXTURE_ATLAS_SIZE.X;

		// 4. Map the corner coordinates of the texture slice
		Vector2 uvA = uvOffset + new Vector2(0, 0);
		Vector2 uvB = uvOffset + new Vector2(0, height);
		Vector2 uvC = uvOffset + new Vector2(width, height);
		Vector2 uvD = uvOffset + new Vector2(width, 0);

		// 5. Add the triangles to your SurfaceTool
		_st.AddTriangleFan(new Vector3[] { a, b, c }, new Vector2[] { uvA, uvB, uvC });
		_st.AddTriangleFan(new Vector3[] { a, c, d }, new Vector2[] { uvA, uvC, uvD });
	}

	private bool CheckTransparent(int x, int y, int z)
	{
		// 1. Check if the block coordinates are within the chunk's boundaries
		if (x >= 0 && x < Global.DIMENSION.X &&
			y >= 0 && y < Global.DIMENSION.Y &&
			z >= 0 && z < Global.DIMENSION.Z)
		{
			// 2. Look up the block type at this coordinate
			Global.BlockType block = _blocks[x, y, z];

			// 3. Get the block properties from the Global Types dictionary
			Global.BlockInfo blockInfo = Global.Instance.Types[block];

			// 4. Return true if the block is NOT solid (meaning it is transparent)
			return !blockInfo.IsSolid;
		}

		// 5. If the coordinate is outside this chunk, treat it as transparent 
		// so outer faces on the edge of the chunk still draw.
		return true;
	}

	private void SetChunkPosition(Vector2 pos)
	{
		_chunkPosition = pos;
		
		// In Godot 4, use "Position" instead of "translation"
		Position = new Vector3(pos.X, 0, pos.Y) * Global.DIMENSION;
		
		// Hides the chunk node until update() builds it
		Visible = false;
	}

}
