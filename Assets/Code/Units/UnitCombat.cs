using UnityEngine;

[RequireComponent(typeof(Unit))] // Unit.cs already gets UnitStats and UnitAP
public class UnitCombat : MonoBehaviour
{
    private Unit unit; // Reference to the core Unit data
    private UnitStats stats; // Convenience reference to unit.unitStats
    private UnitAP unitAP; // Reference to the unit's AP
    private Animator animator; // Reference to the Animator for combat animations

    // Constants for attack (we'll use these more in the next steps)
    public const int MELEE_ATTACK_AP_COST = 1; // Example AP cost for a basic melee
    public const int MELEE_ATTACK_RANGE = 1;   // Example range (adjacent hexes)

    // Optional: VFX Prefabs - assign these in the Inspector on the Unit Prefab later if you have them
    // public GameObject hitVFXPrefab;
    // public GameObject deathVFXPrefab;

    void Awake()
    {
        unit = GetComponent<Unit>();
        stats = unit.unitStats; // Get stats via the Unit component
        unitAP = unit.unitAP;   // Get AP via the Unit component
        animator = GetComponentInChildren<Animator>(); // Assumes Animator is on a child or this object

        if (stats == null)
        {
            Debug.LogError($"UnitCombat on {unit.unitName} could not find UnitStats component via Unit.cs!", this);
        }
        if (unitAP == null)
        {
            Debug.LogError($"UnitCombat on {unit.unitName} could not find UnitAP component via Unit.cs!", this);
        }
    }

    // --- Health, Damage, and Death Logic ---

    /// <summary>
    /// Processes incoming damage to this unit.
    /// </summary>
    /// <param name="rawDamageAmount">The initial amount of damage before defense.</param>
    public void ProcessIncomingDamage(int rawDamageAmount)
    {
        if (IsDead()) return; // Already defeated, no further action

        // Let UnitStats handle the actual health reduction and defense calculation
        stats.TakeDamage(rawDamageAmount); 

        // Log is already in UnitStats.TakeDamage, but we can add one here too if needed for combat flow
        // Debug.Log($"{unit.unitName} is processing damage. Current HP: {stats.currentHealth}/{stats.maxHealth}");

        if (animator != null)
        {
            animator.SetTrigger("Take_Hit"); // Assumes "Take_Hit" trigger exists in Animator
        }
        // if (hitVFXPrefab != null) Instantiate(hitVFXPrefab, transform.position, Quaternion.identity);

        if (stats.IsDefeated())
        {
            HandleDefeat();
        }
    }

    /// <summary>
    /// Handles the unit's defeat sequence.
    /// </summary>
    private void HandleDefeat()
    {
        Debug.Log($"{unit.unitName} has been defeated (handled by UnitCombat)!");

        if (animator != null)
        {
            animator.SetTrigger("Die"); // Assumes "Die" trigger exists in Animator
        }
        // if (deathVFXPrefab != null) Instantiate(deathVFXPrefab, transform.position, Quaternion.identity);
        
        // Notify the TacticalCombatManager
        TacticalCombatManager.Instance?.UnitDied(unit); 

        // Disable the unit's GameObject.
        // For a prototype, SetActive(false) is fine.
        // Later, you might want a "dead" state, disable specific components, or use an object pool.
        gameObject.SetActive(false); 
        // Or: Destroy(gameObject, 2f); // To remove after a delay (e.g., for death animation to play)
    }

    /// <summary>
    /// Checks if the unit is currently defeated (health <= 0).
    /// </summary>
    public bool IsDead()
    {
        if (stats == null) return true; // If no stats, assume it can't fight
        return stats.IsDefeated();
    }

    // --- Attack Action Stubs (to be implemented next) ---

    /// <summary>
    /// Checks if the unit can generally initiate any attack (e.g., has AP, not dead).
    /// Does not check for specific ability costs or targets yet.
    /// </summary>
    public bool CanConsiderAttacking()
    {
        // For now, let's assume any attack costs at least 1 AP
        return unitAP != null && unitAP.CanSpend(1) && !IsDead();
    }

    /// <summary>
    /// Placeholder for performing a melee attack.
    /// Actual implementation will require target selection, range checks, AP cost, damage calculation.
    /// </summary>
    /// <param name="targetUnit">The unit to attack.</param>
    public void PerformMeleeAttack(Unit targetUnit)
    {
        // This is a more detailed stub than before, anticipating next steps
        if (targetUnit == null || targetUnit.GetComponent<UnitCombat>().IsDead())
        {
            Debug.LogWarning($"{unit.unitName} tried to attack an invalid or dead target.");
            return;
        }

        if (!unitAP.CanSpend(MELEE_ATTACK_AP_COST))
        {
            Debug.LogWarning($"{unit.unitName} does not have enough AP for a melee attack ({MELEE_ATTACK_AP_COST} AP needed).");
            return;
        }

        // TODO STEP: Implement range check here (e.g., using HexUtils.HexDistance)
        // For now, we assume range check happened before calling this (e.g., in UnitInputHandler)

        // --- If all checks pass: ---
        unitAP.Spend(MELEE_ATTACK_AP_COST);
        
        // Face the target (if UnitMover and FaceTarget exist)
        UnitMover mover = unit.GetComponent<UnitMover>();
        if (mover != null)
        {
            mover.FaceTarget(targetUnit.transform.position);
        }

        Debug.Log($"{unit.unitName} performs MELEE ATTACK on {targetUnit.unitName}!");
        if (animator != null)
        {
            animator.SetTrigger("Attack_Melee"); // Assumes "Attack_Melee" trigger
        }

        // Calculate damage (using GDD Power Strike like formula as example)
        // (unit.core from Unit.cs is now stats.Core from UnitStats.cs)
        int rawDamage = 10 + Mathf.FloorToInt(stats.Core * 0.5f); 

        // Apply damage to target
        UnitCombat targetCombat = targetUnit.GetComponent<UnitCombat>();
        if (targetCombat != null)
        {
            targetCombat.ProcessIncomingDamage(rawDamage);
        }
        else
        {
            Debug.LogError($"Target unit {targetUnit.unitName} is missing UnitCombat component!");
        }

        // Check if this unit should auto-end its turn (e.g., out of AP)
        if (unit.ShouldAutoEndTurn())
        {
            TacticalCombatManager.Instance?.EndCurrentTurn();
        }
    }
}