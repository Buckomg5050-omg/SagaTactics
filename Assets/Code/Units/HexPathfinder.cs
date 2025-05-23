using System.Collections.Generic;

public class HexPathfinder
{
    private HexGrid grid;

    public HexPathfinder(HexGrid grid)
    {
        this.grid = grid;
    }

    public List<HexTile> FindPath(HexTile start, HexTile goal)
    {
        var openSet = new PriorityQueue<HexTile>();
        var cameFrom = new Dictionary<HexTile, HexTile>();
        var gScore = new Dictionary<HexTile, float>();

        openSet.Enqueue(start, 0);
        gScore[start] = 0;

        while (openSet.Count > 0)
        {
            HexTile current = openSet.Dequeue();

            if (current == goal)
                return ReconstructPath(cameFrom, current);

            foreach (HexTile neighbor in grid.GetNeighbors(current))
            {
                if (!neighbor.isWalkable) continue;

                float tentativeG = gScore[current] + neighbor.tileType.moveCost;
                if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    float priority = tentativeG + HexUtils.HexDistance(neighbor.coordinate, goal.coordinate);
                    openSet.Enqueue(neighbor, priority);
                }
            }
        }

        return null;
    }

    private List<HexTile> ReconstructPath(Dictionary<HexTile, HexTile> cameFrom, HexTile current)
    {
        var path = new List<HexTile> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }
}
