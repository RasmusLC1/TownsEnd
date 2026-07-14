using Godot;
using System.Collections.Generic;

public partial class Global : Node
{
    public static Global Instance { get; private set; }

    public static readonly Vector3I DIMENSION = new Vector3I(16, 16, 16);
    public static readonly Vector2 TEXTURE_ATLAS_SIZE = new Vector2(3, 2);

    // Enums for Face directions
    public enum Face
    {
        Top,
        Bottom,
        Left,
        Right,
        Front,
        Back,
        Solid
    }

    // Enums for Block Types
    public enum BlockType
    {
        AIR,
        DIRT,
        GRASS,
        STONE
    }

    // Class to represent block configuration data
    public class BlockInfo
    {
        public bool IsSolid { get; set; } = false;
        // Map each face to its corresponding texture offset coordinate in the atlas
        public Dictionary<Face, Vector2> FaceTextures { get; set; } = new();
    }

    // Our C# equivalent to the GDScript "types" dictionary
    public Dictionary<BlockType, BlockInfo> Types { get; private set; }

    public override void _EnterTree()
    {
        Instance = this;
        InitializeBlockTypes();
    }

    private void InitializeBlockTypes()
    {
        Types = new Dictionary<BlockType, BlockInfo>
        {
            {
                BlockType.AIR, new BlockInfo { IsSolid = false }
            },
            {
                BlockType.DIRT, new BlockInfo 
                { 
                    IsSolid = true,
                    FaceTextures = new()
                    {
                        { Face.Top,    new Vector2(2, 0) },
                        { Face.Bottom, new Vector2(2, 0) },
                        { Face.Left,   new Vector2(2, 0) },
                        { Face.Right,  new Vector2(2, 0) },
                        { Face.Front,  new Vector2(2, 0) },
                        { Face.Back,   new Vector2(2, 0) }
                    }
                }
            },
            {
                BlockType.GRASS, new BlockInfo 
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
                BlockType.STONE, new BlockInfo 
                { 
                    IsSolid = true,
                    FaceTextures = new()
                    {
                        { Face.Top,    new Vector2(0, 1) },
                        { Face.Bottom, new Vector2(0, 1) },
                        { Face.Left,   new Vector2(0, 1) },
                        { Face.Right,  new Vector2(0, 1) },
                        { Face.Front,  new Vector2(0, 1) },
                        { Face.Back,   new Vector2(0, 1) }
                    }
                }
            }
        };
    }
}