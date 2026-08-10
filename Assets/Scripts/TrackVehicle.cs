using System;
using System.Collections.Generic;
using UnityEngine;

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

    private struct RiderState
    {
        public UltimatePlayerMovement movement;
        public Transform originalParent;
        public bool originalKinematic;
        public bool originalGravity;
    }

    #endregion

    #region Constants

    private const int MaximumRiderCapacity = 3;

    private const float MinimumDirectionMagnitude =
        0.0001f;

    private const float MinimumSpeed =
        0.01f;

    private static readonly int DrivingHash =
        Animator.StringToHash(
            "Driving");

    private static readonly int GroundedHash =
        Animator.StringToHash(
            "Grounded");

    private static readonly int SpeedHash =
        Animator.StringToHash(
            "Speed");

    private static readonly int JumpHash =
        Animator.StringToHash(
            "Jump");

    private static readonly int StopHash =
        Animator.StringToHash(
            "Stop");

    #endregion

    #region Vehicle References

    [Header("Vehicle")]

    [SerializeField]
    private Rigidbody vehicleRigidbody;

    [SerializeField]
    private Collider vehicleCollider;

    [SerializeField]
    private Animator vehicleAnimator;

    [SerializeField]
    private AudioSource vehicleAudioSource;

    #endregion

    #region Track

    [Header("Track")]

    [SerializeField]
    private Transform pathRoot;

    [SerializeField]
    private Transform[] pathPoints =
        Array.Empty<Transform>();

    [SerializeField]
    private bool resolvePathAutomatically = true;

    [SerializeField]
    private bool loopPath;

    [SerializeField]
    private bool startAutomatically;

    [SerializeField]
    private bool stopAtFinalPoint = true;

    [SerializeField, Min(0.01f)]
    private float pointArrivalDistance =
        0.75f;

    #endregion

    #region Driving

    [Header("Driving")]

    [SerializeField, Min(0f)]
    private float maximumSpeed =
        35f;

    [SerializeField, Min(0f)]
    private float minimumDrivingSpeed =
        8f;

    [SerializeField, Min(0f)]
    private float acceleration =
        20f;

    [SerializeField, Min(0f)]
    private float deceleration =
        25f;

    [SerializeField, Min(0f)]
    private float rotationSpeed =
        360f;

    #endregion

    #region Steering

    [Header("Steering")]

    [SerializeField]
    private bool allowSteering = true;

    [SerializeField, Min(0f)]
    private float steeringSpeed =
        5f;

    [SerializeField, Min(0f)]
    private float maximumTrackOffset =
        2.5f;

    [SerializeField, Min(0f)]
    private float steeringReturnSpeed =
        8f;

    #endregion

    #region Jump

    [Header("Jump")]

    [SerializeField]
    private bool allowJump = true;

    [SerializeField]
    private KeyCode jumpKey =
        KeyCode.Space;

    [SerializeField, Min(0f)]
    private float jumpVelocity =
        10f;

    [SerializeField, Min(0f)]
    private float jumpCooldown =
        0.25f;

    #endregion

    #region Grounding

    [Header("Grounding")]

    [SerializeField]
    private LayerMask groundMask = ~0;

    [SerializeField, Min(0.01f)]
    private float groundProbeRadius =
        0.35f;

    [SerializeField, Min(0.01f)]
    private float groundProbeDistance =
        1.25f;

    [SerializeField, Range(0f, 89f)]
    private float maximumGroundSlope =
        60f;

    #endregion

    #region Boarding

    [Header("Boarding")]

    [SerializeField]
    private bool allowBoarding = true;

    [SerializeField]
    private bool boardOnTrigger = true;

    [SerializeField]
    private bool boardEntireTeamAutomatically =
        true;

    [SerializeField, Range(
        1,
        MaximumRiderCapacity)]
    private int maximumRiders =
        MaximumRiderCapacity;

    [SerializeField]
    private Transform[] seatPoints =
        Array.Empty<Transform>();

    [SerializeField]
    private Transform exitPoint;

    #endregion

    #region Checkpoints

    [Header("Checkpoint Behavior")]

    [SerializeField]
    private bool stopAtCheckpoint;

    [SerializeField]
    private bool dismountAtCheckpoint;

    [SerializeField]
    private bool finishAtCheckpoint;

    #endregion

    #region Presentation

    [Header("Presentation")]

    [SerializeField]
    private AudioClip driveLoop;

    [SerializeField]
    private AudioClip jumpClip;

    [SerializeField]
    private AudioClip stopClip;

    [SerializeField]
    private ParticleSystem driveEffect;

    [SerializeField]
    private ParticleSystem jumpEffect;

    [SerializeField]
    private ParticleSystem stopEffect;

    #endregion

    #region Safety

    [Header("Safety")]

    [SerializeField, Min(1f)]
    private float maximumLinearSpeed =
        100f;

    [SerializeField, Min(1f)]
    private float maximumAngularSpeed =
        50f;

    [SerializeField, Min(0.01f)]
    private float minimumValidScale =
        0.01f;

    #endregion

    #region Runtime

    private readonly List<UltimatePlayerMovement> riders =
        new(MaximumRiderCapacity);

    private readonly List<RiderState> riderStates =
        new(MaximumRiderCapacity);

    private readonly List<UltimatePlayerMovement>
        boardingCandidates =
            new(MaximumRiderCapacity);

    private readonly HashSet<int> animatorParameters =
        new();

    private VehicleState currentState =
        VehicleState.Waiting;

    private Vector3 trackDirection =
        Vector3.forward;

    private Vector3 groundNormal =
        Vector3.up;

    private Vector3 startingPosition;

    private float currentSpeed;
    private float currentTrackOffset;
    private float jumpCooldownTimer;

    private int currentPathIndex;

    private bool initialized;
    private bool grounded;
    private bool safetyShutdown;
    private bool checkpointReached;
    private bool shuttingDown;

    #endregion

    #region Events

    public event Action<VehicleState>
        StateChanged;

    public event Action<UltimatePlayerMovement>
        RiderBoarded;

    public event Action<UltimatePlayerMovement>
        RiderDismounted;

    public event Action
        CheckpointReached;

    public event Action
        TrackCompleted;

    #endregion

    #region Properties

    public VehicleState CurrentState =>
        currentState;

    public bool IsInitialized =>
        initialized;

    public bool IsDriving =>
        currentState ==
            VehicleState.Driving ||
        currentState ==
            VehicleState.Airborne;

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

    protected IReadOnlyList<UltimatePlayerMovement>
        Riders =>
            riders;

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        ResolveReferences();
        ConfigureVehicle();
    }

    protected virtual void Start()
    {
        InitializeVehicle();
    }

    protected virtual void Update()
    {
        if (!CanRun())
        {
            return;
        }

        UpdateTimers();

        if (allowJump &&
            IsDriving &&
            grounded &&
            jumpCooldownTimer <= 0f &&
            Input.GetKeyDown(
                jumpKey))
        {
            Jump();
        }

        UpdateAnimator();
        RunSafetyChecks();
    }

    protected virtual void FixedUpdate()
    {
        if (!CanRun())
        {
            return;
        }

        UpdateGrounding();

        switch (currentState)
        {
            case VehicleState.Waiting:
                UpdateWaiting();
                break;

            case VehicleState.Boarding:
                UpdateBoarding();
                break;

            case VehicleState.Driving:
                UpdateDriving();
                break;

            case VehicleState.Airborne:
                UpdateAirborne();
                break;

            case VehicleState.Stopping:
                UpdateStopping();
                break;

            case VehicleState.Dismounting:
                UpdateDismounting();
                break;

            case VehicleState.Finished:
                UpdateFinished();
                break;

            case VehicleState.Disabled:
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
        if (shuttingDown)
        {
            return;
        }

        StopVehicleMotion();
        StopDrivePresentation();

        if (initialized &&
            currentState !=
                VehicleState.Finished)
        {
            ChangeState(
                VehicleState.Disabled);
        }
    }

    protected virtual void OnDestroy()
    {
        shuttingDown =
            true;

        DismountAllSafely();
        StopVehicleMotion();

        StateChanged =
            null;

        RiderBoarded =
            null;

        RiderDismounted =
            null;

        CheckpointReached =
            null;

        TrackCompleted =
            null;

        riders.Clear();
        riderStates.Clear();
        boardingCandidates.Clear();
        animatorParameters.Clear();
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
    }

    protected virtual void OnDrawGizmosSelected()
    {
        DrawPath();

        Vector3 origin =
            transform.position +
            Vector3.up * 0.1f;

        Gizmos.DrawWireSphere(
            origin,
            groundProbeRadius);

        Gizmos.DrawLine(
            origin,
            origin +
            Vector3.down *
                groundProbeDistance);
    }

    #endregion

    #region Initialization

    public bool InitializeVehicle()
    {
        if (initialized)
        {
            return true;
        }

        ResolveReferences();

        if (resolvePathAutomatically)
        {
            ResolvePathPoints();
        }

        if (!ValidateConfiguration())
        {
            return false;
        }

        ConfigureVehicle();
        CacheAnimatorParameters();

        startingPosition =
            vehicleRigidbody.position;

        currentPathIndex =
            0;

        currentTrackOffset =
            0f;

        currentSpeed =
            0f;

        jumpCooldownTimer =
            0f;

        checkpointReached =
            false;

        safetyShutdown =
            false;

        grounded =
            DetectGround(
                out groundNormal);

        currentState =
            VehicleState.Waiting;

        initialized =
            true;

        UpdateAnimator();

        if (startAutomatically)
        {
            StartDriving();
        }

        return true;
    }

    private void ResolveReferences()
    {
        vehicleRigidbody ??=
            GetComponent<Rigidbody>();

        vehicleCollider ??=
            GetComponent<Collider>();

        vehicleAnimator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        vehicleAudioSource ??=
            GetComponent<AudioSource>();

        pathPoints ??=
            Array.Empty<Transform>();

        seatPoints ??=
            Array.Empty<Transform>();
    }

    private void ConfigureVehicle()
    {
        if (vehicleRigidbody != null)
        {
            vehicleRigidbody.interpolation =
                RigidbodyInterpolation.Interpolate;

            vehicleRigidbody.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;

            vehicleRigidbody.constraints |=
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationZ;
        }

        if (vehicleCollider != null &&
            !vehicleCollider.enabled)
        {
            vehicleCollider.enabled =
                true;
        }
    }

    private bool ValidateConfiguration()
    {
        if (vehicleRigidbody == null ||
            vehicleCollider == null)
        {
            return false;
        }

        if (pathPoints == null ||
            pathPoints.Length == 0)
        {
            return false;
        }

        for (int index = 0;
            index < pathPoints.Length;
            index++)
        {
            if (pathPoints[index] == null ||
                !IsFiniteVector(
                    pathPoints[index].position))
            {
                return false;
            }
        }

        return
            HasValidScale();
    }

    #endregion

    #region Path

    public bool SetPath(
        Transform newPathRoot)
    {
        if (newPathRoot == null)
        {
            return false;
        }

        pathRoot =
            newPathRoot;

        pathPoints =
            Array.Empty<Transform>();

        ResolvePathPoints();

        currentPathIndex =
            0;

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

    private void ResolvePathPoints()
    {
        if (pathRoot == null)
        {
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

        if (points.Count > 0)
        {
            pathPoints =
                points.ToArray();
        }
    }

    private bool TryGetCurrentPathPoint(
        out Transform point)
    {
        point =
            null;

        if (pathPoints == null ||
            pathPoints.Length == 0 ||
            currentPathIndex < 0 ||
            currentPathIndex >=
                pathPoints.Length)
        {
            return false;
        }

        point =
            pathPoints[
                currentPathIndex];

        return
            point != null &&
            IsFiniteVector(
                point.position);
    }

    private void CheckPathPointArrival(
        Vector3 targetPosition)
    {
        if (vehicleRigidbody == null)
        {
            return;
        }

        float distance =
            Vector3.Distance(
                vehicleRigidbody.position,
                targetPosition);

        if (!float.IsFinite(
                distance) ||
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
            currentPathIndex =
                0;

            return;
        }

        currentPathIndex =
            pathPoints.Length - 1;

        if (stopAtFinalPoint)
        {
            CompleteTrack();
        }
        else
        {
            StopDriving();
        }
    }

    #endregion

    #region Driving API

    public bool StartDriving()
    {
        if (!CanDrive())
        {
            return false;
        }

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
        {
            return false;
        }

        ChangeState(
            VehicleState.Stopping);

        return true;
    }

    private bool CanDrive()
    {
        return
            initialized &&
            !safetyShutdown &&
            vehicleRigidbody != null &&
            pathPoints != null &&
            pathPoints.Length > 0 &&
            currentState !=
                VehicleState.Finished &&
            currentState !=
                VehicleState.Disabled;
    }

    #endregion

    #region Vehicle States

    private void UpdateWaiting()
    {
        StopVehicleMotion();
    }

    private void UpdateBoarding()
    {
        StopVehicleMotion();
    }

    private void UpdateDriving()
    {
        if (!TryGetCurrentPathPoint(
                out Transform target))
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
            target.position);

        UpdateSteering();

        ApplyDrivingVelocity();

        RotateToTrack();

        CheckPathPointArrival(
            target.position);

        OnDrivingPhysicsUpdated();
    }

    private void UpdateAirborne()
    {
        if (!TryGetCurrentPathPoint(
                out Transform target))
        {
            CompleteTrack();

            return;
        }

        UpdateTrackDirection(
            target.position);

        ApplyAirborneVelocity();

        RotateToTrack();

        CheckPathPointArrival(
            target.position);

        OnAirbornePhysicsUpdated();
    }

    private void UpdateStopping()
    {
        currentSpeed =
            Mathf.MoveTowards(
                currentSpeed,
                0f,
                deceleration *
                    Time.fixedDeltaTime);

        ApplyDrivingVelocity();

        if (currentSpeed >
            MinimumSpeed)
        {
            return;
        }

        StopVehicleMotion();
        StopDrivePresentation();

        ChangeState(
            VehicleState.Waiting);

        OnVehicleStopped();
    }

    private void UpdateDismounting()
    {
        StopVehicleMotion();
    }

    private void UpdateFinished()
    {
        StopVehicleMotion();
    }

    #endregion

    #region Movement

    private void UpdateTrackDirection(
        Vector3 targetPosition)
    {
        if (vehicleRigidbody == null)
        {
            return;
        }

        Vector3 direction =
            targetPosition -
            vehicleRigidbody.position;

        direction.y =
            0f;

        if (!IsFiniteVector(
                direction) ||
            direction.sqrMagnitude <=
                MinimumDirectionMagnitude)
        {
            return;
        }

        trackDirection =
            direction.normalized;
    }

    private void UpdateSteering()
    {
        float targetOffset =
            allowSteering
                ? Input.GetAxisRaw(
                        "Horizontal") *
                    maximumTrackOffset
                : 0f;

        float changeSpeed =
            allowSteering
                ? steeringSpeed
                : steeringReturnSpeed;

        currentTrackOffset =
            Mathf.MoveTowards(
                currentTrackOffset,
                targetOffset,
                changeSpeed *
                    Time.fixedDeltaTime);
    }

    private void ApplyDrivingVelocity()
    {
        if (vehicleRigidbody == null)
        {
            return;
        }

        Vector3 right =
            Vector3.Cross(
                groundNormal,
                trackDirection);

        if (right.sqrMagnitude <=
            MinimumDirectionMagnitude)
        {
            right =
                transform.right;
        }
        else
        {
            right.Normalize();
        }

        Vector3 velocity =
            trackDirection *
                currentSpeed +
            right *
                currentTrackOffset;

        velocity.y =
            vehicleRigidbody
                .linearVelocity.y;

        if (!IsFiniteVector(
                velocity))
        {
            EnterSafetyShutdown();

            return;
        }

        vehicleRigidbody.linearVelocity =
            velocity;
    }

    private void ApplyAirborneVelocity()
    {
        if (vehicleRigidbody == null)
        {
            return;
        }

        Vector3 velocity =
            vehicleRigidbody
                .linearVelocity;

        Vector3 forwardVelocity =
            trackDirection *
                currentSpeed;

        velocity.x =
            forwardVelocity.x;

        velocity.z =
            forwardVelocity.z;

        if (!IsFiniteVector(
                velocity))
        {
            EnterSafetyShutdown();

            return;
        }

        vehicleRigidbody.linearVelocity =
            velocity;
    }

    private void RotateToTrack()
    {
        if (vehicleRigidbody == null ||
            trackDirection.sqrMagnitude <=
                MinimumDirectionMagnitude)
        {
            return;
        }

        Vector3 up =
            grounded &&
            IsFiniteVector(
                groundNormal)
                ? groundNormal
                : Vector3.up;

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
            Quaternion.RotateTowards(
                vehicleRigidbody.rotation,
                targetRotation,
                rotationSpeed *
                    Time.fixedDeltaTime);

        vehicleRigidbody.MoveRotation(
            nextRotation);
    }

    #endregion

    #region Jump And Grounding

    private void Jump()
    {
        if (vehicleRigidbody == null ||
            !grounded)
        {
            return;
        }

        Vector3 velocity =
            vehicleRigidbody
                .linearVelocity;

        velocity.y =
            jumpVelocity;

        if (!IsFiniteVector(
                velocity))
        {
            return;
        }

        vehicleRigidbody.linearVelocity =
            velocity;

        grounded =
            false;

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
        bool previousGrounded =
            grounded;

        grounded =
            DetectGround(
                out groundNormal);

        if (previousGrounded &&
            !grounded &&
            currentState ==
                VehicleState.Driving)
        {
            ChangeState(
                VehicleState.Airborne);
        }
        else if (!previousGrounded &&
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
        {
            return false;
        }

        Vector3 origin =
            vehicleRigidbody.position +
            Vector3.up * 0.1f;

        if (!Physics.SphereCast(
                origin,
                groundProbeRadius,
                Vector3.down,
                out RaycastHit hit,
                groundProbeDistance,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (hit.collider == null ||
            IsVehicleCollider(
                hit.collider))
        {
            return false;
        }

        float slope =
            Vector3.Angle(
                hit.normal,
                Vector3.up);

        if (!float.IsFinite(
                slope) ||
            slope >
                maximumGroundSlope)
        {
            return false;
        }

        normal =
            hit.normal.normalized;

        return
            IsFiniteVector(
                normal);
    }

    #endregion

    #region Boarding API

    public bool BoardVehicle(
        UltimatePlayerMovement movement)
    {
        if (!CanBoard(
                movement))
        {
            return false;
        }

        if (!boardEntireTeamAutomatically)
        {
            return
                BoardSingleRider(
                    movement);
        }

        CollectTeamCandidates(
            movement);

        if (boardingCandidates.Count == 0)
        {
            return false;
        }

        ChangeState(
            VehicleState.Boarding);

        int boardedCount =
            0;

        for (int index = 0;
            index <
                boardingCandidates.Count &&
            boardedCount <
                maximumRiders;
            index++)
        {
            if (BoardRiderInternal(
                    boardingCandidates[
                        index]))
            {
                boardedCount++;
            }
        }

        boardingCandidates.Clear();

        ChangeState(
            VehicleState.Waiting);

        if (boardedCount == 0)
        {
            return false;
        }

        if (startAutomatically)
        {
            StartDriving();
        }

        return true;
    }

    public bool BoardSingleRider(
        UltimatePlayerMovement movement)
    {
        if (!CanBoard(
                movement))
        {
            return false;
        }

        ChangeState(
            VehicleState.Boarding);

        bool boarded =
            BoardRiderInternal(
                movement);

        ChangeState(
            VehicleState.Waiting);

        if (boarded &&
            startAutomatically)
        {
            StartDriving();
        }

        return boarded;
    }

    public bool DismountAll()
    {
        if (riders.Count == 0)
        {
            return false;
        }

        bool wasDriving =
            IsDriving;

        ChangeState(
            VehicleState.Dismounting);

        DismountAllSafely();

        if (currentState !=
                VehicleState.Finished &&
            currentState !=
                VehicleState.Disabled)
        {
            ChangeState(
                wasDriving
                    ? VehicleState.Stopping
                    : VehicleState.Waiting);
        }

        return true;
    }

    #endregion

    #region Boarding Implementation

    private bool BoardRiderInternal(
        UltimatePlayerMovement movement)
    {
        if (!CanBoard(
                movement))
        {
            return false;
        }

        Rigidbody riderBody =
            ResolveRigidbody(
                movement);

        RiderState state =
            new()
            {
                movement =
                    movement,

                originalParent =
                    movement.transform.parent,

                originalKinematic =
                    riderBody != null &&
                    riderBody.isKinematic,

                originalGravity =
                    riderBody == null ||
                    riderBody.useGravity
            };

        riders.Add(
            movement);

        riderStates.Add(
            state);

        movement.DisableMovement();

        if (riderBody != null)
        {
            riderBody.linearVelocity =
                Vector3.zero;

            riderBody.angularVelocity =
                Vector3.zero;

            riderBody.useGravity =
                false;

            riderBody.isKinematic =
                true;
        }

        Transform seat =
            ResolveSeat(
                riders.Count - 1);

        movement.transform.SetParent(
            seat,
            worldPositionStays: false);

        movement.transform.localPosition =
            Vector3.zero;

        movement.transform.localRotation =
            Quaternion.identity;

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
            initialized &&
            allowBoarding &&
            !safetyShutdown &&
            movement != null &&
            movement.gameObject
                .activeInHierarchy &&
            !riders.Contains(
                movement) &&
            riders.Count <
                maximumRiders &&
            currentState !=
                VehicleState.Finished &&
            currentState !=
                VehicleState.Disabled;
    }

    private void CollectTeamCandidates(
        UltimatePlayerMovement leader)
    {
        boardingCandidates.Clear();

        if (leader == null)
        {
            return;
        }

        AddBoardingCandidate(
            leader);

        Transform root =
            leader.transform.root;

        if (root == null)
        {
            return;
        }

        UltimatePlayerMovement[] movements =
            root.GetComponentsInChildren<
                UltimatePlayerMovement>(
                    includeInactive: false);

        for (int index = 0;
            index < movements.Length;
            index++)
        {
            AddBoardingCandidate(
                movements[index]);

            if (boardingCandidates.Count >=
                maximumRiders)
            {
                break;
            }
        }
    }

    private void AddBoardingCandidate(
        UltimatePlayerMovement movement)
    {
        if (!CanBoard(
                movement) ||
            boardingCandidates.Contains(
                movement))
        {
            return;
        }

        boardingCandidates.Add(
            movement);
    }

    private Transform ResolveSeat(
        int riderIndex)
    {
        if (seatPoints != null &&
            riderIndex >= 0 &&
            riderIndex <
                seatPoints.Length &&
            seatPoints[riderIndex] !=
                null)
        {
            return
                seatPoints[
                    riderIndex];
        }

        return transform;
    }

    private void DismountAllSafely()
    {
        for (int index =
                riders.Count - 1;
            index >= 0;
            index--)
        {
            DismountRider(
                index);
        }

        riders.Clear();
        riderStates.Clear();
    }

    private void DismountRider(
        int index)
    {
        if (index < 0 ||
            index >= riders.Count ||
            index >= riderStates.Count)
        {
            return;
        }

        UltimatePlayerMovement movement =
            riders[index];

        RiderState state =
            riderStates[index];

        riders.RemoveAt(
            index);

        riderStates.RemoveAt(
            index);

        if (movement == null)
        {
            return;
        }

        movement.transform.SetParent(
            state.originalParent,
            worldPositionStays: true);

        Transform destination =
            exitPoint != null
                ? exitPoint
                : transform;

        Vector3 destinationPosition =
            destination.position;

        if (IsFiniteVector(
                destinationPosition))
        {
            movement.transform.SetPositionAndRotation(
                destinationPosition,
                destination.rotation);
        }

        Rigidbody riderBody =
            ResolveRigidbody(
                movement);

        if (riderBody != null)
        {
            riderBody.isKinematic =
                state.originalKinematic;

            riderBody.useGravity =
                state.originalGravity;

            riderBody.linearVelocity =
                Vector3.zero;

            riderBody.angularVelocity =
                Vector3.zero;

            if (!riderBody.isKinematic)
            {
                riderBody.WakeUp();
            }
        }

        movement.EnableMovement();

        RiderDismounted?.Invoke(
            movement);

        OnRiderDismounted(
            movement);
    }

    #endregion

    #region Checkpoints

    public void NotifyCheckpointReached()
    {
        if (!initialized ||
            checkpointReached)
        {
            return;
        }

        checkpointReached =
            true;

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
        {
            return;
        }

        StopDriving();
    }

    public bool CompleteTrack()
    {
        if (!initialized ||
            currentState ==
                VehicleState.Finished)
        {
            return false;
        }

        StopVehicleMotion();
        StopDrivePresentation();

        DismountAllSafely();

        ChangeState(
            VehicleState.Finished);

        TrackCompleted?.Invoke();

        OnTrackCompleted();

        return true;
    }

    public bool ResetVehicle()
    {
        if (shuttingDown)
        {
            return false;
        }

        DismountAllSafely();

        StopVehicleMotion();
        StopDrivePresentation();

        initialized =
            false;

        safetyShutdown =
            false;

        checkpointReached =
            false;

        currentPathIndex =
            0;

        currentTrackOffset =
            0f;

        currentSpeed =
            0f;

        if (vehicleRigidbody != null &&
            IsFiniteVector(
                startingPosition))
        {
            vehicleRigidbody.position =
                startingPosition;
        }

        enabled =
            true;

        return
            InitializeVehicle();
    }

    #endregion

    #region State

    protected void ChangeState(
        VehicleState newState)
    {
        if (!Enum.IsDefined(
                typeof(VehicleState),
                newState) ||
            currentState ==
                newState)
        {
            return;
        }

        VehicleState previous =
            currentState;

        currentState =
            newState;

        UpdateAnimator();

        StateChanged?.Invoke(
            currentState);

        OnStateChanged(
            previous,
            currentState);
    }

    #endregion

    #region Safety

    private bool CanRun()
    {
        return
            initialized &&
            !shuttingDown &&
            !safetyShutdown &&
            currentState !=
                VehicleState.Disabled &&
            currentState !=
                VehicleState.Finished;
    }

    private void RunSafetyChecks()
    {
        if (vehicleRigidbody == null ||
            vehicleCollider == null ||
            !vehicleCollider.enabled ||
            !HasValidScale())
        {
            EnterSafetyShutdown();

            return;
        }

        Vector3 position =
            vehicleRigidbody.position;

        Vector3 velocity =
            vehicleRigidbody
                .linearVelocity;

        Vector3 angularVelocity =
            vehicleRigidbody
                .angularVelocity;

        if (!IsFiniteVector(
                position) ||
            !IsFiniteVector(
                velocity) ||
            !IsFiniteVector(
                angularVelocity))
        {
            EnterSafetyShutdown();

            return;
        }

        if (velocity.magnitude >
            maximumLinearSpeed)
        {
            vehicleRigidbody.linearVelocity =
                Vector3.ClampMagnitude(
                    velocity,
                    maximumLinearSpeed);
        }

        if (angularVelocity.magnitude >
            maximumAngularSpeed)
        {
            vehicleRigidbody.angularVelocity =
                Vector3.ClampMagnitude(
                    angularVelocity,
                    maximumAngularSpeed);
        }

        RemoveDestroyedRiders();
    }

    private void EnterSafetyShutdown()
    {
        if (safetyShutdown)
        {
            return;
        }

        safetyShutdown =
            true;

        StopVehicleMotion();
        StopDrivePresentation();

        DismountAllSafely();

        ChangeState(
            VehicleState.Disabled);
    }

    private bool HasValidScale()
    {
        Vector3 scale =
            transform.lossyScale;

        return
            IsFiniteVector(
                scale) &&
            Mathf.Abs(scale.x) >=
                minimumValidScale &&
            Mathf.Abs(scale.y) >=
                minimumValidScale &&
            Mathf.Abs(scale.z) >=
                minimumValidScale;
    }

    private void RemoveDestroyedRiders()
    {
        for (int index =
                riders.Count - 1;
            index >= 0;
            index--)
        {
            if (riders[index] != null)
            {
                continue;
            }

            riders.RemoveAt(
                index);

            if (index <
                riderStates.Count)
            {
                riderStates.RemoveAt(
                    index);
            }
        }
    }

    #endregion

    #region Presentation

    private void CacheAnimatorParameters()
    {
        animatorParameters.Clear();

        if (vehicleAnimator == null ||
            vehicleAnimator
                .runtimeAnimatorController ==
                null)
        {
            return;
        }

        foreach (
            AnimatorControllerParameter parameter
            in vehicleAnimator.parameters)
        {
            animatorParameters.Add(
                parameter.nameHash);
        }
    }

    private void UpdateAnimator()
    {
        if (vehicleAnimator == null ||
            !vehicleAnimator.isActiveAndEnabled)
        {
            return;
        }

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

        if (vehicleAudioSource == null ||
            driveLoop == null)
        {
            return;
        }

        vehicleAudioSource.clip =
            driveLoop;

        vehicleAudioSource.loop =
            true;

        vehicleAudioSource.Play();
    }

    private void StopDrivePresentation()
    {
        SetAnimatorTrigger(
            StopHash);

        if (driveEffect != null)
        {
            driveEffect.Stop(
                true,
                ParticleSystemStopBehavior
                    .StopEmitting);
        }

        if (vehicleAudioSource != null &&
            vehicleAudioSource.clip ==
                driveLoop)
        {
            vehicleAudioSource.Stop();

            vehicleAudioSource.loop =
                false;

            vehicleAudioSource.clip =
                null;
        }

        stopEffect?.Play();

        PlayOneShot(
            stopClip);
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

    #endregion

    #region Helpers

    private void UpdateTimers()
    {
        if (jumpCooldownTimer <=
            0f)
        {
            return;
        }

        jumpCooldownTimer =
            Mathf.Max(
                0f,
                jumpCooldownTimer -
                Time.deltaTime);
    }

    private void StopVehicleMotion()
    {
        if (vehicleRigidbody == null)
        {
            return;
        }

        vehicleRigidbody.linearVelocity =
            Vector3.zero;

        vehicleRigidbody.angularVelocity =
            Vector3.zero;

        currentSpeed =
            0f;

        currentTrackOffset =
            0f;
    }

    private static UltimatePlayerMovement
        ResolveMovement(
            Collider collider)
    {
        if (collider == null)
        {
            return null;
        }

        UltimatePlayerMovement movement =
            collider.GetComponent<
                UltimatePlayerMovement>();

        movement ??=
            collider.GetComponentInParent<
                UltimatePlayerMovement>();

        movement ??=
            collider.GetComponentInChildren<
                UltimatePlayerMovement>(
                    includeInactive: true);

        return movement;
    }

    private static Rigidbody ResolveRigidbody(
        UltimatePlayerMovement movement)
    {
        if (movement == null)
        {
            return null;
        }

        if (movement.body != null)
        {
            return
                movement.body;
        }

        Rigidbody result =
            movement.GetComponent<
                Rigidbody>();

        result ??=
            movement.GetComponentInParent<
                Rigidbody>();

        return result;
    }

    private bool IsVehicleCollider(
        Collider candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        return
            candidate ==
                vehicleCollider ||
            candidate.transform
                .IsChildOf(
                    transform);
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

    private void DrawPath()
    {
        if (pathPoints == null)
        {
            return;
        }

        for (int index = 0;
            index < pathPoints.Length;
            index++)
        {
            Transform point =
                pathPoints[index];

            if (point == null)
            {
                continue;
            }

            Gizmos.DrawWireSphere(
                point.position,
                pointArrivalDistance);

            if (index + 1 >=
                    pathPoints.Length ||
                pathPoints[index + 1] ==
                    null)
            {
                continue;
            }

            Gizmos.DrawLine(
                point.position,
                pathPoints[index + 1]
                    .position);
        }
    }

    #endregion

    #region Extensibility

    protected virtual void OnStateChanged(
        VehicleState previousState,
        VehicleState newState)
    {
        if (previousState ==
                VehicleState.Airborne &&
            newState ==
                VehicleState.Driving)
        {
            currentTrackOffset =
                Mathf.Clamp(
                    currentTrackOffset,
                    -maximumTrackOffset,
                    maximumTrackOffset);
        }

        if (newState ==
                VehicleState.Disabled ||
            newState ==
                VehicleState.Finished)
        {
            StopVehicleMotion();
        }
    }

    protected virtual void OnDrivingStarted()
    {
        if (vehicleRigidbody == null)
        {
            return;
        }

        if (currentSpeed <
            minimumDrivingSpeed)
        {
            currentSpeed =
                minimumDrivingSpeed;
        }

        vehicleRigidbody.WakeUp();
    }

    protected virtual void OnDrivingPhysicsUpdated()
    {
        if (vehicleRigidbody == null)
        {
            return;
        }

        Vector3 velocity =
            vehicleRigidbody.linearVelocity;

        if (!IsFiniteVector(
                velocity))
        {
            EnterSafetyShutdown();

            return;
        }

        if (velocity.magnitude >
            maximumLinearSpeed)
        {
            vehicleRigidbody.linearVelocity =
                Vector3.ClampMagnitude(
                    velocity,
                    maximumLinearSpeed);
        }
    }

    protected virtual void OnAirbornePhysicsUpdated()
    {
        if (vehicleRigidbody == null)
        {
            return;
        }

        Vector3 velocity =
            vehicleRigidbody.linearVelocity;

        if (!IsFiniteVector(
                velocity))
        {
            EnterSafetyShutdown();

            return;
        }

        if (velocity.magnitude >
            maximumLinearSpeed)
        {
            vehicleRigidbody.linearVelocity =
                Vector3.ClampMagnitude(
                    velocity,
                    maximumLinearSpeed);
        }
    }

    protected virtual void OnVehicleJumped()
    {
        grounded =
            false;

        groundNormal =
            Vector3.up;

        jumpCooldownTimer =
            Mathf.Max(
                jumpCooldownTimer,
                jumpCooldown);
    }

    protected virtual void OnVehicleStopped()
    {
        currentSpeed =
            0f;

        currentTrackOffset =
            0f;

        if (vehicleRigidbody != null)
        {
            vehicleRigidbody.linearVelocity =
                Vector3.zero;

            vehicleRigidbody.angularVelocity =
                Vector3.zero;
        }
    }

    protected virtual void OnVehicleCollision(
        Collision collision)
    {
        if (collision == null ||
            collision.collider == null ||
            vehicleRigidbody == null)
        {
            return;
        }

        Vector3 velocity =
            vehicleRigidbody.linearVelocity;

        if (!IsFiniteVector(
                velocity))
        {
            EnterSafetyShutdown();

            return;
        }

        if (velocity.magnitude >
            maximumLinearSpeed)
        {
            vehicleRigidbody.linearVelocity =
                Vector3.ClampMagnitude(
                    velocity,
                    maximumLinearSpeed);
        }
    }

    protected virtual void OnRiderBoarded(
        UltimatePlayerMovement movement)
    {
        if (movement == null)
        {
            return;
        }

        Transform rider =
            movement.transform;

        if (!IsFiniteVector(
                rider.position))
        {
            DismountAllSafely();

            return;
        }

        Rigidbody riderBody =
            ResolveRigidbody(
                movement);

        if (riderBody == null)
        {
            return;
        }

        riderBody.linearVelocity =
            Vector3.zero;

        riderBody.angularVelocity =
            Vector3.zero;
    }

    protected virtual void OnRiderDismounted(
        UltimatePlayerMovement movement)
    {
        if (movement == null)
        {
            return;
        }

        Rigidbody riderBody =
            ResolveRigidbody(
                movement);

        if (riderBody == null ||
            riderBody.isKinematic)
        {
            return;
        }

        riderBody.linearVelocity =
            Vector3.zero;

        riderBody.angularVelocity =
            Vector3.zero;

        riderBody.WakeUp();
    }

    protected virtual void OnCheckpointReached()
    {
        currentTrackOffset =
            0f;

        if (currentPathIndex < 0)
        {
            currentPathIndex =
                0;
        }

        if (pathPoints != null &&
            pathPoints.Length > 0)
        {
            currentPathIndex =
                Mathf.Clamp(
                    currentPathIndex,
                    0,
                    pathPoints.Length - 1);
        }
    }

    protected virtual void OnTrackCompleted()
    {
        currentSpeed =
            0f;

        currentTrackOffset =
            0f;

        StopVehicleMotion();
        StopDrivePresentation();
    }

    #endregion
}