using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FlyingEnemyAI : AIController
{
    #region Types

    public enum FlightMovementState
    {
        Stable,
        Ascending,
        Descending,
        AvoidingObstacle,
        ReturningToFlightArea,
        Recovering,
        Disabled
    }

    #endregion

    #region Constants

    private const float MinimumDirectionSqrMagnitude =
        0.0001f;

    private const float MinimumFlightHeight =
        0.1f;

    private const int ObstacleHitCapacity =
        16;

    #endregion

    #region Inspector

    [Header("Flight Movement")]
    [SerializeField, Min(0f)] private float flightSpeed = 8f;
    [SerializeField, Min(0f)] private float flightAcceleration = 20f;
    [SerializeField, Min(0f)] private float flightDeceleration = 25f;
    [SerializeField, Min(0f)] private float rotationSpeed = 360f;
    [SerializeField, Min(0f)] private float arrivalDistance = 0.75f;

    [Header("Hover")]
    [SerializeField] private bool enableHover = true;
    [SerializeField, Min(0f)] private float hoverAmplitude = 0.25f;
    [SerializeField, Min(0f)] private float hoverFrequency = 1.5f;
    [SerializeField, Min(0f)] private float hoverSmoothing = 8f;

    [Header("Altitude")]
    [SerializeField] private bool maintainFlightAltitude = true;
    [SerializeField, Min(MinimumFlightHeight)] private float preferredFlightHeight = 4f;
    [SerializeField, Min(MinimumFlightHeight)] private float minimumFlightHeight = 1.5f;
    [SerializeField, Min(MinimumFlightHeight)] private float maximumFlightHeight = 12f;
    [SerializeField, Min(0f)] private float altitudeCorrectionSpeed = 6f;
    [SerializeField, Min(0.01f)] private float groundProbeDistance = 30f;
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Target Positioning")]
    [SerializeField] private bool maintainTargetAltitudeOffset = true;
    [SerializeField] private float targetAltitudeOffset = 2f;
    [SerializeField, Min(0f)] private float chaseStoppingDistance = 4f;
    [SerializeField] private bool circleTarget;
    [SerializeField, Min(0f)] private float circleRadius = 5f;
    [SerializeField, Min(0f)] private float circleSpeed = 45f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private bool avoidObstacles = true;
    [SerializeField, Min(0.01f)] private float obstacleProbeRadius = 0.5f;
    [SerializeField, Min(0.01f)] private float obstacleProbeDistance = 2.5f;
    [SerializeField, Min(0f)] private float avoidanceStrength = 8f;
    [SerializeField, Min(0f)] private float avoidanceDuration = 0.35f;
    [SerializeField] private LayerMask obstacleLayers = ~0;

    [Header("Flight Area")]
    [SerializeField] private bool constrainToFlightArea = true;
    [SerializeField, Min(0f)] private float maximumHorizontalDistanceFromHome = 25f;
    [SerializeField, Min(0f)] private float maximumVerticalDistanceFromHome = 15f;
    [SerializeField, Min(0f)] private float flightAreaReturnSpeed = 10f;

    [Header("Stuck Detection")]
    [SerializeField] private bool detectStuckMovement = true;
    [SerializeField, Min(0.1f)] private float stuckCheckInterval = 1f;
    [SerializeField, Min(0f)] private float minimumMovementDistance = 0.05f;
    [SerializeField, Min(1)] private int maximumStuckChecks = 3;

    [Header("Recovery")]
    [SerializeField] private bool recoverAutomatically = true;
    [SerializeField, Min(0.1f)] private float recoveryInterval = 0.5f;
    [SerializeField, Min(1)] private int maximumRecoveryAttempts = 5;
    [SerializeField, Min(0f)] private float knockbackRecoveryDelay = 0.2f;

    [Header("Physics")]
    [SerializeField] private Rigidbody flightRigidbody;
    [SerializeField] private bool useRigidbodyMovement = true;
    [SerializeField] private bool disableGravity = true;
    [SerializeField] private bool freezeRigidbodyRotation = true;

    [Header("Runtime Safety")]
    [SerializeField] private bool restoreFlightComponents = true;
    [SerializeField, Min(1f)] private float maximumSafeFlightSpeed = 100f;
    [SerializeField, Min(0.01f)] private float minimumValidScale = 0.01f;

    [Header("Debug")]
    [SerializeField]
    private FlightMovementState flightMovementState =
        FlightMovementState.Stable;

    [SerializeField] private bool logFlightSafety;

    #endregion

    #region Runtime State

    private readonly RaycastHit[] obstacleHits =
        new RaycastHit[ObstacleHitCapacity];

    private Vector3 currentVelocity;
    private Vector3 desiredVelocity;
    private Vector3 avoidanceDirection;
    private Vector3 previousCheckedPosition;
    private Vector3 lastValidFlightPosition;
    private Vector3 baseHoverPosition;

    private float hoverOffset;
    private float circleAngle;
    private float avoidanceTimer;
    private float stuckCheckTimer;
    private float recoveryTimer;
    private float knockbackRecoveryTimer;

    private int consecutiveStuckChecks;
    private int recoveryAttempts;

    private bool movementRequested;
    private bool knockbackRecoveryPending;
    private bool initializedFlight;
    private bool shuttingDownFlight;

    #endregion

    #region Events

    public event Action<FlyingEnemyAI> ObstacleDetected;
    public event Action<FlyingEnemyAI> FlightAreaExited;
    public event Action<FlyingEnemyAI, Vector3> FlightRecovered;
    public event Action<FlyingEnemyAI> MovementStuck;
    public event Action<FlyingEnemyAI, FlightMovementState> FlightStateChanged;

    #endregion

    #region Public API

    public FlightMovementState CurrentFlightMovementState =>
        flightMovementState;

    public Vector3 CurrentFlightVelocity =>
        currentVelocity;

    public Vector3 LastValidFlightPosition =>
        lastValidFlightPosition;

    public bool IsFlightInitialized =>
        initializedFlight;

    public bool RecoverFlight()
    {
        return TryRecoverFlight(
            forceRecovery: true);
    }

    public void NotifyKnockbackEnded()
    {
        if (!recoverAutomatically ||
            IsDead)
        {
            return;
        }

        knockbackRecoveryPending =
            true;

        knockbackRecoveryTimer =
            knockbackRecoveryDelay;
    }

    public bool SetFlightPosition(
        Vector3 position)
    {
        if (!IsFiniteVector(
                position))
        {
            return false;
        }

        ApplyFlightPosition(
            position);

        lastValidFlightPosition =
            position;

        currentVelocity =
            Vector3.zero;

        desiredVelocity =
            Vector3.zero;

        return true;
    }

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        ResolveFlightReferences();
        ConfigureFlightComponents();
        InitializeFlightRuntime();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (shuttingDownFlight)
            return;

        ResolveFlightReferences();
        ConfigureFlightComponents();
        InitializeFlightRuntime();
    }

    protected override void Update()
    {
        base.Update();

        if (!IsInitialized ||
            IsDead ||
            CurrentState ==
                AIState.Disabled)
        {
            return;
        }

        UpdateFlightTimers();
        UpdateHover();
        UpdateStuckDetection();
        ValidateFlightArea();
    }

    private void FixedUpdate()
    {
        if (!IsInitialized ||
            !initializedFlight ||
            IsDead ||
            CurrentState ==
                AIState.Disabled)
        {
            return;
        }

        UpdateFlightMovement();
    }

    protected override void OnDisable()
    {
        StopFlightMovement();

        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        shuttingDownFlight =
            true;

        ObstacleDetected = null;
        FlightAreaExited = null;
        FlightRecovered = null;
        MovementStuck = null;
        FlightStateChanged = null;

        flightRigidbody = null;

        base.OnDestroy();
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        flightSpeed =
            Mathf.Max(
                0f,
                flightSpeed);

        flightAcceleration =
            Mathf.Max(
                0f,
                flightAcceleration);

        flightDeceleration =
            Mathf.Max(
                0f,
                flightDeceleration);

        rotationSpeed =
            Mathf.Max(
                0f,
                rotationSpeed);

        arrivalDistance =
            Mathf.Max(
                0f,
                arrivalDistance);

        hoverAmplitude =
            Mathf.Max(
                0f,
                hoverAmplitude);

        hoverFrequency =
            Mathf.Max(
                0f,
                hoverFrequency);

        hoverSmoothing =
            Mathf.Max(
                0f,
                hoverSmoothing);

        preferredFlightHeight =
            Mathf.Max(
                MinimumFlightHeight,
                preferredFlightHeight);

        minimumFlightHeight =
            Mathf.Max(
                MinimumFlightHeight,
                minimumFlightHeight);

        maximumFlightHeight =
            Mathf.Max(
                minimumFlightHeight,
                maximumFlightHeight);

        preferredFlightHeight =
            Mathf.Clamp(
                preferredFlightHeight,
                minimumFlightHeight,
                maximumFlightHeight);

        altitudeCorrectionSpeed =
            Mathf.Max(
                0f,
                altitudeCorrectionSpeed);

        groundProbeDistance =
            Mathf.Max(
                0.01f,
                groundProbeDistance);

        chaseStoppingDistance =
            Mathf.Max(
                0f,
                chaseStoppingDistance);

        circleRadius =
            Mathf.Max(
                0f,
                circleRadius);

        circleSpeed =
            Mathf.Max(
                0f,
                circleSpeed);

        obstacleProbeRadius =
            Mathf.Max(
                0.01f,
                obstacleProbeRadius);

        obstacleProbeDistance =
            Mathf.Max(
                0.01f,
                obstacleProbeDistance);

        avoidanceStrength =
            Mathf.Max(
                0f,
                avoidanceStrength);

        avoidanceDuration =
            Mathf.Max(
                0f,
                avoidanceDuration);

        maximumHorizontalDistanceFromHome =
            Mathf.Max(
                0f,
                maximumHorizontalDistanceFromHome);

        maximumVerticalDistanceFromHome =
            Mathf.Max(
                0f,
                maximumVerticalDistanceFromHome);

        flightAreaReturnSpeed =
            Mathf.Max(
                0f,
                flightAreaReturnSpeed);

        stuckCheckInterval =
            Mathf.Max(
                0.1f,
                stuckCheckInterval);

        minimumMovementDistance =
            Mathf.Max(
                0f,
                minimumMovementDistance);

        maximumStuckChecks =
            Mathf.Max(
                1,
                maximumStuckChecks);

        recoveryInterval =
            Mathf.Max(
                0.1f,
                recoveryInterval);

        maximumRecoveryAttempts =
            Mathf.Max(
                1,
                maximumRecoveryAttempts);

        knockbackRecoveryDelay =
            Mathf.Max(
                0f,
                knockbackRecoveryDelay);

        maximumSafeFlightSpeed =
            Mathf.Max(
                1f,
                maximumSafeFlightSpeed);

        minimumValidScale =
            Mathf.Max(
                0.01f,
                minimumValidScale);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ResolveFlightReferences();
            ConfigureFlightComponents();
        }
#endif
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Vector3 origin =
            transform.position;

        Vector3 forward =
            GetFlightForward();

        Gizmos.DrawWireSphere(
            origin +
            forward *
            obstacleProbeDistance,
            obstacleProbeRadius);

        Gizmos.DrawLine(
            origin,
            origin +
            Vector3.down *
            groundProbeDistance);

        if (maximumHorizontalDistanceFromHome >
            0f)
        {
            Gizmos.DrawWireSphere(
                Application.isPlaying
                    ? HomePosition
                    : transform.position,
                maximumHorizontalDistanceFromHome);
        }

        Gizmos.DrawLine(
            origin,
            origin +
            currentVelocity);
    }

    #endregion

    #region Initialization

    protected override bool Initialize()
    {
        bool initialized =
            base.Initialize();

        if (!initialized)
            return false;

        ResolveFlightReferences();
        ConfigureFlightComponents();
        InitializeFlightRuntime();

        return initializedFlight;
    }

    private void ResolveFlightReferences()
    {
        if (flightRigidbody != null)
            return;

        flightRigidbody =
            GetComponent<Rigidbody>();

        flightRigidbody ??=
            GetComponentInParent<Rigidbody>();

        flightRigidbody ??=
            GetComponentInChildren<Rigidbody>(
                includeInactive: true);
    }

    private void ConfigureFlightComponents()
    {
        if (Agent != null)
        {
            Agent.updatePosition =
                false;

            Agent.updateRotation =
                false;

            Agent.isStopped =
                true;
        }

        if (flightRigidbody == null)
            return;

        flightRigidbody.useGravity =
            !disableGravity;

        flightRigidbody.isKinematic =
            !useRigidbodyMovement;

        flightRigidbody.interpolation =
            RigidbodyInterpolation.Interpolate;

        flightRigidbody.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;

        if (freezeRigidbodyRotation)
        {
            flightRigidbody.constraints =
                RigidbodyConstraints.FreezeRotation;
        }

        flightRigidbody.WakeUp();
    }

    private void InitializeFlightRuntime()
    {
        if (!IsFiniteVector(
                transform.position))
        {
            initializedFlight =
                false;

            return;
        }

        currentVelocity =
            Vector3.zero;

        desiredVelocity =
            Vector3.zero;

        avoidanceDirection =
            Vector3.zero;

        previousCheckedPosition =
            transform.position;

        lastValidFlightPosition =
            transform.position;

        baseHoverPosition =
            transform.position;

        hoverOffset =
            0f;

        circleAngle =
            0f;

        avoidanceTimer =
            0f;

        stuckCheckTimer =
            stuckCheckInterval;

        recoveryTimer =
            0f;

        knockbackRecoveryTimer =
            0f;

        consecutiveStuckChecks =
            0;

        recoveryAttempts =
            0;

        movementRequested =
            false;

        knockbackRecoveryPending =
            false;

        ChangeFlightState(
            FlightMovementState.Stable);

        initializedFlight =
            true;
    }

    #endregion

    #region AI State Overrides

    protected override void UpdateGrounding()
    {
    }

    protected override void UpdatePatrolState()
    {
        if (TryAcquireTarget())
        {
            SetState(
                AIState.Chase);

            return;
        }

        Transform patrolPoint =
            GetCurrentPatrolPoint();

        if (patrolPoint == null)
        {
            SetState(
                AIState.Idle);

            return;
        }

        Vector3 patrolDestination =
            ResolveFlightDestination(
                patrolPoint.position,
                maintainPreferredAltitude: false);

        MoveAgentTo(
            patrolDestination);

        if (!HasReachedFlightPosition(
                patrolDestination,
                arrivalDistance))
        {
            return;
        }

        StopFlightMovement();
        AdvancePatrolPoint();
    }

    protected override void UpdateChaseState()
    {
        if (Target == null ||
            !Target.gameObject.activeInHierarchy)
        {
            ReturnHome();
            return;
        }

        if (IsOutsideFlightArea(
                transform.position))
        {
            ReturnHome();
            return;
        }

        Vector3 destination =
            GetTargetFlightPosition();

        float distance =
            Vector3.Distance(
                transform.position,
                destination);

        if (!float.IsFinite(
                distance))
        {
            TryRecoverFlight(
                forceRecovery: true);

            return;
        }

        if (distance <=
            chaseStoppingDistance)
        {
            StopFlightMovement();
            FaceFlightTarget();
            return;
        }

        MoveAgentTo(
            destination);
    }

    protected override void UpdateReturningState()
    {
        Vector3 destination =
            ResolveFlightDestination(
                HomePosition,
                maintainPreferredAltitude: true);

        float previousSpeed =
            flightSpeed;

        if (flightAreaReturnSpeed >
            0f)
        {
            flightSpeed =
                flightAreaReturnSpeed;
        }

        MoveAgentTo(
            destination);

        flightSpeed =
            previousSpeed;

        if (!HasReachedFlightPosition(
                destination,
                arrivalDistance))
        {
            return;
        }

        StopFlightMovement();

        recoveryAttempts =
            0;

        ChangeFlightState(
            FlightMovementState.Stable);

        SetState(
            HasValidPatrolPoints()
                ? AIState.Patrol
                : AIState.Idle);
    }

    protected override void UpdateInAirState()
    {
        SetState(
            Target != null
                ? AIState.Chase
                : AIState.Idle);
    }

    protected override bool MoveAgentTo(
        Vector3 destination)
    {
        if (!initializedFlight ||
            IsDead ||
            !IsFiniteVector(
                destination))
        {
            return false;
        }

        Vector3 resolvedDestination =
            ResolveFlightDestination(
                destination,
                maintainPreferredAltitude: true);

        Vector3 direction =
            resolvedDestination -
            transform.position;

        if (direction.sqrMagnitude <=
            MinimumDirectionSqrMagnitude)
        {
            desiredVelocity =
                Vector3.zero;

            movementRequested =
                false;

            return true;
        }

        direction.Normalize();

        avoidanceDirection =
            CalculateObstacleAvoidance(
                direction);

        Vector3 finalDirection =
            direction +
            avoidanceDirection *
            avoidanceStrength;

        if (finalDirection.sqrMagnitude <=
            MinimumDirectionSqrMagnitude)
        {
            finalDirection =
                direction;
        }

        finalDirection.Normalize();

        desiredVelocity =
            finalDirection *
            flightSpeed;

        movementRequested =
            true;

        UpdateFlightStateForDirection(
            desiredVelocity);

        return true;
    }

    protected override void StopAgent()
    {
        StopFlightMovement();

        if (Agent != null &&
            Agent.enabled &&
            Agent.isOnNavMesh)
        {
            Agent.isStopped =
                true;

            Agent.ResetPath();
        }
    }

    protected override void FaceTarget()
    {
        FaceFlightTarget();
    }

    #endregion

    #region Flight Movement

    private void UpdateFlightMovement()
    {
        if (!movementRequested)
        {
            currentVelocity =
                Vector3.MoveTowards(
                    currentVelocity,
                    Vector3.zero,
                    flightDeceleration *
                    Time.fixedDeltaTime);
        }
        else
        {
            currentVelocity =
                Vector3.MoveTowards(
                    currentVelocity,
                    desiredVelocity,
                    flightAcceleration *
                    Time.fixedDeltaTime);
        }

        currentVelocity =
            Vector3.ClampMagnitude(
                currentVelocity,
                maximumSafeFlightSpeed);

        if (!IsFiniteVector(
                currentVelocity))
        {
            TryRecoverFlight(
                forceRecovery: true);

            return;
        }

        Vector3 nextPosition =
            GetCurrentPosition() +
            currentVelocity *
            Time.fixedDeltaTime;

        nextPosition =
            ApplyAltitudeConstraints(
                nextPosition);

        if (enableHover &&
            !movementRequested)
        {
            nextPosition.y +=
                hoverOffset;
        }

        if (!IsFiniteVector(
                nextPosition))
        {
            TryRecoverFlight(
                forceRecovery: true);

            return;
        }

        ApplyFlightPosition(
            nextPosition);

        RotateTowardVelocity();

        if (!IsOutsideFlightArea(
                nextPosition))
        {
            lastValidFlightPosition =
                nextPosition;
        }

        movementRequested =
            false;
    }

    private Vector3 ApplyAltitudeConstraints(
        Vector3 position)
    {
        if (!maintainFlightAltitude)
            return position;

        if (!TryGetGroundHeight(
                position,
                out float groundHeight))
        {
            float minimumWorldHeight =
                HomePosition.y -
                maximumVerticalDistanceFromHome;

            float maximumWorldHeight =
                HomePosition.y +
                maximumVerticalDistanceFromHome;

            if (maximumVerticalDistanceFromHome >
                0f)
            {
                position.y =
                    Mathf.Clamp(
                        position.y,
                        minimumWorldHeight,
                        maximumWorldHeight);
            }

            return position;
        }

        float heightAboveGround =
            position.y -
            groundHeight;

        float targetHeight =
            Mathf.Clamp(
                heightAboveGround,
                minimumFlightHeight,
                maximumFlightHeight);

        float targetY =
            groundHeight +
            targetHeight;

        position.y =
            Mathf.MoveTowards(
                position.y,
                targetY,
                altitudeCorrectionSpeed *
                Time.fixedDeltaTime);

        return position;
    }

    private Vector3 ResolveFlightDestination(
        Vector3 destination,
        bool maintainPreferredAltitude)
    {
        if (!IsFiniteVector(
                destination))
        {
            return transform.position;
        }

        if (!maintainFlightAltitude)
            return destination;

        if (TryGetGroundHeight(
                destination,
                out float groundHeight))
        {
            float desiredHeight =
                maintainPreferredAltitude
                    ? preferredFlightHeight
                    : destination.y -
                      groundHeight;

            desiredHeight =
                Mathf.Clamp(
                    desiredHeight,
                    minimumFlightHeight,
                    maximumFlightHeight);

            destination.y =
                groundHeight +
                desiredHeight;
        }

        return destination;
    }

    private Vector3 GetTargetFlightPosition()
    {
        if (Target == null)
            return transform.position;

        Vector3 targetPosition =
            Target.position;

        if (maintainTargetAltitudeOffset)
        {
            targetPosition.y +=
                targetAltitudeOffset;
        }

        if (circleTarget &&
            circleRadius > 0f)
        {
            circleAngle +=
                circleSpeed *
                Time.deltaTime;

            Vector3 circleOffset =
                Quaternion.Euler(
                    0f,
                    circleAngle,
                    0f) *
                Vector3.forward *
                circleRadius;

            targetPosition +=
                circleOffset;
        }

        return ResolveFlightDestination(
            targetPosition,
            maintainPreferredAltitude: false);
    }

    private void RotateTowardVelocity()
    {
        Vector3 direction =
            currentVelocity;

        if (direction.sqrMagnitude <=
            MinimumDirectionSqrMagnitude)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);

        Quaternion rotation =
            Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed *
                Time.fixedDeltaTime);

        if (flightRigidbody != null &&
            useRigidbodyMovement &&
            !flightRigidbody.isKinematic)
        {
            flightRigidbody.MoveRotation(
                rotation);
        }
        else
        {
            transform.rotation =
                rotation;
        }
    }

    private void FaceFlightTarget()
    {
        if (Target == null)
            return;

        Vector3 direction =
            Target.position -
            transform.position;

        if (direction.sqrMagnitude <=
            MinimumDirectionSqrMagnitude)
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
                Time.deltaTime);
    }

    private bool HasReachedFlightPosition(
        Vector3 destination,
        float threshold)
    {
        if (!IsFiniteVector(
                destination))
        {
            return false;
        }

        float distance =
            Vector3.Distance(
                transform.position,
                destination);

        return
            float.IsFinite(
                distance) &&
            distance <=
                threshold;
    }

    private void StopFlightMovement()
    {
        movementRequested =
            false;

        desiredVelocity =
            Vector3.zero;

        currentVelocity =
            Vector3.MoveTowards(
                currentVelocity,
                Vector3.zero,
                flightDeceleration *
                Time.fixedDeltaTime);
    }

    #endregion

    #region Hover

    private void UpdateHover()
    {
        if (!enableHover ||
            movementRequested ||
            IsDead)
        {
            hoverOffset =
                Mathf.MoveTowards(
                    hoverOffset,
                    0f,
                    hoverSmoothing *
                    Time.deltaTime);

            return;
        }

        float targetOffset =
            Mathf.Sin(
                Time.time *
                hoverFrequency *
                Mathf.PI *
                2f) *
            hoverAmplitude;

        hoverOffset =
            Mathf.Lerp(
                hoverOffset,
                targetOffset,
                hoverSmoothing *
                Time.deltaTime);
    }

    #endregion

    #region Obstacle Avoidance

    private Vector3 CalculateObstacleAvoidance(
        Vector3 desiredDirection)
    {
        if (!avoidObstacles)
        {
            avoidanceTimer =
                0f;

            return Vector3.zero;
        }

        Vector3 origin =
            transform.position;

        int hitCount =
            Physics.SphereCastNonAlloc(
                origin,
                obstacleProbeRadius,
                desiredDirection,
                obstacleHits,
                obstacleProbeDistance,
                obstacleLayers,
                QueryTriggerInteraction.Ignore);

        Vector3 combinedNormal =
            Vector3.zero;

        int validHitCount =
            0;

        for (int index = 0;
             index < hitCount;
             index++)
        {
            RaycastHit hit =
                obstacleHits[index];

            if (hit.collider == null ||
                IsOwnCollider(
                    hit.collider))
            {
                continue;
            }

            combinedNormal +=
                hit.normal;

            validHitCount++;
        }

        if (validHitCount > 0)
        {
            combinedNormal /=
                validHitCount;

            avoidanceTimer =
                avoidanceDuration;

            ChangeFlightState(
                FlightMovementState.AvoidingObstacle);

            ObstacleDetected?.Invoke(
                this);

            return combinedNormal.normalized;
        }

        if (avoidanceTimer > 0f)
        {
            return avoidanceDirection;
        }

        return Vector3.zero;
    }

    #endregion

    #region Flight Area

    private void ValidateFlightArea()
    {
        if (!constrainToFlightArea ||
            !IsOutsideFlightArea(
                transform.position))
        {
            return;
        }

        ChangeFlightState(
            FlightMovementState.ReturningToFlightArea);

        FlightAreaExited?.Invoke(
            this);

        ReturnHome();
    }

    private bool IsOutsideFlightArea(
        Vector3 position)
    {
        if (!constrainToFlightArea ||
            !IsFiniteVector(
                position))
        {
            return false;
        }

        Vector3 offset =
            position -
            HomePosition;

        Vector2 horizontalOffset =
            new(
                offset.x,
                offset.z);

        bool outsideHorizontal =
            maximumHorizontalDistanceFromHome >
                0f &&
            horizontalOffset.sqrMagnitude >
                maximumHorizontalDistanceFromHome *
                maximumHorizontalDistanceFromHome;

        bool outsideVertical =
            maximumVerticalDistanceFromHome >
                0f &&
            Mathf.Abs(
                offset.y) >
                maximumVerticalDistanceFromHome;

        return
            outsideHorizontal ||
            outsideVertical;
    }

    #endregion

    #region Recovery

    private void UpdateFlightTimers()
    {
        if (avoidanceTimer > 0f)
        {
            avoidanceTimer =
                Mathf.Max(
                    0f,
                    avoidanceTimer -
                    Time.deltaTime);

            if (avoidanceTimer <= 0f &&
                flightMovementState ==
                    FlightMovementState.AvoidingObstacle)
            {
                ChangeFlightState(
                    FlightMovementState.Stable);
            }
        }

        if (recoveryTimer > 0f)
        {
            recoveryTimer =
                Mathf.Max(
                    0f,
                    recoveryTimer -
                    Time.deltaTime);
        }

        if (!knockbackRecoveryPending)
            return;

        knockbackRecoveryTimer =
            Mathf.Max(
                0f,
                knockbackRecoveryTimer -
                Time.deltaTime);

        if (knockbackRecoveryTimer > 0f)
            return;

        knockbackRecoveryPending =
            false;

        TryRecoverFlight(
            forceRecovery: true);
    }

    private bool TryRecoverFlight(
        bool forceRecovery)
    {
        if (!recoverAutomatically ||
            IsDead)
        {
            return false;
        }

        if (!forceRecovery &&
            recoveryTimer > 0f)
        {
            return false;
        }

        if (recoveryAttempts >=
            maximumRecoveryAttempts)
        {
            ApplyFlightPosition(
                HomePosition);

            StopFlightMovement();

            recoveryAttempts =
                0;

            lastValidFlightPosition =
                HomePosition;

            ChangeFlightState(
                FlightMovementState.Stable);

            return true;
        }

        recoveryTimer =
            recoveryInterval;

        recoveryAttempts++;

        ChangeFlightState(
            FlightMovementState.Recovering);

        Vector3 recoveryPosition =
            lastValidFlightPosition;

        if (!IsFiniteVector(
                recoveryPosition) ||
            IsOutsideFlightArea(
                recoveryPosition))
        {
            recoveryPosition =
                HomePosition;
        }

        recoveryPosition =
            ResolveFlightDestination(
                recoveryPosition,
                maintainPreferredAltitude: true);

        if (!IsFiniteVector(
                recoveryPosition))
        {
            return false;
        }

        ApplyFlightPosition(
            recoveryPosition);

        StopFlightMovement();

        recoveryAttempts =
            0;

        consecutiveStuckChecks =
            0;

        previousCheckedPosition =
            recoveryPosition;

        lastValidFlightPosition =
            recoveryPosition;

        ChangeFlightState(
            FlightMovementState.Stable);

        FlightRecovered?.Invoke(
            this,
            recoveryPosition);

        LogFlightState(
            "Flight movement recovered.");

        return true;
    }

    #endregion

    #region Stuck Detection

    private void UpdateStuckDetection()
    {
        if (!detectStuckMovement ||
            !IsMovementState())
        {
            previousCheckedPosition =
                transform.position;

            consecutiveStuckChecks =
                0;

            return;
        }

        stuckCheckTimer -=
            Time.deltaTime;

        if (stuckCheckTimer > 0f)
            return;

        stuckCheckTimer =
            stuckCheckInterval;

        Vector3 currentPosition =
            transform.position;

        if (!IsFiniteVector(
                currentPosition) ||
            !IsFiniteVector(
                previousCheckedPosition))
        {
            previousCheckedPosition =
                currentPosition;

            consecutiveStuckChecks =
                0;

            return;
        }

        float movedDistance =
            Vector3.Distance(
                currentPosition,
                previousCheckedPosition);

        previousCheckedPosition =
            currentPosition;

        if (!float.IsFinite(
                movedDistance) ||
            !movementRequested ||
            movedDistance >=
                minimumMovementDistance)
        {
            consecutiveStuckChecks =
                0;

            return;
        }

        consecutiveStuckChecks++;

        if (consecutiveStuckChecks <
            maximumStuckChecks)
        {
            return;
        }

        consecutiveStuckChecks =
            0;

        MovementStuck?.Invoke(
            this);

        LogFlightState(
            "Flying enemy appears to be stuck.");

        TryRecoverFlight(
            forceRecovery: true);
    }

    private bool IsMovementState()
    {
        return
            CurrentState ==
                AIState.Patrol ||
            CurrentState ==
                AIState.Chase ||
            CurrentState ==
                AIState.Returning;
    }

    #endregion

    #region Runtime Safety

    protected override bool RunRuntimeSafetyChecks()
    {
        if (!base.RunRuntimeSafetyChecks())
            return false;

        if (!ValidateFlightReferences())
        {
            ResolveFlightReferences();
            ConfigureFlightComponents();

            if (!ValidateFlightReferences())
            {
                EnterFlightSafetyShutdown(
                    "Flight Rigidbody could not be restored.");

                return false;
            }
        }

        if (!ValidateFlightTransform())
        {
            if (!TryRecoverFlight(
                    forceRecovery: true))
            {
                EnterFlightSafetyShutdown(
                    "Flight Transform contains invalid values.");

                return false;
            }
        }

        if (!ValidateFlightVelocity())
        {
            currentVelocity =
                Vector3.ClampMagnitude(
                    IsFiniteVector(
                        currentVelocity)
                        ? currentVelocity
                        : Vector3.zero,
                    maximumSafeFlightSpeed);

            desiredVelocity =
                Vector3.ClampMagnitude(
                    IsFiniteVector(
                        desiredVelocity)
                        ? desiredVelocity
                        : Vector3.zero,
                    maximumSafeFlightSpeed);
        }

        if (restoreFlightComponents)
        {
            RestoreFlightComponents();
        }

        return true;
    }

    private bool ValidateFlightReferences()
    {
        if (!useRigidbodyMovement)
            return true;

        return
            flightRigidbody != null &&
            flightRigidbody.gameObject.activeInHierarchy;
    }

    private bool ValidateFlightTransform()
    {
        Vector3 scale =
            transform.lossyScale;

        return
            IsFiniteVector(
                transform.position) &&
            IsFiniteQuaternion(
                transform.rotation) &&
            IsFiniteVector(
                scale) &&
            Mathf.Abs(
                scale.x) >=
                minimumValidScale &&
            Mathf.Abs(
                scale.y) >=
                minimumValidScale &&
            Mathf.Abs(
                scale.z) >=
                minimumValidScale;
    }

    private bool ValidateFlightVelocity()
    {
        return
            IsFiniteVector(
                currentVelocity) &&
            IsFiniteVector(
                desiredVelocity) &&
            currentVelocity.sqrMagnitude <=
                maximumSafeFlightSpeed *
                maximumSafeFlightSpeed &&
            desiredVelocity.sqrMagnitude <=
                maximumSafeFlightSpeed *
                maximumSafeFlightSpeed;
    }

    private void RestoreFlightComponents()
    {
        if (flightRigidbody != null)
        {
            flightRigidbody.useGravity =
                !disableGravity;

            flightRigidbody.isKinematic =
                !useRigidbodyMovement;

            flightRigidbody.WakeUp();
        }

        if (Agent != null)
        {
            Agent.updatePosition =
                false;

            Agent.updateRotation =
                false;
        }
    }

    private void EnterFlightSafetyShutdown(
        string reason)
    {
        initializedFlight =
            false;

        StopFlightMovement();

        ChangeFlightState(
            FlightMovementState.Disabled);

        Debug.LogError(
            $"{nameof(FlyingEnemyAI)} entered safety shutdown on '{name}': {reason}",
            this);

        enabled =
            false;
    }

    #endregion

    #region Flight State

    private void UpdateFlightStateForDirection(
        Vector3 velocity)
    {
        if (flightMovementState ==
            FlightMovementState.AvoidingObstacle)
        {
            return;
        }

        if (velocity.y >
            0.1f)
        {
            ChangeFlightState(
                FlightMovementState.Ascending);
        }
        else if (velocity.y <
                 -0.1f)
        {
            ChangeFlightState(
                FlightMovementState.Descending);
        }
        else
        {
            ChangeFlightState(
                FlightMovementState.Stable);
        }
    }

    private void ChangeFlightState(
        FlightMovementState newState)
    {
        if (!Enum.IsDefined(
                typeof(FlightMovementState),
                newState))
        {
            return;
        }

        if (flightMovementState ==
            newState)
        {
            return;
        }

        flightMovementState =
            newState;

        FlightStateChanged?.Invoke(
            this,
            flightMovementState);

        LogFlightState(
            $"Flight state changed to {flightMovementState}.");
    }

    #endregion

    #region Helpers

    private Vector3 GetCurrentPosition()
    {
        if (flightRigidbody != null &&
            useRigidbodyMovement &&
            !flightRigidbody.isKinematic)
        {
            return flightRigidbody.position;
        }

        return transform.position;
    }

    private void ApplyFlightPosition(
        Vector3 position)
    {
        if (flightRigidbody != null &&
            useRigidbodyMovement &&
            !flightRigidbody.isKinematic)
        {
            flightRigidbody.MovePosition(
                position);
        }
        else
        {
            transform.position =
                position;
        }
    }

    private bool TryGetGroundHeight(
        Vector3 position,
        out float groundHeight)
    {
        groundHeight =
            0f;

        Vector3 origin =
            position +
            Vector3.up *
            groundProbeDistance *
            0.5f;

        int hitCount =
            Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                obstacleHits,
                groundProbeDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

        float closestDistance =
            float.PositiveInfinity;

        bool foundGround =
            false;

        for (int index = 0;
             index < hitCount;
             index++)
        {
            RaycastHit hit =
                obstacleHits[index];

            if (hit.collider == null ||
                IsOwnCollider(
                    hit.collider) ||
                hit.distance >=
                    closestDistance)
            {
                continue;
            }

            closestDistance =
                hit.distance;

            groundHeight =
                hit.point.y;

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

        return
            candidateTransform ==
                transform ||
            candidateTransform.IsChildOf(
                transform);
    }

    private Vector3 GetFlightForward()
    {
        Vector3 forward =
            transform.forward;

        if (forward.sqrMagnitude <=
            MinimumDirectionSqrMagnitude)
        {
            return Vector3.forward;
        }

        return forward.normalized;
    }

    private void LogFlightState(
        string message)
    {
        if (!logFlightSafety)
            return;

        Debug.Log(
            $"{nameof(FlyingEnemyAI)} on '{name}': {message}",
            this);
    }

    #endregion
}
