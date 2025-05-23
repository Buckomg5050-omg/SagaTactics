using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(UnitMover))]
[RequireComponent(typeof(UnitAP))] // NEW: Ensure UnitAP is present
public class UnitHighlighter : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject hoverHighlightPrefab;
    [SerializeField] private GameObject rangeHighlightPrefab;
    [SerializeField] private GameObject attackHighlightPrefab;
    [SerializeField] private GameObject previewMarkerPrefab;

    [Header("Offsets")]
    [SerializeField] private float hoverYOffset = 0.3f;
    [SerializeField] private float rangeYOffset = 0.3f;
    [SerializeField] private float attackRangeYOffset = 0.3f;
    [SerializeField] private float previewYOffset = 0.31f;

    [Header("Reactivation Delay")]
    [SerializeField] private float reenableDelay = 0.25f;

    private GameObject hoverHighlightInstance;
    private List<GameObject> activeRangeHighlights = new();
    private List<GameObject> activeAttackHighlights = new();
    private List<GameObject> activePreviewMarkers = new();

    private HexGrid gridManager;
    private HexPathfinder pathfinder;
    private UnitMover mover;
    private Unit unit;
    private UnitAP unitAP; // NEW: Direct reference to UnitAP

    private Vector2Int? currentHoverCoords = null;
    private float hoverUpdateTimer = 0f;
    private float hoverUpdateInterval = 0.05f;
    private bool highlightsSuppressed = false;

    public bool IsInAttackMode { get; private set; } = false;

    void Awake()
    {
        unit = GetComponent<Unit>();
        unitAP = GetComponent<UnitAP>(); // NEW: Get UnitAP component
        mover = GetComponent<UnitMover>(); // Mover needs to be gotten before using it in checks

        if (unit == null || unit.Team != Unit.UnitTeam.Player || unitAP == null) // MODIFIED: Check unitAP
        {
            enabled = false;
            return;
        }

        gridManager = FindFirstObjectByType<HexGrid>();
        if (gridManager == null) { // Robustness check
            Debug.LogError("UnitHighlighter: HexGrid not found!", this);
            enabled = false;
            return;
        }
        pathfinder = new HexPathfinder(gridManager);
        

        if (hoverHighlightPrefab != null)
        {
            hoverHighlightInstance = Instantiate(hoverHighlightPrefab);
            hoverHighlightInstance.SetActive(false);
        }

        if (mover != null)
        {
            mover.OnMoveComplete += HandleMoveComplete;
        }
         else // Robustness check
        {
            Debug.LogError("UnitHighlighter: UnitMover not found!", this);
            enabled = false;
            return;
        }
    }

    void OnDestroy()
    {
        if (mover != null)
        {
            mover.OnMoveComplete -= HandleMoveComplete;
        }
    }
    
    private void HandleMoveComplete()
    {
        StartCoroutine(DelayedEnableHighlights());
    }

    void Update()
    {
        if (mover == null || gridManager == null || pathfinder == null || unitAP == null || highlightsSuppressed)
        {
            hoverHighlightInstance?.SetActive(false);
            ClearPreview();
            ClearMoveRange();
            ClearAttackRange();
            return;
        }
        
        bool isActivePlayerUnit = TacticalCombatManager.Instance != null &&
                                  TacticalCombatManager.Instance.CurrentUnit == unit &&
                                  TacticalCombatManager.Instance.IsPlayerTurn;

        if (!isActivePlayerUnit || mover.IsMoving)
        {
            hoverHighlightInstance?.SetActive(false);
            ClearPreview();
            return;
        }

        hoverUpdateTimer += Time.deltaTime;
        if (hoverUpdateTimer >= hoverUpdateInterval)
        {
            hoverUpdateTimer = 0f;
            UpdateHoverHighlightAndPreview();
        }
    }

    public void ToggleAttackMode(bool enabled)
    {
        IsInAttackMode = enabled;
        ClearPreview(); 
        StartCoroutine(DelayedHighlightUpdateAfterModeToggle());
    }

    private System.Collections.IEnumerator DelayedHighlightUpdateAfterModeToggle()
    {
        highlightsSuppressed = true;
        ClearMoveRange();
        ClearAttackRange();
        yield return new WaitForSeconds(0.05f); 
        highlightsSuppressed = false;
        UpdateHighlightsBasedOnMode();
    }

    public void UpdateHighlightsBasedOnMode()
    {
        if (mover == null || highlightsSuppressed || unitAP == null) return;

        ClearMoveRange();
        ClearAttackRange();

        bool isActivePlayerUnit = TacticalCombatManager.Instance != null &&
                                  TacticalCombatManager.Instance.CurrentUnit == unit &&
                                  TacticalCombatManager.Instance.IsPlayerTurn;
        if (!isActivePlayerUnit) return;

        if (IsInAttackMode)
        {
            UnitCombat combat = GetComponent<UnitCombat>();
            if (combat != null)
            {
                ShowAttackRange(mover.CurrentGridCoords, UnitCombat.MELEE_ATTACK_RANGE);
            }
        }
        else
        {
            ShowMoveRange(mover.CurrentGridCoords);
        }
    }

    public void ShowMoveRange(Vector2Int centerCoord)
    {
        if (mover == null || gridManager == null || highlightsSuppressed || IsInAttackMode || unitAP == null) // MODIFIED: Added unitAP null check
        {
            return;
        }
        ClearMoveRange();

        int maxApCostForMove = unitAP.CurrentAP; // MODIFIED: Use this.unitAP

        Dictionary<Vector2Int, float> visited = new();
        Queue<(Vector2Int coord, float cost)> frontier = new();

        visited[centerCoord] = 0;
        frontier.Enqueue((centerCoord, 0));

        while (frontier.Count > 0)
        {
            var (current, costSoFar) = frontier.Dequeue();
            HexTile currentTile = gridManager.GetTileAt(current);
            if (currentTile == null || !currentTile.isWalkable) continue;

            if (current != mover.CurrentGridCoords && costSoFar <= maxApCostForMove)
            {
                Vector3 worldPos = gridManager.GetPositionForHexFromCoordinate(current) + Vector3.up * rangeYOffset;
                GameObject marker = Instantiate(rangeHighlightPrefab, worldPos, Quaternion.identity);
                activeRangeHighlights.Add(marker);
            }

            if (costSoFar < maxApCostForMove)
            {
                foreach (HexTile neighbor in gridManager.GetNeighbors(currentTile))
                {
                    if (neighbor == null || !neighbor.isWalkable) continue;
                    float newCost = costSoFar + neighbor.tileType.moveCost;
                    Vector2Int nextCoord = neighbor.coordinate;
                    if (newCost <= maxApCostForMove && (!visited.ContainsKey(nextCoord) || newCost < visited[nextCoord]))
                    {
                        visited[nextCoord] = newCost;
                        frontier.Enqueue((nextCoord, newCost));
                    }
                }
            }
        }
    }
    
    public void ShowAttackRange(Vector2Int centerCoord, int attackRange)
    {
        if (gridManager == null || highlightsSuppressed || !IsInAttackMode || unit == null) // MODIFIED: Added unit null check
        {
            return;
        }
        ClearAttackRange();

        HexTile currentUnitTile = gridManager.GetTileAt(centerCoord);
        if (currentUnitTile == null) return;

        if (attackRange == 1)
        {
            foreach (HexTile neighbor in gridManager.GetNeighbors(currentUnitTile))
            {
                if (neighbor != null)
                {
                    // This line will still error until HexGrid.GetUnitOnTile is implemented
                    Unit unitOnTile = gridManager.GetUnitOnTile(neighbor.coordinate); 
                    if (unitOnTile != null && unitOnTile.Team != unit.Team)
                    {
                        UnitCombat targetCombat = unitOnTile.GetComponent<UnitCombat>();
                        if (targetCombat != null && !targetCombat.IsDead())
                        {
                            Vector3 worldPos = gridManager.GetPositionForHexFromCoordinate(neighbor.coordinate) + Vector3.up * attackRangeYOffset;
                            GameObject markerPrefabToUse = attackHighlightPrefab != null ? attackHighlightPrefab : rangeHighlightPrefab;
                            GameObject marker = Instantiate(markerPrefabToUse, worldPos, Quaternion.identity);
                            activeAttackHighlights.Add(marker);
                        }
                    }
                }
            }
        }
    }

    public void ShowPreview(HexTile targetTile)
    {
        ClearPreview();
        if (IsInAttackMode || mover == null || gridManager == null || highlightsSuppressed || mover.IsMoving || targetTile == null || !targetTile.isWalkable || unitAP == null) // MODIFIED: Added unitAP null check
            return;

        HexTile startTile = gridManager.GetTileAt(mover.CurrentGridCoords);
        if (startTile == null) return;

        List<HexTile> path = pathfinder.FindPath(startTile, targetTile);
        if (path == null || path.Count == 0) return;

        int pathApCost = 0;
        for (int i = 1; i < path.Count; i++)
        {
            if (path[i] == null || path[i].tileType == null) { // Robustness for path data
                Debug.LogWarning("Null tile or tileType in path for preview.", this);
                return;
            }
            pathApCost += path[i].tileType.moveCost;
        }

        int currentUnitAP = unitAP.CurrentAP; // MODIFIED: Use this.unitAP
        if (pathApCost > currentUnitAP)
        {
            return;
        }

        foreach (HexTile tile in path)
        {
            if (tile == startTile && path.Count > 1) continue;
            GameObject marker = Instantiate(previewMarkerPrefab);
            marker.transform.position = gridManager.GetPositionForHexFromCoordinate(tile.coordinate) + Vector3.up * previewYOffset;
            activePreviewMarkers.Add(marker);
        }
    }

    public void ClearPreview()
    {
        foreach (var marker in activePreviewMarkers) { if (marker != null) Destroy(marker); }
        activePreviewMarkers.Clear();
    }

    public void ClearMoveRange()
    {
        foreach (var obj in activeRangeHighlights) { if (obj != null) Destroy(obj); }
        activeRangeHighlights.Clear();
    }
    
    public void ClearAttackRange()
    {
        foreach (var obj in activeAttackHighlights) { if (obj != null) Destroy(obj); }
        activeAttackHighlights.Clear();
    }

    private void UpdateHoverHighlightAndPreview()
    {
        if (hoverHighlightInstance == null || highlightsSuppressed || gridManager == null) return;
        if (Mouse.current == null) return; 
        Vector2 screenPos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            HexTile tile = hit.collider.GetComponent<HexTile>();
            if (tile == null || (!tile.isWalkable && !IsInAttackMode) )
            {
                hoverHighlightInstance.SetActive(false);
                currentHoverCoords = null;
                if(!IsInAttackMode) ClearPreview();
                return;
            }

            Vector2Int coords = tile.coordinate;
            if (coords != currentHoverCoords)
            {
                currentHoverCoords = coords;
                Vector3 pos = gridManager.GetPositionForHexFromCoordinate(coords) + Vector3.up * hoverYOffset;
                hoverHighlightInstance.transform.position = pos;
                hoverHighlightInstance.SetActive(true);
                if (!IsInAttackMode && tile.isWalkable)
                {
                    ShowPreview(tile);
                } else {
                    ClearPreview();
                }
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
        ClearMoveRange();
        ClearAttackRange();
        ClearPreview();
        hoverHighlightInstance?.SetActive(false);
        yield return new WaitForSeconds(reenableDelay);
        highlightsSuppressed = false;
        bool isActivePlayerUnit = TacticalCombatManager.Instance != null &&
                                  TacticalCombatManager.Instance.CurrentUnit == unit &&
                                  TacticalCombatManager.Instance.IsPlayerTurn;
        if (isActivePlayerUnit) {
            UpdateHighlightsBasedOnMode();
        }
    }
}