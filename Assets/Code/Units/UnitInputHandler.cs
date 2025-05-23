using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(UnitMover))]
[RequireComponent(typeof(UnitCombat))] 
[RequireComponent(typeof(UnitHighlighter))] 
public class UnitInputHandler : MonoBehaviour
{
    [Header("Raycasting Settings")]
    [SerializeField] private LayerMask groundLayerMask; 

    private Camera mainCam;
    private Unit unit; 
    private UnitMover mover;
    private UnitCombat combat; 
    private HexGrid gridManager;
    private HexPathfinder pathfinder;
    private UnitHighlighter highlighter;

    private PlayerInputActions inputActions;
    private bool inputEnabled = false;

    void Awake()
    {
        unit = GetComponent<Unit>();
        if (unit == null || unit.Team != Unit.UnitTeam.Player)
        {
            Debug.LogWarning($"UnitInputHandler on {gameObject.name} is either not on a PlayerUnit or missing Unit component. Disabling self.", this);
            enabled = false; 
            return;
        }

        mainCam = Camera.main;
        if (mainCam == null) 
        {
            Debug.LogError("UnitInputHandler: Main Camera not found! Disabling input handler.", this);
            enabled = false;
            return;
        }

        mover = GetComponent<UnitMover>();
        combat = GetComponent<UnitCombat>(); 
        highlighter = GetComponent<UnitHighlighter>(); 

        gridManager = FindFirstObjectByType<HexGrid>();
        if (gridManager != null)
        {
            pathfinder = new HexPathfinder(gridManager);
        }
        else
        {
            Debug.LogError("UnitInputHandler: HexGrid not found in scene! Disabling input handler.", this);
            enabled = false; 
            return;
        }
        if (combat == null)
        {
             Debug.LogError($"UnitInputHandler ({gameObject.name}): UnitCombat component not found! Disabling input handler.", this);
            enabled = false;
            return;
        }
        if (highlighter == null)
        {
             Debug.LogError($"UnitInputHandler ({gameObject.name}): UnitHighlighter component not found! Disabling input handler.", this);
            enabled = false;
            return;
        }
        if (mover == null)
        {
            Debug.LogError($"UnitInputHandler ({gameObject.name}): UnitMover component not found! Disabling input handler.", this);
            enabled = false;
            return;
        }
    }

    void OnEnable()
    {
        if (unit == null) unit = GetComponent<Unit>();

        if (unit == null || unit.Team != Unit.UnitTeam.Player) {
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
        inputEnabled = false; 
    }

    public void EnableInput(bool enable)
    {
        inputEnabled = enable;

        if (highlighter != null)
        {
            if (enable)
            {
                highlighter.ToggleAttackMode(false); 
            }
            else
            {
                highlighter.ToggleAttackMode(false); 
                highlighter.ClearMoveRange();
                highlighter.ClearAttackRange();
                highlighter.ClearPreview();
            }
        }
        else {
            Debug.LogWarning($"UnitInputHandler ({gameObject.name}): EnableInput - highlighter is null!", this);
        }
    }

    public void HandleAttackModeToggle()
    {
        if (combat == null || highlighter == null) 
        {
            Debug.LogError($"HandleAttackModeToggle on {gameObject.name} called but critical components (combat or highlighter) are null. Awake may not have run correctly for this instance or this is an unexpected call.", this);
            return;
        }

        if (!inputEnabled || (mover != null && mover.IsMoving))
        {
            Debug.LogWarning($"HandleAttackModeToggle on {gameObject.name} exited. inputEnabled: {inputEnabled}, isMoving: {mover?.IsMoving}", this);
            return;
        }

        if (!highlighter.IsInAttackMode) 
        {
            if (!combat.CanConsiderAttacking()) 
            {
                Debug.Log($"{unit?.unitName ?? gameObject.name} cannot enter attack mode (CanConsiderAttacking failed). AP: {unit?.unitAP?.CurrentAP}", this);
                return;
            }
        }
        
        highlighter.ToggleAttackMode(!highlighter.IsInAttackMode);
        Debug.Log(highlighter.IsInAttackMode ? $"{unit?.unitName ?? gameObject.name} ATTACK MODE: ON" : $"{unit?.unitName ?? gameObject.name} ATTACK MODE: OFF (Movement Mode)", this);
    }
    
    private void OnCancel(InputAction.CallbackContext context)
    {
        if (!inputEnabled || (mover != null && mover.IsMoving) || highlighter == null)
        {
            return;
        }

        if (highlighter.IsInAttackMode)
        {
            highlighter.ToggleAttackMode(false); 
            Debug.Log($"{unit?.unitName ?? gameObject.name} ATTACK MODE: OFF (Cancelled via input action)", this);
        }
    }

    private void OnClick(InputAction.CallbackContext context)
    {
        if (!inputEnabled || (mover != null && mover.IsMoving) || mainCam == null || gridManager == null || combat == null || highlighter == null)
        {
            Debug.LogWarning($"OnClick on {gameObject.name} exited early at top guard. inputEnabled:{inputEnabled}, isMoving:{mover?.IsMoving}, mainCamNull:{mainCam == null}, gridNull:{gridManager==null}, combatNull:{combat==null}, highlighterNull:{highlighter==null}");
            return;
        }

        if (Mouse.current == null) return;
        Vector2 screenPos = Mouse.current.position.ReadValue();
        Ray ray = mainCam.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundLayerMask)) 
        {
            HexTile clickedTile = hit.collider.GetComponent<HexTile>();
            if (clickedTile == null) 
            {
                Debug.Log($"OnClick: Clicked on object '{hit.collider.gameObject.name}' on the 'Ground' layer, but it doesn't have a HexTile component.", this);
                return;
            }
            Debug.Log($"OnClick: Clicked on tile {clickedTile.coordinate} (Object: {hit.collider.name}, Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}). IsInAttackMode: {highlighter.IsInAttackMode}", this);


            if (highlighter.IsInAttackMode)
            {
                Debug.Log("OnClick: In Attack Mode branch.", this);
                Unit targetUnitOnTile = gridManager.GetUnitOnTile(clickedTile.coordinate);
                
                if (targetUnitOnTile != null)
                {
                    Debug.Log($"OnClick AttackMode: Found unit '{targetUnitOnTile.unitName}' of team '{targetUnitOnTile.Team}' on clicked tile.", this);
                    if (targetUnitOnTile.Team != unit.Team)
                    {
                        Debug.Log("OnClick AttackMode: Target unit is an enemy.", this);
                        HexTile myTile = gridManager.GetTileAt(mover.CurrentGridCoords);
                        if (myTile == null)
                        {
                            Debug.LogError("OnClick AttackMode: Current unit's tile (myTile) is null!", this);
                            return;
                        }
                        Debug.Log($"OnClick AttackMode: My tile is {myTile.coordinate}. Clicked tile for target is {clickedTile.coordinate}.", this);

                        bool isAdjacent = false;
                        List<HexTile> neighbors = gridManager.GetNeighbors(myTile);
                        if (neighbors.Contains(clickedTile)) 
                        {
                            isAdjacent = true;
                        }
                        Debug.Log($"OnClick AttackMode: Is target adjacent? {isAdjacent}.", this);

                        if (isAdjacent) 
                        {
                            if (unit.unitAP != null && unit.unitAP.CanSpend(UnitCombat.MELEE_ATTACK_AP_COST))
                            {
                                Debug.Log("OnClick AttackMode: AP sufficient. Performing melee attack.", this);
                                combat.PerformMeleeAttack(targetUnitOnTile);
                                highlighter.ToggleAttackMode(false); 
                            }
                            else
                            {
                                Debug.Log($"{unit?.unitName ?? gameObject.name} not enough AP to perform melee attack. Needed: {UnitCombat.MELEE_ATTACK_AP_COST}, Has: {unit?.unitAP?.CurrentAP}", this);
                            }
                        }
                        else
                        {
                            Debug.Log("Target is not in melee range (not adjacent).", this);
                        }
                    }
                    else
                    {
                        Debug.Log("OnClick AttackMode: Clicked unit is friendly. Cancelling attack mode.", this);
                        highlighter.ToggleAttackMode(false);
                    }
                }
                else
                {
                    Debug.Log("OnClick AttackMode: No unit on clicked tile. Cancelling attack mode.", this);
                    highlighter.ToggleAttackMode(false); 
                }
            }
            else // Movement Mode
            {
                Debug.Log("OnClick: In Movement Mode branch.", this);
                if (!clickedTile.isWalkable)
                {
                     Debug.Log("Clicked on a non-walkable tile for movement.", this);
                     return; 
                }

                HexTile startTile = gridManager.GetTileAt(mover.CurrentGridCoords);
                if (startTile == null)
                {
                    Debug.LogWarning("Could not find start tile at unit's current coordinates for movement.", this);
                    return;
                }

                if (pathfinder == null) { 
                    Debug.LogError("Pathfinder is null in OnClick movement logic!", this);
                    return;
                }

                List<HexTile> path = pathfinder.FindPath(startTile, clickedTile);
                if (path == null || path.Count < 2) 
                {
                    Debug.Log("No valid path found for movement or clicked on self.", this);
                    return;
                }

                int pathApCost = 0;
                for (int i = 1; i < path.Count; i++)
                {
                    if(path[i] == null || path[i].tileType == null) { 
                        Debug.LogError("Path contains invalid tile data for cost calculation.", this);
                        return;
                    }
                    pathApCost += path[i].tileType.moveCost;
                }

                if (unit.unitAP != null && unit.unitAP.CanSpend(pathApCost)) 
                {
                    Debug.Log($"Path found to {clickedTile.coordinate} with {path.Count-1} steps. Cost: {pathApCost} AP.", this);
                    mover.MoveAlongPath(path);
                }
                else
                {
                    Debug.Log($"Not enough AP for this move. Needed: {pathApCost}, Have: {unit?.unitAP?.CurrentAP}.", this);
                }
            }
        }
        else // Raycast didn't hit anything ON THE SPECIFIED LAYER
        {
            // MODIFIED DEBUG LOG
            Debug.Log($"OnClick: Raycast did not hit any colliders on the 'groundLayerMask'. Ensure tiles are on this layer AND the layer is selected in the UnitInputHandler's 'Ground Layer Mask' Inspector field.", this);
        }
    }

    private void OnEndTurn(InputAction.CallbackContext context)
    {
        EndTurn();
    }

    public void EndTurn() 
    {
        if (!inputEnabled) 
        {
            return; 
        }

        if (mover != null && mover.IsMoving)
        {
            Debug.Log("Cannot end turn while moving.", this);
            return;
        }
        
        if (highlighter != null && highlighter.IsInAttackMode)
        {
            highlighter.ToggleAttackMode(false);
        }

        var manager = TacticalCombatManager.Instance; 
        if (manager != null && manager.CurrentUnit == unit && manager.IsPlayerTurn) 
        {
            Debug.Log($"Ending player turn for {unit?.unitName ?? gameObject.name} via input handler.", this);
            manager.EndCurrentTurn();
        }
        else
        {
            Debug.LogWarning($"Attempted to end turn for {unit?.unitName ?? gameObject.name}, but it's not their active turn, manager is null, or combat not active.", this);
        }
    }

    public bool IsInputEnabledForDebug()
    {
        return inputEnabled;
    }
}