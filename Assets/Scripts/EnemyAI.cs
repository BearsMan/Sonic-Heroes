using UnityEngine;

public class EnemyAI : AIController
{
    #region Enemy State

    private enum EnemyState
    {
        Idle,
        Patrolling,
        Chasing,
        Attacking,
        Staggered,
        Returning,
        Dead
    }

    #endregion

    #region Detection

    [Header("Detection")]

    [SerializeField, Min(0.1f)]
    private float detectionRadius = 15f;

    [SerializeField, Min(0.1f)]
    private float loseTargetRadius = 25f;

    [SerializeField]
    private LayerMask playerLayers = ~0;

    #endregion

    #region Combat

    [Header("Combat")]

    [SerializeField, Min(0.1f)]
    private float attackRange = 2.5f;

    [SerializeField, Min(1)]
    private int attackDamage = 10;

    [SerializeField, Min(0f)]
    private float attackCooldown = 1.5f;

    #endregion

    #region Stagger

    [Header("Stagger")]

    [SerializeField]
    private bool canBeStaggered = true;

    [SerializeField, Min(0f)]
    private float staggerDuration = 0.5f;

    #endregion

    #region Home

    [Header("Home")]

    [SerializeField, Min(0f)]
    private float maximumRoamDistance = 30f;

    [SerializeField, Min(0.1f)]
    private float homeArrivalDistance = 1f;

    [SerializeField]
    private bool returnHomeWhenTargetLost = true;

    #endregion

    #region Animation

    [Header("Animation")]

    [SerializeField]
    private Animator animator;

    [SerializeField]
    private string idleAnimation = "Idle";

    [SerializeField]
    private string moveAnimation = "Move";

    [SerializeField]
    private string attackAnimation = "Attack";

    [SerializeField]
    private string hurtAnimation = "Hurt";

    [SerializeField]
    private string deathAnimation = "Death";

    #endregion

    #region Audio

    [Header("Audio")]

    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip detectionSound;

    [SerializeField]
    private AudioClip attackSound;

    [SerializeField]
    private AudioClip hurtSound;

    [SerializeField]
    private AudioClip deathSound;

    #endregion

    #region Effects

    [Header("Effects")]

    [SerializeField]
    private GameObject hitEffect;

    [SerializeField]
    private GameObject deathEffect;

    [SerializeField, Min(0f)]
    private float effectLifetime = 3f;

    #endregion

    #region Runtime State

    [SerializeField]
    private EnemyState enemyState =
        EnemyState.Idle;

    private Transform detectedTarget;

    private float attackTimer;
    private float staggerTimer;

    private bool targetDetected;
    private bool deathHandled;

    #endregion

    #region Properties

    public bool HasTarget =>
        detectedTarget != null;

    public bool IsStaggered =>
        enemyState ==
        EnemyState.Staggered;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        ResolveReferences();

        enemyState =
            EnemyState.Idle;
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        ResolveReferences();

        attackTimer =
            0f;

        staggerTimer =
            0f;

        targetDetected =
            false;

        deathHandled =
            false;

        enemyState =
            EnemyState.Idle;
    }

    protected override void Update()
    {
        base.Update();

        if (!IsInitialized)
        {
            return;
        }

        if (IsDead)
        {
            HandleDeath();

            return;
        }

        UpdateTimers();

        if (enemyState ==
            EnemyState.Staggered)
        {
            UpdateStagger();

            return;
        }

        UpdateTargetDetection();
        UpdateEnemyBehavior();
    }

    protected override void OnDisable()
    {
        detectedTarget =
            null;

        targetDetected =
            false;

        base.OnDisable();
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        detectionRadius =
            Mathf.Max(
                0.1f,
                detectionRadius);

        loseTargetRadius =
            Mathf.Max(
                detectionRadius,
                loseTargetRadius);

        attackRange =
            Mathf.Max(
                0.1f,
                attackRange);

        attackDamage =
            Mathf.Max(
                1,
                attackDamage);

        attackCooldown =
            Mathf.Max(
                0f,
                attackCooldown);

        staggerDuration =
            Mathf.Max(
                0f,
                staggerDuration);

        maximumRoamDistance =
            Mathf.Max(
                0f,
                maximumRoamDistance);

        homeArrivalDistance =
            Mathf.Max(
                0.1f,
                homeArrivalDistance);

        effectLifetime =
            Mathf.Max(
                0f,
                effectLifetime);
    }

    #endregion

    #region Target Detection

    private void UpdateTargetDetection()
    {
        if (detectedTarget == null)
        {
            FindPlayerTarget();

            return;
        }

        if (!detectedTarget.gameObject.activeInHierarchy ||
            !IsFiniteVector(
                detectedTarget.position))
        {
            LoseTarget();

            return;
        }

        float distanceSquared =
            (
                detectedTarget.position -
                transform.position
            ).sqrMagnitude;

        float loseDistanceSquared =
            loseTargetRadius *
            loseTargetRadius;

        if (!float.IsFinite(
                distanceSquared) ||
            distanceSquared >
                loseDistanceSquared)
        {
            LoseTarget();
        }
    }

    private void FindPlayerTarget()
    {
        Collider[] results =
            Physics.OverlapSphere(
                transform.position,
                detectionRadius,
                playerLayers,
                QueryTriggerInteraction.Ignore);

        Transform closestTarget =
            null;

        float closestDistance =
            float.PositiveInfinity;

        foreach (Collider candidate
            in results)
        {
            if (candidate == null)
            {
                continue;
            }

            UltimatePlayerMovement player =
                candidate
                    .GetComponentInParent<
                        UltimatePlayerMovement>();

            if (player == null)
            {
                continue;
            }

            Transform playerTransform =
                player.transform;

            float distance =
                (
                    playerTransform.position -
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

            closestTarget =
                playerTransform;
        }

        if (closestTarget == null)
        {
            return;
        }

        detectedTarget =
            closestTarget;

        targetDetected =
            true;

        enemyState =
            EnemyState.Chasing;

        SetState(
            AIState.Chase);

        PlaySound(
            detectionSound);
    }

    private void LoseTarget()
    {
        detectedTarget =
            null;

        targetDetected =
            false;

        if (returnHomeWhenTargetLost)
        {
            enemyState =
                EnemyState.Returning;

            SetState(
                AIState.Returning);
        }
        else
        {
            enemyState =
                EnemyState.Idle;

            SetState(
                AIState.Idle);
        }
    }

    #endregion

    #region Behavior

    private void UpdateEnemyBehavior()
    {
        if (detectedTarget == null)
        {
            UpdateWithoutTarget();

            return;
        }

        Vector3 difference =
            detectedTarget.position -
            transform.position;

        float distance =
            difference.magnitude;

        if (!float.IsFinite(
                distance))
        {
            LoseTarget();

            return;
        }

        if (distance <=
            attackRange)
        {
            enemyState =
                EnemyState.Attacking;

            SetState(
                AIState.Attack);

            TryAttack();

            return;
        }

        enemyState =
            EnemyState.Chasing;

        SetState(
            AIState.Chase);
    }

    private void UpdateWithoutTarget()
    {
        if (enemyState ==
            EnemyState.Returning)
        {
            UpdateReturnHome();

            return;
        }

        enemyState =
            EnemyState.Idle;

        SetState(
            AIState.Idle);

        PlayAnimation(
            idleAnimation);
    }

    #endregion

    #region Base State Overrides

    protected override void UpdateIdleState()
    {
        if (detectedTarget != null)
        {
            SetState(
                AIState.Chase);

            return;
        }

        base.UpdateIdleState();

        PlayAnimation(
            idleAnimation);
    }

    protected override void UpdatePatrolState()
    {
        if (detectedTarget != null)
        {
            SetState(
                AIState.Chase);

            return;
        }

        enemyState =
            EnemyState.Patrolling;

        base.UpdatePatrolState();

        PlayAnimation(
            moveAnimation);
    }

    protected override void UpdateChaseState()
    {
        if (detectedTarget == null)
        {
            LoseTarget();

            return;
        }

        if (IsTooFarFromHome())
        {
            LoseTarget();

            return;
        }

        enemyState =
            EnemyState.Chasing;

        base.UpdateChaseState();

        PlayAnimation(
            moveAnimation);
    }

    protected override void UpdateAttackState()
    {
        if (detectedTarget == null)
        {
            LoseTarget();

            return;
        }

        float distance =
            Vector3.Distance(
                transform.position,
                detectedTarget.position);

        if (!float.IsFinite(
                distance))
        {
            LoseTarget();

            return;
        }

        if (distance >
            attackRange)
        {
            enemyState =
                EnemyState.Chasing;

            SetState(
                AIState.Chase);

            return;
        }

        StopAgent();

        TryAttack();
    }

    protected override void UpdateReturningState()
    {
        UpdateReturnHome();
    }

    #endregion

    #region Attack

    private void TryAttack()
    {
        if (attackTimer > 0f ||
            detectedTarget == null ||
            IsDead)
        {
            return;
        }

        StopAgent();

        FaceTarget(
            detectedTarget);

        PlayAnimation(
            attackAnimation);

        PlaySound(
            attackSound);

        Health targetHealth =
            ResolveTargetHealth(
                detectedTarget);

        if (targetHealth != null &&
            !targetHealth.dead)
        {
            targetHealth.TakeDamage(
                attackDamage);
        }

        attackTimer =
            attackCooldown;
    }

    private static Health ResolveTargetHealth(
        Transform target)
    {
        if (target == null)
        {
            return null;
        }

        Health health =
            target.GetComponent<Health>();

        health ??=
            target.GetComponentInParent<Health>();

        health ??=
            target.GetComponentInChildren<Health>(
                includeInactive: true);

        return health;
    }

    #endregion

    #region Damage And Stagger

    public void NotifyDamaged(
        int damage)
    {
        if (IsDead ||
            damage <= 0)
        {
            return;
        }

        PlayAnimation(
            hurtAnimation);

        PlaySound(
            hurtSound);

        SpawnEffect(
            hitEffect,
            transform.position);

        if (!canBeStaggered ||
            staggerDuration <= 0f)
        {
            return;
        }

        BeginStagger();
    }

    private void BeginStagger()
    {
        enemyState =
            EnemyState.Staggered;

        staggerTimer =
            staggerDuration;

        StopAgent();

        PlayAnimation(
            hurtAnimation);
    }

    private void UpdateStagger()
    {
        staggerTimer =
            Mathf.Max(
                0f,
                staggerTimer -
                Time.deltaTime);

        if (staggerTimer > 0f)
        {
            return;
        }

        if (detectedTarget != null)
        {
            enemyState =
                EnemyState.Chasing;

            SetState(
                AIState.Chase);
        }
        else
        {
            enemyState =
                EnemyState.Idle;

            SetState(
                AIState.Idle);
        }
    }

    #endregion

    #region Returning Home

    private void UpdateReturnHome()
    {
        Vector3 homePosition =
            HomePosition;

        if (!IsFiniteVector(
                homePosition))
        {
            StopAgent();

            enemyState =
                EnemyState.Idle;

            SetState(
                AIState.Idle);

            return;
        }

        float distance =
            Vector3.Distance(
                transform.position,
                homePosition);

        if (!float.IsFinite(
                distance))
        {
            return;
        }

        if (distance <=
            homeArrivalDistance)
        {
            StopAgent();

            enemyState =
                EnemyState.Idle;

            SetState(
                AIState.Idle);

            return;
        }

        MoveAgentTo(
            homePosition);

        PlayAnimation(
            moveAnimation);
    }

    private bool IsTooFarFromHome()
    {
        if (maximumRoamDistance <= 0f)
        {
            return false;
        }

        Vector3 difference =
            transform.position -
            HomePosition;

        float distanceSquared =
            difference.sqrMagnitude;

        return
            float.IsFinite(
                distanceSquared) &&
            distanceSquared >
                maximumRoamDistance *
                maximumRoamDistance;
    }

    #endregion

    #region Death

    private void HandleDeath()
    {
        if (deathHandled)
        {
            return;
        }

        deathHandled =
            true;

        enemyState =
            EnemyState.Dead;

        detectedTarget =
            null;

        targetDetected =
            false;

        StopAgent();

        PlayAnimation(
            deathAnimation);

        PlaySound(
            deathSound);

        SpawnEffect(
            deathEffect,
            transform.position);
    }

    #endregion

    #region Reset

    public void ResetEnemy()
    {
        ResetAI();

        detectedTarget =
            null;

        targetDetected =
            false;

        attackTimer =
            0f;

        staggerTimer =
            0f;

        deathHandled =
            false;

        enemyState =
            EnemyState.Idle;

        SetState(
            AIState.Idle);
    }

    #endregion

    #region Timers

    private void UpdateTimers()
    {
        if (attackTimer > 0f)
        {
            attackTimer =
                Mathf.Max(
                    0f,
                    attackTimer -
                        Time.deltaTime);
        }
    }

    #endregion

    #region Rotation

    private void FaceTarget(
        Transform target)
    {
        if (target == null)
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

        transform.rotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);
    }

    #endregion

    #region References

    private void ResolveReferences()
    {
        animator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        audioSource ??=
            GetComponent<AudioSource>();

        audioSource ??=
            GetComponentInChildren<AudioSource>(
                includeInactive: true);
    }

    #endregion

    #region Animation

    private void PlayAnimation(
        string animationName)
    {
        if (animator == null ||
            !animator.isActiveAndEnabled ||
            animator.runtimeAnimatorController ==
                null ||
            string.IsNullOrWhiteSpace(
                animationName))
        {
            return;
        }

        animator.Play(
            animationName);
    }

    #endregion

    #region Audio

    private void PlaySound(
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

    private void SpawnEffect(
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

    #region Validation

    private static bool IsFiniteVector(
        Vector3 value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }

    #endregion

    #region Gizmos

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.DrawWireSphere(
            transform.position,
            detectionRadius);

        Gizmos.DrawWireSphere(
            transform.position,
            attackRange);

        if (maximumRoamDistance > 0f)
        {
            Gizmos.DrawWireSphere(
                Application.isPlaying
                    ? HomePosition
                    : transform.position,
                maximumRoamDistance);
        }
    }

    #endregion
}