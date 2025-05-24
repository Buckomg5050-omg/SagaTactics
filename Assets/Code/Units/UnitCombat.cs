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
    
    [Header("Hit Timing Delays (from start of hit processing)")]
    public float sfxHitDelay = 0.0f; 
    public float vfxHitDelay = 0.5f;
    public float takeHitAnimationDelay = 0.5f; 

    [Header("Death Presentation")]
    public float dyingAnimationHoldDuration = 3.0f; 
    public float sfxPlayDelayAfterVFX_Death = 0.1f; 

    [Header("Hit Effects")]
    public GameObject hitImpactVFX_Prefab; 
    public AudioClip sfx_MeleeHit;
    public Vector3 vfxSpawnOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Death Effects (Played at Disappearance)")]
    public GameObject deathEffectVFX_Prefab; 
    public AudioClip sfx_UnitDeath;          
    public Vector3 deathVfxSpawnOffset = new Vector3(0f, 0.5f, 0f); 
            
    private AudioSource unitAudioSource; // Still used for HIT sfx

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
            StartCoroutine(PlayHitEffectsAndAnimationCoroutine());
        else 
            StartCoroutine(DeathSequenceCoroutine());
    }

    private class TimedEvent {
        public float TargetTime; public System.Action Action; public bool IsTriggered; public string Name;
        public TimedEvent(string name, float targetTime, System.Action action) {
            Name = name; TargetTime = Mathf.Max(0, targetTime); Action = action; IsTriggered = false; }
    }

    private IEnumerator PlayHitEffectsAndAnimationCoroutine() { 
        List<TimedEvent> events = new List<TimedEvent>();
        if (sfx_MeleeHit != null && unitAudioSource != null) { events.Add(new TimedEvent("SFX_Hit", sfxHitDelay, () => { unitAudioSource.PlayOneShot(sfx_MeleeHit); Debug.Log($"{unit.unitName} playing sfx_MeleeHit (Targeted at {sfxHitDelay:F2}s).", this); })); }
        if (hitImpactVFX_Prefab != null) { events.Add(new TimedEvent("VFX_Hit", vfxHitDelay, () => { if (stats != null && !stats.IsDefeated()) { Vector3 spawnPosition = transform.position + vfxSpawnOffset; Debug.Log($"Attempting to instantiate Hit VFX: '{hitImpactVFX_Prefab.name}' at {spawnPosition} for {unit.unitName} (Targeted at {vfxHitDelay:F2}s).", this); Instantiate(hitImpactVFX_Prefab, spawnPosition, Quaternion.identity); } }));
        } else { Debug.LogWarning($"UNITCOMBAT ({unit.unitName}): hitImpactVFX_Prefab is NULL, Hit VFX event will not be scheduled.", this); }
        if (animator != null) { events.Add(new TimedEvent("Animation_Hit", takeHitAnimationDelay, () => { if (stats != null && !stats.IsDefeated()) { animator.SetTrigger("Take_Hit"); Debug.Log($"{unit.unitName} playing Take_Hit animation (Targeted at {takeHitAnimationDelay:F2}s).", this); } })); }
        float currentTime = 0f; int eventsTriggeredCount = 0; float maxDelay = 0f; 
        foreach (TimedEvent evt in events) { if (evt.TargetTime > maxDelay) maxDelay = evt.TargetTime; }
        if (events.Count == 0) yield break;
        while (eventsTriggeredCount < events.Count && currentTime <= maxDelay + 0.1f) {
            foreach (TimedEvent evt in events) { if (!evt.IsTriggered && currentTime >= evt.TargetTime) { evt.Action.Invoke(); evt.IsTriggered = true; eventsTriggeredCount++; } }
            if (eventsTriggeredCount == events.Count) break;
            yield return null; currentTime += Time.deltaTime;
        }
    }
            
    private IEnumerator DeathSequenceCoroutine()
    {
        Debug.Log($"UNITDEATH ({unit.unitName}): Starting death sequence.", this);

        if (animator != null) animator.SetTrigger("Die"); 
        else Debug.LogWarning($"UNITDEATH ({unit.unitName}): No Animator, skipping Die animation.", this);
            
        if (dyingAnimationHoldDuration > 0.001f)
        {
            Debug.Log($"UNITDEATH ({unit.unitName}): Holding death pose for {dyingAnimationHoldDuration:F2}s.", this);
            yield return new WaitForSeconds(dyingAnimationHoldDuration);
        }

        Debug.Log($"UNITDEATH ({unit.unitName}): Death pose hold complete. Initiating disappearance effects.", this);

        // --- DEFINE effectSpawnPosition EARLIER ---
        Vector3 effectSpawnPosition = transform.position + deathVfxSpawnOffset;

        if (deathEffectVFX_Prefab != null)
        {
            Debug.Log($"UNITDEATH ({unit.unitName}): Attempting to instantiate Death VFX: '{deathEffectVFX_Prefab.name}' at {effectSpawnPosition}.", this);
            Instantiate(deathEffectVFX_Prefab, effectSpawnPosition, Quaternion.identity);
        } else { Debug.LogWarning($"UNITDEATH ({unit.unitName}): deathEffectVFX_Prefab is NULL, Death VFX will not play.", this); }
        
        if (sfxPlayDelayAfterVFX_Death > 0.001f)
        {
            Debug.Log($"UNITDEATH ({unit.unitName}): Waiting {sfxPlayDelayAfterVFX_Death:F2}s after VFX init before playing death SFX.", this);
            yield return new WaitForSeconds(sfxPlayDelayAfterVFX_Death);
        }

        if (sfx_UnitDeath != null)
        {
            if (GlobalSFXPlayer.Instance != null)
            {
                // --- USE effectSpawnPosition HERE ---
                Debug.Log($"UNITDEATH ({unit.unitName}): Telling GlobalSFXPlayer to play '{sfx_UnitDeath.name}' at {effectSpawnPosition}.", this);
                GlobalSFXPlayer.Instance.PlaySFXAtPosition(sfx_UnitDeath, effectSpawnPosition);
            }
            else
            {
                Debug.LogWarning($"UNITDEATH ({unit.unitName}): GlobalSFXPlayer.Instance is null. Cannot play death SFX.", this);
            }
        }
        
        TacticalCombatManager.Instance?.UnitDied(unit);

        // No longer need the extra yield return null here if GlobalSFXPlayer handles sound.
        // The unit can be deactivated immediately after telling GlobalSFXPlayer to play.

        Debug.Log($"UNITDEATH ({unit.unitName}): Death effects initiated by UnitCombat. Deactivating GameObject.", this);
        gameObject.SetActive(false);
    }

    public bool IsDead() { 
        if (stats == null) { Debug.LogWarning($"Stats component is null for {unit?.unitName ?? gameObject.name}. Considering unit as dead.", this); return true; }
        return stats.IsDefeated();
    }
    public bool CanConsiderAttacking() { 
        return unitAP != null && unitAP.CanSpend(1) && !IsDead();
    }
    public void PerformMeleeAttack(Unit targetUnit) { 
        if (targetUnit == null) { Debug.LogWarning($"{unit.unitName} tried to attack a null target.", this); return; }
        UnitCombat targetUnitCombat = targetUnit.GetComponent<UnitCombat>();
        if (targetUnitCombat == null || targetUnitCombat.IsDead()) { Debug.LogWarning($"UNITCOMBAT ({unit.unitName}): Target {targetUnit.unitName} is null or dead.", this); return; }
        if (unitAP == null || !unitAP.CanSpend(MELEE_ATTACK_AP_COST)) { Debug.LogWarning($"UNITCOMBAT ({unit.unitName}): Not enough AP for melee attack.", this); return; }
        
        unitAP.Spend(MELEE_ATTACK_AP_COST);
        
        UnitMover mover = GetComponent<UnitMover>(); // Get component from self
        if (mover != null) { mover.FaceTarget(targetUnit.transform.position); }
        else { Debug.LogWarning($"UNITCOMBAT ({unit.unitName}): Missing UnitMover component.", this); }
        
        Debug.Log($"UNITCOMBAT ({unit.unitName}): Performs MELEE ATTACK on {targetUnit.unitName}!", this);
        if (animator != null) { animator.SetTrigger("Attack_Melee");  }
        else { Debug.LogWarning($"UNITCOMBAT ({unit.unitName}): Missing Animator component.", this); }
        
        int rawDamage = 10 + Mathf.FloorToInt(stats.Core * 0.5f); // Ensure stats.Core is correct
        targetUnitCombat.ProcessIncomingDamage(rawDamage);
        
        if (unit != null && unit.ShouldAutoEndTurn()) { TacticalCombatManager.Instance?.EndCurrentTurn(); }
    }
}