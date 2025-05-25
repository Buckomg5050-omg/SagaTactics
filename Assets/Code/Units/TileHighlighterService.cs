// File: TileHighlighterService.cs
using UnityEngine;
using System.Collections.Generic;

public class TileHighlighterService : MonoBehaviour
{
    public static TileHighlighterService Instance { get; private set; }

    [SerializeField] private HighlightSettings highlightSettings;
    
    private List<GameObject> activeMoveRangeVisuals = new List<GameObject>();
    private List<GameObject> activeAttackRangeVisuals = new List<GameObject>();
    private List<GameObject> activePathPreviewVisuals = new List<GameObject>();
    private GameObject activeHoverVisual = null;

    private HexGrid gridManager; 
    private string THS_LOG_PREFIX => $"THS_{GetHashCode()}: "; // Unique prefix for this instance

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate TileHighlighterService instance found. Destroying this one.", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // DontDestroyOnLoad(gameObject); // Optional: if this service needs to persist across scenes

        if (highlightSettings == null)
        {
            Debug.LogError($"{THS_LOG_PREFIX}HighlightSettings ScriptableObject not assigned! Disabling service.", this);
            enabled = false; // Disable if settings are missing
            return;
        }
        gridManager = FindFirstObjectByType<HexGrid>();
        if (gridManager == null)
        {
            Debug.LogError($"{THS_LOG_PREFIX}HexGrid not found! Disabling service.", this);
            enabled = false; // Disable if grid is missing
            return;
        }
        Debug.Log($"{THS_LOG_PREFIX}Initialized successfully.", this);
    }
    
    void OnDestroy() // Good practice to nullify instance
    {
        if(Instance == this) Instance = null;
    }


    // --- Movement Range ---
    public void ShowMovementRange(HashSet<Vector2Int> tilesToHighlight)
    {
        if (!enabled || highlightSettings == null || ObjectPooler.Instance == null || gridManager == null) {
            Debug.LogWarning($"{THS_LOG_PREFIX}ShowMovementRange bailing: Enabled:{enabled}, SettingsNull:{highlightSettings==null}, PoolerNull:{ObjectPooler.Instance==null}, GridNull:{gridManager==null}", this);
            return;
        }
        
        ClearMovementRange(); 
        int count = 0;
        if (tilesToHighlight != null) {
            foreach (Vector2Int coord in tilesToHighlight)
            {
                Vector3 worldPos = gridManager.GetPositionForHexFromCoordinate(coord) + Vector3.up * highlightSettings.movementRangeYOffset;
                GameObject visual = ObjectPooler.Instance.SpawnFromPool("MoveHighlight", worldPos, Quaternion.identity);
                if (visual != null)
                {
                    if (this.transform != null) visual.transform.SetParent(this.transform); // Parent to self for organization
                    activeMoveRangeVisuals.Add(visual);
                    count++;
                }
            }
        }
        // Debug.Log($"{THS_LOG_PREFIX}ShowMovementRange: Displayed {count} move highlights.", this);
    }

    public void ClearMovementRange()
    {
        if (ObjectPooler.Instance == null && activeMoveRangeVisuals.Count > 0) // Only log warning if there's something to clear
        {
            Debug.LogWarning($"{THS_LOG_PREFIX}ObjectPooler.Instance is null in ClearMovementRange. Manually destroying {activeMoveRangeVisuals.Count} visuals.", this);
            for (int i = activeMoveRangeVisuals.Count - 1; i >= 0; i--) { if (activeMoveRangeVisuals[i] != null) Destroy(activeMoveRangeVisuals[i]); }
            activeMoveRangeVisuals.Clear();
            return;
        }
        // Debug.Log($"{THS_LOG_PREFIX}Clearing {activeMoveRangeVisuals.Count} active move range visuals.", this);
        for (int i = activeMoveRangeVisuals.Count - 1; i >= 0; i--)
        {
            GameObject visual = activeMoveRangeVisuals[i];
            if (visual != null) 
            {
                ObjectPooler.Instance.ReturnToPool("MoveHighlight", visual);
            }
        }
        activeMoveRangeVisuals.Clear();
    }

    // --- Attack Range ---
    public void ShowAttackRange(HashSet<Vector2Int> tilesWithValidTargets)
    {
        if (!enabled || highlightSettings == null || ObjectPooler.Instance == null || gridManager == null) return;
        ClearAttackRange();
        int count = 0;
        if (tilesWithValidTargets != null) {
            foreach (Vector2Int coord in tilesWithValidTargets)
            {
                Vector3 worldPos = gridManager.GetPositionForHexFromCoordinate(coord) + Vector3.up * highlightSettings.attackRangeYOffset;
                GameObject visual = ObjectPooler.Instance.SpawnFromPool("AttackHighlight", worldPos, Quaternion.identity);
                if (visual != null)
                {
                     if (this.transform != null) visual.transform.SetParent(this.transform);
                    activeAttackRangeVisuals.Add(visual);
                    count++;
                }
            }
        }
        // Debug.Log($"{THS_LOG_PREFIX}ShowAttackRange: Displayed {count} attack highlights.", this);
    }

    public void ClearAttackRange()
    {
        if (ObjectPooler.Instance == null && activeAttackRangeVisuals.Count > 0) {
            Debug.LogWarning($"{THS_LOG_PREFIX}ObjectPooler.Instance is null in ClearAttackRange. Manually destroying {activeAttackRangeVisuals.Count} visuals.", this);
            for (int i = activeAttackRangeVisuals.Count - 1; i >= 0; i--) { if (activeAttackRangeVisuals[i] != null) Destroy(activeAttackRangeVisuals[i]); }
            activeAttackRangeVisuals.Clear();
            return;
        }
        // Debug.Log($"{THS_LOG_PREFIX}Clearing {activeAttackRangeVisuals.Count} active attack range visuals.", this);
        for (int i = activeAttackRangeVisuals.Count - 1; i >= 0; i--) {
            if (activeAttackRangeVisuals[i] != null) ObjectPooler.Instance.ReturnToPool("AttackHighlight", activeAttackRangeVisuals[i]);
        }
        activeAttackRangeVisuals.Clear();
    }
    
    // --- Path Preview ---
    public void ShowPathPreview(List<Vector2Int> pathCoords) 
    {
        if (!enabled || highlightSettings == null || ObjectPooler.Instance == null || gridManager == null) return;
        ClearPathPreview();
        int count = 0;
        if (pathCoords == null || pathCoords.Count < 2) return; 

        for (int i = 1; i < pathCoords.Count; i++) 
        {
            Vector2Int coord = pathCoords[i];
            Vector3 worldPos = gridManager.GetPositionForHexFromCoordinate(coord) + Vector3.up * highlightSettings.pathPreviewYOffset;
            GameObject visual = ObjectPooler.Instance.SpawnFromPool("PathMarker", worldPos, Quaternion.identity);
            if (visual != null)
            {
                if (this.transform != null) visual.transform.SetParent(this.transform);
                activePathPreviewVisuals.Add(visual);
                count++;
            }
        }
        // Debug.Log($"{THS_LOG_PREFIX}ShowPathPreview: Displayed {count} path markers.", this);
    }

    public void ClearPathPreview()
    {
        if (ObjectPooler.Instance == null && activePathPreviewVisuals.Count > 0) {
             Debug.LogWarning($"{THS_LOG_PREFIX}ObjectPooler.Instance is null in ClearPathPreview. Manually destroying {activePathPreviewVisuals.Count} visuals.", this);
            for (int i = activePathPreviewVisuals.Count - 1; i >= 0; i--) { if (activePathPreviewVisuals[i] != null) Destroy(activePathPreviewVisuals[i]); }
            activePathPreviewVisuals.Clear();
            return;
        }
        // Debug.Log($"{THS_LOG_PREFIX}Clearing {activePathPreviewVisuals.Count} active path preview visuals.", this);
        for (int i = activePathPreviewVisuals.Count - 1; i >= 0; i--) {
             if (activePathPreviewVisuals[i] != null) ObjectPooler.Instance.ReturnToPool("PathMarker", activePathPreviewVisuals[i]);
        }
        activePathPreviewVisuals.Clear();
    }

    // --- Tile Hover ---
    public void ShowTileHover(Vector2Int? coord) 
    {
        if (!enabled || highlightSettings == null || ObjectPooler.Instance == null || gridManager == null) return;

        if (activeHoverVisual != null)
        {
            ObjectPooler.Instance.ReturnToPool("HoverHighlight", activeHoverVisual);
            activeHoverVisual = null; // Clear reference immediately after returning
        }

        if (coord.HasValue)
        {
            Vector3 worldPos = gridManager.GetPositionForHexFromCoordinate(coord.Value) + Vector3.up * highlightSettings.tileHoverYOffset;
            activeHoverVisual = ObjectPooler.Instance.SpawnFromPool("HoverHighlight", worldPos, Quaternion.identity);
            if (activeHoverVisual != null && this.transform != null)
            {
                activeHoverVisual.transform.SetParent(this.transform);
            }
        }
        // Debug.Log($"{THS_LOG_PREFIX}ShowTileHover: {(coord.HasValue ? "Showing at "+coord.Value : "Hiding")}", this);
    }
    
    public void ClearAllHighlights()
    {
        // Debug.Log($"{THS_LOG_PREFIX}ClearAllHighlights called.", this);
        ClearMovementRange();
        ClearAttackRange();
        ClearPathPreview();
        ShowTileHover(null); 
    }

    // This can be called by TacticalCombatManager or a game state manager when combat ends
    // to ensure everything is cleaned up if this service persists.
    public void OnCombatPhaseEnd() 
    {
        Debug.Log($"{THS_LOG_PREFIX}Combat Phase End detected, clearing all highlights.", this);
        ClearAllHighlights();
    }
}