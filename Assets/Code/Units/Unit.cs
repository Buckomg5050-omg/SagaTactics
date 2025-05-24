using UnityEngine;

public class Unit : MonoBehaviour
{
    public enum UnitTeam { Player, Enemy, Neutral }

    public string unitName = "Unit"; // Will be synced with UnitStats.unitName if available
    // public bool isPlayerControlled = true; // Team enum + UnitInputHandler/EnemyAIController implies this

    public int Echo = 5;
    public int Aura = 3;

    public UnitInputHandler playerInput { get; private set; }
    public EnemyAIController aiController { get; private set; }
    public UnitAP unitAP { get; private set; }
    public UnitSelector unitSelector { get; private set; }
    public UnitStats unitStats { get; private set; }
    public UnitFacing unitFacing { get; private set; } // NEW: Reference to UnitFacing

    public UnitTeam Team { get; private set; }

    private TurnIndicatorUI turnIndicator;

    void Awake()
    {
        playerInput = GetComponent<UnitInputHandler>();
        aiController = GetComponent<EnemyAIController>();
        unitAP = GetComponent<UnitAP>();
        unitSelector = GetComponent<UnitSelector>();
        unitStats = GetComponent<UnitStats>();
        unitFacing = GetComponent<UnitFacing>(); // NEW: Get UnitFacing

        if (unitStats == null) Debug.LogError($"Unit '{gameObject.name}' is missing a UnitStats component!", this);
        if (unitAP == null) Debug.LogError($"Unit '{gameObject.name}' is missing a UnitAP component!", this);
        if (unitFacing == null) Debug.LogWarning($"Unit '{gameObject.name}' is missing a UnitFacing component. Facing logic might be limited.", this);
        
        if (unitStats != null && !string.IsNullOrEmpty(unitStats.unitName))
        {
            this.unitName = unitStats.unitName;
        }

        if (CompareTag("PlayerUnit")) Team = UnitTeam.Player;
        else if (CompareTag("EnemyUnit")) Team = Unit.UnitTeam.Enemy; // Corrected typo
        else Team = UnitTeam.Neutral;
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
        // ... (other logs)

        unitSelector?.SetSelected(true);

        // Active unit should initially hold its facing from end of last turn, or snap to a default action-ready facing.
        // Movement and attack commands will then dictate its facing.
        // We don't want it to immediately try to face camera if it's about to act.
        if (unitFacing != null)
        {
            // unitFacing.HoldCurrentFacing(); // Let actions during the turn define facing.
            // Or, if it just finished moving via AI and is now starting its "action" phase:
            // it would already be holding from UnitMover.
        }


        if (turnIndicator == null) turnIndicator = FindFirstObjectByType<TurnIndicatorUI>();
        turnIndicator?.ShowTurn(unitName, Team == UnitTeam.Player);

        if (Team == UnitTeam.Player && playerInput != null)
        {
            playerInput.EnableInput(true);
            // Debug.Log($"Unit ({unitName}): Called playerInput.EnableInput(true). playerInput.inputEnabled is now: {playerInput.IsInputEnabledForDebug()}", this);
        }
        else if (Team == UnitTeam.Enemy && aiController != null)
        {
            aiController.RunAI();
        }
    }

    public void EndTurn()
    {
        unitSelector?.SetSelected(false);

        if (Team == UnitTeam.Player && playerInput != null)
        {
            // Debug.Log($"Unit ({unitName}): Preparing to call playerInput.EnableInput(false). playerInput.inputEnabled is currently: {playerInput.IsInputEnabledForDebug()}", this);
            playerInput.EnableInput(false);
        }

        // When any unit's turn ends, it should now try to face the camera (idle behavior)
        // This will be reinforced by TacticalCombatManager for non-active units.
        if (unitFacing != null)
        {
            Debug.Log($"Unit ({unitName}): Turn ended. Telling UnitFacing to look towards camera.", this);
            unitFacing.SetTargetLookTowardsCamera();
        }
    }

    public bool ShouldAutoEndTurn()
    {
        return unitAP != null && (unitAP.CurrentAP <= 0);
    }
}