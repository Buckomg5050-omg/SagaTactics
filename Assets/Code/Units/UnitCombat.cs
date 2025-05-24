using UnityEngine;
using System.Collections;
using System.Collections.Generic; // For List

[RequireComponent(typeof(Unit))]
public class UnitCombat : MonoBehaviour
{
    private Unit unit;
    private UnitStats stats;
    private UnitAP unitAP;
    private Animator animator;

    public const int MELEE_ATTACK_AP_COST = 1;
    public const int MELEE_ATTACK_RANGE = 1;
    
    [Header("Timing Delays (from start of hit processing)")]
    public float sfxHitDelay = 0.0f; 
    public float vfxHitDelay = 0.5f;         // Independent delay for VFX
    public float takeHitAnimationDelay = 0.5f; // Independent delay for Take_Hit animation
    public float deathAnimationDuration = 4.0f;

    [Header("Hit Effects")]
    public GameObject hitImpactVFX_Prefab; 
    public AudioClip sfx_MeleeHit;
    public Vector3 vfxSpawnOffset = new Vector3(0f, 0.5f, 0f);
        
    private AudioSource unitAudioSource; 

    void Awake()
    {
        unit = GetComponent<Unit>();
        stats = unit.unitStats;
        unitAP = unit.unitAP;
        animator = GetComponentInChildren<Animator>();

        if (unit == null) Debug.LogError($"UnitCombat on {gameObject.name} is missing its Unit component reference!", this);
        if (stats == null) Debug.LogError($"UnitCombat on {unit?.unitName ?? gameObject.name} could not find UnitStats component via Unit.cs!", this);
        if (unitAP == null) Debug.LogError($"UnitCombat on {unit?.unitName ?? gameObject.name} could not find UnitAP component via Unit.cs!", this);

        unitAudioSource = GetComponent<AudioSource>();
        if (unitAudioSource == null)
        {
            unitAudioSource = gameObject.AddComponent<AudioSource>();
            unitAudioSource.playOnAwake = false;
        }
    }

    public void ProcessIncomingDamage(int rawDamageAmount)
    {
        if (IsDead()) return;

        stats.TakeDamage(rawDamageAmount);

        if (!stats.IsDefeated()) 
        {
            StartCoroutine(PlayHitEffectsAndAnimationCoroutine());
        }
        else 
        {
            StartCoroutine(DeathSequenceCoroutine());
        }
    }

    private class TimedEvent
    {
        public float TargetTime;
        public System.Action Action;
        public bool IsTriggered;
        public string Name;

        public TimedEvent(string name, float targetTime, System.Action action)
        {
            Name = name;
            TargetTime = Mathf.Max(0, targetTime); // Ensure non-negative
            Action = action;
            IsTriggered = false;
        }
    }

    private IEnumerator PlayHitEffectsAndAnimationCoroutine()
    {
        // Create a list of events to manage
        List<TimedEvent> events = new List<TimedEvent>();

        // Add SFX event
        if (sfx_MeleeHit != null && unitAudioSource != null)
        {
            events.Add(new TimedEvent("SFX", sfxHitDelay, () => {
                unitAudioSource.PlayOneShot(sfx_MeleeHit);
                Debug.Log($"{unit.unitName} playing sfx_MeleeHit (Targeted at {sfxHitDelay:F2}s).", this);
            }));
        }

        // Add VFX event
        if (hitImpactVFX_Prefab != null) // Check prefab existence before adding event
        {
             events.Add(new TimedEvent("VFX", vfxHitDelay, () => {
                if (stats != null && !stats.IsDefeated()) // Re-check status at execution time
                {
                    Vector3 spawnPosition = transform.position + vfxSpawnOffset;
                    Debug.Log($"Attempting to instantiate VFX: '{hitImpactVFX_Prefab.name}' at {spawnPosition} for {unit.unitName} (Targeted at {vfxHitDelay:F2}s).", this);
                    Instantiate(hitImpactVFX_Prefab, spawnPosition, Quaternion.identity);
                }
            }));
        } else {
            Debug.LogWarning($"UNITCOMBAT ({unit.unitName}): hitImpactVFX_Prefab is NULL, VFX event will not be scheduled.", this);
        }


        // Add Animation event
        if (animator != null) // Check animator existence
        {
            events.Add(new TimedEvent("Animation", takeHitAnimationDelay, () => {
                 if (stats != null && !stats.IsDefeated()) // Re-check status
                 {
                    animator.SetTrigger("Take_Hit"); 
                    Debug.Log($"{unit.unitName} playing Take_Hit animation (Targeted at {takeHitAnimationDelay:F2}s).", this);
                 }
            }));
        }


        float currentTime = 0f;
        int eventsTriggeredCount = 0;
        float maxDelay = 0f; // Find the longest delay to know when to stop waiting

        foreach (TimedEvent evt in events)
        {
            if (evt.TargetTime > maxDelay)
            {
                maxDelay = evt.TargetTime;
            }
        }
        
        // If no events, exit early
        if (events.Count == 0) yield break;

        Debug.Log($"UNITCOMBAT ({unit.unitName}): Starting Hit Effects Coroutine. Max delay to wait for: {maxDelay:F2}s. Number of events: {events.Count}", this);

        while (eventsTriggeredCount < events.Count && currentTime <= maxDelay + 0.1f) // Add a small buffer to ensure last event triggers
        {
            foreach (TimedEvent evt in events)
            {
                if (!evt.IsTriggered && currentTime >= evt.TargetTime)
                {
                    Debug.Log($"UNITCOMBAT ({unit.unitName}): Triggering event '{evt.Name}' at coroutine time {currentTime:F2}s.", this);
                    evt.Action.Invoke();
                    evt.IsTriggered = true;
                    eventsTriggeredCount++;
                }
            }

            if (eventsTriggeredCount == events.Count)
            {
                Debug.Log($"UNITCOMBAT ({unit.unitName}): All hit events triggered.", this);
                break;
            }

            yield return null; // Wait for the next frame
            currentTime += Time.deltaTime;
        }
        Debug.Log($"UNITCOMBAT ({unit.unitName}): Exiting Hit Effects Coroutine. Time elapsed: {currentTime:F2}s.", this);
    }
        
    private IEnumerator DeathSequenceCoroutine()
    {
        Debug.Log($"{unit.unitName} has been defeated! Starting death sequence.", this);
        if (animator != null) animator.SetTrigger("Die"); 
        else Debug.LogWarning($"{unit.unitName} has no Animator, skipping Die animation.", this);
            
        // TODO: Add Death VFX/SFX here, similar to PlayHitEffectsAndAnimationCoroutine
        // You'll need public variables for deathVFX_Prefab, sfx_Death, deathVFXOffset, sfxDeathDelay, vfxDeathDelay

        Debug.Log($"{unit.unitName} waiting {deathAnimationDuration}s for death animation.", this);
        yield return new WaitForSeconds(deathAnimationDuration);

        Debug.Log($"{unit.unitName} death animation presumed complete. Notifying TCM and deactivating.", this);
        TacticalCombatManager.Instance?.UnitDied(unit);
        gameObject.SetActive(false);
    }

    public bool IsDead()
    {
        if (stats == null)
        {
            Debug.LogWarning($"Stats component is null for {unit?.unitName ?? gameObject.name}. Considering unit as dead.", this);
            return true;
        }
        return stats.IsDefeated();
    }

    public bool CanConsiderAttacking()
    {
        return unitAP != null && unitAP.CanSpend(1) && !IsDead();
    }

    public void PerformMeleeAttack(Unit targetUnit)
    {
        if (targetUnit == null) {
            Debug.LogWarning($"{unit.unitName} tried to attack a null target.", this);
            return;
        }
        UnitCombat targetUnitCombat = targetUnit.GetComponent<UnitCombat>();
        if (targetUnitCombat == null || targetUnitCombat.IsDead())
        {
            Debug.LogWarning($"{unit.unitName} tried to attack an invalid or dead target ({targetUnit.unitName}).", this);
            return;
        }

        if (unitAP == null || !unitAP.CanSpend(MELEE_ATTACK_AP_COST))
        {
            Debug.LogWarning($"{unit.unitName} does not have enough AP for a melee attack ({MELEE_ATTACK_AP_COST} AP needed).", this);
            return;
        }

        unitAP.Spend(MELEE_ATTACK_AP_COST);

        UnitMover mover = unit.GetComponent<UnitMover>();
        if (mover != null)
        {
            mover.FaceTarget(targetUnit.transform.position);
        }
        else
        {
            Debug.LogWarning($"{unit.unitName} is missing a UnitMover component, cannot face target.", this);
        }

        Debug.Log($"{unit.unitName} performs MELEE ATTACK on {targetUnit.unitName}!", this);
        if (animator != null)
        {
            animator.SetTrigger("Attack_Melee"); 
        }
        else
        {
            Debug.LogWarning($"{unit.unitName} is missing an Animator component, cannot play attack animation.", this);
        }

        int rawDamage = 10 + Mathf.FloorToInt(stats.Core * 0.5f);
        targetUnitCombat.ProcessIncomingDamage(rawDamage);

        if (unit != null && unit.ShouldAutoEndTurn())
        {
            TacticalCombatManager.Instance?.EndCurrentTurn();
        }
    }
}