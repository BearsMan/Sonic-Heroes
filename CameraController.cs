using UnityEngine;

[DisallowMultipleComponent]
public sealed class CameraController : MonoBehaviour
{
    #region Constants
    private const string MouseXAxis = "Mouse X";
    private const string MouseYAxis = "Mouse Y";
    private const string CameraPivotName = "Camera Pivot";

    #endregion

    #region Inspector

    [Header("References")]
    [Tooltip("The player or object that the camera follows.")]
    [SerializeField] private Transform target;

    [Tooltip("The transform used to rotate the camera.")]
    [SerializeField] private Transform cameraPivot;

    [Tooltip("The actual gameplay camera.")]
    [SerializeField] private Camera playerCamera;

    [Header("Follow Settings")]
    [SerializeField, Min(0f)] private float targetHeight = 2f;
    [SerializeField, Min(0.1f)] private float followDistance = 8f;
    [SerializeField, Min(0f)] private float followSmoothness = 18f;

    [Header("Rotation Settings")]
    [SerializeField, Min(0f)] private float horizontalSensitivity = 5f;
    [SerializeField, Min(0f)] private float verticalSensitivity = 3f;
    [SerializeField, Min(0f)] private float rotationSmoothness = 15f;

    [SerializeField, Range(-89f, 0f)]
    private float minimumPitch = -30f;

    [SerializeField, Range(0f, 89f)]
    private float maximumPitch = 60f;

    [SerializeField] private bool invertVerticalInput;

    [Header("Collision Settings")]
    [SerializeField] private bool useCollision = true;
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField, Min(0.01f)] private float collisionRadius = 0.25f;
    [SerializeField, Min(0f)] private float collisionPadding = 0.15f;
    [SerializeField, Min(0f)] private float collisionSmoothness = 25f;

    [Header("Formation Offsets")]
    [SerializeField] private Vector3 speedOffset;
    [SerializeField] private Vector3 flyOffset;
    [SerializeField] private Vector3 powerOffset;

    [Header("Cursor")]
    [SerializeField] private bool lockCursor = true;

    #endregion

    #region Runtime State

    private Vector3 activeOffset;

    private float yaw;
    private float pitch;
    private float currentDistance;

    private bool isInitialized;
    private bool isShuttingDown;
    private bool ownsCameraPivot;

    #endregion

    #region Public API

    public Transform Target => target;
    public Transform CameraPivot => cameraPivot;
    public Camera PlayerCamera => playerCamera;
    public bool IsInitialized => isInitialized;
    public Vector3 ActiveOffset => activeOffset;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheComponents();
        ResolveReferences();
        ConfigureComponents();
    }

    private void Start()
    {
        if (!InitializeCamera())
        {
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (isShuttingDown)
            return;

        CacheComponents();
        ResolveReferences();
        ConfigureComponents();
        ApplyCursorState();

        if (isInitialized)
        {
            RestoreRuntimeState();
        }
    }

    private void LateUpdate()
    {
        if (!isInitialized)
            return;

        if (!EnsureRuntimeReferences())
            return;

        ReadRotationInput();
        UpdatePivot();
        UpdateCameraDistance();
    }

    private void OnDisable()
    {
        CleanupRuntimeState();
    }

    private void OnDestroy()
    {
        isShuttingDown = true;
        CleanupDestroyedState();
    }

    private void OnValidate()
    {
        targetHeight = Mathf.Max(0f, targetHeight);
        followDistance = Mathf.Max(0.1f, followDistance);
        followSmoothness = Mathf.Max(0f, followSmoothness);
        horizontalSensitivity = Mathf.Max(0f, horizontalSensitivity);
        verticalSensitivity = Mathf.Max(0f, verticalSensitivity);
        rotationSmoothness = Mathf.Max(0f, rotationSmoothness);

        minimumPitch = Mathf.Clamp(minimumPitch, -89f, 0f);
        maximumPitch = Mathf.Clamp(maximumPitch, 0f, 89f);

        if (maximumPitch < minimumPitch)
            maximumPitch = minimumPitch;

        collisionRadius = Mathf.Max(0.01f, collisionRadius);
        collisionPadding = Mathf.Max(0f, collisionPadding);
        collisionSmoothness = Mathf.Max(0f, collisionSmoothness);
    }

    private void OnDrawGizmosSelected()
    {
        if (target == null)
            return;

        Vector3 targetPosition = GetTargetPosition();

        Gizmos.DrawWireSphere(targetPosition, 0.15f);

        if (cameraPivot == null)
            return;

        Gizmos.DrawLine(
            targetPosition,
            cameraPivot.position -
            cameraPivot.forward * followDistance);

        if (useCollision)
        {
            Gizmos.DrawWireSphere(
                cameraPivot.position -
                cameraPivot.forward * followDistance,
                collisionRadius);
        }
    }

    #endregion

    #region Initialization

    public bool InitializeCamera()
    {
        if (isInitialized)
            return true;

        CacheComponents();
        ResolveReferences();
        ConfigureComponents();

        if (!ValidateConfiguration())
        {
            isInitialized = false;

            Debug.LogError(
                $"CameraController failed to initialize on '{name}'.",
                this);

            return false;
        }

        Vector3 startingEuler =
            cameraPivot.rotation.eulerAngles;

        yaw = startingEuler.y;

        pitch =
            Mathf.Clamp(
                NormalizeAngle(startingEuler.x),
                minimumPitch,
                maximumPitch);

        currentDistance = followDistance;

        cameraPivot.position = GetTargetPosition();

        cameraPivot.rotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f);

        ApplyCameraPosition(currentDistance);
        ApplyCursorState();

        isInitialized = true;
        return true;
    }

    private void CacheComponents()
    {
        if (playerCamera == null)
        {
            playerCamera =
                GetComponentInChildren<Camera>(
                    includeInactive: true);
        }
    }

    private void ResolveReferences()
    {
        ResolveTarget();
        ResolvePlayerCamera();
        ResolveCameraPivot();
    }

    private void ResolveTarget()
    {
        if (target != null)
            return;

        UltimatePlayerMovement movement =
            GetComponent<UltimatePlayerMovement>();

        movement ??=
            GetComponentInParent<UltimatePlayerMovement>();

        if (movement != null)
            target = movement.transform;
    }

    private void ResolvePlayerCamera()
    {
        if (playerCamera != null)
            return;

        playerCamera =
            GetComponentInChildren<Camera>(
                includeInactive: true);

        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    private void ResolveCameraPivot()
    {
        if (cameraPivot != null)
            return;

        if (playerCamera != null)
        {
            Transform cameraParent =
                playerCamera.transform.parent;

            if (cameraParent != null &&
                cameraParent != target)
            {
                cameraPivot = cameraParent;
                ownsCameraPivot = false;
                return;
            }
        }

        CreateCameraPivot();
    }

    private void CreateCameraPivot()
    {
        if (target == null ||
            playerCamera == null)
        {
            return;
        }

        GameObject pivotObject =
            new(CameraPivotName);

        cameraPivot = pivotObject.transform;

        cameraPivot.SetParent(
            target,
            worldPositionStays: false);

        cameraPivot.localPosition =
            Vector3.up * targetHeight;

        playerCamera.transform.SetParent(
            cameraPivot,
            worldPositionStays: true);

        ownsCameraPivot = true;
    }

    private void ConfigureComponents()
    {
        if (playerCamera == null)
            return;

        playerCamera.transform.localRotation =
            Quaternion.identity;
    }

    private void RestoreRuntimeState()
    {
        ApplyCursorState();

        currentDistance =
            Mathf.Clamp(
                currentDistance,
                0f,
                followDistance);
    }

    #endregion

    #region Validation

    private bool ValidateConfiguration()
    {
        bool valid = true;

        valid &= ValidateReference(target, "Target");
        valid &= ValidateReference(cameraPivot, "Camera Pivot");
        valid &= ValidateReference(playerCamera, nameof(Camera));

        return valid;
    }

    private bool ValidateReference(
        Object reference,
        string displayName)
    {
        if (reference != null)
            return true;

        Debug.LogError(
            $"CameraController requires {displayName}.",
            this);

        return false;
    }

    private bool EnsureRuntimeReferences()
    {
        if (target != null &&
            cameraPivot != null &&
            playerCamera != null)
        {
            return true;
        }

        ResolveReferences();

        if (target == null ||
            cameraPivot == null ||
            playerCamera == null)
        {
            return false;
        }

        ConfigureComponents();
        return true;
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        RestoreCursorState();
    }

    private void CleanupDestroyedState()
    {
        CleanupRuntimeState();

        isInitialized = false;

        if (ownsCameraPivot &&
            cameraPivot != null)
        {
            if (Application.isPlaying)
                Destroy(cameraPivot.gameObject);
            else
                DestroyImmediate(cameraPivot.gameObject);
        }

        ownsCameraPivot = false;

        target = null;
        cameraPivot = null;
        playerCamera = null;
    }

    #endregion

    #region Target Management

    public bool SetTarget(
        Transform newTarget,
        bool snapImmediately = true)
    {
        if (newTarget == null)
        {
            Debug.LogWarning(
                "CameraController rejected a null target.",
                this);

            return false;
        }

        target = newTarget;

        if (ownsCameraPivot &&
            cameraPivot != null)
        {
            cameraPivot.SetParent(
                target,
                worldPositionStays: true);
        }

        if (snapImmediately &&
            cameraPivot != null)
        {
            cameraPivot.position =
                GetTargetPosition();
        }

        return true;
    }

    public void ClearTarget()
    {
        target = null;
    }

    public void ApplySpeedOffset()
    {
        activeOffset = speedOffset;
    }

    public void ApplyFlyOffset()
    {
        activeOffset = flyOffset;
    }

    public void ApplyPowerOffset()
    {
        activeOffset = powerOffset;
    }

    public void SetActiveOffset(Vector3 newOffset)
    {
        activeOffset = newOffset;
    }

    public void ClearActiveOffset()
    {
        activeOffset = Vector3.zero;
    }

    #endregion

    #region Input

    private void ReadRotationInput()
    {
        float horizontalInput = Input.GetAxis(MouseXAxis);
        float verticalInput = Input.GetAxis(MouseYAxis);

        yaw += horizontalInput * horizontalSensitivity;

        float verticalDirection = invertVerticalInput ? 1f : -1f;

        pitch += verticalInput * verticalSensitivity * verticalDirection;
        pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);
    }

    #endregion

    #region Camera Follow

    private void UpdatePivot()
    {
        if (cameraPivot == null ||
            target == null)
        {
            return;
        }

        Vector3 desiredPosition =
            GetTargetPosition();

        if (followSmoothness <= 0f)
        {
            cameraPivot.position =
                desiredPosition;
        }
        else
        {
            float positionBlend =
                CalculateExponentialBlend(
                    followSmoothness,
                    Time.unscaledDeltaTime);

            cameraPivot.position =
                Vector3.Lerp(
                    cameraPivot.position,
                    desiredPosition,
                    positionBlend);
        }

        Quaternion targetRotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f);

        if (rotationSmoothness <= 0f)
        {
            cameraPivot.rotation =
                targetRotation;
        }
        else
        {
            float rotationBlend =
                CalculateExponentialBlend(
                    rotationSmoothness,
                    Time.unscaledDeltaTime);

            cameraPivot.rotation =
                Quaternion.Slerp(
                    cameraPivot.rotation,
                    targetRotation,
                    rotationBlend);
        }
    }

    private Vector3 GetTargetPosition()
    {
        if (target == null)
            return transform.position;

        return
            target.position +
            target.up * targetHeight +
            target.TransformDirection(activeOffset);
    }

    #endregion

    #region Collision

    private void UpdateCameraDistance()
    {
        if (cameraPivot == null ||
            playerCamera == null)
        {
            return;
        }

        float desiredDistance =
            ResolveCollisionDistance();

        if (collisionSmoothness <= 0f)
        {
            currentDistance =
                desiredDistance;
        }
        else
        {
            float distanceBlend =
                CalculateExponentialBlend(
                    collisionSmoothness,
                    Time.unscaledDeltaTime);

            currentDistance =
                Mathf.Lerp(
                    currentDistance,
                    desiredDistance,
                    distanceBlend);
        }

        ApplyCameraPosition(currentDistance);
    }

    private float ResolveCollisionDistance()
    {
        if (cameraPivot == null ||
            !useCollision ||
            collisionMask.value == 0)
        {
            return followDistance;
        }

        Vector3 castOrigin =
            cameraPivot.position;

        Vector3 castDirection =
            -cameraPivot.forward;

        if (!Physics.SphereCast(
                castOrigin,
                collisionRadius,
                castDirection,
                out RaycastHit hit,
                followDistance,
                collisionMask,
                QueryTriggerInteraction.Ignore))
        {
            return followDistance;
        }

        float safeDistance =
            hit.distance -
            collisionPadding;

        return
            Mathf.Clamp(
                safeDistance,
                0f,
                followDistance);
    }

    private void ApplyCameraPosition(float distance)
    {
        if (playerCamera == null)
            return;

        playerCamera.transform.localPosition =
            new Vector3(
                0f,
                0f,
                -Mathf.Max(0f, distance));

        playerCamera.transform.localRotation =
            Quaternion.identity;
    }

    #endregion

    #region Cursor

    private void ApplyCursorState()
    {
        if (!Application.isPlaying)
            return;

        Cursor.lockState =
            lockCursor
                ? CursorLockMode.Locked
                : CursorLockMode.None;

        Cursor.visible = !lockCursor;
    }

    private void RestoreCursorState()
    {
        if (!Application.isPlaying)
            return;

        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible = true;
    }

    #endregion

    #region Utilities

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f)
            angle -= 360f;

        while (angle < -180f)
            angle += 360f;

        return angle;
    }

    private static float CalculateExponentialBlend(
        float smoothness,
        float deltaTime)
    {
        if (smoothness <= 0f ||
            deltaTime <= 0f)
        {
            return 1f;
        }

        return
            1f -
            Mathf.Exp(
                -smoothness *
                deltaTime);
    }

    #endregion
}
