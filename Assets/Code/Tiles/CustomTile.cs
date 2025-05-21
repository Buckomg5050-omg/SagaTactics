using UnityEngine;
using UnityEngine.Tilemaps; // Still needed for the Tile base class

// This allows us to create instances of this CustomTile through the Unity Editor.
// The path will be: "Assets/Create/Tile/Custom"
[CreateAssetMenu(fileName = "CustomTile", menuName = "Tile/Custom")]
public class CustomTile : Tile // Inherits from Unity's Tile class
{
    // This is the link to our custom TileData ScriptableObject (your own data container)
    public TileData tileData; // This is your custom ScriptableObject, named TileData

    // Optional: Override the RefreshTile method if you want specific refresh logic
    public override void RefreshTile(Vector3Int location, ITilemap tilemap)
    {
        base.RefreshTile(location, tilemap);
    }

    // CORRECTED GetTileData method signature - using fully qualified namespace
    // The 'UnityEngine.Tilemaps.TileData' here refers to Unity's internal struct
    public override void GetTileData(Vector3Int location, ITilemap tilemap, ref UnityEngine.Tilemaps.TileData tileDataStruct)
    {
        // Call the base method first
        base.GetTileData(location, tilemap, ref tileDataStruct);

        // Now, if our custom tileData ScriptableObject is assigned,
        // we use its sprite and other properties.
        if (this.tileData != null)
        {
            // Assign the display sprite from our custom TileData ScriptableObject
            tileDataStruct.sprite = this.tileData.displaySprite;

            // You can also assign the color, transform, etc., from your custom TileData
            // For example:
            // tileDataStruct.color = this.tileData.displayColor; // If you add a displayColor to your TileData SO
            // tileDataStruct.transform = Matrix4x4.identity; // If you want to control transform
        }
        // If tileData is null, it will just use default TileBase.TileData properties.
    }
}