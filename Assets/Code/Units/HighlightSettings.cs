// File: HighlightSettings.cs
using UnityEngine;

[CreateAssetMenu(fileName = "HighlightSettings", menuName = "Tactics/Highlight Settings")]
public class HighlightSettings : ScriptableObject
{
    [Header("Movement Range")]
    public GameObject movementRangeHighlightPrefab;
    public float movementRangeYOffset = 0.05f; // Slightly above ground

    [Header("Attack Range")]
    public GameObject attackRangeHighlightPrefab;
    public float attackRangeYOffset = 0.06f; // Slightly different from move range

    [Header("Path Preview")]
    public GameObject pathPreviewMarkerPrefab;
    public float pathPreviewYOffset = 0.07f;

    [Header("Tile Hover")]
    public GameObject tileHoverHighlightPrefab;
    public float tileHoverYOffset = 0.08f;

    // Add any other visual settings, like colors or materials, if needed later
}