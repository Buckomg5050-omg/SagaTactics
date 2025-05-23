using UnityEngine;
using System.Collections;

[RequireComponent(typeof(UnitMover))]
public class UnitSelector : MonoBehaviour
{
    [SerializeField] private GameObject selectionMarkerPrefab;
    [SerializeField] private float reenableDelay = 0.25f;

    private GameObject selectionMarkerInstance;
    private HexGrid gridManager;
    private UnitMover mover;
    private bool isSelected = false;

    public Vector2Int CurrentCoords { get; set; }

    void Awake()
    {
        gridManager = FindFirstObjectByType<HexGrid>();
        mover = GetComponent<UnitMover>();

        if (selectionMarkerPrefab != null)
        {
            selectionMarkerInstance = Instantiate(selectionMarkerPrefab);
            selectionMarkerInstance.SetActive(false);
        }

        mover.OnMoveComplete += () =>
        {
            StartCoroutine(ReenableAfterDelay());
        };
    }

    void Update()
    {
        if (mover.IsMoving && selectionMarkerInstance != null)
        {
            selectionMarkerInstance.SetActive(false);
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        if (selectionMarkerInstance != null)
        {
            if (selected && !mover.IsMoving)
            {
                Vector3 pos = gridManager.GetPositionForHexFromCoordinate(CurrentCoords);
                selectionMarkerInstance.transform.position = pos + Vector3.up * 0.3f;
                selectionMarkerInstance.SetActive(true);
            }
            else
            {
                selectionMarkerInstance.SetActive(false);
            }
        }
    }

    private IEnumerator ReenableAfterDelay()
    {
        yield return new WaitForSeconds(reenableDelay);

        if (isSelected && selectionMarkerInstance != null)
        {
            Vector3 pos = gridManager.GetPositionForHexFromCoordinate(CurrentCoords);
            selectionMarkerInstance.transform.position = pos + Vector3.up * 0.3f;
            selectionMarkerInstance.SetActive(true);
        }
    }
}
