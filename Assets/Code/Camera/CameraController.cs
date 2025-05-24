using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class CameraController : MonoBehaviour
{
    [Header("Camera Rig References")]
    [SerializeField] private Transform cameraFocusPoint;
    [SerializeField] private CinemachineCamera freeLookVCam;

    [Header("Panning Settings")]
    [SerializeField] private float panSpeed = 10f;
    [SerializeField] private float panSmoothing = 0.1f;

    [Header("Focus Settings")]
    [SerializeField] private float focusTransitionTime = 0.5f;

    private PlayerInputActions playerInputActions;
    private Vector2 panInput;
    private Vector3 currentPanVelocity;
    private Vector3 targetFocusPointPosition;

    private Transform currentFocusTarget;
    private bool isFocusingOnUnit = false;

    public bool IsPanningEnabled { get; set; } = true;

    private void Awake()
    {
        Debug.Log("CameraController: Awake", this);
        if (cameraFocusPoint == null)
        {
            Debug.LogError("CameraController: CameraFocusPoint is not assigned!", this);
            enabled = false; return;
        }
        if (freeLookVCam == null)
        {
            Debug.LogError("CameraController: FreeLookVCam is not assigned!", this);
            enabled = false; return;
        }

        playerInputActions = new PlayerInputActions();
        targetFocusPointPosition = cameraFocusPoint.position;
        Debug.Log($"CameraController: Initial targetFocusPointPosition: {targetFocusPointPosition}", this);

        // Ensure CinemachineBrain Default Blend Time is set in Inspector
    }

    private void OnEnable()
    {
        Debug.Log("CameraController: OnEnable", this);
        if (playerInputActions == null) playerInputActions = new PlayerInputActions();

        playerInputActions.CameraControls.Enable();
        playerInputActions.CameraControls.PanCamera.performed += OnPanCameraInput;
        playerInputActions.CameraControls.PanCamera.canceled += OnPanCameraInput;

        if (TacticalCombatManager.Instance != null)
        {
            TacticalCombatManager.OnActiveUnitChanged += HandleActiveUnitChanged;
            Debug.Log("CameraController: Subscribed to OnActiveUnitChanged.", this);
        }
        else
        {
            Debug.LogWarning("CameraController: TacticalCombatManager.Instance is null on Enable.", this);
        }
    }

    private void OnDisable()
    {
        Debug.Log("CameraController: OnDisable", this);
        if (playerInputActions != null)
        {
            playerInputActions.CameraControls.PanCamera.performed -= OnPanCameraInput;
            playerInputActions.CameraControls.PanCamera.canceled -= OnPanCameraInput;
            playerInputActions.CameraControls.Disable();
        }
        TacticalCombatManager.OnActiveUnitChanged -= HandleActiveUnitChanged;
        Debug.Log("CameraController: Unsubscribed from OnActiveUnitChanged.", this);
    }

    private void OnPanCameraInput(InputAction.CallbackContext context)
    {
        panInput = context.ReadValue<Vector2>();
        // Debug.Log($"CameraController: OnPanCameraInput - panInput: {panInput}, isFocusingOnUnit: {isFocusingOnUnit}", this);

        if (isFocusingOnUnit && panInput.sqrMagnitude > 0.01f)
        {
            Debug.Log("CameraController: Pan input received while focusing, calling BreakFocus().", this);
            BreakFocus();
        }
    }

    private void Update()
    {
        HandlePanning();
        // DEBUG: Log VCam targets every few frames
        // if (Time.frameCount % 60 == 0 && freeLookVCam != null) {
        //     Debug.Log($"VCam Update: LookAt={freeLookVCam.LookAt?.name ?? "null"}, Follow={freeLookVCam.Follow?.name ?? "null"}, isFocusing={isFocusingOnUnit}, currentFocusTgt={currentFocusTarget?.name ?? "null"}", this);
        // }
    }

    private void HandlePanning()
    {
        if (!IsPanningEnabled || cameraFocusPoint == null) return;

        if (panInput.sqrMagnitude > 0.01f)
        {
            if (isFocusingOnUnit) // This check might be redundant if OnPanCameraInput handles BreakFocus
            {
                // Debug.Log("CameraController: HandlePanning - Pan input while focused, ensuring BreakFocus was called.", this);
                // BreakFocus(); // Already called in OnPanCameraInput if input is new
            }

            Vector3 forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(Camera.main.transform.right, Vector3.up).normalized;
            Vector3 panDirection = (forward * panInput.y + right * panInput.x).normalized;
            targetFocusPointPosition += panDirection * panSpeed * Time.deltaTime;
        }

        if (!isFocusingOnUnit)
        {
            cameraFocusPoint.position = Vector3.SmoothDamp(
                cameraFocusPoint.position,
                targetFocusPointPosition,
                ref currentPanVelocity,
                panSmoothing
            );
            if (freeLookVCam.LookAt != cameraFocusPoint.transform)
            {
                // Debug.Log("HandlePanning: Not focused, VCam.LookAt is NOT cameraFocusPoint. Setting it.", this);
                freeLookVCam.LookAt = cameraFocusPoint.transform;
            }
            if (freeLookVCam.Follow != null)
            {
                // Debug.Log("HandlePanning: Not focused, VCam.Follow is NOT null. Clearing it.", this);
                freeLookVCam.Follow = null;
            }
        }
        else if (currentFocusTarget != null)
        {
            targetFocusPointPosition = currentFocusTarget.position;
            cameraFocusPoint.position = Vector3.Lerp(cameraFocusPoint.position, targetFocusPointPosition, Time.deltaTime * panSpeed * 0.5f);
            // When focused, VCam should be targeting currentFocusTarget directly.
            // This else block mainly keeps cameraFocusPoint near the target for smooth unfocus.
            // Ensure VCam is indeed targeting the unit:
            if (freeLookVCam.LookAt != currentFocusTarget) {
                // Debug.LogWarning($"HandlePanning: IS FOCUSED on {currentFocusTarget.name}, but VCam.LookAt is {freeLookVCam.LookAt?.name ?? "null"}. Forcing LookAt.", this);
                // freeLookVCam.LookAt = currentFocusTarget; // This should have been set by FocusOnUnit
            }
            if (freeLookVCam.Follow != currentFocusTarget) {
                 // Debug.LogWarning($"HandlePanning: IS FOCUSED on {currentFocusTarget.name}, but VCam.Follow is {freeLookVCam.Follow?.name ?? "null"}. Forcing Follow.", this);
                // freeLookVCam.Follow = currentFocusTarget; // This should have been set by FocusOnUnit
            }
        }
    }

    private void HandleActiveUnitChanged(Unit newActiveUnit)
    {
        if (newActiveUnit != null)
        {
            Debug.Log($"CameraController: HandleActiveUnitChanged - New active unit: {newActiveUnit.unitName}. Calling FocusOnUnit.", this);
            FocusOnUnit(newActiveUnit.transform);
        }
        else
        {
            Debug.Log("CameraController: HandleActiveUnitChanged - newActiveUnit is null. Calling BreakFocus.", this);
            BreakFocus();
        }
    }

    public void FocusOnUnit(Transform unitTransform)
    {
        if (unitTransform == null || freeLookVCam == null) return;

        Debug.Log($"CameraController: FocusOnUnit() called for {unitTransform.name}. Current VCam L@:{freeLookVCam.LookAt?.name} F:{freeLookVCam.Follow?.name}", this);
        currentFocusTarget = unitTransform;
        isFocusingOnUnit = true;

        freeLookVCam.Follow = unitTransform;
        freeLookVCam.LookAt = unitTransform;
        Debug.Log($"CameraController: FocusOnUnit() - Set VCam L@:{freeLookVCam.LookAt?.name} F:{freeLookVCam.Follow?.name}", this);

        targetFocusPointPosition = unitTransform.position; // For smooth pan-break
    }

    public void BreakFocus()
    {
        // if (!isFocusingOnUnit && freeLookVCam.LookAt == cameraFocusPoint.transform && freeLookVCam.Follow == null)
        // {
        //     Debug.Log("CameraController: BreakFocus() called, but already in free mode or VCam targets match CameraFocusPoint.", this);
        //     return;
        // }

        Debug.Log($"CameraController: BreakFocus() called. Was focusing: {isFocusingOnUnit}. Current VCam L@:{freeLookVCam.LookAt?.name} F:{freeLookVCam.Follow?.name}", this);
        isFocusingOnUnit = false;
        currentFocusTarget = null;

        freeLookVCam.Follow = null;
        freeLookVCam.LookAt = cameraFocusPoint.transform;
        Debug.Log($"CameraController: BreakFocus() - Set VCam L@:{freeLookVCam.LookAt?.name} F:{freeLookVCam.Follow?.name}", this);
    }
}