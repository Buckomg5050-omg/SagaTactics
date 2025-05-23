using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class TacticalCombatManager : MonoBehaviour
{
    public static TacticalCombatManager Instance { get; private set; }

    public static event Action OnTurnChanged;
    public static event Action<Unit.UnitTeam> OnCombatEnd; // NEW: Event for when combat ends (win/loss)

    private List<Unit> turnOrder = new();
    private int currentTurnIndex = -1;
    private bool combatActive = false; // NEW: Flag to control combat state

    public Unit CurrentUnit => combatActive && currentTurnIndex >= 0 && currentTurnIndex < turnOrder.Count ? turnOrder[currentTurnIndex] : null;
    public bool IsPlayerTurn => CurrentUnit != null && CurrentUnit.Team == Unit.UnitTeam.Player;

    void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;
    }

    void Start()
    {
        StartCombat();
    }

    public void StartCombat()
    {
        // Ensure we only find active units in the scene that have UnitStats (implying they are combatants)
        turnOrder = FindObjectsByType<Unit>(FindObjectsSortMode.None)
            .Where(u => u.gameObject.activeInHierarchy && u.unitStats != null && !u.unitStats.IsDefeated()) // MODIFIED: Filter for active, non-defeated units
            .OrderByDescending(u => u.Echo)
            .ThenByDescending(u => u.Aura)
            .ThenBy(_ => UnityEngine.Random.value) // Ensure consistent tie-breaking if Echo and Aura are same
            .ToList();

        if (turnOrder.Count < 2) // Need at least two units (or one player vs one enemy) for combat
        {
            Debug.LogWarning("Not enough active units to start combat. Combat requires at least two opposing units or further logic.");
            combatActive = false;
            // Potentially invoke OnCombatEnd here with a draw or specific state
            return;
        }
        
        currentTurnIndex = -1; // Will be incremented to 0 by NextTurn
        combatActive = true; // NEW: Set combat as active
        Debug.Log("Turn order: " + string.Join(", ", turnOrder.Select(u => u.unitName)));
        NextTurn();
    }

    public void NextTurn()
    {
        if (!combatActive || turnOrder.Count == 0)
        {
            // Combat might have ended due to a unit dying and triggering a win condition,
            // or all units were defeated simultaneously.
            if (combatActive) // If it was active but now turn order is empty, check win
            {
                CheckWinCondition(); // This might set combatActive to false
            }
            if (!combatActive)
            {
                 Debug.Log("NextTurn called, but combat is not active or no units remain.");
            }
            return;
        }

        currentTurnIndex = (currentTurnIndex + 1) % turnOrder.Count;

        Unit current = turnOrder[currentTurnIndex];
        if (current == null || !current.gameObject.activeInHierarchy || (current.unitStats != null && current.unitStats.IsDefeated()))
        {
            // This unit might have been defeated by a counter-attack or status effect before its turn officially started
            // or was disabled externally. We should remove it and try next turn again.
            Debug.LogWarning($"Unit {current?.unitName ?? "Unknown"} found in turn order but is null, inactive, or defeated. Attempting to remove and advance.");
            if (current != null) UnitDied(current, false); // Call UnitDied without forcing next turn yet
            
            // To prevent infinite loop if all remaining units are bad, add a safety break or re-evaluate.
            // For now, let's try advancing once more. If issues persist, StartCombat might need more robust filtering.
            NextTurn(); // Recursive call, be careful. Could also re-evaluate index.
            return;
        }

        Debug.Log($"Now it's {current.unitName}'s turn.");
        current.BeginTurn();
        OnTurnChanged?.Invoke(); // Notify listeners (e.g., UI)
    }

    public void EndCurrentTurn()
    {
        if (!combatActive || CurrentUnit == null)
        {
            Debug.LogWarning("EndCurrentTurn called, but combat is not active or no current unit.");
            return;
        }

        CurrentUnit.EndTurn();
        NextTurn();
    }

    // NEW: UnitDied method
    /// <summary>
    /// Called by UnitCombat when a unit is defeated.
    /// </summary>
    /// <param name="deadUnit">The unit that was defeated.</param>
    /// <param name="forceNextTurnIfCurrent">If true and the dead unit was the current unit, force NextTurn.</param>
    public void UnitDied(Unit deadUnit, bool forceNextTurnIfCurrent = true)
    {
        if (!combatActive || deadUnit == null) return;

        int deadUnitIndex = turnOrder.IndexOf(deadUnit);
        if (deadUnitIndex != -1)
        {
            bool wasCurrentUnit = (deadUnitIndex == currentTurnIndex);
            
            turnOrder.RemoveAt(deadUnitIndex);
            Debug.Log($"{deadUnit.unitName} removed from turn order.");

            if (turnOrder.Count == 0)
            {
                CheckWinCondition(); // All units might be gone
                return;
            }

            // If the removed unit was before or at the current turn index,
            // the currentTurnIndex needs to be adjusted to point to the same logical unit (or the one after it).
            if (deadUnitIndex < currentTurnIndex)
            {
                currentTurnIndex--; // The list shifted, so the current unit is now at index-1
            }
            // If deadUnitIndex == currentTurnIndex, the current unit was removed.
            // currentTurnIndex now effectively points to the *next* unit in the list.
            // If deadUnitIndex > currentTurnIndex, no adjustment to currentTurnIndex is needed for this removal.

            // Ensure currentTurnIndex is valid after removal and adjustment
            if (currentTurnIndex >= turnOrder.Count)
            {
                // This can happen if the last unit in the list was removed,
                // or if the current unit was the last and got removed.
                currentTurnIndex = 0; // Wrap around for the next turn call
            }
            else if (currentTurnIndex < 0 && turnOrder.Count > 0) // Should not happen with current logic but safe check
            {
                currentTurnIndex = 0;
            }


            if (!CheckWinCondition()) // If combat hasn't ended
            {
                // If the unit that died was the one whose turn it currently was,
                // and we want to force the turn to advance.
                if (wasCurrentUnit && forceNextTurnIfCurrent)
                {
                    Debug.Log($"Current unit {deadUnit.unitName} died. Advancing turn.");
                    // currentTurnIndex is already effectively pointing at the "next" unit
                    // or has been adjusted. We need to make sure NextTurn is called correctly.
                    // Calling NextTurn directly might skip the BeginTurn for the *new* current unit if indices were tricky.
                    // A safer way after index adjustment:
                    // If the current unit died, its EndTurn was effectively its death.
                    // We should call NextTurn, but ensure the index is correctly set up for the *next* valid unit.
                    // The current NextTurn logic should handle this: it uses '%' so it will wrap.
                    // However, if the current unit died, we effectively skip its "EndTurn" call and go straight to next.
                    // The currentTurnIndex might have been decremented if an earlier unit died.
                    // If the current unit itself died, the index effectively becomes the "next" unit's index post-removal.
                    // If currentTurnIndex was pointing to the dead unit, then after removal and no adjustment,
                    // currentTurnIndex is now pointing to the *next* unit that shifted into that slot.
                    // So, calling NextTurn would INCORRECTLY increment it again.
                    // We should instead directly start the turn of the NEW CurrentUnit (if any).
                    
                    // Simpler: if the current unit died, its turn is over. Call NextTurn.
                    // The index logic needs to be robust. Let's refine the index handling above.
                    // If current unit died, currentTurnIndex now points to the one that took its place.
                    // We need to call NextTurn() but ensure it doesn't skip that unit.
                    // The cleanest might be to adjust currentTurnIndex to be "before" the new current unit,
                    // so NextTurn() correctly lands on it.
                    if (currentTurnIndex > 0) { // if not already at the start
                        currentTurnIndex--; // Set it so NextTurn() picks the one that took the dead unit's slot.
                    } else if (turnOrder.Count > 0) { // If it was the first unit that died
                        currentTurnIndex = turnOrder.Count - 1; // Set to last, so NextTurn wraps to 0
                    }
                    NextTurn();
                }
                // Else, if another unit died, the current unit's turn continues.
            }
        }
    }

    // NEW: CheckWinCondition method
    private bool CheckWinCondition()
    {
        if (!combatActive) return true; // Already ended

        if (turnOrder.Count == 0)
        {
            Debug.Log("COMBAT ENDED: No units remaining (Draw or error?).");
            combatActive = false;
            OnCombatEnd?.Invoke(Unit.UnitTeam.Neutral); // Indicate a draw or undefined end
            return true;
        }

        bool playersRemain = turnOrder.Any(u => u.Team == Unit.UnitTeam.Player && u.gameObject.activeInHierarchy && (u.unitStats != null && !u.unitStats.IsDefeated()));
        bool enemiesRemain = turnOrder.Any(u => u.Team == Unit.UnitTeam.Enemy && u.gameObject.activeInHierarchy && (u.unitStats != null && !u.unitStats.IsDefeated()));
        // bool neutralsRemain = turnOrder.Any(u => u.Team == Unit.UnitTeam.Neutral && ...); // If neutrals can affect win

        if (!playersRemain && enemiesRemain)
        {
            Debug.Log("PLAYER DEFEATED! Enemy team wins.");
            combatActive = false;
            OnCombatEnd?.Invoke(Unit.UnitTeam.Enemy); // Enemy team wins
            return true;
        }
        else if (!enemiesRemain && playersRemain)
        {
            Debug.Log("ENEMY DEFEATED! Player team wins.");
            combatActive = false;
            OnCombatEnd?.Invoke(Unit.UnitTeam.Player); // Player team wins
            return true;
        }
        else if (!playersRemain && !enemiesRemain) // Should be caught by turnOrder.Count == 0 ideally
        {
            Debug.Log("COMBAT ENDED: No players or enemies remaining (Mutual Destruction?).");
            combatActive = false;
            OnCombatEnd?.Invoke(Unit.UnitTeam.Neutral);
            return true;
        }
        
        return false; // Combat continues
    }
}