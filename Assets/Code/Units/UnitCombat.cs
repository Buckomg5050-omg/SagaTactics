// File: UnitCombat.cs
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
    public AudioClip sfx_RangedHit; 
    public GameObject rangedProjectileVFX_Prefab; 
    public float projectileSpeed = 20f;
    public Vector3 vfxSpawnOffset = new Vector3(0f, 0.5f, 0f);


    [Header("Death Effects (Played at Disappearance)")]
    public GameObject deathEffectVFX_Prefab; 
    public AudioClip sfx_UnitDeath;          
    public Vector3 deathVfxSpawnOffset = new Vector3(0f, 0.5f, 0f); 
            
    private AudioSource unitAudioSource; 
    private HashSet<GameObject> activeProjectiles = new HashSet<GameObject>();


    void Awake()
    {
        unit = GetComponent<Unit>();
        stats = unit.unitStats; 
        unitAP = unit.unitAP;   
        animator = GetComponentInChildren<Animator>();

        if (unit == null) Debug.LogError($"UnitCombat on {gameObject.name} is missing its Unit component reference!", this);
        if (stats == null) Debug.LogError($"UnitCombat on {unit?.unitName ?? gameObject.name} could not find UnitStats. It should be assigned via Unit.cs!", this);
        if (unitAP == null) Debug.LogError($"UnitCombat on {unit?.unitName ?? gameObject.name} could not find UnitAP. It should be assigned via Unit.cs!", this);

        unitAudioSource = GetComponent<AudioSource>();
        if (unitAudioSource == null)
        {
            unitAudioSource = gameObject.AddComponent<AudioSource>();
            unitAudioSource.playOnAwake = false;
        }
    }

    public void ProcessIncomingDamage(int rawDamageAmount, bool isRangedAttack = false) 
    {
        if (IsDead()) return;
        stats.TakeDamage(rawDamageAmount);
        if (!stats.IsDefeated()) 
            StartCoroutine(PlayHitEffectsAndAnimationCoroutine(isRangedAttack)); 
        else 
            StartCoroutine(DeathSequenceCoroutine());
    }

    private class TimedEvent {
        public float TargetTime; public System.Action Action; public bool IsTriggered; public string Name;
        public TimedEvent(string name, float targetTime, System.Action action) {
            Name = name; TargetTime = Mathf.Max(0, targetTime); Action = action; IsTriggered = false; }
    }
    
    private IEnumerator PlayHitEffectsAndAnimationCoroutine(bool isRangedAttack = false) { 
        List<TimedEvent> events = new List<TimedEvent>();
        AudioClip hitSfx = isRangedAttack && sfx_RangedHit != null ? sfx_RangedHit : sfx_MeleeHit;

        if (hitSfx != null && unitAudioSource != null) { events.Add(new TimedEvent("SFX_Hit", sfxHitDelay, () => { if(this != null && unitAudioSource != null) unitAudioSource.PlayOneShot(hitSfx); Debug.Log($"{unit?.unitName} playing {hitSfx?.name} (Targeted at {sfxHitDelay:F2}s).", this); })); }
        if (hitImpactVFX_Prefab != null) { events.Add(new TimedEvent("VFX_Hit", vfxHitDelay, () => { if (this != null && stats != null && !stats.IsDefeated()) { Vector3 spawnPosition = transform.position + vfxSpawnOffset; Debug.Log($"Attempting to instantiate Hit VFX: '{hitImpactVFX_Prefab.name}' at {spawnPosition} for {unit?.unitName} (Targeted at {vfxHitDelay:F2}s).", this); Instantiate(hitImpactVFX_Prefab, spawnPosition, Quaternion.identity); } }));
        } // else { Debug.LogWarning($"UNITCOMBAT ({unit?.unitName}): hitImpactVFX_Prefab is NULL, Hit VFX event will not be scheduled.", this); }
        if (animator != null) { events.Add(new TimedEvent("Animation_Hit", takeHitAnimationDelay, () => { if (this != null && stats != null && !stats.IsDefeated() && animator != null) { animator.SetTrigger("Take_Hit"); Debug.Log($"{unit?.unitName} playing Take_Hit animation (Targeted at {takeHitAnimationDelay:F2}s).", this); } })); }
        
        float currentTime = 0f; int eventsTriggeredCount = 0; float maxDelay = 0f; 
        foreach (TimedEvent evt in events) { if (evt.TargetTime > maxDelay) maxDelay = evt.TargetTime; }
        if (events.Count == 0) yield break;
        
        while (eventsTriggeredCount < events.Count && currentTime <= maxDelay + 0.1f) {
            if (this == null) yield break; // Safety break if object destroyed mid-coroutine
            foreach (TimedEvent evt in events) { if (!evt.IsTriggered && currentTime >= evt.TargetTime) { evt.Action.Invoke(); evt.IsTriggered = true; eventsTriggeredCount++; } }
            if (eventsTriggeredCount == events.Count) break;
            yield return null; currentTime += Time.deltaTime;
        }
    }
            
    private IEnumerator DeathSequenceCoroutine()
    {
        Debug.Log($"UNITDEATH ({unit?.unitName}): Starting death sequence.", this);

        if (animator != null) animator.SetTrigger("Die"); 
        // else Debug.LogWarning($"UNITDEATH ({unit?.unitName}): No Animator, skipping Die animation.", this);
            
        if (dyingAnimationHoldDuration > 0.001f)
        {
            // Debug.Log($"UNITDEATH ({unit?.unitName}): Holding death pose for {dyingAnimationHoldDuration:F2}s.", this);
            yield return new WaitForSeconds(dyingAnimationHoldDuration);
        }
        if (this == null) yield break; // Safety break

        // Debug.Log($"UNITDEATH ({unit?.unitName}): Death pose hold complete. Initiating disappearance effects.", this);

        Vector3 effectSpawnPosition = transform.position + deathVfxSpawnOffset;

        if (deathEffectVFX_Prefab != null)
        {
            // Debug.Log($"UNITDEATH ({unit?.unitName}): Attempting to instantiate Death VFX: '{deathEffectVFX_Prefab.name}' at {effectSpawnPosition}.", this);
            Instantiate(deathEffectVFX_Prefab, effectSpawnPosition, Quaternion.identity);
        } // else { Debug.LogWarning($"UNITDEATH ({unit?.unitName}): deathEffectVFX_Prefab is NULL, Death VFX will not play.", this); }
        
        if (sfxPlayDelayAfterVFX_Death > 0.001f)
        {
            // Debug.Log($"UNITDEATH ({unit?.unitName}): Waiting {sfxPlayDelayAfterVFX_Death:F2}s after VFX init before playing death SFX.", this);
            yield return new WaitForSeconds(sfxPlayDelayAfterVFX_Death);
        }
        if (this == null) yield break; // Safety break

        if (sfx_UnitDeath != null)
        {
            if (GlobalSFXPlayer.Instance != null)
            {
                // Debug.Log($"UNITDEATH ({unit?.unitName}): Telling GlobalSFXPlayer to play '{sfx_UnitDeath.name}' at {effectSpawnPosition}.", this);
                GlobalSFXPlayer.Instance.PlaySFXAtPosition(sfx_UnitDeath, effectSpawnPosition);
            }
            // else { Debug.LogWarning($"UNITDEATH ({unit?.unitName}): GlobalSFXPlayer.Instance is null. Cannot play death SFX.", this); }
        }
        
        TacticalCombatManager.Instance?.UnitDied(unit); // unit might be null if object was destroyed
        // Debug.Log($"UNITDEATH ({unit?.unitName}): Death effects initiated by UnitCombat. Deactivating GameObject.", this);
        if(gameObject != null) gameObject.SetActive(false); // Check if gameObject still exists
    }

    public bool IsDead() { 
        if (stats == null) { /* Debug.LogWarning($"Stats component is null for {unit?.unitName ?? gameObject.name}. Considering unit as dead.", this); */ return true; }
        return stats.IsDefeated();
    }
    
    public bool CanConsiderAttacking() { 
        if (stats == null || unitAP == null || IsDead()) return false; // Added stats null check
        bool canMelee = stats.meleeAttackRange > 0 && unitAP.CanSpend(stats.meleeAPCost);
        bool canRange = stats.rangedAttackRange > 0 && unitAP.CanSpend(stats.rangedAPCost);
        return canMelee || canRange;
    }

    public void PerformMeleeAttack(Unit targetUnit) { 
        if (!CanPerformMeleeAttackChecks(targetUnit)) return; 
        
        if (stats == null || unitAP == null) { Debug.LogError("PerformMeleeAttack: Stats or UnitAP is null!", this); return;} // Safety
        unitAP.Spend(stats.meleeAPCost); 
        
        UnitMover mover = GetComponent<UnitMover>();
        if (mover != null) { mover.FaceTarget(targetUnit.transform.position); }
        // else { Debug.LogWarning($"UNITCOMBAT ({unit?.unitName}): Missing UnitMover component.", this); }
        
        // Debug.Log($"UNITCOMBAT ({unit?.unitName}): Performs MELEE ATTACK on {targetUnit.unitName}!", this);
        if (animator != null) { animator.SetTrigger("Attack_Melee");  }
        // else { Debug.LogWarning($"UNITCOMBAT ({unit?.unitName}): Missing Animator component.", this); }
        
        int rawDamage = 10 + Mathf.FloorToInt(stats.Core * 0.5f); 
        targetUnit.GetComponent<UnitCombat>()?.ProcessIncomingDamage(rawDamage, false); 
        
        if (unit != null && unit.ShouldAutoEndTurn()) { TacticalCombatManager.Instance?.EndCurrentTurn(); }
    }
    
    private bool CanPerformMeleeAttackChecks(Unit targetUnit)
    {
        if (targetUnit == null) { /* Debug.LogWarning($"{unit?.unitName} tried to melee attack a null target.", this); */ return false; }
        UnitCombat targetUnitCombat = targetUnit.GetComponent<UnitCombat>(); // Target might not have UnitCombat if it's a non-combatant destructible
        if (targetUnitCombat != null && targetUnitCombat.IsDead()) { /* Debug.LogWarning($"UNITCOMBAT ({unit?.unitName}): Melee Target {targetUnit.unitName} is dead.", this); */ return false; }
        if (stats == null || unitAP == null) { /* Debug.LogError("CanPerformMeleeAttackChecks: Stats or UnitAP is null!", this); */ return false;} // Safety
        if (!unitAP.CanSpend(stats.meleeAPCost)) { /* Debug.LogWarning($"UNITCOMBAT ({unit?.unitName}): Not enough AP for melee attack (needs {stats.meleeAPCost}).", this); */ return false; }
        if (stats.meleeAttackRange <= 0) { /* Debug.LogWarning($"UNITCOMBAT ({unit?.unitName}): Unit has no melee attack range defined in UnitStats.", this); */ return false; }
        return true;
    }

    public bool CanPerformRangedAttack() // This checks if the unit itself is capable of initiating one
    {
        if (stats == null || unitAP == null) return false;
        return stats.rangedAttackRange > 0 && unitAP.CanSpend(stats.rangedAPCost) && !IsDead();
    }

    public void PerformRangedAttack(Unit targetUnit)
    {
        if (!CanPerformRangedAttackChecks(targetUnit)) {
            Debug.LogWarning($"PerformRangedAttack for {unit?.unitName} on {targetUnit?.unitName} failed pre-checks.", this);
            return;
        }
        if (stats == null || unitAP == null) { Debug.LogError("PerformRangedAttack: Stats or UnitAP is null!", this); return;} // Safety

        unitAP.Spend(stats.rangedAPCost);

        UnitMover mover = GetComponent<UnitMover>();
        if (mover != null) { mover.FaceTarget(targetUnit.transform.position); }
        // else { Debug.LogWarning($"UNITCOMBAT ({unit?.unitName}): Missing UnitMover component for ranged attack.", this); }

        // Debug.Log($"UNITCOMBAT ({unit?.unitName}): Performs RANGED ATTACK on {targetUnit.unitName}!", this);
        if (animator != null) { animator.SetTrigger("AttackRanged"); } 
        // else { Debug.LogWarning($"UNITCOMBAT ({unit?.unitName}): Missing Animator component for ranged attack.", this); }

        if (rangedProjectileVFX_Prefab != null)
        {
            Vector3 spawnPos = transform.position + transform.forward * 0.5f + Vector3.up * 1.0f; 
            GameObject projectileGO = Instantiate(rangedProjectileVFX_Prefab, spawnPos, Quaternion.LookRotation(targetUnit.transform.position - spawnPos));
            activeProjectiles.Add(projectileGO); 

            SimpleProjectile projectile = projectileGO.GetComponent<SimpleProjectile>();
            if (projectile == null) projectile = projectileGO.AddComponent<SimpleProjectile>();
            
            int rawDamage = 8 + Mathf.FloorToInt(stats.Spark * 0.5f); 
            projectile.Initialize(targetUnit, rawDamage, projectileSpeed, this);
        }
        else
        {
            Debug.LogWarning($"UNITCOMBAT ({unit?.unitName}): rangedProjectileVFX_Prefab is null. Ranged attack no projectile, instant damage (FOR DEBUG).");
            int rawDamage = 8 + Mathf.FloorToInt(stats.Spark * 0.5f);
            targetUnit.GetComponent<UnitCombat>()?.ProcessIncomingDamage(rawDamage, true); 
        }

        if (unit != null && unit.ShouldAutoEndTurn()) { TacticalCombatManager.Instance?.EndCurrentTurn(); }
    }
    
    // *** THIS IS THE CORRECTED SIGNATURE ***
    public bool CanPerformRangedAttackChecks(Unit targetUnit)
    {
        if (targetUnit == null) { /* Debug.LogWarning($"{unit?.unitName} tried to range attack a null target.", this); */ return false; }
        UnitCombat targetUnitCombat = targetUnit.GetComponent<UnitCombat>();
        // It's okay if target doesn't have UnitCombat (e.g. destructible object), but if it does and is dead, then fail.
        if (targetUnitCombat != null && targetUnitCombat.IsDead()) { /* Debug.LogWarning($"UNITCOMBAT ({unit?.unitName}): Ranged Target {targetUnit.unitName} is dead.", this); */ return false; }
        
        // Check if this unit itself can initiate a ranged attack (AP, range defined, not dead)
        if (!CanPerformRangedAttack()) { /* Debug.LogWarning($"UNITCOMBAT ({unit?.unitName}): Cannot perform ranged attack (basic capability check failed).", this); */ return false; }
        
        // Line of Sight (LOS) check
        Vector3 eyePosition = transform.position + Vector3.up * 1.5f; 
        Vector3 targetColliderCenter = targetUnit.GetComponent<Collider>()?.bounds.center ?? targetUnit.transform.position + Vector3.up * 1.0f; // Prefer collider center

        // Define what layers block LOS. Exclude layers like "Ignore Raycast".
        // Also exclude the attacker's own layer and target's layer if units shouldn't block each other visually for this check.
        // For simplicity, let's assume "Default" and custom "Obstacle" layers block.
        int losBlockingLayerMask = LayerMask.GetMask("Default", "Obstacle"); // Adjust as needed
        // To ignore specific layers, use ~LayerMask.GetMask("PlayerUnit", "EnemyUnit", "Ignore Raycast");

        if (Physics.Linecast(eyePosition, targetColliderCenter, out RaycastHit hit, losBlockingLayerMask, QueryTriggerInteraction.Ignore))
        {
            // If we hit something, and it's NOT the target unit itself (or a child of the target), then LOS is blocked.
            if (hit.transform != targetUnit.transform && !hit.transform.IsChildOf(targetUnit.transform))
            {
                // No need to check if it's the attacker itself, Linecast from eye should usually avoid that.
                Debug.Log($"UNITCOMBAT ({unit?.unitName}): Ranged attack on {targetUnit.unitName} blocked by {hit.collider.name} on layer {LayerMask.LayerToName(hit.collider.gameObject.layer)}.", this);
                return false; 
            }
        }
        
        // Distance check should have been performed by UnitInputHandler to even consider this target.
        // But if called directly (e.g., by AI later), it might be good to have it here too.
        // For now, assuming caller (UnitInputHandler) verified distance. If not, add:
        // HexGrid grid = FindFirstObjectByType<HexGrid>();
        // UnitMover myMover = GetComponent<UnitMover>();
        // UnitMover targetMover = targetUnit.GetComponent<UnitMover>();
        // if (grid != null && myMover != null && targetMover != null) {
        //    int dist = HexUtils.HexDistance(myMover.CurrentGridCoords, targetMover.CurrentGridCoords);
        //    if (dist > stats.rangedAttackRange) return false;
        // }

        return true;
    }

    public void ProcessProjectileHit(GameObject projectileInstance, Unit targetUnit, int rawDamage)
    {
        if (activeProjectiles.Contains(projectileInstance))
        {
            activeProjectiles.Remove(projectileInstance); 
            UnitCombat targetCombat = targetUnit?.GetComponent<UnitCombat>(); // Target might have been destroyed
            if (targetCombat != null && !targetCombat.IsDead())
            {
                targetCombat.ProcessIncomingDamage(rawDamage, true); 
            }
            if(projectileInstance != null) Destroy(projectileInstance, 0.1f); 
        }
    }
}