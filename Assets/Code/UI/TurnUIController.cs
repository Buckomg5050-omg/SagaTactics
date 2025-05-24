using UnityEngine;
using UnityEngine.UI; // Required for Button type
// using YourGame.Units; // Add this if your Unit class is in a namespace

public class TurnUIController : MonoBehaviour
{
    [Header("UI Button References")]
    [SerializeField] private Button endTurnButton;
    [SerializeField] private Button attackModeButton;

    // [Header("UI Text References")] // Consider adding this if you want to display current unit name
    // [SerializeField] private TMPro.TextMeshProUGUI currentUnitNameText; // Example for TextMeshPro

    private void Start()
    {
        if (endTurnButton == null)
        {
            Debug.LogError("End Turn Button not assigned in TurnUIController Inspector!", this);
        }
        else
        {
            endTurnButton.onClick.AddListener(OnEndTurnButtonClick);
        }

        if (attackModeButton == null)
        {
            Debug.LogError("Attack Mode Button not assigned in TurnUIController Inspector!", this);
        }
        else
        {
            attackModeButton.onClick.AddListener(OnAttackModeToggleButtonClick);
        }

        // Subscribe to the CORRECT event from TacticalCombatManager
        if (TacticalCombatManager.Instance != null)
        {
            TacticalCombatManager.OnActiveUnitChanged += HandleActiveUnitChanged;
        }
        else
        {
            // This could happen if TurnUIController's Start runs before TCM's Awake.
            // A more robust solution might involve a GameManager that ensures order,
            // or having TCM invoke an "Initialized" event that other managers subscribe to for their own setup.
            Debug.LogWarning("TurnUIController: TacticalCombatManager.Instance is null on Start. Event subscription might fail if TCM initializes later.");
        }
        UpdateButtonInteractability(TacticalCombatManager.Instance?.CurrentUnit); // Initial state set, pass current unit if available
    }

    private void OnDestroy()
    {
        if (endTurnButton != null) endTurnButton.onClick.RemoveListener(OnEndTurnButtonClick);
        if (attackModeButton != null) attackModeButton.onClick.RemoveListener(OnAttackModeToggleButtonClick);
        
        // Unsubscribe from the CORRECT event
        // No need to check for TacticalCombatManager.Instance != null here for static event unsubscription
        TacticalCombatManager.OnActiveUnitChanged -= HandleActiveUnitChanged;
    }

    // MODIFIED: Handler now accepts a Unit parameter
    private void HandleActiveUnitChanged(Unit activeUnit)
    {
        // activeUnit can be null if combat ends or no unit is active
        // Debug.Log($"TurnUIController: HandleActiveUnitChanged - New active unit: {activeUnit?.unitName ?? "None"}");
        UpdateButtonInteractability(activeUnit);

        // If you added currentUnitNameText:
        // if (currentUnitNameText != null)
        // {
        //     currentUnitNameText.text = activeUnit != null ? $"{activeUnit.unitName}'s Turn" : "Waiting...";
        // }
    }

    // MODIFIED: Method now accepts the current unit to avoid repeated calls to TCM.Instance.CurrentUnit
    private void UpdateButtonInteractability(Unit currentUnitToEvaluate)
    {
        if (TacticalCombatManager.Instance == null) // Should ideally not happen if Start() logic is robust
        {
            if (endTurnButton != null) endTurnButton.interactable = false;
            if (attackModeButton != null) attackModeButton.interactable = false;
            return;
        }

        bool isPlayerUnitActive = currentUnitToEvaluate != null &&
                                  currentUnitToEvaluate.Team == Unit.UnitTeam.Player &&
                                  currentUnitToEvaluate.gameObject.activeInHierarchy;

        if (endTurnButton != null)
        {
            endTurnButton.interactable = isPlayerUnitActive;
        }

        if (attackModeButton != null)
        {
            bool canAttack = false;
            if (isPlayerUnitActive)
            {
                UnitCombat combat = currentUnitToEvaluate.GetComponent<UnitCombat>();
                if (combat != null)
                {
                    canAttack = combat.CanConsiderAttacking();
                }
            }
            attackModeButton.interactable = canAttack;
        }
    }

    public void OnEndTurnButtonClick()
    {
        if (TacticalCombatManager.Instance == null || TacticalCombatManager.Instance.CurrentUnit == null) return;

        Unit currentUnit = TacticalCombatManager.Instance.CurrentUnit; // This is fine for direct action
        if (currentUnit.Team == Unit.UnitTeam.Player)
        {
            UnitInputHandler inputHandler = currentUnit.GetComponent<UnitInputHandler>();
            if (inputHandler != null && inputHandler.enabled)
            {
                inputHandler.EndTurn();
            }
            else
            {
                Debug.LogWarning("End Turn button clicked, but current unit has no enabled UnitInputHandler.", currentUnit);
            }
        }
        else
        {
            Debug.LogWarning("End Turn button clicked, but it's not a player unit's turn.", currentUnit);
        }
    }

    public void OnAttackModeToggleButtonClick()
    {
        if (TacticalCombatManager.Instance == null || TacticalCombatManager.Instance.CurrentUnit == null) return;
        
        Unit currentUnit = TacticalCombatManager.Instance.CurrentUnit; // Fine for direct action
        if (currentUnit.Team == Unit.UnitTeam.Player)
        {
            UnitInputHandler inputHandler = currentUnit.GetComponent<UnitInputHandler>();
            if (inputHandler != null && inputHandler.enabled)
            {
                // Debug.Log($"TurnUIController: OnAttackModeToggleButtonClick called for unit {currentUnit.unitName}. Instance ID: {inputHandler.GetInstanceID()}", this);
                inputHandler.HandleAttackModeToggle();
            }
            else
            {
                Debug.LogWarning("Attack Mode button clicked, but current unit has no enabled UnitInputHandler.", currentUnit);
            }
        }
        else
        {
            Debug.LogWarning("Attack Mode button clicked, but it's not a player unit's turn.", currentUnit);
        }
    }
}