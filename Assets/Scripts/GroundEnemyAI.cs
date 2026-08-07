using System;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class GroundEnemyAI : AIController
{
    #region Types

    public enum GroundMovementState
    {
        Stable,
        ApproachingLedge,
        Blocked,
        OffNavMesh,
        Recovering
    }

    #endregion

    #region Constants

    private const float MinimumDirectionSqrMagnitude =
        0.0001f;

    private const float MinimumNavMeshSampleRadius =
        0.1f;

    private const int ProbeHitCapacity =
        16;

    #endregion

    #region Inspector

    [Header("Ground Navigation")]
    [SerializeField] private bool preventLedgeFalls = true;
    [SerializeField] private bool detectWalls = true;
    [SerializeField] private bool recoverToNavMeshAutomatically = true;

    [SerializeField, Min(0.01f)]
    private float forwardProbeDistance = 0.75f;

    [SerializeField, Min(0.01f)]
    private float groundProbeDepth = 1.5f;

    [SerializeField, Min(0.01f)]
    private float wallProbeRadius = 0.25f;

    [SerializeField, Min(0.01f)]
    private float wallProbeDistance = 0.6f;

    [SerializeField]
    private LayerMask navigationBlockingLayers = ~0;

    [SerializeField]
    private LayerMask validGroundLayers = ~0;

    [Header("NavMesh Recovery")]
    [SerializeField, Min(MinimumNavMeshSampleRadius)]
    private float navMeshSampleRadius = 3f;

    [SerializeField, Min(0.1f)]
    private float navMeshRecoveryInterval = 0.5f;

    [SerializeField, Min(1)]
    private int maximumRecoveryAttempts = 5;

    [SerializeField, Min(0f)]
    private float recoveryHeightOffset = 0.1f;

    [Header("Stuck Detection")]
    [SerializeField] private bool detectStuckMovement = true;

    [SerializeField, Min(0.1f)]
    private float stuckCheckInterval = 1f;

    [SerializeField, Min(0f)]
    private float minimumMovementDistance = 0.05f;

    [SerializeField, Min(1)]
    private int maximumStuckChecks = 3;

    [Header("Knockback Recovery")]
    [SerializeField] private bool recoverAfterKnockback = true;

    [SerializeField, Min(0f)]
    private float knockbackRecoveryDelay = 0.2f;

    [Header("Debug")]
    [SerializeField]
    private GroundMovementState groundMovementState =
        GroundMovementState.Stable;

    [SerializeField] private bool logGroundSafety;

    #endregion

    #region Runtime State

    private readonly RaycastHit[] probeHits =
        new RaycastHit[ProbeHitCapacity];

    private Vector3 previousCheckedPosition;
    private Vector3 lastValidNavMeshPosition;

    private float navMeshRecoveryTimer;
    private float stuckCheckTimer;
    private float knockbackRecoveryTimer;

    private int recoveryAttempts;
    private int consecutiveStuckChecks;

    private bool knockbackRecoveryPending;
    private bool navMeshLossReported;

    #endregion

    #region Events

    public event Action<GroundEnemyAI> LedgeDetected;
    public event Action<GroundEnemyAI> WallDetected;
    public event Action<GroundEnemyAI> NavMeshLost;
    public event Action<GroundEnemyAI, Vector3> NavMeshRecovered;
    public event Action<GroundEnemyAI> MovementStuck;

    #endregion

    #region Public API

    public GroundMovementState CurrentGroundMovementState =>
        groundMovementState;

    public bool IsOnNavMesh =>
        Agent != null &&
        Agent.enabled &&
        Agent.isOnNavMesh;

    public Vector3 LastValidNavMeshPosition =>
        lastValidNavMeshPosition;

    public bool RecoverToNavMesh()
    {
        return TryRecoverToNavMesh(
            forceRecovery: true);
    }

    public void NotifyKnockbackEnded()
    {
        if (!recoverAfterKnockback ||
            IsDead)
        {
            return;
        }

        knockbackRecoveryPending =
            true;

        knockbackRecoveryTimer =
            knockbackRecoveryDelay;
    }

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        previousCheckedPosition =
            transform.position;

        lastValidNavMeshPosition =
            transform.position;

        ResetGroundRuntimeState();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        previousCheckedPosition =
            transform.position;

        lastValidNavMeshPosition =
            transform.position;

        ResetGroundRuntimeState();
    }

    protected override void Update()
    {
        base.Update();

        if (!IsInitialized ||
            IsDead ||
            CurrentState == AIState.Disabled)
        {
            return;
        }

        UpdateGroundRecoveryTimers();
        UpdateStuckDetection();
    }

    protected override void OnDestroy()
    {
        LedgeDetected = null;
        WallDetected = null;
        NavMeshLost = null;
        NavMeshRecovered = null;
        MovementStuck = null;

        base.OnDestroy();
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        forwardProbeDistance =
            Mathf.Max(
                0.01f,
                forwardProbeDistance);

        groundProbeDepth =
            Mathf.Max(
                0.01f,
                groundProbeDepth);

        wallProbeRadius =
            Mathf.Max(
                0.01f,
                wallProbeRadius);

        wallProbeDistance =
            Mathf.Max(
                0.01f,
                wallProbeDistance);

        navMeshSampleRadius =
            Mathf.Max(
                MinimumNavMeshSampleRadius,
                navMeshSampleRadius);

        navMeshRecoveryInterval =
            Mathf.Max(
                0.1f,
                navMeshRecoveryInterval);

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

        maximumStuckChecks =
            Mathf.Max(
                1,
                maximumStuckChecks);

        knockbackRecoveryDelay =
            Mathf.Max(
                0f,
                knockbackRecoveryDelay);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Vector3 origin =
            transform.position +
            Vector3.up *
            wallProbeRadius;

        Vector3 forward =
            GetPlanarForward();

        Gizmos.DrawWireSphere(
            origin +
            forward *
            wallProbeDistance,
            wallProbeRadius);

        Vector3 ledgeOrigin =
            transform.position +
            forward *
            forwardProbeDistance +
            Vector3.up *
            0.1f;

        Gizmos.DrawLine(
            ledgeOrigin,
            ledgeOrigin +
            Vector3.down *
            groundProbeDepth);

        Gizmos.DrawWireSphere(
            transform.position,
            navMeshSampleRadius);
    }

    #endregion

    #region Initialization

    protected override bool Initialize()
    {
        bool initialized =
            base.Initialize();

        if (!initialized)
            return false;

        previousCheckedPosition =
            transform.position;

        lastValidNavMeshPosition =
            transform.position;

        ResetGroundRuntimeState();

        return true;
    }

    private void ResetGroundRuntimeState()
    {
        navMeshRecoveryTimer =
            0f;

        stuckCheckTimer =
            stuckCheckInterval;

        knockbackRecoveryTimer =
            0f;

        recoveryAttempts =
            0;

        consecutiveStuckChecks =
            0;

        knockbackRecoveryPending =
            false;

        navMeshLossReported =
            false;

        groundMovementState =
            GroundMovementState.Stable;
    }

    #endregion

    #region Ground State Overrides

    protected override void UpdatePatrolState()
    {
        if (!CanContinueGroundMovement())
            return;

        base.UpdatePatrolState();
    }

    protected override void UpdateChaseState()
    {
        if (!CanContinueGroundMovement())
            return;

        base.UpdateChaseState();
    }

    protected override void UpdateReturningState()
    {
        if (!CanContinueGroundMovement())
            return;

        base.UpdateReturningState();
    }

    protected override bool MoveAgentTo(
        Vector3 destination)
    {
        if (!CanMoveToward(
                destination))
        {
            StopAgent();

            return false;
        }

        bool destinationIsValid =
            TryResolveNavMeshDestination(
                destination,
                out Vector3 resolvedDestination);

        if (!destinationIsValid)
        {
            groundMovementState =
                GroundMovementState.OffNavMesh;

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

    #region Ground Safety

    private bool CanContinueGroundMovement()
    {
        if (!recoverToNavMeshAutomatically)
        {
            return IsOnNavMesh;
        }

        if (IsOnNavMesh)
        {
            recoveryAttempts =
                0;

            lastValidNavMeshPosition =
                transform.position;

            navMeshLossReported =
                false;

            if (groundMovementState ==
                GroundMovementState.Recovering ||
                groundMovementState ==
                GroundMovementState.OffNavMesh)
            {
                groundMovementState =
                    GroundMovementState.Stable;
            }

            return true;
        }

        groundMovementState =
            GroundMovementState.OffNavMesh;

        if (!navMeshLossReported)
        {
            navMeshLossReported =
                true;

            NavMeshLost?.Invoke(
                this);
        }

        StopAgent();

        return TryRecoverToNavMesh(
            forceRecovery: false);
    }

    private bool CanMoveToward(
        Vector3 destination)
    {
        Vector3 direction =
            destination -
            transform.position;

        direction.y =
            0f;

        if (direction.sqrMagnitude <=
            MinimumDirectionSqrMagnitude)
        {
            return true;
        }

        direction.Normalize();

        if (detectWalls &&
            HasBlockingWall(
                direction))
        {
            groundMovementState =
                GroundMovementState.Blocked;

            WallDetected?.Invoke(
                this);

            LogGroundState(
                "Movement blocked by a wall.");

            return false;
        }

        if (preventLedgeFalls &&
            !HasGroundAhead(
                direction))
        {
            groundMovementState =
                GroundMovementState.ApproachingLedge;

            LedgeDetected?.Invoke(
                this);

            LogGroundState(
                "Movement stopped before a ledge.");

            return false;
        }

        return true;
    }

    private bool HasBlockingWall(
        Vector3 direction)
    {
        Vector3 origin =
            transform.position +
            Vector3.up *
            wallProbeRadius;

        int hitCount =
            Physics.SphereCastNonAlloc(
                origin,
                wallProbeRadius,
                direction,
                probeHits,
                wallProbeDistance,
                navigationBlockingLayers,
                QueryTriggerInteraction.Ignore);

        for (int index = 0;
             index < hitCount;
             index++)
        {
            Collider candidate =
                probeHits[index].collider;

            if (candidate == null ||
                IsOwnCollider(
                    candidate))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool HasGroundAhead(
        Vector3 direction)
    {
        Vector3 origin =
            transform.position +
            direction *
            forwardProbeDistance +
            Vector3.up *
            0.1f;

        int hitCount =
            Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                probeHits,
                groundProbeDepth,
                validGroundLayers,
                QueryTriggerInteraction.Ignore);

        for (int index = 0;
             index < hitCount;
             index++)
        {
            Collider candidate =
                probeHits[index].collider;

            if (candidate == null ||
                IsOwnCollider(
                    candidate))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool TryResolveNavMeshDestination(
        Vector3 requestedDestination,
        out Vector3 resolvedDestination)
    {
        resolvedDestination =
            requestedDestination;

        if (!IsFiniteVector(
                requestedDestination))
        {
            return false;
        }

        if (NavMesh.SamplePosition(
                requestedDestination,
                out NavMeshHit hit,
                navMeshSampleRadius,
                NavMesh.AllAreas))
        {
            resolvedDestination =
                hit.position;

            return true;
        }

        return false;
    }

    #endregion

    #region NavMesh Recovery

    private void UpdateGroundRecoveryTimers()
    {
        if (navMeshRecoveryTimer > 0f)
        {
            navMeshRecoveryTimer =
                Mathf.Max(
                    0f,
                    navMeshRecoveryTimer -
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

        TryRecoverToNavMesh(
            forceRecovery: true);
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
            navMeshRecoveryTimer > 0f)
        {
            return false;
        }

        if (recoveryAttempts >=
            maximumRecoveryAttempts)
        {
            LogGroundState(
                "Maximum NavMesh recovery attempts reached.");

            if (!TryRecoverToLastValidPosition())
            {
                ReturnHome();
            }

            return false;
        }

        navMeshRecoveryTimer =
            navMeshRecoveryInterval;

        recoveryAttempts++;

        groundMovementState =
            GroundMovementState.Recovering;

        Vector3 sampleOrigin =
            transform.position;

        if (!NavMesh.SamplePosition(
                sampleOrigin,
                out NavMeshHit hit,
                navMeshSampleRadius,
                NavMesh.AllAreas))
        {
            LogGroundState(
                $"NavMesh recovery attempt {recoveryAttempts} failed.");

            return false;
        }

        Vector3 recoveryPosition =
            hit.position +
            Vector3.up *
            recoveryHeightOffset;

        if (!IsFiniteVector(
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
        }

        Physics.SyncTransforms();

        if (!Agent.isOnNavMesh)
        {
            return false;
        }

        Agent.isStopped =
            false;

        recoveryAttempts =
            0;

        consecutiveStuckChecks =
            0;

        previousCheckedPosition =
            transform.position;

        groundMovementState =
            GroundMovementState.Stable;

        lastValidNavMeshPosition =
            recoveryPosition;

        navMeshLossReported =
            false;

        NavMeshRecovered?.Invoke(
            this,
            recoveryPosition);

        LogGroundState(
            "Recovered to the NavMesh.");

        return true;
    }

    #endregion

    #region Stuck Detection

    private void UpdateStuckDetection()
    {
        if (!detectStuckMovement ||
            CurrentState != AIState.Patrol &&
            CurrentState != AIState.Chase &&
            CurrentState != AIState.Returning)
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

        if (!float.IsFinite(
                movedDistance))
        {
            previousCheckedPosition =
                currentPosition;

            consecutiveStuckChecks =
                0;

            return;
        }

        previousCheckedPosition =
            currentPosition;

        bool expectsMovement =
            Agent != null &&
            Agent.enabled &&
            Agent.hasPath &&
            !Agent.isStopped;

        if (!expectsMovement ||
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

        LogGroundState(
            "Ground enemy appears to be stuck.");

        if (!TryRecoverToNavMesh(
                forceRecovery: true))
        {
            ReturnHome();
        }
    }

    #endregion

    #region Runtime Safety

    protected override bool RunRuntimeSafetyChecks()
    {
        if (!base.RunRuntimeSafetyChecks())
            return false;

        if (!recoverToNavMeshAutomatically)
            return true;

        if (IsOnNavMesh)
        {
            lastValidNavMeshPosition =
                transform.position;

            navMeshLossReported =
                false;
        }
        else if (CurrentState != AIState.Dead &&
                 CurrentState != AIState.Disabled)
        {
            TryRecoverToNavMesh(
                forceRecovery: false);
        }

        return true;
    }

    #endregion

    #region Helpers

    private bool TryRecoverToLastValidPosition()
    {
        if (Agent == null ||
            !IsFiniteVector(
                lastValidNavMeshPosition))
        {
            return false;
        }

        if (!NavMesh.SamplePosition(
                lastValidNavMeshPosition,
                out NavMeshHit hit,
                navMeshSampleRadius,
                NavMesh.AllAreas))
        {
            return false;
        }

        Vector3 recoveryPosition =
            hit.position +
            Vector3.up *
            recoveryHeightOffset;

        if (!IsFiniteVector(
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
        }

        Physics.SyncTransforms();

        if (!Agent.isOnNavMesh)
        {
            return false;
        }

        Agent.isStopped =
            false;

        recoveryAttempts =
            0;

        consecutiveStuckChecks =
            0;

        previousCheckedPosition =
            transform.position;

        lastValidNavMeshPosition =
            transform.position;

        navMeshLossReported =
            false;

        groundMovementState =
            GroundMovementState.Stable;

        NavMeshRecovered?.Invoke(
            this,
            transform.position);

        LogGroundState(
            "Recovered to the last valid NavMesh position.");

        return true;
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

    private Vector3 GetPlanarForward()
    {
        Vector3 forward =
            transform.forward;

        forward.y =
            0f;

        if (forward.sqrMagnitude <=
            MinimumDirectionSqrMagnitude)
        {
            return Vector3.forward;
        }

        return forward.normalized;
    }

    private void LogGroundState(
        string message)
    {
        if (!logGroundSafety)
            return;

        Debug.Log(
            $"{nameof(GroundEnemyAI)} on '{name}': {message}",
            this);
    }

    #endregion
}
