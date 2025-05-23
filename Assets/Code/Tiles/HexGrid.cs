using UnityEngine;
using System.Collections.Generic;
using System.Linq; // NEW: Added for FindObjectsByType

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
    public bool isFlatTopped; // NOTE: Your GetNeighbors assumes pointy-topped, even-q. Ensure this matches.

    [Header("Tile Types")]
    public List<TileType> tileTypes = new List<TileType>();
    public bool useRandomTileTypes = true;

    [Header("Editor Testing")]
    public bool randomizeWalkabilityInEditor = false;
    [Range(0f, 1f)] public float walkableChance = 0.85f;

    private List<GameObject> generatedTiles = new List<GameObject>();
    public Dictionary<Vector2Int, HexTile> tiles = new Dictionary<Vector2Int, HexTile>();

    private bool needsLayoutUpdate = false;

    // Cache for active units to avoid repeated FindObjectsByType calls within a frame if GetUnitOnTile is called frequently
    private List<Unit> activeUnitsCache; // NEW
    private int lastUnitsCacheFrame = -1; // NEW

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            RequestLayoutGridUpdate();
        }
        else
        {
            LayoutGrid(); 
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
            if (this == null || !this.gameObject) return; // MODIFIED: Added !this.gameObject check
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
        if (this == null || !this.gameObject) return; // MODIFIED: Added !this.gameObject check for safety

        ClearGrid();
        // tiles.Clear(); // ClearGrid already does this

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
                // Your GetPositionForHexFromCoordinate logic seems to handle flat/pointy for positioning.
                Vector3 position = GetPositionForHexFromCoordinate(coord);


                TileType selectedType = useRandomTileTypes && tileTypes.Count > 0
                    ? tileTypes[Random.Range(0, tileTypes.Count)]
                    : (tileTypes.Count > 0 ? tileTypes[0] : null); // MODIFIED: Safer random selection

                if (selectedType == null || selectedType.prefab == null)
                {
                    Debug.LogWarning($"TileType or its prefab is missing. Cannot instantiate tile at ({q},{r})", this);
                    continue;
                }

                GameObject tileInstance = Instantiate(selectedType.prefab, position, Quaternion.identity, this.transform);
                tileInstance.name = $"Hex_{q}_{r}";
                tileInstance.transform.localPosition = position; // Position relative to parent HexGrid

                HexTile tileComponent = tileInstance.GetComponent<HexTile>(); // Renamed for clarity
                if (tileComponent != null)
                {
                    tileComponent.Initialize(coord); // Assuming HexTile.Initialize(coord, type)
                    tileComponent.ApplyType(selectedType);


#if UNITY_EDITOR
                    if (!Application.isPlaying && randomizeWalkabilityInEditor)
                    {
                        tileComponent.isWalkable = Random.value < walkableChance;
                    }
#endif
                    tiles[coord] = tileComponent;
                }
                else
                {
                    Debug.LogError($"Prefab for TileType '{selectedType.name}' does not have a HexTile component!", selectedType.prefab);
                }
                generatedTiles.Add(tileInstance);
            }
        }
    }

    public void ClearGrid()
    {
        if (this == null || !this.gameObject) return; // MODIFIED: Added !this.gameObject for safety during editor scripts

        // When clearing in editor, DestroyImmediate is needed. In play mode, Destroy.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null) // Check if child still exists (it might have been destroyed by other means)
            {
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }
        generatedTiles.Clear();
        tiles.Clear(); // Clear the dictionary
    }


    public Vector3 GetPositionForHexFromCoordinate(Vector2Int coordinate)
    {
        // axial_to_world:
        // x = size * (     3./2 * q                   )
        // y = size * (sqrt(3)/2 * q  +  sqrt(3) * r)
        // For offset coordinates, it's a bit more complex. Your current method seems to be for offset.
        // Let's stick to your existing implementation for positioning if it works for your hex orientation.
        // The important part is that it's consistent.

        int qCol = coordinate.x; // Assuming q is column
        int rRow = coordinate.y; // Assuming r is row

        float x, z; // Unity's Z is depth, typically mapped to Y in 2D hex math then negated

        if (isFlatTopped) // Flat-topped hexes
        {
            // x = size * 3/2 * col
            // y = size * sqrt(3)/2 * col + size * sqrt(3) * row
            // If col is odd, y further offset by size * sqrt(3)/2
            x = outerSize * 1.5f * qCol;
            z = outerSize * Mathf.Sqrt(3f) * rRow + (qCol % 2 == 1 ? (outerSize * Mathf.Sqrt(3f) * 0.5f) : 0f);
        }
        else // Pointy-topped hexes
        {
            // x = size * sqrt(3) * col + (row is odd ? size * sqrt(3)/2 : 0)
            // y = size * 3/2 * row
            x = outerSize * Mathf.Sqrt(3f) * qCol + (rRow % 2 == 1 ? (outerSize * Mathf.Sqrt(3f) * 0.5f) : 0f);
            z = outerSize * 1.5f * rRow;
        }
        // Your original implementation had z negated. If that's how your world is set up, keep it.
        // return new Vector3(x, 0, z); // If Z is positive "up" on your 2D plane
        return new Vector3(x, 0, -z); // Sticking to your original Z negation for consistency
    }


    public Vector2Int GetCoordinateForPosition(Vector3 worldPos)
    {
        // This is a complex conversion (pixel_to_hex or world_to_axial/offset)
        // Your existing implementation will be kept.
        // Ensure it correctly reflects whether it's flat or pointy topped.
        // The key is consistency with GetPositionForHexFromCoordinate.

        float worldX = worldPos.x;
        float worldZ = -worldPos.z; // Using your negation
        int q, r;

        if (isFlatTopped)
        {
            q = Mathf.RoundToInt(worldX / (outerSize * 1.5f));
            r = Mathf.RoundToInt((worldZ - (q % 2 == 1 ? (outerSize * Mathf.Sqrt(3f) * 0.5f) : 0f)) / (outerSize * Mathf.Sqrt(3f)));
        }
        else // Pointy-topped
        {
            r = Mathf.RoundToInt(worldZ / (outerSize * 1.5f));
            q = Mathf.RoundToInt((worldX - (r % 2 == 1 ? (outerSize * Mathf.Sqrt(3f) * 0.5f) : 0f)) / (outerSize * Mathf.Sqrt(3f)));
        }
        return new Vector2Int(q, r);
    }


    public List<HexTile> GetNeighbors(HexTile tile)
    {
        List<HexTile> neighbors = new();
        if (tile == null) // NEW: Safety check
        {
            Debug.LogWarning("GetNeighbors called with a null tile.", this);
            return neighbors;
        }
        Vector2Int coord = tile.coordinate;

        // Axial directions (more universal for hex grids)
        // Define these based on your coordinate system (odd-r, even-r, odd-q, even-q for offset)
        // Your current implementation uses offset coordinates. Let's stick to it but ensure it's correct for your orientation.
        // The offsets you have are for "odd-q" if pointy-topped, or "odd-r" if flat-topped.
        // Assuming your GetPositionForHexFromCoordinate is for "odd-r" for pointy-topped
        // And "odd-q" for flat-topped (where q is x, r is y)
        // Let's verify your GetNeighbors logic matches your chosen orientation (isFlatTopped).
        // The provided GetNeighbors was specifically for POINTY_TOPPED with EVEN-Q offset.
        // This needs to adapt if you switch to flat_topped.

        // For POINTY-TOPPED (q = col, r = row)
        // If isFlatTopped is FALSE (i.e., pointy-topped)
        Vector2Int[] pointy_offsets_even_r = { // When r (row) is even
            new Vector2Int(+1,  0), new Vector2Int( 0, -1), new Vector2Int(-1, -1),
            new Vector2Int(-1,  0), new Vector2Int(-1, +1), new Vector2Int( 0, +1)
        };
        Vector2Int[] pointy_offsets_odd_r = { // When r (row) is odd
            new Vector2Int(+1,  0), new Vector2Int(+1, -1), new Vector2Int( 0, -1),
            new Vector2Int(-1,  0), new Vector2Int( 0, +1), new Vector2Int(+1, +1)
        };

        // For FLAT-TOPPED (q = col, r = row)
        Vector2Int[] flat_offsets_even_q = { // When q (col) is even
            new Vector2Int(+1,  0), new Vector2Int(+1, -1), new Vector2Int( 0, -1),
            new Vector2Int(-1,  0), new Vector2Int( 0, +1), new Vector2Int(+1, +1)
        };
        Vector2Int[] flat_offsets_odd_q = { // When q (col) is odd
            new Vector2Int(+1,  0), new Vector2Int( 0, -1), new Vector2Int(-1, -1),
            new Vector2Int(-1,  0), new Vector2Int(-1, +1), new Vector2Int( 0, +1)
        };
        
        Vector2Int[] offsetsToUse;
        if (!isFlatTopped) // Pointy-topped
        {
            offsetsToUse = (coord.y % 2 == 0) ? pointy_offsets_even_r : pointy_offsets_odd_r;
        }
        else // Flat-topped
        {
            offsetsToUse = (coord.x % 2 == 0) ? flat_offsets_even_q : flat_offsets_odd_q;
        }

        foreach (var offset in offsetsToUse)
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

    // NEW METHOD: GetUnitOnTile
    /// <summary>
    /// Finds the Unit currently occupying the tile at the given coordinate.
    /// Returns null if no unit is found or the tile is invalid.
    /// </summary>
    /// <param name="coordinate">The grid coordinate to check.</param>
    /// <returns>The Unit on the tile, or null.</returns>
    public Unit GetUnitOnTile(Vector2Int coordinate)
    {
        // First, ensure the tile itself is valid and exists in our grid dictionary
        if (!tiles.ContainsKey(coordinate))
        {
            // This means the coordinate is either out of bounds or no tile was generated there.
            // IsValidCoordinate checks bounds, tiles.ContainsKey checks generation.
            // If you expect all valid coordinates to have a tile, IsValidCoordinate might be enough.
            // For safety, checking both is good.
            return null; 
        }

        // Update unit cache if it's a new frame or cache is empty
        // This is a micro-optimization. If GetUnitOnTile is called many times per frame,
        // this avoids calling FindObjectsByType repeatedly.
        if (Application.isPlaying && (activeUnitsCache == null || Time.frameCount != lastUnitsCacheFrame))
        {
            activeUnitsCache = FindObjectsByType<Unit>(FindObjectsSortMode.None)
                                .Where(u => u.gameObject.activeInHierarchy && u.GetComponent<UnitMover>() != null)
                                .ToList();
            lastUnitsCacheFrame = Time.frameCount;
        }
        else if (!Application.isPlaying) // Editor mode, always find fresh
        {
             activeUnitsCache = FindObjectsByType<Unit>(FindObjectsSortMode.None)
                                .Where(u => u.gameObject.activeInHierarchy && u.GetComponent<UnitMover>() != null)
                                .ToList();
        }


        if (activeUnitsCache != null)
        {
            foreach (Unit unit in activeUnitsCache)
            {
                // UnitMover might be null if the unit is misconfigured or being destroyed
                UnitMover mover = unit.GetComponent<UnitMover>(); 
                if (mover != null && mover.CurrentGridCoords == coordinate)
                {
                    return unit;
                }
            }
        }
        return null; // No unit found at that coordinate
    }
}