using System;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class AIController : MonoBehaviour
{
    #region Types

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

    #region Constants

    private const string DefaultPlayerTag = "Player";
    private const string DefaultSpeedParameter = "Speed";
    private const string DefaultGroundedParameter = "Grounded";
    private const string DefaultStunnedParameter = "Stunned";
    private const string DefaultAttackTrigger = "Attack";
    private const string DefaultSpecialTrigger = "Special";
    private const string DefaultDamageTrigger = "Damaged";
    private const string DefaultDeathTrigger = "Dead";

    #endregion

    #region Inspector

    [Header("State")]
    [SerializeField] private AIState startingState = AIState.Idle;
    [SerializeField] private AIState currentState = AIState.Uninitialized;

    [Header("Targeting")]
    [SerializeField] private Transform target;
    [SerializeField] private bool resolveTargetAutomatically = true;
    [SerializeField] private string playerTag = DefaultPlayerTag;
    [SerializeField, Min(0f)] private float detectionRange = 20f;
    [SerializeField, Min(0f)] private float loseTargetRange = 30f;
    [SerializeField, Min(0f)] private float attackRange = 2f;
    [SerializeField, Range(0f, 360f)] private float fieldOfView = 180f;
    [SerializeField] private bool requireLineOfSight;
    [SerializeField] private LayerMask lineOfSightBlockingLayers = ~0;
    [SerializeField, Min(0.05f)] private float targetRefreshInterval = 0.5f;

    [Header("Navigation")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField, Min(0f)] private float movementSpeed = 5f;
    [SerializeField, Min(0f)] private float angularSpeed = 720f;
    [SerializeField, Min(0f)] private float acceleration = 20f;
    [SerializeField, Min(0f)] private float stoppingDistance = 1.5f;
    [SerializeField] private bool updatePositionAutomatically = true;
    [SerializeField] private bool updateRotationAutomatically = true;
    [SerializeField] private bool autoBraking = true;

    [Header("Home And Return")]
    [SerializeField] private bool returnHomeWhenTargetLost = true;
    [SerializeField, Min(0.01f)] private float homeArrivalDistance = 0.25f;
    [SerializeField, Min(0f)] private float maximumDistanceFromHome = 100f;

    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints = Array.Empty<Transform>();
    [SerializeField, Min(0f)] private float patrolWaitDuration = 1f;
    [SerializeField] private bool loopPatrol = true;

    [Header("Attack")]
    [SerializeField, Min(0f)] private float attackCooldown = 1f;
    [SerializeField, Min(0f)] private float attackWindup = 0.15f;
    [SerializeField, Min(0)] private int attackDamage = 1;
    [SerializeField] private string damageMessage = "TakeDamage";
    [SerializeField] private bool faceTargetWhileAttacking = true;

    [Header("Health")]
    [SerializeField, Min(1)] private int maximumHealth = 3;
    [SerializeField, Min(1)] private int startingHealth = 3;
    [SerializeField, Min(0f)] private float damageImmunityDuration = 0.1f;
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField, Min(0f)] private float destructionDelay = 1f;

    [Header("Stun")]
    [SerializeField] private bool canBeStunned = true;
    [SerializeField, Min(0f)] private float defaultStunDuration = 1f;
    [SerializeField] private bool refreshStunDuration = true;
    [SerializeField] private Behaviour[] behavioursDisabledWhileStunned = Array.Empty<Behaviour>();

    [Header("Grounding")]
    [SerializeField] private bool monitorGrounding = true;
    [SerializeField] private Transform groundProbe;
    [SerializeField, Min(0.01f)] private float groundProbeRadius = 0.25f;
    [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.5f;
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = DefaultSpeedParameter;
    [SerializeField] private string groundedParameter = DefaultGroundedParameter;
    [SerializeField] private string stunnedParameter = DefaultStunnedParameter;
    [SerializeField] private string attackTrigger = DefaultAttackTrigger;
    [SerializeField] private string specialTrigger = DefaultSpecialTrigger;
    [SerializeField] private string damageTrigger = DefaultDamageTrigger;
    [SerializeField] private string deathTrigger = DefaultDeathTrigger;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip alertSound;
    [SerializeField] private AudioClip attackSound;
    [SerializeField] private AudioClip damageSound;
    [SerializeField] private AudioClip stunnedSound;
    [SerializeField] private AudioClip deathSound;

    [Header("Effects")]
    [SerializeField] private ParticleSystem alertEffect;
    [SerializeField] private ParticleSystem attackEffect;
    [SerializeField] private ParticleSystem damageEffect;
    [SerializeField] private ParticleSystem stunnedEffect;
    [SerializeField] private ParticleSystem deathEffect;

    [Header("Runtime Safety")]
    [SerializeField] private bool enableRuntimeSafety = true;
    [SerializeField] private bool restoreDisabledComponents = true;
    [SerializeField, Min(0.1f)] private float safetyCheckInterval = 1f;
    [SerializeField, Min(0.01f)] private float minimumValidScale = 0.01f;
    [SerializeField, Min(1f)] private float maximumSafeAgentSpeed = 100f;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private bool[] originalBehaviourStates = Array.Empty<bool>();
    private Vector3 homePosition;
    private Quaternion homeRotation;
    private float attackCooldownTimer;
    private float attackWindupTimer;
    private float damageImmunityTimer;
    private float stunTimer;
    private float patrolWaitTimer;
    private float targetRefreshTimer;
    private float safetyTimer;
    private int currentHealth;
    private int patrolIndex;
    private int speedParameterHash;
    private int groundedParameterHash;
    private int stunnedParameterHash;
    private int attackTriggerHash;
    private int specialTriggerHash;
    private int damageTriggerHash;
    private int deathTriggerHash;
    private bool initialized;
    private bool grounded;
    private bool attackPending;
    private bool shuttingDown;
    private bool applicationQuitting;

    #endregion

    #region Events

    public event Action<AIController, AIState, AIState> StateChanged;
    public event Action<AIController, Transform> TargetAcquired;
    public event Action<AIController> TargetLost;
    public event Action<AIController, int> Damaged;
    public event Action<AIController> Attacked;
    public event Action<AIController> SpecialStarted;
    public event Action<AIController> Stunned;
    public event Action<AIController> Recovered;
    public event Action<AIController> Died;

    #endregion

    #region Public API

    public AIState CurrentState => currentState;
    public Transform Target => target;
    public NavMeshAgent Agent => agent;
    public int CurrentHealth => currentHealth;
    public int MaximumHealth => maximumHealth;
    public bool IsInitialized => initialized;
    public bool IsDead => currentState == AIState.Dead;
    public bool IsStunned => currentState == AIState.Stunned;
    public bool IsGrounded => grounded;
    public bool IsInvulnerable => damageImmunityTimer > 0f;
    public Vector3 HomePosition => homePosition;

    public bool SetTarget(Transform newTarget)
    {
        if (!IsValidTarget(newTarget))
            return false;

        target = newTarget;
        TargetAcquired?.Invoke(this, target);
        return true;
    }

    public void ClearTarget()
    {
        if (target == null)
            return;

        target = null;
        TargetLost?.Invoke(this);
    }

    public bool SetState(AIState newState)
    {
        if (!initialized ||
            shuttingDown ||
            applicationQuitting ||
            IsDead ||
            !Enum.IsDefined(typeof(AIState), newState))
        {
            return false;
        }

        ChangeState(newState);
        return true;
    }

    public bool TakeDamage(int damage)
    {
        if (!CanReceiveDamage() || damage <= 0)
            return false;

        int appliedDamage = Mathf.Min(currentHealth, damage);
        currentHealth = Mathf.Max(0, currentHealth - appliedDamage);
        damageImmunityTimer = damageImmunityDuration;

        PlayDamagePresentation();
        Damaged?.Invoke(this, appliedDamage);

        if (currentHealth <= 0)
            Die();

        return true;
    }

    public bool HitByThunderShoot(int damage, float stunDuration)
    {
        bool damageApplied = TakeDamage(damage);

        if (!IsDead && stunDuration > 0f)
            Stun(stunDuration);

        return damageApplied || IsStunned;
    }

    public bool Stun()
    {
        return Stun(defaultStunDuration);
    }

    public bool Stun(float duration)
    {
        if (!initialized ||
            IsDead ||
            !canBeStunned ||
            duration <= 0f)
        {
            return false;
        }

        if (IsStunned)
        {
            stunTimer = refreshStunDuration
                ? Mathf.Max(stunTimer, duration)
                : stunTimer + duration;

            return true;
        }

        stunTimer = duration;
        ChangeState(AIState.Stunned);
        return true;
    }

    public bool Heal(int amount)
    {
        if (!initialized || IsDead || amount <= 0)
            return false;

        int previousHealth = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maximumHealth);
        return currentHealth > previousHealth;
    }

    public bool ResetAI()
    {
        if (shuttingDown || applicationQuitting)
            return false;

        CancelInvoke(nameof(DestroyAI));

        currentHealth = Mathf.Clamp(startingHealth, 1, maximumHealth);
        attackCooldownTimer = 0f;
        attackWindupTimer = 0f;
        damageImmunityTimer = 0f;
        stunTimer = 0f;
        patrolWaitTimer = 0f;
        targetRefreshTimer = 0f;
        attackPending = false;

        RestoreStunnedBehaviours();
        transform.SetPositionAndRotation(homePosition, homeRotation);
        RestoreAgent();
        ChangeState(startingState);
        enabled = true;
        return true;
    }

    public bool ReturnHome()
    {
        if (!initialized || IsDead)
            return false;

        ClearTarget();
        ChangeState(AIState.Returning);
        return true;
    }

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        Initialize();
    }

    protected virtual void OnEnable()
    {
        if (shuttingDown || applicationQuitting)
            return;

        if (!initialized)
            Initialize();

        if (initialized && currentState == AIState.Disabled)
            ChangeState(startingState);
    }

    protected virtual void Update()
    {
        if (!initialized ||
            shuttingDown ||
            applicationQuitting ||
            currentState == AIState.Dead ||
            currentState == AIState.Disabled)
        {
            return;
        }

        UpdateTimers();
        UpdateGrounding();
        UpdateAnimator();

        if (enableRuntimeSafety)
        {
            safetyTimer -= Time.deltaTime;

            if (safetyTimer <= 0f)
            {
                safetyTimer = safetyCheckInterval;

                if (!RunRuntimeSafetyChecks())
                    return;
            }
        }

        UpdateState();
    }

    protected virtual void OnDisable()
    {
        StopAgent();

        if (!shuttingDown &&
            !applicationQuitting &&
            currentState != AIState.Dead)
        {
            ChangeState(AIState.Disabled);
        }
    }

    protected virtual void OnApplicationQuit()
    {
        applicationQuitting = true;
    }

    protected virtual void OnDestroy()
    {
        shuttingDown = true;
        initialized = false;

        CancelInvoke();
        RestoreStunnedBehaviours();

        StateChanged = null;
        TargetAcquired = null;
        TargetLost = null;
        Damaged = null;
        Attacked = null;
        SpecialStarted = null;
        Stunned = null;
        Recovered = null;
        Died = null;

        target = null;
        agent = null;
        groundProbe = null;
        animator = null;
        audioSource = null;

        alertEffect = null;
        attackEffect = null;
        damageEffect = null;
        stunnedEffect = null;
        deathEffect = null;

        currentState = AIState.Disabled;
    }

    protected virtual void OnValidate()
    {
        detectionRange = Mathf.Max(0f, detectionRange);
        loseTargetRange = Mathf.Max(detectionRange, loseTargetRange);
        attackRange = Mathf.Clamp(attackRange, 0f, loseTargetRange);
        movementSpeed = Mathf.Max(0f, movementSpeed);
        angularSpeed = Mathf.Max(0f, angularSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        stoppingDistance = Mathf.Max(0f, stoppingDistance);
        homeArrivalDistance = Mathf.Max(0.01f, homeArrivalDistance);
        maximumDistanceFromHome = Mathf.Max(0f, maximumDistanceFromHome);
        patrolWaitDuration = Mathf.Max(0f, patrolWaitDuration);
        attackCooldown = Mathf.Max(0f, attackCooldown);
        attackWindup = Mathf.Max(0f, attackWindup);
        attackDamage = Mathf.Max(0, attackDamage);
        maximumHealth = Mathf.Max(1, maximumHealth);
        startingHealth = Mathf.Clamp(startingHealth, 1, maximumHealth);
        damageImmunityDuration = Mathf.Max(0f, damageImmunityDuration);
        destructionDelay = Mathf.Max(0f, destructionDelay);
        defaultStunDuration = Mathf.Max(0f, defaultStunDuration);
        groundProbeRadius = Mathf.Max(0.01f, groundProbeRadius);
        groundProbeDistance = Mathf.Max(0.01f, groundProbeDistance);
        targetRefreshInterval = Mathf.Max(0.05f, targetRefreshInterval);
        safetyCheckInterval = Mathf.Max(0.1f, safetyCheckInterval);
        minimumValidScale = Mathf.Max(0.01f, minimumValidScale);
        maximumSafeAgentSpeed = Mathf.Max(1f, maximumSafeAgentSpeed);

        CacheAnimatorHashes();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ResolveReferences();
            ConfigureAgent();
        }
#endif
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.DrawWireSphere(transform.position, loseTargetRange);

        Vector3 home = Application.isPlaying
            ? homePosition
            : transform.position;

        Gizmos.DrawWireSphere(home, homeArrivalDistance);
    }

    #endregion

    #region Initialization

    protected virtual bool Initialize()
    {
        if (initialized)
            return true;

        ResolveReferences();
        ConfigureAgent();
        CacheAnimatorHashes();
        CacheStunnedBehaviourStates();

        homePosition = transform.position;
        homeRotation = transform.rotation;
        currentHealth = Mathf.Clamp(startingHealth, 1, maximumHealth);
        safetyTimer = safetyCheckInterval;
        targetRefreshTimer = 0f;

        if (!ValidateConfiguration())
        {
            initialized = false;
            currentState = AIState.Uninitialized;
            enabled = false;
            return false;
        }

        initialized = true;

        if (resolveTargetAutomatically && target == null)
            ResolveTarget();

        ChangeState(startingState);
        return true;
    }

    protected virtual void ResolveReferences()
    {
        agent ??= GetComponent<NavMeshAgent>();

        animator ??= GetComponent<Animator>();
        animator ??= GetComponentInChildren<Animator>(includeInactive: true);

        audioSource ??= GetComponent<AudioSource>();
        audioSource ??= GetComponentInChildren<AudioSource>(includeInactive: true);

        groundProbe ??= transform;
        behavioursDisabledWhileStunned ??= Array.Empty<Behaviour>();
        patrolPoints ??= Array.Empty<Transform>();
    }

    protected virtual void ConfigureAgent()
    {
        if (agent == null)
            return;

        agent.speed = movementSpeed;
        agent.angularSpeed = angularSpeed;
        agent.acceleration = acceleration;
        agent.stoppingDistance = stoppingDistance;
        agent.updateRotation = updateRotationAutomatically;
        agent.updatePosition = updatePositionAutomatically;
        agent.autoBraking = autoBraking;
    }

    protected virtual void CacheAnimatorHashes()
    {
        speedParameterHash = GetAnimatorHash(speedParameter);
        groundedParameterHash = GetAnimatorHash(groundedParameter);
        stunnedParameterHash = GetAnimatorHash(stunnedParameter);
        attackTriggerHash = GetAnimatorHash(attackTrigger);
        specialTriggerHash = GetAnimatorHash(specialTrigger);
        damageTriggerHash = GetAnimatorHash(damageTrigger);
        deathTriggerHash = GetAnimatorHash(deathTrigger);
    }

    protected virtual void CacheStunnedBehaviourStates()
    {
        originalBehaviourStates =
            new bool[behavioursDisabledWhileStunned.Length];

        for (int index = 0;
             index < behavioursDisabledWhileStunned.Length;
             index++)
        {
            Behaviour targetBehaviour =
                behavioursDisabledWhileStunned[index];

            originalBehaviourStates[index] =
                targetBehaviour != null &&
                targetBehaviour.enabled;
        }
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

    protected virtual void ChangeState(AIState newState)
    {
        if (currentState == newState)
            return;

        AIState previousState = currentState;
        ExitState(previousState);
        currentState = newState;
        EnterState(newState);

        StateChanged?.Invoke(this, previousState, newState);
        LogStateChange($"State changed from {previousState} to {newState}.");
    }

    protected virtual void EnterState(AIState state)
    {
        switch (state)
        {
            case AIState.Idle:
                StopAgent();
                break;

            case AIState.Patrol:
                patrolWaitTimer = 0f;
                break;

            case AIState.Chase:
                PlayAlertPresentation();
                break;

            case AIState.Attack:
                StopAgent();
                BeginAttack();
                break;

            case AIState.Special:
                StopAgent();
                SetAnimatorTrigger(specialTriggerHash);
                SpecialStarted?.Invoke(this);
                break;

            case AIState.Stunned:
                StopAgent();
                ApplyStunnedState();
                break;

            case AIState.InAir:
            case AIState.Dead:
            case AIState.Disabled:
                StopAgent();
                break;
        }
    }

    protected virtual void ExitState(AIState state)
    {
        if (state == AIState.Stunned)
            RestoreStunnedState();
    }

    #endregion

    #region States

    protected virtual void UpdateIdleState()
    {
        if (TryAcquireTarget())
        {
            ChangeState(AIState.Chase);
            return;
        }

        if (HasValidPatrolPoints())
            ChangeState(AIState.Patrol);
    }

    protected virtual void UpdatePatrolState()
    {
        if (TryAcquireTarget())
        {
            ChangeState(AIState.Chase);
            return;
        }

        Transform patrolPoint = GetCurrentPatrolPoint();

        if (patrolPoint == null)
        {
            ChangeState(AIState.Idle);
            return;
        }

        MoveAgentTo(patrolPoint.position);

        if (!HasReachedPosition(
                patrolPoint.position,
                homeArrivalDistance))
        {
            return;
        }

        StopAgent();
        patrolWaitTimer += Time.deltaTime;

        if (patrolWaitTimer < patrolWaitDuration)
            return;

        patrolWaitTimer = 0f;
        AdvancePatrolPoint();
    }

    protected virtual void UpdateChaseState()
    {
        if (!IsValidTarget(target))
        {
            HandleTargetLost();
            return;
        }

        float distance = GetDistanceToTarget();

        if (distance > loseTargetRange)
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

        if (distance <= attackRange)
        {
            ChangeState(AIState.Attack);
            return;
        }

        MoveAgentTo(target.position);
    }

    protected virtual void UpdateAttackState()
    {
        if (!IsValidTarget(target))
        {
            HandleTargetLost();
            return;
        }

        float distance = GetDistanceToTarget();

        if (distance > attackRange)
        {
            attackPending = false;
            ChangeState(AIState.Chase);
            return;
        }

        if (faceTargetWhileAttacking)
            FaceTarget();

        if (attackPending)
        {
            attackWindupTimer -= Time.deltaTime;

            if (attackWindupTimer <= 0f)
            {
                attackPending = false;
                PerformAttack();
            }

            return;
        }

        if (attackCooldownTimer <= 0f)
            BeginAttack();
    }

    protected virtual void UpdateSpecialState()
    {
    }

    protected virtual void UpdateStunnedState()
    {
        stunTimer -= Time.deltaTime;

        if (stunTimer > 0f)
            return;

        ChangeState(
            IsValidTarget(target)
                ? AIState.Chase
                : AIState.Idle);

        Recovered?.Invoke(this);
    }

    protected virtual void UpdateInAirState()
    {
        if (!monitorGrounding || grounded)
        {
            ChangeState(
                IsValidTarget(target)
                    ? AIState.Chase
                    : AIState.Idle);
        }
    }

    protected virtual void UpdateReturningState()
    {
        MoveAgentTo(homePosition);

        if (!HasReachedPosition(
                homePosition,
                homeArrivalDistance))
        {
            return;
        }

        StopAgent();
        transform.rotation = homeRotation;

        ChangeState(
            HasValidPatrolPoints()
                ? AIState.Patrol
                : AIState.Idle);
    }

    #endregion

    #region Targeting

    protected virtual bool TryAcquireTarget()
    {
        if (IsValidTarget(target))
            return IsTargetDetectable(target);

        if (!resolveTargetAutomatically)
            return false;

        targetRefreshTimer -= Time.deltaTime;

        if (targetRefreshTimer > 0f)
            return false;

        targetRefreshTimer = targetRefreshInterval;
        ResolveTarget();

        return
            IsValidTarget(target) &&
            IsTargetDetectable(target);
    }

    protected virtual void ResolveTarget()
    {
        UltimatePlayerMovement[] players =
            FindObjectsByType<UltimatePlayerMovement>(
                FindObjectsInactive.Exclude);

        UltimatePlayerMovement closestPlayer = null;
        float closestSqrDistance = float.PositiveInfinity;

        foreach (UltimatePlayerMovement player in players)
        {
            if (player == null ||
                !player.isActiveAndEnabled)
            {
                continue;
            }

            float sqrDistance =
                (player.transform.position -
                 transform.position)
                .sqrMagnitude;

            if (sqrDistance >= closestSqrDistance)
                continue;

            closestSqrDistance = sqrDistance;
            closestPlayer = player;
        }

        if (closestPlayer != null)
        {
            SetTarget(closestPlayer.transform);
            return;
        }

        if (string.IsNullOrWhiteSpace(playerTag))
            return;

        GameObject taggedTarget =
            GameObject.FindGameObjectWithTag(playerTag);

        if (taggedTarget != null)
            SetTarget(taggedTarget.transform);
    }

    protected virtual bool IsTargetDetectable(
        Transform candidate)
    {
        if (!IsValidTarget(candidate))
            return false;

        Vector3 toTarget =
            candidate.position -
            transform.position;

        float sqrDistance =
            toTarget.sqrMagnitude;

        if (sqrDistance >
            detectionRange *
            detectionRange)
        {
            return false;
        }

        if (fieldOfView < 360f)
        {
            float angle =
                Vector3.Angle(
                    transform.forward,
                    toTarget.normalized);

            if (angle >
                fieldOfView *
                0.5f)
            {
                return false;
            }
        }

        return
            !requireLineOfSight ||
            HasLineOfSight(candidate);
    }

    protected virtual bool HasLineOfSight(
        Transform candidate)
    {
        Vector3 origin =
            transform.position +
            Vector3.up *
            0.5f;

        Vector3 destination =
            candidate.position +
            Vector3.up *
            0.5f;

        Vector3 direction =
            destination -
            origin;

        float distance =
            direction.magnitude;

        if (distance <= 0.001f)
            return true;

        direction /= distance;

        if (!Physics.Raycast(
                origin,
                direction,
                out RaycastHit hit,
                distance,
                lineOfSightBlockingLayers,
                QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        return
            hit.transform == candidate ||
            hit.transform.IsChildOf(candidate) ||
            candidate.IsChildOf(hit.transform);
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
        if (!IsValidTarget(target))
            return float.PositiveInfinity;

        return Vector3.Distance(
            transform.position,
            target.position);
    }

    protected virtual bool IsValidTarget(
        Transform candidate)
    {
        return
            candidate != null &&
            candidate.gameObject.activeInHierarchy &&
            IsFiniteVector(candidate.position);
    }

    #endregion

    #region Navigation

    protected virtual bool MoveAgentTo(
        Vector3 destination)
    {
        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh ||
            !IsFiniteVector(destination))
        {
            return false;
        }

        agent.isStopped = false;
        return agent.SetDestination(destination);
    }

    protected virtual void StopAgent()
    {
        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
        {
            return;
        }

        agent.isStopped = true;
        agent.ResetPath();
    }

    protected virtual void RestoreAgent()
    {
        if (agent == null)
            return;

        if (!agent.enabled &&
            restoreDisabledComponents)
        {
            agent.enabled = true;
        }

        ConfigureAgent();

        if (agent.enabled &&
            agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }

    protected virtual bool HasReachedPosition(
        Vector3 position,
        float distance)
    {
        return
            Vector3.SqrMagnitude(
                transform.position -
                position) <=
            distance *
            distance;
    }

    #endregion

    #region Combat

    protected virtual void BeginAttack()
    {
        if (attackCooldownTimer > 0f ||
            attackPending)
        {
            return;
        }

        attackPending = true;
        attackWindupTimer = attackWindup;

        SetAnimatorTrigger(attackTriggerHash);
        attackEffect?.Play();
        PlaySound(attackSound);
    }

    protected virtual void PerformAttack()
    {
        if (!IsValidTarget(target) ||
            GetDistanceToTarget() >
            attackRange)
        {
            return;
        }

        if (attackDamage > 0 &&
            !string.IsNullOrWhiteSpace(
                damageMessage))
        {
            target.gameObject.SendMessage(
                damageMessage,
                attackDamage,
                SendMessageOptions.DontRequireReceiver);
        }

        attackCooldownTimer = attackCooldown;
        Attacked?.Invoke(this);
    }

    protected virtual void FaceTarget()
    {
        if (!IsValidTarget(target))
            return;

        Vector3 direction =
            target.position -
            transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            return;

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

    #region Stun

    protected virtual void ApplyStunnedState()
    {
        StopAgent();
        DisableStunnedBehaviours();

        if (animator != null &&
            stunnedParameterHash != 0)
        {
            animator.SetBool(
                stunnedParameterHash,
                true);
        }

        stunnedEffect?.Play();
        PlaySound(stunnedSound);
        Stunned?.Invoke(this);
    }

    protected virtual void RestoreStunnedState()
    {
        RestoreStunnedBehaviours();

        if (animator != null &&
            stunnedParameterHash != 0)
        {
            animator.SetBool(
                stunnedParameterHash,
                false);
        }

        if (stunnedEffect != null)
        {
            stunnedEffect.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting);
        }
    }

    protected virtual void DisableStunnedBehaviours()
    {
        for (int index = 0;
             index <
             behavioursDisabledWhileStunned.Length;
             index++)
        {
            Behaviour targetBehaviour =
                behavioursDisabledWhileStunned[index];

            if (targetBehaviour == null ||
                targetBehaviour == this)
            {
                continue;
            }

            targetBehaviour.enabled = false;
        }
    }

    protected virtual void RestoreStunnedBehaviours()
    {
        int count =
            Mathf.Min(
                behavioursDisabledWhileStunned.Length,
                originalBehaviourStates.Length);

        for (int index = 0;
             index < count;
             index++)
        {
            Behaviour targetBehaviour =
                behavioursDisabledWhileStunned[index];

            if (targetBehaviour == null ||
                targetBehaviour == this)
            {
                continue;
            }

            targetBehaviour.enabled =
                originalBehaviourStates[index];
        }
    }

    #endregion

    #region Death

    protected virtual void Die()
    {
        if (IsDead)
            return;

        currentHealth = 0;
        ChangeState(AIState.Dead);
        StopAgent();
        RestoreStunnedBehaviours();

        SetAnimatorTrigger(deathTriggerHash);
        deathEffect?.Play();
        PlaySound(deathSound);
        Died?.Invoke(this);

        if (!destroyOnDeath)
            return;

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
        if (shuttingDown ||
            applicationQuitting)
        {
            return;
        }

        Destroy(gameObject);
    }

    #endregion

    #region Patrol

    protected virtual bool HasValidPatrolPoints()
    {
        if (patrolPoints == null ||
            patrolPoints.Length == 0)
        {
            return false;
        }

        foreach (Transform patrolPoint in patrolPoints)
        {
            if (patrolPoint != null)
                return true;
        }

        return false;
    }

    protected virtual Transform GetCurrentPatrolPoint()
    {
        if (!HasValidPatrolPoints())
            return null;

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
                (patrolIndex + offset) %
                patrolPoints.Length;

            if (patrolPoints[index] != null)
            {
                patrolIndex = index;
                return patrolPoints[index];
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

        patrolIndex = loopPatrol
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
            grounded = true;
            return;
        }

        Vector3 origin =
            groundProbe != null
                ? groundProbe.position
                : transform.position;

        grounded =
            Physics.SphereCast(
                origin +
                Vector3.up *
                0.05f,
                groundProbeRadius,
                Vector3.down,
                out _,
                groundProbeDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

        if (!grounded &&
            currentState != AIState.Stunned &&
            currentState != AIState.Dead &&
            currentState != AIState.InAir)
        {
            ChangeState(AIState.InAir);
        }
    }

    #endregion

    #region Runtime Safety

    protected virtual bool RunRuntimeSafetyChecks()
    {
        if (agent == null)
        {
            ResolveReferences();
            ConfigureAgent();

            if (agent == null)
            {
                EnterSafetyShutdown(
                    "NavMeshAgent could not be restored.");

                return false;
            }
        }

        if (!ValidateTransform())
        {
            EnterSafetyShutdown(
                "Transform contains invalid values.");

            return false;
        }

        if (restoreDisabledComponents &&
            currentState != AIState.Dead)
        {
            if (!agent.enabled)
                agent.enabled = true;

            if (animator != null &&
                !animator.enabled)
            {
                animator.enabled = true;
            }

            if (audioSource != null &&
                !audioSource.enabled)
            {
                audioSource.enabled = true;
            }
        }

        if (agent.enabled &&
            agent.isOnNavMesh &&
            agent.velocity.sqrMagnitude >
            maximumSafeAgentSpeed *
            maximumSafeAgentSpeed)
        {
            agent.velocity =
                Vector3.ClampMagnitude(
                    agent.velocity,
                    maximumSafeAgentSpeed);
        }

        if (currentHealth < 0 ||
            currentHealth > maximumHealth)
        {
            currentHealth =
                Mathf.Clamp(
                    currentHealth,
                    0,
                    maximumHealth);
        }

        if (target != null &&
            !IsValidTarget(target))
        {
            ClearTarget();
        }

        return true;
    }

    protected virtual bool ValidateTransform()
    {
        Vector3 scale =
            transform.lossyScale;

        return
            IsFiniteVector(transform.position) &&
            IsFiniteQuaternion(transform.rotation) &&
            IsFiniteVector(scale) &&
            Mathf.Abs(scale.x) >= minimumValidScale &&
            Mathf.Abs(scale.y) >= minimumValidScale &&
            Mathf.Abs(scale.z) >= minimumValidScale;
    }

    protected virtual void EnterSafetyShutdown(
        string reason)
    {
        StopAgent();
        initialized = false;
        currentState = AIState.Disabled;

        Debug.LogError(
            $"{nameof(AIController)} entered safety shutdown on '{name}': {reason}",
            this);

        enabled = false;
    }

    #endregion

    #region Validation

    protected virtual bool ValidateConfiguration()
    {
        if (agent != null)
            return true;

        Debug.LogError(
            $"{nameof(AIController)} requires a NavMeshAgent on '{name}'.",
            this);

        return false;
    }

    protected virtual bool CanReceiveDamage()
    {
        return
            initialized &&
            !IsDead &&
            !shuttingDown &&
            !applicationQuitting &&
            damageImmunityTimer <= 0f;
    }

    #endregion

    #region Timers And Animation

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

    protected virtual void UpdateAnimator()
    {
        if (animator == null)
            return;

        if (speedParameterHash != 0)
        {
            float speed =
                agent != null &&
                agent.enabled &&
                agent.isOnNavMesh
                    ? agent.velocity.magnitude
                    : 0f;

            animator.SetFloat(
                speedParameterHash,
                speed);
        }

        if (groundedParameterHash != 0)
        {
            animator.SetBool(
                groundedParameterHash,
                grounded);
        }
    }

    protected virtual void PlayDamagePresentation()
    {
        SetAnimatorTrigger(damageTriggerHash);
        damageEffect?.Play();
        PlaySound(damageSound);
    }

    protected virtual void SetAnimatorTrigger(
        int triggerHash)
    {
        if (animator == null ||
            triggerHash == 0)
        {
            return;
        }

        animator.SetTrigger(triggerHash);
    }

    protected virtual void PlayAlertPresentation()
    {
        alertEffect?.Play();
        PlaySound(alertSound);
    }

    protected virtual void PlaySound(
        AudioClip clip)
    {
        if (audioSource == null ||
            clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip);
    }

    #endregion

    #region Helpers

    protected static int GetAnimatorHash(
        string parameterName)
    {
        return
            string.IsNullOrWhiteSpace(
                parameterName)
                ? 0
                : Animator.StringToHash(
                    parameterName);
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

    #region Debug

    protected virtual void LogStateChange(
        string message)
    {
        if (!logStateChanges)
            return;

        Debug.Log(message, this);
    }

    #endregion
}
