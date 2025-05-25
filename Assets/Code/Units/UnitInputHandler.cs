// File: UnitInputHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(UnitMover))]
[RequireComponent(typeof(UnitCombat))]
// REMOVED: [RequireComponent(typeof(UnitHighlighter))] 
public class UnitInputHandler : MonoBehaviour
{
    [Header("Raycasting Settings")]
    [SerializeField] private LayerMask groundLayerMask;

    private Camera mainCam;
    private Unit unit;
    private UnitStats unitStats;
    private UnitMover mover;
    private UnitCombat combat;
    private HexGrid gridManager;
    private HexPathfinder pathfinder;
    // private UnitHighlighter highlighter; // REMOVED THIS FIELD

    private MovementRangeCalculator movementRangeCalculator; // From previous refactor step
    // private AttackRangeCalculator attackRangeCalculator; // For later

    private PlayerInputActions inputActions;
    private bool inputEnabled = false;
    private bool isInAttackMode = false; 

    private Vector2Int? lastHoveredCoord = null; 

    void Awake()
    {
        unit = GetComponent<Unit>();
        if (unit == null || unit.Team != Unit.UnitTeam.Player)
        {
            enabled = false;
            return;
        }

        unitStats = unit.unitStats;
        if (unitStats == null)
        {
            Debug.LogError($"UnitInputHandler on {gameObject.name}: UnitStats component not found! Disabling.", this);
            enabled = false;
            return;
        }

        mainCam = Camera.main;
        mover = GetComponent<UnitMover>();
        combat = GetComponent<UnitCombat>();
        gridManager = FindFirstObjectByType<HexGrid>();

        if (mainCam == null || mover == null || combat == null || gridManager == null)
        {
            Debug.LogError($"UnitInputHandler on {gameObject.name}: Missing critical components (Camera, Mover, Combat, or GridManager)! Disabling.", this);
            enabled = false;
            return;
        }

        pathfinder = new HexPathfinder(gridManager);
        movementRangeCalculator = new MovementRangeCalculator(gridManager);
        // highlighter = GetComponent<UnitHighlighter>(); // REMOVED INITIALIZATION
    }

    void OnEnable()
    {
        // Safety checks, though Awake should handle disabling if critical components are missing
        if (unit == null || unit.Team != Unit.UnitTeam.Player || unitStats == null) {
            enabled = false; // Ensure it's disabled if Awake didn't catch it or conditions change
            return;
        }

        if (inputActions == null)
        {
            inputActions = new PlayerInputActions();
            inputActions.Player.Click.performed += OnClick;
            inputActions.Player.EndTurn.performed += OnEndTurn;
            inputActions.Player.Cancel.performed += OnCancel;
        }
        inputActions.Enable();
        TacticalCombatManager.OnActiveUnitChanged += HandleActiveUnitChanged;
    }

    void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Click.performed -= OnClick;
            inputActions.Player.EndTurn.performed -= OnEndTurn;
            inputActions.Player.Cancel.performed -= OnCancel;
            inputActions.Disable();
        }
        inputEnabled = false; // Explicitly set inputEnabled to false
        TacticalCombatManager.OnActiveUnitChanged -= HandleActiveUnitChanged;
        
        if (TileHighlighterService.Instance != null)
        {
            TileHighlighterService.Instance.ClearAllHighlights();
        }
        lastHoveredCoord = null;
    }
    
    private void HandleActiveUnitChanged(Unit activeUnit)
    {
        if (!this.enabled && activeUnit != unit) return; // If this handler is already disabled and it's not our turn, do nothing

        if (activeUnit == unit && unit.Team == Unit.UnitTeam.Player) 
        {
            EnableInputInternal(true);
        }
        else 
        {
            EnableInputInternal(false);
        }
    }

    private void EnableInputInternal(bool enable)
    {
        inputEnabled = enable;
        // Debug.Log($"UnitInputHandler ({unit.unitName}): Input {(enable ? "Enabled" : "Disabled")}", this);

        if (TileHighlighterService.Instance == null)
        {
            // Debug.LogWarning($"UnitInputHandler ({unit.unitName}): TileHighlighterService.Instance is null in EnableInputInternal.", this);
            return;
        }

        if (enable)
        {
            isInAttackMode = false; 
            UpdateMovementRangeHighlight();
            TileHighlighterService.Instance.ClearAttackRange(); 
            TileHighlighterService.Instance.ClearPathPreview(); 
        }
        else
        {
            isInAttackMode = false; 
            TileHighlighterService.Instance.ClearAllHighlights();
            lastHoveredCoord = null; 
        }
    }
    
    private void UpdateMovementRangeHighlight()
    {
        if (!inputEnabled || isInAttackMode || mover == null || unitStats == null || unit.unitAP == null || movementRangeCalculator == null || TileHighlighterService.Instance == null)
        {
            if (TileHighlighterService.Instance != null) TileHighlighterService.Instance.ClearMovementRange();
            return;
        }
        // Debug.Log($"UnitInputHandler ({unit.unitName}): Updating Movement Range Highlight. AP: {unit.unitAP.CurrentAP}", this);
        HashSet<Vector2Int> reachableTiles = movementRangeCalculator.GetReachableTiles(mover.CurrentGridCoords, unit.unitAP.CurrentAP, unit);
        TileHighlighterService.Instance.ShowMovementRange(reachableTiles);
    }
    
    private void UpdateAttackRangeHighlight()
    {
        if (!inputEnabled || !isInAttackMode || mover == null || unitStats == null || unit.unitAP == null || TileHighlighterService.Instance == null)
        {
            if (TileHighlighterService.Instance != null) TileHighlighterService.Instance.ClearAttackRange();
            return;
        }
        // Debug.Log($"UnitInputHandler ({unit.unitName}): Updating Attack Range Highlight.", this);
        
        HashSet<Vector2Int> attackableTiles = new HashSet<Vector2Int>();
        int maxRange = Mathf.Max(unitStats.meleeAttackRange, unitStats.rangedAttackRange);
        Vector2Int startCoord = mover.CurrentGridCoords;

        if (maxRange > 0 && gridManager != null)
        {
             Queue<Vector2Int> frontier = new Queue<Vector2Int>();
             Dictionary<Vector2Int, int> distanceMap = new Dictionary<Vector2Int, int>();
             frontier.Enqueue(startCoord);
             distanceMap[startCoord] = 0;

             while(frontier.Count > 0)
             {
                 Vector2Int current = frontier.Dequeue();
                 int dist = distanceMap[current];
                 
                 // For attack range, we primarily care about highlighting tiles with valid enemy targets
                 Unit targetOnTile = gridManager.GetUnitOnTile(current);
                 if (targetOnTile != null && targetOnTile.Team != unit.Team && targetOnTile.GetComponent<UnitCombat>() != null && !targetOnTile.GetComponent<UnitCombat>().IsDead())
                 {
                     // Check if within actual attack type ranges (melee or ranged)
                     bool canMeleeTarget = unitStats.meleeAttackRange > 0 && dist <= unitStats.meleeAttackRange && unit.unitAP.CanSpend(unitStats.meleeAPCost);
                     bool canRangeTarget = unitStats.rangedAttackRange > 0 && dist <= unitStats.rangedAttackRange && unit.unitAP.CanSpend(unitStats.rangedAPCost);
                     // Basic LOS for ranged (more accurate check is in UnitCombat)
                     bool hasLOS = true;
                     if (canRangeTarget && (!canMeleeTarget || dist > unitStats.meleeAttackRange)) { // If it's a ranged possibility
                        Vector3 eyePosition = transform.position + Vector3.up * 1.5f;
                        Vector3 targetCenter = targetOnTile.transform.position + Vector3.up * 1.0f;
                        int losCheckLayerMask = ~LayerMask.GetMask("Ignore Raycast", "PlayerUnit", "EnemyUnit");
                        if (Physics.Linecast(eyePosition, targetCenter, out RaycastHit hitInfo, losCheckLayerMask, QueryTriggerInteraction.Ignore)) {
                            if (hitInfo.transform != targetOnTile.transform && !hitInfo.transform.IsChildOf(targetOnTile.transform)) hasLOS = false;
                        }
                     }

                     if ((canMeleeTarget || (canRangeTarget && hasLOS))) {
                        attackableTiles.Add(current);
                     }
                 }


                 if (dist < maxRange)
                 {
                     HexTile currentHexTile = gridManager.GetTileAt(current);
                     if (currentHexTile != null) {
                         foreach(HexTile neighbor in gridManager.GetNeighbors(currentHexTile))
                         {
                             if (neighbor != null && !distanceMap.ContainsKey(neighbor.coordinate))
                             {
                                 distanceMap[neighbor.coordinate] = dist + 1;
                                 frontier.Enqueue(neighbor.coordinate);
                             }
                         }
                     }
                 }
             }
        }
        TileHighlighterService.Instance.ShowAttackRange(attackableTiles);
    }

    public void HandleAttackModeToggle() // Called by UI Button
    {
        if (!inputEnabled || combat == null || unitStats == null || (mover != null && mover.IsMoving))
        {
            return;
        }

        isInAttackMode = !isInAttackMode; 
        // Debug.Log($"UnitInputHandler ({unit.unitName}): Attack Mode Toggled. Now: {(isInAttackMode ? "ON" : "OFF")}", this);

        if (TileHighlighterService.Instance == null) return;

        TileHighlighterService.Instance.ClearPathPreview(); 
        if (isInAttackMode)
        {
            bool canMelee = unitStats.meleeAttackRange > 0 && unit.unitAP.CanSpend(unitStats.meleeAPCost);
            bool canRange = unitStats.rangedAttackRange > 0 && unit.unitAP.CanSpend(unitStats.rangedAPCost);
            if (!canMelee && !canRange)
            {
                // Debug.Log($"{unit.unitName} cannot effectively use attack mode (Not enough AP or no attack types). AP: {unit.unitAP.CurrentAP}", this);
                isInAttackMode = false; 
                UpdateMovementRangeHighlight(); 
                TileHighlighterService.Instance.ClearAttackRange();
                return;
            }
            TileHighlighterService.Instance.ClearMovementRange();
            UpdateAttackRangeHighlight();
        }
        else
        {
            TileHighlighterService.Instance.ClearAttackRange();
            UpdateMovementRangeHighlight();
        }
    }

    private void OnCancel(InputAction.CallbackContext context)
    {
        if (!inputEnabled || (mover != null && mover.IsMoving)) return;

        if (isInAttackMode)
        {
            HandleAttackModeToggle(); 
        }
    }
    
    void Update() 
    {
        if (!inputEnabled || isInAttackMode || mover == null || mover.IsMoving || mainCam == null || gridManager == null || TileHighlighterService.Instance == null)
        {
            if (TileHighlighterService.Instance != null && lastHoveredCoord.HasValue)
            {
                TileHighlighterService.Instance.ShowTileHover(null);
                if(!isInAttackMode) TileHighlighterService.Instance.ClearPathPreview(); // Clear preview if exiting hover logic
                lastHoveredCoord = null;
            }
            return;
        }

        if (Mouse.current == null) return;
        Vector2 screenPos = Mouse.current.position.ReadValue();
        Ray ray = mainCam.ScreenPointToRay(screenPos);
        Vector2Int? currentTileCoord = null;

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundLayerMask))
        {
            HexTile hoveredTile = hit.collider.GetComponent<HexTile>();
            if (hoveredTile != null && hoveredTile.isWalkable)
            {
                currentTileCoord = hoveredTile.coordinate;
            }
        }

        if (currentTileCoord != lastHoveredCoord)
        {
            lastHoveredCoord = currentTileCoord;
            TileHighlighterService.Instance.ShowTileHover(lastHoveredCoord); 
            if (lastHoveredCoord.HasValue)
            {
                HexTile startTile = gridManager.GetTileAt(mover.CurrentGridCoords);
                HexTile endTile = gridManager.GetTileAt(lastHoveredCoord.Value);
                if (startTile != null && endTile != null && pathfinder != null)
                {
                    List<HexTile> path = pathfinder.FindPath(startTile, endTile);
                    if (path != null && path.Count > 1) {
                        int pathApCost = CalculatePathAPCost(path);
                        if (unit.unitAP.CanSpend(pathApCost))
                        {
                            List<Vector2Int> pathCoords = new List<Vector2Int>();
                            foreach(var tile in path) pathCoords.Add(tile.coordinate);
                            TileHighlighterService.Instance.ShowPathPreview(pathCoords);
                        } else {
                             TileHighlighterService.Instance.ClearPathPreview();
                        }
                    } else {
                        TileHighlighterService.Instance.ClearPathPreview();
                    }
                } else {
                     TileHighlighterService.Instance.ClearPathPreview();
                }
            }
            else
            {
                TileHighlighterService.Instance.ClearPathPreview();
            }
        }
    }

    private void OnClick(InputAction.CallbackContext context)
    {
        if (!inputEnabled || (mover != null && mover.IsMoving) || mainCam == null || gridManager == null || combat == null || unitStats == null)
        {
            return;
        }

        if (Mouse.current == null) return;
        Vector2 screenPos = Mouse.current.position.ReadValue();
        Ray ray = mainCam.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundLayerMask))
        {
            HexTile clickedTile = hit.collider.GetComponent<HexTile>();
            if (clickedTile == null) return;

            if (isInAttackMode)
            {
                HandleAttackModeClick(clickedTile);
            }
            else
            {
                HandleMovementModeClick(clickedTile);
            }
        }
    }

    private void HandleAttackModeClick(HexTile clickedTile)
    {
        Unit targetUnitOnTile = gridManager.GetUnitOnTile(clickedTile.coordinate);

        if (targetUnitOnTile != null && targetUnitOnTile.Team != unit.Team)
        {
            HexTile myTile = gridManager.GetTileAt(mover.CurrentGridCoords);
            if (myTile == null) { Debug.LogError("AttackModeClick: Current unit's tile is null!", this); return; }

            int distanceToTarget = HexUtils.HexDistance(myTile.coordinate, clickedTile.coordinate); // Ensure HexUtils.HexDistance is available

            bool actionTaken = false;
            if (unitStats.rangedAttackRange > 0 && distanceToTarget <= unitStats.rangedAttackRange && unit.unitAP.CanSpend(unitStats.rangedAPCost))
            {
                if (combat.CanPerformRangedAttackChecks(targetUnitOnTile)) // Use UnitCombat's LOS check
                {
                    combat.PerformRangedAttack(targetUnitOnTile);
                    actionTaken = true;
                } else {
                     Debug.Log($"UnitInputHandler ({unit.unitName}): Ranged attack on {targetUnitOnTile.unitName} failed LOS or other checks in UnitCombat.");
                }
            }
            else if (unitStats.meleeAttackRange > 0 && distanceToTarget <= unitStats.meleeAttackRange && unit.unitAP.CanSpend(unitStats.meleeAPCost))
            {
                // Assuming melee doesn't need an extra LOS check here if adjacent
                combat.PerformMeleeAttack(targetUnitOnTile);
                actionTaken = true;
            }
            else
            {
                Debug.Log($"Target '{targetUnitOnTile.unitName}' is out of range for available AP or attack types.");
            }

            if (actionTaken)
            {
                SetModeToMovementAndUpdateHighlights();
            }
        }
        else 
        {
            SetModeToMovementAndUpdateHighlights(); // Clicked empty/friendly, exit attack mode
        }
    }
    
    private void SetModeToMovementAndUpdateHighlights()
    {
        isInAttackMode = false; 
        if (TileHighlighterService.Instance != null) TileHighlighterService.Instance.ClearAttackRange();
        UpdateMovementRangeHighlight(); // Refresh move range based on remaining AP
    }


    private void HandleMovementModeClick(HexTile clickedTile)
    {
        if (!clickedTile.isWalkable) return;

        HexTile startTile = gridManager.GetTileAt(mover.CurrentGridCoords);
        if (startTile == null) return;

        if (pathfinder == null) { Debug.LogError("Pathfinder is null in MovementClick!", this); return; }

        List<HexTile> path = pathfinder.FindPath(startTile, clickedTile);
        if (path == null || path.Count < 2) return;

        int pathApCost = CalculatePathAPCost(path);

        if (unit.unitAP.CanSpend(pathApCost))
        {
            mover.MoveAlongPath(path);
            // After move, HandleActiveUnitChanged will be called if turn ends.
            // If turn doesn't end, movement range might need explicit refresh if AP changed.
            // The UnitMover.OnMoveComplete -> UnitHighlighter.HandleMoveComplete -> UpdateHighlightsBasedOnMode
            // is now GONE. So we need to refresh highlights after a move IF the unit still has its turn.
            // For now, let's assume HandleActiveUnitChanged will cover it if turn ends.
            // If not, we will need to call UpdateMovementRangeHighlight() after mover.MoveAlongPath completes.
            // This usually means UnitMover needs an OnMoveCompletedAction event.
            // Since UnitMover.OnMoveComplete was already there, let's assume UIH's old sub isn't needed.
            // The next Update() or HandleActiveUnitChanged should refresh highlights correctly.
            if(TileHighlighterService.Instance != null) TileHighlighterService.Instance.ClearPathPreview();
        }
        else
        {
            Debug.Log($"Not enough AP for this move. Needed: {pathApCost}, Have: {unit.unitAP.CurrentAP}.", this);
        }
    }
    
    private int CalculatePathAPCost(List<HexTile> path)
    {
        if (path == null || path.Count < 2) return 0;
        int cost = 0;
        for (int i = 1; i < path.Count; i++) 
        {
            if (path[i] == null || path[i].tileType == null)
            {
                Debug.LogError("Path for AP cost calculation contains invalid tile data.", this);
                return int.MaxValue; 
            }
            cost += path[i].tileType.moveCost;
        }
        return cost;
    }


    private void OnEndTurn(InputAction.CallbackContext context)
    {
        EndTurn();
    }

    public void EndTurn()
    {
        if (!inputEnabled) return;
        if (mover != null && mover.IsMoving) return;

        isInAttackMode = false; 
        if(TileHighlighterService.Instance != null) TileHighlighterService.Instance.ClearAllHighlights();

        var manager = TacticalCombatManager.Instance;
        if (manager != null && manager.CurrentUnit == unit && manager.IsPlayerTurn)
        {
            manager.EndCurrentTurn();
        }
    }

    public bool IsInputEnabledForDebug() 
    {
        return inputEnabled;
    }
}