using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class TacticalCombatManager : MonoBehaviour
{
    public static TacticalCombatManager Instance { get; private set; }

    // MODIFIED: Changed from Action to Action<Unit> to pass the current unit
    public static event Action<Unit> OnActiveUnitChanged; // Renamed for clarity, passes the new active unit
    public static event Action<Unit.UnitTeam> OnCombatEnd;

    private List<Unit> turnOrder = new();
    private int currentTurnIndex = -1;
    private bool combatActive = false;

    public Unit CurrentUnit => combatActive && currentTurnIndex >= 0 && currentTurnIndex < turnOrder.Count ? turnOrder[currentTurnIndex] : null;
    public bool IsPlayerTurn => CurrentUnit != null && CurrentUnit.Team == Unit.UnitTeam.Player;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate TacticalCombatManager instance found. Destroying this one.");
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    void Start()
    {
        // Consider if combat should always start automatically, or be triggered by another game event.
        // For testing, Start() is fine.
        StartCombat();
    }

    public void StartCombat()
    {
        // Ensure units are properly filtered for active stats and not already defeated
        turnOrder = FindObjectsByType<Unit>(FindObjectsSortMode.None)
            .Where(u => u != null && u.gameObject.activeInHierarchy && u.unitStats != null && !u.unitStats.IsDefeated())
            .OrderByDescending(u => u.Echo) // Assuming Echo is a stat for initiative
            .ThenByDescending(u => u.Aura)   // Assuming Aura is a tie-breaker
            .ThenBy(_ => UnityEngine.Random.value) // Final random tie-breaker
            .ToList();

        if (turnOrder.Count < 2) // Basic check, might need refinement based on game rules (e.g., 1 player vs 0 enemies is still a win)
        {
            Debug.LogWarning("Not enough uniquely-teamed active units to start meaningful combat. Found: " + turnOrder.Count);
            combatActive = false;
            // Potentially invoke OnCombatEnd here if it signifies an immediate resolution
            // For now, just preventing combat start.
            if (turnOrder.Count == 1) OnCombatEnd?.Invoke(turnOrder[0].Team); // If only one unit/team, they win
            else if (turnOrder.Count == 0) OnCombatEnd?.Invoke(Unit.UnitTeam.Neutral); // No units, neutral outcome
            return;
        }

        currentTurnIndex = -1; // Will be incremented to 0 on the first NextTurn()
        combatActive = true;
        Debug.Log("Combat Started. Turn order: " + string.Join(", ", turnOrder.Select(u => u.unitName)));
        NextTurn();
    }

    public void NextTurn()
    {
        if (!combatActive)
        {
            Debug.Log("NextTurn called, but combat is not active.");
            // CheckWinCondition might still be relevant if combat became inactive due to it
            if (turnOrder.Count > 0 && (turnOrder.All(u => u.Team == turnOrder[0].Team) || turnOrder.Count < 2)) {
                 // This situation implies a win condition was met that didn't get caught by CheckWinCondition
                 // or combat was stopped externally.
            }
            return;
        }

        if (turnOrder.Count == 0)
        {
            Debug.Log("NextTurn called, but no units remain in turn order. Checking win condition.");
            CheckWinCondition(); // This should set combatActive to false if applicable
            return;
        }

        Unit previousUnit = CurrentUnit;

        currentTurnIndex = (currentTurnIndex + 1) % turnOrder.Count;
        Unit newCurrentUnit = turnOrder[currentTurnIndex]; // This is THE new active unit

        // Ensure the previous unit (if valid and not the same as new unit) faces camera
        if (previousUnit != null && previousUnit != newCurrentUnit && previousUnit.gameObject.activeInHierarchy)
        {
            UnitFacing prevFacing = previousUnit.GetComponent<UnitFacing>();
            UnitMover prevMover = previousUnit.GetComponent<UnitMover>();
            if (prevFacing != null && (prevMover == null || !prevMover.IsMoving))
            {
                // Debug.Log($"TCM: Telling {previousUnit.unitName} to look towards camera as its turn ended.", previousUnit);
                prevFacing.SetTargetLookTowardsCamera();
            }
        }

        // Sanity check for the new current unit
        if (newCurrentUnit == null || !newCurrentUnit.gameObject.activeInHierarchy || (newCurrentUnit.unitStats != null && newCurrentUnit.unitStats.IsDefeated()))
        {
            Debug.LogWarning($"Unit {newCurrentUnit?.unitName ?? "Unknown (was null)"} in turn order is invalid (null, inactive, or defeated). Removing and advancing turn.");
            if (newCurrentUnit != null) UnitDied(newCurrentUnit, false); // Remove it, don't force next turn from here as NextTurn() will be called recursively
            else turnOrder.RemoveAt(currentTurnIndex); // If unit was truly null, remove placeholder

            // Adjust index carefully if we removed an element before the current one.
            // However, UnitDied handles index adjustment. If it was null, we might need to re-evaluate.
            // For safety, if we removed a null entry, we might need to decrement currentTurnIndex before the recursive NextTurn.
            // But given UnitDied is called, it should be okay.
            
            // To prevent infinite loops if something is very wrong with turnOrder:
            if(turnOrder.Count > 0) NextTurn();
            else CheckWinCondition(); // No units left
            return;
        }

        Debug.Log($"It is now {newCurrentUnit.unitName}'s turn.");
        newCurrentUnit.BeginTurn(); // Let the unit know its turn has started (e.g., refresh AP)

        // MODIFIED: Invoke the new event, passing the newCurrentUnit
        OnActiveUnitChanged?.Invoke(newCurrentUnit);
    }

    public void EndCurrentTurn()
    {
        if (!combatActive || CurrentUnit == null)
        {
            Debug.LogWarning("EndCurrentTurn called, but combat is not active or no current unit.");
            return;
        }
        // Debug.Log($"{CurrentUnit.unitName} is ending their turn.");
        CurrentUnit.EndTurn(); // Let the unit do any end-of-turn cleanup
        NextTurn();
    }

    public void UnitDied(Unit deadUnit, bool forceNextTurnIfCurrent = true)
    {
        if (deadUnit == null)
        {
            Debug.LogWarning("UnitDied called with a null unit.");
            return;
        }
        if (!combatActive)
        {
            // Debug.Log($"UnitDied ({deadUnit.unitName}) called, but combat is not active. Ignoring.");
            return; // Combat already ended, or never started properly
        }

        int deadUnitIndex = turnOrder.IndexOf(deadUnit);

        if (deadUnitIndex != -1)
        {
            bool wasCurrentUnit = (CurrentUnit == deadUnit);
            // Debug.Log($"{deadUnit.unitName} is being removed from turn order. Was current unit: {wasCurrentUnit}");
            turnOrder.RemoveAt(deadUnitIndex);

            if (turnOrder.Count == 0)
            {
                // Debug.Log("Last unit died. Checking win condition.");
                CheckWinCondition(); // This will set combatActive = false
                return;
            }

            // Adjust currentTurnIndex carefully
            if (wasCurrentUnit)
            {
                // The current unit died. The list shifted. currentTurnIndex effectively now points
                // to the *next* unit in sequence (or is out of bounds if the dead unit was last).
                // To make NextTurn() pick up correctly, we decrement currentTurnIndex.
                // So, (currentTurnIndex + 1) % count will correctly select the unit that
                // took the dead unit's spot, or wrap around if the dead unit was the last one.
                currentTurnIndex--;
                if (currentTurnIndex < -1) currentTurnIndex = -1; // Should ideally be turnOrder.Count -1 if list becomes empty, but NextTurn() handles empty
            }
            else if (deadUnitIndex < currentTurnIndex)
            {
                // A unit died *before* the current unit in the turn order list.
                // So, the current unit's index effectively shifted down by one.
                currentTurnIndex--;
            }
            // If deadUnitIndex was > currentTurnIndex, the current unit's position is unaffected.

            if (!CheckWinCondition()) // If combat hasn't ended
            {
                if (wasCurrentUnit && forceNextTurnIfCurrent)
                {
                    // Debug.Log($"Current unit {deadUnit.unitName} died. Forcing NextTurn().");
                    NextTurn();
                }
                // If a non-current unit died, the current unit continues its turn.
                // We might want to refresh UI or other elements.
                // else { OnActiveUnitChanged?.Invoke(CurrentUnit); } // Optionally re-signal current unit if UI needs update
            }
        }
        else
        {
            // Debug.LogWarning($"{deadUnit.unitName} was not found in the turn order when UnitDied was called.");
        }
    }

    private bool CheckWinCondition()
    {
        if (!combatActive && turnOrder.Count > 0 && turnOrder.All(u => u.Team == turnOrder[0].Team)) {
            // This is a specific state where combat might have been marked inactive, but a clear winner exists.
            // For example, if an external script stopped combat but one team remains.
            Debug.Log($"Combat was inactive, but a single team ({turnOrder[0].Team}) remains. Declaring winner.");
            OnCombatEnd?.Invoke(turnOrder[0].Team);
            combatActive = false; // Ensure it's false
            return true;
        }

        if (!combatActive && turnOrder.Count > 0 && turnOrder.Count(u => u.Team == Unit.UnitTeam.Player) > 0 && turnOrder.Count(u => u.Team == Unit.UnitTeam.Enemy) > 0) {
            // Combat was inactive, but multiple teams remain. No clear winner.
             Debug.Log("Combat was inactive, multiple teams remain. No decisive outcome.");
             // OnCombatEnd?.Invoke(Unit.UnitTeam.Neutral); // Or maybe don't invoke if combat was externally stopped
             combatActive = false; // Ensure it's false
             return true; // Still treating as "condition checked" because combat is over
        }


        if (turnOrder.Count == 0)
        {
            if (combatActive) // Only log/invoke if combat was considered active before this check
            {
                Debug.Log("COMBAT ENDED: No units remaining (Draw or error?).");
                OnCombatEnd?.Invoke(Unit.UnitTeam.Neutral);
            }
            combatActive = false;
            return true;
        }

        // Efficiently check remaining teams
        bool playersRemain = false;
        bool enemiesRemain = false;
        foreach (Unit u in turnOrder)
        {
            if (u == null || u.unitStats == null || !u.gameObject.activeInHierarchy || u.unitStats.IsDefeated()) continue; // Skip invalid units

            if (u.Team == Unit.UnitTeam.Player) playersRemain = true;
            else if (u.Team == Unit.UnitTeam.Enemy) enemiesRemain = true;

            if (playersRemain && enemiesRemain) break; // Found members of both teams, no need to check further
        }

        if (playersRemain && !enemiesRemain)
        {
            if (combatActive) Debug.Log("ENEMY DEFEATED! Player team wins.");
            combatActive = false;
            OnCombatEnd?.Invoke(Unit.UnitTeam.Player);
            return true;
        }
        else if (!playersRemain && enemiesRemain)
        {
            if (combatActive) Debug.Log("PLAYER DEFEATED! Enemy team wins.");
            combatActive = false;
            OnCombatEnd?.Invoke(Unit.UnitTeam.Enemy);
            return true;
        }
        else if (!playersRemain && !enemiesRemain && turnOrder.Count > 0) // All remaining units are neutral or invalid
        {
            if (combatActive) Debug.Log("COMBAT ENDED: No Player or Enemy units remain (Mutual Destruction or Neutral clear?).");
            combatActive = false;
            OnCombatEnd?.Invoke(Unit.UnitTeam.Neutral);
            return true;
        }
        // If both playersRemain and enemiesRemain are true, or if only neutral units are left but combat was meant to continue,
        // then the combat continues.
        return false; // Combat continues
    }
}