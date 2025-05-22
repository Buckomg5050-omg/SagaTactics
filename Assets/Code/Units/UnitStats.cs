using UnityEngine;

public class UnitStats : MonoBehaviour
{
    [Header("Core Stats")]
    public string unitName = "New Unit";
    public string className = "Warrior"; // Default for Aryn, e.g., "Warrior", "Mage", "Rogue"
    public int classLevel = 1;

    [Space(10)] // Adds some space in the inspector for readability
    [Header("Essences")]
    public int Core = 10;    // Strength (melee damage)
    public int Spark = 8;    // Intelligence (magic damage)
    public int Echo = 12;    // Dexterity (accuracy, evasion, turn order)
    public int Pulse = 10;   // Constitution (health)
    public int Glimmer = 8;  // Wisdom (support effects)
    public int Aura = 10;    // Charisma (buff duration, secondary for turn order)

    [Space(10)]
    [Header("Combat Attributes")]
    public int maxHealth; // Calculated from Pulse
    public int currentHealth;

    public int moveRange = 4; // How many tiles unit can move
    public int actionPoints = 2; // How many actions unit can perform per turn

    // Dynamic combat modifiers (set by terrain, buffs, etc.)
    [HideInInspector] public float defenseBonus = 0f; // From terrain, etc.
    [HideInInspector] public float accuracyPenalty = 0f; // From terrain, etc.

    // Array to hold ability names (e.g., "PowerStrike", "ShieldBash")
    public string[] abilities;

    void Awake()
    {
        // Calculate maxHealth based on Pulse, for example:
        maxHealth = Pulse * 10; // Simple example: 10 HP per Pulse point
        currentHealth = maxHealth;

        // Assign initial abilities based on class (for prototyping)
        if (className == "Warrior")
        {
            abilities = new string[] { "PowerStrike", "ShieldBash" }; // Just Power Strike for now
        }
        // Add more class initialization here later
    }

    // Simple method to take damage
    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Debug.Log(unitName + " has been defeated!");
            // TODO: Implement unit defeat logic (e.g., disable, destroy)
        }
        Debug.Log(unitName + " took " + damage + " damage. Current HP: " + currentHealth);
    }

    // Method to restore health
    public void Heal(int amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }
        Debug.Log(unitName + " healed " + amount + ". Current HP: " + currentHealth);
    }

    // Placeholder for changing class (will be expanded later)
    public void ChangeClass(string newClassName)
    {
        className = newClassName;
        // For now, just a debug log. We'll implement actual stat/ability changes later.
        Debug.Log(unitName + " changed class to " + newClassName);
        // UpdateVisuals(); // Will implement this later too
    }
}