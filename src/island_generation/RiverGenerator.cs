using Godot;
using System;

[Tool]
public partial class RiverGenerator : Node
{
    private const int TileRiver = 304;

    public void GenerateRiver(IslandGenerator generator, RandomNumberGenerator rng)
    {
        GD.Print("[RiverGenerator] Carving raised river pathway...");

        float randomAngle = rng.Randf() * MathF.PI * 2.0f;
        Vector2 direction = new Vector2(MathF.Cos(randomAngle), MathF.Sin(randomAngle)).Normalized();

        Vector2 currentStepPos = Vector2.Zero;
        bool reachedOcean = false;

        int maxRiverLength = (int)generator.IslandRadius + 20; 
        int stepsTaken = 0;

        while (!reachedOcean && stepsTaken < maxRiverLength)
		{
			int gridX = Mathf.RoundToInt(currentStepPos.X);
			int gridZ = Mathf.RoundToInt(currentStepPos.Y);

			int surfaceY = generator.GetSurfaceYAt(gridX, gridZ);

			if (surfaceY == -1)
			{
				if (stepsTaken > 3)
				{
					reachedOcean = true;
					break;
				}
			}
			else
			{
				// 💡 FIX: Place the river tile ONE grid unit HIGHER than the surface grass
				Vector3I riverGridPos = new Vector3I(gridX, surfaceY + 5, gridZ);
				generator.SetCellItem(riverGridPos, TileRiver);

				// Lock down the grass tile directly beneath the water surface 
				Vector3I groundGridPos = new Vector3I(gridX, surfaceY, gridZ);
				IslandTile groundTile = generator.GetTileAt(groundGridPos);
				if (groundTile != null)
				{
					groundTile.IsWalkable = false;
					groundTile.IsOccupied = true; // This prevents trees/rocks from dropping into the stream!
				}
			}

			currentStepPos += direction;
			stepsTaken++;
		}

        GD.Print($"[RiverGenerator] River successfully generated across {stepsTaken} steps.");
    }
}