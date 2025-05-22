using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlayerUnitMover : MonoBehaviour
{
    [Header("References")]
    public HexGrid gridManager;
    public GameObject hoverHighlightPrefab;
    public GameObject rangeHighlightPrefab;
    public GameObject selectedTileMarkerPrefab;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float yOffset = 0.5f;

    [Header("Highlight Settings")]
    public float highlightYOffset = 0.3f;

    [Header("Movement Range")]
    public int moveRange = 3;

    [Header("Unit State")]
    public bool isSelected = true;

    [Header("Debug Options")]
    public bool logHoverInfo = true;
    public bool logClickEvents = true;

    private Vector2Int currentGridCoords;
    private Vector3 targetWorldPosition;
    private bool isMoving = false;

    private PlayerInputActions inputActions;
    private GameObject hoverHighlightInstance;
    private GameObject selectedTileMarkerInstance;
    private Vector2Int? currentHoverCoords = null;
    private List<GameObject> activeRangeHighlights = new List<GameObject>();

    void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    void OnEnable()
    {
        inputActions.Enable();
        inputActions.Player.Click.performed += OnClick;
    }

    void OnDisable()
    {
        inputActions.Player.Click.performed -= OnClick;
        inputActions.Disable();
    }

    void Start()
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<HexGrid>();
            if (gridManager == null)
            {
                Debug.LogError("HexGrid manager not found!");
                enabled = false;
                return;
            }
        }

        currentGridCoords = Vector2Int.zero;
        SnapToGridPosition(currentGridCoords);
        Debug.Log($"[Init] Player Start - Grid: {currentGridCoords}, World: {transform.position}");

        if (hoverHighlightPrefab != null)
        {
            hoverHighlightInstance = Instantiate(hoverHighlightPrefab);
            hoverHighlightInstance.SetActive(false);
        }

        if (selectedTileMarkerPrefab != null)
        {
            selectedTileMarkerInstance = Instantiate(selectedTileMarkerPrefab);
            selectedTileMarkerInstance.SetActive(false);
        }

        ShowMoveRange();
        UpdateSelectionMarker();
    }

    void Update()
    {
        UpdateHoverHighlight();
        MovePlayer();
        UpdateSelectionMarker();
    }

    void OnClick(InputAction.CallbackContext context)
    {
        if (!isSelected || isMoving) return;

        if (logClickEvents)
            Debug.Log("[Input] Click event triggered");

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        if (logClickEvents)
            Debug.Log($"[Input] Mouse screen pos: {mouseScreenPos}");

        Ray ray = Camera.main.ScreenPointToRay(mouseScreenPos);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            HexTile targetTile = hit.collider.GetComponent<HexTile>();
            if (targetTile == null || !targetTile.isWalkable)
            {
                Debug.Log("[Move] Tile is not walkable!");
                return;
            }

            Vector2Int clickedCoords = targetTile.coordinate;
            if (logClickEvents)
                Debug.Log($"[Click] World Pos: {hit.point}, Grid Coords: {clickedCoords}");

            if (clickedCoords != currentGridCoords &&
                clickedCoords.x >= 0 && clickedCoords.x < gridManager.gridSize.x &&
                clickedCoords.y >= 0 && clickedCoords.y < gridManager.gridSize.y)
            {
                currentGridCoords = clickedCoords;
                targetWorldPosition = gridManager.GetPositionForHexFromCoordinate(currentGridCoords) + Vector3.up * yOffset;
                isMoving = true;

                if (logClickEvents)
                    Debug.Log($"[Move] Moving to {currentGridCoords} → {targetWorldPosition}");
            }
            else
            {
                Debug.Log("[Click] Already on this tile or invalid coords.");
            }
        }
        else
        {
            Debug.LogWarning("[Raycast] No object hit by raycast");
        }
    }

    void MovePlayer()
    {
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetWorldPosition, moveSpeed * Time.deltaTime);
            if ((transform.position - targetWorldPosition).sqrMagnitude < 0.0001f)
            {
                transform.position = targetWorldPosition;
                isMoving = false;
                Debug.Log($"[Move] Arrived at {currentGridCoords}, World: {transform.position}");
                ShowMoveRange();
            }
        }
    }

    void SnapToGridPosition(Vector2Int gridCoords)
    {
        currentGridCoords = gridCoords;

        if (gridManager != null &&
            gridCoords.x >= 0 && gridCoords.x < gridManager.gridSize.x &&
            gridCoords.y >= 0 && gridCoords.y < gridManager.gridSize.y)
        {
            targetWorldPosition = gridManager.GetPositionForHexFromCoordinate(gridCoords) + Vector3.up * yOffset;
            transform.position = targetWorldPosition;
        }
        else
        {
            Debug.LogWarning($"[Snap] Invalid grid coord: {gridCoords}, snapping to (0,0)");
            currentGridCoords = Vector2Int.zero;
            targetWorldPosition = gridManager.GetPositionForHexFromCoordinate(Vector2Int.zero) + Vector3.up * yOffset;
            transform.position = targetWorldPosition;
        }

        isMoving = false;
    }

    void UpdateHoverHighlight()
    {
        if (hoverHighlightInstance == null || isMoving || !isSelected) return;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mouseScreenPos);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            HexTile tile = hit.collider.GetComponent<HexTile>();
            if (tile == null || !tile.isWalkable)
            {
                hoverHighlightInstance.SetActive(false);
                currentHoverCoords = null;
                return;
            }

            Vector2Int hoverCoords = tile.coordinate;

            if (hoverCoords != currentHoverCoords)
            {
                currentHoverCoords = hoverCoords;

                Vector3 worldPos = gridManager.GetPositionForHexFromCoordinate(hoverCoords) + Vector3.up * highlightYOffset;
                hoverHighlightInstance.transform.position = worldPos;
                hoverHighlightInstance.SetActive(true);

                if (logHoverInfo)
                {
                    Debug.Log($"[Hover] Coords: {tile.coordinate}, Walkable: {tile.isWalkable}");
                }
            }
        }
        else
        {
            hoverHighlightInstance.SetActive(false);
            currentHoverCoords = null;
        }
    }

    void UpdateSelectionMarker()
    {
        if (selectedTileMarkerInstance != null)
        {
            if (isSelected)
            {
                selectedTileMarkerInstance.transform.position = gridManager.GetPositionForHexFromCoordinate(currentGridCoords) + Vector3.up * highlightYOffset;
                selectedTileMarkerInstance.SetActive(true);
            }
            else
            {
                selectedTileMarkerInstance.SetActive(false);
            }
        }
    }

    void ShowMoveRange()
    {
        ClearMoveRange();

        for (int dx = -moveRange; dx <= moveRange; dx++)
        {
            for (int dy = -moveRange; dy <= moveRange; dy++)
            {
                Vector2Int offset = new Vector2Int(dx, dy);
                Vector2Int checkCoords = currentGridCoords + offset;

                if (Mathf.Abs(dx) + Mathf.Abs(dy) > moveRange)
                    continue;

                if (checkCoords.x < 0 || checkCoords.x >= gridManager.gridSize.x ||
                    checkCoords.y < 0 || checkCoords.y >= gridManager.gridSize.y)
                    continue;

                Vector3 worldPos = gridManager.GetPositionForHexFromCoordinate(checkCoords) + Vector3.up * highlightYOffset;

                Ray ray = new Ray(worldPos + Vector3.up * 5f, Vector3.down);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    HexTile tile = hit.collider.GetComponent<HexTile>();
                    if (tile != null && tile.isWalkable)
                    {
                        GameObject marker = Instantiate(rangeHighlightPrefab, worldPos, Quaternion.identity);
                        activeRangeHighlights.Add(marker);
                    }
                }
            }
        }
    }

    void ClearMoveRange()
    {
        foreach (var marker in activeRangeHighlights)
        {
            if (marker != null)
                Destroy(marker);
        }
        activeRangeHighlights.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        if (gridManager != null)
        {
            if (currentGridCoords.x >= 0 && currentGridCoords.x < gridManager.gridSize.x &&
                currentGridCoords.y >= 0 && currentGridCoords.y < gridManager.gridSize.y)
            {
                Gizmos.color = Color.cyan;
                Vector3 worldPos = gridManager.GetPositionForHexFromCoordinate(currentGridCoords) + Vector3.up * yOffset;
                Gizmos.DrawWireSphere(worldPos, gridManager.outerSize * 0.3f);
            }
        }
    }
}
