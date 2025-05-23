using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(UnitMover))]
public class UnitHighlighter : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject hoverHighlightPrefab;
    [SerializeField] private GameObject rangeHighlightPrefab;
    [SerializeField] private GameObject previewMarkerPrefab;

    [Header("Offsets")]
    [SerializeField] private float hoverYOffset = 0.3f;
    [SerializeField] private float rangeYOffset = 0.3f;
    [SerializeField] private float previewYOffset = 0.31f;

    [Header("Reactivation Delay")]
    [SerializeField] private float reenableDelay = 0.25f;

    private GameObject hoverHighlightInstance;
    private List<GameObject> activeRangeHighlights = new();
    private List<GameObject> activePreviewMarkers = new();

    private HexGrid gridManager;
    private HexPathfinder pathfinder;
    private UnitMover mover;

    private Vector2Int? currentHoverCoords = null;
    private float hoverUpdateTimer = 0f;
    private float hoverUpdateInterval = 0.05f;
    private bool highlightsSuppressed = false;

    void Awake()
    {
        gridManager = FindFirstObjectByType<HexGrid>();
        pathfinder = new HexPathfinder(gridManager);
        mover = GetComponent<UnitMover>();

        if (hoverHighlightPrefab != null)
        {
            hoverHighlightInstance = Instantiate(hoverHighlightPrefab);
            hoverHighlightInstance.SetActive(false);
        }

        mover.OnMoveComplete += () =>
        {
            StartCoroutine(DelayedEnableHighlights());
        };
    }

    void Update()
    {
        hoverUpdateTimer += Time.deltaTime;

        if (mover.IsMoving || highlightsSuppressed)
        {
            hoverHighlightInstance?.SetActive(false);
            ClearPreview();
            ClearMoveRange();
            return;
        }

        if (hoverUpdateTimer >= hoverUpdateInterval)
        {
            hoverUpdateTimer = 0f;
            UpdateHoverHighlightAndPreview();
        }
    }

    public void ShowMoveRange(Vector2Int centerCoord)
    {
        if (mover.IsMoving || highlightsSuppressed)
        {
            ClearMoveRange();
            return;
        }

        ClearMoveRange();

        int maxCost = mover.MovementRange;
        Dictionary<Vector2Int, float> visited = new();
        Queue<(Vector2Int coord, float cost)> frontier = new();

        visited[centerCoord] = 0;
        frontier.Enqueue((centerCoord, 0));

        while (frontier.Count > 0)
        {
            var (current, costSoFar) = frontier.Dequeue();

            HexTile currentTile = gridManager.GetTileAt(current);
            if (currentTile == null || !currentTile.isWalkable)
                continue;

            if (current != mover.CurrentGridCoords && costSoFar <= maxCost)
            {
                Vector3 worldPos = gridManager.GetPositionForHexFromCoordinate(current) + Vector3.up * rangeYOffset;
                GameObject marker = Instantiate(rangeHighlightPrefab, worldPos, Quaternion.identity);
                activeRangeHighlights.Add(marker);
            }

            foreach (HexTile neighbor in gridManager.GetNeighbors(currentTile))
            {
                if (!neighbor.isWalkable)
                    continue;

                float newCost = costSoFar + neighbor.tileType.moveCost;
                Vector2Int nextCoord = neighbor.coordinate;

                if (newCost <= maxCost && (!visited.ContainsKey(nextCoord) || newCost < visited[nextCoord]))
                {
                    visited[nextCoord] = newCost;
                    frontier.Enqueue((nextCoord, newCost));
                }
            }
        }
    }

    public void ShowPreview(HexTile targetTile)
    {
        ClearPreview();

        if (mover.IsMoving || highlightsSuppressed || targetTile == null || !targetTile.isWalkable)
            return;

        HexTile startTile = gridManager.GetTileAt(mover.CurrentGridCoords);
        if (startTile == null)
            return;

        List<HexTile> path = pathfinder.FindPath(startTile, targetTile);
        if (path == null || path.Count == 0)
            return;

        float totalCost = 0f;

        for (int i = 1; i < path.Count; i++)
        {
            totalCost += path[i].tileType.moveCost;
            if (totalCost > mover.MovementRange)
                return;
        }

        foreach (HexTile tile in path)
        {
            GameObject marker = Instantiate(previewMarkerPrefab);
            marker.transform.position = tile.transform.position + new Vector3(0f, previewYOffset, 0f);
            marker.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            marker.transform.SetParent(transform);
            activePreviewMarkers.Add(marker);
        }
    }

    public void ClearPreview()
    {
        foreach (var marker in activePreviewMarkers)
        {
            if (marker != null) Destroy(marker);
        }
        activePreviewMarkers.Clear();
    }

    public void ClearMoveRange()
    {
        foreach (var obj in activeRangeHighlights)
        {
            if (obj != null) Destroy(obj);
        }
        activeRangeHighlights.Clear();
    }

    private void UpdateHoverHighlightAndPreview()
    {
        if (hoverHighlightInstance == null || highlightsSuppressed)
            return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            HexTile tile = hit.collider.GetComponent<HexTile>();
            if (tile == null || !tile.isWalkable)
            {
                hoverHighlightInstance.SetActive(false);
                currentHoverCoords = null;
                ClearPreview();
                return;
            }

            Vector2Int coords = tile.coordinate;
            if (coords != currentHoverCoords)
            {
                currentHoverCoords = coords;
                Vector3 pos = gridManager.GetPositionForHexFromCoordinate(coords) + Vector3.up * hoverYOffset;
                hoverHighlightInstance.transform.position = pos;
                hoverHighlightInstance.SetActive(true);

                ShowPreview(tile);
            }
        }
        else
        {
            hoverHighlightInstance.SetActive(false);
            currentHoverCoords = null;
            ClearPreview();
        }
    }

    private System.Collections.IEnumerator DelayedEnableHighlights()
    {
        highlightsSuppressed = true;
        yield return new WaitForSeconds(reenableDelay);
        highlightsSuppressed = false;

        ShowMoveRange(mover.CurrentGridCoords); // ✅ Re-show range after delay
    }

    private bool IsValidCoordinate(Vector2Int coord)
    {
        return coord.x >= 0 && coord.x < gridManager.gridSize.x &&
               coord.y >= 0 && coord.y < gridManager.gridSize.y;
    }
}
