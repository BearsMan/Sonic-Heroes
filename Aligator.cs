using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class AligatorEnemy : MonoBehaviour
{
    [Header("Spawn Settings")]
    public Transform spawnPoint;
    public float spawnDelay = 1.0f;

    [Header("Patrol Settings")]
    public Transform[] patrolPoints;
    public float patrolSpeed = 3f;
    private int patrolIndex = 0;

    [Header("Player Detection")]
    public Transform player;
    public float detectionRange = 14f;
    public float aggroRoarDistance = 10f;
    public float biteRange = 2.4f;

    [Header("Movement")]
    public float swimSpeed = 5f;
    public float turnSpeed = 180f;

    [Header("Attacks")]
    public float biteCooldown = 1.2f;
    public Transform biteOrigin;
    public float biteRadius = 1.3f;
    public LayerMask playerLayer;

    [Header("Hit Reaction")]
    public float knockbackForce = 6f;
    public float hitStunDuration = 0.4f;

    [Header("Optimization")]
    [SerializeField]
    [Tooltip("Maximum number of colliders to check for bite overlap (used for non-alloc OverlapSphere)")]
    private int maxOverlapResults = 8;

    private Rigidbody rb;
    private Animator anim;

    private bool isActive = false;
    private bool isAttacking = false;
    private bool isHit = false;
    private bool hasRoared = false;
    private float biteTimer = 0f;

    private Collider[] overlapResults;

    private static readonly int AnimIdle = Animator.StringToHash("Idle");
    private static readonly int AnimSwim = Animator.StringToHash("Swim");
    private static readonly int AnimBite1 = Animator.StringToHash("Bite1");
    private static readonly int AnimBite2 = Animator.StringToHash("Bite2");
    private static readonly int AnimRoar = Animator.StringToHash("Roar");
    private static readonly int AnimHit = Animator.StringToHash("Hit");

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        // allocate buffer for non-alloc overlap checks to avoid heap allocations
        overlapResults = new Collider[Mathf.Max(1, maxOverlapResults)];
    }

    private void Start()
    {
        if (spawnPoint != null)
        {
            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation;
        }

        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        anim.SetTrigger(AnimIdle);
        yield return new WaitForSeconds(spawnDelay);
        isActive = true;
    }

    private void Update()
    {
        if (!isActive || isHit) return;

        if (biteTimer > 0f)
            biteTimer -= Time.deltaTime;

        float distance = Vector3.Distance(transform.position, player.position);

        // Roar when player gets close
        if (!hasRoared && distance <= aggroRoarDistance)
        {
            StartCoroutine(DoRoar());
            return;
        }

        // Attack if close enough
        if (distance <= biteRange && biteTimer <= 0f)
        {
            StartCoroutine(DoBiteCombo());
            return;
        }

        // Chase if detected
        if (!isAttacking && distance <= detectionRange)
        {
            ChasePlayer();
        }
        else
        {
            Patrol();
        }
    }

    private void Patrol()
    {
        if (patrolPoints.Length == 0)
        {
            Idle();
            return;
        }

        anim.SetBool(AnimSwim, true);

        Transform target = patrolPoints[patrolIndex];
        Vector3 direction = (target.position - transform.position).normalized;
        direction.y = 0f;

        RotateToward(direction);
        rb.linearVelocity = transform.forward * patrolSpeed;

        if (Vector3.Distance(transform.position, target.position) < 1f)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }
    }

    private void ChasePlayer()
    {
        anim.SetBool(AnimSwim, true);

        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0f;

        RotateToward(direction);
        rb.linearVelocity = transform.forward * swimSpeed;
    }

    private void RotateToward(Vector3 direction)
    {
        Quaternion targetRot = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRot,
            turnSpeed * Time.deltaTime
        );
    }

    private void Idle()
    {
        anim.SetBool(AnimSwim, false);
        rb.linearVelocity = Vector3.zero;
    }

    private IEnumerator DoRoar()
    {
        hasRoared = true;
        isAttacking = true;

        rb.linearVelocity = Vector3.zero;
        anim.SetTrigger(AnimRoar);

        yield return new WaitForSeconds(1.0f);

        isAttacking = false;
    }

    private IEnumerator DoBiteCombo()
    {
        isAttacking = true;
        biteTimer = biteCooldown;

        rb.linearVelocity = Vector3.zero;

        // Bite 1
        anim.SetTrigger(AnimBite1);
        yield return new WaitForSeconds(0.25f);
        ApplyBiteDamage();

        // Bite 2 (combo finisher)
        anim.SetTrigger(AnimBite2);
        yield return new WaitForSeconds(0.25f);
        ApplyBiteDamage();

        yield return new WaitForSeconds(0.3f);
        isAttacking = false;
    }

    private void ApplyBiteDamage()
    {
        // Use non-alloc version to avoid GC allocations (fixes UNT0028)
        int hitCount = Physics.OverlapSphereNonAlloc(biteOrigin.position, biteRadius, overlapResults, playerLayer.value);
        for (int i = 0; i < hitCount; i++)
        {
            var hit = overlapResults[i];
            // hit.GetComponent<PlayerHealth>()?.TakeDamage(1);
        }
    }

    public void TakeHit(Vector3 hitDirection)
    {
        if (isHit) return;

        StartCoroutine(HitReaction(hitDirection));
    }

    private IEnumerator HitReaction(Vector3 hitDirection)
    {
        isHit = true;
        isAttacking = false;

        anim.SetTrigger(AnimHit);

        rb.linearVelocity = Vector3.zero;
        rb.AddForce(hitDirection.normalized * knockbackForce, ForceMode.VelocityChange);

        yield return new WaitForSeconds(hitStunDuration);

        isHit = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (biteOrigin != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(biteOrigin.position, biteRadius);
        }
    }
}
