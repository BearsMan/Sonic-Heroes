using UnityEngine;

[DisallowMultipleComponent]
public class CameraController : MonoBehaviour
{
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
    [SerializeField] private Vector3 speedOffset;
    [SerializeField] private Vector3 flyOffset;
    [SerializeField] private Vector3 powerOffset;

    [SerializeField] private float rotationSmoothness = 15f;


    [Header("Cursor")]
    [SerializeField] private bool lockCursor = true;

    private float yaw;
    private float pitch;
    private float currentDistance;

    private bool isInitialized;

    public Transform Target => target;
    public Transform CameraPivot => cameraPivot;
    public Camera PlayerCamera => playerCamera;

    private void Awake()
    {
        FindReferences();

        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        InitializeCamera();
    }

    private void OnEnable()
    {
        ApplyCursorState();
    }

    private void OnDisable()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
    private void LateUpdate()
    {
        if (!isInitialized)
        {
            return;
        }

        ReadRotationInput();
        UpdatePivot();
        UpdateCameraDistance();
    }

    private void FindReferences()
    {
        if (target == null)
        {
            UltimatePlayerMovement movement =
                GetComponent<UltimatePlayerMovement>();

            if (movement == null)
            {
                movement =
                    GetComponentInParent<UltimatePlayerMovement>();
            }

            if (movement != null)
            {
                target = movement.transform;
            }
        }

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>(true);
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (cameraPivot == null && playerCamera != null)
        {
            Transform cameraParent = playerCamera.transform.parent;

            if (cameraParent != null &&
                cameraParent != target)
            {
                cameraPivot = cameraParent;
            }
        }

        if (cameraPivot == null)
        {
            CreateCameraPivot();
        }
    }

    private void CreateCameraPivot()
    {
        if (target == null || playerCamera == null)
        {
            return;
        }

        GameObject pivotObject =
            new GameObject("Camera Pivot");

        cameraPivot = pivotObject.transform;

        cameraPivot.SetParent(target, false);
        cameraPivot.localPosition =
            Vector3.up * targetHeight;

        playerCamera.transform.SetParent(
            cameraPivot,
            true);
    }

    private bool HasRequiredReferences()
    {
        bool valid = true;

        if (target == null)
        {
            Debug.LogError(
                "CameraController could not find a target.",
                this);

            valid = false;
        }

        if (cameraPivot == null)
        {
            Debug.LogError(
                "CameraController could not find or create a camera pivot.",
                this);

            valid = false;
        }

        if (playerCamera == null)
        {
            Debug.LogError(
                "CameraController could not find a Camera.",
                this);

            valid = false;
        }

        return valid;
    }
    private void InitializeCamera()
    {
        Vector3 startingEuler =
            cameraPivot.rotation.eulerAngles;

        yaw = startingEuler.y;
        pitch = NormalizeAngle(startingEuler.x);

        pitch = Mathf.Clamp(
            pitch,
            minimumPitch,
            maximumPitch);

        currentDistance = followDistance;

        cameraPivot.position =
            GetTargetPosition();

        cameraPivot.rotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f);

        playerCamera.transform.localRotation =
            Quaternion.identity;

        ApplyCameraPosition(currentDistance);

        ApplyCursorState();

        isInitialized = true;
    }

    private void ReadRotationInput()
    {
        float horizontalInput =
            Input.GetAxis("Mouse X");

        float verticalInput =
            Input.GetAxis("Mouse Y");

        yaw += horizontalInput *
               horizontalSensitivity;

        float verticalDirection =
            invertVerticalInput ? 1f : -1f;

        pitch += verticalInput *
                 verticalSensitivity *
                 verticalDirection;

        pitch = Mathf.Clamp(
            pitch,
            minimumPitch,
            maximumPitch);
    }

    private void UpdatePivot()
    {
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
                1f - Mathf.Exp(
                    -followSmoothness *
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

        float rotationBlend =
            1f - Mathf.Exp(
                -rotationSmoothness *
                Time.unscaledDeltaTime);

        cameraPivot.rotation =
            Quaternion.Slerp(
                cameraPivot.rotation,
                targetRotation,
                rotationBlend);
    }
    private void UpdateCameraDistance()
    {
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
                1f - Mathf.Exp(
                    -collisionSmoothness *
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
        if (!useCollision ||
            collisionMask.value == 0)
        {
            return followDistance;
        }

        Vector3 castOrigin =
            cameraPivot.position;

        Vector3 castDirection =
            -cameraPivot.forward;

        if (Physics.SphereCast(
            castOrigin,
            collisionRadius,
            castDirection,
            out RaycastHit hit,
            followDistance,
            collisionMask,
            QueryTriggerInteraction.Ignore))
        {
            float safeDistance =
                hit.distance -
                collisionPadding;

            return Mathf.Clamp(
                safeDistance,
                0f,
                followDistance);
        }

        return followDistance;
    }

    private void ApplyCameraPosition(
        float distance)
    {
        playerCamera.transform.localPosition =
            new Vector3(
                0f,
                0f,
                -distance);

        playerCamera.transform.localRotation =
            Quaternion.identity;
    }

    private Vector3 GetTargetPosition()
    {
        return target.position +
       target.up * targetHeight;
    }

    private void ApplyCursorState()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Cursor.lockState =
            lockCursor
                ? CursorLockMode.Locked
                : CursorLockMode.None;

        Cursor.visible = !lockCursor;
    }

    private static float NormalizeAngle(
        float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }

        while (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
    }

    private void OnValidate()
    {
        maximumPitch =
            Mathf.Max(
                maximumPitch,
                minimumPitch);

        followDistance =
            Mathf.Max(
                0.1f,
                followDistance);

        collisionRadius =
            Mathf.Max(
                0.01f,
                collisionRadius);
    }

    private void OnDrawGizmosSelected()
    {
        if (target == null)
        {
            return;
        }

        Vector3 targetPosition =
            target.position +
            target.up * targetHeight;

        Gizmos.DrawWireSphere(
            targetPosition,
            0.15f);

        if (cameraPivot != null)
        {
            Gizmos.DrawLine(
                targetPosition,
                cameraPivot.position -
                cameraPivot.forward *
                followDistance);
        }
    }
}