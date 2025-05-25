// File: EnemyAIController.cs
using UnityEngine;
using System.Collections; // NEW: For Coroutines
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(UnitMover))]
[RequireComponent(typeof(Unit))]
[RequireComponent(typeof(UnitCombat))] // NEW: Ensure UnitCombat is present
public class EnemyAIController : MonoBehaviour
{
    private UnitMover mover;
    private Unit unit;
    private UnitCombat unitCombat; 
    private UnitAP unitAP;       
    private UnitStats unitStats; // MODIFICATION: Added reference for UnitStats
    private HexGrid grid;
    private HexPathfinder pathfinder;

    private bool isExecutingTurn = false; 

    void Awake()
    {
        mover = GetComponent<UnitMover>();
        unit = GetComponent<Unit>();
        unitCombat = GetComponent<UnitCombat>(); 
        unitAP = unit.unitAP; 
        unitStats = unit.unitStats; // MODIFICATION: Get UnitStats from Unit component

        if (unitStats == null) // MODIFICATION: Check for unitStats
        {
            Debug.LogError($"EnemyAIController on {unit?.unitName ?? gameObject.name}: UnitStats component not found (via Unit)! This is critical.", this);
            enabled = false;
        }

        grid = FindFirstObjectByType<HexGrid>();
        if (grid != null)
        {
            pathfinder = new HexPathfinder(grid);
        }
        else
        {
            Debug.LogError($"EnemyAIController on {unit?.unitName ?? gameObject.name}: HexGrid not found!", this);
            enabled = false; 
        }

        if (unitCombat == null) 
        {
            Debug.LogError($"EnemyAIController on {unit?.unitName ?? gameObject.name}: UnitCombat component not found!", this);
            enabled = false;
        }
        if (unitAP == null) 
        {
            Debug.LogError($"EnemyAIController on {unit?.unitName ?? gameObject.name}: UnitAP component not found (via Unit)! This is critical.", this);
            enabled = false;
        }
    }

    public void RunAI()
    {
        if (isExecutingTurn || unit == null || unitAP == null || unitCombat == null || unitStats == null || mover == null || grid == null || pathfinder == null) // MODIFICATION: Added unitStats check
        {
            if (isExecutingTurn) Debug.LogWarning($"🧠 {unit?.unitName ?? "AI"} RunAI called while already executing. Ignoring.", this);
            else Debug.LogError($"🧠 {unit?.unitName ?? "AI"} RunAI called but critical components are missing. Ending turn early.", this);
            
            if (!isExecutingTurn) EndAITurnImmediately(); 
            return;
        }
        StartCoroutine(ExecuteAITurnCoroutine());
    }

    private IEnumerator ExecuteAITurnCoroutine()
    {
        isExecutingTurn = true;
        Debug.Log($"🧠 {unit.unitName} (AI) starting turn execution... AP: {unitAP.CurrentAP}");

        if (unitCombat.IsDead() || unitAP.CurrentAP <= 0 || unitStats == null) { // MODIFICATION: Added unitStats null check
            Debug.Log($"🧠 {unit.unitName} is dead, has no AP, or missing stats at start of AI turn.");
            EndAITurnImmediately();
            yield break;
        }

        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        Unit target = FindClosestPlayerUnit(allUnits);

        if (target == null || target.GetComponent<UnitCombat>() == null || target.GetComponent<UnitCombat>().IsDead())
        {
            Debug.LogWarning($"🧠 {unit.unitName}: No valid live player unit found or target is misconfigured.");
            EndAITurnImmediately();
            yield break;
        }
        Debug.Log($"🧠 {unit.unitName}: Targeting {target.unitName}.");

        bool attackedThisTurn = false;
        if (CanAttackTargetFromCurrentPosition(target))
        {
            Debug.Log($"🧠 {unit.unitName}: Can attack {target.unitName} from current position. AP: {unitAP.CurrentAP}");
            // Assuming AI currently only uses melee. Will need to expand for ranged AI.
            if (unitStats.meleeAttackRange > 0 && unitAP.CanSpend(unitStats.meleeAPCost)) // MODIFICATION: Check if unit can melee
            {
                unitCombat.PerformMeleeAttack(target); 
                attackedThisTurn = true;
                yield return new WaitForSeconds(0.5f); 
            }
            
            // MODIFICATION: Check against the actual AP cost of the cheapest action
            int cheapestActionCost = Mathf.Min(unitStats.meleeAPCost > 0 ? unitStats.meleeAPCost : int.MaxValue, 
                                            unitStats.rangedAPCost > 0 ? unitStats.rangedAPCost : int.MaxValue, 
                                            GetCheapestMoveCost());
            if (cheapestActionCost == int.MaxValue) cheapestActionCost = 1; // Failsafe

            if (unitAP.CurrentAP < cheapestActionCost) 
            {                                                                                                 
                Debug.Log($"🧠 {unit.unitName}: Out of AP for any further significant action after initial attack.");
                EndAITurnImmediately();
                yield break;
            }
        }

        if (!attackedThisTurn && unitAP.CurrentAP > 0) 
        {
            Debug.Log($"🧠 {unit.unitName}: Did not attack from initial position. Attempting to move. AP: {unitAP.CurrentAP}");
            HexTile currentUnitTile = grid.GetTileAt(mover.CurrentGridCoords);
            UnitMover targetMover = target.GetComponent<UnitMover>();
            if (currentUnitTile == null || targetMover == null) {
                Debug.LogError($"🧠 {unit.unitName}: Critical info missing for pathfinding (own tile or target mover).", this);
                EndAITurnImmediately();
                yield break;
            }
            HexTile targetUnitTile = grid.GetTileAt(targetMover.CurrentGridCoords);
            if (targetUnitTile == null) {
                 Debug.LogError($"🧠 {unit.unitName}: Target unit tile is null.", this);
                 EndAITurnImmediately();
                 yield break;
            }

            List<HexTile> fullPath = pathfinder.FindPath(currentUnitTile, targetUnitTile);

            if (fullPath == null || fullPath.Count < 2)
            {
                Debug.LogWarning($"🧠 {unit.unitName}: No valid path to target {target.unitName}.");
            }
            else
            {
                List<HexTile> pathToAdjacent = GetPathToAdjacentTile(fullPath, targetUnitTile, allUnits);

                if (pathToAdjacent != null && pathToAdjacent.Count > 1)
                {
                    HexTile bestTileToMoveTo = pathToAdjacent.LastOrDefault(); 
                    List<HexTile> finalPathToMove = pathfinder.FindPath(currentUnitTile, bestTileToMoveTo);

                    if (finalPathToMove != null && finalPathToMove.Count > 1)
                    {
                        int moveCost = CalculatePathCost(finalPathToMove);
                        if (unitAP.CanSpend(moveCost))
                        {
                            Debug.Log($"🧠 {unit.unitName} moving to {bestTileToMoveTo.coordinate} (Cost: {moveCost} AP) to get adjacent to {target.unitName}. AP before move: {unitAP.CurrentAP}");
                            
                            bool moveCompleted = false;
                            System.Action onMoveCompleteCallback = () => { moveCompleted = true; }; // Store callback to remove it
                            mover.OnMoveComplete += onMoveCompleteCallback;
                            mover.MoveAlongPath(finalPathToMove); 

                            yield return new WaitUntil(() => moveCompleted || !mover.IsMoving); 
                            mover.OnMoveComplete -= onMoveCompleteCallback; 
                            yield return new WaitForSeconds(0.1f); 

                            if (CanAttackTargetFromCurrentPosition(target))
                            {
                                Debug.Log($"🧠 {unit.unitName}: Moved to {mover.CurrentGridCoords}. Now attacking {target.unitName}. AP: {unitAP.CurrentAP}");
                                // Assuming AI currently only uses melee.
                                if (unitStats.meleeAttackRange > 0 && unitAP.CanSpend(unitStats.meleeAPCost)) // MODIFICATION
                                {
                                    unitCombat.PerformMeleeAttack(target);
                                    attackedThisTurn = true; 
                                    yield return new WaitForSeconds(0.5f); 
                                }
                            }
                            else
                            {
                                Debug.Log($"🧠 {unit.unitName}: Moved to {mover.CurrentGridCoords}, but still cannot attack {target.unitName} or not enough AP.");
                            }
                        }
                        else
                        {
                            Debug.Log($"🧠 {unit.unitName}: Cannot afford path to get adjacent to {target.unitName}. Cost: {moveCost}, AP: {unitAP.CurrentAP}");
                        }
                    }
                    else
                    {
                         Debug.Log($"🧠 {unit.unitName}: No path to chosen adjacent tile {bestTileToMoveTo?.coordinate.ToString() ?? "NULL"}.");
                    }
                }
                else
                {
                    Debug.Log($"🧠 {unit.unitName}: Could not find an affordable path to a tile adjacent to {target.unitName}.");
                }
            }
        }
        else if (attackedThisTurn)
        {
            Debug.Log($"🧠 {unit.unitName}: Attacked from initial position. Deciding next action or ending turn. AP: {unitAP.CurrentAP}");
        }

        EndAITurnImmediately();
    }

    private bool CanAttackTargetFromCurrentPosition(Unit target)
    {
        if (target == null || unitCombat == null || unitAP == null || unitStats == null || mover == null || grid == null) return false; // MODIFICATION: Added unitStats check
        if (target.GetComponent<UnitCombat>() == null || target.GetComponent<UnitCombat>().IsDead()) return false;

        // For now, AI only considers melee. This needs to be expanded for AI to use ranged attacks.
        if (unitStats.meleeAttackRange <= 0) return false; // AI unit doesn't have melee

        if (!unitAP.CanSpend(unitStats.meleeAPCost)) // MODIFICATION: Use unitStats.meleeAPCost
        {
            return false;
        }

        HexTile myTile = grid.GetTileAt(mover.CurrentGridCoords);
        HexTile targetTile = grid.GetTileAt(target.GetComponent<UnitMover>().CurrentGridCoords);

        if (myTile == null || targetTile == null) return false;

        // Check distance for melee (using unitStats.meleeAttackRange, which is typically 1)
        // A more robust distance check for hex grids might be needed if meleeAttackRange can be > 1.
        // For range 1, adjacency is sufficient.
        if (HexUtils.HexDistance(myTile.coordinate, targetTile.coordinate) <= unitStats.meleeAttackRange) // MODIFICATION: Use HexUtils & unitStats.meleeAttackRange
        {
             return true;
        }
        // List<HexTile> neighbors = grid.GetNeighbors(myTile); // Old adjacency check
        // return neighbors.Contains(targetTile);
        return false;
    }
    
    private int GetCheapestMoveCost() 
    {
        HexTile myTile = grid.GetTileAt(mover.CurrentGridCoords);
        if (myTile == null) return int.MaxValue;
        
        int minCost = int.MaxValue;
        foreach(var neighbor in grid.GetNeighbors(myTile))
        {
            if(neighbor.isWalkable && neighbor.tileType.moveCost < minCost)
            {
                minCost = neighbor.tileType.moveCost;
            }
        }
        return minCost == int.MaxValue ? 1 : minCost; 
    }

    private Unit FindClosestPlayerUnit(Unit[] allUnits)
    {
        Unit closestUnit = null;
        float closestDistSqr = float.MaxValue; // Using this as a stand-in for path cost comparison
        Vector2Int myCoords = mover.CurrentGridCoords;

        foreach (Unit u in allUnits)
        {
            if (u == null || u == unit || u.Team != Unit.UnitTeam.Player || !u.gameObject.activeInHierarchy) continue;
            
            UnitCombat otherCombat = u.GetComponent<UnitCombat>();
            if (otherCombat != null && otherCombat.IsDead()) continue; 

            UnitMover otherMover = u.GetComponent<UnitMover>();
            if (otherMover == null) continue;
            
            HexTile myTile = grid.GetTileAt(myCoords);
            HexTile otherTile = grid.GetTileAt(otherMover.CurrentGridCoords);
            if (myTile != null && otherTile != null)
            {
                List<HexTile> path = pathfinder.FindPath(myTile, otherTile);
                float dist = (path != null && path.Count > 0) ? CalculatePathCost(path) : float.MaxValue; // Use actual path cost

                if (dist < closestDistSqr)
                {
                    closestDistSqr = dist;
                    closestUnit = u;
                }
            }
        }
        return closestUnit;
    }

    private List<HexTile> GetPathToAdjacentTile(List<HexTile> fullPathToTarget, HexTile targetActualTile, Unit[] allUnits)
    {
        if (fullPathToTarget == null || fullPathToTarget.Count < 1) return null; 
        if (targetActualTile == null) return null;

        HexTile currentTile = grid.GetTileAt(mover.CurrentGridCoords);
        if (currentTile == null) return null;

        List<HexTile> bestPathToAdjacent = null;
        float bestPathScore = float.MaxValue; // Lower score is better (e.g., path cost)

        foreach (HexTile adjacentToTargetTile in grid.GetNeighbors(targetActualTile))
        {
            if (adjacentToTargetTile == null || !adjacentToTargetTile.isWalkable) continue;
            if (adjacentToTargetTile == currentTile) { 
                 return new List<HexTile>{currentTile}; // Already adjacent, no move needed for this function's purpose
            }

            bool isOccupiedByOther = false;
            foreach(Unit u in allUnits)
            {
                // Check if the adjacent tile is occupied by any unit that isn't the current AI unit OR the target unit itself
                if (u != unit && u.gameObject != targetActualTile.gameObject && u.GetComponent<UnitMover>()?.CurrentGridCoords == adjacentToTargetTile.coordinate)
                {
                    isOccupiedByOther = true;
                    break;
                }
            }
            if(isOccupiedByOther) continue;

            List<HexTile> pathCandidate = pathfinder.FindPath(currentTile, adjacentToTargetTile);
            if (pathCandidate != null && pathCandidate.Count > 1) // Path must involve at least one step
            {
                int cost = CalculatePathCost(pathCandidate);
                if (unitAP.CanSpend(cost)) 
                {
                    // Simple heuristic: prefer shorter, affordable paths
                    if (cost < bestPathScore) 
                    {
                        bestPathScore = cost;
                        bestPathToAdjacent = pathCandidate;
                    }
                }
            }
        }
        return bestPathToAdjacent; 
    }
    
    private int CalculatePathCost(List<HexTile> path)
    {
        if (path == null || path.Count < 2) return 0;
        int cost = 0;
        for(int i = 1; i < path.Count; i++) 
        {
            cost += path[i].tileType.moveCost;
        }
        return cost;
    }

    private void EndAITurnImmediately() 
    {
        isExecutingTurn = false; 
        Debug.Log($"🧠 {unit?.unitName ?? gameObject.name} AI turn logic finished. Ending turn via TacticalCombatManager.");
        TacticalCombatManager.Instance?.EndCurrentTurn();
    }
}