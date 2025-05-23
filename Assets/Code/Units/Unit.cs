using UnityEngine;

public class Unit : MonoBehaviour
{
    public enum UnitTeam { Player, Enemy, Neutral }

    public string unitName = "Unit";
    public bool isPlayerControlled = true;

    public int Echo = 5;
    public int Aura = 3;

    public UnitInputHandler playerInput { get; private set; }
    public EnemyAIController aiController { get; private set; }
    public UnitAP unitAP { get; private set; }
    public UnitSelector unitSelector { get; private set; }
    public UnitStats unitStats { get; private set; }

    public UnitTeam Team { get; private set; }

    private TurnIndicatorUI turnIndicator;

    void Awake()
    {
        playerInput = GetComponent<UnitInputHandler>();
        aiController = GetComponent<EnemyAIController>();
        unitAP = GetComponent<UnitAP>();
        unitSelector = GetComponent<UnitSelector>();
        unitStats = GetComponent<UnitStats>();

        if (unitStats == null)
        {
            Debug.LogError($"Unit '{unitName}' is missing a UnitStats component!", this);
        }
        if (unitAP == null)
        {
            Debug.LogError($"Unit '{unitName}' is missing a UnitAP component!", this);
        }
        
        if (unitStats != null && !string.IsNullOrEmpty(unitStats.unitName))
        {
            this.unitName = unitStats.unitName;
        }

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

        // MODIFIED: Enhanced Debug Log
        if (unitStats != null && unitAP != null)
        {
            Debug.Log($"{unitName} begins turn. " +
                      $"AP: {unitAP.CurrentAP}/{unitAP.MaxAP}, " +
                      $"HP: {unitStats.currentHealth}/{unitStats.maxHealth}, " +
                      $"Core: {unitStats.Core}, " +
                      $"Defense: {unitStats.Defense}");
        }
        else // Fallback if components are missing, though Awake should catch UnitStats/UnitAP nulls
        {
            Debug.LogWarning($"{unitName} begins turn. Stats or AP component missing for full debug log.");
            if (unitAP != null) Debug.Log($"{unitName} has {unitAP.CurrentAP} AP");
        }


        unitSelector?.SetSelected(true);

        if (turnIndicator == null)
            turnIndicator = FindFirstObjectByType<TurnIndicatorUI>();

        turnIndicator?.ShowTurn(unitName, Team == UnitTeam.Player);

        if (Team == UnitTeam.Player && playerInput != null)
        {
            playerInput.EnableInput(true);
            Debug.Log($"Unit ({unitName}): Called playerInput.EnableInput(true). playerInput.inputEnabled is now: {playerInput.IsInputEnabledForDebug()}", this); 
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
            Debug.Log($"Unit ({unitName}): Preparing to call playerInput.EnableInput(false). playerInput.inputEnabled is currently: {playerInput.IsInputEnabledForDebug()}", this);
            playerInput.EnableInput(false);
        }
    }

    public bool ShouldAutoEndTurn()
    {
        return unitAP != null && (unitAP.CurrentAP <= 0);
    }
}