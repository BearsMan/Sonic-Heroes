using UnityEngine;

public class BossAI : MonoBehaviour
{
    private enum BossState
    {
        Idle,
        Chasing,
        Attacking,
        Hurt,
        Dead
    }

    private enum BossPhase
    {
        PhaseOne,
        PhaseTwo,
        PhaseThree
    }

    #region Animator Hashes

    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private static readonly int AttackHash =
        Animator.StringToHash("Attack");

    private static readonly int HurtHash =
        Animator.StringToHash("Hurt");

    private static readonly int DieHash =
        Animator.StringToHash("Die");

    #endregion

    #region Health

    [Header("Health")]

    [SerializeField, Min(1f)]
    private float maxHealth = 100f;

    #endregion

    #region Movement

    [Header("Movement")]

    [SerializeField, Min(0f)]
    private float moveSpeed = 5f;

    [SerializeField, Min(0f)]
    private float rotationSpeed = 360f;

    [SerializeField, Min(0f)]
    private float stoppingDistance = 3f;

    #endregion

    #region Attack

    [Header("Attack")]

    [SerializeField, Min(0.1f)]
    private float attackRange = 3f;

    [SerializeField, Min(1)]
    private int attackDamage = 10;

    [SerializeField, Min(0f)]
    private float attackCooldown = 2f;

    #endregion

    #region Phases

    [Header("Boss Phases")]

    [SerializeField, Range(0f, 1f)]
    private float phaseTwoHealth = 0.66f;

    [SerializeField, Range(0f, 1f)]
    private float phaseThreeHealth = 0.33f;

    [SerializeField, Min(1f)]
    private float phaseTwoSpeedMultiplier = 1.25f;

    [SerializeField, Min(1f)]
    private float phaseThreeSpeedMultiplier = 1.5f;

    [SerializeField, Min(1f)]
    private float phaseTwoAttackMultiplier = 1.25f;

    [SerializeField, Min(1f)]
    private float phaseThreeAttackMultiplier = 1.5f;

    #endregion

    #region References

    [Header("References")]

    [SerializeField]
    private Transform target;

    [SerializeField]
    private Animator animator;

    #endregion

    #region Debug

    [Header("Debug")]

    [SerializeField]
    private bool drawRanges = true;

    #endregion

    #region Runtime State

    private float currentHealth;
    private float attackTimer;

    private BossState currentState;
    private BossPhase currentPhase;

    private TeamSetup teamSetup;

    #endregion

    #region Properties

    public float CurrentHealth =>
        currentHealth;

    public float MaxHealth =>
        maxHealth;

    public bool IsDead =>
        currentState == BossState.Dead;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        currentHealth =
            maxHealth;

        currentState =
            BossState.Idle;

        currentPhase =
            BossPhase.PhaseOne;

        animator ??=
            GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        FindTeamLeader();
    }

    private void Update()
    {
        if (currentState ==
            BossState.Dead)
        {
            return;
        }

        UpdateAttackCooldown();
        UpdatePhase();

        if (target == null)
        {
            FindTeamLeader();

            if (target == null)
            {
                currentState =
                    BossState.Idle;

                UpdateAnimator();

                return;
            }
        }

        UpdateBehavior();
        UpdateAnimator();
    }

    private void OnValidate()
    {
        maxHealth =
            Mathf.Max(
                1f,
                maxHealth);

        moveSpeed =
            Mathf.Max(
                0f,
                moveSpeed);

        rotationSpeed =
            Mathf.Max(
                0f,
                rotationSpeed);

        stoppingDistance =
            Mathf.Max(
                0f,
                stoppingDistance);

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

        phaseTwoHealth =
            Mathf.Clamp01(
                phaseTwoHealth);

        phaseThreeHealth =
            Mathf.Clamp01(
                phaseThreeHealth);

        if (phaseThreeHealth >
            phaseTwoHealth)
        {
            phaseThreeHealth =
                phaseTwoHealth;
        }
    }

    #endregion

    #region Team Targeting

    private void FindTeamLeader()
    {
        if (teamSetup == null)
        {
            teamSetup =
                Object.FindAnyObjectByType<TeamSetup>();
        }

        if (teamSetup == null ||
            teamSetup.player == null)
        {
            target =
                null;

            return;
        }

        target =
            teamSetup.player;
    }

    #endregion

    #region Behavior

    private void UpdateBehavior()
    {
        Vector3 difference =
            target.position -
            transform.position;

        difference.y =
            0f;

        float distance =
            difference.magnitude;

        if (!float.IsFinite(distance))
        {
            currentState =
                BossState.Idle;

            return;
        }

        RotateTowardsTarget(
            difference);

        if (distance <=
            attackRange)
        {
            currentState =
                BossState.Attacking;

            TryAttack();

            return;
        }

        if (distance <=
            stoppingDistance)
        {
            currentState =
                BossState.Idle;

            return;
        }

        MoveTowardsTarget(
            difference);
    }

    #endregion

    #region Movement

    private void MoveTowardsTarget(
        Vector3 direction)
    {
        if (direction.sqrMagnitude <=
            0.0001f)
        {
            currentState =
                BossState.Idle;

            return;
        }

        direction.Normalize();

        currentState =
            BossState.Chasing;

        float speed =
            GetCurrentMoveSpeed();

        Vector3 movement =
            direction *
            speed *
            Time.deltaTime;

        if (!IsFiniteVector(
                movement))
        {
            return;
        }

        transform.position +=
            movement;
    }

    private void RotateTowardsTarget(
        Vector3 direction)
    {
        if (direction.sqrMagnitude <=
            0.0001f)
        {
            return;
        }

        direction.Normalize();

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction,
                Vector3.up);

        transform.rotation =
            Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed *
                Time.deltaTime);
    }

    private float GetCurrentMoveSpeed()
    {
        return currentPhase switch
        {
            BossPhase.PhaseTwo =>
                moveSpeed *
                phaseTwoSpeedMultiplier,

            BossPhase.PhaseThree =>
                moveSpeed *
                phaseThreeSpeedMultiplier,

            _ =>
                moveSpeed
        };
    }

    #endregion

    #region Attack

    private void TryAttack()
    {
        if (attackTimer > 0f ||
            target == null)
        {
            return;
        }

        Health targetHealth =
            FindTargetHealth();

        if (targetHealth == null ||
            targetHealth.dead)
        {
            return;
        }

        int damage =
            GetCurrentAttackDamage();

        targetHealth.TakeDamage(
            damage);

        attackTimer =
            attackCooldown;

        TriggerAnimator(
            AttackHash);
    }

    private Health FindTargetHealth()
    {
        if (target == null)
        {
            return null;
        }

        Health health =
            target.GetComponent<Health>();

        health ??=
            target.GetComponentInChildren<Health>();

        health ??=
            target.GetComponentInParent<Health>();

        return health;
    }

    private int GetCurrentAttackDamage()
    {
        float multiplier =
            currentPhase switch
            {
                BossPhase.PhaseTwo =>
                    phaseTwoAttackMultiplier,

                BossPhase.PhaseThree =>
                    phaseThreeAttackMultiplier,

                _ =>
                    1f
            };

        return Mathf.Max(
            1,
            Mathf.RoundToInt(
                attackDamage *
                multiplier));
    }

    private void UpdateAttackCooldown()
    {
        if (attackTimer <= 0f)
        {
            return;
        }

        attackTimer =
            Mathf.Max(
                0f,
                attackTimer -
                Time.deltaTime);
    }

    #endregion

    #region Damage

    public void TakeDamage(
        float damage)
    {
        if (currentState ==
                BossState.Dead ||
            damage <= 0f ||
            !float.IsFinite(damage))
        {
            return;
        }

        currentHealth =
            Mathf.Max(
                0f,
                currentHealth -
                damage);

        if (currentHealth <= 0f)
        {
            Die();

            return;
        }

        currentState =
            BossState.Hurt;

        TriggerAnimator(
            HurtHash);
    }

    private void Die()
    {
        if (currentState ==
            BossState.Dead)
        {
            return;
        }

        currentState =
            BossState.Dead;

        TriggerAnimator(
            DieHash);

        Destroy(
            gameObject,
            1f);
    }

    #endregion

    #region Boss Phases

    private void UpdatePhase()
    {
        if (maxHealth <= 0f)
        {
            return;
        }

        float healthPercent =
            currentHealth /
            maxHealth;

        BossPhase newPhase;

        if (healthPercent <=
            phaseThreeHealth)
        {
            newPhase =
                BossPhase.PhaseThree;
        }
        else if (healthPercent <=
                 phaseTwoHealth)
        {
            newPhase =
                BossPhase.PhaseTwo;
        }
        else
        {
            newPhase =
                BossPhase.PhaseOne;
        }

        if (newPhase ==
            currentPhase)
        {
            return;
        }

        currentPhase =
            newPhase;
    }

    #endregion

    #region Animation

    private void UpdateAnimator()
    {
        if (!HasValidAnimator())
        {
            return;
        }

        if (HasParameter(
                SpeedHash))
        {
            float speed =
                currentState ==
                BossState.Chasing
                    ? GetCurrentMoveSpeed()
                    : 0f;

            animator.SetFloat(
                SpeedHash,
                speed);
        }
    }

    private void TriggerAnimator(
        int parameterHash)
    {
        if (!HasValidAnimator() ||
            !HasParameter(
                parameterHash))
        {
            return;
        }

        animator.SetTrigger(
            parameterHash);
    }

    private bool HasValidAnimator()
    {
        return
            animator != null &&
            animator.isActiveAndEnabled &&
            animator.runtimeAnimatorController !=
                null;
    }

    private bool HasParameter(
        int parameterHash)
    {
        if (!HasValidAnimator())
        {
            return false;
        }

        foreach (
            AnimatorControllerParameter parameter
            in animator.parameters)
        {
            if (parameter.nameHash ==
                parameterHash)
            {
                return true;
            }
        }

        return false;
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

    private void OnDrawGizmosSelected()
    {
        if (!drawRanges)
        {
            return;
        }

        Gizmos.DrawWireSphere(
            transform.position,
            stoppingDistance);

        Gizmos.DrawWireSphere(
            transform.position,
            attackRange);
    }

    #endregion
}