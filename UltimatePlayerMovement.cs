using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class UltimatePlayerMovement : MonoBehaviour
{
    #region Types

    public enum MovementState
    {
        Ground,
        Air,
        Rolling,
        HomingAttack,
        Grinding,
        Spring,
        Hurt,
        Flying,
        PowerAction
    }

    #endregion

    #region Constants

    private const string GroundProbeName = "GroundCheck";
    private const int HomingTargetCapacity = 32;

    private static readonly int StateHash =
        Animator.StringToHash("State");

    private static readonly int GroundedHash =
        Animator.StringToHash("Grounded");

    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private static readonly int VerticalSpeedHash =
        Animator.StringToHash("VerticalSpeed");

    #endregion

    #region Inspector

    [Header("Character")]
    [SerializeField] private CharacterDefinition characterDefinition;

    [Header("Dependencies")]
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform groundProbe;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField, Min(0.01f)] private float groundProbeRadius = 0.3f;
    [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.65f;

    [Header("Input")]
    [SerializeField] private bool acceptPlayerInput = true;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode rollKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode homingAttackKey = KeyCode.Mouse0;
    [SerializeField] private KeyCode flyDownKey = KeyCode.LeftControl;

    [Header("Homing Targeting")]
    [SerializeField] private LayerMask homingTargetMask;

    [Header("State")]
    [SerializeField]
    private MovementState startingState =
        MovementState.Ground;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Profile Values

    private float runSpeed;
    private float acceleration;
    private float deceleration;
    private float turnSpeed;
    private float brakingForce;

    private float jumpForce;
    private float coyoteTime;
    private float jumpBufferTime;

    private float airSpeed;
    private float airControl;

    private float slopeAcceleration;
    private float maximumSlopeAngle;

    private float rollingSpeed;
    private float rollingTurnSpeed;

    private float homingAttackSpeed;
    private float homingAttackDuration;
    private float homingRange;

    private float flyingSpeed;
    private float flyingVerticalSpeed;

    private float defaultHurtDuration;

    #endregion

    #region Runtime State

    private readonly Collider[] homingTargets =
        new Collider[HomingTargetCapacity];

    private readonly HashSet<int> animatorParameters =
        new();

    private MovementState currentState;

    private Vector3 groundNormal =
        Vector3.up;

    private Vector3 homingDirection;

    private float groundSpeed;
    private float currentSlopeAngle;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float stateTimer;

    private bool grounded;
    private bool groundedLastFrame;
    private bool movementEnabled = true;
    private bool initialized;
    private bool shuttingDown;
    private bool isInTrickZone;

    #endregion

    #region Public API

    public event Action<MovementState> StateChanged;

    public CharacterDefinition CharacterDefinition =>
        characterDefinition;

    public CharacterMovementProfile MovementProfile =>
        characterDefinition != null
            ? characterDefinition.movementProfile
            : null;

    public CharacterAbilityProfile AbilityProfile =>
        characterDefinition != null
            ? characterDefinition.abilityProfile
            : null;

    public MovementState CurrentState =>
        currentState;

    public bool IsGrounded =>
        grounded;

    public bool IsInTrickZone =>
    isInTrickZone;

    public bool IsInitialized =>
        initialized;

    public bool MovementEnabled =>
        movementEnabled;

    public Vector3 Velocity =>
        playerRigidbody != null
            ? playerRigidbody.linearVelocity
            : Vector3.zero;

    public bool SetCharacterDefinition(
        CharacterDefinition definition)
    {
        if (definition == null ||
            !definition.IsValid())
        {
            Debug.LogError(
                "UltimatePlayerMovement received an invalid CharacterDefinition.",
                this);

            return false;
        }

        characterDefinition =
            definition;

        ApplyCharacterDefinition();
        SetupAnimation();

        return true;
    }

    public void SetTrickZoneActive(
    bool active)
    {
        isInTrickZone = active;
    }

    public void EnableMovement()
    {
        movementEnabled = true;
    }

    public void DisableMovement()
    {
        movementEnabled = false;
        StopMovement();
    }

    public void SetInputEnabled(
        bool enabled)
    {
        acceptPlayerInput = enabled;
    }

    public void StopMovement()
    {
        if (playerRigidbody == null)
            return;

        playerRigidbody.linearVelocity =
            Vector3.zero;

        playerRigidbody.angularVelocity =
            Vector3.zero;

        groundSpeed = 0f;
    }

    public void EnterGrindingState()
    {
        if (!initialized ||
            !CanUseAbility(
                profile => profile.canGrind))
        {
            return;
        }

        ChangeState(
            MovementState.Grinding);
    }

    public void ExitGrindingState()
    {
        if (currentState !=
            MovementState.Grinding)
        {
            return;
        }

        ChangeState(
            grounded
                ? MovementState.Ground
                : MovementState.Air);
    }

    public void LaunchFromSpring(
        Vector3 launchVelocity)
    {
        if (!initialized ||
            playerRigidbody == null)
        {
            return;
        }

        playerRigidbody.linearVelocity =
            launchVelocity;

        grounded = false;

        ChangeState(
            MovementState.Spring);
    }

    public void EnterHurtState(
        Vector3 knockbackVelocity,
        float duration = -1f)
    {
        if (!initialized ||
            playerRigidbody == null)
        {
            return;
        }

        playerRigidbody.linearVelocity =
            knockbackVelocity;

        stateTimer =
            duration >= 0f
                ? duration
                : defaultHurtDuration;

        ChangeState(
            MovementState.Hurt);
    }

    public bool StartFlying()
    {
        if (!initialized ||
            !CanUseAbility(
                profile => profile.canFly))
        {
            return false;
        }

        ChangeState(
            MovementState.Flying);

        return true;
    }

    public void StopFlying()
    {
        if (currentState !=
            MovementState.Flying)
        {
            return;
        }

        ChangeState(
            grounded
                ? MovementState.Ground
                : MovementState.Air);
    }

    public bool StartPowerAction()
    {
        if (!initialized ||
            !CanUseAbility(
                profile => profile.canPowerAction))
        {
            return false;
        }

        ChangeState(
            MovementState.PowerAction);

        return true;
    }

    public void StopPowerAction()
    {
        if (currentState !=
            MovementState.PowerAction)
        {
            return;
        }

        ChangeState(
            grounded
                ? MovementState.Ground
                : MovementState.Air);
    }

    public void SetupAnimation()
    {
        ResolveAnimator();
        ApplyAnimatorProfile();
        CacheAnimatorParameters();
        UpdateAnimator();
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveDependencies();
        ConfigureRigidbody();
        ApplyCharacterDefinition();
    }

    private void Start()
    {
        if (!InitializeMovement())
        {
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (shuttingDown)
            return;

        ResolveDependencies();
        ConfigureRigidbody();

        if (initialized)
        {
            SetupAnimation();
        }
    }

    private void Update()
    {
        if (!initialized)
            return;

        UpdateInputTimers();
        UpdateAnimator();

        if (!CanProcessInput())
            return;

        HandleJumpInput();
        HandleRollingInput();
        HandleHomingAttackInput();
    }

    private void FixedUpdate()
    {
        if (!initialized)
            return;

        UpdateGrounding();

        if (!movementEnabled)
            return;

        Vector3 movementInput =
            ReadMovementInput();

        switch (currentState)
        {
            case MovementState.Ground:
                UpdateGroundMovement(
                    movementInput);
                break;

            case MovementState.Air:
                UpdateAirMovement(
                    movementInput);
                break;

            case MovementState.Rolling:
                UpdateRollingMovement(
                    movementInput);
                break;

            case MovementState.HomingAttack:
                UpdateHomingAttack();
                break;

            case MovementState.Grinding:
                break;

            case MovementState.Spring:
                UpdateSpringMovement(
                    movementInput);
                break;

            case MovementState.Hurt:
                UpdateHurtMovement();
                break;

            case MovementState.Flying:
                UpdateFlyingMovement(
                    movementInput);
                break;

            case MovementState.PowerAction:
                UpdatePowerActionMovement(
                    movementInput);
                break;
        }
    }

    private void OnDisable()
    {
        StopMovement();
    }

    private void OnDestroy()
    {
        shuttingDown = true;

        StopMovement();

        StateChanged = null;

        animatorParameters.Clear();

        playerRigidbody = null;
        playerAnimator = null;
        cameraTransform = null;
        groundProbe = null;
        characterDefinition = null;
    }

    private void OnValidate()
    {
        groundProbeRadius =
            Mathf.Max(
                0.01f,
                groundProbeRadius);

        groundProbeDistance =
            Mathf.Max(
                0.01f,
                groundProbeDistance);

        if (!Enum.IsDefined(
                typeof(MovementState),
                startingState))
        {
            startingState =
                MovementState.Ground;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform probe =
            groundProbe != null
                ? groundProbe
                : transform;

        Gizmos.DrawWireSphere(
            probe.position,
            groundProbeRadius);

        Gizmos.DrawLine(
            probe.position,
            probe.position -
            transform.up *
            groundProbeDistance);
    }

    #endregion

    #region Initialization

    public bool InitializeMovement()
    {
        if (initialized)
            return true;

        ResolveDependencies();
        ConfigureRigidbody();
        ApplyCharacterDefinition();

        if (!ValidateConfiguration())
        {
            Debug.LogError(
                $"UltimatePlayerMovement failed to initialize on '{name}'.",
                this);

            initialized = false;
            return false;
        }

        ResetRuntimeState();
        SetupAnimation();

        grounded =
            DetectGround(
                out groundNormal,
                out currentSlopeAngle);

        groundedLastFrame =
            grounded;

        currentState =
            grounded
                ? MovementState.Ground
                : startingState;

        if (currentState ==
            MovementState.Ground &&
            !grounded)
        {
            currentState =
                MovementState.Air;
        }

        initialized = true;

        UpdateAnimatorState();
        UpdateAnimator();

        return true;
    }

    private void ResolveDependencies()
    {
        if (playerRigidbody == null)
        {
            playerRigidbody =
                GetComponent<Rigidbody>();
        }

        if (playerRigidbody == null)
        {
            playerRigidbody =
                GetComponentInParent<Rigidbody>();
        }

        if (playerRigidbody == null)
        {
            playerRigidbody =
                GetComponentInChildren<Rigidbody>(
                    includeInactive: true);
        }

        ResolveAnimator();
        ResolveCamera();
        ResolveGroundProbe();
    }

    private void ResolveAnimator()
    {
        if (playerAnimator != null)
            return;

        playerAnimator =
            GetComponent<Animator>();

        if (playerAnimator == null)
        {
            playerAnimator =
                GetComponentInChildren<Animator>(
                    includeInactive: true);
        }

        if (playerAnimator == null)
        {
            playerAnimator =
                GetComponentInParent<Animator>();
        }
    }

    private void ResolveCamera()
    {
        if (cameraTransform != null)
            return;

        if (Camera.main != null)
        {
            cameraTransform =
                Camera.main.transform;

            return;
        }

        Camera camera =
            FindAnyObjectByType<Camera>();

        if (camera != null)
        {
            cameraTransform =
                camera.transform;
        }
    }
    private void ResolveGroundProbe()
    {
        if (groundProbe != null)
            return;

        Transform[] children =
            GetComponentsInChildren<Transform>(
                includeInactive: true);

        foreach (Transform child in children)
        {
            if (child != null &&
                child.name == GroundProbeName)
            {
                groundProbe = child;
                return;
            }
        }

        Transform root =
            transform.root;

        if (root == transform)
            return;

        Transform[] rootChildren =
            root.GetComponentsInChildren<Transform>(
                includeInactive: true);

        foreach (Transform child in rootChildren)
        {
            if (child != null &&
                child.name == GroundProbeName)
            {
                groundProbe = child;
                return;
            }
        }
    }

    private void ConfigureRigidbody()
    {
        if (playerRigidbody == null)
            return;

        playerRigidbody.constraints =
            RigidbodyConstraints.FreezeRotation;

        playerRigidbody.interpolation =
            RigidbodyInterpolation.Interpolate;
    }

    private void ResetRuntimeState()
    {
        homingDirection =
            Vector3.zero;

        groundNormal =
            Vector3.up;

        groundSpeed = 0f;
        currentSlopeAngle = 0f;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        stateTimer = 0f;

        movementEnabled = true;
        isInTrickZone = false;
    }

    #endregion

    #region Character Profiles

    private void ApplyCharacterDefinition()
    {
        if (characterDefinition == null)
            return;

        ApplyMovementProfile(
            characterDefinition.movementProfile);

        ApplyAnimatorProfile();
    }

    private void ApplyMovementProfile(
        CharacterMovementProfile profile)
    {
        if (profile == null)
            return;

        runSpeed =
            profile.runSpeed;

        acceleration =
            profile.acceleration;

        deceleration =
            profile.deceleration;

        turnSpeed =
            profile.turnSpeed;

        brakingForce =
            profile.brakingForce;

        jumpForce =
            profile.jumpForce;

        coyoteTime =
            profile.coyoteTime;

        jumpBufferTime =
            profile.jumpBufferTime;

        airSpeed =
            profile.airSpeed;

        airControl =
            profile.airControl;

        slopeAcceleration =
            profile.slopeAcceleration;

        maximumSlopeAngle =
            profile.maximumSlopeAngle;

        rollingSpeed =
            profile.rollingSpeed;

        rollingTurnSpeed =
            profile.rollingTurnSpeed;

        homingAttackSpeed =
            profile.homingAttackSpeed;

        homingAttackDuration =
            profile.homingAttackDuration;

        homingRange =
            profile.homingRange;

        flyingSpeed =
            profile.flyingSpeed;

        flyingVerticalSpeed =
            profile.flyingVerticalSpeed;

        defaultHurtDuration =
            profile.defaultHurtDuration;
    }

    private void ApplyAnimatorProfile()
    {
        if (characterDefinition == null ||
            characterDefinition.animatorProfile == null ||
            playerAnimator == null)
        {
            return;
        }

        CharacterAnimatorProfile profile =
            characterDefinition.animatorProfile;

        if (profile.animatorController != null)
        {
            playerAnimator.runtimeAnimatorController =
                profile.animatorController;
        }

        if (profile.avatar != null)
        {
            playerAnimator.avatar =
                profile.avatar;
        }
    }

    private bool CanUseAbility(
        Func<CharacterAbilityProfile, bool> selector)
    {
        CharacterAbilityProfile profile =
            AbilityProfile;

        return
            profile != null &&
            selector(profile);
    }

    #endregion

    #region Input

    private bool CanProcessInput()
    {
        return
            movementEnabled &&
            acceptPlayerInput &&
            currentState != MovementState.Hurt &&
            currentState != MovementState.Grinding;
    }

    private Vector3 ReadMovementInput()
    {
        float horizontal =
            Input.GetAxisRaw("Horizontal");

        float vertical =
            Input.GetAxisRaw("Vertical");

        Vector3 forward =
            cameraTransform != null
                ? cameraTransform.forward
                : transform.forward;

        Vector3 right =
            cameraTransform != null
                ? cameraTransform.right
                : transform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 input =
            forward * vertical +
            right * horizontal;

        return
            Vector3.ClampMagnitude(
                input,
                1f);
    }

    private void UpdateInputTimers()
    {
        if (acceptPlayerInput &&
            Input.GetKeyDown(jumpKey))
        {
            jumpBufferTimer =
                jumpBufferTime;
        }
        else
        {
            jumpBufferTimer =
                Mathf.Max(
                    0f,
                    jumpBufferTimer -
                    Time.deltaTime);
        }

        if (grounded)
        {
            coyoteTimer =
                coyoteTime;
        }
        else
        {
            coyoteTimer =
                Mathf.Max(
                    0f,
                    coyoteTimer -
                    Time.deltaTime);
        }
    }

    private void HandleJumpInput()
    {
        if (!CanUseAbility(
                profile => profile.canJump) ||
            jumpBufferTimer <= 0f ||
            playerRigidbody == null)
        {
            return;
        }

        bool canJump =
            currentState ==
                MovementState.Ground &&
            (grounded ||
             coyoteTimer > 0f);

        if (!canJump)
            return;

        Vector3 velocity =
            playerRigidbody.linearVelocity;

        velocity.y = 0f;

        playerRigidbody.linearVelocity =
            velocity;

        playerRigidbody.AddForce(
            transform.up *
            jumpForce,
            ForceMode.VelocityChange);

        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        grounded = false;

        ChangeState(
            MovementState.Air);
    }

    private void HandleRollingInput()
    {
        if (!CanUseAbility(
                profile => profile.canRoll))
        {
            return;
        }

        if (Input.GetKeyDown(rollKey))
        {
            TryStartRolling();
        }

        if (Input.GetKeyUp(rollKey))
        {
            StopRolling();
        }
    }

    private void HandleHomingAttackInput()
    {
        if (!CanUseAbility(
                profile => profile.canHomingAttack) ||
            currentState != MovementState.Air ||
            !Input.GetKeyDown(homingAttackKey))
        {
            return;
        }

        Transform target =
            FindHomingTarget();

        if (target == null)
            return;

        StartHomingAttack(
            target.position);
    }

    #endregion

    #region Grounding

    private void UpdateGrounding()
    {
        groundedLastFrame =
            grounded;

        grounded =
            DetectGround(
                out groundNormal,
                out currentSlopeAngle);

        if (!groundedLastFrame &&
            grounded)
        {
            HandleLanding();
        }
        else if (groundedLastFrame &&
                 !grounded)
        {
            HandleLeavingGround();
        }
    }

    private bool DetectGround(
        out Vector3 normal,
        out float slopeAngle)
    {
        normal =
            Vector3.up;

        slopeAngle = 0f;

        if (groundProbe == null)
            return false;

        Vector3 origin =
            groundProbe.position +
            transform.up *
            0.05f;

        if (!Physics.SphereCast(
                origin,
                groundProbeRadius,
                -transform.up,
                out RaycastHit hit,
                groundProbeDistance,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        normal =
            hit.normal.normalized;

        slopeAngle =
            Vector3.Angle(
                normal,
                Vector3.up);

        return
            slopeAngle <=
            maximumSlopeAngle;
    }

    private void HandleLanding()
    {
        if (currentState ==
            MovementState.Grinding)
        {
            return;
        }

        homingDirection =
            Vector3.zero;

        stateTimer = 0f;

        ChangeState(
            MovementState.Ground);
    }

    private void HandleLeavingGround()
    {
        if (currentState ==
            MovementState.Ground)
        {
            ChangeState(
                MovementState.Air);
        }
    }

    #endregion

    #region State Machine

    private void ChangeState(
        MovementState nextState)
    {
        if (currentState ==
            nextState)
        {
            return;
        }

        MovementState previousState =
            currentState;

        ExitState(
            previousState);

        currentState =
            nextState;

        EnterState(
            currentState);

        UpdateAnimatorState();

        StateChanged?.Invoke(
            currentState);

        LogStateChange(
            $"{previousState} -> {currentState}");
    }

    private void EnterState(
        MovementState state)
    {
        switch (state)
        {
            case MovementState.HomingAttack:
                stateTimer =
                    homingAttackDuration;
                break;

            case MovementState.Hurt:
                if (stateTimer <= 0f)
                {
                    stateTimer =
                        defaultHurtDuration;
                }
                break;

            case MovementState.Ground:
                homingDirection =
                    Vector3.zero;
                break;
        }
    }

    private void ExitState(
        MovementState state)
    {
        if (state ==
            MovementState.HomingAttack)
        {
            homingDirection =
                Vector3.zero;
        }
    }

    #endregion

    #region Ground Movement

    private void UpdateGroundMovement(
        Vector3 input)
    {
        if (playerRigidbody == null)
            return;

        Vector3 planarInput =
            Vector3.ProjectOnPlane(
                input,
                groundNormal);

        bool hasInput =
            planarInput.sqrMagnitude >
            0.001f;

        if (hasInput)
        {
            planarInput.Normalize();
        }

        Vector3 currentVelocity =
            playerRigidbody.linearVelocity;

        Vector3 surfaceVelocity =
            Vector3.ProjectOnPlane(
                currentVelocity,
                groundNormal);

        if (groundSpeed <= 0f)
        {
            groundSpeed =
                surfaceVelocity.magnitude;
        }

        bool braking =
            CanUseAbility(
                profile => profile.canBrake) &&
            IsBraking(
                planarInput,
                surfaceVelocity);

        if (braking)
        {
            groundSpeed =
                Mathf.MoveTowards(
                    groundSpeed,
                    0f,
                    brakingForce *
                    Time.fixedDeltaTime);
        }
        else
        {
            float targetSpeed =
                hasInput
                    ? runSpeed
                    : 0f;

            float rate =
                hasInput
                    ? acceleration
                    : deceleration;

            groundSpeed =
                Mathf.MoveTowards(
                    groundSpeed,
                    targetSpeed,
                    rate *
                    Time.fixedDeltaTime);
        }

        if (CanUseAbility(
                profile =>
                    profile.usesSlopeMomentum))
        {
            ApplySlopeMomentum(
                surfaceVelocity);
        }

        Vector3 direction =
            ResolveGroundDirection(
                planarInput,
                surfaceVelocity,
                hasInput);

        Vector3 newSurfaceVelocity =
            direction *
            groundSpeed;

        float normalVelocity =
            Vector3.Dot(
                currentVelocity,
                groundNormal);

        if (normalVelocity < 0f)
        {
            normalVelocity = 0f;
        }

        playerRigidbody.linearVelocity =
            newSurfaceVelocity +
            groundNormal *
            normalVelocity;

        RotateTowards(
            direction,
            groundNormal,
            turnSpeed);
    }

    private bool IsBraking(
        Vector3 input,
        Vector3 surfaceVelocity)
    {
        if (input.sqrMagnitude <=
                0.001f ||
            surfaceVelocity.sqrMagnitude <=
                0.001f)
        {
            return false;
        }

        float alignment =
            Vector3.Dot(
                input.normalized,
                surfaceVelocity.normalized);

        return alignment <
            -0.25f;
    }

    private void ApplySlopeMomentum(
        Vector3 surfaceVelocity)
    {
        if (!grounded ||
            currentSlopeAngle <= 0.01f ||
            currentSlopeAngle >
                maximumSlopeAngle)
        {
            return;
        }

        Vector3 downhill =
            Vector3.ProjectOnPlane(
                Physics.gravity,
                groundNormal);

        if (downhill.sqrMagnitude <=
            0.001f)
        {
            return;
        }

        Vector3 direction =
            surfaceVelocity.sqrMagnitude >
                0.001f
                ? surfaceVelocity.normalized
                : transform.forward;

        float influence =
            Vector3.Dot(
                direction,
                downhill.normalized);

        float slopeRatio =
            maximumSlopeAngle > 0f
                ? currentSlopeAngle /
                  maximumSlopeAngle
                : 0f;

        groundSpeed +=
            influence *
            slopeAcceleration *
            slopeRatio *
            Time.fixedDeltaTime;

        groundSpeed =
            Mathf.Max(
                0f,
                groundSpeed);
    }

    private Vector3 ResolveGroundDirection(
        Vector3 input,
        Vector3 surfaceVelocity,
        bool hasInput)
    {
        Vector3 direction;

        if (hasInput)
        {
            direction = input;
        }
        else if (surfaceVelocity.sqrMagnitude >
                 0.001f)
        {
            direction =
                surfaceVelocity.normalized;
        }
        else
        {
            direction =
                Vector3.ProjectOnPlane(
                    transform.forward,
                    groundNormal);
        }

        if (direction.sqrMagnitude <=
            0.001f)
        {
            direction =
                transform.forward;
        }

        return direction.normalized;
    }

    #endregion

    #region Air Movement

    private void UpdateAirMovement(
        Vector3 input)
    {
        if (playerRigidbody == null)
            return;

        Vector3 velocity =
            playerRigidbody.linearVelocity;

        Vector3 horizontalVelocity =
            new(
                velocity.x,
                0f,
                velocity.z);

        Vector3 desiredVelocity =
            input *
            airSpeed;

        horizontalVelocity =
            Vector3.MoveTowards(
                horizontalVelocity,
                desiredVelocity,
                airControl *
                Time.fixedDeltaTime);

        playerRigidbody.linearVelocity =
            new Vector3(
                horizontalVelocity.x,
                velocity.y,
                horizontalVelocity.z);

        if (input.sqrMagnitude >
            0.001f)
        {
            RotateTowards(
                input,
                Vector3.up,
                turnSpeed);
        }
    }

    #endregion

    #region Rolling

    private bool TryStartRolling()
    {
        if (!initialized ||
            !grounded ||
            currentState !=
                MovementState.Ground)
        {
            return false;
        }

        Vector3 surfaceVelocity =
            Vector3.ProjectOnPlane(
                playerRigidbody.linearVelocity,
                groundNormal);

        groundSpeed =
            Mathf.Max(
                groundSpeed,
                surfaceVelocity.magnitude);

        ChangeState(
            MovementState.Rolling);

        return true;
    }

    private void StopRolling()
    {
        if (currentState !=
            MovementState.Rolling)
        {
            return;
        }

        ChangeState(
            grounded
                ? MovementState.Ground
                : MovementState.Air);
    }

    private void UpdateRollingMovement(
        Vector3 input)
    {
        if (playerRigidbody == null)
            return;

        if (!grounded)
        {
            ChangeState(
                MovementState.Air);

            return;
        }

        Vector3 velocity =
            playerRigidbody.linearVelocity;

        Vector3 surfaceVelocity =
            Vector3.ProjectOnPlane(
                velocity,
                groundNormal);

        Vector3 direction =
            surfaceVelocity.sqrMagnitude >
                0.001f
                ? surfaceVelocity.normalized
                : Vector3.ProjectOnPlane(
                    transform.forward,
                    groundNormal).normalized;

        if (input.sqrMagnitude >
            0.001f)
        {
            Vector3 projectedInput =
                Vector3.ProjectOnPlane(
                    input,
                    groundNormal).normalized;

            direction =
                Vector3.Slerp(
                    direction,
                    projectedInput,
                    rollingTurnSpeed *
                    Time.fixedDeltaTime).normalized;
        }

        groundSpeed =
            Mathf.MoveTowards(
                groundSpeed,
                rollingSpeed,
                acceleration *
                Time.fixedDeltaTime);

        if (CanUseAbility(
                profile =>
                    profile.usesSlopeMomentum))
        {
            ApplySlopeMomentum(
                surfaceVelocity);
        }

        playerRigidbody.linearVelocity =
            direction *
            groundSpeed;

        RotateTowards(
            direction,
            groundNormal,
            rollingTurnSpeed);
    }

    #endregion

    #region Homing Attack

    public bool StartHomingAttack(
        Vector3 targetPosition)
    {
        if (!initialized ||
            playerRigidbody == null ||
            !CanUseAbility(
                profile =>
                    profile.canHomingAttack))
        {
            return false;
        }

        Vector3 direction =
            targetPosition -
            playerRigidbody.position;

        if (direction.sqrMagnitude <=
            0.001f)
        {
            return false;
        }

        homingDirection =
            direction.normalized;

        ChangeState(
            MovementState.HomingAttack);

        return true;
    }

    private void UpdateHomingAttack()
    {
        if (playerRigidbody == null)
            return;

        stateTimer -=
            Time.fixedDeltaTime;

        playerRigidbody.linearVelocity =
            homingDirection *
            homingAttackSpeed;

        RotateTowards(
            homingDirection,
            Vector3.up,
            turnSpeed);

        if (stateTimer > 0f)
            return;

        ChangeState(
            grounded
                ? MovementState.Ground
                : MovementState.Air);
    }

    private Transform FindHomingTarget()
    {
        int targetCount =
            Physics.OverlapSphereNonAlloc(
                transform.position,
                homingRange,
                homingTargets,
                homingTargetMask,
                QueryTriggerInteraction.Ignore);

        Transform closestTarget =
            null;

        float closestDistance =
            float.MaxValue;

        for (int index = 0;
             index < targetCount;
             index++)
        {
            Collider candidate =
                homingTargets[index];

            if (candidate == null)
                continue;

            float distance =
                (candidate.transform.position -
                 transform.position)
                .sqrMagnitude;

            if (distance >=
                closestDistance)
            {
                continue;
            }

            closestDistance =
                distance;

            closestTarget =
                candidate.transform;
        }

        return closestTarget;
    }

    #endregion

    #region External States

    private void UpdateSpringMovement(
        Vector3 input)
    {
        UpdateAirMovement(
            input);

        if (playerRigidbody != null &&
            playerRigidbody.linearVelocity.y <= 0f)
        {
            ChangeState(
                MovementState.Air);
        }
    }

    private void UpdateHurtMovement()
    {
        stateTimer -=
            Time.fixedDeltaTime;

        if (stateTimer > 0f)
            return;

        ChangeState(
            grounded
                ? MovementState.Ground
                : MovementState.Air);
    }

    private void UpdateFlyingMovement(
        Vector3 input)
    {
        if (playerRigidbody == null)
            return;

        float verticalInput = 0f;

        if (Input.GetKey(jumpKey))
        {
            verticalInput += 1f;
        }

        if (Input.GetKey(flyDownKey))
        {
            verticalInput -= 1f;
        }

        Vector3 velocity =
            input *
            flyingSpeed;

        velocity.y =
            verticalInput *
            flyingVerticalSpeed;

        playerRigidbody.linearVelocity =
            velocity;

        if (input.sqrMagnitude >
            0.001f)
        {
            RotateTowards(
                input,
                Vector3.up,
                turnSpeed);
        }
    }

    private void UpdatePowerActionMovement(
        Vector3 input)
    {
        if (grounded)
        {
            UpdateGroundMovement(
                input);
        }
        else
        {
            UpdateAirMovement(
                input);
        }
    }

    #endregion

    #region Rotation

    private void RotateTowards(
        Vector3 direction,
        Vector3 up,
        float speed)
    {
        if (direction.sqrMagnitude <=
            0.001f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction.normalized,
                up);

        Quaternion nextRotation =
            Quaternion.Slerp(
                playerRigidbody.rotation,
                targetRotation,
                speed *
                Time.fixedDeltaTime);

        playerRigidbody.MoveRotation(
            nextRotation);
    }

    #endregion

    #region Animation

    private void CacheAnimatorParameters()
    {
        animatorParameters.Clear();

        if (playerAnimator == null ||
            playerAnimator.runtimeAnimatorController ==
                null)
        {
            return;
        }

        foreach (
            AnimatorControllerParameter parameter
            in playerAnimator.parameters)
        {
            animatorParameters.Add(
                parameter.nameHash);
        }
    }

    private bool HasAnimatorParameter(
        int parameterHash)
    {
        return
            playerAnimator != null &&
            animatorParameters.Contains(
                parameterHash);
    }

    private void UpdateAnimator()
    {
        if (playerAnimator == null ||
            playerRigidbody == null)
        {
            return;
        }

        Vector3 velocity =
            playerRigidbody.linearVelocity;

        Vector3 horizontalVelocity =
            new(
                velocity.x,
                0f,
                velocity.z);

        if (HasAnimatorParameter(
                GroundedHash))
        {
            playerAnimator.SetBool(
                GroundedHash,
                grounded);
        }

        if (HasAnimatorParameter(
                SpeedHash))
        {
            playerAnimator.SetFloat(
                SpeedHash,
                horizontalVelocity.magnitude);
        }

        if (HasAnimatorParameter(
                VerticalSpeedHash))
        {
            playerAnimator.SetFloat(
                VerticalSpeedHash,
                velocity.y);
        }
    }

    private void UpdateAnimatorState()
    {
        if (!HasAnimatorParameter(
                StateHash))
        {
            return;
        }

        playerAnimator.SetInteger(
            StateHash,
            (int)currentState);
    }

    #endregion

    #region Validation

    private bool ValidateConfiguration()
    {
        bool valid = true;

        valid &=
            ValidateReference(
                playerRigidbody,
                nameof(Rigidbody));

        valid &=
            ValidateReference(
            cameraTransform,
            "Camera Transform");

        valid &=
            ValidateReference(
                groundProbe,
                GroundProbeName);

        if (characterDefinition != null &&
            !characterDefinition.IsValid())
        {
            Debug.LogError(
                "UltimatePlayerMovement has an invalid CharacterDefinition.",
                this);

            valid = false;
        }

        if (playerAnimator == null)
        {
            Debug.LogWarning(
                "UltimatePlayerMovement could not find an Animator.",
                this);
        }

        return valid;
    }

    private bool ValidateReference(
        UnityEngine.Object reference,
        string displayName)
    {
        if (reference != null)
            return true;

        Debug.LogError(
            $"UltimatePlayerMovement requires {displayName}.",
            this);

        return false;
    }

    #endregion

    #region Debug

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