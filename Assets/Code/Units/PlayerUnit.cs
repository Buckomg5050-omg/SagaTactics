using UnityEngine;

[RequireComponent(typeof(Unit))]
public class PlayerUnit : MonoBehaviour
{
    private HexGrid grid;
    private Unit unit;
    private UnitHighlighter unitHighlighter;
    private UnitMover unitMover;

    void Start()
    {
        grid = FindFirstObjectByType<HexGrid>();
        unit = GetComponent<Unit>();
        unitHighlighter = GetComponent<UnitHighlighter>();
        unitMover = GetComponent<UnitMover>();

        if (grid == null || unit == null)
        {
            Debug.LogError("Missing grid or unit reference.");
            return;
        }

        Vector2Int coords = grid.GetCoordinateForPosition(transform.position);

        if (unitMover != null)
        {
            unitMover.SnapToGrid(coords);
        }

        if (unitHighlighter != null)
        {
            unitHighlighter.ShowMoveRange(coords);
        }
        else
        {
            Debug.LogWarning($"[PlayerUnit] No UnitHighlighter found on {name} — skipping move range display.");
        }
    }
}
