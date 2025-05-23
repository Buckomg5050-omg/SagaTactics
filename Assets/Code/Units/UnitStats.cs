using UnityEngine;

public class UnitStats : MonoBehaviour
{
    [Header("Core Info")]
    public string unitName = "New Unit"; // This will be used by Unit.cs
    public string className = "Warrior"; 
    public int classLevel = 1;

    [Space(10)]
    [Header("Essences (Base Stats)")]
    public int Core = 10;    // Strength (melee/physical damage contribution)
    public int Spark = 8;    // Intelligence (magic damage contribution - for future)
    public int Echo = 12;    // Dexterity (accuracy, evasion, turn order)
    public int Pulse = 10;   // Constitution (determines max health)
    public int Glimmer = 8;  // Wisdom (support effects, magic defense - for future)
    public int Aura = 10;    // Charisma (buff duration, initiative tie-breaker)
    public int Defense = 5;  // NEW: Base physical damage reduction

    [Space(10)]
    [Header("Derived Combat Attributes")]
    public int maxHealth;     // Calculated from Pulse
    public int currentHealth;
    // public int moveRange = 4; // This seems to be handled by UnitMover.cs, consider if needed here
    // public int actionPoints = 2; // This is a base/default, UnitAP.cs manages current/max runtime AP

    // Dynamic combat modifiers (intended to be set by terrain, buffs, status effects etc.)
    // These are typically ADDITIVE or MULTIPLICATIVE bonuses/penalties to base stats or derived values.
    [HideInInspector] public float defenseBonusPercent = 0f; // Example: +0.10 for +10% defense from a buff
    [HideInInspector] public float accuracyBonusPercent = 0f; // Example: -0.05 for -5% accuracy from terrain
    // Consider adding damageBonusPercent here if it's a stat, or handle it in damage calculation from TileType directly

    // Array to hold ability names (placeholders for now)
    public string[] abilities;

    void Awake()
    {
        CalculateDerivedStats();
        currentHealth = maxHealth;

        // Assign initial abilities based on class (for prototyping)
        if (className == "Warrior")
        {
            // GDD Abilities: Power Strike, Shield Bash, Rallying Cry
            abilities = new string[] { "PowerStrike" }; // Just Power Strike for now
        }
        // Add more class initialization here later
    }

    public void CalculateDerivedStats() // NEW: Made this public in case stats change (e.g. level up)
    {
        // Calculate maxHealth based on Pulse, for example:
        maxHealth = Pulse * 10; // GDD doesn't specify exact formula, 10HP/Pulse is a good start
    }

    // MODIFIED: TakeDamage to include Defense stat and defenseBonusPercent
    public void TakeDamage(int rawDamage)
    {
        if (currentHealth <= 0) return; // Already defeated

        // 1. Apply percentage defense bonus (if any) to the base Defense stat
        // This interpretation means defenseBonusPercent enhances your flat Defense.
        // Another interpretation could be that defenseBonusPercent directly reduces incoming damage.
        // Let's go with enhancing flat Defense for now as it's simpler.
        float effectiveDefense = Defense * (1f + defenseBonusPercent);

        // 2. Subtract flat defense from raw damage
        int damageAfterDefense = Mathf.Max(1, rawDamage - Mathf.FloorToInt(effectiveDefense)); // Ensure at least 1 damage if hit

        currentHealth -= damageAfterDefense;
        
        Debug.Log($"{unitName} took {rawDamage} raw damage, {damageAfterDefense} after defense. Current HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            // Defeat logic will be handled by UnitCombat calling Die()
        }
    }

    // Method to restore health
    public void Heal(int amount)
    {
        if (currentHealth <= 0) return; // Cannot heal defeated units (usually)

        currentHealth += amount;
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }
        Debug.Log($"{unitName} healed {amount}. Current HP: {currentHealth}/{maxHealth}");
    }

    // NEW: Method to check if defeated
    public bool IsDefeated()
    {
        return currentHealth <= 0;
    }

    // Placeholder for changing class (will be expanded later)
    public void ChangeClass(string newClassName)
    {
        className = newClassName;
        // TODO: Implement actual stat/ability changes, recalculate derived stats
        CalculateDerivedStats(); // Recalculate health if Pulse changes with class
        Debug.Log($"{unitName} changed class to " + newClassName);
    }

    // Call this if Pulse or other base stats that affect maxHealth are changed externally
    public void RecalculateHealth() 
    {
        int oldMaxHealth = maxHealth;
        CalculateDerivedStats();
        // Adjust current health proportionally if max health changes
        if (maxHealth != oldMaxHealth && oldMaxHealth > 0) 
        {
            currentHealth = Mathf.RoundToInt((float)currentHealth / oldMaxHealth * maxHealth);
        }
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth); // Ensure it's within new bounds
    }
}