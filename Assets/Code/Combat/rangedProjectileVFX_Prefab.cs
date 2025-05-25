// File: SimpleProjectile.cs
using UnityEngine;

public class SimpleProjectile : MonoBehaviour
{
    private Unit targetUnit;
    private int damage;
    private float speed;
    private UnitCombat attackerCombat; // Reference to the attacker's UnitCombat

    private bool initialized = false;
    private Vector3 targetPositionLastKnown;

    public void Initialize(Unit target, int rawDamage, float projectileSpeed, UnitCombat combatSystemOfAttacker)
    {
        targetUnit = target;
        damage = rawDamage;
        speed = projectileSpeed;
        attackerCombat = combatSystemOfAttacker;

        if (targetUnit != null)
        {
            // Aim for the center of the target unit (adjust y-offset as needed)
            targetPositionLastKnown = targetUnit.transform.position + Vector3.up * 1.0f; 
            transform.LookAt(targetPositionLastKnown);
        }
        initialized = true;
        // Destroy after some time if it doesn't hit anything (e.g., target moves out of world)
        Destroy(gameObject, 5f); 
    }

    void Update()
    {
        if (!initialized) return;

        if (targetUnit != null && targetUnit.gameObject.activeInHierarchy) // Target still valid
        {
            targetPositionLastKnown = targetUnit.transform.position + Vector3.up * 1.0f;
        }
        // Else, it continues towards the last known position if target becomes null/inactive

        float step = speed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, targetPositionLastKnown, step);

        // Check for collision (simple distance check for this example)
        // More robust collision would use Rigidbody and OnTriggerEnter/OnCollisionEnter
        // Ensure the projectile's collider is set to IsTrigger if using OnTriggerEnter
        if (Vector3.Distance(transform.position, targetPositionLastKnown) < 0.5f) // Proximity threshold
        {
            HitTarget();
        }
    }

    void HitTarget()
    {
        if (!initialized) return; // Prevent multiple hits from the same Update frame
        initialized = false; // Mark as hit processed

        // Debug.Log($"Projectile hit near {targetPositionLastKnown}. Target unit: {targetUnit?.unitName}");

        if (attackerCombat != null)
        {
            // attackerCombat will handle damage application and VFX on target
            attackerCombat.ProcessProjectileHit(gameObject, targetUnit, damage); 
        }
        else
        {
            Debug.LogError("Attacker's UnitCombat system not found by projectile!");
            // Fallback or destroy self if attacker system is missing
            Destroy(gameObject);
        }
        // The UnitCombat.ProcessProjectileHit will destroy this gameObject after processing.
    }

    // Optional: If using a Rigidbody and Colliders for detection
    // void OnTriggerEnter(Collider other)
    // {
    //     if (!initialized) return;
    //
    //     Unit hitUnit = other.GetComponent<Unit>();
    //     if (hitUnit != null && hitUnit == targetUnit) // Make sure it hit the intended target
    //     {
    //         HitTarget();
    //     }
    //     // Could also handle hitting terrain/obstacles here
    //     else if (hitUnit == null && other.gameObject.layer == LayerMask.NameToLayer("Obstacles")) 
    //     {
    //          Debug.Log("Projectile hit an obstacle.");
    //          Destroy(gameObject); // Destroy projectile on hitting an obstacle
    //     }
    // }
}