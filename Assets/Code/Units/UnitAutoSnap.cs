using UnityEngine;
using System.Collections;

[RequireComponent(typeof(UnitMover))]
public class UnitAutoSnap : MonoBehaviour
{
    [Header("Optional Snap Coordinate")]
    [Tooltip("If set, unit will snap to this tile on start instead of using world position.")]
    public Vector2Int overrideCoordinate = new Vector2Int(-1, -1);

    void Start()
    {
        StartCoroutine(SnapAfterFrame());
    }

    private IEnumerator SnapAfterFrame()
    {
        yield return null; // Ensure grid is initialized

        HexGrid grid = FindFirstObjectByType<HexGrid>();
        if (grid == null)
        {
            Debug.LogError("UnitAutoSnap: HexGrid not found.");
            yield break;
        }

        Vector2Int coordToUse;

        // Use override if it's a valid tile
        if (overrideCoordinate.x >= 0 && overrideCoordinate.y >= 0 && grid.IsValidCoordinate(overrideCoordinate))
        {
            coordToUse = overrideCoordinate;
        }
        else
        {
            coordToUse = grid.GetCoordinateForPosition(transform.position);
        }

        HexTile tile = grid.GetTileAt(coordToUse);

        if (tile != null)
        {
            GetComponent<UnitMover>().SnapToGrid(tile.coordinate);
            Debug.Log($"[UnitAutoSnap] Snapped {name} to tile {tile.coordinate}");
        }
        else
        {
            Debug.LogError($"[UnitAutoSnap] No valid tile under {name} at {coordToUse}");
        }
    }
}
