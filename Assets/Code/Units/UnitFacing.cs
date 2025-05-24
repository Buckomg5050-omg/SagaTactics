using UnityEngine;

public class UnitFacing : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float rotationSpeed = 10f; 
    [Tooltip("The specific Transform of the model to rotate. If null, defaults to this GameObject's transform.")]
    [SerializeField] private Transform modelToRotate; 

    [Header("Idle Camera Facing")]
    [Tooltip("Additional Y-axis rotation (yaw) when facing the camera during idle.")]
    [SerializeField] private float idleCameraYawOffset = 180f;

    private Quaternion targetModelRotation;
    private bool shouldBeUpdatingRotation = false;
    private Camera mainCamera;
    private string currentFacingMode = "None"; // For Debugging

    void Awake()
    {
        if (modelToRotate == null)
        {
            modelToRotate = transform; 
        }
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError($"UnitFacing on {gameObject.name}: Main Camera not found! Disabling facing logic.", this);
            enabled = false; 
            return;
        }
        if (modelToRotate != null) // Check if modelToRotate is valid before accessing its rotation
        {
            targetModelRotation = modelToRotate.rotation;
        } else {
            Debug.LogError($"UnitFacing on {gameObject.name}: modelToRotate is null even after default assignment. This should not happen.", this);
            enabled = false;
        }
    }

    void Update()
    {
        if (shouldBeUpdatingRotation && mainCamera != null && modelToRotate != null) 
        {
            // Debug.Log($"UnitFacing ({gameObject.name}) Update: Mode '{currentFacingMode}'. Slerping from {modelToRotate.rotation.eulerAngles} to {targetModelRotation.eulerAngles}", this);
            if (Quaternion.Angle(modelToRotate.rotation, targetModelRotation) > 0.1f) 
            {
                modelToRotate.rotation = Quaternion.Slerp(modelToRotate.rotation, targetModelRotation, Time.deltaTime * rotationSpeed);
            }
            else
            {
                modelToRotate.rotation = targetModelRotation; 
                // For camera facing, we want it to continue updating if camera/unit moves.
                // For specific target facing, we might set shouldBeUpdatingRotation = false here.
                // Let's refine this: if mode is camera, keep true. Else, set false.
                if (currentFacingMode != "Camera") {
                    shouldBeUpdatingRotation = false; 
                    // Debug.Log($"UnitFacing ({gameObject.name}): Reached target rotation for mode '{currentFacingMode}'. Stopping Slerp.", this);
                }
            }
        }
    }

    public void SetTargetLookAtWorldPosition(Vector3 worldPosition)
    {
        if (modelToRotate == null) return;

        Vector3 direction = worldPosition - modelToRotate.position;
        direction.y = 0f; 

        if (direction.sqrMagnitude > 0.001f) 
        {
            targetModelRotation = Quaternion.LookRotation(direction);
            shouldBeUpdatingRotation = true;
            currentFacingMode = "WorldPosition";
            // Debug.Log($"UnitFacing ({gameObject.name}): SetTargetLookAtWorldPosition. Mode: {currentFacingMode}", this);
        }
    }

    public void SetTargetLookTowardsCamera()
    {
        if (mainCamera == null || modelToRotate == null) return;

        Vector3 directionToCamera = mainCamera.transform.position - modelToRotate.position;
        directionToCamera.y = 0f; 

        if (directionToCamera.sqrMagnitude > 0.001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(-directionToCamera); 
            lookRotation *= Quaternion.Euler(0f, idleCameraYawOffset, 0f); 

            targetModelRotation = lookRotation;
            shouldBeUpdatingRotation = true;
            currentFacingMode = "Camera";
            Debug.Log($"UnitFacing ({gameObject.name}): SetTargetLookTowardsCamera. Mode: {currentFacingMode}. Yaw: {idleCameraYawOffset}. Target Angle: {targetModelRotation.eulerAngles.y}", this);
        }
    }

    public void SnapLookAtWorldPosition(Vector3 worldPosition)
    {
        if (modelToRotate == null) return;

        Vector3 direction = worldPosition - modelToRotate.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            modelToRotate.rotation = Quaternion.LookRotation(direction);
            targetModelRotation = modelToRotate.rotation; 
            shouldBeUpdatingRotation = false; 
            currentFacingMode = "SnapWorldPosition";
            // Debug.Log($"UnitFacing ({gameObject.name}): SnapLookAtWorldPosition. Mode: {currentFacingMode}", this);
        }
    }
    
    public void HoldCurrentFacing()
    {
        shouldBeUpdatingRotation = false;
        if (modelToRotate != null) 
        {
            targetModelRotation = modelToRotate.rotation; 
        }
        currentFacingMode = "Hold";
        // Debug.Log($"UnitFacing ({gameObject.name}): HoldCurrentFacing. Mode: {currentFacingMode}", this);
    }

    public Transform GetModelToRotateForMover() 
    {
        return modelToRotate;
    }
}