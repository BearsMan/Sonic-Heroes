using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public abstract class TrackVehicle : MonoBehaviour
{
    #region Types

    public enum VehicleState
    {
        Waiting,
        Boarding,
        Driving,
        Airborne,
        Stopping,
        Dismounting,
        Finished,
        Disabled
    }

    #endregion

    #region Constants

    private const string PathRootName = "Path";
    private const string SeatPointName = "Seat Point";
    private const string SeatPointPrefix = "Seat Point ";
    private const string ExitPointName = "Exit Point";
    private const string LeaderSlotName = "Team Leader";
    private const string LeftTeamMemberSlotName = "Left Team Member";
    private const string RightTeamMemberSlotName = "Right Team Member";

    private const int MaximumRiderCapacity = 3;
    private const int GroundHitCapacity = 16;

    private const float MinimumDirectionSqrMagnitude = 0.0001f;
    private const float MinimumSpeedThreshold = 0.01f;

    private const RigidbodyConstraints DefaultConstraints =
        RigidbodyConstraints.FreezeRotationX |
        RigidbodyConstraints.FreezeRotationZ;

    private static readonly int DrivingHash =
        Animator.StringToHash("Driving");

    private static readonly int GroundedHash =
        Animator.StringToHash("Grounded");

    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private static readonly int JumpHash =
        Animator.StringToHash("Jump");

    private static readonly int StopHash =
        Animator.StringToHash("Stop");

    #endregion

    #region Inspector

    [Header("Dependencies")]
    [SerializeField] private Rigidbody vehicleRigidbody;
    [SerializeField] private Collider vehicleCollider;
    [SerializeField] private Animator vehicleAnimator;
    [SerializeField] private AudioSource vehicleAudioSource;

    [Header("Track")]
    [SerializeField] private Transform pathRoot;
    [SerializeField]
    private Transform[] pathPoints =
        Array.Empty<Transform>();
    [SerializeField] private bool resolvePathAutomatically = true;
    [SerializeField] private bool loopPath;
    [SerializeField] private bool startAutomatically;
    [SerializeField] private bool stopAtFinalPoint = true;
    [SerializeField, Min(0.01f)] private float pointArrivalDistance = 0.75f;

    [Header("Automatic Driving")]
    [SerializeField, Min(0f)] private float maximumSpeed = 35f;
    [SerializeField, Min(0f)] private float minimumDrivingSpeed = 8f;
    [SerializeField, Min(0f)] private float acceleration = 20f;
    [SerializeField, Min(0f)] private float deceleration = 25f;
    [SerializeField, Min(0f)] private float rotationSpeed = 10f;

    [Header("Limited Steering")]
    [SerializeField] private bool allowSteering = true;
    [SerializeField, Min(0f)] private float steeringSpeed = 5f;
    [SerializeField, Min(0f)] private float maximumTrackOffset = 2.5f;
    [SerializeField, Min(0f)] private float steeringReturnSpeed = 8f;

    [Header("Jump")]
    [SerializeField] private bool allowJump = true;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField, Min(0f)] private float jumpVelocity = 10f;
    [SerializeField, Min(0f)] private float jumpCooldown = 0.25f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField, Min(0.01f)] private float groundProbeRadius = 0.35f;
    [SerializeField, Min(0.01f)] private float groundProbeDistance = 1.25f;
    [SerializeField, Range(0f, 89f)] private float maximumGroundSlope = 60f;

    [Header("Boarding")]
    [SerializeField] private bool allowBoarding = true;
    [SerializeField] private bool boardOnTrigger = true;
    [SerializeField] private bool requirePlayerTag = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Transform seatPoint;
    [SerializeField]
    private Transform[] seatPoints =
        Array.Empty<Transform>();
    [SerializeField] private Transform exitPoint;
    [SerializeField, Range(1, MaximumRiderCapacity)]
    private int maximumRiders = MaximumRiderCapacity;

    [Header("Automatic Full-Team Boarding")]
    [SerializeField] private bool boardEntireTeamAutomatically = true;
    [SerializeField] private bool requireCompleteTeam;
    [SerializeField] private bool preserveFormationOrder = true;
    [SerializeField, Min(0.1f)] private float teammateSearchRadius = 15f;

    [Header("Track Stoppers")]
    [SerializeField] private bool stopAtCheckpoint;
    [SerializeField] private bool dismountAtCheckpoint;
    [SerializeField] private bool finishAtCheckpoint;
    [SerializeField] private bool destroyAfterFinish;
    [SerializeField, Min(0f)] private float destroyDelay = 1f;

    [Header("Presentation")]
    [SerializeField] private AudioClip driveLoop;
    [SerializeField] private AudioClip jumpClip;
    [SerializeField] private AudioClip stopClip;
    [SerializeField] private ParticleSystem driveEffect;
    [SerializeField] private ParticleSystem jumpEffect;
    [SerializeField] private ParticleSystem stopEffect;

    [Header("Physics Safety")]
    [SerializeField] private bool enablePhysicsSafety = true;
    [SerializeField, Min(1f)] private float maximumLinearSpeed = 100f;
    [SerializeField, Min(1f)] private float maximumAngularSpeed = 50f;
    [SerializeField, Min(0.01f)] private float minimumValidScale = 0.01f;

    [Header("Runtime Recovery")]
    [SerializeField] private bool restoreDisabledComponents = true;
    [SerializeField, Min(0.1f)] private float safetyCheckInterval = 0.5f;

    [Header("State")]
    [SerializeField]
    private VehicleState startingState =
        VehicleState.Waiting;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private readonly List<UltimatePlayerMovement> riders =
        new(MaximumRiderCapacity);

    private readonly List<Transform> originalRiderParents =
        new(MaximumRiderCapacity);

    private readonly List<bool> originalMovementComponentStates =
        new(MaximumRiderCapacity);

    private readonly List<bool> originalRiderKinematicStates =
        new(MaximumRiderCapacity);

    private readonly List<bool> originalRiderGravityStates =
        new(MaximumRiderCapacity);

    private readonly List<UltimatePlayerMovement> boardingCandidates =
        new(MaximumRiderCapacity);

    private readonly HashSet<int> animatorParameters =
        new();

    private readonly RaycastHit[] groundHits =
        new RaycastHit[GroundHitCapacity];

    private VehicleState currentState;

    private Vector3 trackDirection =
        Vector3.forward;

    private Vector3 groundNormal =
        Vector3.up;

    private Vector3 startingTrackPosition;
    private Vector3 lateralCorrectionVelocity;

    private float currentSpeed;
    private float currentTrackOffset;
    private float jumpCooldownTimer;
    private float safetyTimer;

    private int currentPathIndex;

    private bool grounded;
    private bool groundedLastFrame;
    private bool initialized;
    private bool shuttingDown;
    private bool applicationQuitting;
    private bool safetyShutdown;
    private bool checkpointReached;

    #endregion

    #region Public API

    public event Action<VehicleState> StateChanged;
    public event Action<UltimatePlayerMovement> RiderBoarded;
    public event Action<UltimatePlayerMovement> RiderDismounted;
    public event Action CheckpointReached;
    public event Action TrackCompleted;

    public VehicleState CurrentState =>
        currentState;

    public bool IsInitialized =>
        initialized;

    public bool IsDriving =>
        currentState == VehicleState.Driving ||
        currentState == VehicleState.Airborne;

    public bool IsGrounded =>
        grounded;

    public bool IsSafetyShutdown =>
        safetyShutdown;

    public bool HasReachedCheckpoint =>
        checkpointReached;

    public float CurrentSpeed =>
        currentSpeed;

    public int CurrentPathIndex =>
        currentPathIndex;

    public int RiderCount =>
        riders.Count;

    public bool BoardsEntireTeamAutomatically =>
        boardEntireTeamAutomatically;

    protected Rigidbody VehicleRigidbody =>
        vehicleRigidbody;

    protected Collider VehicleCollider =>
        vehicleCollider;

    protected Animator VehicleAnimator =>
        vehicleAnimator;

    protected AudioSource VehicleAudioSource =>
        vehicleAudioSource;

    protected IReadOnlyList<UltimatePlayerMovement> Riders =>
        riders;

    public bool InitializeVehicle()
    {
        if (initialized)
            return true;

        ResolveDependencies();
        ConfigureRigidbody();
        ConfigureComponents();

        if (!ValidateConfiguration())
        {
            initialized = false;
            return false;
        }

        ResetRuntimeState();
        CacheAnimatorParameters();

        currentState =
            IsValidVehicleState(startingState)
                ? startingState
                : VehicleState.Waiting;

        grounded =
            DetectGround(
                out groundNormal);

        groundedLastFrame =
            grounded;

        initialized = true;

        if (startAutomatically)
        {
            StartDriving();
        }

        return true;
    }

    public bool StartDriving()
    {
        if (!CanDrive())
            return false;

        currentSpeed =
            Mathf.Max(
                currentSpeed,
                minimumDrivingSpeed);

        ChangeState(
            grounded
                ? VehicleState.Driving
                : VehicleState.Airborne);

        StartDrivePresentation();
        OnDrivingStarted();

        return true;
    }

    public bool StopDriving()
    {
        if (!IsDriving)
            return false;

        ChangeState(
            VehicleState.Stopping);

        return true;
    }

    public bool BoardVehicle(
        UltimatePlayerMovement movement)
    {
        if (movement == null ||
            !allowBoarding ||
            safetyShutdown)
        {
            return false;
        }

        if (!boardEntireTeamAutomatically)
        {
            return BoardSingleRider(
                movement);
        }

        CollectBoardingCandidates(
            movement);

        if (requireCompleteTeam &&
            boardingCandidates.Count <
                MaximumRiderCapacity)
        {
            boardingCandidates.Clear();
            return false;
        }

        if (boardingCandidates.Count == 0)
            return false;

        ChangeState(
            VehicleState.Boarding);

        int boardedCount = 0;

        for (int index = 0;
             index < boardingCandidates.Count &&
             boardedCount < maximumRiders;
             index++)
        {
            UltimatePlayerMovement candidate =
                boardingCandidates[index];

            if (!CanBoard(candidate))
                continue;

            if (!BoardRiderInternal(candidate))
                continue;

            boardedCount++;
        }

        boardingCandidates.Clear();

        ChangeState(
            VehicleState.Waiting);

        if (boardedCount == 0)
            return false;

        if (startAutomatically)
        {
            StartDriving();
        }

        return true;
    }

    public bool BoardSingleRider(
        UltimatePlayerMovement movement)
    {
        if (!CanBoard(movement))
            return false;

        ChangeState(
            VehicleState.Boarding);

        bool boarded =
            BoardRiderInternal(
                movement);

        ChangeState(
            VehicleState.Waiting);

        if (!boarded)
            return false;

        if (startAutomatically)
        {
            StartDriving();
        }

        return true;
    }

    public bool DismountAll()
    {
        if (riders.Count == 0)
            return false;

        bool wasDriving =
            IsDriving;

        ChangeState(
            VehicleState.Dismounting);

        DismountAllSafely();

        if (currentState != VehicleState.Finished &&
            currentState != VehicleState.Disabled)
        {
            ChangeState(
                wasDriving
                    ? VehicleState.Stopping
                    : VehicleState.Waiting);
        }

        return true;
    }

    public bool SetPath(
        Transform newPathRoot)
    {
        if (newPathRoot == null)
            return false;

        pathRoot =
            newPathRoot;

        pathPoints =
            Array.Empty<Transform>();

        ResolvePathPoints();

        currentPathIndex = 0;

        return
            pathPoints.Length > 0;
    }

    public bool SetPathIndex(
        int index)
    {
        if (pathPoints == null ||
            pathPoints.Length == 0)
        {
            return false;
        }

        currentPathIndex =
            Mathf.Clamp(
                index,
                0,
                pathPoints.Length - 1);

        return true;
    }

    public void NotifyCheckpointReached()
    {
        if (!initialized ||
            checkpointReached)
        {
            return;
        }

        checkpointReached = true;

        CheckpointReached?.Invoke();
        OnCheckpointReached();

        if (finishAtCheckpoint)
        {
            CompleteTrack();
            return;
        }

        if (stopAtCheckpoint)
        {
            StopDriving();
        }

        if (dismountAtCheckpoint)
        {
            DismountAll();
        }
    }

    public void NotifyStopperReached()
    {
        if (!initialized)
            return;

        StopDriving();
    }

    public bool CompleteTrack()
    {
        if (!initialized ||
            currentState == VehicleState.Finished)
        {
            return false;
        }

        StopVehicleMotion();
        StopDrivePresentation();

        if (riders.Count > 0)
        {
            DismountAllSafely();
        }

        ChangeState(
            VehicleState.Finished);

        TrackCompleted?.Invoke();
        OnTrackCompleted();

        if (destroyAfterFinish)
        {
            Destroy(
                gameObject,
                destroyDelay);
        }

        return true;
    }

    public bool ResetVehicle()
    {
        if (shuttingDown ||
            applicationQuitting)
        {
            return false;
        }

        DismountAllSafely();
        StopVehicleMotion();

        safetyShutdown = false;
        initialized = false;

        RestoreRequiredComponents();

        if (!InitializeVehicle())
            return false;

        enabled = true;

        ChangeState(
            VehicleState.Waiting);

        return true;
    }

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        ResolveDependencies();
        ConfigureRigidbody();
        ConfigureComponents();
    }

    protected virtual void Start()
    {
        TryInitializeVehicle();
    }

    private bool TryInitializeVehicle()
    {
        if (initialized)
            return true;

        ResolveDependencies();
        ConfigureRigidbody();

        if (pathPoints == null ||
            pathPoints.Length == 0)
        {
            return false;
        }

        return InitializeVehicle();
    }

    protected virtual void OnEnable()
    {
        if (shuttingDown)
            return;

        ResolveDependencies();
        ConfigureComponents();

        if (initialized &&
            !safetyShutdown &&
            currentState == VehicleState.Disabled)
        {
            ChangeState(
                VehicleState.Waiting);
        }
    }

    protected virtual void Update()
    {
        if (!CanRun())
            return;

        UpdateTimers();
        HandleJumpInput();
        UpdateAnimator();

        if (!enablePhysicsSafety)
            return;

        safetyTimer -=
            Time.deltaTime;

        if (safetyTimer > 0f)
            return;

        safetyTimer =
            safetyCheckInterval;

        RunPhysicsSafetyChecks();
    }

    protected virtual void FixedUpdate()
    {
        if (shuttingDown ||
            applicationQuitting ||
            safetyShutdown)
        {
            return;
        }

        if (!initialized)
        {
            TryInitializeVehicle();
            return;
        }

        UpdateGrounding();

        switch (currentState)
        {
            case VehicleState.Waiting:
                UpdateWaitingState();
                break;

            case VehicleState.Boarding:
                UpdateBoardingState();
                break;

            case VehicleState.Driving:
                UpdateDrivingState();
                break;

            case VehicleState.Airborne:
                UpdateAirborneState();
                break;

            case VehicleState.Stopping:
                UpdateStoppingState();
                break;

            case VehicleState.Dismounting:
                UpdateDismountingState();
                break;

            case VehicleState.Finished:
                UpdateFinishedState();
                break;

            case VehicleState.Disabled:
                break;

            default:
                EnterSafetyShutdown(
                    $"Unhandled vehicle state '{currentState}'.");
                break;
        }
    }

    protected virtual void OnTriggerEnter(
        Collider other)
    {
        if (!allowBoarding ||
            !boardOnTrigger ||
            other == null)
        {
            return;
        }

        UltimatePlayerMovement movement =
            ResolveMovement(
                other);

        if (movement != null)
        {
            BoardVehicle(
                movement);
        }
    }

    protected virtual void OnCollisionEnter(
        Collision collision)
    {
        OnVehicleCollision(
            collision);
    }

    protected virtual void OnDisable()
    {
        StopVehicleMotion();

        if (!shuttingDown &&
            !applicationQuitting &&
            initialized &&
            !safetyShutdown)
        {
            ChangeState(
                VehicleState.Disabled);
        }
    }

    protected virtual void OnDestroy()
    {
        shuttingDown = true;

        DismountAllSafely();
        StopVehicleMotion();

        StateChanged = null;
        RiderBoarded = null;
        RiderDismounted = null;
        CheckpointReached = null;
        TrackCompleted = null;

        ClearRiderCollections();

        animatorParameters.Clear();
        boardingCandidates.Clear();

        vehicleRigidbody = null;
        vehicleCollider = null;
        vehicleAnimator = null;
        vehicleAudioSource = null;

        pathRoot = null;
        pathPoints = Array.Empty<Transform>();
        seatPoint = null;
        seatPoints = Array.Empty<Transform>();
        exitPoint = null;
    }

    protected virtual void OnApplicationQuit()
    {
        applicationQuitting = true;
    }

    protected virtual void OnApplicationPause(
        bool paused)
    {
        if (!paused)
            return;

        StopVehicleMotion();
    }

    protected virtual void OnValidate()
    {
        pointArrivalDistance =
            Mathf.Max(
                0.01f,
                pointArrivalDistance);

        maximumSpeed =
            Mathf.Max(
                0f,
                maximumSpeed);

        minimumDrivingSpeed =
            Mathf.Clamp(
                minimumDrivingSpeed,
                0f,
                maximumSpeed);

        acceleration =
            Mathf.Max(
                0f,
                acceleration);

        deceleration =
            Mathf.Max(
                0f,
                deceleration);

        rotationSpeed =
            Mathf.Max(
                0f,
                rotationSpeed);

        steeringSpeed =
            Mathf.Max(
                0f,
                steeringSpeed);

        maximumTrackOffset =
            Mathf.Max(
                0f,
                maximumTrackOffset);

        steeringReturnSpeed =
            Mathf.Max(
                0f,
                steeringReturnSpeed);

        jumpVelocity =
            Mathf.Max(
                0f,
                jumpVelocity);

        jumpCooldown =
            Mathf.Max(
                0f,
                jumpCooldown);

        groundProbeRadius =
            Mathf.Max(
                0.01f,
                groundProbeRadius);

        groundProbeDistance =
            Mathf.Max(
                0.01f,
                groundProbeDistance);

        maximumGroundSlope =
            Mathf.Clamp(
                maximumGroundSlope,
                0f,
                89f);

        maximumRiders =
            Mathf.Clamp(
                maximumRiders,
                1,
                MaximumRiderCapacity);

        teammateSearchRadius =
            Mathf.Max(
                0.1f,
                teammateSearchRadius);

        destroyDelay =
            Mathf.Max(
                0f,
                destroyDelay);

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

        safetyCheckInterval =
            Mathf.Max(
                0.1f,
                safetyCheckInterval);

        if (!IsValidVehicleState(
                startingState))
        {
            startingState =
                VehicleState.Waiting;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ResolveDependencies();
            ConfigureComponents();
        }
#endif
    }

    protected virtual void OnDrawGizmosSelected()
    {
        DrawPathGizmos();

        Vector3 origin =
            transform.position +
            transform.up *
            0.1f;

        Gizmos.DrawWireSphere(
            origin,
            groundProbeRadius);

        Gizmos.DrawLine(
            origin,
            origin -
            transform.up *
            groundProbeDistance);
    }

    #endregion

    #region State Machine

    protected void ChangeState(
        VehicleState newState)
    {
        if (!IsValidVehicleState(
                newState))
        {
            RecoverInvalidVehicleState();
            return;
        }

        if (currentState ==
            newState)
        {
            return;
        }

        VehicleState previousState =
            currentState;

        ExitState(
            previousState);

        currentState =
            newState;

        EnterState(
            currentState);

        UpdateAnimator();

        StateChanged?.Invoke(
            currentState);

        OnStateChanged(
            previousState,
            currentState);

        LogStateChange(
            $"{previousState} -> {currentState}");
    }

    private void EnterState(
        VehicleState state)
    {
        switch (state)
        {
            case VehicleState.Waiting:
                lateralCorrectionVelocity =
                    Vector3.zero;
                break;

            case VehicleState.Driving:
                currentSpeed =
                    Mathf.Max(
                        currentSpeed,
                        minimumDrivingSpeed);
                break;

            case VehicleState.Airborne:
                grounded = false;
                break;

            case VehicleState.Finished:
            case VehicleState.Disabled:
                StopVehicleMotion();
                break;
        }
    }

    private void ExitState(
        VehicleState state)
    {
        if (state ==
            VehicleState.Driving ||
            state ==
            VehicleState.Airborne)
        {
            lateralCorrectionVelocity =
                Vector3.zero;
        }
    }

    private void RecoverInvalidVehicleState()
    {
        VehicleState recoveredState =
            grounded
                ? VehicleState.Waiting
                : VehicleState.Airborne;

        currentState =
            recoveredState;

        currentSpeed = 0f;
        lateralCorrectionVelocity =
            Vector3.zero;

        UpdateAnimator();

        StateChanged?.Invoke(
            currentState);

        Debug.LogError(
            $"{GetType().Name} recovered an invalid vehicle state on '{name}' to {recoveredState}.",
            this);
    }

    private void UpdateWaitingState()
    {
        StopVehicleMotion();
    }

    private void UpdateBoardingState()
    {
        StopVehicleMotion();
    }

    private void UpdateDrivingState()
    {
        if (!TryGetCurrentPathPoint(
                out Transform targetPoint))
        {
            CompleteTrack();
            return;
        }

        currentSpeed =
            Mathf.MoveTowards(
                currentSpeed,
                maximumSpeed,
                acceleration *
                Time.fixedDeltaTime);

        UpdateTrackDirection(
            targetPoint.position);

        ApplySteering();
        ApplyDrivingVelocity();
        RotateToTrack();

        CheckPathPointArrival(
            targetPoint.position);

        OnDrivingPhysicsUpdated();
    }

    private void UpdateAirborneState()
    {
        if (!TryGetCurrentPathPoint(
                out Transform targetPoint))
        {
            CompleteTrack();
            return;
        }

        UpdateTrackDirection(
            targetPoint.position);

        ApplyAirborneVelocity();
        RotateToTrack();

        CheckPathPointArrival(
            targetPoint.position);

        OnAirbornePhysicsUpdated();
    }

    private void UpdateStoppingState()
    {
        currentSpeed =
            Mathf.MoveTowards(
                currentSpeed,
                0f,
                deceleration *
                Time.fixedDeltaTime);

        ApplyDrivingVelocity();

        if (currentSpeed >
            MinimumSpeedThreshold)
        {
            return;
        }

        StopVehicleMotion();
        StopDrivePresentation();

        ChangeState(
            VehicleState.Waiting);

        OnVehicleStopped();
    }

    private void UpdateDismountingState()
    {
        StopVehicleMotion();
    }

    private void UpdateFinishedState()
    {
        StopVehicleMotion();
    }

    #endregion

    #region Track Movement

    private bool CanDrive()
    {
        return
            initialized &&
            !safetyShutdown &&
            vehicleRigidbody != null &&
            pathPoints != null &&
            pathPoints.Length > 0 &&
            currentState != VehicleState.Disabled &&
            currentState != VehicleState.Finished;
    }

    private bool TryGetCurrentPathPoint(
        out Transform targetPoint)
    {
        targetPoint = null;

        if (pathPoints == null ||
            pathPoints.Length == 0 ||
            currentPathIndex < 0 ||
            currentPathIndex >= pathPoints.Length)
        {
            return false;
        }

        targetPoint =
            pathPoints[currentPathIndex];

        return
            targetPoint != null &&
            IsFiniteVector(
                targetPoint.position);
    }

    private void UpdateTrackDirection(
        Vector3 targetPosition)
    {
        if (vehicleRigidbody == null)
            return;

        Vector3 direction =
            targetPosition -
            vehicleRigidbody.position;

        direction.y = 0f;

        if (!IsFiniteVector(direction) ||
            direction.sqrMagnitude <=
                MinimumDirectionSqrMagnitude)
        {
            return;
        }

        trackDirection =
            direction.normalized;
    }

    private void ApplySteering()
    {
        if (vehicleRigidbody == null)
            return;

        float targetOffset = 0f;

        if (allowSteering)
        {
            targetOffset =
                Input.GetAxisRaw("Horizontal") *
                maximumTrackOffset;
        }

        float offsetSpeed =
            allowSteering
                ? steeringSpeed
                : steeringReturnSpeed;

        currentTrackOffset =
            Mathf.MoveTowards(
                currentTrackOffset,
                targetOffset,
                offsetSpeed *
                Time.fixedDeltaTime);

        if (!TryGetTrackFrame(
                out Vector3 trackCenter,
                out Vector3 trackRight))
        {
            lateralCorrectionVelocity =
                Vector3.zero;

            return;
        }

        Vector3 desiredPosition =
            trackCenter +
            trackRight *
            currentTrackOffset;

        Vector3 correction =
            Vector3.Project(
                desiredPosition -
                vehicleRigidbody.position,
                trackRight);

        float maximumCorrectionSpeed =
            Mathf.Max(
                steeringSpeed,
                steeringReturnSpeed);

        lateralCorrectionVelocity =
            Vector3.ClampMagnitude(
                correction /
                Mathf.Max(
                    Time.fixedDeltaTime,
                    0.0001f),
                maximumCorrectionSpeed);

        if (!IsFiniteVector(
                lateralCorrectionVelocity))
        {
            lateralCorrectionVelocity =
                Vector3.zero;
        }
    }

    private void ApplyDrivingVelocity()
    {
        if (vehicleRigidbody == null)
            return;

        Vector3 velocity =
            trackDirection *
            currentSpeed +
            lateralCorrectionVelocity;

        velocity.y =
            vehicleRigidbody.linearVelocity.y;

        if (!IsFiniteVector(velocity))
        {
            EnterSafetyShutdown(
                "Driving velocity became invalid.");

            return;
        }

        vehicleRigidbody.linearVelocity =
            velocity;
    }

    private void ApplyAirborneVelocity()
    {
        if (vehicleRigidbody == null)
            return;

        Vector3 velocity =
            vehicleRigidbody.linearVelocity;

        Vector3 horizontalVelocity =
            trackDirection *
            currentSpeed;

        velocity.x =
            horizontalVelocity.x;

        velocity.z =
            horizontalVelocity.z;

        if (!IsFiniteVector(velocity))
        {
            EnterSafetyShutdown(
                "Airborne velocity became invalid.");

            return;
        }

        vehicleRigidbody.linearVelocity =
            velocity;
    }

    private void RotateToTrack()
    {
        if (vehicleRigidbody == null ||
            trackDirection.sqrMagnitude <=
                MinimumDirectionSqrMagnitude)
        {
            return;
        }

        Vector3 up =
            grounded
                ? groundNormal
                : Vector3.up;

        if (!IsFiniteVector(up) ||
            up.sqrMagnitude <=
                MinimumDirectionSqrMagnitude)
        {
            up =
                Vector3.up;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                trackDirection,
                up);

        if (!IsFiniteQuaternion(
                targetRotation))
        {
            return;
        }

        Quaternion nextRotation =
            Quaternion.Slerp(
                vehicleRigidbody.rotation,
                targetRotation,
                Mathf.Clamp01(
                    rotationSpeed *
                    Time.fixedDeltaTime));

        vehicleRigidbody.MoveRotation(
            nextRotation);
    }

    private void CheckPathPointArrival(
        Vector3 targetPosition)
    {
        if (vehicleRigidbody == null)
            return;

        float distance =
            Vector3.Distance(
                vehicleRigidbody.position,
                targetPosition);

        if (!float.IsFinite(distance) ||
            distance >
                pointArrivalDistance)
        {
            return;
        }

        currentPathIndex++;

        if (currentPathIndex <
            pathPoints.Length)
        {
            return;
        }

        if (loopPath)
        {
            currentPathIndex = 0;
            return;
        }

        currentPathIndex =
            Mathf.Max(
                0,
                pathPoints.Length - 1);

        if (stopAtFinalPoint)
        {
            CompleteTrack();
        }
    }

    private bool TryGetTrackFrame(
        out Vector3 trackCenter,
        out Vector3 trackRight)
    {
        trackCenter =
            vehicleRigidbody != null
                ? vehicleRigidbody.position
                : transform.position;

        trackRight =
            transform.right;

        if (pathPoints == null ||
            pathPoints.Length == 0)
        {
            return false;
        }

        Vector3 segmentStart =
            currentPathIndex > 0 &&
            currentPathIndex - 1 <
                pathPoints.Length &&
            pathPoints[currentPathIndex - 1] != null
                ? pathPoints[currentPathIndex - 1].position
                : startingTrackPosition;

        Vector3 segmentEnd =
            currentPathIndex <
                pathPoints.Length &&
            pathPoints[currentPathIndex] != null
                ? pathPoints[currentPathIndex].position
                : segmentStart +
                  trackDirection;

        Vector3 segment =
            segmentEnd -
            segmentStart;

        segment.y = 0f;

        if (!IsFiniteVector(segment) ||
            segment.sqrMagnitude <=
                MinimumDirectionSqrMagnitude)
        {
            return false;
        }

        Vector3 segmentDirection =
            segment.normalized;

        Vector3 position =
            vehicleRigidbody != null
                ? vehicleRigidbody.position
                : transform.position;

        Vector3 fromStart =
            position -
            segmentStart;

        fromStart.y = 0f;

        float segmentLength =
            segment.magnitude;

        float distanceAlongSegment =
            Mathf.Clamp(
                Vector3.Dot(
                    fromStart,
                    segmentDirection),
                0f,
                segmentLength);

        trackCenter =
            segmentStart +
            segmentDirection *
            distanceAlongSegment;

        Vector3 up =
            grounded
                ? groundNormal
                : Vector3.up;

        trackRight =
            Vector3.Cross(
                up,
                segmentDirection);

        if (trackRight.sqrMagnitude <=
            MinimumDirectionSqrMagnitude)
        {
            trackRight =
                transform.right;
        }

        trackRight.Normalize();

        return
            IsFiniteVector(trackCenter) &&
            IsFiniteVector(trackRight);
    }

    #endregion

    #region Jump And Grounding

    private void HandleJumpInput()
    {
        if (!allowJump ||
            !IsDriving ||
            !grounded ||
            jumpCooldownTimer > 0f ||
            vehicleRigidbody == null ||
            !Input.GetKeyDown(jumpKey))
        {
            return;
        }

        Vector3 velocity =
            vehicleRigidbody.linearVelocity;

        velocity.y = 0f;

        velocity +=
            Vector3.up *
            jumpVelocity;

        if (!IsFiniteVector(velocity))
            return;

        vehicleRigidbody.linearVelocity =
            velocity;

        grounded = false;

        jumpCooldownTimer =
            jumpCooldown;

        ChangeState(
            VehicleState.Airborne);

        SetAnimatorTrigger(
            JumpHash);

        jumpEffect?.Play();

        PlayOneShot(
            jumpClip);

        OnVehicleJumped();
    }

    private void UpdateGrounding()
    {
        groundedLastFrame =
            grounded;

        grounded =
            DetectGround(
                out groundNormal);

        if (groundedLastFrame &&
            !grounded &&
            currentState ==
                VehicleState.Driving)
        {
            ChangeState(
                VehicleState.Airborne);
        }
        else if (!groundedLastFrame &&
                 grounded &&
                 currentState ==
                    VehicleState.Airborne)
        {
            ChangeState(
                VehicleState.Driving);
        }
    }

    private bool DetectGround(
        out Vector3 normal)
    {
        normal =
            Vector3.up;

        if (vehicleRigidbody == null)
            return false;

        Vector3 origin =
            vehicleRigidbody.position +
            Vector3.up *
            0.1f;

        int hitCount =
            Physics.SphereCastNonAlloc(
                origin,
                groundProbeRadius,
                Vector3.down,
                groundHits,
                groundProbeDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);

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

            if (candidate.collider == null ||
                IsOwnCollider(
                    candidate.collider) ||
                candidate.distance >=
                    closestDistance)
            {
                continue;
            }

            float slopeAngle =
                Vector3.Angle(
                    candidate.normal,
                    Vector3.up);

            if (!float.IsFinite(
                    slopeAngle) ||
                slopeAngle >
                    maximumGroundSlope)
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

        for (int index = 0;
             index < hitCount;
             index++)
        {
            groundHits[index] =
                default;
        }

        if (!foundGround)
            return false;

        normal =
            closestHit.normal.normalized;

        return
            IsFiniteVector(normal);
    }

    #endregion

    #region Boarding

    private bool BoardRiderInternal(
        UltimatePlayerMovement movement)
    {
        if (!CanBoard(movement))
            return false;

        Rigidbody riderRigidbody =
            ResolveRigidbody(
                movement);

        CacheRiderState(
            movement,
            riderRigidbody);

        AttachRider(
            movement,
            riderRigidbody);

        RiderBoarded?.Invoke(
            movement);

        OnRiderBoarded(
            movement);

        return true;
    }

    private bool CanBoard(
        UltimatePlayerMovement movement)
    {
        return
            allowBoarding &&
            initialized &&
            !safetyShutdown &&
            movement != null &&
            movement.IsInitialized &&
            !movement.IsSafetyShutdown &&
            movement.gameObject.activeInHierarchy &&
            riders.Count <
                maximumRiders &&
            !riders.Contains(
                movement) &&
            currentState !=
                VehicleState.Finished &&
            currentState !=
                VehicleState.Disabled;
    }

    private void CacheRiderState(
        UltimatePlayerMovement movement,
        Rigidbody riderRigidbody)
    {
        riders.Add(
            movement);

        originalRiderParents.Add(
            movement.transform.parent);

        originalMovementComponentStates.Add(
            movement.enabled);

        originalRiderKinematicStates.Add(
            riderRigidbody != null &&
            riderRigidbody.isKinematic);

        originalRiderGravityStates.Add(
            riderRigidbody == null ||
            riderRigidbody.useGravity);
    }

    private void AttachRider(
        UltimatePlayerMovement movement,
        Rigidbody riderRigidbody)
    {
        movement.DisableMovement();
        movement.SetInputEnabled(
            false);

        movement.enabled =
            false;

        if (riderRigidbody != null)
        {
            riderRigidbody.linearVelocity =
                Vector3.zero;

            riderRigidbody.angularVelocity =
                Vector3.zero;

            riderRigidbody.isKinematic =
                true;

            riderRigidbody.useGravity =
                false;
        }

        int riderIndex =
            riders.IndexOf(
                movement);

        Transform targetSeat =
            ResolveSeatForRider(
                riderIndex);

        movement.transform.SetParent(
            targetSeat,
            worldPositionStays: false);

        movement.transform.localPosition =
            Vector3.zero;

        movement.transform.localRotation =
            Quaternion.identity;
    }

    private void DismountAllSafely()
    {
        for (int index =
                 riders.Count - 1;
             index >= 0;
             index--)
        {
            DismountRiderAt(
                index);
        }
    }

    private void DismountRiderAt(
        int index)
    {
        if (index < 0 ||
            index >= riders.Count)
        {
            return;
        }

        UltimatePlayerMovement movement =
            riders[index];

        Transform originalParent =
            originalRiderParents[index];

        bool movementComponentWasEnabled =
            originalMovementComponentStates[index];

        bool riderWasKinematic =
            originalRiderKinematicStates[index];

        bool riderUsedGravity =
            originalRiderGravityStates[index];

        RemoveRiderStateAt(
            index);

        if (movement == null)
            return;

        movement.transform.SetParent(
            originalParent,
            worldPositionStays: true);

        Transform destination =
            exitPoint != null
                ? exitPoint
                : transform;

        movement.transform.SetPositionAndRotation(
            destination.position,
            destination.rotation);

        Rigidbody riderRigidbody =
            ResolveRigidbody(
                movement);

        if (riderRigidbody != null)
        {
            riderRigidbody.isKinematic =
                riderWasKinematic;

            riderRigidbody.useGravity =
                riderUsedGravity;

            riderRigidbody.linearVelocity =
                Vector3.zero;

            riderRigidbody.angularVelocity =
                Vector3.zero;

            if (!riderRigidbody.isKinematic)
            {
                riderRigidbody.WakeUp();
            }
        }

        movement.enabled =
            movementComponentWasEnabled;

        movement.EnableMovement();
        movement.SetInputEnabled(
            true);

        RiderDismounted?.Invoke(
            movement);

        OnRiderDismounted(
            movement);
    }

    private void CollectBoardingCandidates(
        UltimatePlayerMovement activatingMovement)
    {
        boardingCandidates.Clear();

        if (!IsValidBoardingCandidate(
                activatingMovement))
        {
            return;
        }

        Transform teamRoot =
            ResolveTeamRoot(
                activatingMovement.transform);

        if (preserveFormationOrder &&
            teamRoot != null)
        {
            AddMovementFromNamedSlot(
                teamRoot,
                LeaderSlotName);

            AddMovementFromNamedSlot(
                teamRoot,
                LeftTeamMemberSlotName);

            AddMovementFromNamedSlot(
                teamRoot,
                RightTeamMemberSlotName);
        }

        AddBoardingCandidate(
            activatingMovement);

        if (teamRoot != null)
        {
            UltimatePlayerMovement[] teamMovements =
                teamRoot.GetComponentsInChildren<UltimatePlayerMovement>(
                    includeInactive: true);

            foreach (UltimatePlayerMovement candidate
                     in teamMovements)
            {
                AddBoardingCandidate(
                    candidate);
            }
        }

        if (boardingCandidates.Count <
            maximumRiders)
        {
            CollectNearbyTeamCandidates(
                activatingMovement,
                teamRoot);
        }

        if (!preserveFormationOrder)
        {
            boardingCandidates.Sort(
                (left, right) =>
                {
                    float leftDistance =
                        (left.transform.position -
                         activatingMovement.transform.position)
                        .sqrMagnitude;

                    float rightDistance =
                        (right.transform.position -
                         activatingMovement.transform.position)
                        .sqrMagnitude;

                    return leftDistance.CompareTo(
                        rightDistance);
                });
        }

        if (boardingCandidates.Count >
            maximumRiders)
        {
            boardingCandidates.RemoveRange(
                maximumRiders,
                boardingCandidates.Count -
                maximumRiders);
        }
    }

    private void AddMovementFromNamedSlot(
        Transform teamRoot,
        string slotName)
    {
        Transform slot =
            FindDescendantByName(
                teamRoot,
                slotName);

        if (slot == null)
            return;

        UltimatePlayerMovement movement =
            slot.GetComponent<UltimatePlayerMovement>();

        movement ??=
            slot.GetComponentInChildren<UltimatePlayerMovement>(
                includeInactive: true);

        AddBoardingCandidate(
            movement);
    }

    private void CollectNearbyTeamCandidates(
        UltimatePlayerMovement activatingMovement,
        Transform teamRoot)
    {
        UltimatePlayerMovement[] movements =
            FindObjectsByType<UltimatePlayerMovement>(
                FindObjectsInactive.Exclude);

        float maximumDistanceSquared =
            teammateSearchRadius *
            teammateSearchRadius;

        foreach (UltimatePlayerMovement movement
                 in movements)
        {
            if (!IsValidBoardingCandidate(
                    movement))
            {
                continue;
            }

            if (teamRoot != null &&
                movement.transform.root !=
                    teamRoot.root)
            {
                continue;
            }

            float distanceSquared =
                (movement.transform.position -
                 activatingMovement.transform.position)
                .sqrMagnitude;

            if (distanceSquared >
                maximumDistanceSquared)
            {
                continue;
            }

            AddBoardingCandidate(
                movement);

            if (boardingCandidates.Count >=
                maximumRiders)
            {
                return;
            }
        }
    }

    private void AddBoardingCandidate(
        UltimatePlayerMovement movement)
    {
        if (!IsValidBoardingCandidate(
                movement) ||
            boardingCandidates.Contains(
                movement))
        {
            return;
        }

        boardingCandidates.Add(
            movement);
    }

    private bool IsValidBoardingCandidate(
        UltimatePlayerMovement movement)
    {
        return
            movement != null &&
            movement.IsInitialized &&
            !movement.IsSafetyShutdown &&
            movement.gameObject.activeInHierarchy &&
            !riders.Contains(
                movement);
    }

    private static Transform ResolveTeamRoot(
        Transform characterTransform)
    {
        if (characterTransform == null)
            return null;

        Transform current =
            characterTransform;

        while (current != null)
        {
            bool hasLeaderSlot =
                FindDirectOrNestedChild(
                    current,
                    LeaderSlotName) != null;

            bool hasLeftSlot =
                FindDirectOrNestedChild(
                    current,
                    LeftTeamMemberSlotName) != null;

            bool hasRightSlot =
                FindDirectOrNestedChild(
                    current,
                    RightTeamMemberSlotName) != null;

            if (hasLeaderSlot &&
                (hasLeftSlot ||
                 hasRightSlot))
            {
                return current;
            }

            current =
                current.parent;
        }

        return characterTransform.root;
    }

    #endregion

    #region Presentation

    private void CacheAnimatorParameters()
    {
        animatorParameters.Clear();

        if (vehicleAnimator == null ||
            vehicleAnimator.runtimeAnimatorController ==
                null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter
                 in vehicleAnimator.parameters)
        {
            animatorParameters.Add(
                parameter.nameHash);
        }
    }

    private void UpdateAnimator()
    {
        if (vehicleAnimator == null)
            return;

        SetAnimatorBool(
            DrivingHash,
            IsDriving);

        SetAnimatorBool(
            GroundedHash,
            grounded);

        SetAnimatorFloat(
            SpeedHash,
            currentSpeed);
    }

    private void StartDrivePresentation()
    {
        driveEffect?.Play();

        if (vehicleAudioSource != null &&
            driveLoop != null)
        {
            vehicleAudioSource.clip =
                driveLoop;

            vehicleAudioSource.loop =
                true;

            vehicleAudioSource.Play();
        }
    }

    private void StopDrivePresentation()
    {
        SetAnimatorTrigger(
            StopHash);

        driveEffect?.Stop(
            true,
            ParticleSystemStopBehavior.StopEmitting);

        if (vehicleAudioSource != null)
        {
            vehicleAudioSource.Stop();
            vehicleAudioSource.loop = false;
        }

        stopEffect?.Play();

        PlayOneShot(
            stopClip);
    }

    private void SetAnimatorBool(
        int hash,
        bool value)
    {
        if (vehicleAnimator == null ||
            !animatorParameters.Contains(
                hash))
        {
            return;
        }

        vehicleAnimator.SetBool(
            hash,
            value);
    }

    private void SetAnimatorFloat(
        int hash,
        float value)
    {
        if (vehicleAnimator == null ||
            !animatorParameters.Contains(
                hash))
        {
            return;
        }

        vehicleAnimator.SetFloat(
            hash,
            value);
    }

    private void SetAnimatorTrigger(
        int hash)
    {
        if (vehicleAnimator == null ||
            !animatorParameters.Contains(
                hash))
        {
            return;
        }

        vehicleAnimator.SetTrigger(
            hash);
    }

    protected void PlayOneShot(
        AudioClip clip)
    {
        if (vehicleAudioSource == null ||
            clip == null)
        {
            return;
        }

        vehicleAudioSource.PlayOneShot(
            clip);
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
                EnterSafetyShutdown(
                    "Required runtime references could not be restored.");

                return false;
            }
        }

        if (!ValidateRuntimePhysicsState())
        {
            if (!TryRecoverInvalidPhysicsState())
            {
                EnterSafetyShutdown(
                    "Vehicle physics entered an unrecoverable state.");

                return false;
            }

            return false;
        }

        if (!ValidateTransformScale())
        {
            EnterSafetyShutdown(
                "Vehicle transform scale is invalid or near zero.");

            return false;
        }

        RemoveDestroyedRiders();

        return true;
    }

    private bool ValidateRuntimeReferences()
    {
        if (vehicleRigidbody == null ||
            vehicleCollider == null)
        {
            return false;
        }

        if (vehicleRigidbody.gameObject !=
                gameObject ||
            vehicleCollider.gameObject !=
                gameObject)
        {
            return false;
        }

        if (!vehicleRigidbody.gameObject.activeInHierarchy ||
            !vehicleCollider.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (restoreDisabledComponents &&
            !vehicleCollider.enabled)
        {
            vehicleCollider.enabled = true;
        }

        if (restoreDisabledComponents &&
            vehicleAnimator != null &&
            !vehicleAnimator.enabled)
        {
            vehicleAnimator.enabled = true;
        }

        if (restoreDisabledComponents &&
            vehicleAudioSource != null &&
            !vehicleAudioSource.enabled)
        {
            vehicleAudioSource.enabled = true;
        }

        return
            vehicleCollider.enabled &&
            !vehicleCollider.isTrigger;
    }

    private void AttemptRuntimeReferenceRecovery()
    {
        ResolveDependencies();
        ConfigureComponents();

        if (vehicleRigidbody != null)
        {
            vehicleRigidbody.interpolation =
                RigidbodyInterpolation.Interpolate;

            vehicleRigidbody.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;

            if (!vehicleRigidbody.isKinematic)
            {
                vehicleRigidbody.WakeUp();
            }
        }

        CacheAnimatorParameters();
    }

    private bool ValidateRuntimePhysicsState()
    {
        if (vehicleRigidbody == null)
            return false;

        Vector3 position =
            vehicleRigidbody.position;

        Vector3 velocity =
            vehicleRigidbody.linearVelocity;

        Vector3 angularVelocity =
            vehicleRigidbody.angularVelocity;

        return
            IsFiniteVector(position) &&
            IsFiniteVector(velocity) &&
            IsFiniteVector(angularVelocity) &&
            velocity.sqrMagnitude <=
                maximumLinearSpeed *
                maximumLinearSpeed &&
            angularVelocity.sqrMagnitude <=
                maximumAngularSpeed *
                maximumAngularSpeed;
    }

    private bool TryRecoverInvalidPhysicsState()
    {
        if (vehicleRigidbody == null ||
            !IsFiniteVector(
                vehicleRigidbody.position))
        {
            return false;
        }

        Vector3 velocity =
            IsFiniteVector(
                vehicleRigidbody.linearVelocity)
                ? vehicleRigidbody.linearVelocity
                : Vector3.zero;

        Vector3 angularVelocity =
            IsFiniteVector(
                vehicleRigidbody.angularVelocity)
                ? vehicleRigidbody.angularVelocity
                : Vector3.zero;

        vehicleRigidbody.linearVelocity =
            Vector3.ClampMagnitude(
                velocity,
                maximumLinearSpeed);

        vehicleRigidbody.angularVelocity =
            Vector3.ClampMagnitude(
                angularVelocity,
                maximumAngularSpeed);

        if (!vehicleRigidbody.isKinematic)
        {
            vehicleRigidbody.WakeUp();
        }

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

    private void EnterSafetyShutdown(
        string reason)
    {
        if (safetyShutdown)
            return;

        safetyShutdown = true;

        StopVehicleMotion();
        StopDrivePresentation();
        DismountAllSafely();
        boardingCandidates.Clear();

        if (vehicleRigidbody != null)
        {
            vehicleRigidbody.useGravity = false;
            vehicleRigidbody.isKinematic = true;
        }

        ChangeState(
            VehicleState.Disabled);

        Debug.LogError(
            $"{GetType().Name} entered safety shutdown on '{name}': {reason}",
            this);
    }

    private void RestoreRequiredComponents()
    {
        ResolveDependencies();
        ConfigureRigidbody();
        ConfigureComponents();

        if (vehicleCollider != null)
        {
            vehicleCollider.enabled = true;
            vehicleCollider.isTrigger = false;
        }

        if (vehicleAnimator != null)
        {
            vehicleAnimator.enabled = true;
        }

        if (vehicleAudioSource != null)
        {
            vehicleAudioSource.enabled = true;
        }
    }

    private void RemoveDestroyedRiders()
    {
        for (int index =
                 riders.Count - 1;
             index >= 0;
             index--)
        {
            if (riders[index] != null)
                continue;

            RemoveRiderStateAt(
                index);
        }
    }

    #endregion

    #region Initialization Helpers

    private void ResolveDependencies()
    {
        ResolveRigidbody();
        ResolveCollider();
        ResolveAnimator();
        ResolveAudioSource();
        ResolveSeatPoints();
        ResolveExitPoint();
        ResolvePathRoot();
        ResolvePathPoints();
    }

    private void ResolveRigidbody()
    {
        if (vehicleRigidbody != null &&
            vehicleRigidbody.gameObject ==
                gameObject)
        {
            return;
        }

        vehicleRigidbody =
            GetComponent<Rigidbody>();
    }

    private void ResolveCollider()
    {
        if (vehicleCollider != null &&
            vehicleCollider.gameObject ==
                gameObject &&
            !vehicleCollider.isTrigger)
        {
            return;
        }

        Collider[] colliders =
            GetComponents<Collider>();

        vehicleCollider =
            null;

        foreach (Collider candidate
                 in colliders)
        {
            if (candidate == null ||
                !candidate.enabled ||
                candidate.isTrigger)
            {
                continue;
            }

            vehicleCollider =
                candidate;

            break;
        }
    }

    private void ResolveAnimator()
    {
        if (vehicleAnimator != null)
            return;

        vehicleAnimator =
            GetComponent<Animator>();

        vehicleAnimator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        vehicleAnimator ??=
            GetComponentInParent<Animator>();
    }

    private void ResolveAudioSource()
    {
        if (vehicleAudioSource != null)
            return;

        vehicleAudioSource =
            GetComponent<AudioSource>();

        vehicleAudioSource ??=
            GetComponentInChildren<AudioSource>(
                includeInactive: true);

        vehicleAudioSource ??=
            GetComponentInParent<AudioSource>();
    }

    private void ResolveSeatPoints()
    {
        seatPoint ??=
            FindChildByName(
                SeatPointName);

        if (seatPoints == null ||
            seatPoints.Length == 0)
        {
            List<Transform> resolvedSeats =
                new(MaximumRiderCapacity);

            for (int index = 1;
                 index <= MaximumRiderCapacity;
                 index++)
            {
                Transform resolvedSeat =
                    FindChildByName(
                        $"{SeatPointPrefix}{index}");

                if (resolvedSeat != null)
                {
                    resolvedSeats.Add(
                        resolvedSeat);
                }
            }

            if (resolvedSeats.Count == 0 &&
                seatPoint != null)
            {
                resolvedSeats.Add(
                    seatPoint);
            }

            seatPoints =
                resolvedSeats.ToArray();
        }

        seatPoint ??=
            seatPoints != null &&
            seatPoints.Length > 0
                ? seatPoints[0]
                : transform;
    }

    private void ResolveExitPoint()
    {
        exitPoint ??=
            FindChildByName(
                ExitPointName);
    }

    private void ResolvePathRoot()
    {
        if (!resolvePathAutomatically ||
            pathRoot != null)
        {
            return;
        }

        pathRoot =
            FindChildByName(
                PathRootName);
    }

    private void ResolvePathPoints()
    {
        if (pathPoints != null &&
            pathPoints.Length > 0)
        {
            CompactPathPoints();
            return;
        }

        if (pathRoot == null)
        {
            pathPoints =
                Array.Empty<Transform>();

            return;
        }

        List<Transform> points =
            new();

        foreach (Transform child
                 in pathRoot)
        {
            if (child != null)
            {
                points.Add(
                    child);
            }
        }

        pathPoints =
            points.ToArray();
    }

    private void CompactPathPoints()
    {
        if (pathPoints == null ||
            pathPoints.Length == 0)
        {
            pathPoints =
                Array.Empty<Transform>();

            return;
        }

        List<Transform> validPoints =
            new(pathPoints.Length);

        foreach (Transform point
                 in pathPoints)
        {
            if (point != null)
            {
                validPoints.Add(
                    point);
            }
        }

        pathPoints =
            validPoints.ToArray();
    }

    private void ConfigureRigidbody()
    {
        ResolveRigidbody();

        if (vehicleRigidbody == null)
            return;

        vehicleRigidbody.useGravity = true;
        vehicleRigidbody.isKinematic = false;
        vehicleRigidbody.interpolation =
            RigidbodyInterpolation.Interpolate;
        vehicleRigidbody.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;
        vehicleRigidbody.constraints =
            DefaultConstraints;

        vehicleRigidbody.WakeUp();
    }

    private void ConfigureComponents()
    {
        if (vehicleCollider != null)
        {
            vehicleCollider.enabled = true;
            vehicleCollider.isTrigger = false;
        }

        if (vehicleAudioSource != null)
        {
            vehicleAudioSource.playOnAwake =
                false;
        }
    }

    private void ResetRuntimeState()
    {
        currentSpeed = 0f;
        currentTrackOffset = 0f;
        lateralCorrectionVelocity =
            Vector3.zero;

        startingTrackPosition =
            vehicleRigidbody != null
                ? vehicleRigidbody.position
                : transform.position;

        jumpCooldownTimer = 0f;
        safetyTimer =
            safetyCheckInterval;
        currentPathIndex = 0;

        grounded = false;
        groundedLastFrame = false;
        checkpointReached = false;
        safetyShutdown = false;

        boardingCandidates.Clear();
    }

    private bool ValidateConfiguration()
    {
        bool valid = true;

        if (vehicleRigidbody == null)
        {
            Debug.LogError(
                $"{GetType().Name} requires a Rigidbody.",
                this);

            valid = false;
        }

        if (vehicleCollider == null)
        {
            Debug.LogError(
                $"{GetType().Name} requires a non-trigger Collider.",
                this);

            valid = false;
        }

        if (pathPoints == null ||
            pathPoints.Length == 0)
        {
            Debug.LogError(
                $"{GetType().Name} requires at least one path point.",
                this);

            valid = false;
        }

        if (maximumRiders > 1 &&
            (seatPoints == null ||
             seatPoints.Length <
                maximumRiders))
        {
            Debug.LogWarning(
                $"{GetType().Name} on '{name}' has fewer seat points than Maximum Riders. The last valid seat will be reused.",
                this);
        }

        return valid;
    }

    #endregion

    #region Helpers

    private bool CanRun()
    {
        return
            initialized &&
            !shuttingDown &&
            !applicationQuitting &&
            !safetyShutdown;
    }

    private static bool IsValidVehicleState(
        VehicleState state)
    {
        return
            Enum.IsDefined(
                typeof(VehicleState),
                state);
    }

    private UltimatePlayerMovement ResolveMovement(
        Collider other)
    {
        if (other == null)
            return null;

        if (requirePlayerTag &&
            !HasTagInHierarchy(
                other.transform,
                playerTag))
        {
            return null;
        }

        UltimatePlayerMovement movement =
            other.GetComponent<UltimatePlayerMovement>();

        movement ??=
            other.GetComponentInParent<UltimatePlayerMovement>();

        movement ??=
            other.GetComponentInChildren<UltimatePlayerMovement>(
                includeInactive: true);

        return movement;
    }

    private static Rigidbody ResolveRigidbody(
        UltimatePlayerMovement movement)
    {
        if (movement == null)
            return null;

        Rigidbody body =
            movement.GetComponent<Rigidbody>();

        body ??=
            movement.GetComponentInParent<Rigidbody>();

        body ??=
            movement.GetComponentInChildren<Rigidbody>(
                includeInactive: true);

        return body;
    }

    private Transform ResolveSeatForRider(
        int riderIndex)
    {
        if (seatPoints != null &&
            seatPoints.Length > 0)
        {
            int seatIndex =
                Mathf.Clamp(
                    riderIndex,
                    0,
                    seatPoints.Length - 1);

            if (seatPoints[seatIndex] != null)
            {
                return
                    seatPoints[seatIndex];
            }
        }

        return
            seatPoint != null
                ? seatPoint
                : transform;
    }

    private static Transform FindDirectOrNestedChild(
        Transform root,
        string targetName)
    {
        return
            FindDescendantByName(
                root,
                targetName);
    }

    private static Transform FindDescendantByName(
        Transform root,
        string targetName)
    {
        if (root == null ||
            string.IsNullOrWhiteSpace(
                targetName))
        {
            return null;
        }

        Transform[] children =
            root.GetComponentsInChildren<Transform>(
                includeInactive: true);

        foreach (Transform child
                 in children)
        {
            if (child != null &&
                string.Equals(
                    child.name,
                    targetName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private Transform FindChildByName(
        string targetName)
    {
        return
            FindDescendantByName(
                transform,
                targetName);
    }

    private bool IsOwnCollider(
        Collider candidate)
    {
        if (candidate == null)
            return false;

        Transform root =
            vehicleRigidbody != null
                ? vehicleRigidbody.transform
                : transform;

        return
            candidate.transform == root ||
            candidate.transform.IsChildOf(
                root);
    }

    private static bool HasTagInHierarchy(
        Transform source,
        string requiredTag)
    {
        if (source == null)
            return false;

        if (string.IsNullOrWhiteSpace(
                requiredTag))
        {
            return true;
        }

        Transform current =
            source;

        while (current != null)
        {
            if (current.CompareTag(
                    requiredTag))
            {
                return true;
            }

            current =
                current.parent;
        }

        return false;
    }

    private void RemoveRiderStateAt(
        int index)
    {
        riders.RemoveAt(
            index);

        originalRiderParents.RemoveAt(
            index);

        originalMovementComponentStates.RemoveAt(
            index);

        originalRiderKinematicStates.RemoveAt(
            index);

        originalRiderGravityStates.RemoveAt(
            index);
    }

    private void ClearRiderCollections()
    {
        riders.Clear();
        originalRiderParents.Clear();
        originalMovementComponentStates.Clear();
        originalRiderKinematicStates.Clear();
        originalRiderGravityStates.Clear();
    }

    private void StopVehicleMotion()
    {
        if (vehicleRigidbody == null)
            return;

        vehicleRigidbody.linearVelocity =
            Vector3.zero;

        vehicleRigidbody.angularVelocity =
            Vector3.zero;

        currentSpeed = 0f;
        lateralCorrectionVelocity =
            Vector3.zero;
    }

    private void UpdateTimers()
    {
        if (jumpCooldownTimer <= 0f)
            return;

        jumpCooldownTimer =
            Mathf.Max(
                0f,
                jumpCooldownTimer -
                Time.deltaTime);
    }

    private void DrawPathGizmos()
    {
        Transform[] points =
            pathPoints;

        if ((points == null ||
             points.Length == 0) &&
            pathRoot != null)
        {
            List<Transform> foundPoints =
                new();

            foreach (Transform child
                     in pathRoot)
            {
                if (child != null)
                {
                    foundPoints.Add(
                        child);
                }
            }

            points =
                foundPoints.ToArray();
        }

        if (points == null)
            return;

        for (int index = 0;
             index < points.Length;
             index++)
        {
            Transform point =
                points[index];

            if (point == null)
                continue;

            Gizmos.DrawWireSphere(
                point.position,
                pointArrivalDistance);

            if (index + 1 <
                    points.Length &&
                points[index + 1] != null)
            {
                Gizmos.DrawLine(
                    point.position,
                    points[index + 1].position);
            }
        }
    }

    private static bool IsFiniteVector(
        Vector3 value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
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

    #region Extensibility

    protected virtual void OnStateChanged(
        VehicleState previousState,
        VehicleState newState)
    {
    }

    protected virtual void OnDrivingStarted()
    {
    }

    protected virtual void OnDrivingPhysicsUpdated()
    {
    }

    protected virtual void OnAirbornePhysicsUpdated()
    {
    }

    protected virtual void OnVehicleJumped()
    {
    }

    protected virtual void OnVehicleStopped()
    {
    }

    protected virtual void OnVehicleCollision(
        Collision collision)
    {
    }

    protected virtual void OnRiderBoarded(
        UltimatePlayerMovement movement)
    {
    }

    protected virtual void OnRiderDismounted(
        UltimatePlayerMovement movement)
    {
    }

    protected virtual void OnCheckpointReached()
    {
    }

    protected virtual void OnTrackCompleted()
    {
    }

    #endregion
}
