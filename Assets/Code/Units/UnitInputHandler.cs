using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(UnitMover))]
public class UnitInputHandler : MonoBehaviour
{
    private Camera mainCam;
    private UnitMover mover;
    private HexGrid gridManager;
    private HexPathfinder pathfinder;
    private UnitHighlighter highlighter;

    private PlayerInputActions inputActions;

    private bool inputEnabled = false;

    void Awake()
    {
        if (!CompareTag("PlayerUnit"))
        {
            enabled = false;
            return;
        }

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
        inputActions.Player.EndTurn.performed += OnEndTurn;
    }

    void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Click.performed -= OnClick;
            inputActions.Player.EndTurn.performed -= OnEndTurn;
            inputActions.Disable();
        }
    }

    public void EnableInput(bool enable)
    {
        inputEnabled = enable;

        if (highlighter != null)
        {
            if (enable)
                highlighter.ShowMoveRange(mover.CurrentGridCoords);
            else
                highlighter.ClearMoveRange();
        }
    }

    private void OnClick(InputAction.CallbackContext context)
    {
        if (!inputEnabled || mover.IsMoving || !mainCam || gridManager == null)
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
            {
                Debug.LogWarning("Could not find start tile at unit's current coordinates.");
                return;
            }

            List<HexTile> path = pathfinder.FindPath(startTile, targetTile);
            if (path == null || path.Count == 0)
            {
                Debug.Log("No path found.");
                return;
            }

            float totalCost = 0f;
            for (int i = 1; i < path.Count; i++)
                totalCost += path[i].tileType.moveCost;

            Debug.Log($"Path found with {path.Count} steps. Cost: {totalCost}");

            mover.MoveAlongPath(path);
        }
    }

    private void OnEndTurn(InputAction.CallbackContext context)
    {
        EndTurn();
    }

    public void EndTurn()
    {
        if (mover != null && mover.IsMoving)
            return;

        var manager = FindFirstObjectByType<TacticalCombatManager>();
        if (manager != null && manager.IsPlayerTurn)
        {
            Debug.Log("Ending player turn from button.");
            manager.EndCurrentTurn();
        }
    }
}
