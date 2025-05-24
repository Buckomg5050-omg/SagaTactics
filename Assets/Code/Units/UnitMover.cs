using UnityEngine;
using System.Collections.Generic;
using System.Linq; 

public class UnitMover : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [Tooltip("Vertical offset from the hex tile's pivot to the unit's pivot.")]
    [SerializeField] private float yOffset = 0.5f; 
    [Tooltip("Max potential move distance if AP was infinite and tiles cost 1. Used by some highlighter logic as a fallback.")]
    [SerializeField] private int movementRange = 6; 

    public bool IsMoving => isMoving;
    public Vector2Int CurrentGridCoords => currentGridCoords;
    public int MovementRange => movementRange; 

    public event System.Action OnMoveStart; 
    public event System.Action OnMoveSegmentComplete; 
    public event System.Action OnMoveComplete; 

    private Vector2Int currentGridCoords = Vector2Int.zero; 
    private Vector2Int targetGridCoords = Vector2Int.zero; 
    private Vector3 targetWorldPosition;
    private bool isMoving = false;

    private Animator animator;
    private HexGrid gridManager;
    private UnitFacing facing; 
    private UnitAP unitAP; 
    private int pendingAPCostForPath = 0; 
    private Unit unit;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>(); 
        gridManager = FindFirstObjectByType<HexGrid>(); 
        facing = GetComponent<UnitFacing>();
        unitAP = GetComponent<UnitAP>(); 
        unit = GetComponent<Unit>();

        if (gridManager == null) Debug.LogError($"UnitMover on {gameObject.name} could not find HexGrid!", this);
        if (facing == null) Debug.LogWarning($"UnitMover on {gameObject.name} did not find a UnitFacing component. Facing logic will be skipped.", this);
        if (unitAP == null) Debug.LogError($"UnitMover on {gameObject.name} did not find a UnitAP component! Movement will not cost AP.", this);
        if (unit == null) Debug.LogError($"UnitMover on {gameObject.name} did not find a Unit component!", this);
    }

    void Update()
    {
        if (isMoving)
        {
            MoveToTarget();
        }
    }

    private void InitiateMoveToSegment(Vector2Int targetSegmentCoords)
    {
        if (gridManager == null) { 
            Debug.LogError("HexGrid manager not found in InitiateMoveToSegment!", this);
            return; 
        }
        if (!gridManager.IsValidCoordinate(targetSegmentCoords)) { 
            Debug.LogWarning($"InitiateMoveToSegment called with invalid coordinate: {targetSegmentCoords}", this);
            return; 
        }
        if (targetSegmentCoords == currentGridCoords && !isMoving) { 
            Debug.LogWarning($"InitiateMoveToSegment: Already at target {targetSegmentCoords} and not moving.", this);
            OnMoveSegmentComplete?.Invoke(); 
            return; 
        }

        targetGridCoords = targetSegmentCoords;
        targetWorldPosition = gridManager.GetPositionForHexFromCoordinate(targetGridCoords) + (Vector3.up * yOffset);
        isMoving = true;

        if (animator) animator.SetBool("isMoving", true);
        if (facing != null) 
        {
            facing.SetTargetLookAtWorldPosition(targetWorldPosition);
        }
    }

    private void MoveToTarget()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetWorldPosition, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetWorldPosition) < 0.01f)
        {
            transform.position = targetWorldPosition; 
            currentGridCoords = targetGridCoords; 
            isMoving = false;

            if (animator) animator.SetBool("isMoving", false);
            
            OnMoveSegmentComplete?.Invoke(); 
        }
    }

    public void MoveAlongPath(List<HexTile> path)
    {
        if (unitAP == null || unit == null) { 
            Debug.LogError($"UnitMover.MoveAlongPath: Missing critical components (unitAP or unit). Aborting. unitAP null: {unitAP == null}, unit null: {unit == null}", this);
            return;
        }

        if (path == null || path.Count < 2) 
        {
            Debug.LogWarning("MoveAlongPath called with null or insufficient path (must include current tile and at least one destination).", this);
            return;
        }

        if (gridManager != null && gridManager.GetTileAt(currentGridCoords) != path[0])
        {
            Debug.LogWarning($"MoveAlongPath: Path does not start at unit's current tile ({currentGridCoords}). Path starts at {path[0]?.coordinate.ToString() ?? "NULL"}. This might lead to incorrect AP calculation or behavior.", this);
        }

        // --- CORRECTED: totalCost calculation moved before its use ---
        int totalCost = 0;
        for (int i = 1; i < path.Count; i++) 
        {
            if (path[i] == null || path[i].tileType == null)
            {
                Debug.LogError($"Path contains null tile or tile with null tileType at index {i}. Aborting move.", this);
                return; 
            }
            totalCost += path[i].tileType.moveCost;
        }
        // --- END CORRECTION ---

        if (!unitAP.CanSpend(totalCost)) // Now totalCost is defined
        {
            Debug.Log($"Not enough AP to move. Needed {totalCost}, but have {unitAP.CurrentAP}. Path length: {path.Count -1} steps.", this);
            return;
        }

        pendingAPCostForPath = totalCost; 
        StopAllCoroutines(); 
        StartCoroutine(MovePathCoroutine(path));
    }

    private System.Collections.IEnumerator MovePathCoroutine(List<HexTile> path)
    {
        OnMoveStart?.Invoke(); 

        for (int i = 1; i < path.Count; i++)
        {
            if (path[i] == null) { 
                Debug.LogError($"Null tile found in path at index {i} during MovePathCoroutine. Aborting move.", this);
                pendingAPCostForPath = 0; 
                yield break; 
            }
            InitiateMoveToSegment(path[i].coordinate);
            
            yield return new WaitUntil(() => !isMoving && currentGridCoords == path[i].coordinate);
        }

        if (unitAP != null) 
        {
            unitAP.Spend(pendingAPCostForPath);
            Debug.Log($"{unit?.unitName ?? gameObject.name} completed move. Spent {pendingAPCostForPath} AP. AP Remaining: {unitAP.CurrentAP}", this);
        }
        pendingAPCostForPath = 0;

        if (facing != null)
        {
            facing.HoldCurrentFacing(); 
            Debug.Log($"UnitMover ({unit?.unitName ?? gameObject.name}): Path complete. Called HoldCurrentFacing.", this);
        }

        OnMoveComplete?.Invoke(); 

        if (unit != null && unit.ShouldAutoEndTurn())
        {
            TacticalCombatManager.Instance?.EndCurrentTurn();
        }
    }

    public void SnapToGrid(Vector2Int gridCoords)
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<HexGrid>(); 
            if (gridManager == null)
            {
                Debug.LogError("HexGrid manager not found in SnapToGrid! Cannot snap.", this);
                return;
            }
        }

        currentGridCoords = gridCoords;
        targetWorldPosition = gridManager.GetPositionForHexFromCoordinate(currentGridCoords) + (Vector3.up * yOffset);
        transform.position = targetWorldPosition;
        isMoving = false; 

        if (facing != null)
        {
            Transform actualModelToRotate = facing.GetModelToRotateForMover();
            if(actualModelToRotate == null) actualModelToRotate = transform; 

            Vector3 currentForward = actualModelToRotate.forward; 
            currentForward.y = 0;
            if (currentForward.sqrMagnitude < 0.001f) currentForward = Vector3.forward; 

            facing.SnapLookAtWorldPosition(actualModelToRotate.position + currentForward.normalized * 0.1f); 
        }
    }
    
    public void FaceTarget(Vector3 targetWorldPositionToFace) 
    {
        if (facing != null)
        {
            facing.SetTargetLookAtWorldPosition(targetWorldPositionToFace); 
            Debug.Log($"{unit?.unitName ?? gameObject.name} UnitMover.FaceTarget: Telling UnitFacing to look at {targetWorldPositionToFace} for attack.", this);
        }
        else
        {
            Debug.LogWarning($"UnitMover on {gameObject.name} called FaceTarget, but no UnitFacing component is assigned.", this);
        }
    }
}