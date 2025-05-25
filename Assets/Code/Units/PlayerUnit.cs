// File: PlayerUnit.cs
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Unit))]
[RequireComponent(typeof(UnitMover))]
public class PlayerUnit : MonoBehaviour
{
    private HexGrid grid;
    private Unit unit;
    private UnitMover unitMover;
    private string PUNIT_NAME => $"PUnit_{unit?.unitName ?? (gameObject != null ? gameObject.name : "UnknownPlayerUnit")}";


    void Awake() // Changed Start to Awake for earlier component grabbing
    {
        Debug.Log($"{PUNIT_NAME}: Awake - Attempting to get components.", this);
        unit = GetComponent<Unit>();
        if (unit == null) {
            Debug.LogError($"{PUNIT_NAME}: Unit component NOT FOUND in Awake! Disabling PlayerUnit.", this);
            if (this != null) this.enabled = false;
            return;
        }

        unitMover = GetComponent<UnitMover>();
        if (unitMover == null) {
            Debug.LogError($"{PUNIT_NAME}: UnitMover component NOT FOUND in Awake! Disabling PlayerUnit.", this);
            if (this != null) this.enabled = false;
            return;
        }
        Debug.Log($"{PUNIT_NAME}: Awake - Unit and UnitMover components acquired.", this);
    }

    void Start()
    {
        // Ensure components grabbed in Awake are still valid
        if (unit == null || unitMover == null)
        {
            Debug.LogError($"{PUNIT_NAME}: Start - Unit or UnitMover is null, though should have been acquired in Awake. Aborting. UnitNull: {unit==null}, MoverNull: {unitMover==null}", this);
            if (this != null) this.enabled = false;
            return;
        }

        grid = FindFirstObjectByType<HexGrid>(); // Find HexGrid in Start, after its Awake has likely run
        if (grid == null)
        {
            Debug.LogError($"{PUNIT_NAME}: Start - HexGrid NOT FOUND! Aborting setup.", this);
            if (this != null) this.enabled = false;
            return;
        }
        
        Debug.Log($"{PUNIT_NAME}: Start - HexGrid acquired. Starting DelayedSnapToGrid coroutine.", this);
        StartCoroutine(DelayedSnapToGrid());
    }

    private IEnumerator DelayedSnapToGrid() 
    {
        Debug.Log($"{PUNIT_NAME}: Coroutine DelayedSnapToGrid - Waiting for EndOfFrame. Grid valid: {grid != null}, Mover valid: {unitMover != null}", this);
        yield return new WaitForEndOfFrame(); 
        Debug.Log($"{PUNIT_NAME}: Coroutine DelayedSnapToGrid - Resumed after EndOfFrame.", this);

        // Check references immediately after resuming
        if (grid == null) 
        {
             Debug.LogError($"{PUNIT_NAME}: Coroutine DelayedSnapToGrid - HexGrid (grid field) is NULL after EndOfFrame! Aborting snap.", this);
             yield break; 
        }
        if (unitMover == null)
        {
             Debug.LogError($"{PUNIT_NAME}: Coroutine DelayedSnapToGrid - UnitMover (unitMover field) is NULL after EndOfFrame! Aborting snap.", this);
             yield break;
        }
        // Also check the unit component itself
        if (unit == null)
        {
            Debug.LogError($"{PUNIT_NAME}: Coroutine DelayedSnapToGrid - Unit (unit field) is NULL after EndOfFrame! Aborting snap.", this);
            yield break;
        }


        // If we reach here, all components are assumed valid by the checks above
        Debug.Log($"{PUNIT_NAME}: Coroutine DelayedSnapToGrid - All references seem valid. Getting coords.", this);
        Vector2Int coords = grid.GetCoordinateForPosition(transform.position);
        Debug.Log($"{PUNIT_NAME}: In DelayedSnapToGrid. World pos: {transform.position}, grid coords: {coords}. Mover's current: {unitMover.CurrentGridCoords}", this);
        
        unitMover.SnapToGrid(coords);
        
        // No need for another yield return null here unless SnapToGrid is also a coroutine or has deferred effects
        // that we need to wait for, which is unlikely for a snap operation.

        Debug.Log($"{PUNIT_NAME}: After SnapToGrid. Mover's current grid coords: {unitMover.CurrentGridCoords}", this);
    }
}