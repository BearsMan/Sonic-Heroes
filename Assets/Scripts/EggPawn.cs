using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class EggPawn : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;

    [Header("Detection")]
    [SerializeField, Min(0f)] private float detectionRange = 30f;
    [SerializeField, Min(0f)] private float attackRange = 3f;

    [Header("Attack")]
    [SerializeField, Min(0f)] private float attackDelay = 2f;

    private static readonly int SleepHash = Animator.StringToHash("Sleep");

    private static readonly int PunchHash = Animator.StringToHash("Punch");

    private Health health;
    private Vector3 origin;
    private float attackTimer;

    private void Awake()
    {
        health = GetComponent<Health>();

        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        origin = transform.position;
    }

    private void Start()
    {
        FindPlayer();

        if (animator != null)
        {
            animator.SetBool(SleepHash, true);
        }
    }

    private void Update()
    {
        if (health.dead)
        {
            StopMoving();
            return;
        }

        if (animator != null &&
            animator.GetBool(SleepHash))
        {
            StopMoving();
            return;
        }

        if (player == null)
        {
            FindPlayer();
            return;
        }

        float distanceFromOrigin =
            (player.position - origin).sqrMagnitude;

        if (distanceFromOrigin <=
            detectionRange * detectionRange)
        {
            ChasePlayer();
            AttackPlayer();
            return;
        }

        ReturnToOrigin();
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
            return;
        }

        Debug.LogWarning("EggPawn could not find an object tagged Player.", this);
    }

    private void ChasePlayer()
    {
        SetDestination(player.position);
        LookAt(player);
    }

    private void ReturnToOrigin()
    {
        attackTimer = 0f;

        if (animator != null)
        {
            animator.ResetTrigger(PunchHash);
        }

        SetDestination(origin);
    }

    private void AttackPlayer()
    {
        float distanceToPlayer = (player.position - transform.position).sqrMagnitude;

        if (distanceToPlayer > attackRange * attackRange)
        {
            attackTimer = 0f;
            return;
        }

        attackTimer += Time.deltaTime;

        if (attackTimer < attackDelay)
            return;

        attackTimer = 0f;

        if (animator != null)
        {
            animator.SetTrigger(PunchHash);
        }
    }

    public void DamagePlayer()
    {
        Debug.Log("Damage Player", this);
    }

    private void LookAt(Transform target)
    {
        if (target == null)
        {
            return;
        }

        Vector3 direction =
            target.position - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction.normalized);
    }

    private void SetDestination(Vector3 destination)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(destination);
    }

    private void StopMoving()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        agent.isStopped = true;
        agent.ResetPath();
    }

    private void OnValidate()
    {
        detectionRange = Mathf.Max(0f, detectionRange);
        attackRange = Mathf.Max(0f, attackRange);
        attackDelay = Mathf.Max(0f, attackDelay);
    }
}