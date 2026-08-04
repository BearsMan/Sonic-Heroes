using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CameraController))]
[RequireComponent(typeof(RailGrinding))]
public sealed class UltimatePlayerMovement : MonoBehaviour
{
    #region Constants

    [Header("Movement Constants")]
    [SerializeField, Min(0f)] private float acceleration = 5f;
    [SerializeField, Min(0f)] private float deceleration = 3f;

    [SerializeField, Min(0f)] private float groundCheckRadius = 0.3f;
    private const string GroundCheckName = "GroundCheck";

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

    [Header("References")]
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private TeamActionController teamController;

    [Header("Character Configuration")]
    [SerializeField] private CharacterDefinition characterDefinition;

    [Header("Grinding")]
    [SerializeField] private RailGrinding grinding;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float runSpeed = 20f;
    [SerializeField, Min(0f)] private float airSpeed = 15f;
    [SerializeField, Min(0f)] private float turnSpeed = 15f;
    [SerializeField, Min(0f)] private float jumpForce = 10f;
    [SerializeField, Min(0f)] private float airControl = 20f;

    [Header("Advanced Movement")]
    [SerializeField, Min(0f)] private float brakingForce = 60f;
    [SerializeField, Min(0f)] private float slopeAcceleration = 15f;
    [SerializeField, Range(0f, 89f)] private float maximumSlopeAngle = 55f;
    [SerializeField, Min(0f)] private float coyoteTime = 0.15f;
    [SerializeField, Min(0f)] private float jumpBufferTime = 0.1f;

    [Header("Rolling")]
    [SerializeField, Min(0f)] private float rollingSpeed = 25f;
    [SerializeField, Min(0f)] private float rollingTurnSpeed = 6f;
    [SerializeField] private KeyCode rollKey = KeyCode.LeftShift;

    [Header("Homing Attack")]
    [SerializeField, Min(0f)] private float homingAttackSpeed = 35f;
    [SerializeField, Min(0f)] private float homingAttackDuration = 0.5f;
    [SerializeField] private KeyCode homingAttackKey = KeyCode.Mouse0;

    [Header("Targeting")]
    [SerializeField, Min(0f)] private float homingRange = 20f;
    [SerializeField] private LayerMask homingTargetMask;

    [Header("Flying")]
    [SerializeField, Min(0f)] private float flyingSpeed = 15f;
    [SerializeField, Min(0f)] private float flyingVerticalSpeed = 10f;

    [Header("Hurt")]
    [SerializeField, Min(0f)] private float defaultHurtDuration = 1f;

    [Header("Player State")]
    [SerializeField] private PlayerState currentState = PlayerState.Ground;
    [SerializeField] private bool isGrounded;

    #endregion

    #region Runtime State

    private readonly HashSet<int> animatorParameters = new();

    private Coroutine surrenderRoutine;

    private Vector3 homingDirection;

    private float currentGroundSpeed;
    
    private Vector3 groundNormal = Vector3.up;

    private float currentSlopeAngle;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float maxAirSpeed;
    private float stateTimer;

    private bool isBraking;
    private bool isGroundedLastFrame;
    private bool isInitialized;
    private bool isSurrendered;
    private bool isShuttingDown;

    #endregion

    #region Public API

    public enum PlayerState
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

    public bool IsGrounded => isGrounded;
    public bool IsInitialized => isInitialized;
    public bool IsSurrendered => isSurrendered;
    public PlayerState CurrentState => currentState;

    public bool TrickZone { get; internal set; }

    public bool MovementEnabled { get; private set; } = true;

    // Preserved for compatibility with existing project code.
    public object LeftTeamMember { get; internal set; }
    public object RightTeamMember { get; internal set; }
    public object TeamSetup { get; private set; }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheComponents();
        ResolveReferences();
        ApplyCharacterDefinition();
        InitializeCachedValues();
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
        if (isShuttingDown)
            return;

        CacheComponents();
        ResolveReferences();
        ConfigureComponents();

        if (isInitialized)
        {
            RestoreRuntimeState();
        }
    }

    private void Update()
    {
        if (!isInitialized)
            return;

        UpdateJumpTimers();
        UpdateAnimation();

        if (!CanMove())
            return;

        HandleJumpInput();
        HandleRollingInput();
        HandleHomingAttackInput();
    }

    private void FixedUpdate()
    {
        if (!isInitialized)
            return;

        UpdateGroundStateTransitions();

        if (!CanMove())
            return;

        Vector3 movementInput =
            GetMovementInput();

        switch (currentState)
        {
            case PlayerState.Ground:
                UpdateGroundState(movementInput);
                break;

            case PlayerState.Air:
                UpdateAirState(movementInput);
                break;

            case PlayerState.Rolling:
                UpdateRollingState(movementInput);
                break;

            case PlayerState.HomingAttack:
                UpdateHomingAttackState();
                break;

            case PlayerState.Grinding:
                UpdateGrindingState();
                break;

            case PlayerState.Spring:
                UpdateSpringState(movementInput);
                break;

            case PlayerState.Hurt:
                UpdateHurtState();
                break;

            case PlayerState.Flying:
                UpdateFlyingState(movementInput);
                break;

            case PlayerState.PowerAction:
                UpdatePowerActionState(movementInput);
                break;

            default:
                Debug.LogWarning(
                    $"Unsupported player state: {currentState}.",
                    this);

                ChangeState(
                    isGrounded
                        ? PlayerState.Ground
                        : PlayerState.Air);
                break;
        }

        if (currentState != PlayerState.Grinding)
        {
            RotateToGround();
        }
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
        acceleration =
            Mathf.Max(
                0f,
                acceleration);

        deceleration =
            Mathf.Max(
                0f,
                deceleration);

        groundCheckRadius =
            Mathf.Max(
                0f,
                groundCheckRadius);

        runSpeed =
            Mathf.Max(
                0f,
                runSpeed);

        airSpeed =
            Mathf.Max(
                0f,
                airSpeed);

        turnSpeed =
            Mathf.Max(
                0f,
                turnSpeed);

        jumpForce =
            Mathf.Max(
                0f,
                jumpForce);

        airControl =
            Mathf.Max(
                0f,
                airControl);

        brakingForce =
            Mathf.Max(
                0f,
                brakingForce);

        slopeAcceleration =
            Mathf.Max(
                0f,
                slopeAcceleration);

        maximumSlopeAngle =
            Mathf.Clamp(
                maximumSlopeAngle,
                0f,
                89f);

        coyoteTime =
            Mathf.Max(
                0f,
                coyoteTime);

        jumpBufferTime =
            Mathf.Max(
                0f,
                jumpBufferTime);

        rollingSpeed =
            Mathf.Max(
                0f,
                rollingSpeed);

        rollingTurnSpeed =
            Mathf.Max(
                0f,
                rollingTurnSpeed);

        homingAttackSpeed =
            Mathf.Max(
                0f,
                homingAttackSpeed);

        homingAttackDuration =
            Mathf.Max(
                0f,
                homingAttackDuration);

        homingRange =
            Mathf.Max(
                0f,
                homingRange);

        flyingSpeed =
            Mathf.Max(
                0f,
                flyingSpeed);

        flyingVerticalSpeed =
            Mathf.Max(
                0f,
                flyingVerticalSpeed);

        defaultHurtDuration =
            Mathf.Max(
                0f,
                defaultHurtDuration);

        if (!Enum.IsDefined(
                typeof(PlayerState),
                currentState))
        {
            currentState =
                PlayerState.Ground;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundCheckRadius);
    }

    #endregion

    #region Initialization

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
        return true;
    }

    private void ApplyCharacterDefinition()
    {
        if (characterDefinition == null)
            return;

        ApplyMovementProfile(
            characterDefinition.movementProfile);

        ApplyAnimatorProfile(
            characterDefinition.animatorProfile);
    }

    private void ApplyMovementProfile(
        CharacterMovementProfile profile)
    {
        if (profile == null)
            return;

        runSpeed = profile.runSpeed;
        acceleration = profile.acceleration;
        deceleration = profile.deceleration;
        turnSpeed = profile.turnSpeed;
        brakingForce = profile.brakingForce;

        jumpForce = profile.jumpForce;
        coyoteTime = profile.coyoteTime;
        jumpBufferTime = profile.jumpBufferTime;

        airSpeed = profile.airSpeed;
        airControl = profile.airControl;

        slopeAcceleration =
            profile.slopeAcceleration;

        maximumSlopeAngle =
            profile.maximumSlopeAngle;

        rollingSpeed = profile.rollingSpeed;
        rollingTurnSpeed = profile.rollingTurnSpeed;

        homingAttackSpeed =
            profile.homingAttackSpeed;

        homingAttackDuration =
            profile.homingAttackDuration;

        homingRange = profile.homingRange;

        flyingSpeed = profile.flyingSpeed;

        flyingVerticalSpeed =
            profile.flyingVerticalSpeed;

        defaultHurtDuration =
            profile.defaultHurtDuration;

        maxAirSpeed =
            Mathf.Max(
                0f,
                airSpeed);
    }

    private void ApplyAnimatorProfile(
        CharacterAnimatorProfile profile)
    {
        if (profile == null ||
            playerAnimator == null)
        {
            return;
        }

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

        RefreshAnimatorParameterCache();
    }

    public bool InitializeMovement()
    {
        if (isInitialized)
            return true;

        CacheComponents();
        ResolveReferences();
        ConfigureComponents();
        ApplyCharacterDefinition();
        InitializeCachedValues();

        if (!ValidateConfiguration())
        {
            isInitialized = false;

            Debug.LogError(
                $"UltimatePlayerMovement failed to initialize on '{name}'.",
                this);

            return false;
        }

        RefreshAnimatorParameterCache();

        isGrounded =
            CheckGrounded();

        isGroundedLastFrame =
            isGrounded;

        maxAirSpeed =
            Mathf.Max(
                0f,
                airSpeed);

        UpdateAnimatorState();
        UpdateAnimation();

        isInitialized = true;
        return true;
    }

    private void CacheComponents()
    {
        playerRigidbody ??=
            GetComponent<Rigidbody>();

        grinding ??=
            GetComponent<RailGrinding>();

        playerAnimator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        teamController ??=
            GetComponent<TeamActionController>();

        teamController ??=
            GetComponentInParent<TeamActionController>();
    }

    private void ResolveReferences()
    {
        ResolveCameraTransform();
        ResolveGroundCheck();

        TeamSetup =
            global::TeamSetup.Instance;
    }

    private void ResolveCameraTransform()
    {
        if (cameraTransform != null)
            return;

        CameraController cameraController =
            GetComponent<CameraController>();

        if (cameraController != null &&
            cameraController.PlayerCamera != null)
        {
            cameraTransform =
                cameraController.PlayerCamera.transform;

            return;
        }

        CameraController sceneCameraController =
            FindAnyObjectByType<CameraController>();

        if (sceneCameraController != null &&
            sceneCameraController.PlayerCamera != null)
        {
            cameraTransform =
                sceneCameraController.PlayerCamera.transform;

            return;
        }

        if (Camera.main != null)
        {
            cameraTransform =
                Camera.main.transform;
        }
    }

    private void ResolveGroundCheck()
    {
        if (groundCheck != null)
            return;

        Transform searchRoot =
            transform.parent != null
                ? transform.parent
                : transform;

        groundCheck =
            FindDescendantByName(
                searchRoot,
                GroundCheckName);

        if (groundCheck == null &&
            searchRoot != transform)
        {
            groundCheck =
                FindDescendantByName(
                    transform,
                    GroundCheckName);
        }
    }

    private void ConfigureComponents()
    {
        if (playerRigidbody != null)
        {
            playerRigidbody.constraints =
                RigidbodyConstraints.FreezeRotation;
        }
    }

    private void InitializeCachedValues()
    {
        maxAirSpeed =
            Mathf.Max(
                0f,
                airSpeed);

        currentGroundSpeed = 0f;
        currentSlopeAngle = 0f;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        stateTimer = 0f;

        groundNormal = Vector3.up;
        homingDirection = Vector3.zero;

        isBraking = false;
    }

    private void RestoreRuntimeState()
    {
        isSurrendered = false;

        RefreshAnimatorParameterCache();
        UpdateAnimatorState();
        UpdateAnimation();
    }

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
                groundCheck,
                GroundCheckName);

        valid &=
            ValidateReference(
                grinding,
                nameof(RailGrinding));

        if (playerAnimator == null)
        {
            Debug.LogWarning(
                "UltimatePlayerMovement could not find an Animator.",
                this);
        }

        if (teamController == null)
        {
            Debug.LogWarning(
                "UltimatePlayerMovement could not find a TeamActionController.",
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

    private static Transform FindDescendantByName(
        Transform root,
        string objectName)
    {
        if (root == null ||
            string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] descendants =
            root.GetComponentsInChildren<Transform>(
                includeInactive: true);

        foreach (Transform descendant in descendants)
        {
            if (descendant != null &&
                descendant.name == objectName)
            {
                return descendant;
            }
        }

        return null;
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        StopSurrenderRoutine();

        isSurrendered = false;

        if (playerRigidbody != null)
        {
            StopMovement();
        }
    }

    private void CleanupDestroyedState()
    {
        CleanupRuntimeState();

        isInitialized = false;

        animatorParameters.Clear();

        cameraTransform = null;
        groundCheck = null;
        playerAnimator = null;
        teamController = null;
        grinding = null;
        playerRigidbody = null;

        LeftTeamMember = null;
        RightTeamMember = null;
        TeamSetup = null;
    }

    #endregion

    #region Input

    private bool HasAbility(
    System.Func<CharacterAbilityProfile, bool> selector,
    bool defaultValue = true)
    {
        CharacterAbilityProfile profile =
            AbilityProfile;

        return profile == null
            ? defaultValue
            : selector(profile);
    }

    private Vector3 GetMovementInput()
    {
        if (cameraTransform == null)
            return Vector3.zero;

        float horizontal =
            Input.GetAxis("Horizontal");

        float vertical =
            Input.GetAxis("Vertical");

        Vector3 cameraForward =
            cameraTransform.forward;

        Vector3 cameraRight =
            cameraTransform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 movementInput =
            cameraForward * vertical +
            cameraRight * horizontal;

        return
            Vector3.ClampMagnitude(
                movementInput,
                1f);
    }


    private void HandleJumpInput()
    {
        if (!HasAbility(
        abilities => abilities.canJump))
        {
            return;
        }

        if (jumpBufferTimer <= 0f)
            return;

        bool canUseGroundJump =
            currentState == PlayerState.Ground &&
            (isGrounded || coyoteTimer > 0f);

        if (!canUseGroundJump ||
            playerRigidbody == null)
        {
            return;
        }

        Vector3 velocity =
            playerRigidbody.linearVelocity;

        velocity.y = 0f;

        playerRigidbody.linearVelocity =
            velocity;

        playerRigidbody.AddForce(
            transform.up * jumpForce,
            ForceMode.VelocityChange);

        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        isGrounded = false;

        OnJump();
    }

    private void UpdateJumpTimers()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferTimer =
                jumpBufferTime;
        }
        else if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer -=
                Time.deltaTime;
        }

        if (isGrounded)
        {
            coyoteTimer =
                coyoteTime;
        }
        else if (coyoteTimer > 0f)
        {
            coyoteTimer -=
                Time.deltaTime;
        }

        jumpBufferTimer =
            Mathf.Max(
                0f,
                jumpBufferTimer);

        coyoteTimer =
            Mathf.Max(
                0f,
                coyoteTimer);
    }

    private void HandleRollingInput()
    {
        if (!HasAbility(
        abilities => abilities.canRoll))
        {
            return;
        }

        if (Input.GetKeyDown(rollKey))
        {
            StartRolling();
        }

        if (Input.GetKeyUp(rollKey))
        {
            StopRolling();
        }
    }

    private void HandleHomingAttackInput()
    {
        if (!HasAbility(
        abilities => abilities.canHomingAttack))
        {
            return;
        }

        if (currentState != PlayerState.Air ||
            !Input.GetKeyDown(homingAttackKey))
        {
            return;
        }

        Transform homingTarget =
            FindHomingTarget();

        if (homingTarget == null)
            return;

        StartHomingAttack(
            homingTarget.position);
    }

    #endregion

    #region State Machine

    private void ChangeState(
        PlayerState newState)
    {
        if (currentState == newState)
            return;

        if (!Enum.IsDefined(
                typeof(PlayerState),
                newState))
        {
            Debug.LogWarning(
                $"UltimatePlayerMovement rejected unsupported state '{newState}'.",
                this);

            return;
        }

        ExitState(currentState);

        currentState =
            newState;

        EnterState(currentState);
        UpdateAnimatorState();
    }

    private void EnterState(
        PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Ground:
                teamController?.EnableFollowers();
                break;

            case PlayerState.Air:
                EnterAirState();
                break;

            case PlayerState.HomingAttack:
                stateTimer =
                    homingAttackDuration;
                break;

            case PlayerState.Hurt:
                if (stateTimer <= 0f)
                {
                    stateTimer =
                        defaultHurtDuration;
                }

                break;
        }
    }

    private void ExitState(
        PlayerState state)
    {
        switch (state)
        {
            case PlayerState.HomingAttack:
                homingDirection =
                    Vector3.zero;
                break;
        }
    }

    private void UpdateGroundStateTransitions()
    {
        isGroundedLastFrame =
            isGrounded;

        isGrounded =
            CheckGrounded();

        if (!isGroundedLastFrame &&
            isGrounded)
        {
            OnLanded();
        }

        if (isGroundedLastFrame &&
            !isGrounded)
        {
            OnLeftGround();
        }
    }

    private void OnLanded()
    {
        if (currentState == PlayerState.Grinding)
            return;

        ResetAirAbilities();
        ChangeState(PlayerState.Ground);
    }

    private void OnLeftGround()
    {
        if (currentState == PlayerState.Ground)
        {
            OnJump();
        }
    }

    private void OnJump()
    {
        ResetAirAbilities();
        ChangeState(PlayerState.Air);
    }

    private void EnterAirState()
    {
        maxAirSpeed =
            Mathf.Max(
                0f,
                airSpeed);
    }

    private void ResetAirAbilities()
    {
        homingDirection =
            Vector3.zero;

        stateTimer = 0f;
    }

    #endregion

    #region State Updates

    private void UpdateGroundState(
        Vector3 movementInput)
    {
        GroundMovement(movementInput);
        Turn(movementInput);
    }

    private void UpdateAirState(
        Vector3 movementInput)
    {
        AirMovement(movementInput);
    }

    private void UpdateRollingState(
    Vector3 movementInput)
    {
        
        if (playerRigidbody == null)
            return;

        UpdateGroundSurface();

        if (HasAbility(
            abilities => abilities.usesSlopeMomentum))
        {
            ApplySlopeAcceleration();
        }


        movementInput =
            Vector3.ProjectOnPlane(
                movementInput,
                groundNormal);

        bool hasMovementInput =
            movementInput.sqrMagnitude >
            0.001f;

        if (hasMovementInput)
        {
            movementInput.Normalize();
        }

        Vector3 currentVelocity =
            playerRigidbody.linearVelocity;

        Vector3 currentSurfaceVelocity =
            Vector3.ProjectOnPlane(
                currentVelocity,
                groundNormal);

        if (currentGroundSpeed <= 0f &&
            currentSurfaceVelocity.sqrMagnitude >
            0.001f)
        {
            currentGroundSpeed =
                currentSurfaceVelocity.magnitude;
        }

        currentGroundSpeed =
            Mathf.MoveTowards(
                currentGroundSpeed,
                rollingSpeed,
                acceleration *
                Time.fixedDeltaTime);

        

        Vector3 rollingDirection;

        if (currentSurfaceVelocity.sqrMagnitude >
            0.001f)
        {
            rollingDirection =
                currentSurfaceVelocity.normalized;
        }
        else
        {
            rollingDirection =
                Vector3.ProjectOnPlane(
                    transform.forward,
                    groundNormal).normalized;
        }

        if (hasMovementInput)
        {
            rollingDirection =
                Vector3.Slerp(
                    rollingDirection,
                    movementInput,
                    rollingTurnSpeed *
                    Time.fixedDeltaTime).normalized;
        }

        if (rollingDirection.sqrMagnitude <
            0.001f)
        {
            rollingDirection =
                transform.forward;
        }

        Vector3 rollingVelocity =
            rollingDirection *
            currentGroundSpeed;

        float normalVelocity =
            Vector3.Dot(
                currentVelocity,
                groundNormal);

        if (isGrounded &&
            normalVelocity < 0f)
        {
            normalVelocity = 0f;
        }

        playerRigidbody.linearVelocity =
            rollingVelocity +
            groundNormal *
            normalVelocity;

        if (rollingDirection.sqrMagnitude >
            0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(
                    rollingDirection,
                    groundNormal);

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rollingTurnSpeed *
                    Time.fixedDeltaTime);
        }

        if (!isGrounded)
        {
            ChangeState(
                PlayerState.Air);
        }
    }

    private void UpdateHomingAttackState()
    {
        if (playerRigidbody == null)
            return;

        stateTimer -=
            Time.fixedDeltaTime;

        playerRigidbody.linearVelocity =
            homingDirection *
            homingAttackSpeed;

        if (homingDirection.sqrMagnitude >
            0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(
                    homingDirection,
                    Vector3.up);

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    Time.fixedDeltaTime *
                    turnSpeed);
        }

        if (stateTimer <= 0f)
        {
            ChangeState(
                isGrounded
                    ? PlayerState.Ground
                    : PlayerState.Air);
        }
    }

    private void UpdateGrindingState()
    {
        if (grinding != null &&
            grinding.IsGrinding)
        {
            return;
        }

        ChangeState(PlayerState.Air);
    }

    private void UpdateSpringState(
        Vector3 movementInput)
    {
        AirMovement(movementInput);

        if (playerRigidbody != null &&
            playerRigidbody.linearVelocity.y <= 0f)
        {
            ChangeState(PlayerState.Air);
        }
    }

    private void UpdateHurtState()
    {
        stateTimer -=
            Time.fixedDeltaTime;

        if (stateTimer > 0f)
            return;

        ChangeState(
            isGrounded
                ? PlayerState.Ground
                : PlayerState.Air);
    }

    private void UpdateFlyingState(
        Vector3 movementInput)
    {
        if (playerRigidbody == null)
            return;

        float verticalInput = 0f;

        if (Input.GetKey(KeyCode.Space))
        {
            verticalInput += 1f;
        }

        if (Input.GetKey(KeyCode.LeftControl))
        {
            verticalInput -= 1f;
        }

        Vector3 flyingVelocity =
            movementInput *
            flyingSpeed;

        flyingVelocity.y =
            verticalInput *
            flyingVerticalSpeed;

        playerRigidbody.linearVelocity =
            flyingVelocity;

        Turn(movementInput);
    }

    private void UpdatePowerActionState(
        Vector3 movementInput)
    {
        if (isGrounded)
        {
            GroundMovement(movementInput);
            Turn(movementInput);
            return;
        }

        AirMovement(movementInput);
    }

    #endregion

    #region Movement

    private void GroundMovement(
    Vector3 movementInput)
    {
        bool canBrake =
            HasAbility(
                abilities => abilities.canBrake);

        isBraking =
            canBrake &&
            IsBraking(movementInput);

        if (playerRigidbody == null)
        {
            return;
        }

        UpdateGroundSurface();

        if (HasAbility(
            abilities =>
                abilities.usesSlopeMomentum))
        {
            ApplySlopeAcceleration();
        }

        movementInput =
            Vector3.ProjectOnPlane(
                movementInput,
                groundNormal);

        bool hasMovementInput =
            movementInput.sqrMagnitude >
            0.001f;

        if (hasMovementInput)
        {
            movementInput.Normalize();
        }

        Vector3 currentVelocity =
            playerRigidbody.linearVelocity;

        Vector3 currentSurfaceVelocity =
            Vector3.ProjectOnPlane(
                currentVelocity,
                groundNormal);

        if (currentGroundSpeed <= 0f &&
            currentSurfaceVelocity.sqrMagnitude >
            0.001f)
        {
            currentGroundSpeed =
                currentSurfaceVelocity.magnitude;
        }

        if (isBraking)
        {
            ApplyBraking();
        }
        else
        {
            float targetSpeed =
                hasMovementInput
                    ? runSpeed
                    : 0f;

            float movementRate =
                hasMovementInput
                    ? acceleration
                    : deceleration;

            currentGroundSpeed =
                Mathf.MoveTowards(
                    currentGroundSpeed,
                    targetSpeed,
                    movementRate *
                    Time.fixedDeltaTime);
        }

        Vector3 movementDirection;

        if (hasMovementInput)
        {
            movementDirection =
                movementInput;
        }
        else if (currentSurfaceVelocity.sqrMagnitude >
                 0.001f)
        {
            movementDirection =
                currentSurfaceVelocity.normalized;
        }
        else
        {
            movementDirection =
                Vector3.ProjectOnPlane(
                    transform.forward,
                    groundNormal).normalized;
        }

        if (movementDirection.sqrMagnitude <
            0.001f)
        {
            movementDirection =
                transform.forward;
        }

        Vector3 newSurfaceVelocity =
            movementDirection *
            currentGroundSpeed;

        float normalVelocity =
            Vector3.Dot(
                currentVelocity,
                groundNormal);

        if (isGrounded &&
            normalVelocity < 0f)
        {
            normalVelocity = 0f;
        }

        playerRigidbody.linearVelocity =
            newSurfaceVelocity +
            groundNormal *
            normalVelocity;
    }

    private void AirMovement(
    Vector3 movementInput)
    {
        if (playerRigidbody == null)
            return;

        Vector3 horizontalVelocity =
    playerRigidbody.linearVelocity;

        horizontalVelocity.y = 0f;

        currentGroundSpeed =
            Mathf.Max(
                currentGroundSpeed,
                horizontalVelocity.magnitude);

        playerRigidbody.AddForce(
            movementInput *
            airControl,
            ForceMode.Acceleration);

        horizontalVelocity.y = 0f;

        if (horizontalVelocity.magnitude <=
            maxAirSpeed)
        {
            return;
        }

        horizontalVelocity =
            horizontalVelocity.normalized *
            maxAirSpeed;

        horizontalVelocity.y =
            playerRigidbody.linearVelocity.y;

        playerRigidbody.linearVelocity =
            horizontalVelocity;
    }

    private void UpdateGroundSurface()
    {
        groundNormal = Vector3.up;
        currentSlopeAngle = 0f;

        if (groundCheck == null)
            return;

        Vector3 rayOrigin =
            groundCheck.position +
            transform.up *
            0.25f;

        float rayDistance =
            groundCheckRadius +
            0.75f;

        if (!Physics.Raycast(
                rayOrigin,
                -transform.up,
                out RaycastHit hit,
                rayDistance,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        groundNormal =
            hit.normal.normalized;

        currentSlopeAngle =
            Vector3.Angle(
                groundNormal,
                Vector3.up);
    }

    private bool IsOnSlope()
    {
        return
            isGrounded &&
            currentSlopeAngle > 0.01f &&
            currentSlopeAngle <=
                maximumSlopeAngle;
    }

    private void ApplySlopeAcceleration()
    {
        if (!IsOnSlope() ||
            currentGroundSpeed <= 0f)
        {
            return;
        }

        Vector3 downhillDirection =
            Vector3.ProjectOnPlane(
                Physics.gravity,
                groundNormal);

        if (downhillDirection.sqrMagnitude <
            0.001f)
        {
            return;
        }

        downhillDirection.Normalize();

        Vector3 movementDirection =
            Vector3.ProjectOnPlane(
                playerRigidbody.linearVelocity,
                groundNormal);

        if (movementDirection.sqrMagnitude <
            0.001f)
        {
            movementDirection =
                Vector3.ProjectOnPlane(
                    transform.forward,
                    groundNormal);
        }

        if (movementDirection.sqrMagnitude <
            0.001f)
        {
            return;
        }

        movementDirection.Normalize();

        float slopeInfluence =
            Vector3.Dot(
                movementDirection,
                downhillDirection);

        float normalizedSlope =
            maximumSlopeAngle > 0f
                ? Mathf.Clamp01(
                    currentSlopeAngle /
                    maximumSlopeAngle)
                : 0f;

        currentGroundSpeed +=
            slopeInfluence *
            slopeAcceleration *
            normalizedSlope *
            Time.fixedDeltaTime;

        currentGroundSpeed =
            Mathf.Max(
                0f,
                currentGroundSpeed);
    }

    private bool IsBraking(
        Vector3 movementInput)
    {
        if (movementInput.sqrMagnitude <
            0.001f ||
            playerRigidbody == null ||
            currentGroundSpeed <= 0.01f)
        {
            return false;
        }

        Vector3 currentDirection =
            Vector3.ProjectOnPlane(
                playerRigidbody.linearVelocity,
                groundNormal);

        if (currentDirection.sqrMagnitude <
            0.001f)
        {
            return false;
        }

        currentDirection.Normalize();

        float inputAlignment =
            Vector3.Dot(
                currentDirection,
                movementInput.normalized);

        return inputAlignment < -0.25f;
    }

    private void ApplyBraking()
    {
        currentGroundSpeed =
            Mathf.MoveTowards(
                currentGroundSpeed,
                0f,
                brakingForce *
                Time.fixedDeltaTime);
    }

    private void Turn(
        Vector3 movementInput)
    {
        if (movementInput.sqrMagnitude <
            0.001f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                movementInput,
                transform.up);

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.fixedDeltaTime *
                turnSpeed);
    }

    public void RotateToGround()
    {
        if (!isGrounded)
        {
            Vector3 cross =
                Vector3.Cross(
                    transform.right,
                    Vector3.up);

            if (cross.sqrMagnitude <=
                0.001f)
            {
                return;
            }

            Quaternion airRotation =
                Quaternion.LookRotation(
                    cross,
                    Vector3.up);

            transform.rotation =
                Quaternion.LerpUnclamped(
                    transform.rotation,
                    airRotation,
                    Time.deltaTime *
                    100f);

            return;
        }

        Vector3 origin =
            transform.position +
            transform.up *
            0.5f;

        if (!Physics.Raycast(
                origin,
                -transform.up,
                out RaycastHit hit,
                2f,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        Vector3 newUp =
            hit.normal;

        float angle =
            Vector3.Angle(
                transform.up,
                newUp);

        if (angle > 30f)
            return;

        Vector3 groundDirection =
            Vector3.Cross(
                transform.right,
                newUp);

        if (groundDirection.sqrMagnitude <=
            0.001f)
        {
            return;
        }

        Quaternion groundRotation =
            Quaternion.LookRotation(
                groundDirection,
                newUp);

        transform.rotation =
            Quaternion.LerpUnclamped(
                transform.rotation,
                groundRotation,
                Time.deltaTime *
                100f);
    }

    private bool CheckGrounded()
    {
        if (groundCheck == null)
            return false;

        return
            Physics.CheckSphere(
                groundCheck.position,
                groundCheckRadius,
                groundMask,
                QueryTriggerInteraction.Ignore);
    }

    private bool CanMove()
    {
        return
            MovementEnabled &&
            !isSurrendered &&
            HasRequiredRuntimeReferences();
    }

    private bool HasRequiredRuntimeReferences()
    {
        return
            playerRigidbody != null &&
            cameraTransform != null &&
            groundCheck != null;
    }

    public void StopMovement()
    {
        if (playerRigidbody == null)
            return;

        playerRigidbody.linearVelocity =
            Vector3.zero;

        playerRigidbody.angularVelocity =
            Vector3.zero;

        currentGroundSpeed = 0f;
        isBraking = false;
    }

    public void EnableMovement()
    {
        MovementEnabled = true;
    }

    public void DisableMovement()
    {
        MovementEnabled = false;
        StopMovement();
    }

    #endregion

    #region Actions

    public void StartRolling()
    {
        if (!HasAbility(
            abilities => abilities.canRoll))
        {
            return;
        }

        if (!isInitialized ||
            !isGrounded ||
            currentState != PlayerState.Ground ||
            playerRigidbody == null)
        {
            return;
        }

        Vector3 surfaceVelocity =
            Vector3.ProjectOnPlane(
                playerRigidbody.linearVelocity,
                groundNormal);

        currentGroundSpeed =
            Mathf.Max(
                currentGroundSpeed,
                surfaceVelocity.magnitude);

        ChangeState(
            PlayerState.Rolling);
    }

    public void StopRolling()
    {
        if (currentState != PlayerState.Rolling)
            return;

        ChangeState(
            isGrounded
                ? PlayerState.Ground
                : PlayerState.Air);
    }

    public void StartHomingAttack(
        Vector3 targetPosition)
    {
        if (!HasAbility(
            abilities => abilities.canHomingAttack))
        {
            return;
        }

        if (!isInitialized ||
            playerRigidbody == null ||
            currentState == PlayerState.Hurt)
        {
            return;
        }

        homingDirection =
            (targetPosition -
             playerRigidbody.position).normalized;

        if (homingDirection.sqrMagnitude <
            0.001f)
        {
            return;
        }

        ChangeState(
            PlayerState.HomingAttack);
    }

    public void EnterGrindingState()
    {
        if (!HasAbility(
        abilities => abilities.canGrind))
        {
            return;
        }

        if (!isInitialized ||
            currentState == PlayerState.Hurt)
        {
            return;
        }

        ChangeState(PlayerState.Grinding);
    }

    public void ExitGrindingState()
    {
        if (currentState !=
            PlayerState.Grinding)
        {
            return;
        }

        ChangeState(PlayerState.Air);
    }

    public void LaunchFromSpring(
        Vector3 launchDirection,
        float height)
    {
        if (!isInitialized ||
            playerRigidbody == null)
        {
            return;
        }

        Vector3 launchVelocity =
            CalculateLaunchVelocity(
                launchDirection,
                height);

        playerRigidbody.linearVelocity =
            launchVelocity;

        ChangeState(PlayerState.Spring);
    }

    public void EnterHurtState(
        Vector3 knockbackVelocity,
        float duration)
    {
        if (!isInitialized ||
            playerRigidbody == null)
        {
            return;
        }

        playerRigidbody.linearVelocity =
            knockbackVelocity;

        stateTimer =
            Mathf.Max(
                0f,
                duration);

        ChangeState(PlayerState.Hurt);
    }

    public void StartFlying()
    {
        if (!HasAbility(
            abilities => abilities.canFly))
        {
            return;
        }

        if (!isInitialized ||
            currentState == PlayerState.Hurt)
        {
            return;
        }

        ChangeState(PlayerState.Flying);
    }

    public void StopFlying()
    {
        if (currentState != PlayerState.Flying)
            return;

        ChangeState(
            isGrounded
                ? PlayerState.Ground
                : PlayerState.Air);
    }

    public void StartPowerAction()
    {
        if (!HasAbility(
            abilities => abilities.canPowerAction))
        {
            return;
        }

        if (!isInitialized ||
            currentState == PlayerState.Hurt)
        {
            return;
        }

        ChangeState(PlayerState.PowerAction);
    }

    public void StopPowerAction()
    {
        if (currentState != PlayerState.PowerAction)
            return;

        ChangeState(
            isGrounded
                ? PlayerState.Ground
                : PlayerState.Air);
    }

    public void Launch(
        Vector3 launchDirection,
        float height)
    {
        if (!isInitialized ||
            playerRigidbody == null)
        {
            return;
        }

        playerRigidbody.linearVelocity =
            CalculateLaunchVelocity(
                launchDirection,
                height);
    }

    private static Vector3 CalculateLaunchVelocity(
        Vector3 launchDirection,
        float height)
    {
        float safeHeight =
            Mathf.Max(
                0f,
                height);

        float gravityMagnitude =
            Mathf.Abs(
                Physics.gravity.y);

        float launchSpeed =
            gravityMagnitude > 0f
                ? Mathf.Sqrt(
                    safeHeight *
                    2f *
                    gravityMagnitude)
                : 0f;

        Vector3 direction =
            launchDirection.sqrMagnitude >
            0.001f
                ? launchDirection.normalized
                : Vector3.up;

        return
            direction *
            launchSpeed;
    }

    public void SurrenderControl(
        Vector2 up,
        float newSurrenderTime)
    {
        _ = up;

        StopSurrenderRoutine();

        if (!isActiveAndEnabled)
        {
            isSurrendered = false;
            return;
        }

        surrenderRoutine =
            StartCoroutine(
                Surrender(
                    Mathf.Max(
                        0f,
                        newSurrenderTime)));
    }

    private IEnumerator Surrender(
        float duration)
    {
        isSurrendered = true;

        if (duration > 0f)
        {
            yield return
                new WaitForSeconds(
                    duration);
        }

        isSurrendered = false;
        surrenderRoutine = null;
    }

    private void StopSurrenderRoutine()
    {
        if (surrenderRoutine == null)
            return;

        StopCoroutine(
            surrenderRoutine);

        surrenderRoutine = null;
        isSurrendered = false;
    }

    #endregion

    #region Targeting

    private Transform FindHomingTarget()
    {
        Collider[] targets =
            Physics.OverlapSphere(
                transform.position,
                homingRange,
                homingTargetMask,
                QueryTriggerInteraction.Ignore);

        Transform closestTarget = null;

        float closestDistance =
            float.MaxValue;

        foreach (Collider targetCollider in targets)
        {
            if (targetCollider == null)
                continue;

            float distance =
                Vector3.SqrMagnitude(
                    targetCollider.transform.position -
                    transform.position);

            if (distance >= closestDistance)
                continue;

            closestDistance = distance;

            closestTarget =
                targetCollider.transform;
        }

        return closestTarget;
    }

    #endregion

    #region Animation

    public void SetupAnimation()
    {
        CacheComponents();

        playerAnimator =
            GetComponentInChildren<Animator>(
                includeInactive: true);

        RefreshAnimatorParameterCache();
        UpdateAnimatorState();
        UpdateAnimation();
    }

    private void RefreshAnimatorParameterCache()
    {
        animatorParameters.Clear();

        if (playerAnimator == null ||
            playerAnimator.runtimeAnimatorController == null)
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

    private void UpdateAnimation()
    {
        if (playerAnimator == null ||
            playerRigidbody == null)
        {
            return;
        }

        Vector3 horizontalVelocity =
            playerRigidbody.linearVelocity;

        horizontalVelocity.y = 0f;

        if (HasAnimatorParameter(
                GroundedHash))
        {
            playerAnimator.SetBool(
                GroundedHash,
                isGrounded);
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
                playerRigidbody.linearVelocity.y);
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
}
