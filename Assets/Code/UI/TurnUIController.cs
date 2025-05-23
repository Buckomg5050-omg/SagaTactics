using UnityEngine;
using UnityEngine.UI; // Required for Button type

public class TurnUIController : MonoBehaviour
{
    [Header("UI Button References")]
    [SerializeField] private Button endTurnButton;
    [SerializeField] private Button attackModeButton; // NEW: Assign your AttackButton here in Inspector

    // No need to store inputHandler globally here anymore, we'll get it from CurrentUnit

    private void Start()
    {
        if (endTurnButton == null)
        {
            Debug.LogError("End Turn Button not assigned in TurnUIController Inspector!", this);
        }
        else
        {
            endTurnButton.onClick.AddListener(OnEndTurnButtonClick); // Renamed for clarity
        }

        if (attackModeButton == null)
        {
            Debug.LogError("Attack Mode Button not assigned in TurnUIController Inspector!", this);
        }
        else
        {
            attackModeButton.onClick.AddListener(OnAttackModeToggleButtonClick); // NEW
        }

        TacticalCombatManager.OnTurnChanged += HandleTurnChanged; // Renamed listener
        UpdateButtonInteractability(); // Initial state set
    }

    private void OnDestroy()
    {
        // Always good to remove listeners
        if (endTurnButton != null) endTurnButton.onClick.RemoveListener(OnEndTurnButtonClick);
        if (attackModeButton != null) attackModeButton.onClick.RemoveListener(OnAttackModeToggleButtonClick);
        TacticalCombatManager.OnTurnChanged -= HandleTurnChanged;
    }

    private void HandleTurnChanged() // Renamed from UpdateButtonState for clarity
    {
        UpdateButtonInteractability();
    }

    private void UpdateButtonInteractability()
    {
        if (TacticalCombatManager.Instance == null)
        {
            // Manager might not be ready yet, disable buttons
            if (endTurnButton != null) endTurnButton.interactable = false;
            if (attackModeButton != null) attackModeButton.interactable = false;
            return;
        }

        bool isPlayerUnitActive = TacticalCombatManager.Instance.CurrentUnit != null &&
                                  TacticalCombatManager.Instance.CurrentUnit.Team == Unit.UnitTeam.Player &&
                                  TacticalCombatManager.Instance.CurrentUnit.gameObject.activeInHierarchy; 
                                  // Added activeInHierarchy check

        if (endTurnButton != null)
        {
            endTurnButton.interactable = isPlayerUnitActive;
        }

        if (attackModeButton != null)
        {
            bool canAttack = false;
            if (isPlayerUnitActive)
            {
                UnitCombat combat = TacticalCombatManager.Instance.CurrentUnit.GetComponent<UnitCombat>();
                if (combat != null)
                {
                    canAttack = combat.CanConsiderAttacking(); // Check if unit can even enter attack mode
                }
            }
            attackModeButton.interactable = canAttack;
        }
    }

    // This method is called by the End Turn UI Button's OnClick event
    public void OnEndTurnButtonClick() // Renamed for clarity
    {
        if (TacticalCombatManager.Instance == null || TacticalCombatManager.Instance.CurrentUnit == null) return;

        Unit currentUnit = TacticalCombatManager.Instance.CurrentUnit;
        if (currentUnit.Team == Unit.UnitTeam.Player)
        {
            UnitInputHandler inputHandler = currentUnit.GetComponent<UnitInputHandler>();
            if (inputHandler != null && inputHandler.enabled) // Check if input handler is active
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

    // NEW: This method is called by the Attack Mode UI Button's OnClick event
    public void OnAttackModeToggleButtonClick()
    {
        if (TacticalCombatManager.Instance == null || TacticalCombatManager.Instance.CurrentUnit == null) return;
        
        Unit currentUnit = TacticalCombatManager.Instance.CurrentUnit;
        if (currentUnit.Team == Unit.UnitTeam.Player)
        {
            UnitInputHandler inputHandler = currentUnit.GetComponent<UnitInputHandler>();
            if (inputHandler != null && inputHandler.enabled) // Check if input handler is active
            {
                // Add a log here to confirm this proxy method is being called
                Debug.Log($"TurnUIController: OnAttackModeToggleButtonClick called for unit {currentUnit.unitName}. Instance ID: {inputHandler.GetInstanceID()}", this);
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