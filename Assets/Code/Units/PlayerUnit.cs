using UnityEngine;

[RequireComponent(typeof(UnitMover))]
[RequireComponent(typeof(UnitFacing))]
[RequireComponent(typeof(UnitSelector))]
[RequireComponent(typeof(UnitHighlighter))]
[RequireComponent(typeof(UnitInputHandler))]
public class PlayerUnit : MonoBehaviour
{
    private UnitMover mover;
    private UnitFacing facing;
    private UnitSelector selector;
    private UnitHighlighter highlighter;
    private UnitInputHandler inputHandler;

    public bool isSelected = true;

    void Awake()
    {
        mover = GetComponent<UnitMover>();
        facing = GetComponent<UnitFacing>();
        selector = GetComponent<UnitSelector>();
        highlighter = GetComponent<UnitHighlighter>();
        inputHandler = GetComponent<UnitInputHandler>();
    }

   void Start()
{
    selector.SetSelected(isSelected);
    selector.CurrentCoords = mover.CurrentGridCoords;

    if (isSelected)
        highlighter.ShowMoveRange(selector.CurrentCoords);

    mover.OnMoveComplete += () =>
    {
        selector.CurrentCoords = mover.CurrentGridCoords;

        if (isSelected)
        {
            facing.FaceCamera();
            highlighter.ShowMoveRange(selector.CurrentCoords);
            selector.SetSelected(true); // Reposition selection marker
        }
    };
}


    void Update()
    {
        if (!mover.IsMoving && isSelected)
        {
            facing.FaceCamera();
        }

        if (!mover.IsMoving)
        {
            selector.CurrentCoords = FindCurrentCoord();
        }
    }

    private Vector2Int FindCurrentCoord()
    {
        HexGrid grid = FindFirstObjectByType<HexGrid>();
        return grid.GetCoordinateForPosition(transform.position);
    }

    public void SetSelected(bool value)
    {
        isSelected = value;
        selector.SetSelected(value);

        if (value)
            highlighter.ShowMoveRange(selector.CurrentCoords);
        else
            highlighter.ClearMoveRange();
    }
}
