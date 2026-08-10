using System;
using UnityEngine;
using UnityEngine.AI;

public class AIController : MonoBehaviour
{
    #region States

    public enum AIState
    {
        Uninitialized,
        Idle,
        Patrol,
        Chase,
        Attack,
        Special,
        Stunned,
        InAir,
        Returning,
        Dead,
        Disabled
    }

    #endregion

    #region State

    [Header("State")]

    [SerializeField]
    private AIState startingState = AIState.Idle;

    [SerializeField]
    private AIState currentState = AIState.Uninitialized;

    #endregion

    #region Targeting

    [Header("Targeting")]

    [SerializeField]
    private Transform target;

    [SerializeField]
    private bool findTargetAutomatically = true;

    [SerializeField, Min(0f)]
    private float detectionRange = 20f;

    [SerializeField, Min(0f)]
    private float loseTargetRange = 30f;

    [SerializeField, Range(0f, 360f)]
    private float fieldOfView = 180f;

    [SerializeField]
    private bool requireLineOfSight;

    [SerializeField]
    private LayerMask sightBlockingLayers = ~0;

    [SerializeField, Min(0.05f)]
    private float targetSearchInterval = 0.5f;

    #endregion

    #region Navigation

    [Header("Navigation")]

    [SerializeField]
    private NavMeshAgent agent;

    [SerializeField, Min(0f)]
    private float movementSpeed = 5f;

    [SerializeField, Min(0f)]
    private float angularSpeed = 720f;

    [SerializeField, Min(0f)]
    private float acceleration = 20f;

    [SerializeField, Min(0f)]
    private float stoppingDistance = 1.5f;

    #endregion

    #region Home

    [Header("Home")]

    [SerializeField]
    private bool returnHomeWhenTargetLost = true;

    [SerializeField, Min(0.01f)]
    private float homeArrivalDistance = 0.5f;

    [SerializeField, Min(0f)]
    private float maximumDistanceFromHome = 50f;

    #endregion

    #region Patrol

    [Header("Patrol")]

    [SerializeField]
    private Transform[] patrolPoints =
        Array.Empty<Transform>();

    [SerializeField, Min(0f)]
    private float patrolWaitDuration = 1f;

    [SerializeField]
    private bool loopPatrol = true;

    #endregion

    #region Attack

    [Header("Attack")]

    [SerializeField, Min(0.1f)]
    private float attackRange = 2f;

    [SerializeField, Min(0)]
    private int attackDamage = 1;

    [SerializeField, Min(0f)]
    private float attackCooldown = 1f;

    [SerializeField, Min(0f)]
    private float attackWindup = 0.15f;

    [SerializeField]
    private bool faceTargetWhileAttacking = true;

    #endregion

    #region Health

    [Header("Health")]

    [SerializeField, Min(1)]
    private int maximumHealth = 3;

    [SerializeField, Min(1)]
    private int startingHealth = 3;

    [SerializeField, Min(0f)]
    private float damageImmunityDuration = 0.1f;

    [SerializeField]
    private bool destroyOnDeath = true;

    [SerializeField, Min(0f)]
    private float destructionDelay = 1f;

    #endregion

    #region Stun

    [Header("Stun")]

    [SerializeField]
    private bool canBeStunned = true;

    [SerializeField, Min(0f)]
    private float defaultStunDuration = 1f;

    #endregion

    #region Grounding

    [Header("Grounding")]

    [SerializeField]
    private bool monitorGrounding;

    [SerializeField]
    private Transform groundProbe;

    [SerializeField, Min(0.01f)]
    private float groundProbeRadius = 0.25f;

    [SerializeField, Min(0.01f)]
    private float groundProbeDistance = 0.5f;

    [SerializeField]
    private LayerMask groundLayers = ~0;

    #endregion

    #region Animation

    [Header("Animation")]

    [SerializeField]
    private Animator animator;

    [SerializeField]
    private string speedParameter = "Speed";

    [SerializeField]
    private string groundedParameter = "Grounded";

    [SerializeField]
    private string stunnedParameter = "Stunned";

    [SerializeField]
    private string alertTrigger = "Alert";

    [SerializeField]
    private string attackTrigger = "Attack";

    [SerializeField]
    private string specialTrigger = "Special";

    [SerializeField]
    private string damageTrigger = "Damaged";

    [SerializeField]
    private string deathTrigger = "Dead";

    #endregion

    #region Audio

    [Header("Audio")]

    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip alertSound;

    [SerializeField]
    private AudioClip attackSound;

    [SerializeField]
    private AudioClip damageSound;

    [SerializeField]
    private AudioClip stunnedSound;

    [SerializeField]
    private AudioClip deathSound;

    #endregion

    #region Effects

    [Header("Effects")]

    [SerializeField]
    private GameObject alertEffect;

    [SerializeField]
    private GameObject attackEffect;

    [SerializeField]
    private GameObject damageEffect;

    [SerializeField]
    private GameObject stunnedEffect;

    [SerializeField]
    private GameObject deathEffect;

    [SerializeField, Min(0f)]
    private float effectLifetime = 3f;

    #endregion

    #region Runtime

    private Vector3 homePosition;
    private Quaternion homeRotation;

    private float attackCooldownTimer;
    private float attackWindupTimer;
    private float damageImmunityTimer;
    private float stunTimer;
    private float patrolWaitTimer;
    private float targetSearchTimer;

    private int currentHealth;
    private int patrolIndex;

    private bool attackPending;
    private bool initialized;
    private bool grounded;
    private bool shuttingDown;

    #endregion

    #region Events

    public event Action<AIController, AIState, AIState>
        StateChanged;

    public event Action<AIController, Transform>
        TargetAcquired;

    public event Action<AIController>
        TargetLost;

    public event Action<AIController, int>
        Damaged;

    public event Action<AIController>
        Attacked;

    public event Action<AIController>
        SpecialStarted;

    public event Action<AIController>
        Stunned;

    public event Action<AIController>
        Recovered;

    public event Action<AIController>
        Died;

    #endregion

    #region Properties

    public AIState CurrentState =>
        currentState;

    public Transform Target =>
        target;

    public NavMeshAgent Agent =>
        agent;

    public int CurrentHealth =>
        currentHealth;

    public int MaximumHealth =>
        maximumHealth;

    public bool IsInitialized =>
        initialized;

    public bool IsDead =>
        currentState == AIState.Dead;

    public bool IsStunned =>
        currentState == AIState.Stunned;

    public bool IsGrounded =>
        grounded;

    public bool IsInvulnerable =>
        damageImmunityTimer > 0f;

    public Vector3 HomePosition =>
        homePosition;

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        Initialize();
    }

    protected virtual void OnEnable()
    {
        if (shuttingDown)
        {
            return;
        }

        if (!initialized)
        {
            Initialize();
        }

        if (initialized &&
            currentState == AIState.Disabled)
        {
            ChangeState(
                startingState);
        }
    }

    protected virtual void Update()
    {
        if (!initialized ||
            shuttingDown ||
            IsDead ||
            currentState == AIState.Disabled)
        {
            return;
        }

        UpdateTimers();
        UpdateGrounding();
        UpdateAnimator();
        UpdateState();
    }

    protected virtual void OnDisable()
    {
        StopAgent();

        if (!shuttingDown &&
            !IsDead)
        {
            currentState =
                AIState.Disabled;
        }
    }

    protected virtual void OnDestroy()
    {
        shuttingDown =
            true;

        CancelInvoke();

        StateChanged =
            null;

        TargetAcquired =
            null;

        TargetLost =
            null;

        Damaged =
            null;

        Attacked =
            null;

        SpecialStarted =
            null;

        Stunned =
            null;

        Recovered =
            null;

        Died =
            null;
    }

    protected virtual void OnValidate()
    {
        detectionRange =
            Mathf.Max(
                0f,
                detectionRange);

        loseTargetRange =
            Mathf.Max(
                detectionRange,
                loseTargetRange);

        attackRange =
            Mathf.Max(
                0.1f,
                attackRange);

        movementSpeed =
            Mathf.Max(
                0f,
                movementSpeed);

        angularSpeed =
            Mathf.Max(
                0f,
                angularSpeed);

        acceleration =
            Mathf.Max(
                0f,
                acceleration);

        stoppingDistance =
            Mathf.Max(
                0f,
                stoppingDistance);

        homeArrivalDistance =
            Mathf.Max(
                0.01f,
                homeArrivalDistance);

        maximumDistanceFromHome =
            Mathf.Max(
                0f,
                maximumDistanceFromHome);

        patrolWaitDuration =
            Mathf.Max(
                0f,
                patrolWaitDuration);

        attackDamage =
            Mathf.Max(
                0,
                attackDamage);

        attackCooldown =
            Mathf.Max(
                0f,
                attackCooldown);

        attackWindup =
            Mathf.Max(
                0f,
                attackWindup);

        maximumHealth =
            Mathf.Max(
                1,
                maximumHealth);

        startingHealth =
            Mathf.Clamp(
                startingHealth,
                1,
                maximumHealth);

        damageImmunityDuration =
            Mathf.Max(
                0f,
                damageImmunityDuration);

        destructionDelay =
            Mathf.Max(
                0f,
                destructionDelay);

        defaultStunDuration =
            Mathf.Max(
                0f,
                defaultStunDuration);

        groundProbeRadius =
            Mathf.Max(
                0.01f,
                groundProbeRadius);

        groundProbeDistance =
            Mathf.Max(
                0.01f,
                groundProbeDistance);

        targetSearchInterval =
            Mathf.Max(
                0.05f,
                targetSearchInterval);

        effectLifetime =
            Mathf.Max(
                0f,
                effectLifetime);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            detectionRange);

        Gizmos.DrawWireSphere(
            transform.position,
            attackRange);

        Gizmos.DrawWireSphere(
            transform.position,
            loseTargetRange);
    }

    #endregion

    #region Initialization

    protected virtual bool Initialize()
    {
        if (initialized)
        {
            return true;
        }

        ResolveReferences();

        if (agent == null)
        {
            currentState =
                AIState.Uninitialized;

            return false;
        }

        ConfigureAgent();

        homePosition =
            transform.position;

        homeRotation =
            transform.rotation;

        currentHealth =
            Mathf.Clamp(
                startingHealth,
                1,
                maximumHealth);

        attackCooldownTimer =
            0f;

        attackWindupTimer =
            0f;

        damageImmunityTimer =
            0f;

        stunTimer =
            0f;

        patrolWaitTimer =
            0f;

        targetSearchTimer =
            0f;

        attackPending =
            false;

        initialized =
            true;

        ChangeState(
            startingState);

        return true;
    }

    protected virtual void ResolveReferences()
    {
        agent ??=
            GetComponent<NavMeshAgent>();

        animator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        audioSource ??=
            GetComponent<AudioSource>();

        audioSource ??=
            GetComponentInChildren<AudioSource>(
                includeInactive: true);

        groundProbe ??=
            transform;

        patrolPoints ??=
            Array.Empty<Transform>();
    }

    protected virtual void ConfigureAgent()
    {
        if (agent == null)
        {
            return;
        }

        agent.speed =
            movementSpeed;

        agent.angularSpeed =
            angularSpeed;

        agent.acceleration =
            acceleration;

        agent.stoppingDistance =
            stoppingDistance;

        agent.autoBraking =
            true;
    }

    #endregion

    #region Public API

    public bool SetTarget(
        Transform newTarget)
    {
        if (!IsValidTarget(
                newTarget))
        {
            return false;
        }

        if (target ==
            newTarget)
        {
            return true;
        }

        target =
            newTarget;

        TargetAcquired?.Invoke(
            this,
            target);

        return true;
    }

    public void ClearTarget()
    {
        if (target == null)
        {
            return;
        }

        target =
            null;

        TargetLost?.Invoke(
            this);
    }

    public bool SetState(
        AIState newState)
    {
        if (!initialized ||
            shuttingDown ||
            IsDead)
        {
            return false;
        }

        if (!Enum.IsDefined(
                typeof(AIState),
                newState))
        {
            return false;
        }

        ChangeState(
            newState);

        return true;
    }

    public bool TakeDamage(
        int damage)
    {
        if (!CanReceiveDamage() ||
            damage <= 0)
        {
            return false;
        }

        int appliedDamage =
            Mathf.Min(
                currentHealth,
                damage);

        currentHealth =
            Mathf.Max(
                0,
                currentHealth -
                    appliedDamage);

        damageImmunityTimer =
            damageImmunityDuration;

        PlayDamagePresentation();

        Damaged?.Invoke(
            this,
            appliedDamage);

        if (currentHealth <= 0)
        {
            Die();
        }

        return true;
    }

    public bool HitByThunderShoot(
        int damage,
        float stunDuration)
    {
        bool damaged =
            TakeDamage(
                damage);

        bool stunned =
            !IsDead &&
            stunDuration > 0f &&
            Stun(
                stunDuration);

        return
            damaged ||
            stunned;
    }

    public bool Heal(
        int amount)
    {
        if (!initialized ||
            IsDead ||
            amount <= 0)
        {
            return false;
        }

        int previousHealth =
            currentHealth;

        currentHealth =
            Mathf.Clamp(
                currentHealth + amount,
                0,
                maximumHealth);

        return
            currentHealth >
            previousHealth;
    }

    public bool Stun()
    {
        return
            Stun(
                defaultStunDuration);
    }

    public bool Stun(
        float duration)
    {
        if (!initialized ||
            IsDead ||
            !canBeStunned ||
            duration <= 0f)
        {
            return false;
        }

        stunTimer =
            duration;

        ChangeState(
            AIState.Stunned);

        return true;
    }

    public bool ReturnHome()
    {
        if (!initialized ||
            IsDead)
        {
            return false;
        }

        ClearTarget();

        ChangeState(
            AIState.Returning);

        return true;
    }

    public bool ResetAI()
    {
        if (shuttingDown)
        {
            return false;
        }

        CancelInvoke(
            nameof(DestroyAI));

        currentHealth =
            Mathf.Clamp(
                startingHealth,
                1,
                maximumHealth);

        attackCooldownTimer =
            0f;

        attackWindupTimer =
            0f;

        damageImmunityTimer =
            0f;

        stunTimer =
            0f;

        patrolWaitTimer =
            0f;

        targetSearchTimer =
            0f;

        attackPending =
            false;

        target =
            null;

        transform.SetPositionAndRotation(
            homePosition,
            homeRotation);

        RestoreAgent();

        currentState =
            startingState;

        enabled =
            true;

        return true;
    }

    #endregion

    #region State Machine

    protected virtual void UpdateState()
    {
        switch (currentState)
        {
            case AIState.Idle:
                UpdateIdleState();
                break;

            case AIState.Patrol:
                UpdatePatrolState();
                break;

            case AIState.Chase:
                UpdateChaseState();
                break;

            case AIState.Attack:
                UpdateAttackState();
                break;

            case AIState.Special:
                UpdateSpecialState();
                break;

            case AIState.Stunned:
                UpdateStunnedState();
                break;

            case AIState.InAir:
                UpdateInAirState();
                break;

            case AIState.Returning:
                UpdateReturningState();
                break;
        }
    }

    protected virtual void ChangeState(
        AIState newState)
    {
        if (currentState ==
            newState)
        {
            return;
        }

        AIState previousState =
            currentState;

        currentState =
            newState;

        EnterState(
            newState);

        StateChanged?.Invoke(
            this,
            previousState,
            newState);
    }

    protected virtual void EnterState(
        AIState state)
    {
        switch (state)
        {
            case AIState.Idle:
                StopAgent();
                break;

            case AIState.Chase:
                PlayAlertPresentation();
                break;

            case AIState.Attack:
                StopAgent();
                break;

            case AIState.Special:
                StopAgent();

                TriggerAnimator(
                    specialTrigger);

                SpecialStarted?.Invoke(
                    this);
                break;

            case AIState.Stunned:
                StopAgent();

                ApplyStunnedPresentation();

                Stunned?.Invoke(
                    this);
                break;

            case AIState.Dead:
            case AIState.Disabled:
                StopAgent();
                break;
        }
    }

    #endregion

    #region Idle

    protected virtual void UpdateIdleState()
    {
        if (TryAcquireTarget())
        {
            ChangeState(
                AIState.Chase);

            return;
        }

        if (HasPatrolPoint())
        {
            ChangeState(
                AIState.Patrol);
        }
    }

    #endregion

    #region Patrol

    protected virtual void UpdatePatrolState()
    {
        if (TryAcquireTarget())
        {
            ChangeState(
                AIState.Chase);

            return;
        }

        Transform patrolPoint =
            GetCurrentPatrolPoint();

        if (patrolPoint == null)
        {
            ChangeState(
                AIState.Idle);

            return;
        }

        MoveAgentTo(
            patrolPoint.position);

        if (!HasReachedPosition(
                patrolPoint.position,
                homeArrivalDistance))
        {
            return;
        }

        StopAgent();

        patrolWaitTimer +=
            Time.deltaTime;

        if (patrolWaitTimer <
            patrolWaitDuration)
        {
            return;
        }

        patrolWaitTimer =
            0f;

        AdvancePatrolPoint();
    }

    #endregion

    #region Chase

    protected virtual void UpdateChaseState()
    {
        if (!IsValidTarget(
                target))
        {
            HandleTargetLost();

            return;
        }

        float distance =
            GetDistanceToTarget();

        if (distance >
            loseTargetRange)
        {
            HandleTargetLost();

            return;
        }

        if (maximumDistanceFromHome > 0f &&
            Vector3.Distance(
                transform.position,
                homePosition) >
            maximumDistanceFromHome)
        {
            ReturnHome();

            return;
        }

        if (distance <=
            attackRange)
        {
            ChangeState(
                AIState.Attack);

            return;
        }

        MoveAgentTo(
            target.position);
    }

    #endregion

    #region Attack

    protected virtual void UpdateAttackState()
    {
        if (!IsValidTarget(
                target))
        {
            HandleTargetLost();

            return;
        }

        float distance =
            GetDistanceToTarget();

        if (distance >
            attackRange)
        {
            attackPending =
                false;

            ChangeState(
                AIState.Chase);

            return;
        }

        if (faceTargetWhileAttacking)
        {
            FaceTarget();
        }

        if (attackPending)
        {
            attackWindupTimer -=
                Time.deltaTime;

            if (attackWindupTimer <= 0f)
            {
                attackPending =
                    false;

                PerformAttack();
            }

            return;
        }

        if (attackCooldownTimer <= 0f)
        {
            BeginAttack();
        }
    }

    protected virtual void BeginAttack()
    {
        if (attackCooldownTimer > 0f ||
            attackPending)
        {
            return;
        }

        attackPending =
            true;

        attackWindupTimer =
            attackWindup;

        TriggerAnimator(
            attackTrigger);

        SpawnEffect(
            attackEffect,
            transform.position);

        PlaySound(
            attackSound);
    }

    protected virtual void PerformAttack()
    {
        if (!IsValidTarget(
                target) ||
            GetDistanceToTarget() >
                attackRange)
        {
            return;
        }

        Health health =
            ResolveHealth(
                target);

        if (health != null &&
            !health.dead &&
            attackDamage > 0)
        {
            health.TakeDamage(
                attackDamage);
        }

        attackCooldownTimer =
            attackCooldown;

        Attacked?.Invoke(
            this);
    }

    #endregion

    #region Special

    protected virtual void UpdateSpecialState()
    {
    }

    #endregion

    #region Stun

    protected virtual void UpdateStunnedState()
    {
        stunTimer -=
            Time.deltaTime;

        if (stunTimer > 0f)
        {
            return;
        }

        SetAnimatorBool(
            stunnedParameter,
            false);

        ChangeState(
            IsValidTarget(target)
                ? AIState.Chase
                : AIState.Idle);

        Recovered?.Invoke(
            this);
    }

    protected virtual void ApplyStunnedPresentation()
    {
        SetAnimatorBool(
            stunnedParameter,
            true);

        SpawnEffect(
            stunnedEffect,
            transform.position);

        PlaySound(
            stunnedSound);
    }

    #endregion

    #region Air

    protected virtual void UpdateInAirState()
    {
        if (!monitorGrounding ||
            grounded)
        {
            ChangeState(
                IsValidTarget(target)
                    ? AIState.Chase
                    : AIState.Idle);
        }
    }

    #endregion

    #region Returning

    protected virtual void UpdateReturningState()
    {
        MoveAgentTo(
            homePosition);

        if (!HasReachedPosition(
                homePosition,
                homeArrivalDistance))
        {
            return;
        }

        StopAgent();

        transform.rotation =
            homeRotation;

        ChangeState(
            HasPatrolPoint()
                ? AIState.Patrol
                : AIState.Idle);
    }

    #endregion

    #region Targeting

    protected virtual bool TryAcquireTarget()
    {
        if (IsValidTarget(
                target))
        {
            return
                IsTargetDetectable(
                    target);
        }

        if (!findTargetAutomatically)
        {
            return false;
        }

        targetSearchTimer -=
            Time.deltaTime;

        if (targetSearchTimer > 0f)
        {
            return false;
        }

        targetSearchTimer =
            targetSearchInterval;

        ResolveTarget();

        return
            IsValidTarget(target) &&
            IsTargetDetectable(
                target);
    }

    protected virtual void ResolveTarget()
    {
        UltimatePlayerMovement[] players =
            FindObjectsByType<
                UltimatePlayerMovement>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        UltimatePlayerMovement closestPlayer =
            null;

        float closestDistance =
            float.PositiveInfinity;

        foreach (UltimatePlayerMovement player
            in players)
        {
            if (player == null ||
                !player.isActiveAndEnabled ||
                !IsFiniteVector(
                    player.transform.position))
            {
                continue;
            }

            float distance =
                (
                    player.transform.position -
                    transform.position
                ).sqrMagnitude;

            if (!float.IsFinite(
                    distance) ||
                distance >=
                    closestDistance)
            {
                continue;
            }

            closestDistance =
                distance;

            closestPlayer =
                player;
        }

        if (closestPlayer != null)
        {
            SetTarget(
                closestPlayer.transform);
        }
    }

    protected virtual bool IsTargetDetectable(
        Transform candidate)
    {
        if (!IsValidTarget(
                candidate))
        {
            return false;
        }

        Vector3 direction =
            candidate.position -
            transform.position;

        float distanceSquared =
            direction.sqrMagnitude;

        if (!float.IsFinite(
                distanceSquared) ||
            distanceSquared >
                detectionRange *
                detectionRange)
        {
            return false;
        }

        if (fieldOfView < 360f &&
            direction.sqrMagnitude >
                0.0001f)
        {
            float angle =
                Vector3.Angle(
                    transform.forward,
                    direction.normalized);

            if (angle >
                fieldOfView * 0.5f)
            {
                return false;
            }
        }

        return
            !requireLineOfSight ||
            HasLineOfSight(
                candidate);
    }

    protected virtual bool HasLineOfSight(
        Transform candidate)
    {
        if (!IsValidTarget(
                candidate))
        {
            return false;
        }

        Vector3 origin =
            transform.position +
            Vector3.up * 0.5f;

        Vector3 destination =
            candidate.position +
            Vector3.up * 0.5f;

        Vector3 direction =
            destination -
            origin;

        float distance =
            direction.magnitude;

        if (distance <= 0.001f)
        {
            return true;
        }

        if (!Physics.Raycast(
                origin,
                direction.normalized,
                out RaycastHit hit,
                distance,
                sightBlockingLayers,
                QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        return
            hit.transform ==
                candidate ||
            hit.transform.IsChildOf(
                candidate) ||
            candidate.IsChildOf(
                hit.transform);
    }

    protected virtual void HandleTargetLost()
    {
        ClearTarget();

        ChangeState(
            returnHomeWhenTargetLost
                ? AIState.Returning
                : AIState.Idle);
    }

    protected virtual float GetDistanceToTarget()
    {
        if (!IsValidTarget(
                target))
        {
            return
                float.PositiveInfinity;
        }

        return
            Vector3.Distance(
                transform.position,
                target.position);
    }

    protected virtual bool IsValidTarget(
        Transform candidate)
    {
        return
            candidate != null &&
            candidate.gameObject.activeInHierarchy &&
            IsFiniteVector(
                candidate.position);
    }

    #endregion

    #region Navigation

    protected virtual bool MoveAgentTo(
        Vector3 destination)
    {
        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh ||
            !IsFiniteVector(
                destination))
        {
            return false;
        }

        agent.isStopped =
            false;

        return
            agent.SetDestination(
                destination);
    }

    protected virtual void StopAgent()
    {
        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
        {
            return;
        }

        agent.isStopped =
            true;

        agent.ResetPath();
    }

    protected virtual void RestoreAgent()
    {
        if (agent == null)
        {
            return;
        }

        ConfigureAgent();

        if (agent.enabled &&
            agent.isOnNavMesh)
        {
            agent.isStopped =
                false;
        }
    }

    protected virtual bool HasReachedPosition(
        Vector3 position,
        float distance)
    {
        if (!IsFiniteVector(
                position))
        {
            return false;
        }

        return
            (
                transform.position -
                position
            ).sqrMagnitude <=
            distance *
                distance;
    }

    #endregion

    #region Patrol

    protected virtual bool HasPatrolPoint()
    {
        if (patrolPoints == null ||
            patrolPoints.Length == 0)
        {
            return false;
        }

        foreach (Transform point
            in patrolPoints)
        {
            if (point != null)
            {
                return true;
            }
        }

        return false;
    }

    protected virtual Transform GetCurrentPatrolPoint()
    {
        if (!HasPatrolPoint())
        {
            return null;
        }

        patrolIndex =
            Mathf.Clamp(
                patrolIndex,
                0,
                patrolPoints.Length - 1);

        for (int offset = 0;
            offset < patrolPoints.Length;
            offset++)
        {
            int index =
                (
                    patrolIndex +
                    offset
                ) %
                patrolPoints.Length;

            if (patrolPoints[index] !=
                null)
            {
                patrolIndex =
                    index;

                return
                    patrolPoints[index];
            }
        }

        return null;
    }

    protected virtual void AdvancePatrolPoint()
    {
        if (patrolPoints == null ||
            patrolPoints.Length == 0)
        {
            return;
        }

        patrolIndex =
            loopPatrol
                ? (patrolIndex + 1) %
                    patrolPoints.Length
                : Mathf.Min(
                    patrolIndex + 1,
                    patrolPoints.Length - 1);
    }

    #endregion

    #region Grounding

    protected virtual void UpdateGrounding()
    {
        if (!monitorGrounding)
        {
            grounded =
                true;

            return;
        }

        Vector3 origin =
            groundProbe != null
                ? groundProbe.position
                : transform.position;

        grounded =
            Physics.SphereCast(
                origin +
                    Vector3.up * 0.05f,
                groundProbeRadius,
                Vector3.down,
                out _,
                groundProbeDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

        if (!grounded &&
            currentState !=
                AIState.Stunned &&
            currentState !=
                AIState.Dead &&
            currentState !=
                AIState.InAir)
        {
            ChangeState(
                AIState.InAir);
        }
    }

    #endregion

    #region Damage

    protected virtual bool CanReceiveDamage()
    {
        return
            initialized &&
            !IsDead &&
            !shuttingDown &&
            damageImmunityTimer <= 0f;
    }

    protected virtual void PlayDamagePresentation()
    {
        TriggerAnimator(
            damageTrigger);

        SpawnEffect(
            damageEffect,
            transform.position);

        PlaySound(
            damageSound);
    }

    #endregion

    #region Death

    protected virtual void Die()
    {
        if (IsDead)
        {
            return;
        }

        currentHealth =
            0;

        ChangeState(
            AIState.Dead);

        StopAgent();

        TriggerAnimator(
            deathTrigger);

        SpawnEffect(
            deathEffect,
            transform.position);

        PlaySound(
            deathSound);

        Died?.Invoke(
            this);

        if (!destroyOnDeath)
        {
            return;
        }

        if (destructionDelay <= 0f)
        {
            DestroyAI();

            return;
        }

        Invoke(
            nameof(DestroyAI),
            destructionDelay);
    }

    protected virtual void DestroyAI()
    {
        if (!shuttingDown)
        {
            Destroy(
                gameObject);
        }
    }

    #endregion

    #region Rotation

    protected virtual void FaceTarget()
    {
        if (!IsValidTarget(
                target))
        {
            return;
        }

        Vector3 direction =
            target.position -
            transform.position;

        direction.y =
            0f;

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

        transform.rotation =
            Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                angularSpeed *
                    Time.deltaTime);
    }

    #endregion

    #region Timers

    protected virtual void UpdateTimers()
    {
        if (attackCooldownTimer > 0f)
        {
            attackCooldownTimer =
                Mathf.Max(
                    0f,
                    attackCooldownTimer -
                        Time.deltaTime);
        }

        if (damageImmunityTimer > 0f)
        {
            damageImmunityTimer =
                Mathf.Max(
                    0f,
                    damageImmunityTimer -
                        Time.deltaTime);
        }
    }

    #endregion

    #region Animation

    protected virtual void UpdateAnimator()
    {
        if (!HasValidAnimator())
        {
            return;
        }

        float speed =
            agent != null &&
            agent.enabled &&
            agent.isOnNavMesh
                ? agent.velocity.magnitude
                : 0f;

        SetAnimatorFloat(
            speedParameter,
            speed);

        SetAnimatorBool(
            groundedParameter,
            grounded);
    }

    protected virtual void TriggerAnimator(
        string parameterName)
    {
        if (!HasAnimatorParameter(
                parameterName,
                AnimatorControllerParameterType.Trigger))
        {
            return;
        }

        animator.SetTrigger(
            parameterName);
    }

    protected virtual void SetAnimatorBool(
        string parameterName,
        bool value)
    {
        if (!HasAnimatorParameter(
                parameterName,
                AnimatorControllerParameterType.Bool))
        {
            return;
        }

        animator.SetBool(
            parameterName,
            value);
    }

    protected virtual void SetAnimatorFloat(
        string parameterName,
        float value)
    {
        if (!HasAnimatorParameter(
                parameterName,
                AnimatorControllerParameterType.Float))
        {
            return;
        }

        animator.SetFloat(
            parameterName,
            value);
    }

    private bool HasAnimatorParameter(
        string parameterName,
        AnimatorControllerParameterType parameterType)
    {
        if (!HasValidAnimator() ||
            string.IsNullOrWhiteSpace(
                parameterName))
        {
            return false;
        }

        foreach (
            AnimatorControllerParameter parameter
            in animator.parameters)
        {
            if (parameter.name ==
                    parameterName &&
                parameter.type ==
                    parameterType)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasValidAnimator()
    {
        return
            animator != null &&
            animator.isActiveAndEnabled &&
            animator.runtimeAnimatorController !=
                null;
    }

    protected virtual void PlayAlertPresentation()
    {
        TriggerAnimator(
            alertTrigger);

        SpawnEffect(
            alertEffect,
            transform.position);

        PlaySound(
            alertSound);
    }

    #endregion

    #region Audio

    protected virtual void PlaySound(
        AudioClip clip)
    {
        if (audioSource == null ||
            clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(
            clip);
    }

    #endregion

    #region Effects

    protected virtual void SpawnEffect(
        GameObject effect,
        Vector3 position)
    {
        if (effect == null ||
            !IsFiniteVector(
                position))
        {
            return;
        }

        GameObject instance =
            Instantiate(
                effect,
                position,
                transform.rotation);

        if (effectLifetime > 0f)
        {
            Destroy(
                instance,
                effectLifetime);
        }
    }

    #endregion

    #region Helpers

    private static Health ResolveHealth(
        Transform targetTransform)
    {
        if (targetTransform == null)
        {
            return null;
        }

        Health health =
            targetTransform
                .GetComponent<Health>();

        health ??=
            targetTransform
                .GetComponentInParent<Health>();

        health ??=
            targetTransform
                .GetComponentInChildren<Health>(
                    includeInactive: true);

        return health;
    }

    protected static bool IsFiniteVector(
        Vector3 value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }

    protected static bool IsFiniteQuaternion(
        Quaternion value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z) &&
            float.IsFinite(value.w);
    }

    #endregion
}