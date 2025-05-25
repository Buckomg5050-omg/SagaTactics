// File: Unit.cs
using UnityEngine;

public class Unit : MonoBehaviour
{
    public enum UnitTeam { Player, Enemy, Neutral }

    public string unitName = "Unit"; 
    
    public int Echo = 5; // For initiative
    public int Aura = 3; // For initiative tie-breaking

    // Component References
    public UnitInputHandler playerInput { get; private set; }
    public EnemyAIController aiController { get; private set; }
    public UnitAP unitAP { get; private set; }
    public UnitSelector unitSelector { get; private set; }
    public UnitStats unitStats { get; private set; }
    public UnitFacing unitFacing { get; private set; }
    public UnitMover unitMover { get; private set; } // Added for completeness if needed elsewhere

    public UnitTeam Team { get; private set; }

    private TurnIndicatorUI turnIndicator; // Assuming this is your UI for turns

    void Awake()
    {
        playerInput = GetComponent<UnitInputHandler>();
        aiController = GetComponent<EnemyAIController>();
        unitAP = GetComponent<UnitAP>();
        unitSelector = GetComponent<UnitSelector>();
        unitStats = GetComponent<UnitStats>();
        unitFacing = GetComponent<UnitFacing>();
        unitMover = GetComponent<UnitMover>(); // Initialize Mover

        // Critical component checks
        if (unitStats == null) Debug.LogError($"Unit '{gameObject.name}' is missing a UnitStats component!", this);
        if (unitAP == null) Debug.LogError($"Unit '{gameObject.name}' is missing a UnitAP component!", this);
        if (unitMover == null) Debug.LogWarning($"Unit '{gameObject.name}' is missing a UnitMover component. Movement might be impaired.", this);
        // UnitFacing is optional per earlier logs, so a warning is fine.
        if (unitFacing == null) Debug.LogWarning($"Unit '{gameObject.name}' is missing a UnitFacing component. Facing logic might be limited.", this);
        
        // Sync unitName from UnitStats if available
        if (unitStats != null && !string.IsNullOrEmpty(unitStats.unitName))
        {
            this.unitName = unitStats.unitName;
        }

        // Determine Team based on GameObject Tag
        if (CompareTag("PlayerUnit"))
        {
            Team = UnitTeam.Player;
            if (aiController != null) aiController.enabled = false; // Disable AI for player units
            if (playerInput != null) playerInput.enabled = true; // Ensure input handler is enabled for player
        }
        else if (CompareTag("EnemyUnit"))
        {
            Team = UnitTeam.Enemy;
            if (playerInput != null) playerInput.enabled = false; // Disable PlayerInput for AI units
            if (aiController != null) aiController.enabled = true; // Ensure AI is enabled
        }
        else
        {
            Team = UnitTeam.Neutral;
            if (playerInput != null) playerInput.enabled = false;
            if (aiController != null) aiController.enabled = false;
        }
        // Note: UnitInputHandler's internal 'inputEnabled' flag is managed by HandleActiveUnitChanged
        // based on whose turn it is, so we don't need to call an EnableInput method here anymore.
    }

    public void BeginTurn()
    {
        unitAP?.RestoreFull();

        if (unitStats != null && unitAP != null)
        {
            Debug.Log($"{unitName} begins turn. " +
                      $"AP: {unitAP.CurrentAP}/{unitAP.MaxAP}, " +
                      $"HP: {unitStats.currentHealth}/{unitStats.maxHealth}, " +
                      $"Core: {unitStats.Core}, " +
                      $"Defense: {unitStats.Defense}");
        }
        
        unitSelector?.SetSelected(true); // Visually select the unit

        if (turnIndicator == null) turnIndicator = FindFirstObjectByType<TurnIndicatorUI>();
        turnIndicator?.ShowTurn(unitName, Team == UnitTeam.Player);

        // UnitInputHandler is now responsible for enabling/disabling its own input processing
        // based on the OnActiveUnitChanged event from TacticalCombatManager.
        // So, no direct call to playerInput.EnableInput(true); is needed here.

        if (Team == UnitTeam.Enemy && aiController != null && aiController.enabled)
        {
            aiController.RunAI();
        }
    }

    public void EndTurn()
    {
        unitSelector?.SetSelected(false); // Visually deselect the unit

        // UnitInputHandler is now responsible for enabling/disabling its own input processing
        // based on the OnActiveUnitChanged event from TacticalCombatManager.
        // So, no direct call to playerInput.EnableInput(false); is needed here.

        if (unitFacing != null)
        {
            // Debug.Log($"Unit ({unitName}): Turn ended. Telling UnitFacing to look towards camera.", this);
            unitFacing.SetTargetLookTowardsCamera();
        }
    }

    public bool ShouldAutoEndTurn()
    {
        // Ends turn if out of AP or cannot perform any more meaningful actions.
        // This could be more complex, e.g., if unit has 0 AP actions.
        return unitAP != null && (unitAP.CurrentAP <= 0);
    }
}