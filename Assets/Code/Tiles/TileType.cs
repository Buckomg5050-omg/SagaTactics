using UnityEngine;

[CreateAssetMenu(menuName = "Hex/Tile Type")]
public class TileType : ScriptableObject
{
    public string tileName;
    public GameObject prefab;

    public bool isWalkable = true;
    public int moveCost = 1;

    [Header("Combat Modifiers")]
    [Range(-50, 50)] public int defenseBonusPercent = 0;
    [Range(-50, 50)] public int damageBonusPercent = 0;
    public int rangeModifier = 0;
    [Range(-50, 50)] public int accuracyModifierPercent = 0;
}
