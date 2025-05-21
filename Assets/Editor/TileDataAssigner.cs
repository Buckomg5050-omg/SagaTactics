using UnityEditor; // Needed for Editor-specific classes
using UnityEngine;
using UnityEngine.Tilemaps; // Needed for the Tile class

public class TileDataAssigner : EditorWindow // This class creates a custom editor window
{
    [MenuItem("Tools/Eryndor/Generate CustomTiles")] // This creates a menu item in Unity's top bar
    static void Open()
    {
        GetWindow<TileDataAssigner>("TileData Assigner"); // Opens the custom window
    }

    void OnGUI() // This method defines the content of our custom window
    {
        GUILayout.Label("Generate CustomTiles from TileData ScriptableObjects", EditorStyles.boldLabel);

        if (GUILayout.Button("Generate CustomTiles"))
        {
            GenerateAndAssignCustomTiles();
        }
    }

    void GenerateAndAssignCustomTiles()
    {
        // Load all TileData ScriptableObjects from the "Assets/ScriptableObjects/TileData" folder
        // Make sure your TileData assets are in this specific path (or adjust this path)
        TileData[] tileDatas = Resources.LoadAll<TileData>("TileData");

        if (tileDatas.Length == 0)
        {
            Debug.LogError("No TileData ScriptableObjects found in Resources/TileData. Please create some first!");
            return;
        }

        // Ensure the "Assets/Tiles" folder exists for CustomTiles
        if (!AssetDatabase.IsValidFolder("Assets/Tiles"))
        {
            AssetDatabase.CreateFolder("Assets", "Tiles");
        }

        foreach (TileData data in tileDatas)
        {
            // Create a new CustomTile asset
            CustomTile newTile = CreateInstance<CustomTile>();
            newTile.tileData = data; // Assign the TileData to our CustomTile

            // Optional: If you want the tile to automatically use the sprite from TileData
            // newTile.sprite = data.displaySprite; // This line might be redundant if GetTileData is overridden correctly

            // Save the new CustomTile asset
            AssetDatabase.CreateAsset(newTile, $"Assets/Tiles/{data.name}Tile.asset");
            Debug.Log($"Generated CustomTile: Assets/Tiles/{data.name}Tile.asset");
        }
        AssetDatabase.SaveAssets(); // Save all created assets
        AssetDatabase.Refresh(); // Refresh the Unity Project window to show new assets

        Debug.Log("CustomTiles generation complete!");
    }
}