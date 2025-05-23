using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class TacticalCombatManager : MonoBehaviour
{
    public static TacticalCombatManager Instance { get; private set; }

    public static event Action OnTurnChanged;

    private List<Unit> turnOrder = new();
    private int currentTurnIndex = -1;

    public Unit CurrentUnit => currentTurnIndex >= 0 && currentTurnIndex < turnOrder.Count ? turnOrder[currentTurnIndex] : null;
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
            .OrderByDescending(u => u.Echo)
            .ThenByDescending(u => u.Aura)
            .ThenBy(_ => UnityEngine.Random.value)
            .ToList();

        Debug.Log("Turn order: " + string.Join(", ", turnOrder.Select(u => u.unitName)));
        NextTurn();
    }

    public void NextTurn()
    {
        currentTurnIndex = (currentTurnIndex + 1) % turnOrder.Count;

        Unit current = turnOrder[currentTurnIndex];
        Debug.Log($"Now it's {current.unitName}'s turn.");

        current.BeginTurn();
        OnTurnChanged?.Invoke(); // Notify listeners (e.g., UI)
    }

    public void EndCurrentTurn()
    {
        if (currentTurnIndex < 0 || currentTurnIndex >= turnOrder.Count)
            return;

        Unit current = turnOrder[currentTurnIndex];
        current.EndTurn();

        NextTurn();
    }
}
