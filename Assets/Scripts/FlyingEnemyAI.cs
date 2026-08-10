using UnityEngine;

public class FlyingEnemyAI : AIController
{
    #region Flight State

    public enum FlightState
    {
        Hovering,
        Patrolling,
        Chasing,
        Attacking,
        Ascending,
        Descending,
        Avoiding,
        Returning,
        Stunned,
        Recovering,
        Dead
    }

    #endregion

    #region Flight Movement

    [Header("Flight Movement")]

    [SerializeField, Min(0.1f)]
    private float flightSpeed = 8f;

    [SerializeField, Min(0f)]
    private float flightAcceleration = 20f;

    [SerializeField, Min(0f)]
    private float flightDeceleration = 25f;

    [SerializeField, Min(0f)]
    private float rotationSpeed = 360f;

    [SerializeField, Min(0.01f)]
    private float arrivalDistance = 0.75f;

    #endregion

    #region Hover

    [Header("Hover")]

    [SerializeField]
    private bool enableHover = true;

    [SerializeField, Min(0f)]
    private float hoverAmplitude = 0.25f;

    [SerializeField, Min(0f)]
    private float hoverFrequency = 1.5f;

    [SerializeField, Min(0f)]
    private float hoverSmoothing = 8f;

    #endregion

    #region Altitude

    [Header("Altitude")]

    [SerializeField]
    private bool maintainAltitude = true;

    [SerializeField, Min(0.1f)]
    private float preferredHeight = 4f;

    [SerializeField, Min(0.1f)]
    private float minimumHeight = 1.5f;

    [SerializeField, Min(0.1f)]
    private float maximumHeight = 12f;

    [SerializeField, Min(0f)]
    private float altitudeCorrectionSpeed = 6f;

    [SerializeField, Min(0.1f)]
    private float groundCheckDistance = 30f;

    [SerializeField]
    private LayerMask groundLayers = ~0;

    #endregion

    #region Target Flight

    [Header("Target Flight")]

    [SerializeField]
    private float targetHeightOffset = 2f;

    [SerializeField, Min(0f)]
    private float targetStoppingDistance = 4f;

    [SerializeField]
    private bool circleTarget;

    [SerializeField, Min(0f)]
    private float circleRadius = 5f;

    [SerializeField, Min(0f)]
    private float circleSpeed = 45f;

    #endregion

    #region Obstacle Avoidance

    [Header("Obstacle Avoidance")]

    [SerializeField]
    private bool avoidObstacles = true;

    [SerializeField, Min(0.05f)]
    private float obstacleRadius = 0.5f;

    [SerializeField, Min(0.1f)]
    private float obstacleDistance = 3f;

    [SerializeField, Min(0f)]
    private float avoidanceStrength = 2f;

    [SerializeField]
    private LayerMask obstacleLayers = ~0;

    #endregion

    #region Flight Area

    [Header("Flight Area")]

    [SerializeField]
    private bool stayNearHome = true;

    [SerializeField, Min(0f)]
    private float maximumHorizontalDistance = 25f;

    [SerializeField, Min(0f)]
    private float maximumVerticalDistance = 15f;

    [SerializeField, Min(0f)]
    private float returnSpeedMultiplier = 1.5f;

    #endregion

    #region Recovery

    [Header("Recovery")]

    [SerializeField]
    private bool recoverWhenStuck = true;

    [SerializeField, Min(0.1f)]
    private float stuckCheckInterval = 1f;

    [SerializeField, Min(0f)]
    private float minimumMovementDistance = 0.05f;

    [SerializeField, Min(1)]
    private int stuckChecksBeforeRecovery = 3;

    [SerializeField, Min(0f)]
    private float recoveryHeight = 2f;

    #endregion

    #region Physics

    [Header("Physics")]

    [SerializeField]
    private Rigidbody flightRigidbody;

    [SerializeField]
    private bool useRigidbodyMovement = true;

    [SerializeField]
    private bool disableGravity = true;

    [SerializeField]
    private bool freezePhysicsRotation = true;

    [SerializeField, Min(1f)]
    private float maximumSafeSpeed = 100f;

    #endregion

    #region Debug

    [Header("Debug")]

    [SerializeField]
    private FlightState flightState =
        FlightState.Hovering;

    [SerializeField]
    private bool drawFlightDebug = true;

    #endregion

    #region Runtime

    private Vector3 desiredDestination;
    private Vector3 currentVelocity;
    private Vector3 lastValidPosition;
    private Vector3 previousStuckPosition;

    private float hoverOffset;
    private float circleAngle;
    private float stuckTimer;

    private int stuckChecks;

    private bool hasFlightDestination;
    private bool flightInitialized;

    #endregion

    #region Properties

    public FlightState CurrentFlightState =>
        flightState;

    public Vector3 FlightVelocity =>
        currentVelocity;

    public bool IsFlightInitialized =>
        flightInitialized;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        ResolveFlightReferences();
        ConfigureFlight();
        InitializeFlight();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        ResolveFlightReferences();
        ConfigureFlight();

        if (!flightInitialized)
        {
            InitializeFlight();
        }
    }

    protected override void Update()
    {
        base.Update();

        if (!IsInitialized ||
            !flightInitialized ||
            IsDead ||
            CurrentState == AIState.Disabled)
        {
            return;
        }

        UpdateHover();
        UpdateStuckDetection();

        if (stayNearHome &&
            IsOutsideFlightArea(
                transform.position))
        {
            ReturnHome();
        }
    }

    private void FixedUpdate()
    {
        if (!IsInitialized ||
            !flightInitialized ||
            IsDead ||
            CurrentState == AIState.Disabled)
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
        flightInitialized =
            false;

        flightRigidbody =
            null;

        base.OnDestroy();
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        flightSpeed =
            Mathf.Max(
                0.1f,
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
                0.01f,
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

        minimumHeight =
            Mathf.Max(
                0.1f,
                minimumHeight);

        maximumHeight =
            Mathf.Max(
                minimumHeight,
                maximumHeight);

        preferredHeight =
            Mathf.Clamp(
                preferredHeight,
                minimumHeight,
                maximumHeight);

        altitudeCorrectionSpeed =
            Mathf.Max(
                0f,
                altitudeCorrectionSpeed);

        groundCheckDistance =
            Mathf.Max(
                0.1f,
                groundCheckDistance);

        targetStoppingDistance =
            Mathf.Max(
                0f,
                targetStoppingDistance);

        circleRadius =
            Mathf.Max(
                0f,
                circleRadius);

        circleSpeed =
            Mathf.Max(
                0f,
                circleSpeed);

        obstacleRadius =
            Mathf.Max(
                0.05f,
                obstacleRadius);

        obstacleDistance =
            Mathf.Max(
                0.1f,
                obstacleDistance);

        avoidanceStrength =
            Mathf.Max(
                0f,
                avoidanceStrength);

        maximumHorizontalDistance =
            Mathf.Max(
                0f,
                maximumHorizontalDistance);

        maximumVerticalDistance =
            Mathf.Max(
                0f,
                maximumVerticalDistance);

        returnSpeedMultiplier =
            Mathf.Max(
                0f,
                returnSpeedMultiplier);

        stuckCheckInterval =
            Mathf.Max(
                0.1f,
                stuckCheckInterval);

        minimumMovementDistance =
            Mathf.Max(
                0f,
                minimumMovementDistance);

        stuckChecksBeforeRecovery =
            Mathf.Max(
                1,
                stuckChecksBeforeRecovery);

        recoveryHeight =
            Mathf.Max(
                0f,
                recoveryHeight);

        maximumSafeSpeed =
            Mathf.Max(
                1f,
                maximumSafeSpeed);
    }

    #endregion

    #region Initialization

    protected override bool Initialize()
    {
        bool initialized =
            base.Initialize();

        if (!initialized)
        {
            return false;
        }

        ResolveFlightReferences();
        ConfigureFlight();
        InitializeFlight();

        return
            flightInitialized;
    }

    private void ResolveFlightReferences()
    {
        flightRigidbody ??=
            GetComponent<Rigidbody>();

        flightRigidbody ??=
            GetComponentInParent<Rigidbody>();

        flightRigidbody ??=
            GetComponentInChildren<Rigidbody>(
                includeInactive: true);
    }

    private void ConfigureFlight()
    {
        if (Agent != null)
        {
            Agent.updatePosition =
                false;

            Agent.updateRotation =
                false;

            if (Agent.enabled &&
                Agent.isOnNavMesh)
            {
                Agent.isStopped =
                    true;

                Agent.ResetPath();
            }
        }

        if (flightRigidbody == null)
        {
            return;
        }

        if (disableGravity)
        {
            flightRigidbody.useGravity =
                false;
        }

        if (freezePhysicsRotation)
        {
            flightRigidbody.constraints |=
                RigidbodyConstraints.FreezeRotation;
        }
    }

    private void InitializeFlight()
    {
        if (!IsFiniteVector(
                transform.position))
        {
            flightInitialized =
                false;

            return;
        }

        currentVelocity =
            Vector3.zero;

        desiredDestination =
            transform.position;

        lastValidPosition =
            transform.position;

        previousStuckPosition =
            transform.position;

        hoverOffset =
            0f;

        circleAngle =
            0f;

        stuckTimer =
            stuckCheckInterval;

        stuckChecks =
            0;

        hasFlightDestination =
            false;

        flightState =
            FlightState.Hovering;

        flightInitialized =
            true;
    }

    #endregion

    #region Grounding

    protected override void UpdateGrounding()
    {
        // Flying enemies deliberately ignore
        // ground-based AI grounding.
    }

    protected override void UpdateInAirState()
    {
        SetState(
            Target != null
                ? AIState.Chase
                : AIState.Idle);
    }

    #endregion

    #region Idle

    protected override void UpdateIdleState()
    {
        StopFlightMovement();

        flightState =
            FlightState.Hovering;

        if (TryAcquireTarget())
        {
            SetState(
                AIState.Chase);

            return;
        }

        if (HasPatrolPoint())
        {
            SetState(
                AIState.Patrol);
        }
    }

    #endregion

    #region Patrol

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

        flightState =
            FlightState.Patrolling;

        Vector3 destination =
            ResolveFlightDestination(
                patrolPoint.position,
                false);

        MoveAgentTo(
            destination);

        if (!HasReachedFlightPosition(
                destination,
                arrivalDistance))
        {
            return;
        }

        StopFlightMovement();
        AdvancePatrolPoint();
    }

    #endregion

    #region Chase

    protected override void UpdateChaseState()
    {
        if (Target == null ||
            !Target.gameObject.activeInHierarchy ||
            !IsFiniteVector(
                Target.position))
        {
            ReturnHome();

            return;
        }

        if (stayNearHome &&
            IsOutsideFlightArea(
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
            RecoverFlight();

            return;
        }

        flightState =
            FlightState.Chasing;

        if (distance <=
            targetStoppingDistance)
        {
            StopFlightMovement();
            FaceTarget();

            return;
        }

        MoveAgentTo(
            destination);
    }

    #endregion

    #region Attack

    protected override void UpdateAttackState()
    {
        flightState =
            FlightState.Attacking;

        StopFlightMovement();

        if (Target != null)
        {
            FaceTarget();
        }

        base.UpdateAttackState();
    }

    #endregion

    #region Returning

    protected override void UpdateReturningState()
    {
        flightState =
            FlightState.Returning;

        Vector3 destination =
            ResolveFlightDestination(
                HomePosition,
                true);

        float originalSpeed =
            flightSpeed;

        if (returnSpeedMultiplier > 0f)
        {
            flightSpeed =
                originalSpeed *
                returnSpeedMultiplier;
        }

        MoveAgentTo(
            destination);

        flightSpeed =
            originalSpeed;

        if (!HasReachedFlightPosition(
                destination,
                arrivalDistance))
        {
            return;
        }

        StopFlightMovement();

        stuckChecks =
            0;

        SetState(
            HasPatrolPoint()
                ? AIState.Patrol
                : AIState.Idle);
    }

    #endregion

    #region Flight Destination

    protected override bool MoveAgentTo(
        Vector3 destination)
    {
        if (!flightInitialized ||
            IsDead ||
            !IsFiniteVector(
                destination))
        {
            return false;
        }

        desiredDestination =
            ResolveFlightDestination(
                destination,
                true);

        hasFlightDestination =
            true;

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

    private void StopFlightMovement()
    {
        hasFlightDestination =
            false;
    }

    #endregion

    #region Flight Movement

    private void UpdateFlightMovement()
    {
        Vector3 desiredVelocity =
            Vector3.zero;

        if (hasFlightDestination)
        {
            Vector3 direction =
                desiredDestination -
                GetCurrentPosition();

            if (direction.sqrMagnitude >
                0.0001f)
            {
                direction.Normalize();

                direction =
                    ApplyObstacleAvoidance(
                        direction);

                desiredVelocity =
                    direction *
                    flightSpeed;
            }
        }

        float accelerationRate =
            hasFlightDestination
                ? flightAcceleration
                : flightDeceleration;

        currentVelocity =
            Vector3.MoveTowards(
                currentVelocity,
                desiredVelocity,
                accelerationRate *
                Time.fixedDeltaTime);

        currentVelocity =
            Vector3.ClampMagnitude(
                currentVelocity,
                maximumSafeSpeed);

        if (!IsFiniteVector(
                currentVelocity))
        {
            RecoverFlight();

            return;
        }

        Vector3 nextPosition =
            GetCurrentPosition() +
            currentVelocity *
                Time.fixedDeltaTime;

        nextPosition =
            ApplyAltitude(
                nextPosition);

        if (!hasFlightDestination &&
            enableHover)
        {
            nextPosition.y +=
                hoverOffset;
        }

        if (!IsFiniteVector(
                nextPosition))
        {
            RecoverFlight();

            return;
        }

        ApplyPosition(
            nextPosition);

        RotateTowardVelocity();

        if (!IsOutsideFlightArea(
                nextPosition))
        {
            lastValidPosition =
                nextPosition;
        }

        UpdateDirectionalFlightState();
    }

    #endregion

    #region Hover

    private void UpdateHover()
    {
        if (!enableHover ||
            hasFlightDestination ||
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
                Mathf.Clamp01(
                    hoverSmoothing *
                        Time.deltaTime));
    }

    #endregion

    #region Target Position

    private Vector3 GetTargetFlightPosition()
    {
        if (Target == null)
        {
            return
                transform.position;
        }

        Vector3 targetPosition =
            Target.position;

        targetPosition.y +=
            targetHeightOffset;

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

        return
            ResolveFlightDestination(
                targetPosition,
                false);
    }

    #endregion

    #region Altitude

    private Vector3 ResolveFlightDestination(
        Vector3 destination,
        bool usePreferredHeight)
    {
        if (!IsFiniteVector(
                destination))
        {
            return
                transform.position;
        }

        if (!maintainAltitude)
        {
            return destination;
        }

        if (!TryGetGroundHeight(
                destination,
                out float groundHeight))
        {
            return ClampToHomeHeight(
                destination);
        }

        float desiredHeight =
            usePreferredHeight
                ? preferredHeight
                : destination.y -
                    groundHeight;

        desiredHeight =
            Mathf.Clamp(
                desiredHeight,
                minimumHeight,
                maximumHeight);

        destination.y =
            groundHeight +
            desiredHeight;

        return destination;
    }

    private Vector3 ApplyAltitude(
        Vector3 position)
    {
        if (!maintainAltitude)
        {
            return
                ClampToHomeHeight(
                    position);
        }

        if (!TryGetGroundHeight(
                position,
                out float groundHeight))
        {
            return
                ClampToHomeHeight(
                    position);
        }

        float height =
            position.y -
            groundHeight;

        float clampedHeight =
            Mathf.Clamp(
                height,
                minimumHeight,
                maximumHeight);

        float desiredY =
            groundHeight +
            clampedHeight;

        position.y =
            Mathf.MoveTowards(
                position.y,
                desiredY,
                altitudeCorrectionSpeed *
                    Time.fixedDeltaTime);

        return
            ClampToHomeHeight(
                position);
    }

    private Vector3 ClampToHomeHeight(
        Vector3 position)
    {
        if (maximumVerticalDistance <=
            0f)
        {
            return position;
        }

        position.y =
            Mathf.Clamp(
                position.y,
                HomePosition.y -
                    maximumVerticalDistance,
                HomePosition.y +
                    maximumVerticalDistance);

        return position;
    }

    private bool TryGetGroundHeight(
        Vector3 position,
        out float height)
    {
        height =
            0f;

        Vector3 origin =
            position +
            Vector3.up *
                groundCheckDistance *
                0.5f;

        if (!Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                groundCheckDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (IsOwnCollider(
                hit.collider))
        {
            return false;
        }

        height =
            hit.point.y;

        return
            float.IsFinite(
                height);
    }

    #endregion

    #region Obstacle Avoidance

    private Vector3 ApplyObstacleAvoidance(
        Vector3 desiredDirection)
    {
        if (!avoidObstacles ||
            desiredDirection.sqrMagnitude <=
                0.0001f)
        {
            return desiredDirection;
        }

        if (!Physics.SphereCast(
                GetCurrentPosition(),
                obstacleRadius,
                desiredDirection,
                out RaycastHit hit,
                obstacleDistance,
                obstacleLayers,
                QueryTriggerInteraction.Ignore))
        {
            return desiredDirection;
        }

        if (IsOwnCollider(
                hit.collider))
        {
            return desiredDirection;
        }

        flightState =
            FlightState.Avoiding;

        Vector3 avoidance =
            Vector3.ProjectOnPlane(
                desiredDirection,
                hit.normal);

        if (avoidance.sqrMagnitude <=
            0.0001f)
        {
            avoidance =
                hit.normal +
                Vector3.up * 0.5f;
        }

        Vector3 result =
            desiredDirection +
            avoidance.normalized *
                avoidanceStrength;

        return
            result.sqrMagnitude >
                0.0001f
                ? result.normalized
                : desiredDirection;
    }

    #endregion

    #region Flight Area

    private bool IsOutsideFlightArea(
        Vector3 position)
    {
        if (!stayNearHome ||
            !IsFiniteVector(
                position))
        {
            return false;
        }

        Vector3 offset =
            position -
            HomePosition;

        Vector2 horizontal =
            new(
                offset.x,
                offset.z);

        bool outsideHorizontal =
            maximumHorizontalDistance >
                0f &&
            horizontal.sqrMagnitude >
                maximumHorizontalDistance *
                maximumHorizontalDistance;

        bool outsideVertical =
            maximumVerticalDistance >
                0f &&
            Mathf.Abs(
                offset.y) >
                maximumVerticalDistance;

        return
            outsideHorizontal ||
            outsideVertical;
    }

    #endregion

    #region Recovery

    public bool RecoverFlight()
    {
        if (!recoverWhenStuck ||
            IsDead)
        {
            return false;
        }

        flightState =
            FlightState.Recovering;

        Vector3 recoveryPosition =
            lastValidPosition;

        if (!IsFiniteVector(
                recoveryPosition) ||
            IsOutsideFlightArea(
                recoveryPosition))
        {
            recoveryPosition =
                HomePosition +
                Vector3.up *
                    recoveryHeight;
        }

        recoveryPosition =
            ResolveFlightDestination(
                recoveryPosition,
                true);

        if (!IsFiniteVector(
                recoveryPosition))
        {
            return false;
        }

        currentVelocity =
            Vector3.zero;

        hasFlightDestination =
            false;

        ApplyPosition(
            recoveryPosition);

        lastValidPosition =
            recoveryPosition;

        previousStuckPosition =
            recoveryPosition;

        stuckChecks =
            0;

        flightState =
            FlightState.Hovering;

        return true;
    }

    public void NotifyKnockbackEnded()
    {
        RecoverFlight();
    }

    private void UpdateStuckDetection()
    {
        if (!recoverWhenStuck ||
            !hasFlightDestination)
        {
            stuckChecks =
                0;

            previousStuckPosition =
                transform.position;

            return;
        }

        stuckTimer -=
            Time.deltaTime;

        if (stuckTimer > 0f)
        {
            return;
        }

        stuckTimer =
            stuckCheckInterval;

        Vector3 currentPosition =
            transform.position;

        if (!IsFiniteVector(
                currentPosition) ||
            !IsFiniteVector(
                previousStuckPosition))
        {
            previousStuckPosition =
                currentPosition;

            stuckChecks =
                0;

            return;
        }

        float distance =
            Vector3.Distance(
                currentPosition,
                previousStuckPosition);

        previousStuckPosition =
            currentPosition;

        if (!float.IsFinite(
                distance) ||
            distance >=
                minimumMovementDistance)
        {
            stuckChecks =
                0;

            return;
        }

        stuckChecks++;

        if (stuckChecks <
            stuckChecksBeforeRecovery)
        {
            return;
        }

        stuckChecks =
            0;

        RecoverFlight();
    }

    #endregion

    #region Position

    private Vector3 GetCurrentPosition()
    {
        if (flightRigidbody != null &&
            useRigidbodyMovement &&
            !flightRigidbody.isKinematic)
        {
            return
                flightRigidbody.position;
        }

        return
            transform.position;
    }

    private void ApplyPosition(
        Vector3 position)
    {
        if (!IsFiniteVector(
                position))
        {
            return;
        }

        if (flightRigidbody != null &&
            useRigidbodyMovement &&
            !flightRigidbody.isKinematic)
        {
            flightRigidbody.MovePosition(
                position);

            return;
        }

        transform.position =
            position;
    }

    #endregion

    #region Rotation

    protected override void FaceTarget()
    {
        if (Target == null ||
            !IsFiniteVector(
                Target.position))
        {
            return;
        }

        Vector3 direction =
            Target.position -
            transform.position;

        RotateTowardsDirection(
            direction,
            Time.deltaTime);
    }

    private void RotateTowardVelocity()
    {
        if (currentVelocity.sqrMagnitude <=
            0.0001f)
        {
            return;
        }

        RotateTowardsDirection(
            currentVelocity,
            Time.fixedDeltaTime);
    }

    private void RotateTowardsDirection(
        Vector3 direction,
        float deltaTime)
    {
        if (!IsFiniteVector(
                direction) ||
            direction.sqrMagnitude <=
                0.0001f)
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
                    deltaTime);

        if (flightRigidbody != null &&
            useRigidbodyMovement &&
            !flightRigidbody.isKinematic)
        {
            flightRigidbody.MoveRotation(
                rotation);

            return;
        }

        transform.rotation =
            rotation;
    }

    #endregion

    #region Flight State

    private void UpdateDirectionalFlightState()
    {
        if (flightState ==
            FlightState.Avoiding)
        {
            return;
        }

        if (!hasFlightDestination)
        {
            flightState =
                FlightState.Hovering;

            return;
        }

        if (currentVelocity.y >
            0.1f)
        {
            flightState =
                FlightState.Ascending;

            return;
        }

        if (currentVelocity.y <
            -0.1f)
        {
            flightState =
                FlightState.Descending;

            return;
        }

        switch (CurrentState)
        {
            case AIState.Patrol:
                flightState =
                    FlightState.Patrolling;
                break;

            case AIState.Chase:
                flightState =
                    FlightState.Chasing;
                break;

            case AIState.Attack:
                flightState =
                    FlightState.Attacking;
                break;

            case AIState.Returning:
                flightState =
                    FlightState.Returning;
                break;

            case AIState.Stunned:
                flightState =
                    FlightState.Stunned;
                break;

            case AIState.Dead:
                flightState =
                    FlightState.Dead;
                break;

            default:
                flightState =
                    FlightState.Hovering;
                break;
        }
    }

    #endregion

    #region Helpers

    private bool HasReachedFlightPosition(
        Vector3 destination,
        float distance)
    {
        if (!IsFiniteVector(
                destination))
        {
            return false;
        }

        float squaredDistance =
            (
                transform.position -
                destination
            ).sqrMagnitude;

        return
            float.IsFinite(
                squaredDistance) &&
            squaredDistance <=
                distance *
                distance;
    }

    private bool IsOwnCollider(
        Collider candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        Transform candidateTransform =
            candidate.transform;

        return
            candidateTransform ==
                transform ||
            candidateTransform.IsChildOf(
                transform);
    }

    #endregion

    #region Gizmos

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        if (!drawFlightDebug)
        {
            return;
        }

        Vector3 position =
            transform.position;

        Gizmos.DrawWireSphere(
            position,
            obstacleRadius);

        Gizmos.DrawLine(
            position,
            position +
                transform.forward *
                    obstacleDistance);

        Gizmos.DrawLine(
            position,
            position +
                Vector3.down *
                    groundCheckDistance);

        if (stayNearHome &&
            maximumHorizontalDistance > 0f)
        {
            Vector3 center =
                Application.isPlaying
                    ? HomePosition
                    : position;

            Gizmos.DrawWireSphere(
                center,
                maximumHorizontalDistance);
        }

        if (Application.isPlaying &&
            hasFlightDestination)
        {
            Gizmos.DrawLine(
                position,
                desiredDestination);

            Gizmos.DrawWireSphere(
                desiredDestination,
                arrivalDistance);
        }
    }

    #endregion
}