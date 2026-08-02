using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CapsuleCollider))]
public sealed class FollowerNavigation : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private static readonly int GroundedHash = Animator.StringToHash("Grounded");

    [Header("Follow Target")]
    [SerializeField] private Transform target;

    [Header("Movement")]
    [SerializeField, Min(0f)]
    private float stoppingDistance = 0.1f;

    [SerializeField, Min(0f)]
    private float rotationSpeed = 540f;

    [SerializeField, Min(0f)]
    private float teleportDistance = 20f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundMask = ~0;

    [SerializeField, Min(0.01f)]
    private float groundCheckRadius = 0.35f;

    [SerializeField, Min(0f)]
    private float groundCheckDistance = 0.1f;

    [Header("NavMesh")]
    [SerializeField, Min(0.1f)]
    private float navMeshSearchRadius = 5f;

    [SerializeField]
    private bool warpToNearestNavMeshOnSetup = true;

    private NavMeshAgent agent;
    private Animator animator;
    private Rigidbody body;
    private CapsuleCollider capsule;

    private bool isGrounded;
    private bool isInitialized;
    private bool isExternallyMoving;

    public Transform Target => target;
    public bool IsGrounded => isGrounded;
    public bool IsInitialized => isInitialized;
    public bool IsFollowing =>
        isInitialized &&
        target != null &&
        agent != null &&
        agent.enabled &&
        agent.isOnNavMesh;

    private void Awake()
    {
        CacheComponents();
        ResolveReferences();
        ConfigureComponents();
        ConfigureAgent();
    }

    private void Start()
    {
        if (!ValidateConfiguration())
        {
            enabled = false;
            return;
        }
    }

    private void Update()
    {
        if (!isInitialized)
            return;

        UpdateGroundedState();
        UpdateAnimator();

        if (isExternallyMoving)
            return;

        if (target == null)
        {
            StopAgent();
            return;
        }

        EnsureAgentState();

        if (!agent.enabled ||
            !agent.isOnNavMesh)
        {
            return;
        }

        HandleTeleportIfTooFar();
        FollowTarget();
        RotateTowardsMovement();
    }

    private void OnEnable()
    {
        CacheComponents();

        if (isInitialized)
        {
            EnableAgent();
        }
    }

    private void CleanupRuntimeState()
    {
        StopAgent();

        isExternallyMoving = false;
    }

    private void CleanupDestroyedState()
    {
        CleanupRuntimeState();

        DisableAgent();

        isInitialized = false;
        target = null;
    }

    private void OnDisable()
    {
        CleanupRuntimeState();
    }

    private void OnDestroy()
    {
        CleanupDestroyedState();
    }

    public bool Initialize(Transform followTarget)
    {
        if (followTarget == null)
        {
            Debug.LogError($"FollowerNavigation on '{name}' received no follow target.", this);

            isInitialized = false;
            return false;
        }

        target = followTarget;

        CacheComponents();
        ConfigureComponents();

        if (!ValidateConfiguration())
        {
            isInitialized = false;

            Debug.LogError($"FollowerNavigation failed to initialize on '{name}'.",
                this);

            return false;
        }

        if (warpToNearestNavMeshOnSetup && !TryWarpToNearestNavMesh())
        {
            Debug.LogWarning($"FollowerNavigation on '{name}' could not reach the NavMesh.", this);
        }

        isInitialized = true;
        return true;
    }

    private void ResolveReferences()
    {
        // Reserved for future automatic reference resolution.
    }

    public void SetFollowTarget(Transform followTarget)
    {
        target = followTarget;

        if (target == null)
            StopAgent();
    }

    public void ClearTarget()
    {
        SetFollowTarget(null);
    }

    public void BeginExternalMovement()
    {
        isExternallyMoving = true;
        DisableAgent();
    }

    public void EndExternalMovement()
    {
        isExternallyMoving = false;
        EnableAgent();
    }

    public void Jump(
        Vector3 velocity)
    {
        BeginExternalMovement();

        body.linearVelocity =
            velocity;
    }

    public void Launch(
        Vector3 velocity)
    {
        Jump(velocity);
    }

    public void EnableAgent()
    {
        if (agent == null)
            return;

        if (!agent.enabled)
            agent.enabled = true;

        if (agent.isOnNavMesh)
            return;

        TryWarpToNearestNavMesh();
    }

    public void DisableAgent()
    {
        if (agent == null)
            return;

        if (agent.enabled)
            agent.enabled = false;
    }

    public bool CanReach(
        Vector3 destination)
    {
        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
        {
            return false;
        }

        NavMeshPath path =
            new();

        bool pathCalculated =
            agent.CalculatePath(
                destination,
                path);

        return
            pathCalculated &&
            path.status ==
            NavMeshPathStatus.PathComplete;
    }

    private void CacheComponents()
    {
        agent ??=
            GetComponent<NavMeshAgent>();

        animator ??=
            GetComponentInChildren<Animator>();

        body ??=
            GetComponent<Rigidbody>();

        capsule ??=
            GetComponent<CapsuleCollider>();
    }

    private void ConfigureComponents()
    {
        if (body != null)
        {
            body.constraints =
                RigidbodyConstraints.FreezeRotation;
        }

        if (agent != null)
        {
            agent.updateRotation = false;
        }
    }

    private void ConfigureAgent()
    {
        if (agent == null)
            return;

        agent.stoppingDistance =
            Mathf.Max(0f, stoppingDistance);

        agent.angularSpeed =
            Mathf.Max(0f, rotationSpeed);

        agent.autoBraking = true;
    }

    private void UpdateGroundedState()
    {
        if (capsule == null)
        {
            isGrounded = false;
            return;
        }

        Vector3 checkPosition =
            capsule.bounds.center -
            Vector3.up *
            (capsule.bounds.extents.y -
             groundCheckDistance);

        isGrounded =
            Physics.CheckSphere(
                checkPosition,
                groundCheckRadius,
                groundMask,
                QueryTriggerInteraction.Ignore);

        if (isGrounded &&
            isExternallyMoving &&
            body.linearVelocity.y <= 0f)
        {
            isExternallyMoving = false;
            EnableAgent();
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        float speed =
            agent != null &&
            agent.enabled &&
            agent.isOnNavMesh
                ? agent.velocity.magnitude
                : body != null
                    ? body.linearVelocity.magnitude
                    : 0f;

        if (HasParameter(SpeedHash))
        {
            animator.SetFloat(
                SpeedHash,
                speed);
        }

        if (HasParameter(GroundedHash))
        {
            animator.SetBool(GroundedHash, isGrounded);
        }
    }

    private void EnsureAgentState()
    {
        if (agent == null)
        {
            return;
        }

        EnableAgent();

        if (agent.enabled &&
            !agent.isOnNavMesh)
        {
            TryWarpToNearestNavMesh();
        }
    }

    private void FollowTarget()
    {
        if (target == null ||
            agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
        {
            return;
        }

        agent.SetDestination(target.position);
    }


    private void RotateTowardsMovement()
    {
        if (agent == null ||
    !agent.enabled ||
    !agent.isOnNavMesh)
        {
            return;
        }

        Vector3 direction =
            agent.velocity;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.01f)
        {
            if (target != null)
            {
                transform.rotation =
                    Quaternion.RotateTowards(
                        transform.rotation,
                        target.rotation,
                        rotationSpeed * Time.deltaTime);
            }

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
                rotationSpeed * Time.deltaTime);
    }

    private void HandleTeleportIfTooFar()
    {
        if (target == null ||
            teleportDistance <= 0f)
        {
            return;
        }

        float squaredDistance =
            (transform.position -
             target.position).sqrMagnitude;

        float squaredTeleportDistance =
            teleportDistance *
            teleportDistance;

        if (squaredDistance <=
            squaredTeleportDistance)
        {
            return;
        }

        Vector3 destination =
            target.position -
            target.forward *
            stoppingDistance;

        if (NavMesh.SamplePosition(
                destination,
                out NavMeshHit hit,
                navMeshSearchRadius,
                NavMesh.AllAreas))
        {
            agent.Warp(
                hit.position);

            body.position =
                hit.position;
        }
    }

    private bool TryWarpToNearestNavMesh()
    {
        if (agent == null)
            return false;

        if (!NavMesh.SamplePosition(
                transform.position,
                out NavMeshHit hit,
                navMeshSearchRadius,
                NavMesh.AllAreas))
        {
            Debug.LogWarning(
                $"Follower '{name}' could not find a NavMesh nearby.",
                this);

            return false;
        }

        if (!agent.enabled)
            agent.enabled = true;

        bool warped =
            agent.Warp(
                hit.position);

        if (warped &&
            body != null)
        {
            body.position =
                hit.position;
        }

        return warped;
    }

    private void StopAgent()
    {
        if (agent == null ||
    !agent.enabled ||
    !agent.isOnNavMesh)
        {
            return;
        }

        agent.ResetPath();
    }

    private bool HasParameter(
        int parameterHash)
    {
        if (animator == null ||
            animator.runtimeAnimatorController == null)
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

    private bool ValidateReference(
        Object reference,
        string displayName)
    {
        if (reference != null)
            return true;

        Debug.LogError(
            $"FollowerNavigation requires {displayName}.",
            this);

        return false;
    }

    private bool ValidateConfiguration()
    {
        bool valid = true;

        valid &= ValidateReference(
            agent,
            nameof(NavMeshAgent));

        valid &= ValidateReference(
            body,
            nameof(Rigidbody));

        valid &= ValidateReference(
            capsule,
            nameof(CapsuleCollider));

        if (animator == null)
        {
            Debug.LogWarning(
                "FollowerNavigation could not find an Animator.",
                this);
        }

        return valid;
    }

    private void OnValidate()
    {
        stoppingDistance =
            Mathf.Max(
                0f,
                stoppingDistance);

        rotationSpeed =
            Mathf.Max(
                0f,
                rotationSpeed);

        teleportDistance =
            Mathf.Max(
                0f,
                teleportDistance);

        groundCheckRadius =
            Mathf.Max(
                0.01f,
                groundCheckRadius);

        groundCheckDistance =
            Mathf.Max(
                0f,
                groundCheckDistance);

        navMeshSearchRadius =
            Mathf.Max(
                0.1f,
                navMeshSearchRadius);
    }

    private void OnDrawGizmosSelected()
    {
        CapsuleCollider currentCapsule =
            capsule != null
                ? capsule
                : GetComponent<CapsuleCollider>();

        if (currentCapsule == null)
            return;

        Vector3 checkPosition =
            currentCapsule.bounds.center -
            Vector3.up *
            (currentCapsule.bounds.extents.y -
             groundCheckDistance);

        Gizmos.DrawWireSphere(
            checkPosition,
            groundCheckRadius);
    }
}