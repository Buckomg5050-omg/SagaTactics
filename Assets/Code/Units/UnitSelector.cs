using UnityEngine;

public class UnitSelector : MonoBehaviour
{
    [SerializeField] private GameObject selectionMarkerPrefab;
    private GameObject selectionMarkerInstance;

    public Vector2Int CurrentCoords { get; set; }

    private HexGrid gridManager;

    void Awake()
    {
        gridManager = FindFirstObjectByType<HexGrid>();

        if (selectionMarkerPrefab != null)
        {
            selectionMarkerInstance = Instantiate(selectionMarkerPrefab);
            selectionMarkerInstance.SetActive(false);
        }
    }

    public void SetSelected(bool isSelected)
    {
        if (selectionMarkerInstance != null)
        {
            if (isSelected)
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
}
