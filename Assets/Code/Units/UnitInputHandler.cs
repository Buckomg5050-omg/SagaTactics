using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(UnitMover))]
[RequireComponent(typeof(UnitHighlighter))]
public class UnitInputHandler : MonoBehaviour
{
    private Camera mainCam;
    private UnitMover mover;
    private HexGrid gridManager;
    private HexPathfinder pathfinder;
    private UnitHighlighter highlighter;

    private PlayerInputActions inputActions;

    void Awake()
    {
        mainCam = Camera.main;
        mover = GetComponent<UnitMover>();
        gridManager = FindFirstObjectByType<HexGrid>();
        pathfinder = new HexPathfinder(gridManager);
        highlighter = GetComponent<UnitHighlighter>();
    }

    void OnEnable()
    {
        if (inputActions == null)
            inputActions = new PlayerInputActions();

        inputActions.Enable();
        inputActions.Player.Click.performed += OnClick;
    }

    void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Click.performed -= OnClick;
            inputActions.Disable();
        }
    }

    private void OnClick(InputAction.CallbackContext context)
    {
        if (mover.IsMoving || mainCam == null || gridManager == null)
            return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Ray ray = mainCam.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            HexTile targetTile = hit.collider.GetComponent<HexTile>();
            if (targetTile == null || !targetTile.isWalkable)
                return;

            HexTile startTile = gridManager.GetTileAt(mover.CurrentGridCoords);
            if (startTile == null)
                return;

            List<HexTile> path = pathfinder.FindPath(startTile, targetTile);
            if (path == null || path.Count == 0)
            {
                Debug.Log("No path found.");
                return;
            }

            // ✅ Skip the first tile (unit's current tile) for cost calculation
            float totalCost = 0f;
            for (int i = 1; i < path.Count; i++)
                totalCost += path[i].tileType.moveCost;

            if (totalCost > mover.MovementRange)
            {
                Debug.Log($"Path exceeds movement range. Cost: {totalCost}, Limit: {mover.MovementRange}");
                return;
            }

            Debug.Log($"Path found with {path.Count} steps. Cost: {totalCost}");
            mover.MoveAlongPath(path);
            highlighter.ClearPreview();
        }
        else
        {
            Debug.Log("Raycast did not hit anything.");
        }
    }
}
