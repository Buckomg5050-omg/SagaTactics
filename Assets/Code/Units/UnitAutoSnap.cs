using UnityEngine;
using System.Collections;

[RequireComponent(typeof(UnitMover))]
public class UnitAutoSnap : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(SnapAfterFrame());
    }

    private IEnumerator SnapAfterFrame()
    {
        // Wait one frame so all transforms and grid state are initialized
        yield return null;

        HexGrid grid = FindFirstObjectByType<HexGrid>();
        if (grid == null)
        {
            Debug.LogError("UnitAutoSnap: HexGrid not found.");
            yield break;
        }

        Vector2Int coord = grid.GetCoordinateForPosition(transform.position);
        HexTile tile = grid.GetTileAt(coord);

        if (tile != null)
        {
            GetComponent<UnitMover>().SnapToGrid(tile.coordinate);
            Debug.Log($"[UnitAutoSnap] Snapped {name} to tile {tile.coordinate}");
        }
        else
        {
            Debug.LogError($"[UnitAutoSnap] No valid tile under {name} at {coord}");
        }
    }
}
