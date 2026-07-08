using Godot;
using System;

[Tool]
public partial class IslandGenerator : GridMap
{
    [Export] public int MapWidth { get; set; } = 60;
    [Export] public int MapDepth { get; set; } = 60;
    [Export] public int MaxHeight { get; set; } = 10;

    [Export] public float NoiseFrequency { get; set; } = 0.05f;
    [Export] public int NoiseSeed { get; set; } = 0;

    // Mesh library IDs (Verify these in your MeshLibrary tab)
    private const int TileGrass = 41;
    private const int TileSand = 83;
    private const int TileStone = 85;

    private FastNoiseLite _noise;

    public override void _Ready()
    {
        Clear(); // Clear manual tiles
        GenerateIsland();
    }

    public void GenerateIsland()
    {
        // Print a basic initialization message
        GD.Print("Initializing Island Generation...");

        _noise = new FastNoiseLite();
        _noise.Seed = NoiseSeed != 0 ? NoiseSeed : (int)GD.Randi();
        _noise.Frequency = NoiseFrequency;
        _noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;

        GD.Print($"Using Noise Seed: {_noise.Seed}");

        float centerX = MapWidth / 2.0f;    
        float centerZ = MapDepth / 2.0f;
        float maxDistance = new Vector2(centerX, centerZ).Length();

        int totalTilesPlaced = 0; // Track tile count

        for (int x = 0; x < MapWidth; x++)
        {
            for (int z = 0; z < MapDepth; z++)
            {
                float noiseVal = _noise.GetNoise2D(x, z);
                noiseVal = (noiseVal + 1.0f) / 2.0f;

                float distFromCenter = new Vector2(x - centerX, z - centerZ).Length();
                float mask = 1.0f - (distFromCenter / maxDistance);
                mask = MathF.Max(0.0f, MathF.Min(mask, 1.0f));

                float finalHeightFactor = noiseVal * (mask * mask);
                int calculatedHeight = (int)(finalHeightFactor * MaxHeight);

                if (calculatedHeight <= 0)
                    continue;

                for (int y = 0; y < calculatedHeight; y++)
                {
                    int tileToPlace = TileGrass;

                    if (y == calculatedHeight - 1)
                    {
                        tileToPlace = (y <= 1) ? TileSand : TileGrass;
                    }
                    else
                    {
                        tileToPlace = TileStone;
                    }

                    int centeredX = x - (MapWidth / 2);
                    int centeredZ = z - (MapDepth / 2);

                    SetCellItem(new Vector3I(centeredX, y, centeredZ), tileToPlace);
                    totalTilesPlaced++;
                }
            }
        }

        // Print the completion state so you know the loop successfully finished
        GD.Print($"Generation Complete! Placed {totalTilesPlaced} tiles across the grid.");
    }
}