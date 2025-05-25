// File: MovementRangeCalculator.cs
using UnityEngine;
using System.Collections.Generic;

public class MovementRangeCalculator
{
    private HexGrid gridManager;
    // No HexPathfinder needed if we're just doing a BFS for reachable tiles within AP cost

    public MovementRangeCalculator(HexGrid grid)
    {
        if (grid == null)
        {
            Debug.LogError("MovementRangeCalculator: HexGrid dependency is null!");
            // Potentially throw an error or handle this state
            return;
        }
        this.gridManager = grid;
    }

    /// <summary>
    /// Calculates all reachable tiles within the given AP budget from a start coordinate.
    /// </summary>
    /// <param name="startCoord">The starting coordinate of the unit.</param>
    /// <param name="maxApCost">The maximum AP the unit can spend on movement.</param>
    /// <param name="unitToExclude">The unit performing the move (to allow pathing through its own start tile).</param>
    /// <returns>A HashSet of Vector2Int coordinates representing reachable tiles (excluding the start tile itself).</returns>
    public HashSet<Vector2Int> GetReachableTiles(Vector2Int startCoord, int maxApCost, Unit unitToExclude)
    {
        HashSet<Vector2Int> reachableTiles = new HashSet<Vector2Int>();
        if (gridManager == null || maxApCost <= 0)
        {
            return reachableTiles; // No grid or no AP to move
        }

        Dictionary<Vector2Int, int> costToReachTile = new Dictionary<Vector2Int, int>();
        Queue<(Vector2Int coord, int costSoFar)> frontier = new Queue<(Vector2Int coord, int costSoFar)>();

        costToReachTile[startCoord] = 0;
        frontier.Enqueue((startCoord, 0));

        while (frontier.Count > 0)
        {
            var (currentCoord, currentCost) = frontier.Dequeue();

            HexTile currentHexTile = gridManager.GetTileAt(currentCoord);
            if (currentHexTile == null) continue; // Should not happen if coords are valid

            // Add to reachable if it's not the start tile and within budget
            if (currentCoord != startCoord && currentCost <= maxApCost)
            {
                reachableTiles.Add(currentCoord);
            }

            // Explore neighbors if we can still afford to move from the current tile
            if (currentCost < maxApCost)
            {
                foreach (HexTile neighborTile in gridManager.GetNeighbors(currentHexTile))
                {
                    if (neighborTile == null || !neighborTile.isWalkable) continue;

                    // Check if occupied by another unit (different from the one moving)
                    Unit unitOnNeighbor = gridManager.GetUnitOnTile(neighborTile.coordinate);
                    if (unitOnNeighbor != null && unitOnNeighbor != unitToExclude) continue;

                    float stepCost = (neighborTile.tileType != null) ? neighborTile.tileType.moveCost : 1f;
                    if (stepCost <= 0) stepCost = 1; // Ensure positive cost

                    int newCostToNeighbor = currentCost + (int)stepCost;

                    if (newCostToNeighbor <= maxApCost)
                    {
                        if (!costToReachTile.ContainsKey(neighborTile.coordinate) || newCostToNeighbor < costToReachTile[neighborTile.coordinate])
                        {
                            costToReachTile[neighborTile.coordinate] = newCostToNeighbor;
                            frontier.Enqueue((neighborTile.coordinate, newCostToNeighbor));
                        }
                    }
                }
            }
        }
        return reachableTiles;
    }
}