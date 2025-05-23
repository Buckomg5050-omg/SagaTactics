using UnityEngine;

public class UnitFacing : MonoBehaviour
{
    private Transform modelRoot; // Optional: assign if needed, otherwise rotates entire GameObject

    void Awake()
    {
        modelRoot = transform; // Or set via inspector
    }

    /// <summary>
    /// Face the camera horizontally (used while idle).
    /// </summary>
    public void FaceCamera()
{
    Camera cam = Camera.main;
    if (cam == null) return;

    Vector3 camDirection = cam.transform.position - transform.position;
    camDirection.y = 0f;

    if (camDirection.sqrMagnitude > 0.01f)
    {
        Quaternion lookRotation = Quaternion.LookRotation(-camDirection);
        
        // Add yaw rotation of 180 degrees
        lookRotation *= Quaternion.Euler(0f, 180f, 0f);

        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 10f);
    }
}


    /// <summary>
    /// Face a world direction (used while moving).
    /// </summary>
    public void FaceDirection(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 10f);
        }
    }
}
