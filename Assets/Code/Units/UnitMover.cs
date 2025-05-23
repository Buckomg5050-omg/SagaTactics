using UnityEngine;
using System.Collections.Generic;

public class UnitMover : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float yOffset = 0.5f;
    [SerializeField] private int movementRange = 6; // This is the unit's max move *potential* in AP/tiles, pathfinding checks actual reach

    public bool IsMoving => isMoving;
    public Vector2Int CurrentGridCoords => currentGridCoords;
    public int MovementRange => movementRange; // Used by highlighter perhaps, or as a base for pathfinding range limits

    public System.Action OnMoveComplete;

    private Vector2Int currentGridCoords = Vector2Int.zero;
    private Vector2Int targetGridCoords = Vector2Int.zero;
    private Vector3 targetWorldPosition;
    private bool isMoving = false;

    private Animator animator;
    private HexGrid gridManager;
    private UnitFacing facing; // Assuming this is your component for handling visual rotation
    private UnitAP unitAP;
    private int pendingAPCost = 0;
    private Unit unit;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        gridManager = FindFirstObjectByType<HexGrid>(); // Consider caching this more robustly if multiple grids or scenes
        facing = GetComponent<UnitFacing>();
        if (facing == null)
        {
            Debug.LogWarning($"UnitMover on {gameObject.name} did not find a UnitFacing component. Facing logic will be skipped.", this);
        }
        unitAP = GetComponent<UnitAP>();
        unit = GetComponent<Unit>();

        SnapToGrid(currentGridCoords); // Initial snap
    }

    void Update()
    {
        if (isMoving)
        {
            MoveToTarget();
        }
    }

    public void MoveTo(Vector2Int targetCoords)
    {
        if (gridManager == null)
        {
            // This might happen if the grid is spawned later or this unit is instantiated before grid
            Debug.LogError("HexGrid manager not found in MoveTo!", this);
            return;
        }

        if (!gridManager.IsValidCoordinate(targetCoords))
        {
            Debug.LogWarning($"MoveTo called with invalid coordinate: {targetCoords}", this);
            return;
        }
        if (targetCoords == currentGridCoords && !isMoving) // Don't restart move if already at target and not moving
        {
            return;
        }

        targetGridCoords = targetCoords;
        // Ensure yOffset is applied correctly if GetPositionForHexFromCoordinate returns ground level
        targetWorldPosition = gridManager.GetPositionForHexFromCoordinate(targetGridCoords) + (Vector3.up * yOffset);
        isMoving = true;

        if (animator) animator.SetBool("isMoving", true);
        // Facing during movement is handled in MoveToTarget
    }

    private void MoveToTarget()
    {
        if (facing != null)
        {
            // Face the direction of movement
            facing.FaceDirection(targetWorldPosition);
        }

        transform.position = Vector3.MoveTowards(transform.position, targetWorldPosition, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetWorldPosition) < 0.01f)
        {
            transform.position = targetWorldPosition; // Snap to exact position
            currentGridCoords = targetGridCoords;
            isMoving = false;

            if (animator) animator.SetBool("isMoving", false);

            OnMoveComplete?.Invoke();

            // Check for auto end turn after movement is fully complete
            if (unit != null && unit.ShouldAutoEndTurn())
            {
                TacticalCombatManager.Instance?.EndCurrentTurn();
            }
        }
    }

    public void MoveAlongPath(List<HexTile> path)
    {
        if (path == null || path.Count < 2) // Path must have at least current tile and one target tile
        {
            Debug.LogWarning("MoveAlongPath called with null or insufficient path.", this);
            return;
        }
        if (unitAP == null)
        {
            Debug.LogError("UnitAP component not found on unit trying to MoveAlongPath.", this);
            return;
        }


        int totalCost = 0;
        // Cost calculation starts from the first step (path[1]), as path[0] is the current tile
        for (int i = 1; i < path.Count; i++) 
        {
            if (path[i] == null || path[i].tileType == null)
            {
                Debug.LogError($"Path contains null tile or tile with null tileType at index {i}", this);
                return; // Invalid path data
            }
            totalCost += path[i].tileType.moveCost;
        }

        if (!unitAP.CanSpend(totalCost))
        {
            Debug.Log($"Not enough AP to move. Needed {totalCost}, but have {unitAP.CurrentAP}. Path length: {path.Count -1} steps.");
            return;
        }

        pendingAPCost = totalCost; // Store the cost to be deducted upon completion
        StopAllCoroutines(); // Stop any previous movement coroutine
        StartCoroutine(MovePathCoroutine(path));
    }

    private System.Collections.IEnumerator MovePathCoroutine(List<HexTile> path)
    {
        // First element (path[0]) is the starting tile, so iterate from index 1
        for (int i = 1; i < path.Count; i++)
        {
            if (path[i] == null)
            {
                Debug.LogError($"Null tile found in path at index {i} during MovePathCoroutine. Aborting move.", this);
                pendingAPCost = 0; // Reset pending cost as move is aborted
                yield break; // Exit coroutine
            }
            MoveTo(path[i].coordinate);
            // Wait until the unit is no longer moving towards the current segment's target
            yield return new WaitUntil(() => !isMoving && currentGridCoords == path[i].coordinate);
        }

        if (unitAP != null) // Double check unitAP before spending
        {
            unitAP.Spend(pendingAPCost);
        }
        pendingAPCost = 0;

        OnMoveComplete?.Invoke(); // Invoke after the entire path is traversed

        // Check for auto end turn after the entire path movement is fully complete and AP spent
        if (unit != null && unit.ShouldAutoEndTurn())
        {
            TacticalCombatManager.Instance?.EndCurrentTurn();
        }
    }

    public void SnapToGrid(Vector2Int gridCoords)
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<HexGrid>(); // Attempt to re-acquire if null
            if (gridManager == null)
            {
                Debug.LogError("HexGrid manager not found in SnapToGrid!", this);
                return;
            }
        }

        currentGridCoords = gridCoords;
        // Ensure yOffset is applied correctly if GetPositionForHexFromCoordinate returns ground level
        targetWorldPosition = gridManager.GetPositionForHexFromCoordinate(currentGridCoords) + (Vector3.up * yOffset);
        transform.position = targetWorldPosition;
        isMoving = false; // Ensure isMoving is reset
    }

    // NEW METHOD to allow external calls for facing a target (e.g., before an attack)
    /// <summary>
    /// Makes the unit face a specific world position.
    /// </summary>
    /// <param name="targetWorldPositionToFace">The world position to face towards.</param>
    public void FaceTarget(Vector3 targetWorldPositionToFace)
    {
        if (facing != null)
        {
            // We only want to change the Y rotation, keep the unit upright.
            // The targetWorldPositionToFace should ideally be at the same Y-level as the unit's pivot
            // or UnitFacing should handle this.
            // For simplicity, if UnitFacing.FaceDirection expects a world point:
            facing.FaceDirection(targetWorldPositionToFace);
        }
        else
        {
            // Fallback if no UnitFacing component: basic LookAt (might have undesirable X/Z rotation)
            // Vector3 direction = (targetWorldPositionToFace - transform.position).normalized;
            // if (direction != Vector3.zero)
            // {
            //     transform.rotation = Quaternion.LookRotation(direction);
            // }
            Debug.LogWarning($"UnitMover on {gameObject.name} called FaceTarget, but no UnitFacing component is assigned. Facing logic skipped.", this);
        }
    }
}