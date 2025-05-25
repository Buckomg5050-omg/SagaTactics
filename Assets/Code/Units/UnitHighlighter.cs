// File: UnitHighlighter.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Text;

[RequireComponent(typeof(UnitMover))]
[RequireComponent(typeof(UnitAP))]
public class UnitHighlighter : MonoBehaviour
{
    // ... (Fields remain the same as your last version) ...
    [Header("Prefabs")]
    [SerializeField] private GameObject hoverHighlightPrefab;
    [SerializeField] private GameObject rangeHighlightPrefab; // Used for movement
    [SerializeField] private GameObject attackHighlightPrefab; // Used for attack range
    [SerializeField] private GameObject previewMarkerPrefab;

    [Header("Offsets")]
    [SerializeField] private float hoverYOffset = 0.3f;
    [SerializeField] private float rangeYOffset = 0.3f;     // For movement
    [SerializeField] private float attackRangeYOffset = 0.3f; // For attack
    [SerializeField] private float previewYOffset = 0.31f;

    [Header("Raycasting Settings")]
    [SerializeField] private LayerMask groundLayerMask;

    [Header("Reactivation Delay")]
    [SerializeField] private float reenableDelay = 0.25f;

    private GameObject hoverHighlightInstance;
    private List<GameObject> activeRangeHighlights = new List<GameObject>();
    private List<GameObject> activeAttackHighlights = new List<GameObject>();
    private List<GameObject> activePreviewMarkers = new List<GameObject>();

    private HexGrid gridManager;
    private HexPathfinder pathfinder;
    private UnitMover mover;
    private Unit unit;
    private UnitStats unitStats;
    private UnitAP unitAP;

    private Vector2Int? currentHoverCoords = null;
    private float hoverUpdateTimer = 0f;
    private float hoverUpdateInterval = 0.05f;
    private bool highlightsSuppressed = false;

    public bool IsInAttackMode { get; private set; } = false;
    private string UHN => $"UH_{unit?.unitName ?? (gameObject != null ? gameObject.name : "UnknownHighlighter")}";


    void Awake()
    {
        unit = GetComponent<Unit>();
        if (unit == null) { Debug.LogError($"UnitHighlighter on {gameObject.name} is missing Unit component!", this); enabled = false; return; }

        unitStats = unit.unitStats;
        unitAP = unit.unitAP;
        mover = GetComponent<UnitMover>();

        if (unit.Team != Unit.UnitTeam.Player || unitStats == null || unitAP == null || mover == null)
        {
            enabled = false;
            return;
        }

        gridManager = FindFirstObjectByType<HexGrid>();
        if (gridManager == null)
        {
            Debug.LogError($"{UHN}: HexGrid not found!", this);
            enabled = false;
            return;
        }
        pathfinder = new HexPathfinder(gridManager);
        // Debug.Log($"{UHN}: Awake complete. Ready.", this);


        if (hoverHighlightPrefab != null)
        {
            hoverHighlightInstance = Instantiate(hoverHighlightPrefab);
            if(this.transform != null) hoverHighlightInstance.transform.SetParent(this.transform);
            hoverHighlightInstance.SetActive(false);
        }

        if (mover != null)
        {
            mover.OnMoveComplete += HandleMoveComplete;
        }
        TacticalCombatManager.OnCombatEnd += HandleCombatEnd;
    }

    void OnDestroy()
    {
        // Debug.Log($"{UHN}: OnDestroy called.", this);
        if (mover != null)
        {
            mover.OnMoveComplete -= HandleMoveComplete;
        }
        TacticalCombatManager.OnCombatEnd -= HandleCombatEnd;

        ClearAllHighlightsAndDestroyInstances();
    }

    private void HandleMoveComplete()
    {
        // Debug.Log($"{UHN}: HandleMoveComplete triggered.", this);
        if (gameObject.activeInHierarchy && enabled) 
        {
            StartCoroutine(DelayedEnableHighlightsAfterMove());
        } 
        // else { Debug.LogWarning($"{UHN}: HandleMoveComplete - Not starting coroutine as GameObject is inactive or component disabled.", this); }
    }


    private void HandleCombatEnd(Unit.UnitTeam winningTeam)
    {
        // Debug.Log($"{UHN}: HandleCombatEnd triggered. Winning Team: {winningTeam}. Disabling highlighter.", this);
        highlightsSuppressed = true;
        ClearAllHighlightsAndDestroyInstances();
        if (this != null && gameObject != null && gameObject.activeInHierarchy) 
        {
            enabled = false;
        }
    }

    private void ClearAllHighlightsAndDestroyInstances()
    {
        // Debug.Log($"{UHN}: ClearAllHighlightsAndDestroyInstances called.", this);
        ClearAndDestroyList(activeRangeHighlights);
        ClearAndDestroyList(activeAttackHighlights);
        ClearAndDestroyList(activePreviewMarkers);

        if (hoverHighlightInstance != null)
        {
            Destroy(hoverHighlightInstance);
            hoverHighlightInstance = null;
        }
    }
    
    // MODIFICATION: Make sure this is the only place GameObjects are destroyed from lists
    private void ClearAndDestroyList(List<GameObject> highlightList) 
    {
        // Debug.Log($"{UHN}: ClearAndDestroyList for list with {highlightList.Count} items.", this);
        for (int i = highlightList.Count - 1; i >= 0; i--) 
        {
            if (highlightList[i] != null) 
            {
                // Debug.Log($"{UHN}: Destroying {highlightList[i].name}", this);
                Destroy(highlightList[i]);
            }
        }
        highlightList.Clear();
    }

    // These methods now *only* call the destructive clear.
    public void ClearMoveRangeVisuals() { ClearAndDestroyList(activeRangeHighlights); }
    public void ClearAttackRangeVisuals() { ClearAndDestroyList(activeAttackHighlights); }
    public void ClearPreviewVisuals() { ClearAndDestroyList(activePreviewMarkers); }


    void Update()
    {
        if (!enabled) return;

        if (highlightsSuppressed || unit == null || mover == null || gridManager == null || pathfinder == null || unitAP == null || unitStats == null)
        {
            if (hoverHighlightInstance != null) hoverHighlightInstance.SetActive(false);
            return;
        }

        Unit currentActiveUnit = TacticalCombatManager.Instance?.CurrentUnit; // Use ?. for safety
        bool isPlayerActiveTurn = TacticalCombatManager.Instance != null && TacticalCombatManager.Instance.IsPlayerTurn;


        if (currentActiveUnit == null && TacticalCombatManager.Instance != null) { // Combat might be starting/ending
             if (hoverHighlightInstance != null) hoverHighlightInstance.SetActive(false);
             ClearPreviewVisuals();
             return;
        }


        bool isActiveUnitThisHighlightersUnit = (currentActiveUnit == unit);

        if (!isActiveUnitThisHighlightersUnit || !isPlayerActiveTurn || mover.IsMoving)
        {
            if (hoverHighlightInstance != null) hoverHighlightInstance.SetActive(false);
            ClearPreviewVisuals();
            if (!isActiveUnitThisHighlightersUnit)
            {
                ClearMoveRangeVisuals();
                ClearAttackRangeVisuals();
            }
            return;
        }

        hoverUpdateTimer += Time.deltaTime;
        if (hoverUpdateTimer >= hoverUpdateInterval)
        {
            hoverUpdateTimer = 0f;
            UpdateHoverHighlightAndPreview();
        }
    }

    public void ToggleAttackMode(bool shouldBeInAttackMode)
    {
        if (!enabled) return;
        // Debug.Log($"{UHN}: ToggleAttackMode called. Setting IsInAttackMode to: {shouldBeInAttackMode}", this);
        IsInAttackMode = shouldBeInAttackMode;
        ClearPreviewVisuals(); // Preview is always cleared on mode toggle
        if (gameObject.activeInHierarchy) 
        {
            StartCoroutine(DelayedHighlightUpdateAfterModeToggle());
        }
    }

    private System.Collections.IEnumerator DelayedHighlightUpdateAfterModeToggle()
    {
        // Debug.Log($"{UHN}: Coroutine DelayedHighlightUpdateAfterModeToggle START. Suppressing highlights.", this);
        highlightsSuppressed = true;
        // Clear visuals immediately before suppression ends and new ones are shown
        ClearMoveRangeVisuals();
        ClearAttackRangeVisuals();
        yield return new WaitForSeconds(0.05f); // Very short delay
        highlightsSuppressed = false;
        // Debug.Log($"{UHN}: Coroutine DelayedHighlightUpdateAfterModeToggle END. Unsuppressed. Enabled:{enabled}, UnitNull:{unit == null}", this);
        if (enabled && unit != null)
        {
            UpdateHighlightsBasedOnMode();
        }
        // else { Debug.LogWarning($"{UHN}: Coroutine DelayedHighlightUpdateAfterModeToggle conditions not met to update highlights after delay. Enabled: {enabled}, Unit null: {unit == null}", this); }
    }

    public void UpdateHighlightsBasedOnMode()
    {
        if (!enabled || mover == null || highlightsSuppressed || unitAP == null || unitStats == null)
        {
            // Debug.LogWarning($"{UHN} UpdateHighlightsBasedOnMode: Bailed early. Enabled:{enabled}, MoverNull:{mover == null}, Suppressed:{highlightsSuppressed}, APNull:{unitAP == null}, StatsNull:{unitStats == null}", this);
            return;
        }
        // Debug.Log($"{UHN}: UpdateHighlightsBasedOnMode called. IsInAttackMode: {IsInAttackMode}. Unit AP: {unitAP.CurrentAP}", this);

        // Clear visuals first, then show new ones.
        ClearMoveRangeVisuals(); 
        ClearAttackRangeVisuals();

        bool isActivePlayerUnit = false;
        if (TacticalCombatManager.Instance != null && TacticalCombatManager.Instance.CurrentUnit == unit)
        {
            isActivePlayerUnit = TacticalCombatManager.Instance.IsPlayerTurn;
        }
        if (!isActivePlayerUnit)
        {
            // Debug.LogWarning($"{UHN} UpdateHighlightsBasedOnMode: Not this unit's active player turn. Bailing.", this);
            return;
        }

        if (IsInAttackMode)
        {
            // Debug.Log($"{UHN}: UpdateHighlightsBasedOnMode -> Calling ShowAttackRange.", this);
            ShowAttackRange(mover.CurrentGridCoords);
        }
        else
        {
            // Debug.Log($"{UHN}: UpdateHighlightsBasedOnMode -> Calling ShowMoveRange.", this);
            ShowMoveRange(mover.CurrentGridCoords);
        }
    }

    public void ShowMoveRange(Vector2Int centerCoordParam) // Renamed param to avoid confusion
    {
        if (!enabled || mover == null || gridManager == null || highlightsSuppressed || IsInAttackMode || unitAP == null || unitStats == null)
        {
            // Debug.LogWarning($"{UHN} ShowMoveRange: Bailed early due to initial checks. Param Center: {centerCoordParam}", this);
            return;
        }
        
        // *** ENSURE VISUALS ARE CLEARED BEFORE ADDING NEW ONES ***
        ClearMoveRangeVisuals(); 

        Vector2Int actualStartCoord = mover.CurrentGridCoords;
        // Debug.Log($"{UHN}: ShowMoveRange START for unit at {actualStartCoord} (Param was {centerCoordParam}). AP: {unitAP.CurrentAP}", this);

        int maxApCostForMove = unitAP.CurrentAP;
        if (maxApCostForMove <= 0)
        {
            // Debug.Log($"{UHN}: ShowMoveRange - No AP ({maxApCostForMove}) to show range from {actualStartCoord}.", this);
            // Debug.Log($"{UHN}: ShowMoveRange END for {actualStartCoord}. Added 0 move range highlights (due to no AP).", this);
            return;
        }

        Dictionary<Vector2Int, float> visited = new Dictionary<Vector2Int, float>();
        Queue<(Vector2Int coord, float cost)> frontier = new Queue<(Vector2Int coord, float cost)>();

        visited[actualStartCoord] = 0;
        frontier.Enqueue((actualStartCoord, 0));
        int highlightsAdded = 0;
        // StringBuilder bfsLog = new StringBuilder(); 

        while (frontier.Count > 0)
        {
            var (current, costSoFar) = frontier.Dequeue();
            // bfsLog.Append($"\n  BFS: Dequeued {current}, cost {costSoFar}. ");

            HexTile currentTile = gridManager.GetTileAt(current);
            if (currentTile == null) { /*bfsLog.Append($"Tile null. ");*/ continue; }
            if (!currentTile.isWalkable) { /*bfsLog.Append($"Not walkable. ");*/ continue; }

            Unit unitOnTile = gridManager.GetUnitOnTile(current);
            if (unitOnTile != null && unitOnTile != unit) { /*bfsLog.Append($"Occupied by {unitOnTile.unitName}. ");*/ continue; }

            // bfsLog.Append($"Valid tile. ");

            if (current != actualStartCoord && costSoFar <= maxApCostForMove)
            {
                // bfsLog.Append($"Highlighting {current}. ");
                Vector3 worldPos = gridManager.GetPositionForHexFromCoordinate(current) + Vector3.up * rangeYOffset;
                GameObject marker = Instantiate(rangeHighlightPrefab, worldPos, Quaternion.identity);
                if (this.transform != null) marker.transform.SetParent(this.transform);
                activeRangeHighlights.Add(marker);
                highlightsAdded++;
            }
            // else if (current == actualStartCoord) { /*bfsLog.Append($"Is start tile, not highlighting. ");*/ }
            // else if (costSoFar > maxApCostForMove) { /*bfsLog.Append($"Cost {costSoFar} > MaxAP {maxApCostForMove}, not highlighting. ");*/ }

            if (costSoFar < maxApCostForMove)
            {
                // bfsLog.Append($"Exploring neighbors of {current}: ");
                // int neighborCount = 0;
                foreach (HexTile neighbor in gridManager.GetNeighbors(currentTile))
                {
                    // neighborCount++;
                    if (neighbor == null) { /*bfsLog.Append($"N{neighborCount} null. ");*/ continue; }
                    if (!neighbor.isWalkable) { /*bfsLog.Append($"N{neighborCount} {neighbor.coordinate} not walkable. ");*/ continue; }

                    Unit unitOnNeighbor = gridManager.GetUnitOnTile(neighbor.coordinate);
                    if (unitOnNeighbor != null && unitOnNeighbor != unit) { /*bfsLog.Append($"N{neighborCount} {neighbor.coordinate} occupied by {unitOnNeighbor.unitName}. ");*/ continue; }

                    float stepCost = (neighbor.tileType != null) ? neighbor.tileType.moveCost : 1f; 
                    if (stepCost <= 0) stepCost = 1;
                    float newCost = costSoFar + stepCost;
                    Vector2Int nextCoord = neighbor.coordinate;

                    if (newCost <= maxApCostForMove)
                    {
                        if (!visited.ContainsKey(nextCoord) || newCost < visited[nextCoord])
                        {
                            // bfsLog.Append($"N{neighborCount} Enqueueing {nextCoord} (cost {newCost}). ");
                            visited[nextCoord] = newCost;
                            frontier.Enqueue((nextCoord, newCost));
                        } 
                        // else { /*bfsLog.Append($"N{neighborCount} {nextCoord} visited or higher cost. ");*/ }
                    } 
                    // else { /*bfsLog.Append($"N{neighborCount} {nextCoord} newCost {newCost} > maxAP {maxApCostForMove}. ");*/ }
                }
                // if (neighborCount == 0 && gridManager.GetNeighbors(currentTile).Count == 0) bfsLog.Append("No neighbors found (method returned empty). ");
                // else if (neighborCount == 0) bfsLog.Append("All neighbors were invalid. ");
            } 
            // else { /*bfsLog.Append($"Max AP ({maxApCostForMove}) reached for {current}, not exploring neighbors. ");*/ }
        }
        // if(highlightsAdded > 0 || frontier.Count == 0) 
            //  Debug.Log($"{UHN}: ShowMoveRange BFS Path Trace for {actualStartCoord}:{bfsLog.ToString()}", this);
        // Debug.Log($"{UHN}: ShowMoveRange END for {actualStartCoord}. Added {highlightsAdded} move range highlights. MaxAP: {maxApCostForMove}", this);
    }

    public void ShowAttackRange(Vector2Int centerCoordParam) // Renamed param
    {
        if (!enabled || gridManager == null || highlightsSuppressed || !IsInAttackMode || unit == null || unitStats == null || unitAP == null)
        {
            // Debug.LogWarning($"{UHN} ShowAttackRange: Bailed early due to initial checks. Param Center: {centerCoordParam}", this);
            return;
        }
        
        // *** ENSURE VISUALS ARE CLEARED BEFORE ADDING NEW ONES ***
        ClearAttackRangeVisuals(); 

        Vector2Int actualStartCoord = mover.CurrentGridCoords;
        // Debug.Log($"{UHN}: ShowAttackRange START for unit at {actualStartCoord} (Param was {centerCoordParam}). MeleeR:{unitStats.meleeAttackRange}, RangedR:{unitStats.rangedAttackRange}, AP:{unitAP.CurrentAP}", this);
        
        HexTile currentUnitTile = gridManager.GetTileAt(actualStartCoord);
        if (currentUnitTile == null) { /*Debug.LogWarning($"{UHN} ShowAttackRange: Current unit tile at {actualStartCoord} is null.", this);*/ return; }

        int maxDisplayRange = Mathf.Max(unitStats.meleeAttackRange, unitStats.rangedAttackRange);
        if (maxDisplayRange <= 0) { /*Debug.Log($"{UHN}: ShowAttackRange - No attack range defined.", this);*/ return; }

        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        Dictionary<Vector2Int, int> distanceMap = new Dictionary<Vector2Int, int>();
        frontier.Enqueue(actualStartCoord);
        distanceMap[actualStartCoord] = 0;
        int highlightsAdded = 0;

        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();
            int currentDist = distanceMap[current]; 
            Unit targetUnitOnTile = gridManager.GetUnitOnTile(current);
            bool canHighlightThisTileAsTarget = false;

            if (targetUnitOnTile != null && targetUnitOnTile.Team != unit.Team)
            {
                UnitCombat targetCombat = targetUnitOnTile.GetComponent<UnitCombat>();
                if (targetCombat != null && !targetCombat.IsDead())
                {
                    bool inMeleeRange = unitStats.meleeAttackRange > 0 && currentDist <= unitStats.meleeAttackRange;
                    bool canAffordMelee = unitAP.CanSpend(unitStats.meleeAPCost);
                    bool inRangedRange = unitStats.rangedAttackRange > 0 && currentDist <= unitStats.rangedAttackRange;
                    bool canAffordRanged = unitAP.CanSpend(unitStats.rangedAPCost);

                    if ((inMeleeRange && canAffordMelee) || (inRangedRange && canAffordRanged))
                    {
                        bool hasLOS = true; 
                        if (inRangedRange && canAffordRanged && (!inMeleeRange || !canAffordMelee || currentDist > unitStats.meleeAttackRange))
                        {
                            Vector3 eyePosition = transform.position + Vector3.up * 1.5f;
                            Vector3 targetCenter = targetUnitOnTile.transform.position + Vector3.up * 1.0f;
                            int losCheckLayerMask = ~LayerMask.GetMask("Ignore Raycast", "PlayerUnit", "EnemyUnit"); 
                            RaycastHit hitInfo;
                            if (Physics.Linecast(eyePosition, targetCenter, out hitInfo, losCheckLayerMask, QueryTriggerInteraction.Ignore))
                            {
                                if (hitInfo.transform != targetUnitOnTile.transform && !hitInfo.transform.IsChildOf(targetUnitOnTile.transform))
                                {
                                    hasLOS = false;
                                }
                            }
                        }
                        if (hasLOS) canHighlightThisTileAsTarget = true;
                    }
                }
            }

            if (canHighlightThisTileAsTarget && current != actualStartCoord)
            {
                Vector3 worldPos = gridManager.GetPositionForHexFromCoordinate(current) + Vector3.up * attackRangeYOffset;
                GameObject markerPrefabToUse = attackHighlightPrefab != null ? attackHighlightPrefab : rangeHighlightPrefab;
                GameObject marker = Instantiate(markerPrefabToUse, worldPos, Quaternion.identity);
                if (this.transform != null) marker.transform.SetParent(this.transform);
                activeAttackHighlights.Add(marker);
                highlightsAdded++;
            }

            if (currentDist < maxDisplayRange)
            {
                HexTile tileObj = gridManager.GetTileAt(current);
                if (tileObj != null)
                {
                    foreach (HexTile neighbor in gridManager.GetNeighbors(tileObj))
                    {
                        if (neighbor != null && !distanceMap.ContainsKey(neighbor.coordinate))
                        {
                            distanceMap[neighbor.coordinate] = currentDist + 1; 
                            frontier.Enqueue(neighbor.coordinate);
                        }
                    }
                }
            }
        }
        // Debug.Log($"{UHN}: ShowAttackRange END for {actualStartCoord}. Added {highlightsAdded} attack range highlights. Max Display Range: {maxDisplayRange}", this);
    }


    public void ShowPreview(HexTile targetTile)
    {
        if (!enabled || IsInAttackMode || mover == null || gridManager == null || highlightsSuppressed || mover.IsMoving || targetTile == null || !targetTile.isWalkable || unitAP == null || unitStats == null)
            return;
        
        // *** ENSURE VISUALS ARE CLEARED BEFORE ADDING NEW ONES ***
        ClearPreviewVisuals(); 
        
        Vector2Int actualStartCoord = mover.CurrentGridCoords;
        HexTile startTileObj = gridManager.GetTileAt(actualStartCoord); 
        if (startTileObj == null) { /*Debug.LogWarning($"{UHN} ShowPreview: Start tile at {actualStartCoord} is null.", this);*/ return; }

        List<HexTile> path = pathfinder.FindPath(startTileObj, targetTile); 
        if (path == null || path.Count < 2) { return; }

        int pathApCost = 0;
        for (int i = 1; i < path.Count; i++)
        {
            if (path[i] == null || path[i].tileType == null)
            {
                // Debug.LogWarning($"{UHN} ShowPreview: Null tile or tileType in path at index {i}.", this);
                return;
            }
            pathApCost += path[i].tileType.moveCost;
        }

        if (pathApCost > unitAP.CurrentAP) { return; }

        // int markersAdded = 0;
        for (int i = 1; i < path.Count; i++)
        {
            HexTile tile = path[i];
            GameObject marker = Instantiate(previewMarkerPrefab);
            marker.transform.position = gridManager.GetPositionForHexFromCoordinate(tile.coordinate) + Vector3.up * previewYOffset;
            if (this.transform != null) marker.transform.SetParent(this.transform);
            activePreviewMarkers.Add(marker);
            // markersAdded++;
        }
    }

    private void UpdateHoverHighlightAndPreview()
    {
        if (!enabled || highlightsSuppressed || gridManager == null || Camera.main == null)
        {
            if (hoverHighlightInstance != null) hoverHighlightInstance.SetActive(false);
            return;
        }

        if (Mouse.current == null)
        {
            if (hoverHighlightInstance != null) hoverHighlightInstance.SetActive(false);
            currentHoverCoords = null;
            ClearPreviewVisuals();
            return;
        }
        Vector2 screenPos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, groundLayerMask))
        {
            HexTile tile = hit.collider.GetComponent<HexTile>();
            if (tile == null || (!tile.isWalkable && !IsInAttackMode))
            {
                if (hoverHighlightInstance != null) hoverHighlightInstance.SetActive(false);
                currentHoverCoords = null;
                if (!IsInAttackMode) ClearPreviewVisuals();
                return;
            }

            Vector2Int coords = tile.coordinate;
            if (coords != currentHoverCoords)
            {
                currentHoverCoords = coords;
                if (hoverHighlightInstance != null)
                {
                    Vector3 pos = gridManager.GetPositionForHexFromCoordinate(coords) + Vector3.up * hoverYOffset;
                    hoverHighlightInstance.transform.position = pos;
                    hoverHighlightInstance.SetActive(true);
                }

                if (!IsInAttackMode && tile.isWalkable)
                {
                    ShowPreview(tile);
                }
                else
                {
                    ClearPreviewVisuals();
                }
            }
        }
        else
        {
            if (hoverHighlightInstance != null) hoverHighlightInstance.SetActive(false);
            currentHoverCoords = null;
            ClearPreviewVisuals();
        }
    }

    private System.Collections.IEnumerator DelayedEnableHighlightsAfterMove()
    {
        // Debug.Log($"{UHN}: Coroutine DelayedEnableHighlightsAfterMove START. Suppressing highlights.", this);
        highlightsSuppressed = true;
        // Don't destroy here, just clear the lists for a moment.
        // Actual destruction happens if UpdateHighlightsBasedOnMode calls the Clear...Visuals methods.
        activeRangeHighlights.Clear(); 
        activeAttackHighlights.Clear();
        activePreviewMarkers.Clear();
        if (hoverHighlightInstance != null) hoverHighlightInstance.SetActive(false);

        yield return new WaitForSeconds(reenableDelay);
        highlightsSuppressed = false;
        // Debug.Log($"{UHN}: Coroutine DelayedEnableHighlightsAfterMove END. Unsuppressed. Enabled:{enabled}, UnitNull:{unit == null}", this);


        if (!enabled || unit == null || unitStats == null) {
            // Debug.LogWarning($"{UHN}: Coroutine DelayedEnableHighlightsAfterMove conditions not met for highlight update (comp disabled or unit/stats null).", this);
            yield break;
        }

        bool isActivePlayerUnit = false;
        if (TacticalCombatManager.Instance != null && TacticalCombatManager.Instance.CurrentUnit == unit)
        {
            isActivePlayerUnit = TacticalCombatManager.Instance.IsPlayerTurn;
        }

        if (isActivePlayerUnit)
        {
            // Debug.Log($"{UHN}: Coroutine DelayedEnableHighlightsAfterMove -> Calling UpdateHighlightsBasedOnMode.", this);
            UpdateHighlightsBasedOnMode();
        } 
        // else { Debug.LogWarning($"{UHN}: Coroutine DelayedEnableHighlightsAfterMove -> Not active player unit's turn ({TacticalCombatManager.Instance?.CurrentUnit?.unitName}), not updating highlights for {unit.unitName}.", this); }
    }
}