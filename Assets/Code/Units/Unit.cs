using UnityEngine;

public class Unit : MonoBehaviour
{
    public enum UnitTeam { Player, Enemy, Neutral }

    public string unitName = "Unit";
    public bool isPlayerControlled = true;

    public int Echo = 5;
    public int Aura = 3;

    public UnitInputHandler playerInput;
    public EnemyAIController aiController;
    public UnitAP unitAP;
    public UnitSelector unitSelector;

    public UnitTeam Team { get; private set; }

    private TurnIndicatorUI turnIndicator;

    void Awake()
    {
        playerInput = GetComponent<UnitInputHandler>();
        aiController = GetComponent<EnemyAIController>();
        unitAP = GetComponent<UnitAP>();
        unitSelector = GetComponent<UnitSelector>();

        // Determine team from tag
        if (CompareTag("PlayerUnit"))
            Team = UnitTeam.Player;
        else if (CompareTag("EnemyUnit"))
            Team = UnitTeam.Enemy;
        else
            Team = UnitTeam.Neutral;
    }

    public void BeginTurn()
    {
        unitAP?.RestoreFull();
        Debug.Log($"{unitName} begins turn with {unitAP.CurrentAP} AP");

        unitSelector?.SetSelected(true);

        // Update UI turn text using Unity's recommended method
        if (turnIndicator == null)
            turnIndicator = FindFirstObjectByType<TurnIndicatorUI>();

        turnIndicator?.ShowTurn(unitName, Team == UnitTeam.Player);

        if (Team == UnitTeam.Player)
        {
            playerInput?.EnableInput(true);
        }
        else if (Team == UnitTeam.Enemy)
        {
            aiController?.RunAI();
        }
    }

    public void EndTurn()
    {
        unitSelector?.SetSelected(false);

        if (Team == UnitTeam.Player)
        {
            playerInput?.EnableInput(false);
        }
    }

    public bool ShouldAutoEndTurn()
    {
        return unitAP != null && unitAP.CurrentAP <= 0;
    }
}
