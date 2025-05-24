using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class TacticalCombatManager : MonoBehaviour
{
    public static TacticalCombatManager Instance { get; private set; }

    public static event Action OnTurnChanged;
    public static event Action<Unit.UnitTeam> OnCombatEnd; 

    private List<Unit> turnOrder = new();
    private int currentTurnIndex = -1;
    private bool combatActive = false; 

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
        turnOrder = FindObjectsByType<Unit>(FindObjectsSortMode.None)
            .Where(u => u.gameObject.activeInHierarchy && u.unitStats != null && !u.unitStats.IsDefeated()) 
            .OrderByDescending(u => u.Echo)
            .ThenByDescending(u => u.Aura)
            .ThenBy(_ => UnityEngine.Random.value) 
            .ToList();

        if (turnOrder.Count < 2) 
        {
            Debug.LogWarning("Not enough active units to start combat. Combat requires at least two opposing units or further logic.");
            combatActive = false;
            return;
        }
        
        currentTurnIndex = -1; 
        combatActive = true; 
        Debug.Log("Turn order: " + string.Join(", ", turnOrder.Select(u => u.unitName)));
        NextTurn();
    }

    public void NextTurn()
    {
        if (!combatActive || turnOrder.Count == 0)
        {
            if (combatActive) 
            {
                CheckWinCondition(); 
            }
            if (!combatActive)
            {
                 Debug.Log("NextTurn called, but combat is not active or no units remain.");
            }
            return;
        }

        // Tell the PREVIOUS unit (if any, and not the new current unit) to face camera if idle
        Unit previousUnit = CurrentUnit; // Get unit that was current before index change (could be null if first turn)

        currentTurnIndex = (currentTurnIndex + 1) % turnOrder.Count;
        Unit newCurrentUnit = turnOrder[currentTurnIndex];

        if (previousUnit != null && previousUnit != newCurrentUnit && previousUnit.gameObject.activeInHierarchy)
        {
            UnitFacing prevFacing = previousUnit.GetComponent<UnitFacing>();
            UnitMover prevMover = previousUnit.GetComponent<UnitMover>();
            if (prevFacing != null && (prevMover == null || !prevMover.IsMoving)) 
            {
                Debug.Log($"TCM: Telling {previousUnit.unitName} to look towards camera as its turn ended.", previousUnit);
                prevFacing.SetTargetLookTowardsCamera();
            }
        }
            
        if (newCurrentUnit == null || !newCurrentUnit.gameObject.activeInHierarchy || (newCurrentUnit.unitStats != null && newCurrentUnit.unitStats.IsDefeated()))
        {
            Debug.LogWarning($"Unit {newCurrentUnit?.unitName ?? "Unknown"} found in turn order but is null, inactive, or defeated. Attempting to remove and advance.");
            if (newCurrentUnit != null) UnitDied(newCurrentUnit, false); 
            
            NextTurn(); 
            return;
        }

        Debug.Log($"Now it's {newCurrentUnit.unitName}'s turn.");
        newCurrentUnit.BeginTurn();
        OnTurnChanged?.Invoke(); 
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

    public void UnitDied(Unit deadUnit, bool forceNextTurnIfCurrent = true)
    {
        if (!combatActive || deadUnit == null) return;

        int deadUnitIndex = turnOrder.IndexOf(deadUnit);
        if (deadUnitIndex != -1)
        {
            bool wasCurrentUnit = (CurrentUnit == deadUnit); // Check if the unit dying IS the current unit
            
            turnOrder.RemoveAt(deadUnitIndex);
            Debug.Log($"{deadUnit.unitName} removed from turn order.");

            if (turnOrder.Count == 0)
            {
                CheckWinCondition(); 
                return;
            }

            if (wasCurrentUnit)
            {
                // If the current unit died, the currentTurnIndex now points to the unit that took its place,
                // or is out of bounds if it was the last. We need NextTurn to pick up from the "new" current unit.
                // To do this, we decrement currentTurnIndex so that NextTurn's increment lands correctly.
                currentTurnIndex--; 
                if (currentTurnIndex < -1) currentTurnIndex = -1; // Safety, should not happen often
            }
            else if (deadUnitIndex < currentTurnIndex)
            {
                currentTurnIndex--; 
            }
            // If deadUnitIndex > currentTurnIndex, no adjustment to currentTurnIndex is needed for this removal.

            if (!CheckWinCondition()) 
            {
                if (wasCurrentUnit && forceNextTurnIfCurrent)
                {
                    Debug.Log($"Current unit {deadUnit.unitName} died. Advancing turn.");
                    NextTurn(); // This will now correctly pick the next unit or wrap around
                }
            }
        }
    }

    private bool CheckWinCondition()
    {
        if (!combatActive) return true; 

        if (turnOrder.Count == 0)
        {
            Debug.Log("COMBAT ENDED: No units remaining (Draw or error?).");
            combatActive = false;
            OnCombatEnd?.Invoke(Unit.UnitTeam.Neutral); 
            return true;
        }

        bool playersRemain = turnOrder.Any(u => u.Team == Unit.UnitTeam.Player && u.gameObject.activeInHierarchy && (u.unitStats != null && !u.unitStats.IsDefeated()));
        bool enemiesRemain = turnOrder.Any(u => u.Team == Unit.UnitTeam.Enemy && u.gameObject.activeInHierarchy && (u.unitStats != null && !u.unitStats.IsDefeated()));
        
        if (!playersRemain && enemiesRemain)
        {
            Debug.Log("PLAYER DEFEATED! Enemy team wins.");
            combatActive = false;
            OnCombatEnd?.Invoke(Unit.UnitTeam.Enemy); 
            return true;
        }
        else if (!enemiesRemain && playersRemain)
        {
            Debug.Log("ENEMY DEFEATED! Player team wins.");
            combatActive = false;
            OnCombatEnd?.Invoke(Unit.UnitTeam.Player); 
            return true;
        }
        else if (!playersRemain && !enemiesRemain) 
        {
            Debug.Log("COMBAT ENDED: No players or enemies remaining (Mutual Destruction?).");
            combatActive = false;
            OnCombatEnd?.Invoke(Unit.UnitTeam.Neutral);
            return true;
        }
        
        return false; 
    }
}