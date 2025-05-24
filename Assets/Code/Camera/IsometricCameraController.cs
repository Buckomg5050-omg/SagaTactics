// Assets/Code/Camera/IsometricCameraController.cs
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class IsometricCameraController : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private PlayerInput playerInput;
    private InputAction panAction;
    private InputAction zoomAction;
    private InputAction lookAction;
    private InputAction toggleOrbitLockAction;

    [Header("Gimbal & Camera References")]
    [SerializeField] private Transform cameraGimbal;
    [SerializeField] private CinemachineCamera cinemachineCameraComponent;

    [Header("Panning")]
    [SerializeField] private float panSpeed = 10f;
    private bool isManuallyPanning = false;

    [Header("Zooming (Local Offset Interpolation)")]
    [SerializeField] private Vector3 localPosZoomOut = new Vector3(0f, 15f, -15f);
    [SerializeField] private Vector3 localPosZoomIn = new Vector3(0f, 5f, -5f);
    [SerializeField] private float zoomLerpSpeed = 5f;
    private float currentZoomLevel = 0.5f;
    private float targetZoomLevel = 0.5f;

    [Header("Rotation (Orbit)")]
    [SerializeField] private bool startWithOrbitLocked = true;
    [SerializeField] private bool allowOrbitControl = true; 
    [SerializeField] private Vector2 rotationSpeed = new Vector2(100f, 80f);
    // MODIFICATION: Commented out invertYOrbit as its usage is also commented out.
    // If pitch control is re-enabled, this should be uncommented.
    // [SerializeField] private bool invertYOrbit = false; 
    private float gimbalYaw = 0f;
    private bool isOrbitActive = false;

    [Header("Active Unit Following")]
    [SerializeField] private bool followActiveUnit = true;
    [SerializeField] private float followLerpSpeed = 5f;
    [SerializeField] private Vector3 followOffset = Vector3.zero; 
    private Transform activeUnitTransform;
    private Vector3 targetGimbalPosition; 
    [Tooltip("Snap to the first active unit instantly on game start/combat start.")]
    [SerializeField] private bool snapToFirstUnitInstantly = true;
    private bool firstUnitFocused = false;

    [Header("Panning Limits (Optional)")]
    [SerializeField] private bool limitPan = false;
    [SerializeField] private Vector2 xPanLimit = new Vector2(-20f, 20f);
    [SerializeField] private Vector2 zPanLimit = new Vector2(-20f, 20f);

    void Awake()
    {
        if (playerInput == null)
        {
            // MODIFICATION: Used FindFirstObjectByType instead of FindObjectOfType
            playerInput = FindFirstObjectByType<PlayerInput>(); 
        }
        if (playerInput == null) { Debug.LogError("PLAYERINPUT NOT FOUND", this); return; }

        panAction = playerInput.actions["Pan"];
        zoomAction = playerInput.actions["ZoomCamera"];
        lookAction = playerInput.actions["LookCamera"];
        toggleOrbitLockAction = playerInput.actions["ToggleOrbitLock"];

        if (panAction == null) Debug.LogError("'Pan' action NOT FOUND", this);
        if (zoomAction == null) Debug.LogError("'ZoomCamera' action NOT FOUND", this);
        if (lookAction == null) Debug.LogError("'LookCamera' action NOT FOUND", this);
        if (toggleOrbitLockAction == null) Debug.LogError("'ToggleOrbitLock' action NOT FOUND", this);
            
        if (cinemachineCameraComponent == null)
        {
            if (cameraGimbal != null) cinemachineCameraComponent = cameraGimbal.GetComponentInChildren<CinemachineCamera>();
            if (cinemachineCameraComponent == null) Debug.LogError("CinemachineCamera Component not assigned AND not found", this);
        }

        if (cameraGimbal != null)
        {
            gimbalYaw = cameraGimbal.transform.eulerAngles.y;
            targetGimbalPosition = cameraGimbal.position;
        }
        if (cinemachineCameraComponent != null) cinemachineCameraComponent.transform.localPosition = Vector3.Lerp(localPosZoomOut, localPosZoomIn, currentZoomLevel);
        
        if (allowOrbitControl) SetOrbitActive(startWithOrbitLocked);
        else SetOrbitActive(false);
    }

    void OnEnable()
    {
        if (toggleOrbitLockAction != null) toggleOrbitLockAction.performed += OnToggleOrbitLock;
        TacticalCombatManager.OnActiveUnitChanged += HandleActiveUnitChanged; 
    }

    void OnDisable()
    {
        if (toggleOrbitLockAction != null) toggleOrbitLockAction.performed -= OnToggleOrbitLock;
        TacticalCombatManager.OnActiveUnitChanged -= HandleActiveUnitChanged;

        if (Cursor.lockState == CursorLockMode.Locked) Debug.Log("OnDisable: Unlocking cursor.", this);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void HandleActiveUnitChanged(Unit newActiveUnit) 
    {
        if (newActiveUnit != null)
        {
            // Debug.Log($"CameraController: Active unit changed to {newActiveUnit.name}", this);
            activeUnitTransform = newActiveUnit.transform;
            isManuallyPanning = false; 

            if (followActiveUnit)
            {
                targetGimbalPosition = activeUnitTransform.position + followOffset;
                if (limitPan)
                {
                    targetGimbalPosition.x = Mathf.Clamp(targetGimbalPosition.x, xPanLimit.x, xPanLimit.y);
                    targetGimbalPosition.z = Mathf.Clamp(targetGimbalPosition.z, zPanLimit.x, zPanLimit.y);
                }

                if (snapToFirstUnitInstantly && !firstUnitFocused && cameraGimbal != null)
                {
                    // Debug.Log($"CameraController: Snapping gimbal instantly to first unit: {newActiveUnit.name}", this);
                    cameraGimbal.position = targetGimbalPosition;
                    firstUnitFocused = true;
                }
            }
        }
        else
        {
            // Debug.Log("CameraController: Active unit set to null.", this);
            activeUnitTransform = null;
        }
    }

    private void OnToggleOrbitLock(InputAction.CallbackContext context)
    {
        if (!allowOrbitControl) return;
        SetOrbitActive(!isOrbitActive);
    }

    private void SetOrbitActive(bool active)
    {
        isOrbitActive = active;
        if (isOrbitActive && allowOrbitControl) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        else { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    void Update()
    {
        if (cameraGimbal == null || cinemachineCameraComponent == null || playerInput == null) return;

        if (panAction != null && panAction.enabled) HandlePanningInput(); 
        if (zoomAction != null && zoomAction.enabled) HandleZoomScrollInput();
        if (lookAction != null && lookAction.enabled && isOrbitActive && allowOrbitControl) HandleRotationInput();
        
        UpdateTargetGimbalPosition(); 
        ApplySmoothGimbalMovement();
        ApplySmoothZoom();
    }

    private void HandlePanningInput()
    {
        Vector2 inputVector = panAction.ReadValue<Vector2>();
        if (inputVector.sqrMagnitude > 0.01f) 
        {
            isManuallyPanning = true; 
            Vector3 panDirection = Vector3.zero;
            if (inputVector.y > 0) panDirection += GetForwardBasedOnCamera();
            if (inputVector.y < 0) panDirection -= GetForwardBasedOnCamera();
            if (inputVector.x > 0) panDirection += GetRightBasedOnCamera();
            if (inputVector.x < 0) panDirection -= GetRightBasedOnCamera();
            
            targetGimbalPosition += panDirection.normalized * panSpeed * Time.deltaTime;

            if (limitPan)
            {
                targetGimbalPosition.x = Mathf.Clamp(targetGimbalPosition.x, xPanLimit.x, xPanLimit.y);
                targetGimbalPosition.z = Mathf.Clamp(targetGimbalPosition.z, zPanLimit.x, zPanLimit.y);
            }
        }
    }
    
    private void UpdateTargetGimbalPosition()
    {
        if (followActiveUnit && activeUnitTransform != null && !isManuallyPanning)
        {
            Vector3 unitTargetPos = activeUnitTransform.position + followOffset;
            if (limitPan)
            {
                unitTargetPos.x = Mathf.Clamp(unitTargetPos.x, xPanLimit.x, xPanLimit.y);
                unitTargetPos.z = Mathf.Clamp(unitTargetPos.z, zPanLimit.x, zPanLimit.y);
            }
            if (Vector3.Distance(targetGimbalPosition, unitTargetPos) > 0.01f) // Reduced threshold for smoother follow if needed
            {
                 targetGimbalPosition = unitTargetPos;
            }
        }
    }

    private void ApplySmoothGimbalMovement()
    {
        if (!snapToFirstUnitInstantly || firstUnitFocused)
        {
            if (cameraGimbal != null) // Added null check for safety
                cameraGimbal.position = Vector3.Lerp(cameraGimbal.position, targetGimbalPosition, Time.deltaTime * followLerpSpeed);
        }
    }

    private void HandleZoomScrollInput() 
    {
        float scrollValue = zoomAction.ReadValue<float>();
        if (Mathf.Approximately(scrollValue, 0f)) return;
        // Consider making this scroll step a serialized field for sensitivity adjustment
        float zoomStep = 0.1f; 
        if (scrollValue > 0) targetZoomLevel += zoomStep; 
        else if (scrollValue < 0) targetZoomLevel -= zoomStep;
        targetZoomLevel = Mathf.Clamp01(targetZoomLevel);
    }

    private void ApplySmoothZoom()
    {
        if (cinemachineCameraComponent == null) return;
        currentZoomLevel = Mathf.Lerp(currentZoomLevel, targetZoomLevel, Time.deltaTime * zoomLerpSpeed);
        cinemachineCameraComponent.transform.localPosition = Vector3.Lerp(localPosZoomOut, localPosZoomIn, currentZoomLevel);
    }

    private void HandleRotationInput() 
    {
        if (cameraGimbal == null) return;
        Vector2 lookDelta = lookAction.ReadValue<Vector2>();
        gimbalYaw += lookDelta.x * rotationSpeed.x * Time.deltaTime;
        cameraGimbal.transform.rotation = Quaternion.Euler(0f, gimbalYaw, 0f);
        
        // Pitch control section remains commented out
        /* 
        if (cinemachineCameraComponent != null)
        {
            float pitchDelta = lookDelta.y * rotationSpeed.y * Time.deltaTime;
            // if (invertYOrbit) pitchDelta *= -1; // This is where invertYOrbit would be used
            // ... rest of pitch logic
        }
        */
    }

    private Vector3 GetForwardBasedOnCamera() 
    {
        if (Camera.main == null) return Vector3.forward; // Safety check
        Quaternion cameraPlanarRotation = Quaternion.Euler(0, Camera.main.transform.eulerAngles.y, 0);
        return cameraPlanarRotation * Vector3.forward;
    }

    private Vector3 GetRightBasedOnCamera() 
    {
        if (Camera.main == null) return Vector3.right; // Safety check
        Quaternion cameraPlanarRotation = Quaternion.Euler(0, Camera.main.transform.eulerAngles.y, 0);
        return cameraPlanarRotation * Vector3.right;
    }
}