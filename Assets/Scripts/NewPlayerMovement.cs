using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class NewPlayerMovement : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Transform cameraTransform;

    [Header("Input")]
    [SerializeField] private bool inputEnabled = true;
    [SerializeField] private string horizontalAxis = "Horizontal";
    [SerializeField] private string verticalAxis = "Vertical";
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;

    [Header("Ground Movement")]
    [SerializeField, Min(0f)] private float walkSpeed = 5f;
    [SerializeField, Min(0f)] private float runSpeed = 12f;
    [SerializeField, Min(0f)] private float acceleration = 30f;
    [SerializeField, Min(0f)] private float deceleration = 40f;
    [SerializeField, Min(0f)] private float directionChangeAcceleration = 55f;

    [Header("Super Movement")]
    [SerializeField, Min(0f)] private float superRunSpeed = 18f;
    [SerializeField, Min(0f)] private float superAcceleration = 42f;

    [Header("Air Movement")]
    [SerializeField, Min(0f)] private float airAcceleration = 12f;
    [SerializeField, Range(0f, 1f)] private float airControl = 0.65f;
    [SerializeField, Min(0f)] private float maximumFallSpeed = 45f;

    [Header("Jump")]
    [SerializeField, Min(0f)] private float jumpHeight = 3f;
    [SerializeField, Min(0f)] private float coyoteTime = 0.12f;
    [SerializeField, Min(0f)] private float jumpBufferTime = 0.12f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -30f;
    [SerializeField] private float groundedVerticalVelocity = -2f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField]
    private Vector3 groundCheckOffset =
        new(0f, 0.1f, 0f);

    [SerializeField, Min(0.01f)] private float groundCheckRadius = 0.25f;

    [Header("Rotation")]
    [SerializeField, Min(0f)] private float rotationSpeed = 720f;
    [SerializeField, Min(0f)] private float minimumRotationSpeed = 0.1f;

    private Vector2 movementInput;
    private Vector2 forcedInput;

    private Vector3 horizontalVelocity;
    private float verticalVelocity;

    private Vector3 boostDirection;
    private float boostSpeed;
    private float boostTimeRemaining;

    private float forcedInputTimeRemaining;
    private float coyoteTimeRemaining;
    private float jumpBufferRemaining;

    private bool isGrounded;
    private bool wasGrounded;
    private bool isSuper;
    private bool isInitialized;

    public event Action Jumped;
    public event Action Landed;
    public event Action<bool> GroundedChanged;
    public event Action<bool> SuperStateChanged;

    public bool IsGrounded => isGrounded;

    public bool IsAirborne => !isGrounded;

    public bool IsSuper => isSuper;

    public bool InputEnabled => inputEnabled;

    public bool IsBoosting => boostTimeRemaining > 0f;

    public bool HasForcedInput => forcedInputTimeRemaining > 0f;

    public Vector3 Velocity =>
        horizontalVelocity +
        Vector3.up * verticalVelocity;

    public Vector3 HorizontalVelocity =>
        horizontalVelocity;

    public float VerticalVelocity =>
        verticalVelocity;

    public float CurrentSpeed =>
        horizontalVelocity.magnitude;

    public float CurrentMaximumSpeed =>
        isSuper
            ? superRunSpeed
            : runSpeed;

    public float CurrentAcceleration =>
        isSuper
            ? superAcceleration
            : acceleration;

    private void Awake()
    {
        ResolveReferences();

        isInitialized =
            ValidateConfiguration();

        if (!isInitialized)
            enabled = false;
    }

    private void Update()
    {
        float deltaTime =
            Time.deltaTime;

        UpdateGroundState();
        UpdateTimers(deltaTime);
        ReadInput();
        UpdateJumpBuffer();

        if (IsBoosting)
            UpdateBoostVelocity();
        else
            UpdateHorizontalVelocity(deltaTime);

        UpdateVerticalVelocity(deltaTime);
        ApplyMovement(deltaTime);
        RotateTowardsMovement(deltaTime);
    }

    public void SetInputEnabled(
        bool enabledState)
    {
        inputEnabled =
            enabledState;

        if (!inputEnabled)
            movementInput = Vector2.zero;
    }

    public void SetSuperState(
        bool enabledState)
    {
        if (isSuper == enabledState)
            return;

        isSuper =
            enabledState;

        SuperStateChanged?.Invoke(
            isSuper);
    }

    public void ToggleSuperState()
    {
        SetSuperState(
            !isSuper);
    }

    public void ApplyBoost(
        Vector3 direction,
        float speed,
        float duration)
    {
        if (direction.sqrMagnitude <=
            Mathf.Epsilon)
        {
            Debug.LogWarning(
                "NewPlayerMovement received a boost with no direction.",
                this);

            return;
        }

        boostDirection =
            Vector3.ProjectOnPlane(
                direction.normalized,
                Vector3.up).normalized;

        boostSpeed =
            Mathf.Max(0f, speed);

        boostTimeRemaining =
            Mathf.Max(0f, duration);

        horizontalVelocity =
            boostDirection * boostSpeed;

        if (boostDirection.sqrMagnitude >
            Mathf.Epsilon)
        {
            transform.rotation =
                Quaternion.LookRotation(
                    boostDirection,
                    Vector3.up);
        }
    }

    public void Launch(
        Vector3 launchVelocity)
    {
        horizontalVelocity =
            Vector3.ProjectOnPlane(
                launchVelocity,
                Vector3.up);

        verticalVelocity =
            launchVelocity.y;

        coyoteTimeRemaining = 0f;
        jumpBufferRemaining = 0f;
    }

    public void Launch(
        Vector3 direction,
        float speed)
    {
        if (direction.sqrMagnitude <=
            Mathf.Epsilon)
        {
            return;
        }

        Launch(
            direction.normalized *
            Mathf.Max(0f, speed));
    }

    public void SuspendControl(
        Vector2 direction,
        float duration)
    {
        forcedInput =
            Vector2.ClampMagnitude(
                direction,
                1f);

        forcedInputTimeRemaining =
            Mathf.Max(0f, duration);
    }

    public void CancelForcedMovement()
    {
        forcedInput = Vector2.zero;
        forcedInputTimeRemaining = 0f;

        boostDirection = Vector3.zero;
        boostSpeed = 0f;
        boostTimeRemaining = 0f;
    }

    public void StopImmediately()
    {
        horizontalVelocity =
            Vector3.zero;

        boostDirection =
            Vector3.zero;

        boostSpeed = 0f;
        boostTimeRemaining = 0f;
    }

    public MovementState CaptureState()
    {
        return new MovementState
        {
            Position = transform.position,
            Rotation = transform.rotation,
            HorizontalVelocity = horizontalVelocity,
            VerticalVelocity = verticalVelocity,
            IsSuper = isSuper,
            InputEnabled = inputEnabled
        };
    }

    public void RestoreState(
        MovementState state)
    {
        bool controllerWasEnabled =
            characterController.enabled;

        characterController.enabled =
            false;

        transform.SetPositionAndRotation(
            state.Position,
            state.Rotation);

        characterController.enabled =
            controllerWasEnabled;

        horizontalVelocity =
            state.HorizontalVelocity;

        verticalVelocity =
            state.VerticalVelocity;

        inputEnabled =
            state.InputEnabled;

        SetSuperState(
            state.IsSuper);

        CancelForcedMovement();
        UpdateGroundState();
    }

    private void ResolveReferences()
    {
        if (characterController == null)
        {
            characterController =
                GetComponent<CharacterController>();
        }

        if (cameraTransform == null &&
            Camera.main != null)
        {
            cameraTransform =
                Camera.main.transform;
        }
    }

    private void ReadInput()
    {
        if (HasForcedInput)
        {
            movementInput =
                forcedInput;

            return;
        }

        if (!inputEnabled)
        {
            movementInput =
                Vector2.zero;

            return;
        }

        movementInput =
            Vector2.ClampMagnitude(
                new Vector2(
                    Input.GetAxisRaw(horizontalAxis),
                    Input.GetAxisRaw(verticalAxis)),
                1f);
    }

    private void UpdateJumpBuffer()
    {
        if (inputEnabled &&
            Input.GetKeyDown(jumpKey))
        {
            jumpBufferRemaining =
                jumpBufferTime;
        }

        if (jumpBufferRemaining <= 0f)
            return;

        if (coyoteTimeRemaining <= 0f)
            return;

        PerformJump();
    }

    private void PerformJump()
    {
        float effectiveGravity =
            Mathf.Abs(gravity);

        verticalVelocity =
            Mathf.Sqrt(
                jumpHeight *
                2f *
                effectiveGravity);

        isGrounded = false;
        coyoteTimeRemaining = 0f;
        jumpBufferRemaining = 0f;

        Jumped?.Invoke();
        GroundedChanged?.Invoke(false);
    }

    private void UpdateHorizontalVelocity(
        float deltaTime)
    {
        Vector3 desiredDirection =
            GetCameraRelativeDirection(
                movementInput);

        float desiredSpeed =
            GetDesiredSpeed(
                movementInput.magnitude);

        Vector3 desiredVelocity =
            desiredDirection *
            desiredSpeed;

        if (!isGrounded)
        {
            desiredVelocity *=
                airControl;

            horizontalVelocity =
                Vector3.MoveTowards(
                    horizontalVelocity,
                    desiredVelocity,
                    airAcceleration *
                    deltaTime);

            return;
        }

        float rate =
            GetGroundVelocityChangeRate(
                desiredVelocity);

        horizontalVelocity =
            Vector3.MoveTowards(
                horizontalVelocity,
                desiredVelocity,
                rate * deltaTime);
    }

    private float GetGroundVelocityChangeRate(
        Vector3 desiredVelocity)
    {
        if (desiredVelocity.sqrMagnitude <=
            Mathf.Epsilon)
        {
            return deceleration;
        }

        if (horizontalVelocity.sqrMagnitude <=
            Mathf.Epsilon)
        {
            return CurrentAcceleration;
        }

        float directionAlignment =
            Vector3.Dot(
                horizontalVelocity.normalized,
                desiredVelocity.normalized);

        return directionAlignment < 0f
            ? directionChangeAcceleration
            : CurrentAcceleration;
    }

    private float GetDesiredSpeed(
        float inputMagnitude)
    {
        if (inputMagnitude <=
            Mathf.Epsilon)
        {
            return 0f;
        }

        float maximumSpeed =
            CurrentMaximumSpeed;

        return Mathf.Lerp(
            walkSpeed,
            maximumSpeed,
            inputMagnitude);
    }

    private Vector3 GetCameraRelativeDirection(
        Vector2 input)
    {
        if (input.sqrMagnitude <=
            Mathf.Epsilon)
        {
            return Vector3.zero;
        }

        Vector3 forward =
            cameraTransform != null
                ? cameraTransform.forward
                : transform.forward;

        Vector3 right =
            cameraTransform != null
                ? cameraTransform.right
                : transform.right;

        forward =
            Vector3.ProjectOnPlane(
                forward,
                Vector3.up).normalized;

        right =
            Vector3.ProjectOnPlane(
                right,
                Vector3.up).normalized;

        Vector3 direction =
            forward * input.y +
            right * input.x;

        return direction.sqrMagnitude >
               Mathf.Epsilon
            ? direction.normalized
            : Vector3.zero;
    }

    private void UpdateBoostVelocity()
    {
        horizontalVelocity =
            boostDirection *
            boostSpeed;
    }

    private void UpdateVerticalVelocity(
        float deltaTime)
    {
        if (isGrounded &&
            verticalVelocity < 0f)
        {
            verticalVelocity =
                groundedVerticalVelocity;

            return;
        }

        verticalVelocity +=
            gravity * deltaTime;

        verticalVelocity =
            Mathf.Max(
                verticalVelocity,
                -maximumFallSpeed);
    }

    private void ApplyMovement(
        float deltaTime)
    {
        Vector3 totalVelocity =
            horizontalVelocity +
            Vector3.up *
            verticalVelocity;

        characterController.Move(
            totalVelocity *
            deltaTime);
    }

    private void RotateTowardsMovement(
        float deltaTime)
    {
        Vector3 direction =
            horizontalVelocity;

        direction.y = 0f;

        if (direction.sqrMagnitude <
            minimumRotationSpeed *
            minimumRotationSpeed)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);

        transform.rotation =
            Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed *
                deltaTime);
    }

    private void UpdateGroundState()
    {
        wasGrounded =
            isGrounded;

        Vector3 checkPosition =
            transform.TransformPoint(
                groundCheckOffset);

        bool sphereGrounded =
            Physics.CheckSphere(
                checkPosition,
                groundCheckRadius,
                groundMask,
                QueryTriggerInteraction.Ignore);

        isGrounded =
            characterController.isGrounded ||
            sphereGrounded;

        if (isGrounded)
        {
            coyoteTimeRemaining =
                coyoteTime;
        }

        if (isGrounded == wasGrounded)
            return;

        GroundedChanged?.Invoke(
            isGrounded);

        if (isGrounded)
            Landed?.Invoke();
    }

    private void UpdateTimers(
        float deltaTime)
    {
        coyoteTimeRemaining =
            Mathf.Max(
                0f,
                coyoteTimeRemaining -
                deltaTime);

        jumpBufferRemaining =
            Mathf.Max(
                0f,
                jumpBufferRemaining -
                deltaTime);

        forcedInputTimeRemaining =
            Mathf.Max(
                0f,
                forcedInputTimeRemaining -
                deltaTime);

        boostTimeRemaining =
            Mathf.Max(
                0f,
                boostTimeRemaining -
                deltaTime);

        if (forcedInputTimeRemaining <= 0f)
            forcedInput = Vector2.zero;

        if (boostTimeRemaining > 0f)
            return;

        boostDirection = Vector3.zero;
        boostSpeed = 0f;
    }

    private bool ValidateConfiguration()
    {
        bool valid = true;

        if (characterController == null)
        {
            Debug.LogError(
                "NewPlayerMovement requires a CharacterController.",
                this);

            valid = false;
        }

        if (cameraTransform == null)
        {
            Debug.LogWarning(
                "NewPlayerMovement has no camera assigned. " +
                "Movement will use the character's orientation.",
                this);
        }

        if (string.IsNullOrWhiteSpace(
                horizontalAxis))
        {
            Debug.LogError(
                "Horizontal input axis is empty.",
                this);

            valid = false;
        }

        if (string.IsNullOrWhiteSpace(
                verticalAxis))
        {
            Debug.LogError(
                "Vertical input axis is empty.",
                this);

            valid = false;
        }

        return valid;
    }

    private void OnValidate()
    {
        walkSpeed =
            Mathf.Max(0f, walkSpeed);

        runSpeed =
            Mathf.Max(walkSpeed, runSpeed);

        superRunSpeed =
            Mathf.Max(runSpeed, superRunSpeed);

        acceleration =
            Mathf.Max(0f, acceleration);

        superAcceleration =
            Mathf.Max(
                acceleration,
                superAcceleration);

        deceleration =
            Mathf.Max(0f, deceleration);

        directionChangeAcceleration =
            Mathf.Max(
                acceleration,
                directionChangeAcceleration);

        airAcceleration =
            Mathf.Max(0f, airAcceleration);

        maximumFallSpeed =
            Mathf.Max(0f, maximumFallSpeed);

        jumpHeight =
            Mathf.Max(0f, jumpHeight);

        coyoteTime =
            Mathf.Max(0f, coyoteTime);

        jumpBufferTime =
            Mathf.Max(0f, jumpBufferTime);

        groundCheckRadius =
            Mathf.Max(
                0.01f,
                groundCheckRadius);

        rotationSpeed =
            Mathf.Max(0f, rotationSpeed);

        minimumRotationSpeed =
            Mathf.Max(
                0f,
                minimumRotationSpeed);

        if (gravity >= 0f)
            gravity = -30f;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 checkPosition =
            transform.TransformPoint(
                groundCheckOffset);

        Gizmos.DrawWireSphere(
            checkPosition,
            groundCheckRadius);
    }
}

[Serializable]
public struct MovementState
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 HorizontalVelocity;
    public float VerticalVelocity;
    public bool IsSuper;
    public bool InputEnabled;
}