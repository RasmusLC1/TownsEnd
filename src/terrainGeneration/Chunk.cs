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
	List<object> blocks = new(); 

	SurfaceTool _st = new();

	MeshInstance3D meshInstance = null;
    private ArrayMesh _mesh = null;
    private MeshInstance3D _meshInstance = null;
	[Export] private Material _material;

	public override void _Ready()
	{
		Update();
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
        Vector3I dimension = Global.Instance.DIMENSION; 

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

        Visible = true;
    }

	public void CreateBlock(int x, int y, int z)
	{
		CreateFace(TOP, x, y, z);
		CreateFace(BOTTOM, x, y, z);
		CreateFace(LEFT, x, y, z);
		CreateFace(RIGHT, x, y, z);
		CreateFace(FRONT, x, y, z);
		CreateFace(BACK, x, y, z);
	}


	public void CreateFace(int[] indices, int x, int y, int z)
    {
        // 1. Define the offset as a Vector3 (float) so we can build 3D mesh points
        Vector3 offset = new Vector3(x, y, z);

        // 2. Extract the 4 vertices belonging to this face and apply the position offset
        // (We cast the Vector3I from our 'vertices' array to Vector3 to match the offset)
        Vector3 a = (Vector3)vertices[indices[0]] + offset;
        Vector3 b = (Vector3)vertices[indices[1]] + offset;
        Vector3 c = (Vector3)vertices[indices[2]] + offset;
        Vector3 d = (Vector3)vertices[indices[3]] + offset;

        // 3. Temporary UV array
        Vector2[] uvs = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(1, 0)
        };

        // 4. Send the triangles to your SurfaceTool
        _st.AddTriangleFan(new Vector3[] { a, b, c }, new Vector2[] { uvs[0], uvs[1], uvs[2] });
        _st.AddTriangleFan(new Vector3[] { a, c, d }, new Vector2[] { uvs[0], uvs[2], uvs[3] });
    }

}
