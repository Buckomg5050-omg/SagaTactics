using UnityEngine;

// This allows us to create instances of this ScriptableObject through the Unity Editor.
// The path will be: "Assets/Create/Tile/Data"
[CreateAssetMenu(fileName = "TileData", menuName = "Tile/Data")]
public class TileData : ScriptableObject
{
    public enum TerrainType { Normal, HighGround, Marsh, Elevated } // Defines different terrain types
    public TerrainType type; // The specific type of this tile data

    // Combat modifiers for units standing on this tile
    [Header("Combat Modifiers")]
    public float defenseBonus;  // e.g., 0.1f for HighGround (+10% defense)
    public int movePenalty;     // e.g., 1 for Marsh (-1 movement)
    public float accuracyPenalty; // e.g., 0.05f for Marsh (-5% accuracy)
    public float damageBonusDownward; // e.g., 0.15f for Spires (+15% damage when attacking downward)

    // Visual properties (e.g., if this tile should have a specific sprite or material)
    [Header("Visuals")]
    public Sprite displaySprite; // The sprite used to render this tile
}