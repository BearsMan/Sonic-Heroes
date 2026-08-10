using UnityEngine;
using UnityEngine.AI;

public class GroundEnemyAI : AIController
{
    #region Ground State

    public enum GroundMovementState
    {
        Stable,
        ApproachingLedge,
        Blocked,
        OffNavMesh,
        Recovering,
        Stuck
    }

    #endregion

    #region Ground Navigation

    [Header("Ground Navigation")]

    [SerializeField]
    private bool preventLedgeFalls = true;

    [SerializeField]
    private bool detectWalls = true;

    [SerializeField, Min(0.01f)]
    private float forwardGroundCheckDistance = 0.75f;

    [SerializeField, Min(0.01f)]
    private float groundCheckDepth = 1.5f;

    [SerializeField, Min(0.01f)]
    private float wallCheckRadius = 0.25f;

    [SerializeField, Min(0.01f)]
    private float wallCheckDistance = 0.6f;

    [SerializeField]
    private LayerMask groundLayers = ~0;

    [SerializeField]
    private LayerMask obstacleLayers = ~0;

    #endregion

    #region Slopes

    [Header("Slopes")]

    [SerializeField]
    private bool validateSlopes = true;

    [SerializeField, Range(0f, 89f)]
    private float maximumSlopeAngle = 55f;

    #endregion

    #region NavMesh

    [Header("NavMesh")]

    [SerializeField, Min(0.1f)]
    private float navMeshSampleRadius = 3f;

    [SerializeField]
    private bool recoverToNavMesh = true;

    [SerializeField, Min(0.1f)]
    private float recoveryInterval = 0.5f;

    [SerializeField, Min(1)]
    private int maximumRecoveryAttempts = 5;

    [SerializeField, Min(0f)]
    private float recoveryHeightOffset = 0.1f;

    #endregion

    #region Stuck Detection

    [Header("Stuck Detection")]

    [SerializeField]
    private bool detectStuckMovement = true;

    [SerializeField, Min(0.1f)]
    private float stuckCheckInterval = 1f;

    [SerializeField, Min(0f)]
    private float minimumMovementDistance = 0.05f;

    [SerializeField, Min(1)]
    private int stuckChecksBeforeRecovery = 3;

    #endregion

    #region Knockback Recovery

    [Header("Knockback Recovery")]

    [SerializeField]
    private bool recoverAfterKnockback = true;

    [SerializeField, Min(0f)]
    private float knockbackRecoveryDelay = 0.2f;

    #endregion

    #region Debug

    [Header("Debug")]

    [SerializeField]
    private GroundMovementState groundMovementState =
        GroundMovementState.Stable;

    [SerializeField]
    private bool drawGroundChecks = true;

    #endregion

    #region Runtime State

    private Vector3 lastValidGroundPosition;
    private Vector3 previousStuckPosition;

    private float recoveryTimer;
    private float stuckTimer;
    private float knockbackTimer;

    private int recoveryAttempts;
    private int stuckChecks;

    private bool knockbackRecoveryPending;

    #endregion

    #region Properties

    public GroundMovementState CurrentGroundMovementState =>
        groundMovementState;

    public bool IsOnNavMesh =>
        Agent != null &&
        Agent.enabled &&
        Agent.isOnNavMesh;

    public Vector3 LastValidGroundPosition =>
        lastValidGroundPosition;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        InitializeGroundRuntime();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        InitializeGroundRuntime();
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

        UpdateRecoveryTimer();
        UpdateKnockbackRecovery();
        UpdateStuckDetection();
        UpdateValidGroundPosition();
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        forwardGroundCheckDistance =
            Mathf.Max(
                0.01f,
                forwardGroundCheckDistance);

        groundCheckDepth =
            Mathf.Max(
                0.01f,
                groundCheckDepth);

        wallCheckRadius =
            Mathf.Max(
                0.01f,
                wallCheckRadius);

        wallCheckDistance =
            Mathf.Max(
                0.01f,
                wallCheckDistance);

        navMeshSampleRadius =
            Mathf.Max(
                0.1f,
                navMeshSampleRadius);

        recoveryInterval =
            Mathf.Max(
                0.1f,
                recoveryInterval);

        maximumRecoveryAttempts =
            Mathf.Max(
                1,
                maximumRecoveryAttempts);

        recoveryHeightOffset =
            Mathf.Max(
                0f,
                recoveryHeightOffset);

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

        knockbackRecoveryDelay =
            Mathf.Max(
                0f,
                knockbackRecoveryDelay);
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

        InitializeGroundRuntime();

        return true;
    }

    private void InitializeGroundRuntime()
    {
        if (IsFiniteVector(
                transform.position))
        {
            lastValidGroundPosition =
                transform.position;

            previousStuckPosition =
                transform.position;
        }

        recoveryTimer =
            0f;

        stuckTimer =
            stuckCheckInterval;

        knockbackTimer =
            0f;

        recoveryAttempts =
            0;

        stuckChecks =
            0;

        knockbackRecoveryPending =
            false;

        groundMovementState =
            GroundMovementState.Stable;
    }

    #endregion

    #region Ground State Overrides

    protected override void UpdatePatrolState()
    {
        if (!CanContinueGroundMovement())
        {
            return;
        }

        base.UpdatePatrolState();
    }

    protected override void UpdateChaseState()
    {
        if (!CanContinueGroundMovement())
        {
            return;
        }

        base.UpdateChaseState();
    }

    protected override void UpdateReturningState()
    {
        if (!CanContinueGroundMovement())
        {
            return;
        }

        base.UpdateReturningState();
    }

    protected override bool MoveAgentTo(
        Vector3 destination)
    {
        if (!CanContinueGroundMovement())
        {
            return false;
        }

        if (!IsFiniteVector(
                destination))
        {
            return false;
        }

        if (!TryResolveDestination(
                destination,
                out Vector3 resolvedDestination))
        {
            groundMovementState =
                GroundMovementState.OffNavMesh;

            return false;
        }

        if (!CanMoveToward(
                resolvedDestination))
        {
            StopAgent();

            return false;
        }

        bool moved =
            base.MoveAgentTo(
                resolvedDestination);

        if (moved)
        {
            groundMovementState =
                GroundMovementState.Stable;
        }

        return moved;
    }

    #endregion

    #region Ground Movement Validation

    private bool CanContinueGroundMovement()
    {
        if (IsOnNavMesh)
        {
            recoveryAttempts =
                0;

            if (IsFiniteVector(
                    transform.position))
            {
                lastValidGroundPosition =
                    transform.position;
            }

            if (groundMovementState ==
                    GroundMovementState.OffNavMesh ||
                groundMovementState ==
                    GroundMovementState.Recovering ||
                groundMovementState ==
                    GroundMovementState.Stuck)
            {
                groundMovementState =
                    GroundMovementState.Stable;
            }

            return true;
        }

        groundMovementState =
            GroundMovementState.OffNavMesh;

        StopAgent();

        if (!recoverToNavMesh)
        {
            return false;
        }

        return
            TryRecoverToNavMesh(
                false);
    }

    private bool CanMoveToward(
        Vector3 destination)
    {
        Vector3 direction =
            destination -
            transform.position;

        direction.y =
            0f;

        if (!IsFiniteVector(
                direction))
        {
            return false;
        }

        if (direction.sqrMagnitude <=
            0.0001f)
        {
            return true;
        }

        direction.Normalize();

        if (detectWalls &&
            HasBlockingObstacle(
                direction))
        {
            groundMovementState =
                GroundMovementState.Blocked;

            return false;
        }

        if (preventLedgeFalls &&
            !HasValidGroundAhead(
                direction))
        {
            groundMovementState =
                GroundMovementState.ApproachingLedge;

            return false;
        }

        return true;
    }

    #endregion

    #region Wall Detection

    private bool HasBlockingObstacle(
        Vector3 direction)
    {
        Vector3 origin =
            transform.position +
            Vector3.up *
                wallCheckRadius;

        if (!Physics.SphereCast(
                origin,
                wallCheckRadius,
                direction,
                out RaycastHit hit,
                wallCheckDistance,
                obstacleLayers,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return
            hit.collider != null &&
            !IsOwnCollider(
                hit.collider);
    }

    #endregion

    #region Ledge Detection

    private bool HasValidGroundAhead(
        Vector3 direction)
    {
        Vector3 origin =
            transform.position +
            direction *
                forwardGroundCheckDistance +
            Vector3.up *
                0.1f;

        if (!Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                groundCheckDepth,
                groundLayers,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (hit.collider == null ||
            IsOwnCollider(
                hit.collider))
        {
            return false;
        }

        if (!validateSlopes)
        {
            return true;
        }

        float slopeAngle =
            Vector3.Angle(
                hit.normal,
                Vector3.up);

        return
            float.IsFinite(
                slopeAngle) &&
            slopeAngle <=
                maximumSlopeAngle;
    }

    #endregion

    #region NavMesh Destination

    private bool TryResolveDestination(
        Vector3 destination,
        out Vector3 resolvedDestination)
    {
        resolvedDestination =
            destination;

        if (!IsFiniteVector(
                destination))
        {
            return false;
        }

        if (!NavMesh.SamplePosition(
                destination,
                out NavMeshHit hit,
                navMeshSampleRadius,
                NavMesh.AllAreas))
        {
            return false;
        }

        if (!IsFiniteVector(
                hit.position))
        {
            return false;
        }

        resolvedDestination =
            hit.position;

        return true;
    }

    #endregion

    #region Recovery

    public bool RecoverToNavMesh()
    {
        return
            TryRecoverToNavMesh(
                true);
    }

    private bool TryRecoverToNavMesh(
        bool forceRecovery)
    {
        if (IsDead ||
            Agent == null)
        {
            return false;
        }

        if (IsOnNavMesh)
        {
            groundMovementState =
                GroundMovementState.Stable;

            recoveryAttempts =
                0;

            return true;
        }

        if (!forceRecovery &&
            recoveryTimer > 0f)
        {
            return false;
        }

        recoveryTimer =
            recoveryInterval;

        groundMovementState =
            GroundMovementState.Recovering;

        Vector3 samplePosition =
            transform.position;

        if (!IsFiniteVector(
                samplePosition))
        {
            samplePosition =
                lastValidGroundPosition;
        }

        if (TryFindRecoveryPosition(
                samplePosition,
                out Vector3 recoveryPosition))
        {
            return
                ApplyRecoveryPosition(
                    recoveryPosition);
        }

        recoveryAttempts++;

        if (recoveryAttempts <
            maximumRecoveryAttempts)
        {
            return false;
        }

        recoveryAttempts =
            0;

        if (TryFindRecoveryPosition(
                lastValidGroundPosition,
                out recoveryPosition))
        {
            return
                ApplyRecoveryPosition(
                    recoveryPosition);
        }

        if (TryFindRecoveryPosition(
                HomePosition,
                out recoveryPosition))
        {
            return
                ApplyRecoveryPosition(
                    recoveryPosition);
        }

        ReturnHome();

        return false;
    }

    private bool TryFindRecoveryPosition(
        Vector3 source,
        out Vector3 recoveryPosition)
    {
        recoveryPosition =
            Vector3.zero;

        if (!IsFiniteVector(
                source))
        {
            return false;
        }

        if (!NavMesh.SamplePosition(
                source,
                out NavMeshHit hit,
                navMeshSampleRadius,
                NavMesh.AllAreas))
        {
            return false;
        }

        recoveryPosition =
            hit.position +
            Vector3.up *
                recoveryHeightOffset;

        return
            IsFiniteVector(
                recoveryPosition);
    }

    private bool ApplyRecoveryPosition(
        Vector3 recoveryPosition)
    {
        if (Agent == null ||
            !IsFiniteVector(
                recoveryPosition))
        {
            return false;
        }

        if (!Agent.enabled)
        {
            Agent.enabled =
                true;
        }

        bool warped =
            Agent.Warp(
                recoveryPosition);

        if (!warped)
        {
            transform.position =
                recoveryPosition;

            Physics.SyncTransforms();
        }

        if (!Agent.enabled ||
            !Agent.isOnNavMesh)
        {
            return false;
        }

        Agent.isStopped =
            false;

        recoveryAttempts =
            0;

        stuckChecks =
            0;

        lastValidGroundPosition =
            transform.position;

        previousStuckPosition =
            transform.position;

        groundMovementState =
            GroundMovementState.Stable;

        return true;
    }

    #endregion

    #region Knockback Recovery

    public void NotifyKnockbackEnded()
    {
        if (!recoverAfterKnockback ||
            IsDead)
        {
            return;
        }

        knockbackRecoveryPending =
            true;

        knockbackTimer =
            knockbackRecoveryDelay;
    }

    private void UpdateKnockbackRecovery()
    {
        if (!knockbackRecoveryPending)
        {
            return;
        }

        knockbackTimer =
            Mathf.Max(
                0f,
                knockbackTimer -
                    Time.deltaTime);

        if (knockbackTimer > 0f)
        {
            return;
        }

        knockbackRecoveryPending =
            false;

        TryRecoverToNavMesh(
            true);
    }

    #endregion

    #region Stuck Detection

    private void UpdateStuckDetection()
    {
        if (!detectStuckMovement ||
            !ShouldBeMoving())
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

        float movedDistance =
            Vector3.Distance(
                currentPosition,
                previousStuckPosition);

        previousStuckPosition =
            currentPosition;

        if (!float.IsFinite(
                movedDistance))
        {
            stuckChecks =
                0;

            return;
        }

        if (movedDistance >=
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

        groundMovementState =
            GroundMovementState.Stuck;

        if (!TryRecoverToNavMesh(
                true))
        {
            ReturnHome();
        }
    }

    private bool ShouldBeMoving()
    {
        if (Agent == null ||
            !Agent.enabled ||
            !Agent.isOnNavMesh ||
            Agent.isStopped ||
            !Agent.hasPath)
        {
            return false;
        }

        return
            CurrentState ==
                AIState.Patrol ||
            CurrentState ==
                AIState.Chase ||
            CurrentState ==
                AIState.Returning;
    }

    #endregion

    #region Valid Ground Position

    private void UpdateValidGroundPosition()
    {
        if (!IsOnNavMesh ||
            !IsFiniteVector(
                transform.position))
        {
            return;
        }

        lastValidGroundPosition =
            transform.position;
    }

    private void UpdateRecoveryTimer()
    {
        if (recoveryTimer <= 0f)
        {
            return;
        }

        recoveryTimer =
            Mathf.Max(
                0f,
                recoveryTimer -
                    Time.deltaTime);
    }

    #endregion

    #region Helpers

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

    private Vector3 GetPlanarForward()
    {
        Vector3 forward =
            transform.forward;

        forward.y =
            0f;

        if (!IsFiniteVector(
                forward) ||
            forward.sqrMagnitude <=
                0.0001f)
        {
            return
                Vector3.forward;
        }

        return
            forward.normalized;
    }

    #endregion

    #region Gizmos

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        if (!drawGroundChecks)
        {
            return;
        }

        Vector3 forward =
            GetPlanarForward();

        Vector3 wallOrigin =
            transform.position +
            Vector3.up *
                wallCheckRadius;

        Gizmos.DrawLine(
            wallOrigin,
            wallOrigin +
                forward *
                    wallCheckDistance);

        Gizmos.DrawWireSphere(
            wallOrigin +
                forward *
                    wallCheckDistance,
            wallCheckRadius);

        Vector3 groundOrigin =
            transform.position +
            forward *
                forwardGroundCheckDistance +
            Vector3.up *
                0.1f;

        Gizmos.DrawLine(
            groundOrigin,
            groundOrigin +
                Vector3.down *
                    groundCheckDepth);

        Gizmos.DrawWireSphere(
            transform.position,
            navMeshSampleRadius);

        if (Application.isPlaying &&
            IsFiniteVector(
                lastValidGroundPosition))
        {
            Gizmos.DrawWireSphere(
                lastValidGroundPosition,
                0.25f);
        }
    }

    #endregion
}