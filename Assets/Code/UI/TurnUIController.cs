using UnityEngine;
using UnityEngine.UI;

public class TurnUIController : MonoBehaviour
{
    [SerializeField] private Button endTurnButton;
    private UnitInputHandler inputHandler;

    private void Start()
    {
        inputHandler = FindFirstObjectByType<UnitInputHandler>();

        if (endTurnButton != null)
            endTurnButton.onClick.AddListener(OnEndTurnClicked);

        TacticalCombatManager.OnTurnChanged += UpdateButtonState;
        UpdateButtonState(); // Initial state
    }

    private void OnDestroy()
    {
        TacticalCombatManager.OnTurnChanged -= UpdateButtonState;
    }

    private void UpdateButtonState()
    {
        bool isPlayerTurn = TacticalCombatManager.Instance != null && TacticalCombatManager.Instance.IsPlayerTurn;
        endTurnButton.interactable = isPlayerTurn;
    }

    private void OnEndTurnClicked()
    {
        if (inputHandler != null && TacticalCombatManager.Instance.IsPlayerTurn)
            inputHandler.EndTurn();
    }
}
