using UnityEngine;

public class HexTile : MonoBehaviour
{
    public Vector2Int coordinate;
    public TileType tileType;
    public bool isWalkable = true;

    public void Initialize(Vector2Int coord)
    {
        coordinate = coord;
    }

    public void ApplyType(TileType type)
    {
        tileType = type;
        isWalkable = type != null && type.isWalkable;
    }
}
