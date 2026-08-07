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

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

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

    public Vector3 ActiveOffset => activeOffset;

    public bool IsInitialized => isInitialized;
    public bool HasTarget => target != null;

    public bool Setup(
        Transform followTarget,
        bool snapImmediately = true)
    {
        if (followTarget == null)
        {
            Debug.LogError(
                "CameraController Setup received no follow target.",
                this);

            return false;
        }

        target =
            followTarget;

        CacheComponents();
        ResolveReferences();
        ConfigureComponents();

        if (!InitializeCamera())
            return false;

        if (snapImmediately)
        {
            SnapToTarget();
        }

        return true;
    }

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

        target =
            newTarget;

        CacheComponents();
        ResolvePlayerCamera();
        ResolveCameraPivot();
        ConfigureComponents();

        ReparentOwnedPivot();

        if (!isInitialized)
        {
            return Setup(
                newTarget,
                snapImmediately);
        }

        if (snapImmediately)
        {
            SnapToTarget();
        }

        LogStateChange(
            $"Camera target changed to '{newTarget.name}'.");

        return true;
    }

    public void ClearTarget()
    {
        target = null;
    }

    public void ApplySpeedOffset()
    {
        SetActiveOffset(
            speedOffset);
    }

    public void ApplyFlyOffset()
    {
        SetActiveOffset(
            flyOffset);
    }

    public void ApplyPowerOffset()
    {
        SetActiveOffset(
            powerOffset);
    }

    public void SetActiveOffset(
        Vector3 newOffset)
    {
        activeOffset =
            newOffset;
    }

    public void ClearActiveOffset()
    {
        activeOffset =
            Vector3.zero;
    }

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
        if (target == null)
        {
            ResolveTarget();
        }

        if (target == null)
        {
            LogStateChange(
                "CameraController is waiting for CharacterSwitch to assign a target.");

            return;
        }

        Setup(
            target,
            snapImmediately: true);
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
        if (!isInitialized ||
            target == null)
        {
            return;
        }

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
        targetHeight =
            Mathf.Max(
                0f,
                targetHeight);

        followDistance =
            Mathf.Max(
                0.1f,
                followDistance);

        followSmoothness =
            Mathf.Max(
                0f,
                followSmoothness);

        horizontalSensitivity =
            Mathf.Max(
                0f,
                horizontalSensitivity);

        verticalSensitivity =
            Mathf.Max(
                0f,
                verticalSensitivity);

        rotationSmoothness =
            Mathf.Max(
                0f,
                rotationSmoothness);

        minimumPitch =
            Mathf.Clamp(
                minimumPitch,
                -89f,
                0f);

        maximumPitch =
            Mathf.Clamp(
                maximumPitch,
                0f,
                89f);

        collisionRadius =
            Mathf.Max(
                0.01f,
                collisionRadius);

        collisionPadding =
            Mathf.Max(
                0f,
                collisionPadding);

        collisionSmoothness =
            Mathf.Max(
                0f,
                collisionSmoothness);
    }

    private void OnDrawGizmosSelected()
    {
        if (target == null)
            return;

        Vector3 targetPosition =
            GetTargetPosition();

        Gizmos.DrawWireSphere(
            targetPosition,
            0.15f);

        if (cameraPivot == null)
            return;

        Vector3 cameraPosition =
            cameraPivot.position -
            cameraPivot.forward *
            followDistance;

        Gizmos.DrawLine(
            targetPosition,
            cameraPosition);

        if (useCollision)
        {
            Gizmos.DrawWireSphere(
                cameraPosition,
                collisionRadius);
        }
    }

    #endregion

    #region Initialization

    private bool InitializeCamera()
    {
        if (isInitialized)
            return true;

        if (!ValidateConfiguration())
        {
            Debug.LogError(
                $"CameraController failed to initialize on '{name}'.",
                this);

            return false;
        }

        Vector3 startingEuler =
            cameraPivot.rotation.eulerAngles;

        yaw =
            startingEuler.y;

        pitch =
            Mathf.Clamp(
                NormalizeAngle(
                    startingEuler.x),
                minimumPitch,
                maximumPitch);

        currentDistance =
            followDistance;

        ApplyCursorState();

        isInitialized = true;

        LogStateChange(
            "CameraController initialized.");

        return true;
    }

    private void CacheComponents()
    {
        playerCamera ??=
            GetComponent<Camera>();

        playerCamera ??=
            GetComponentInChildren<Camera>(
                includeInactive: true);
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

        CharacterSwitch characterSwitch =
            FindAnyObjectByType<CharacterSwitch>(
                FindObjectsInactive.Include);

        if (characterSwitch != null &&
            characterSwitch.CurrentLeader != null)
        {
            target =
                characterSwitch.CurrentLeader;

            return;
        }

        UltimatePlayerMovement movement =
            FindAnyObjectByType<UltimatePlayerMovement>(
                FindObjectsInactive.Include);

        if (movement != null)
        {
            target =
                movement.transform;
        }
    }

    private void ResolvePlayerCamera()
    {
        if (playerCamera != null)
            return;

        playerCamera =
            GetComponentInChildren<Camera>(
                includeInactive: true);

        playerCamera ??=
            Camera.main;

        if (playerCamera == null)
        {
            playerCamera =
                FindAnyObjectByType<Camera>(
                    FindObjectsInactive.Include);
        }
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
                cameraParent != transform &&
                cameraParent != target)
            {
                cameraPivot =
                    cameraParent;

                ownsCameraPivot =
                    false;

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

        cameraPivot =
            pivotObject.transform;

        cameraPivot.SetParent(
            target,
            worldPositionStays: false);

        cameraPivot.localPosition =
            Vector3.up *
            targetHeight;

        cameraPivot.localRotation =
            Quaternion.identity;

        playerCamera.transform.SetParent(
            cameraPivot,
            worldPositionStays: false);

        ownsCameraPivot =
            true;
    }

    private void ConfigureComponents()
    {
        if (playerCamera == null)
            return;

        playerCamera.transform.localRotation =
            Quaternion.identity;
    }

    private void ReparentOwnedPivot()
    {
        if (!ownsCameraPivot ||
            cameraPivot == null ||
            target == null ||
            cameraPivot.parent == target)
        {
            return;
        }

        cameraPivot.SetParent(
            target,
            worldPositionStays: true);
    }

    private void RestoreRuntimeState()
    {
        currentDistance =
            Mathf.Clamp(
                currentDistance,
                0f,
                followDistance);

        ApplyCursorState();
    }

    #endregion

    #region Target Positioning

    private void SnapToTarget()
    {
        if (target == null ||
            cameraPivot == null)
        {
            return;
        }

        cameraPivot.position =
            GetTargetPosition();

        cameraPivot.rotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f);

        currentDistance =
            followDistance;

        ApplyCameraPosition(
            currentDistance);
    }

    private Vector3 GetTargetPosition()
    {
        if (target == null)
            return transform.position;

        return
            target.position +
            target.up *
            targetHeight +
            target.TransformDirection(
                activeOffset);
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

        float positionBlend =
            CalculateExponentialBlend(
                followSmoothness,
                Time.unscaledDeltaTime);

        cameraPivot.position =
            Vector3.Lerp(
                cameraPivot.position,
                desiredPosition,
                positionBlend);

        Quaternion desiredRotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f);

        float rotationBlend =
            CalculateExponentialBlend(
                rotationSmoothness,
                Time.unscaledDeltaTime);

        cameraPivot.rotation =
            Quaternion.Slerp(
                cameraPivot.rotation,
                desiredRotation,
                rotationBlend);
    }

    private void UpdateCameraDistance()
    {
        if (cameraPivot == null ||
            playerCamera == null)
        {
            return;
        }

        float desiredDistance =
            ResolveCollisionDistance();

        float distanceBlend =
            CalculateExponentialBlend(
                collisionSmoothness,
                Time.unscaledDeltaTime);

        currentDistance =
            Mathf.Lerp(
                currentDistance,
                desiredDistance,
                distanceBlend);

        ApplyCameraPosition(
            currentDistance);
    }

    private void ApplyCameraPosition(
        float distance)
    {
        if (playerCamera == null)
            return;

        playerCamera.transform.localPosition =
            new Vector3(
                0f,
                0f,
                -Mathf.Max(
                    0f,
                    distance));

        playerCamera.transform.localRotation =
            Quaternion.identity;
    }

    #endregion

    #region Collision

    private float ResolveCollisionDistance()
    {
        if (!useCollision ||
            cameraPivot == null ||
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

    #endregion

    #region Input

    private void ReadRotationInput()
    {
        float horizontalInput =
            Input.GetAxis(
                MouseXAxis);

        float verticalInput =
            Input.GetAxis(
                MouseYAxis);

        yaw +=
            horizontalInput *
            horizontalSensitivity;

        float verticalDirection =
            invertVerticalInput
                ? 1f
                : -1f;

        pitch +=
            verticalInput *
            verticalSensitivity *
            verticalDirection;

        pitch =
            Mathf.Clamp(
                pitch,
                minimumPitch,
                maximumPitch);
    }

    #endregion

    #region Validation

    private bool ValidateConfiguration()
    {
        bool valid = true;

        valid &=
            ValidateReference(
                target,
                "Target");

        valid &=
            ValidateReference(
                playerCamera,
                nameof(Camera));

        valid &=
            ValidateReference(
                cameraPivot,
                "Camera Pivot");

        return valid;
    }

    private bool EnsureRuntimeReferences()
    {
        if (target != null &&
            playerCamera != null &&
            cameraPivot != null)
        {
            return true;
        }

        CacheComponents();
        ResolveReferences();
        ConfigureComponents();

        return
            target != null &&
            playerCamera != null &&
            cameraPivot != null;
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

        Cursor.visible =
            !lockCursor;
    }

    private void RestoreCursorState()
    {
        if (!Application.isPlaying)
            return;

        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible =
            true;
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

        isInitialized =
            false;

        if (ownsCameraPivot &&
            cameraPivot != null)
        {
            if (Application.isPlaying)
            {
                Destroy(
                    cameraPivot.gameObject);
            }
            else
            {
                DestroyImmediate(
                    cameraPivot.gameObject);
            }
        }

        ownsCameraPivot =
            false;

        target =
            null;

        cameraPivot =
            null;

        playerCamera =
            null;
    }

    #endregion

    #region Utilities

    private static float NormalizeAngle(
        float angle)
    {
        while (angle > 180f)
        {
            angle -=
                360f;
        }

        while (angle < -180f)
        {
            angle +=
                360f;
        }

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

    private void LogStateChange(
        string message)
    {
        if (!logStateChanges)
            return;

        Debug.Log(
            message,
            this);
    }

    #endregion
}