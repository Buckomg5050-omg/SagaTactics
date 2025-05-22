using UnityEngine;

public class HexMapGenerator : MonoBehaviour
{
    [Header("Hex Settings")]
    public GameObject hexPrefab;
    public int width = 10;
    public int height = 10;
    public float radius = 1f; // From center to any corner

    void Start()
    {
        GenerateCenteredGridAtCamera();
    }

    void GenerateCenteredGridAtCamera()
    {
        float horizSpacing = Mathf.Sqrt(3f) * radius; // flat-to-flat (X)
        float vertSpacing = 1.5f * radius;            // point-to-point row spacing (Z)

        float gridWidth = horizSpacing * (width - 1);
        float gridHeight = vertSpacing * (height - 1);

        Vector3 camCenter = Camera.main.transform.position;
        camCenter.y = 0f;

        Vector3 origin = camCenter - new Vector3(gridWidth / 2f, 0f, gridHeight / 2f);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                float posX = x * horizSpacing;
                float posZ = z * vertSpacing;

                // Offset odd columns vertically
                if (x % 2 == 1)
                    posZ += vertSpacing / 2f;

                Vector3 position = new Vector3(posX, 0f, posZ) + origin;
                Instantiate(hexPrefab, position, Quaternion.identity, transform);
            }
        }
    }
}
