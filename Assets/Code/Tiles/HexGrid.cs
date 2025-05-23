using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class HexGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    public Vector2Int gridSize = new Vector2Int(10, 10);

    [Header("Tile Size & Orientation")]
    [Tooltip("This MUST match the effective outer radius of your hexPrefab for correct spacing.")]
    public float outerSize = 1f;
    [Tooltip("Flat-topped = true, Pointy-topped = false")]
    public bool isFlatTopped;

    [Header("Tile Types")]
    public List<TileType> tileTypes = new List<TileType>();
    public bool useRandomTileTypes = true;

    [Header("Editor Testing")]
    public bool randomizeWalkabilityInEditor = false;
    [Range(0f, 1f)] public float walkableChance = 0.85f;

    private List<GameObject> generatedTiles = new List<GameObject>();
    public Dictionary<Vector2Int, HexTile> tiles = new Dictionary<Vector2Int, HexTile>();

    private bool needsLayoutUpdate = false;

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            RequestLayoutGridUpdate();
        }
         else
        {
        LayoutGrid(); // 🔥 This is what was missing at runtime
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        RequestLayoutGridUpdate();
    }

    void RequestLayoutGridUpdate()
    {
        if (needsLayoutUpdate) return;
        needsLayoutUpdate = true;

#if UNITY_EDITOR
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            if (needsLayoutUpdate)
            {
                LayoutGrid();
                needsLayoutUpdate = false;
            }
        };
#endif
    }

    public void LayoutGrid()
    {
        if (this == null) return;

        ClearGrid();
        tiles.Clear();

        if (tileTypes == null || tileTypes.Count == 0)
        {
            Debug.LogError("No TileTypes assigned in HexGrid!", this);
            return;
        }

        for (int r = 0; r < gridSize.y; r++)
        {
            for (int q = 0; q < gridSize.x; q++)
            {
                Vector2Int coord = new Vector2Int(q, r);
                Vector3 position = GetPositionForHexFromCoordinate(coord);

                TileType selectedType = useRandomTileTypes
                    ? tileTypes[Random.Range(0, tileTypes.Count)]
                    : tileTypes[0];

                if (selectedType == null || selectedType.prefab == null)
                {
                    Debug.LogWarning($"TileType is missing prefab at ({q},{r})", this);
                    continue;
                }

                GameObject tileInstance = Instantiate(selectedType.prefab, position, Quaternion.identity, this.transform);
                tileInstance.name = $"Hex_{q}_{r}";
                tileInstance.transform.localPosition = position;

                HexTile tile = tileInstance.GetComponent<HexTile>();
                if (tile != null)
                {
                    tile.Initialize(coord);
                    tile.ApplyType(selectedType);

#if UNITY_EDITOR
                    if (!Application.isPlaying && randomizeWalkabilityInEditor)
                    {
                        tile.isWalkable = Random.value < walkableChance;
                    }
#endif

                    tiles[coord] = tile;
                }

                generatedTiles.Add(tileInstance);
            }
        }
    }

    public void ClearGrid()
    {
        if (this == null) return;

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null)
            {
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        generatedTiles.Clear();
        tiles.Clear();
    }

    public Vector3 GetPositionForHexFromCoordinate(Vector2Int coordinate)
    {
        int q = coordinate.x;
        int r = coordinate.y;

        float xPosition, zPosition;

        if (!isFlatTopped) // Pointy-topped
        {
            float hexWidth = Mathf.Sqrt(3f) * outerSize;
            float verticalSpacing = outerSize * 1.5f;
            float horizontalSpacing = hexWidth;

            xPosition = q * horizontalSpacing;
            if (r % 2 == 1) xPosition += hexWidth / 2f;

            zPosition = r * verticalSpacing;
        }
        else // Flat-topped
        {
            float hexHeight = Mathf.Sqrt(3f) * outerSize;
            float horizontalSpacing = outerSize * 1.5f;
            float verticalSpacing = hexHeight;

            xPosition = q * horizontalSpacing;
            zPosition = r * verticalSpacing;
            if (q % 2 == 1) zPosition += hexHeight / 2f;
        }

        return new Vector3(xPosition, 0, -zPosition);
    }

    public Vector2Int GetCoordinateForPosition(Vector3 worldPos)
    {
        float x = worldPos.x;
        float z = -worldPos.z;

        int q, r;

        if (!isFlatTopped) // Pointy-topped
        {
            float hexWidth = Mathf.Sqrt(3f) * outerSize;
            float verticalSpacing = outerSize * 1.5f;
            float horizontalSpacing = hexWidth;

            float approxR = z / verticalSpacing;
            int rBase = Mathf.FloorToInt(approxR);

            float rowOffset = (rBase % 2 == 1) ? (hexWidth / 2f) : 0f;
            float approxQ = (x - rowOffset) / horizontalSpacing;
            int qBase = Mathf.FloorToInt(approxQ);

            q = qBase;
            r = rBase;
        }
        else // Flat-topped
        {
            float hexHeight = Mathf.Sqrt(3f) * outerSize;
            float horizontalSpacing = outerSize * 1.5f;
            float verticalSpacing = hexHeight;

            float approxQ = x / horizontalSpacing;
            int qBase = Mathf.FloorToInt(approxQ);

            float colOffset = (qBase % 2 == 1) ? (hexHeight / 2f) : 0f;
            float approxR = (z - colOffset) / verticalSpacing;
            int rBase = Mathf.FloorToInt(approxR);

            q = qBase;
            r = rBase;
        }

        return new Vector2Int(q, r);
    }
    public List<HexTile> GetNeighbors(HexTile tile)
{
    List<HexTile> neighbors = new();
    Vector2Int coord = tile.coordinate;

    // Pointy-topped even-q layout
    Vector2Int[] offsetsEven = new Vector2Int[]
    {
        new Vector2Int(+1, 0), new Vector2Int(0, +1), new Vector2Int(-1, +1),
        new Vector2Int(-1, 0), new Vector2Int(-1, -1), new Vector2Int(0, -1)
    };

    Vector2Int[] offsetsOdd = new Vector2Int[]
    {
        new Vector2Int(+1, 0), new Vector2Int(+1, +1), new Vector2Int(0, +1),
        new Vector2Int(-1, 0), new Vector2Int(0, -1), new Vector2Int(+1, -1)
    };

    Vector2Int[] offsets = (coord.y % 2 == 0) ? offsetsEven : offsetsOdd;

    foreach (var offset in offsets)
    {
        Vector2Int neighborCoord = coord + offset;
        if (IsValidCoordinate(neighborCoord) && tiles.TryGetValue(neighborCoord, out HexTile neighbor))
        {
            neighbors.Add(neighbor);
        }
    }

    return neighbors;
}


    public bool IsValidCoordinate(Vector2Int coord)
    {
        return coord.x >= 0 && coord.x < gridSize.x &&
               coord.y >= 0 && coord.y < gridSize.y;
    }


    public HexTile GetTileAt(Vector2Int coords)
    {
        tiles.TryGetValue(coords, out HexTile tile);
        return tile;
    }
}
