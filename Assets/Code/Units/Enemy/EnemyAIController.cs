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
    private UnitCombat unitCombat; // NEW: Reference to UnitCombat
    private UnitAP unitAP;       // NEW: Reference to UnitAP (from unit)
    private HexGrid grid;
    private HexPathfinder pathfinder;

    private bool isExecutingTurn = false; // NEW: Flag to prevent re-triggering AI logic while one is running

    void Awake()
    {
        mover = GetComponent<UnitMover>();
        unit = GetComponent<Unit>();
        unitCombat = GetComponent<UnitCombat>(); // NEW
        unitAP = unit.unitAP; // NEW: Get UnitAP from the Unit component

        grid = FindFirstObjectByType<HexGrid>();
        if (grid != null)
        {
            pathfinder = new HexPathfinder(grid);
        }
        else
        {
            Debug.LogError($"EnemyAIController on {unit?.unitName ?? gameObject.name}: HexGrid not found!", this);
            enabled = false; // Disable AI if no grid
        }

        if (unitCombat == null) // NEW
        {
            Debug.LogError($"EnemyAIController on {unit?.unitName ?? gameObject.name}: UnitCombat component not found!", this);
            enabled = false;
        }
        if (unitAP == null) // NEW
        {
            Debug.LogError($"EnemyAIController on {unit?.unitName ?? gameObject.name}: UnitAP component not found (via Unit)! This is critical.", this);
            enabled = false;
        }
    }

    public void RunAI()
    {
        if (isExecutingTurn || unit == null || unitAP == null || unitCombat == null || mover == null || grid == null || pathfinder == null) // Check all critical components
        {
            if (isExecutingTurn) Debug.LogWarning($"🧠 {unit?.unitName ?? "AI"} RunAI called while already executing. Ignoring.", this);
            else Debug.LogError($"🧠 {unit?.unitName ?? "AI"} RunAI called but critical components are missing. Ending turn early.", this);
            
            if (!isExecutingTurn) EndAITurnImmediately(); // Only end turn if not already in the middle of execution to avoid double end.
            return;
        }
        StartCoroutine(ExecuteAITurnCoroutine());
    }

    private IEnumerator ExecuteAITurnCoroutine()
    {
        isExecutingTurn = true;
        Debug.Log($"🧠 {unit.unitName} (AI) starting turn execution... AP: {unitAP.CurrentAP}");

        // 0. Ensure unit is alive and can act
        if (unitCombat.IsDead() || unitAP.CurrentAP <= 0) {
            Debug.Log($"🧠 {unit.unitName} is dead or has no AP at start of AI turn.");
            EndAITurnImmediately();
            yield break;
        }

        // 1. Find Target
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        Unit target = FindClosestPlayerUnit(allUnits);

        if (target == null || target.GetComponent<UnitCombat>() == null || target.GetComponent<UnitCombat>().IsDead())
        {
            Debug.LogWarning($"🧠 {unit.unitName}: No valid live player unit found or target is misconfigured.");
            EndAITurnImmediately();
            yield break;
        }
        Debug.Log($"🧠 {unit.unitName}: Targeting {target.unitName}.");

        // 2. Attempt to Attack from Current Position (if possible and sensible)
        bool attackedThisTurn = false;
        if (CanAttackTargetFromCurrentPosition(target))
        {
            Debug.Log($"🧠 {unit.unitName}: Can attack {target.unitName} from current position. AP: {unitAP.CurrentAP}");
            unitCombat.PerformMeleeAttack(target); // PerformMeleeAttack already handles AP cost
            attackedThisTurn = true;
            yield return new WaitForSeconds(0.5f); // Brief pause for attack animation/VFX to be perceived
            
            // If out of AP after attack, end turn
            if (unitAP.CurrentAP < UnitCombat.MELEE_ATTACK_AP_COST && unitAP.CurrentAP < GetCheapestMoveCost()) // Example: if can't even move 1 tile
            {                                                                                                  // Or simply unitAP.CurrentAP <=0
                Debug.Log($"🧠 {unit.unitName}: Out of AP after initial attack.");
                EndAITurnImmediately();
                yield break;
            }
        }

        // 3. If didn't attack, or still has AP and wants to move (e.g., to a better position or closer to another target)
        // For now, if it attacked, it won't try to move then attack again in the same turn.
        // More complex AI could decide to move even after attacking.
        // Let's assume if it attacks, its main action is done unless it has significant AP left for a strategic reposition.
        // For this basic AI: if attacked, and still has AP, it *could* move.
        // If it *didn't* attack, it *will try* to move and then potentially attack.

        if (!attackedThisTurn && unitAP.CurrentAP > 0) // Only move if we haven't attacked yet, and have AP
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
                // EndAITurnImmediately(); // Don't end yet, might still be able to attack if already adjacent
            }
            else
            {
                // Try to move to a tile ADJACENT to the target, not ON the target's tile.
                List<HexTile> pathToAdjacent = GetPathToAdjacentTile(fullPath, targetUnitTile, allUnits);

                if (pathToAdjacent != null && pathToAdjacent.Count > 1)
                {
                    HexTile bestTileToMoveTo = pathToAdjacent.LastOrDefault(); // The tile adjacent to target
                    // Path to this best tile (excluding current)
                    List<HexTile> finalPathToMove = pathfinder.FindPath(currentUnitTile, bestTileToMoveTo);

                    if (finalPathToMove != null && finalPathToMove.Count > 1)
                    {
                        int moveCost = CalculatePathCost(finalPathToMove);
                        if (unitAP.CanSpend(moveCost))
                        {
                            Debug.Log($"🧠 {unit.unitName} moving to {bestTileToMoveTo.coordinate} (Cost: {moveCost} AP) to get adjacent to {target.unitName}. AP before move: {unitAP.CurrentAP}");
                            
                            bool moveCompleted = false;
                            mover.OnMoveComplete += () => { moveCompleted = true; };
                            mover.MoveAlongPath(finalPathToMove); // MoveAlongPath handles AP spending

                            yield return new WaitUntil(() => moveCompleted || !mover.IsMoving); // Wait for movement
                            mover.OnMoveComplete -= () => { moveCompleted = true; }; // Clean up temp listener
                            yield return new WaitForSeconds(0.1f); // Small buffer after move

                            // Now, attempt to attack from the new position
                            if (CanAttackTargetFromCurrentPosition(target))
                            {
                                Debug.Log($"🧠 {unit.unitName}: Moved to {mover.CurrentGridCoords}. Now attacking {target.unitName}. AP: {unitAP.CurrentAP}");
                                unitCombat.PerformMeleeAttack(target);
                                attackedThisTurn = true; // Mark that an attack occurred
                                yield return new WaitForSeconds(0.5f); // Pause for attack
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
            // AI could have more logic here if it has AP left after an attack (e.g., reposition, secondary ability)
            // For now, if it attacked, it's done.
        }


        // 4. End Turn
        EndAITurnImmediately();
    }

    private bool CanAttackTargetFromCurrentPosition(Unit target)
    {
        if (target == null || unitCombat == null || unitAP == null || mover == null || grid == null) return false;
        if (target.GetComponent<UnitCombat>() == null || target.GetComponent<UnitCombat>().IsDead()) return false;


        if (!unitAP.CanSpend(UnitCombat.MELEE_ATTACK_AP_COST))
        {
            // Debug.Log($"🧠 {unit.unitName}: Not enough AP to attack {target.unitName} (needs {UnitCombat.MELEE_ATTACK_AP_COST}, has {unitAP.CurrentAP}).");
            return false;
        }

        HexTile myTile = grid.GetTileAt(mover.CurrentGridCoords);
        HexTile targetTile = grid.GetTileAt(target.GetComponent<UnitMover>().CurrentGridCoords);

        if (myTile == null || targetTile == null) return false;

        // Simple adjacency check for melee
        List<HexTile> neighbors = grid.GetNeighbors(myTile);
        return neighbors.Contains(targetTile);
    }
    
    private int GetCheapestMoveCost() // Helper to see if unit can move at all
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
        return minCost == int.MaxValue ? 1 : minCost; // Default to 1 if no walkable neighbors (should not happen if on grid)
    }


    private Unit FindClosestPlayerUnit(Unit[] allUnits)
    {
        Unit closestUnit = null;
        float closestDistSqr = float.MaxValue;
        Vector2Int myCoords = mover.CurrentGridCoords;

        foreach (Unit u in allUnits)
        {
            if (u == null || u == unit || u.Team != Unit.UnitTeam.Player || !u.gameObject.activeInHierarchy) continue;
            
            UnitCombat otherCombat = u.GetComponent<UnitCombat>();
            if (otherCombat != null && otherCombat.IsDead()) continue; // Skip dead units

            UnitMover otherMover = u.GetComponent<UnitMover>();
            if (otherMover == null) continue;

            // Using squared Euclidean distance for comparison is faster than HexDistance if only comparing relative distances
            // However, for tactical hex grids, true hex distance is usually better.
            // Let's use hex distance for accuracy in targeting.
            HexTile myTile = grid.GetTileAt(myCoords);
            HexTile otherTile = grid.GetTileAt(otherMover.CurrentGridCoords);
            if (myTile != null && otherTile != null)
            {
                // This requires HexUtils or a public HexDistance. For now, let's use path length as proxy if HexDistance isn't easily available.
                // Or, simply use Manhattan distance on grid coords as a rough estimate for ordering.
                // int dist = HexUtils.HexDistance(myCoords, otherMover.CurrentGridCoords); // IDEAL
                
                // Fallback to path length if HexUtils.HexDistance is not available:
                List<HexTile> path = pathfinder.FindPath(myTile, otherTile);
                float dist = (path != null && path.Count > 0) ? path.Count -1 : float.MaxValue;


                if (dist < closestDistSqr)
                {
                    closestDistSqr = dist;
                    closestUnit = u;
                }
            }
        }
        return closestUnit;
    }

    // MODIFIED: Tries to find a path to a tile ADJACENT to the target, not on it.
    private List<HexTile> GetPathToAdjacentTile(List<HexTile> fullPathToTarget, HexTile targetActualTile, Unit[] allUnits)
    {
        if (fullPathToTarget == null || fullPathToTarget.Count < 1) return null; // Path must at least contain target
        if (targetActualTile == null) return null;

        // If already adjacent or on target tile (which shouldn't happen for movement)
        HexTile currentTile = grid.GetTileAt(mover.CurrentGridCoords);
        if (currentTile == null) return null;

        if (grid.GetNeighbors(currentTile).Contains(targetActualTile)) {
            // Already adjacent, no need to move closer to attack (unless AI wants to reposition for better angle/terrain)
            // For now, if adjacent, we assume it would have attacked in the first check.
            // So this function implies we are NOT adjacent yet.
            // Return a path that ends on current tile.
            // return new List<HexTile> { currentTile }; // No, this is wrong if we need to move *to* an adjacent tile.
        }


        List<HexTile> bestPathToAdjacent = null;
        float bestPathCost = float.MaxValue;

        // Get all tiles adjacent to the target
        foreach (HexTile adjacentToTargetTile in grid.GetNeighbors(targetActualTile))
        {
            if (adjacentToTargetTile == null || !adjacentToTargetTile.isWalkable) continue;
            if (adjacentToTargetTile == currentTile) { // AI is already on an adjacent tile
                 // This means the AI is already in a position to attack. Path is just its current tile.
                 // However, the main logic should have caught this with CanAttackTargetFromCurrentPosition.
                 // If we reach here and are already adjacent, it means CanAttack might have failed due to AP.
                 // So, don't move.
                 return new List<HexTile>{currentTile};
            }

            // Check if this adjacent tile is occupied by another unit (not self, not target)
            bool isOccupiedByOther = false;
            foreach(Unit u in allUnits)
            {
                if (u != unit && u != targetActualTile.GetComponentInParent<Unit>() && u.GetComponent<UnitMover>()?.CurrentGridCoords == adjacentToTargetTile.coordinate)
                {
                    isOccupiedByOther = true;
                    break;
                }
            }
            if(isOccupiedByOther) continue;


            List<HexTile> pathCandidate = pathfinder.FindPath(currentTile, adjacentToTargetTile);
            if (pathCandidate != null && pathCandidate.Count > 1)
            {
                int cost = CalculatePathCost(pathCandidate);
                if (unitAP.CanSpend(cost)) // Can we afford this path?
                {
                    if (cost < bestPathCost) // Is this affordable path better than previous best?
                    {
                        bestPathCost = cost;
                        bestPathToAdjacent = pathCandidate;
                    }
                }
            }
        }
        return bestPathToAdjacent; // This could be null if no valid adjacent tile is reachable
    }
    
    private int CalculatePathCost(List<HexTile> path)
    {
        if (path == null || path.Count < 2) return 0;
        int cost = 0;
        for(int i = 1; i < path.Count; i++) // Start from 1 as path[0] is current tile
        {
            cost += path[i].tileType.moveCost;
        }
        return cost;
    }

    // Your existing GetAffordableTiles, ChooseBestTile, TileScore are more for general movement.
    // The new logic above is more specific for "move to attack". We can integrate them later if needed.

    private void EndAITurnImmediately() // Renamed from EndAITurn to avoid conflict if it was an event handler
    {
        // mover.OnMoveComplete -= EndAITurnImmediately; // Ensure it's removed if it was ever added
        isExecutingTurn = false; // Allow RunAI to be called again next turn
        Debug.Log($"🧠 {unit?.unitName ?? gameObject.name} AI turn logic finished. Ending turn via TacticalCombatManager.");
        TacticalCombatManager.Instance?.EndCurrentTurn();
    }
}