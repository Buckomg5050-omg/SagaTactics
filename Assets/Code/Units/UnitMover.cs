using UnityEngine;
using System.Collections.Generic;

public class UnitMover : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float yOffset = 0.5f;
    [SerializeField] private int movementRange = 6;

    public bool IsMoving => isMoving;
    public Vector2Int CurrentGridCoords => currentGridCoords;
    public int MovementRange => movementRange;

    public System.Action OnMoveComplete;

    private Vector2Int currentGridCoords = Vector2Int.zero;
    private Vector2Int targetGridCoords = Vector2Int.zero;
    private Vector3 targetWorldPosition;
    private bool isMoving = false;

    private Animator animator;
    private HexGrid gridManager;
    private UnitFacing facing;
    private UnitAP unitAP;
    private int pendingAPCost = 0;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        gridManager = FindFirstObjectByType<HexGrid>();
        facing = GetComponent<UnitFacing>();
        unitAP = GetComponent<UnitAP>();
        SnapToGrid(currentGridCoords);
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
            gridManager = FindFirstObjectByType<HexGrid>();

        if (!gridManager.IsValidCoordinate(targetCoords)) return;
        if (targetCoords == currentGridCoords) return;

        targetGridCoords = targetCoords;
        targetWorldPosition = gridManager.GetPositionForHexFromCoordinate(targetGridCoords) + Vector3.up * yOffset;
        isMoving = true;

        if (animator) animator.SetBool("isMoving", true);
    }

    private void MoveToTarget()
    {
        if (facing != null)
        {
            facing.FaceDirection(targetWorldPosition);
        }

        transform.position = Vector3.MoveTowards(transform.position, targetWorldPosition, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetWorldPosition) < 0.01f)
        {
            transform.position = targetWorldPosition;
            currentGridCoords = targetGridCoords;
            isMoving = false;

            if (animator) animator.SetBool("isMoving", false);

            OnMoveComplete?.Invoke();
        }
    }

    public void MoveAlongPath(List<HexTile> path)
    {
        if (path == null || path.Count < 2 || unitAP == null)
            return;

        int totalCost = 0;
        for (int i = 1; i < path.Count; i++)
            totalCost += path[i].tileType.moveCost;

        if (!unitAP.CanSpend(totalCost))
        {
            Debug.Log($"Not enough AP to move. Needed {totalCost}, but have {unitAP.CurrentAP}");
            return;
        }

        pendingAPCost = totalCost;
        StartCoroutine(MovePathCoroutine(path));
    }

    private System.Collections.IEnumerator MovePathCoroutine(List<HexTile> path)
    {
        foreach (var tile in path)
        {
            MoveTo(tile.coordinate);
            yield return new WaitUntil(() => !IsMoving);
        }

        unitAP.Spend(pendingAPCost);
        pendingAPCost = 0;
        OnMoveComplete?.Invoke();
    }

    public void SnapToGrid(Vector2Int gridCoords)
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<HexGrid>();

        currentGridCoords = gridCoords;
        targetWorldPosition = gridManager.GetPositionForHexFromCoordinate(currentGridCoords) + Vector3.up * yOffset;
        transform.position = targetWorldPosition;
        isMoving = false;
    }
}
