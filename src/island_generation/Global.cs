using Godot;
using System.Collections.Generic;

public static class Global
{
    public static readonly Vector3I DIMENSION = new Vector3I(16, 16, 16);
    public static readonly Vector2 TEXTURE_ATLAS_SIZE = new Vector2(3, 2);

    public enum Face
    {
        Top, Bottom, Left, Right, Front, Back
    }

    public class BlockInfo
    {
        public bool IsSolid { get; set; } = false;
        public Dictionary<Face, Vector2> FaceTextures { get; set; } = new();
    }

    // Backing field for lazy loading
    private static Dictionary<TileType, BlockInfo> _types;

    // Bulletproof property: Rebuilds the dictionary automatically if Godot's assembly reload wipes it
    public static IReadOnlyDictionary<TileType, BlockInfo> Types
    {
        get
        {
            if (_types == null)
            {
                InitializeBlockTypes();
            }
            return _types;
        }
    }

    private static void InitializeBlockTypes()
    {
        _types = new Dictionary<TileType, BlockInfo>
        {
            { TileType.Air, new BlockInfo { IsSolid = false } },

            {
                TileType.Grass, new BlockInfo
                {
                    IsSolid = true,
                    FaceTextures = new()
                    {
                        { Face.Top,    new Vector2(0, 0) },
                        { Face.Bottom, new Vector2(2, 0) },
                        { Face.Left,   new Vector2(1, 0) },
                        { Face.Right,  new Vector2(1, 0) },
                        { Face.Front,  new Vector2(1, 0) },
                        { Face.Back,   new Vector2(1, 0) }
                    }
                }
            },
            {
                TileType.Sand, new BlockInfo
                {
                    IsSolid = true,
                    FaceTextures = AllFaces(new Vector2(0, 1))
                }
            },
            {
                TileType.Stone, new BlockInfo
                {
                    IsSolid = true,
                    FaceTextures = AllFaces(new Vector2(2, 0))
                }
            },
            {
                TileType.RockUnderground, new BlockInfo
                {
                    IsSolid = true,
                    FaceTextures = AllFaces(new Vector2(1, 1))
                }
            },
            {
                TileType.Snow, new BlockInfo
                {
                    IsSolid = true,
                    FaceTextures = AllFaces(new Vector2(2, 1))
                }
            },
        };
    }

    private static Dictionary<Face, Vector2> AllFaces(Vector2 atlasCell)
    {
        return new Dictionary<Face, Vector2>
        {
            { Face.Top, atlasCell }, { Face.Bottom, atlasCell },
            { Face.Left, atlasCell }, { Face.Right, atlasCell },
            { Face.Front, atlasCell }, { Face.Back, atlasCell },
        };
    }
}