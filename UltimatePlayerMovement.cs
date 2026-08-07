using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
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
    private const int OverlapCapacity = 32;
    private const int GroundHitCapacity = 16;

    private const RigidbodyConstraints DefaultConstraints =
    RigidbodyConstraints.FreezeRotationX |
    RigidbodyConstraints.FreezeRotationY |
    RigidbodyConstraints.FreezeRotationZ;

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

    [Header("Automatic Spawn Stabilization")]
    [SerializeField] private bool stabilizeOnSpawn = true;
    [SerializeField, Min(0.1f)] private float spawnGroundSearchHeight = 3f;
    [SerializeField, Min(0.1f)] private float spawnGroundSearchDistance = 10f;
    [SerializeField, Min(0.1f)] private float maximumStabilizationTime = 2f;
    [SerializeField] private float spawnGroundOffset = 0.05f;

    [Header("Automatic World Recovery")]
    [SerializeField] private bool enableWorldRecovery = true;
    [SerializeField] private Transform recoveryPoint;
    [SerializeField] private bool resolveRecoveryPointAutomatically = true;
    [SerializeField] private bool rememberSafeGroundAutomatically = true;
    [SerializeField] private bool useStartingPoseAsFallback = true;
    [SerializeField] private bool stabilizeAfterRecovery = true;

    [SerializeField] private float worldFallLimit = -30f;
    [SerializeField, Min(0f)] private float recoveryCooldown = 0.5f;
    [SerializeField, Min(0.1f)] private float safeGroundSaveDelay = 0.5f;
    [SerializeField, Min(0.1f)] private float maximumSafeDistance = 500f;

    [Header("Recovery Grounding")]
    [SerializeField, Min(0.1f)] private float recoverySearchHeight = 3f;
    [SerializeField, Min(0.1f)] private float recoverySearchDistance = 12f;
    [SerializeField, Min(0.01f)] private float recoveryCastRadius = 0.25f;
    [SerializeField, Range(0f, 89f)] private float maximumRecoverySlope = 55f;
    [SerializeField] private float recoveryGroundOffset = 0.05f;

    [Header("Physics Safety")]
    [SerializeField] private bool enablePhysicsSafety = true;
    [SerializeField, Min(1f)] private float maximumLinearSpeed = 250f;
    [SerializeField, Min(1f)] private float maximumAngularSpeed = 100f;
    [SerializeField, Min(0.01f)] private float minimumValidScale = 0.01f;
    [SerializeField, Min(0.1f)] private float missingGroundGraceTime = 3f;
    [SerializeField, Min(0.01f)] private float overlapCheckPadding = 0.05f;

    [Header("Recovery Loop Protection")]
    [SerializeField, Min(1)] private int maximumRecoveriesPerWindow = 3;
    [SerializeField, Min(0.1f)] private float recoveryWindowDuration = 5f;
    [SerializeField, Min(0.1f)] private float recoveryLockoutDuration = 2f;

    [Header("Moving Platform Safety")]
    [SerializeField] private bool trackMovingPlatforms = true;
    [SerializeField, Min(0.01f)] private float platformDetachDistance = 2f;

    [Header("Input")]
    [SerializeField] private bool acceptPlayerInput = true;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode rollKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode homingAttackKey = KeyCode.Mouse0;
    [SerializeField] private KeyCode flyDownKey = KeyCode.LeftControl;

    [Header("Homing Targeting")]
    [SerializeField] private LayerMask homingTargetMask;

    [Header("Rail Grinding")]
    [SerializeField] private RailGrinding railGrinding;

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

    private readonly Collider[] blockingOverlaps =
        new Collider[OverlapCapacity];

    private readonly HashSet<int> animatorParameters =
        new();

    private readonly RaycastHit[] groundHits =
    new RaycastHit[GroundHitCapacity];



    private MovementState currentState;

    private Vector3 groundNormal =
        Vector3.up;

    private Vector3 homingDirection;

    private float groundSpeed;
    private float currentSlopeAngle;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float stateTimer;
    private float stabilizationTimer;
    private bool isStabilizingSpawn;
    private bool originalUseGravity;
    private RigidbodyConstraints originalConstraints;
    private bool grounded;
    private bool groundedLastFrame;
    private bool movementEnabled = true;
    private bool initialized;
    private bool shuttingDown;
    private bool isInTrickZone;

    private Vector3 startingRecoveryPosition;
    private Quaternion startingRecoveryRotation;

    private Vector3 safeDestinationPosition;
    private Quaternion safeDestinationRotation;

    private float recoveryCooldownTimer;
    private float safeGroundTimer;

    private bool recoveryInitialized;
    private bool hasSafeDestination;
    private bool isRecovering;

    private Collider playerCollider;
    private Rigidbody currentGroundRigidbody;
    private Transform currentGroundTransform;

    private Vector3 currentGroundLocalPoint;
    private Vector3 previousGroundPosition;
    private Quaternion previousGroundRotation;

    private float missingGroundTimer;
    private float recoveryWindowTimer;
    private float recoveryLockoutTimer;

    private int recoveryCountInWindow;

    private bool applicationQuitting;
    private bool safetyShutdown;

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

    public Transform RecoveryPoint =>
    recoveryPoint;

    public bool HasSafeDestination =>
        hasSafeDestination;

    public bool IsRecovering =>
        isRecovering;

    public bool IsSafetyShutdown =>
    safetyShutdown;

    public bool HasValidRecoveryPoint =>
        IsRecoveryPointValid();

    public bool HasValidPhysicsState =>
        ValidateRuntimePhysicsState(
            logErrors: false);

    public bool SetRecoveryPoint(
        Transform newRecoveryPoint)
    {
        if (newRecoveryPoint == null)
            return false;

        recoveryPoint =
            newRecoveryPoint;

        return true;
    }

    public bool SetSafeDestination(
    Vector3 destination,
    Quaternion rotation)
    {
        if (!IsFiniteVector(destination) ||
            !IsFiniteQuaternion(rotation))
        {
            return false;
        }

        if (!TryResolveGroundedDestination(
                destination,
                out Vector3 groundedDestination,
                out _))
        {
            return false;
        }

        safeDestinationPosition =
            groundedDestination;

        safeDestinationRotation =
            rotation;

        safeGroundTimer = 0f;
        hasSafeDestination = true;

        return true;
    }

    public bool SetSafeDestination(
        Transform destination)
    {
        if (destination == null)
            return false;

        return SetSafeDestination(
            destination.position,
            destination.rotation);
    }

    public bool RecoverFromFall()
    {
        if (!initialized ||
            playerRigidbody == null ||
            isRecovering)
        {
            return false;
        }

        return PerformWorldRecovery();
    }

    public Vector3 Velocity =>
        playerRigidbody != null
            ? playerRigidbody.linearVelocity
            : Vector3.zero;

    public bool SetCharacterDefinition(
    CharacterDefinition definition)
    {
        if (!ValidateCharacterDefinition(
                definition))
        {
            return false;
        }

        bool definitionChanged =
            characterDefinition != definition;

        characterDefinition =
            definition;

        if (!initialized)
        {
            return InitializeMovement();
        }

        return RefreshCharacterRuntime(
            forceAnimatorResolution: definitionChanged);
    }

    private bool ValidateCharacterDefinition(
    CharacterDefinition definition)
    {
        if (definition == null)
        {
            Debug.LogError(
                "UltimatePlayerMovement received no CharacterDefinition.",
                this);

            return false;
        }

        if (!definition.IsValid())
        {
            Debug.LogError(
                $"UltimatePlayerMovement received an invalid CharacterDefinition from '{definition.name}'.",
                this);

            return false;
        }

        if (definition.movementProfile == null)
        {
            Debug.LogError(
                $"CharacterDefinition '{definition.name}' has no MovementProfile.",
                definition);

            return false;
        }

        if (definition.abilityProfile == null)
        {
            Debug.LogError(
                $"CharacterDefinition '{definition.name}' has no AbilityProfile.",
                definition);

            return false;
        }

        if (definition.animatorProfile == null)
        {
            Debug.LogError(
                $"CharacterDefinition '{definition.name}' has no AnimatorProfile.",
                definition);

            return false;
        }

        return true;
    }

    private bool RefreshCharacterRuntime(
    bool forceAnimatorResolution)
    {
        if (characterDefinition == null)
            return false;

        MovementState preservedState =
            currentState;

        bool preservedGrounded =
            grounded;

        bool preservedMovementEnabled =
            movementEnabled;

        float preservedStateTimer =
            stateTimer;

        Vector3 preservedVelocity =
            playerRigidbody != null
                ? playerRigidbody.linearVelocity
                : Vector3.zero;

        Vector3 preservedAngularVelocity =
            playerRigidbody != null
                ? playerRigidbody.angularVelocity
                : Vector3.zero;

        if (forceAnimatorResolution)
        {
            playerAnimator = null;
            animatorParameters.Clear();
        }

        ResolveDependencies();

        ApplyMovementProfile(
            characterDefinition.movementProfile);

        if (!RefreshAnimatorRuntime())
        {
            return false;
        }

        if (!initialized)
        {
            return true;
        }

        currentState =
            preservedState;

        grounded =
            preservedGrounded;

        movementEnabled =
            preservedMovementEnabled;

        stateTimer =
            preservedStateTimer;

        if (playerRigidbody != null &&
            !playerRigidbody.isKinematic)
        {
            if (IsFiniteVector(preservedVelocity))
            {
                playerRigidbody.linearVelocity =
                    preservedVelocity;
            }

            if (IsFiniteVector(preservedAngularVelocity))
            {
                playerRigidbody.angularVelocity =
                    preservedAngularVelocity;
            }

            playerRigidbody.WakeUp();
        }

        UpdateAnimatorState();
        UpdateAnimator();

        return true;
    }

    private bool RefreshAnimatorRuntime()
    {
        ResolveAnimator();

        if (playerAnimator == null)
        {
            Debug.LogError(
                $"UltimatePlayerMovement could not resolve an Animator for '{characterDefinition.name}'.",
                this);

            return false;
        }

        ApplyAnimatorProfile();

        playerAnimator.enabled = true;
        playerAnimator.speed = 1f;

        playerAnimator.Rebind();
        playerAnimator.Update(0f);

        CacheAnimatorParameters();

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

        if (!IsFiniteVector(
                launchVelocity))
        {
            return;
        }

        if (launchVelocity.sqrMagnitude >
            maximumLinearSpeed *
            maximumLinearSpeed)
        {
            launchVelocity =
                Vector3.ClampMagnitude(
                    launchVelocity,
                    maximumLinearSpeed);
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

        if (!IsFiniteVector(
                knockbackVelocity))
        {
            knockbackVelocity =
                Vector3.zero;
        }

        if (knockbackVelocity.sqrMagnitude >
            maximumLinearSpeed *
            maximumLinearSpeed)
        {
            knockbackVelocity =
                Vector3.ClampMagnitude(
                    knockbackVelocity,
                    maximumLinearSpeed);
        }

        playerRigidbody.linearVelocity =
            knockbackVelocity;

        stateTimer =
            float.IsFinite(duration) &&
            duration >= 0f
                ? duration
                : Mathf.Max(
                    0f,
                    defaultHurtDuration);

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
        if (!RefreshAnimatorRuntime())
            return;

        UpdateAnimatorState();
        UpdateAnimator();
    }
    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveDependencies();
        ConfigureRigidbody();
    }

    private void Start()
    {
        TryInitializeMovement();
    }

    private bool TryInitializeMovement()
    {
        if (initialized)
            return true;

        if (characterDefinition == null)
            return false;

        ResolveDependencies();
        ConfigureRigidbody();

        return InitializeMovement();
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

        UpdateAnimator();

        if (!CanProcessInput())
        {
            jumpBufferTimer = 0f;
            return;
        }

        UpdateInputTimers();
        HandleJumpInput();
        HandleRollingInput();
        HandleHomingAttackInput();
    }

    private void FixedUpdate()
    {
        if (shuttingDown ||
    applicationQuitting ||
    safetyShutdown)
        {
            return;
        }

        if (!initialized)
        {
            TryInitializeMovement();
            return;
        }

        UpdateSafetyTimers();

        if (enablePhysicsSafety &&
            !RunPhysicsSafetyChecks())
        {
            return;
        }

        UpdateRecoveryCooldown();

        if (ShouldRecoverFromWorld())
        {
            PerformWorldRecovery();
            return;
        }

        if (isStabilizingSpawn)
        {
            UpdateSpawnStabilization();
            return;
        }

        UpdateGrounding();
        UpdateMovingPlatformTracking();
        UpdateAutomaticSafeDestination();
        UpdateMissingGroundProtection();

        if (!movementEnabled)
            return;

        Vector3 movementInput =
            ReadMovementInput();

        switch (currentState)
        {
            case MovementState.Ground:
                UpdateGroundState(
                    movementInput);
                break;

            case MovementState.Air:
                UpdateAirState(
                    movementInput);
                break;

            case MovementState.Rolling:
                UpdateRollingState(
                    movementInput);
                break;

            case MovementState.HomingAttack:
                UpdateHomingAttackState();
                break;

            case MovementState.Grinding:
                UpdateGrindingState();
                break;

            case MovementState.Spring:
                UpdateSpringState(
                    movementInput);
                break;

            case MovementState.Hurt:
                UpdateHurtState();
                break;

            case MovementState.Flying:
                UpdateFlyingState(
                    movementInput);
                break;

            case MovementState.PowerAction:
                UpdatePowerActionState(
                    movementInput);
                break;

            default:
                RecoverInvalidMovementState();
                break;
        }
    }

    #region State Updates

    private void UpdateGroundState(
        Vector3 movementInput)
    {
        if (playerRigidbody == null)
            return;

        if (!grounded)
        {
            ChangeState(
                MovementState.Air);

            return;
        }

        UpdateGroundMovement(
            movementInput);
    }

    private void UpdateAirState(
        Vector3 movementInput)
    {
        if (playerRigidbody == null)
            return;

        if (grounded)
        {
            ChangeState(
                MovementState.Ground);

            return;
        }

        UpdateAirMovement(
            movementInput);
    }

    private void UpdateRollingState(
        Vector3 movementInput)
    {
        if (!CanUseAbility(
                profile => profile.canRoll))
        {
            StopRolling();
            return;
        }

        UpdateRollingMovement(
            movementInput);
    }

    private void UpdateHomingAttackState()
    {
        if (!CanUseAbility(
                profile => profile.canHomingAttack))
        {
            ChangeState(
                grounded
                    ? MovementState.Ground
                    : MovementState.Air);

            return;
        }

        if (playerRigidbody == null)
        {
            ChangeState(
                grounded
                    ? MovementState.Ground
                    : MovementState.Air);

            return;
        }

        if (!IsFiniteVector(
                homingDirection) ||
            homingDirection.sqrMagnitude <=
                0.000001f)
        {
            ChangeState(
                grounded
                    ? MovementState.Ground
                    : MovementState.Air);

            return;
        }

        UpdateHomingAttack();
    }

    private void UpdateGrindingState()
    {
        if (!CanUseAbility(
                profile => profile.canGrind))
        {
            ExitGrindingState();
            return;
        }

        if (railGrinding == null)
        {
            ExitGrindingState();
            return;
        }

        if (playerRigidbody == null)
        {
            ExitGrindingState();
            return;
        }

        if (!railGrinding.IsGrinding)
        {
            ExitGrindingState();
            return;
        }
    }

    private void UpdateSpringState(
        Vector3 movementInput)
    {
        if (playerRigidbody == null)
        {
            ChangeState(
                grounded
                    ? MovementState.Ground
                    : MovementState.Air);

            return;
        }

        UpdateSpringMovement(
            movementInput);
    }

    private void UpdateHurtState()
    {
        if (playerRigidbody == null)
        {
            ChangeState(
                grounded
                    ? MovementState.Ground
                    : MovementState.Air);

            return;
        }

        UpdateHurtMovement();
    }

    private void UpdateFlyingState(
        Vector3 movementInput)
    {
        if (!CanUseAbility(
                profile => profile.canFly))
        {
            StopFlying();
            return;
        }

        if (playerRigidbody == null)
        {
            StopFlying();
            return;
        }

        UpdateFlyingMovement(
            movementInput);
    }

    private void UpdatePowerActionState(
        Vector3 movementInput)
    {
        if (!CanUseAbility(
                profile => profile.canPowerAction))
        {
            StopPowerAction();
            return;
        }

        if (playerRigidbody == null)
        {
            StopPowerAction();
            return;
        }

        UpdatePowerActionMovement(
            movementInput);
    }

    private void RecoverInvalidMovementState()
    {
        MovementState recoveredState =
            grounded
                ? MovementState.Ground
                : MovementState.Air;

        currentState =
            recoveredState;

        stateTimer = 0f;
        homingDirection =
            Vector3.zero;

        UpdateAnimatorState();

        StateChanged?.Invoke(
            currentState);

        Debug.LogError(
            $"UltimatePlayerMovement recovered an invalid movement state on '{name}' to {recoveredState}.",
            this);
    }

    #endregion

    private void OnDisable()
    {
        CancelSpawnStabilization();
        StopMovement();

        isRecovering = false;
        recoveryCooldownTimer = 0f;
        safeGroundTimer = 0f;
    }

    private void OnDestroy()
    {
        shuttingDown = true;

        CancelSpawnStabilization();
        StopMovement();

        StateChanged = null;

        animatorParameters.Clear();

        currentGroundRigidbody = null;
        currentGroundTransform = null;

        recoveryInitialized = false;
        isRecovering = false;
        hasSafeDestination = false;
        recoveryPoint = null;

        playerCollider = null;
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

        spawnGroundSearchHeight =
    Mathf.Max(
        0.1f,
        spawnGroundSearchHeight);

        spawnGroundSearchDistance =
            Mathf.Max(
                0.1f,
                spawnGroundSearchDistance);

        maximumStabilizationTime =
            Mathf.Max(
                0.1f,
                maximumStabilizationTime);

        recoveryCooldown =
            Mathf.Max(
                0f,
                recoveryCooldown);

        safeGroundSaveDelay =
            Mathf.Max(
                0.1f,
                safeGroundSaveDelay);

        maximumSafeDistance =
            Mathf.Max(
                0.1f,
                maximumSafeDistance);

        recoverySearchHeight =
            Mathf.Max(
                0.1f,
                recoverySearchHeight);

        recoverySearchDistance =
            Mathf.Max(
                0.1f,
                recoverySearchDistance);

        recoveryCastRadius =
            Mathf.Max(
                0.01f,
                recoveryCastRadius);

        maximumRecoverySlope =
            Mathf.Clamp(
                maximumRecoverySlope,
                0f,
                89f);

        maximumLinearSpeed =
    Mathf.Max(
        1f,
        maximumLinearSpeed);

        maximumAngularSpeed =
            Mathf.Max(
                1f,
                maximumAngularSpeed);

        minimumValidScale =
            Mathf.Max(
                0.01f,
                minimumValidScale);

        missingGroundGraceTime =
            Mathf.Max(
                0.1f,
                missingGroundGraceTime);

        overlapCheckPadding =
            Mathf.Max(
                0.01f,
                overlapCheckPadding);

        maximumRecoveriesPerWindow =
            Mathf.Max(
                1,
                maximumRecoveriesPerWindow);

        recoveryWindowDuration =
            Mathf.Max(
                0.1f,
                recoveryWindowDuration);

        recoveryLockoutDuration =
            Mathf.Max(
                0.1f,
                recoveryLockoutDuration);

        platformDetachDistance =
            Mathf.Max(
                0.01f,
                platformDetachDistance);

        if (!Enum.IsDefined(
                typeof(MovementState),
                startingState))
        {
            startingState =
                MovementState.Ground;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ResolveDependencies();
            ApplyPhysicsSettings();
        }
#endif
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

    private void OnApplicationQuit()
    {
        applicationQuitting = true;
    }

    private void OnApplicationPause(
        bool paused)
    {
        if (!paused)
            return;

        StopMovement();
    }

    #region Initialization

    public bool InitializeMovement()
    {
        if (initialized)
            return true;

        ResolveDependencies();
        ConfigureRigidbody();

        if (characterDefinition != null &&
            !ApplyCharacterDefinition())
        {
            initialized = false;
            movementEnabled = false;

            Debug.LogError(
                $"UltimatePlayerMovement could not apply the CharacterDefinition on '{name}'.",
                this);

            return false;
        }

        if (!ValidateConfiguration())
        {
            initialized = false;
            movementEnabled = false;

            Debug.LogError(
                $"UltimatePlayerMovement failed to initialize on '{name}'.",
                this);

            return false;
        }

        ResetRuntimeState();
        InitializeRecoverySystem();

        currentState =
            IsValidMovementState(startingState)
                ? startingState
                : MovementState.Ground;

        grounded =
            DetectGround(
                out groundNormal,
                out currentSlopeAngle);

        groundedLastFrame =
            grounded;

        if (!grounded &&
            currentState == MovementState.Ground)
        {
            currentState =
                MovementState.Air;
        }

        initialized = true;

        BeginSpawnStabilization();

        UpdateAnimatorState();
        UpdateAnimator();

        return true;
    }

    private static bool IsValidMovementState(
    MovementState state)
    {
        return Enum.IsDefined(
            typeof(MovementState),
            state);
    }

    private void ResolveDependencies()
    {
        ResolveRigidbody();
        ResolveRailGrinding();
        ResolveCollider();
        ResolveAnimator();
        ResolveCamera();
        ResolveGroundProbe();

        ConfigureCollider();
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
        if (IsValidCameraTransform(
                cameraTransform))
        {
            return;
        }

        cameraTransform = null;

        Camera mainCamera =
            Camera.main;

        if (IsUsableCamera(
                mainCamera))
        {
            cameraTransform =
                mainCamera.transform;

            return;
        }

        CameraController cameraController =
            FindAnyObjectByType<CameraController>(
                FindObjectsInactive.Include);

        if (cameraController != null)
        {
            Camera controllerCamera =
                cameraController.GetComponent<Camera>();

            controllerCamera ??=
                cameraController.GetComponentInChildren<Camera>(
                    includeInactive: true);

            if (IsUsableCamera(
                    controllerCamera))
            {
                cameraTransform =
                    controllerCamera.transform;

                return;
            }
        }

        Camera[] cameras =
    FindObjectsByType<Camera>(
        FindObjectsInactive.Include);

        foreach (Camera candidate in cameras)
        {
            if (!IsUsableCamera(
                    candidate))
            {
                continue;
            }

            cameraTransform =
                candidate.transform;

            return;
        }
    }

    private static bool IsValidCameraTransform(
    Transform candidate)
    {
        if (candidate == null ||
            !candidate.gameObject.scene.IsValid() ||
            !candidate.gameObject.activeInHierarchy)
        {
            return false;
        }

        Camera camera =
            candidate.GetComponent<Camera>();

        camera ??=
            candidate.GetComponentInChildren<Camera>(
                includeInactive: true);

        camera ??=
            candidate.GetComponentInParent<Camera>();

        return IsUsableCamera(
            camera);
    }

    private static bool IsUsableCamera(
        Camera candidate)
    {
        return
            candidate != null &&
            candidate.gameObject.scene.IsValid() &&
            candidate.gameObject.activeInHierarchy &&
            candidate.enabled;
    }

    private void ResolveGroundProbe()
    {
        if (groundProbe != null)
            return;

        Transform existingProbe =
            transform.Find(
                GroundProbeName);

        if (existingProbe != null)
        {
            groundProbe =
                existingProbe;

            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying &&
            UnityEditor.EditorUtility.IsPersistent(gameObject))
        {
            return;
        }
#endif

        GameObject probeObject =
            new(GroundProbeName);

        groundProbe =
            probeObject.transform;

        groundProbe.SetParent(
            transform,
            worldPositionStays: false);

        float probeHeight =
            playerCollider != null
                ? playerCollider.bounds.extents.y
                : 0.5f;

        groundProbe.localPosition =
            Vector3.down *
            Mathf.Max(
                0.05f,
                probeHeight - 0.05f);
    }

    private void ConfigureRigidbody()
    {
        ResolveRigidbody();

        if (playerRigidbody == null)
        {
            Debug.LogError(
                $"{nameof(UltimatePlayerMovement)} could not configure Rigidbody because none was found.",
                this);

            return;
        }

        ApplyPhysicsSettings();
    }

    private void ConfigureCollider()
    {
        ResolveCollider();

        if (playerCollider == null)
            return;

        playerCollider.enabled =
            true;

        playerCollider.isTrigger =
            false;
    }

    private void ApplyPhysicsSettings()
    {
        if (playerRigidbody == null)
            return;

        playerRigidbody.useGravity = true;
        playerRigidbody.isKinematic = false;

        playerRigidbody.interpolation =
            RigidbodyInterpolation.Interpolate;

        playerRigidbody.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;

        playerRigidbody.constraints =
            DefaultConstraints;

        playerRigidbody.WakeUp();
    }

    private void ResolveRailGrinding()
    {
        railGrinding ??=
            GetComponent<RailGrinding>();
    }

    private void ResolveRigidbody()
    {
        if (playerRigidbody != null &&
            playerRigidbody.gameObject == gameObject)
        {
            return;
        }

        playerRigidbody =
            GetComponent<Rigidbody>();
    }

    private void ResolveCollider()
    {
        if (playerCollider != null &&
            playerCollider.gameObject == gameObject &&
            !playerCollider.isTrigger)
        {
            return;
        }

        Collider[] colliders =
            GetComponents<Collider>();

        playerCollider =
            null;

        foreach (Collider candidate in colliders)
        {
            if (candidate == null ||
                !candidate.enabled ||
                candidate.isTrigger)
            {
                continue;
            }

            playerCollider =
                candidate;

            if (candidate is CapsuleCollider)
                break;
        }
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

    private bool ApplyCharacterDefinition()
    {
        if (characterDefinition == null)
            return false;

        if (!ValidateCharacterDefinition(
                characterDefinition))
        {
            return false;
        }

        return RefreshCharacterRuntime(
            forceAnimatorResolution: false);
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

        if (!IsValidCameraTransform(
                cameraTransform))
        {
            ResolveCamera();
        }

        if (!IsValidCameraTransform(
                cameraTransform))
        {
            EnterSafetyShutdown(
                "Movement input could not resolve a valid gameplay camera.");

            return Vector3.zero;
        }

        Vector3 forward =
            cameraTransform.forward;

        Vector3 right =
            cameraTransform.right;

        forward.y = 0f;
        right.y = 0f;

        if (forward.sqrMagnitude <= 0.000001f ||
            right.sqrMagnitude <= 0.000001f)
        {
            EnterSafetyShutdown(
                "Movement input received invalid camera directions.");

            return Vector3.zero;
        }

        forward.Normalize();
        right.Normalize();

        Vector3 movement =
            forward * vertical +
            right * horizontal;

        if (movement.sqrMagnitude > 1f)
        {
            movement.Normalize();
        }

        return movement;
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

    private void CacheGroundPlatform(
    RaycastHit hit)
    {
        if (!trackMovingPlatforms ||
            hit.collider == null ||
            playerRigidbody == null)
        {
            ClearGroundPlatform();
            return;
        }

        Transform detectedGround =
            hit.collider.transform;

        if (currentGroundTransform ==
            detectedGround)
        {
            return;
        }

        currentGroundTransform =
            detectedGround;

        currentGroundRigidbody =
            hit.rigidbody;

        currentGroundLocalPoint =
            currentGroundTransform
                .InverseTransformPoint(
                    playerRigidbody.position);

        previousGroundPosition =
            currentGroundTransform.position;

        previousGroundRotation =
            currentGroundTransform.rotation;
    }

    private void ClearGroundPlatform()
    {
        currentGroundRigidbody = null;
        currentGroundTransform = null;

        currentGroundLocalPoint =
            Vector3.zero;
    }

    private void UpdateMovingPlatformTracking()
    {
        if (!trackMovingPlatforms ||
            !grounded ||
            currentGroundTransform == null ||
            playerRigidbody == null)
        {
            return;
        }

        Vector3 currentPlatformPosition =
            currentGroundTransform.position;

        Quaternion currentPlatformRotation =
            currentGroundTransform.rotation;

        if (!IsFiniteVector(
                currentPlatformPosition) ||
            !IsFiniteQuaternion(
                currentPlatformRotation))
        {
            ClearGroundPlatform();
            return;
        }

        Vector3 positionDelta =
            currentPlatformPosition -
            previousGroundPosition;

        Quaternion rotationDelta =
            currentPlatformRotation *
            Quaternion.Inverse(
                previousGroundRotation);

        if (!IsFiniteVector(
                positionDelta) ||
            !IsFiniteQuaternion(
                rotationDelta))
        {
            ClearGroundPlatform();
            return;
        }

        Vector3 playerOffset =
            playerRigidbody.position -
            previousGroundPosition;

        Vector3 rotatedOffset =
            rotationDelta *
            playerOffset;

        Vector3 rotationMovement =
            rotatedOffset -
            playerOffset;

        Vector3 platformMovement =
            positionDelta +
            rotationMovement;

        if (!IsFiniteVector(
                platformMovement))
        {
            ClearGroundPlatform();
            return;
        }

        if (platformMovement.magnitude >
            platformDetachDistance)
        {
            ClearGroundPlatform();
            return;
        }

        if (platformMovement.sqrMagnitude >
            0.000001f)
        {
            playerRigidbody.MovePosition(
                playerRigidbody.position +
                platformMovement);
        }

        previousGroundPosition =
            currentPlatformPosition;

        previousGroundRotation =
            currentPlatformRotation;
    }

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
        {
            ClearGroundPlatform();
            return false;
        }

        Vector3 origin =
            groundProbe.position +
            transform.up *
            0.05f;

        int hitCount =
            Physics.SphereCastNonAlloc(
                origin,
                groundProbeRadius,
                -transform.up,
                groundHits,
                groundProbeDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
        {
            ClearGroundPlatform();
            return false;
        }

        RaycastHit closestHit =
            default;

        float closestDistance =
            float.PositiveInfinity;

        bool foundGround =
            false;

        for (int index = 0;
             index < hitCount;
             index++)
        {
            RaycastHit candidate =
                groundHits[index];

            groundHits[index] =
                default;

            if (candidate.collider == null)
            {
                continue;
            }

            if (IsOwnCollider(
                    candidate.collider))
            {
                continue;
            }

            float candidateSlope =
                Vector3.Angle(
                    candidate.normal,
                    Vector3.up);

            if (!float.IsFinite(
                    candidateSlope))
            {
                continue;
            }

            if (candidateSlope >
                maximumSlopeAngle)
            {
                continue;
            }

            if (candidate.distance >=
                closestDistance)
            {
                continue;
            }

            closestHit =
                candidate;

            closestDistance =
                candidate.distance;

            foundGround =
                true;
        }

        if (!foundGround)
        {
            ClearGroundPlatform();
            return false;
        }

        normal =
            closestHit.normal.normalized;

        slopeAngle =
            Vector3.Angle(
                normal,
                Vector3.up);

        CacheGroundPlatform(
            closestHit);

        return true;
    }

    private void HandleLanding()
    {
        switch (currentState)
        {
            case MovementState.Grinding:
            case MovementState.Spring:
            case MovementState.Hurt:
                return;
        }

        homingDirection =
            Vector3.zero;

        stateTimer = 0f;

        if (currentState !=
            MovementState.Ground)
        {
            ChangeState(
                MovementState.Ground);
        }
    }

    private void HandleLeavingGround()
    {
        if (currentState !=
            MovementState.Ground)
        {
            return;
        }

        ChangeState(
            MovementState.Air);
    }

    #endregion


    #region Spawn Stabilization

    private void BeginSpawnStabilization()
    {
        stabilizationTimer = 0f;
        isStabilizingSpawn = false;

        if (!stabilizeOnSpawn)
            return;

        if (playerRigidbody == null)
        {
            Debug.LogWarning(
                $"Spawn stabilization could not start on '{name}' because the Rigidbody is missing.",
                this);

            return;
        }

        originalUseGravity =
            playerRigidbody.useGravity;

        originalConstraints =
            playerRigidbody.constraints;

        movementEnabled = false;
        isStabilizingSpawn = true;

        StopRigidbodyMotion();

        playerRigidbody.useGravity = false;
        playerRigidbody.constraints =
            originalConstraints |
            RigidbodyConstraints.FreezePositionY;

        if (TryCompleteSpawnStabilization())
        {
            return;
        }
    }

    private void UpdateSpawnStabilization()
    {
        if (!isStabilizingSpawn)
            return;

        if (playerRigidbody == null)
        {
            EndSpawnStabilization(
                groundedSuccessfully: false);

            return;
        }

        stabilizationTimer +=
            Time.fixedDeltaTime;

        StopRigidbodyMotion();

        if (TryCompleteSpawnStabilization())
            return;

        if (stabilizationTimer <
            maximumStabilizationTime)
        {
            return;
        }

        EndSpawnStabilization(
            groundedSuccessfully: false);
    }

    private bool TryCompleteSpawnStabilization()
    {
        if (!isStabilizingSpawn ||
            playerRigidbody == null)
        {
            return false;
        }

        if (!TryFindSpawnGround(
                out RaycastHit hit))
        {
            return false;
        }

        SnapRigidbodyToGround(
            hit);

        Physics.SyncTransforms();

        grounded =
            DetectGround(
                out groundNormal,
                out currentSlopeAngle);

        groundedLastFrame =
            grounded;

        if (!grounded)
            return false;

        EndSpawnStabilization(
            groundedSuccessfully: true);

        return true;
    }

    private bool TryFindSpawnGround(
    out RaycastHit hit)
    {
        hit = default;

        if (playerRigidbody == null)
            return false;

        float searchHeight =
            Mathf.Max(
                0.1f,
                spawnGroundSearchHeight);

        float searchDistance =
            Mathf.Max(
                0.1f,
                spawnGroundSearchDistance);

        float castRadius =
            Mathf.Max(
                0.01f,
                groundProbeRadius);

        Vector3 origin =
            playerRigidbody.position +
            Vector3.up *
            searchHeight;

        RaycastHit[] hits =
            Physics.SphereCastAll(
                origin,
                castRadius,
                Vector3.down,
                searchHeight +
                searchDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);

        float nearestDistance =
            float.PositiveInfinity;

        bool foundGround =
            false;

        foreach (RaycastHit candidate in hits)
        {
            if (candidate.collider == null ||
                IsOwnCollider(
                    candidate.collider))
            {
                continue;
            }

            float slopeAngle =
                Vector3.Angle(
                    candidate.normal,
                    Vector3.up);

            if (!float.IsFinite(slopeAngle) ||
                slopeAngle >
                    maximumSlopeAngle)
            {
                continue;
            }

            if (candidate.distance >=
                nearestDistance)
            {
                continue;
            }

            nearestDistance =
                candidate.distance;

            hit =
                candidate;

            foundGround =
                true;
        }

        return foundGround;
    }

    private void SnapRigidbodyToGround(
        RaycastHit hit)
    {
        if (playerRigidbody == null ||
            hit.collider == null)
        {
            return;
        }

        float bottomOffset =
            GetColliderBottomOffset();

        Vector3 groundedPosition =
            playerRigidbody.position;

        groundedPosition.y =
            hit.point.y +
            bottomOffset +
            spawnGroundOffset;

        playerRigidbody.position =
            groundedPosition;

        StopRigidbodyMotion();
    }

    private float GetColliderBottomOffset()
    {
        if (playerCollider == null)
        {
            ResolveCollider();
        }

        if (playerCollider == null)
            return 0f;

        float offset =
            transform.position.y -
            playerCollider.bounds.min.y;

        return Mathf.Max(
            0f,
            offset);
    }

    private void EndSpawnStabilization(
        bool groundedSuccessfully)
    {
        if (!isStabilizingSpawn)
            return;

        RestoreRigidbodyAfterStabilization();

        isStabilizingSpawn = false;
        stabilizationTimer = 0f;
        movementEnabled = true;

        if (groundedSuccessfully)
        {
            grounded = true;
            groundedLastFrame = true;

            ChangeState(
                MovementState.Ground);
        }
        else
        {
            grounded =
                DetectGround(
                    out groundNormal,
                    out currentSlopeAngle);

            groundedLastFrame =
                grounded;

            ChangeState(
                grounded
                    ? MovementState.Ground
                    : MovementState.Air);

            Debug.LogWarning(
                $"Spawn stabilization timed out on '{name}'. Normal physics was restored.",
                this);
        }

        UpdateAnimatorState();
        UpdateAnimator();
    }

    private void CancelSpawnStabilization()
    {
        if (!isStabilizingSpawn)
            return;

        RestoreRigidbodyAfterStabilization();

        isStabilizingSpawn = false;
        stabilizationTimer = 0f;
    }

    private void RestoreRigidbodyAfterStabilization()
    {
        if (playerRigidbody == null)
            return;

        playerRigidbody.constraints =
            originalConstraints;

        playerRigidbody.useGravity =
            originalUseGravity;

        StopRigidbodyMotion();
        playerRigidbody.WakeUp();
    }

    private void StopRigidbodyMotion()
    {
        if (playerRigidbody == null)
            return;

        playerRigidbody.linearVelocity =
            Vector3.zero;

        playerRigidbody.angularVelocity =
            Vector3.zero;
    }

    #endregion


    #region World Recovery

    private bool IsRecoveryPointValid()
    {
        if (recoveryPoint == null)
            return false;

        if (!recoveryPoint.gameObject.scene.IsValid())
            return false;

        if (!recoveryPoint.gameObject.activeInHierarchy)
            return false;

        return
            IsFiniteVector(
                recoveryPoint.position) &&
            IsFiniteQuaternion(
                recoveryPoint.rotation);
    }

    private static bool IsFiniteQuaternion(
        Quaternion value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z) &&
            float.IsFinite(value.w);
    }

    private void InitializeRecoverySystem()
    {
        if (recoveryInitialized)
            return;

        Vector3 currentPosition =
            playerRigidbody != null
                ? playerRigidbody.position
                : transform.position;

        Quaternion currentRotation =
            playerRigidbody != null
                ? playerRigidbody.rotation
                : transform.rotation;

        startingRecoveryPosition =
            currentPosition;

        startingRecoveryRotation =
            currentRotation;

        recoveryCooldownTimer = 0f;
        safeGroundTimer = 0f;
        isRecovering = false;

        if (resolveRecoveryPointAutomatically &&
            recoveryPoint == null)
        {
            recoveryPoint =
                ResolveAutomaticRecoveryPoint();
        }

        SetSafeDestination(
            currentPosition,
            currentRotation);

        recoveryInitialized = true;
    }

    private Transform ResolveAutomaticRecoveryPoint()
    {
        Transform point =
            FindSceneTransformByName(
                "Player Spawn Point");

        point ??=
            FindSceneTransformByName(
                "Respawn Point");

        point ??=
            FindSceneTransformByName(
                "Reset Point");

        point ??=
            FindSceneTransformByName(
                "Checkpoint");

        return point;
    }

    private static Transform FindSceneTransformByName(
        string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
            return null;

        Transform[] transforms =
    FindObjectsByType<Transform>(
        FindObjectsInactive.Include);

        string normalizedTarget =
            NormalizeRecoveryName(
                targetName);

        foreach (Transform candidate in transforms)
        {
            if (candidate == null)
                continue;

            if (NormalizeRecoveryName(candidate.name) ==
                normalizedTarget)
            {
                return candidate;
            }
        }

        return null;
    }

    private static string NormalizeRecoveryName(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "")
            .Trim()
            .ToLowerInvariant();
    }

    private void UpdateRecoveryCooldown()
    {
        if (recoveryCooldownTimer <= 0f)
            return;

        recoveryCooldownTimer =
            Mathf.Max(
                0f,
                recoveryCooldownTimer -
                Time.fixedDeltaTime);
    }

    private bool ShouldRecoverFromWorld()
    {
        if (!enableWorldRecovery ||
            !recoveryInitialized ||
            isRecovering ||
            isStabilizingSpawn ||
            recoveryCooldownTimer > 0f ||
            playerRigidbody == null)
        {
            return false;
        }

        Vector3 position =
            playerRigidbody.position;

        if (!IsFiniteVector(position))
            return true;

        if (position.y <
            worldFallLimit)
        {
            return true;
        }

        if (!hasSafeDestination)
            return false;

        return
            Vector3.Distance(
                position,
                safeDestinationPosition) >
            maximumSafeDistance;
    }

    private void UpdateAutomaticSafeDestination()
    {
        if (!rememberSafeGroundAutomatically ||
            !grounded ||
            isRecovering ||
            isStabilizingSpawn ||
            playerRigidbody == null)
        {
            safeGroundTimer = 0f;
            return;
        }

        Vector3 velocity =
            playerRigidbody.linearVelocity;

        if (Mathf.Abs(velocity.y) > 0.25f)
        {
            safeGroundTimer = 0f;
            return;
        }

        safeGroundTimer +=
            Time.fixedDeltaTime;

        if (safeGroundTimer <
            safeGroundSaveDelay)
        {
            return;
        }

        SetSafeDestination(
            playerRigidbody.position,
            playerRigidbody.rotation);
    }

    private bool PerformWorldRecovery()
    {
        if (playerRigidbody == null ||
    !CanStartRecovery())
        {
            return false;
        }

        else
        {
            Debug.LogWarning(
                $"World recovery triggered on '{name}'.",
                this);
        }

        isRecovering = true;
        movementEnabled = false;

        try
        {
            CancelSpawnStabilization();
            StopRigidbodyMotion();

            GetRecoveryPose(
                out Vector3 destination,
                out Quaternion rotation);

            if (TryResolveGroundedDestination(
                    destination,
                    out Vector3 groundedDestination,
                    out _))
            {
                destination =
                    groundedDestination;
            }

            if (!IsFiniteVector(destination))
            {
                destination =
                    startingRecoveryPosition;

                rotation =
                    startingRecoveryRotation;
            }

            playerRigidbody.position =
                destination;

            playerRigidbody.rotation =
                rotation;

            Physics.SyncTransforms();

            StopRigidbodyMotion();

            grounded =
                DetectGround(
                    out groundNormal,
                    out currentSlopeAngle);

            groundedLastFrame =
                grounded;

            ChangeState(
                grounded
                    ? MovementState.Ground
                    : MovementState.Air);

            recoveryCooldownTimer =
                recoveryCooldown;

            if (stabilizeAfterRecovery)
            {
                BeginSpawnStabilization();
            }
            else
            {
                movementEnabled = true;
            }

            return true;
        }
        finally
        {
            isRecovering = false;

            if (!isStabilizingSpawn)
            {
                movementEnabled = true;
            }
        }
    }

    private void GetRecoveryPose(
        out Vector3 position,
        out Quaternion rotation)
    {
        if (IsRecoveryPointValid())
        {
            position =
                recoveryPoint.position;

            rotation =
                recoveryPoint.rotation;

            return;
        }

        if (hasSafeDestination)
        {
            position =
                safeDestinationPosition;

            rotation =
                safeDestinationRotation;

            return;
        }

        position =
            useStartingPoseAsFallback
                ? startingRecoveryPosition
                : transform.position;

        rotation =
            useStartingPoseAsFallback
                ? startingRecoveryRotation
                : transform.rotation;
    }

    private bool TryResolveGroundedDestination(
        Vector3 requestedPosition,
        out Vector3 groundedPosition,
        out Vector3 resolvedNormal)
    {
        groundedPosition =
            requestedPosition;

        resolvedNormal =
            Vector3.up;

        float searchHeight =
            Mathf.Max(
                0.1f,
                recoverySearchHeight);

        float searchDistance =
            Mathf.Max(
                0.1f,
                recoverySearchDistance);

        float castRadius =
            Mathf.Max(
                0.01f,
                recoveryCastRadius);

        Vector3 origin =
            requestedPosition +
            Vector3.up *
            searchHeight;

        RaycastHit[] hits =
            Physics.SphereCastAll(
                origin,
                castRadius,
                Vector3.down,
                searchHeight +
                searchDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);

        float nearestDistance =
            float.PositiveInfinity;

        bool foundGround =
            false;

        foreach (RaycastHit candidate in hits)
        {
            if (candidate.collider == null)
                continue;

            if (IsOwnCollider(
                    candidate.collider))
            {
                continue;
            }

            float slopeAngle =
                Vector3.Angle(
                    candidate.normal,
                    Vector3.up);

            if (slopeAngle >
                maximumRecoverySlope)
            {
                continue;
            }

            if (candidate.distance >=
                nearestDistance)
            {
                continue;
            }

            nearestDistance =
                candidate.distance;

            groundedPosition =
                requestedPosition;

            groundedPosition.y =
                candidate.point.y +
                GetColliderBottomOffset() +
                recoveryGroundOffset;

            resolvedNormal =
                candidate.normal.normalized;

            foundGround =
                true;
        }

        return foundGround;
    }

    private bool IsOwnCollider(
        Collider candidate)
    {
        if (candidate == null)
            return false;

        Transform candidateTransform =
            candidate.transform;

        Transform rigidbodyTransform =
            playerRigidbody != null
                ? playerRigidbody.transform
                : transform;

        return
            candidateTransform == rigidbodyTransform ||
            candidateTransform.IsChildOf(rigidbodyTransform);
    }

    private static bool IsFiniteVector(
        Vector3 value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }

    #endregion

    #region Runtime Safety

    private bool RunPhysicsSafetyChecks()
    {
        if (!ValidateRuntimeReferences())
        {
            AttemptRuntimeReferenceRecovery();

            if (!ValidateRuntimeReferences())
            {
                Debug.LogError(
                    $"Runtime reference recovery failed on '{name}'.\n" +
                    $"Rigidbody: {(playerRigidbody != null)}\n" +
                    $"Collider: {(playerCollider != null)}\n" +
                    $"Animator: {(playerAnimator != null)}\n" +
                    $"Camera: {(cameraTransform != null)}\n" +
                    $"Ground Probe: {(groundProbe != null)}\n" +
                    $"Character Definition: {(characterDefinition != null)}\n" +
                    $"Movement Profile: {(characterDefinition?.movementProfile != null)}\n" +
                    $"Ability Profile: {(characterDefinition?.abilityProfile != null)}",
                    this);

                EnterSafetyShutdown(
                    "Required runtime references could not be restored.");

                return false;
            }
        }

        if (!ValidateRuntimePhysicsState(
                logErrors: true))
        {
            if (!TryRecoverInvalidPhysicsState())
            {
                EnterSafetyShutdown(
                    "The Rigidbody entered an unrecoverable physics state.");

                return false;
            }

            return false;
        }

        if (!ValidateTransformScale())
        {
            EnterSafetyShutdown(
                "The character has an invalid or near-zero transform scale.");

            return false;
        }

        if (IsCharacterInsideBlockingGeometry())
        {
            PerformWorldRecovery();
            return false;
        }

        return true;
    }

    private bool HasValidPhysicsOwnership()
    {
        return
            playerRigidbody != null &&
            playerRigidbody.gameObject == gameObject &&
            playerCollider != null &&
            playerCollider.gameObject == gameObject &&
            playerCollider.enabled &&
            !playerCollider.isTrigger;
    }

    private bool ValidateRuntimeReferences()
    {
        if (playerRigidbody == null ||
            playerCollider == null ||
            playerAnimator == null ||
            !IsValidCameraTransform(cameraTransform) ||
            groundProbe == null ||
            characterDefinition == null ||
            characterDefinition.movementProfile == null ||
            characterDefinition.abilityProfile == null ||
            characterDefinition.animatorProfile == null)
        {
            return false;
        }

        if (!HasValidPhysicsOwnership())
            return false;

        if (!playerRigidbody.gameObject.activeInHierarchy ||
            !groundProbe.gameObject.scene.IsValid() ||
            !groundProbe.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!playerCollider.enabled)
            playerCollider.enabled = true;

        if (!playerAnimator.enabled)
            playerAnimator.enabled = true;

        if (playerRigidbody.isKinematic &&
            !isStabilizingSpawn &&
            !isRecovering)
        {
            playerRigidbody.isKinematic = false;
        }

        if (!playerRigidbody.useGravity &&
            !isStabilizingSpawn &&
            !isRecovering &&
            currentState != MovementState.Flying)
        {
            playerRigidbody.useGravity = true;
        }

        return true;
    }

    private void AttemptRuntimeReferenceRecovery()
    {
        ResolveDependencies();
        ConfigureCollider();

        if (playerRigidbody != null)
        {
            playerRigidbody.interpolation =
                RigidbodyInterpolation.Interpolate;

            playerRigidbody.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;

            if (!isStabilizingSpawn &&
                !isRecovering)
            {
                playerRigidbody.isKinematic = false;

                if (currentState != MovementState.Flying)
                {
                    playerRigidbody.useGravity = true;
                }
            }

            playerRigidbody.WakeUp();
        }

        if (characterDefinition != null)
        {
            ApplyCharacterDefinition();
        }

        if (playerAnimator != null)
        {
            playerAnimator.enabled = true;
        }

        ResolveCamera();
        ResolveGroundProbe();
    }

    private bool ValidateRuntimePhysicsState(
        bool logErrors)
    {
        if (playerRigidbody == null)
            return false;

        Vector3 position =
            playerRigidbody.position;

        Vector3 velocity =
            playerRigidbody.linearVelocity;

        Vector3 angularVelocity =
            playerRigidbody.angularVelocity;

        bool valid =
            IsFiniteVector(position) &&
            IsFiniteVector(velocity) &&
            IsFiniteVector(angularVelocity);

        if (!valid)
        {
            if (logErrors)
            {
                Debug.LogError(
                    $"Invalid Rigidbody values detected on '{name}'.",
                    this);
            }

            return false;
        }

        float maximumLinearSpeedSquared =
            maximumLinearSpeed *
            maximumLinearSpeed;

        float maximumAngularSpeedSquared =
            maximumAngularSpeed *
            maximumAngularSpeed;

        if (velocity.sqrMagnitude >
            maximumLinearSpeedSquared)
        {
            if (logErrors)
            {
                Debug.LogWarning(
                    $"Excessive linear velocity detected on '{name}'.",
                    this);
            }

            return false;
        }

        if (angularVelocity.sqrMagnitude >
            maximumAngularSpeedSquared)
        {
            if (logErrors)
            {
                Debug.LogWarning(
                    $"Excessive angular velocity detected on '{name}'.",
                    this);
            }

            return false;
        }

        return true;
    }

    private bool TryRecoverInvalidPhysicsState()
    {
        if (playerRigidbody == null)
            return false;

        Vector3 position =
            playerRigidbody.position;

        Vector3 linearVelocity =
            playerRigidbody.linearVelocity;

        Vector3 angularVelocity =
            playerRigidbody.angularVelocity;

        if (!IsFiniteVector(
                position))
        {
            return PerformWorldRecovery();
        }

        playerRigidbody.linearVelocity =
            IsFiniteVector(linearVelocity)
                ? Vector3.ClampMagnitude(
                    linearVelocity,
                    maximumLinearSpeed)
                : Vector3.zero;

        playerRigidbody.angularVelocity =
            IsFiniteVector(angularVelocity)
                ? Vector3.ClampMagnitude(
                    angularVelocity,
                    maximumAngularSpeed)
                : Vector3.zero;

        playerRigidbody.isKinematic = false;
        playerRigidbody.WakeUp();

        return true;
    }

    private bool ValidateTransformScale()
    {
        Vector3 scale =
            transform.lossyScale;

        return
            IsFiniteVector(scale) &&
            Mathf.Abs(scale.x) >=
                minimumValidScale &&
            Mathf.Abs(scale.y) >=
                minimumValidScale &&
            Mathf.Abs(scale.z) >=
                minimumValidScale;
    }

    private bool IsCharacterInsideBlockingGeometry()
    {
        if (playerCollider == null)
            return false;

        Bounds bounds =
            playerCollider.bounds;

        Vector3 halfExtents =
            bounds.extents -
            Vector3.one *
            overlapCheckPadding;

        halfExtents.x =
            Mathf.Max(
                halfExtents.x,
                0.01f);

        halfExtents.y =
            Mathf.Max(
                halfExtents.y,
                0.01f);

        halfExtents.z =
            Mathf.Max(
                halfExtents.z,
                0.01f);

        int overlapCount =
            Physics.OverlapBoxNonAlloc(
                bounds.center,
                halfExtents,
                blockingOverlaps,
                playerCollider.transform.rotation,
                groundMask,
                QueryTriggerInteraction.Ignore);

        for (int index = 0;
             index < overlapCount;
             index++)
        {
            Collider overlap =
                blockingOverlaps[index];

            blockingOverlaps[index] =
                null;

            if (overlap == null ||
                IsOwnCollider(overlap))
            {
                continue;
            }

            if (Physics.ComputePenetration(
                    playerCollider,
                    playerCollider.transform.position,
                    playerCollider.transform.rotation,
                    overlap,
                    overlap.transform.position,
                    overlap.transform.rotation,
                    out _,
                    out float penetrationDistance) &&
                penetrationDistance >
                    overlapCheckPadding)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateMissingGroundProtection()
    {
        if (grounded ||
            currentState == MovementState.Flying ||
            currentState == MovementState.Spring ||
            currentState == MovementState.HomingAttack ||
            currentState == MovementState.Grinding ||
            isRecovering ||
            isStabilizingSpawn)
        {
            missingGroundTimer = 0f;
            return;
        }

        missingGroundTimer +=
            Time.fixedDeltaTime;

        if (missingGroundTimer <
            missingGroundGraceTime)
        {
            return;
        }

        if (playerRigidbody != null &&
            playerRigidbody.linearVelocity.y <= 0f)
        {
            PerformWorldRecovery();
        }

        missingGroundTimer = 0f;
    }

    private void UpdateSafetyTimers()
    {
        if (recoveryWindowTimer > 0f)
        {
            recoveryWindowTimer -=
                Time.fixedDeltaTime;

            if (recoveryWindowTimer <= 0f)
            {
                recoveryWindowTimer = 0f;
                recoveryCountInWindow = 0;
            }
        }

        if (recoveryLockoutTimer > 0f)
        {
            recoveryLockoutTimer =
                Mathf.Max(
                    0f,
                    recoveryLockoutTimer -
                    Time.fixedDeltaTime);
        }
    }

    private bool CanStartRecovery()
    {
        if (isRecovering ||
            recoveryLockoutTimer > 0f ||
            shuttingDown ||
            applicationQuitting)
        {
            return false;
        }

        if (recoveryWindowTimer <= 0f)
        {
            recoveryWindowTimer =
                recoveryWindowDuration;

            recoveryCountInWindow = 0;
        }

        if (recoveryCountInWindow >=
            maximumRecoveriesPerWindow)
        {
            recoveryLockoutTimer =
                recoveryLockoutDuration;

            Debug.LogError(
                $"Recovery loop protection activated on '{name}'.",
                this);

            return false;
        }

        recoveryCountInWindow++;

        return true;
    }

    private void EnterSafetyShutdown(
        string reason)
    {
        if (safetyShutdown)
            return;

        safetyShutdown = true;
        movementEnabled = false;

        CancelSpawnStabilization();
        StopRigidbodyMotion();

        if (playerRigidbody != null)
        {
            playerRigidbody.useGravity = false;
            playerRigidbody.isKinematic = true;
        }

        Debug.LogError(
            $"{nameof(UltimatePlayerMovement)} entered safety shutdown on '{name}': {reason}",
            this);
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
            playerRigidbody == null ||
            currentState !=
                MovementState.Ground ||
            !CanUseAbility(
                profile => profile.canRoll))
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
        {
            StopRolling();
            return;
        }

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
        if (playerRigidbody == null)
        {
            ChangeState(
                grounded
                    ? MovementState.Ground
                    : MovementState.Air);

            return;
        }

        if (!IsFiniteVector(
                playerRigidbody.linearVelocity))
        {
            ChangeState(
                MovementState.Air);

            return;
        }

        if (playerRigidbody.linearVelocity.y <= 0f)
        {
            ChangeState(
                grounded
                    ? MovementState.Ground
                    : MovementState.Air);

            return;
        }

        UpdateAirMovement(
            input);
    }

    private void UpdateHurtMovement()
    {
        if (playerRigidbody == null)
        {
            ChangeState(
                grounded
                    ? MovementState.Ground
                    : MovementState.Air);

            return;
        }

        if (!IsFiniteVector(
                playerRigidbody.linearVelocity))
        {
            playerRigidbody.linearVelocity =
                Vector3.zero;

            stateTimer = 0f;

            ChangeState(
                grounded
                    ? MovementState.Ground
                    : MovementState.Air);

            return;
        }

        stateTimer =
            Mathf.Max(
                0f,
                stateTimer -
                Time.fixedDeltaTime);

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
        {
            StopFlying();
            return;
        }

        if (!IsFiniteVector(
                playerRigidbody.linearVelocity))
        {
            playerRigidbody.linearVelocity =
                Vector3.zero;

            StopFlying();
            return;
        }

        if (!IsFiniteVector(input))
        {
            input =
                Vector3.zero;
        }

        float verticalInput =
            0f;

        if (Input.GetKey(jumpKey))
        {
            verticalInput +=
                1f;
        }

        if (Input.GetKey(flyDownKey))
        {
            verticalInput -=
                1f;
        }

        Vector3 horizontalInput =
            Vector3.ProjectOnPlane(
                input,
                Vector3.up);

        if (horizontalInput.sqrMagnitude >
            1f)
        {
            horizontalInput.Normalize();
        }

        Vector3 velocity =
            horizontalInput *
            flyingSpeed;

        velocity.y =
            verticalInput *
            flyingVerticalSpeed;

        if (!IsFiniteVector(velocity))
        {
            playerRigidbody.linearVelocity =
                Vector3.zero;

            StopFlying();
            return;
        }

        playerRigidbody.linearVelocity =
            velocity;

        if (horizontalInput.sqrMagnitude <=
            0.001f)
        {
            return;
        }

        RotateTowards(
            horizontalInput,
            Vector3.up,
            turnSpeed);
    }

    private void UpdatePowerActionMovement(
    Vector3 input)
    {
        if (playerRigidbody == null)
        {
            StopPowerAction();
            return;
        }

        if (!IsFiniteVector(
                playerRigidbody.linearVelocity))
        {
            playerRigidbody.linearVelocity =
                Vector3.zero;

            StopPowerAction();
            return;
        }

        if (!IsFiniteVector(input))
        {
            input =
                Vector3.zero;
        }

        if (grounded)
        {
            UpdateGroundMovement(
                input);

            return;
        }

        UpdateAirMovement(
            input);
    }

    #endregion

    #region Rotation

    private void RotateTowards(
    Vector3 direction,
    Vector3 up,
    float speed)
    {
        if (playerRigidbody == null)
            return;

        if (!IsFiniteVector(direction) ||
            !IsFiniteVector(up))
        {
            return;
        }

        if (direction.sqrMagnitude <=
            0.001f)
        {
            return;
        }

        if (up.sqrMagnitude <=
            0.000001f)
        {
            up =
                Vector3.up;
        }

        direction.Normalize();
        up.Normalize();

        if (Mathf.Abs(
                Vector3.Dot(
                    direction,
                    up)) >=
            0.999f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction,
                up);

        if (!IsFiniteQuaternion(
                targetRotation))
        {
            return;
        }

        float rotationSpeed =
            Mathf.Max(
                0f,
                speed);

        float interpolation =
            Mathf.Clamp01(
                rotationSpeed *
                Time.fixedDeltaTime);

        Quaternion nextRotation =
            Quaternion.Slerp(
                playerRigidbody.rotation,
                targetRotation,
                interpolation);

        if (!IsFiniteQuaternion(
                nextRotation))
        {
            return;
        }

        playerRigidbody.MoveRotation(
            nextRotation);
    }

    #endregion

    #region Animation

    private void CacheAnimatorParameters()
    {
        animatorParameters.Clear();

        if (playerAnimator == null)
            return;

        if (!playerAnimator.enabled)
        {
            playerAnimator.enabled =
                true;
        }

        RuntimeAnimatorController controller =
            playerAnimator.runtimeAnimatorController;

        if (controller == null)
            return;

        AnimatorControllerParameter[] parameters =
            playerAnimator.parameters;

        if (parameters == null ||
            parameters.Length == 0)
        {
            return;
        }

        for (int index = 0;
             index < parameters.Length;
             index++)
        {
            AnimatorControllerParameter parameter =
                parameters[index];

            animatorParameters.Add(
                parameter.nameHash);
        }
    }

    private bool HasAnimatorParameter(
    int parameterHash)
    {
        if (playerAnimator == null)
            return false;

        if (!playerAnimator.enabled)
            return false;

        if (playerAnimator.runtimeAnimatorController ==
            null)
        {
            return false;
        }

        return animatorParameters.Contains(
            parameterHash);
    }

    private void UpdateAnimator()
    {
        if (playerAnimator == null ||
            playerRigidbody == null)
        {
            return;
        }

        if (!playerAnimator.enabled ||
            playerAnimator.runtimeAnimatorController ==
                null)
        {
            return;
        }

        Vector3 velocity =
            playerRigidbody.linearVelocity;

        if (!IsFiniteVector(velocity))
        {
            velocity =
                Vector3.zero;
        }

        Vector3 horizontalVelocity =
            new(
                velocity.x,
                0f,
                velocity.z);

        float horizontalSpeed =
            horizontalVelocity.magnitude;

        if (!float.IsFinite(
                horizontalSpeed))
        {
            horizontalSpeed =
                0f;
        }

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
                horizontalSpeed);
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
        if (playerAnimator == null)
            return;

        if (!playerAnimator.enabled ||
            playerAnimator.runtimeAnimatorController ==
                null)
        {
            return;
        }

        if (!IsValidMovementState(
                currentState))
        {
            RecoverInvalidMovementState();
            return;
        }

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
        bool valid =
            true;

        valid &=
            ValidateReference(
                characterDefinition,
                nameof(CharacterDefinition));

        valid &=
            ValidateReference(
                playerRigidbody,
                nameof(Rigidbody));

        valid &=
            ValidateReference(
                playerCollider,
                nameof(Collider));

        valid &=
            ValidateReference(
                playerAnimator,
                nameof(Animator));

        valid &=
            ValidateReference(
                cameraTransform,
                "Camera Transform");

        valid &=
            ValidateReference(
                groundProbe,
                GroundProbeName);

        if (characterDefinition == null)
            return false;

        if (!ValidateCharacterDefinition(
                characterDefinition))
        {
            valid =
                false;
        }

        if (playerRigidbody != null)
        {
            if (playerRigidbody.isKinematic)
            {
                Debug.LogError(
                    $"{nameof(UltimatePlayerMovement)} requires a non-kinematic Rigidbody on '{name}'.",
                    this);

                valid =
                    false;
            }
        }

        if (playerCollider != null &&
            !playerCollider.enabled)
        {
            Debug.LogError(
                $"{nameof(UltimatePlayerMovement)} requires an enabled Collider on '{name}'.",
                this);

            valid =
                false;
        }

        if (playerAnimator != null &&
            !playerAnimator.enabled)
        {
            Debug.LogError(
                $"{nameof(UltimatePlayerMovement)} requires an enabled Animator on '{name}'.",
                this);

            valid =
                false;
        }

        if (!ValidateTransformScale())
        {
            Debug.LogError(
                $"{nameof(UltimatePlayerMovement)} detected an invalid transform scale on '{name}'.",
                this);

            valid =
                false;
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
            $"{nameof(UltimatePlayerMovement)} requires {displayName}.",
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

        if (string.IsNullOrWhiteSpace(
                message))
        {
            return;
        }

        if (shuttingDown ||
            applicationQuitting)
        {
            return;
        }

        Debug.Log(
            message,
            this);
    }

    #endregion
}