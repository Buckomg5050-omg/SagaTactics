using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(UnitMover))]
[RequireComponent(typeof(Unit))]
public class EnemyAIController : MonoBehaviour
{
    private UnitMover mover;
    private Unit unit;
    private HexGrid grid;
    private HexPathfinder pathfinder;

    void Awake()
    {
        mover = GetComponent<UnitMover>();
        unit = GetComponent<Unit>();
        grid = FindFirstObjectByType<HexGrid>();
        pathfinder = new HexPathfinder(grid);
    }

    public void RunAI()
    {
        Debug.Log($"🧠 {unit.unitName} (AI) starting turn...");

        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        Unit target = FindClosestPlayerUnit(allUnits);

        if (target == null)
        {
            Debug.LogWarning("🧠 No player unit found.");
            EndAITurn();
            return;
        }

        Vector2Int targetCoords = target.GetComponent<UnitMover>().CurrentGridCoords;
        List<HexTile> fullPath = pathfinder.FindPath(
            grid.GetTileAt(mover.CurrentGridCoords),
            grid.GetTileAt(targetCoords)
        );

        if (fullPath == null || fullPath.Count < 2)
        {
            Debug.LogWarning("🧠 No valid path to target.");
            EndAITurn();
            return;
        }

        List<HexTile> reachable = GetAffordableTiles(fullPath);
        HexTile bestTile = ChooseBestTile(reachable, targetCoords, allUnits);

        if (bestTile == null || bestTile == grid.GetTileAt(mover.CurrentGridCoords))
        {
            Debug.Log($"🧠 {unit.unitName} has no good move.");
            EndAITurn();
            return;
        }

        List<HexTile> finalPath = pathfinder.FindPath(grid.GetTileAt(mover.CurrentGridCoords), bestTile);
        if (finalPath == null)
        {
            Debug.LogWarning("🧠 Final path invalid.");
            EndAITurn();
            return;
        }

        Debug.Log($"🧠 {unit.unitName} moving to {bestTile.coordinate} ({finalPath.Count - 1} steps).");
        mover.MoveAlongPath(finalPath.Skip(1).ToList()); // skip current tile
        mover.OnMoveComplete += EndAITurn;
    }

    private Unit FindClosestPlayerUnit(Unit[] allUnits)
    {
        return allUnits
            .Where(u => u != unit && u.Team == Unit.UnitTeam.Player)
            .OrderBy(u => Vector2Int.Distance(mover.CurrentGridCoords, u.GetComponent<UnitMover>().CurrentGridCoords))
            .FirstOrDefault();
    }

    private List<HexTile> GetAffordableTiles(List<HexTile> fullPath)
    {
        float apRemaining = unit.unitAP.CurrentAP;
        float costSum = 0f;
        var reachable = new List<HexTile>();

        for (int i = 1; i < fullPath.Count; i++) // skip current
        {
            costSum += fullPath[i].tileType.moveCost;
            if (costSum <= apRemaining)
                reachable.Add(fullPath[i]);
            else
                break;
        }

        return reachable;
    }

    private HexTile ChooseBestTile(List<HexTile> candidates, Vector2Int targetCoords, Unit[] allUnits)
    {
        HashSet<Vector2Int> occupiedTiles = new HashSet<Vector2Int>(
            allUnits.Where(u => u != unit)
                    .Select(u => u.GetComponent<UnitMover>().CurrentGridCoords)
        );

        return candidates
            .Where(t => !occupiedTiles.Contains(t.coordinate))
            .OrderByDescending(t => TileScore(t, targetCoords))
            .FirstOrDefault();
    }

    private float TileScore(HexTile tile, Vector2Int targetCoords)
    {
        float distanceScore = -Vector2Int.Distance(tile.coordinate, targetCoords);
        
        // Assume high ground provides defensive bonus
        float heightBonus = tile.tileType.defenseBonusPercent > 0 ? 2f : 0f;

        return distanceScore + heightBonus;
    }

    private void EndAITurn()
    {
        mover.OnMoveComplete -= EndAITurn;
        Debug.Log($"🧠 {unit.unitName} ending turn.");
        TacticalCombatManager.Instance.EndCurrentTurn();
    }
}
