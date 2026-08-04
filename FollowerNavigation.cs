using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CapsuleCollider))]
public sealed class FollowerNavigation : MonoBehaviour
{
    #region Constants

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");

    #endregion

    #region Inspector

    [Header("Follow Target")]
    [SerializeField] private Transform target;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float stoppingDistance = 0.1f;
    [SerializeField, Min(0f)] private float rotationSpeed = 540f;
    [SerializeField, Min(0f)] private float teleportDistance = 20f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField, Min(0.01f)] private float groundCheckRadius = 0.35f;
    [SerializeField, Min(0f)] private float groundCheckDistance = 0.1f;

    [Header("NavMesh")]
    [SerializeField, Min(0.1f)] private float navMeshSearchRadius = 5f;
    [SerializeField] private bool warpToNearestNavMeshOnSetup = true;

    #endregion

    #region Runtime State

    private NavMeshAgent agent;
    private Animator animator;
    private Rigidbody body;
    private CapsuleCollider capsule;
    private Vector3 lastDestination;

    private bool isGrounded;
    private bool isInitialized;
    private bool isExternallyMoving;
    private bool isShuttingDown;
    private bool hasSpeedParameter;
    private bool hasGroundedParameter;

    #endregion

    #region Public API

    public Transform Target => target;
    public bool IsGrounded => isGrounded;
    public bool IsInitialized => isInitialized;
    public bool IsExternallyMoving => isExternallyMoving;

    public bool IsFollowing =>
        isInitialized &&
        !isExternallyMoving &&
        target != null &&
        agent != null &&
        agent.enabled &&
        agent.isOnNavMesh;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheComponents();
        ConfigureComponents();
        ConfigureAgent();
        CacheAnimatorParameters();
    }

    private void Start()
    {
        if (!ValidateConfiguration())
        {
            enabled = false;
            return;
        }

        if (target != null)
        {
            Initialize(target);
        }
    }

    private void OnEnable()
    {
        if (isShuttingDown)
            return;

        CacheComponents();
        ConfigureComponents();
        ConfigureAgent();
        CacheAnimatorParameters();


        if (isInitialized && !isExternallyMoving)
        {
            EnableAgent();
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

        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
        {
            return;
        }

        HandleTeleportIfTooFar();
        FollowTarget();
        RotateTowardsMovement();
    }

    private void OnDisable()
    {
        CleanupRuntimeState();
    }

    private void OnDestroy()
    {
        isShuttingDown = true;
        CleanupDestroyedState();
    }

    private void OnValidate()
    {
        stoppingDistance = Mathf.Max(0f, stoppingDistance);
        rotationSpeed = Mathf.Max(0f, rotationSpeed);
        teleportDistance = Mathf.Max(0f, teleportDistance);
        groundCheckRadius = Mathf.Max(0.01f, groundCheckRadius);
        groundCheckDistance = Mathf.Max(0f, groundCheckDistance);
        navMeshSearchRadius = Mathf.Max(0.1f, navMeshSearchRadius);
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
            (currentCapsule.bounds.extents.y - groundCheckDistance);

        Gizmos.DrawWireSphere(checkPosition, groundCheckRadius);
    }

    #endregion

    #region Initialization

    public bool Setup()
    {
        return Initialize(target);
    }

    public bool Initialize(Transform followTarget)
    {
        if (followTarget == null)
        {
            Debug.LogError(
                $"FollowerNavigation on '{name}' received no follow target.",
                this);

            isInitialized = false;
            return false;
        }


        target = followTarget;

        lastDestination =
                new Vector3(
                    float.PositiveInfinity,
                    float.PositiveInfinity,
                    float.PositiveInfinity);

        CacheComponents();
        ConfigureComponents();
        ConfigureAgent();
        CacheAnimatorParameters();

        if (!ValidateConfiguration())
        {
            isInitialized = false;
            return false;
        }

        if (warpToNearestNavMeshOnSetup)
        {
            TryWarpToNearestNavMesh();
        }

        isExternallyMoving = false;
        isInitialized = true;

        EnableAgent();
        return true;
    }

    private void CacheComponents()
    {
        agent ??= GetComponent<NavMeshAgent>();
        animator ??= GetComponentInChildren<Animator>(true);
        body ??= GetComponent<Rigidbody>();
        capsule ??= GetComponent<CapsuleCollider>();
    }

    private void ConfigureComponents()
    {
        if (body == null)
        {
            return;
        }

        body.constraints = RigidbodyConstraints.FreezeRotation;

        body.isKinematic = true;
        body.useGravity = false;
    }

    private void ConfigureAgent()
    {
        if (agent == null)
            return;

        agent.updateRotation = false;
        agent.stoppingDistance = stoppingDistance;
        agent.angularSpeed = rotationSpeed;
        agent.autoBraking = true;
    }

    private void CacheAnimatorParameters()
    {
        hasSpeedParameter =
            HasParameter(SpeedHash);

        hasGroundedParameter =
            HasParameter(GroundedHash);
    }

    #endregion

    #region Target Management

    public bool SetFollowTarget(Transform followTarget)
    {
        if (followTarget == null)
        {
            ClearTarget();
            return false;
        }

        target = followTarget;

        lastDestination =
        new Vector3(
        float.PositiveInfinity,
        float.PositiveInfinity,
        float.PositiveInfinity);

        if (!isInitialized)
        {
            return Initialize(target);
        }

        if (!isExternallyMoving)
        {
            EnableAgent();
        }

        return true;
    }

    public void ClearTarget()
    {
        target = null;
        StopAgent();
    }

    #endregion

    #region External Movement

    public void BeginExternalMovement()
    {
        isExternallyMoving = true;

        StopAgent();
        DisableAgent();

        if (body != null)
        {
            body.isKinematic = false;
            body.useGravity = true;
        }
    }

    public void EndExternalMovement()
    {
        isExternallyMoving = false;

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;
        }

        if (isInitialized)
        {
            EnableAgent();
        }
    }

    public void Jump(Vector3 velocity)
    {
        if (body == null)
            return;

        BeginExternalMovement();
        body.linearVelocity = velocity;
    }

    public void Launch(Vector3 velocity)
    {
        Jump(velocity);
    }

    #endregion

    #region Agent Control

    public void EnableAgent()
    {
        if (agent == null)
            return;

        if (!agent.enabled)
        {
            agent.enabled = true;
        }

        if (!agent.isOnNavMesh)
        {
            TryWarpToNearestNavMesh();
        }
    }

    public void DisableAgent()
    {
        if (agent == null)
            return;

        StopAgent();

        if (agent.enabled)
        {
            agent.enabled = false;
        }
    }

    public bool CanReach(Vector3 destination)
    {
        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
        {
            return false;
        }

        NavMeshPath path = new();

        return
            agent.CalculatePath(destination, path) &&
            path.status == NavMeshPathStatus.PathComplete;
    }

    private void EnsureAgentState()
    {
        if (agent == null)
            return;

        EnableAgent();
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

    #endregion

    #region Navigation

    private void FollowTarget()
    {
        if (target == null ||
            agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
        {
            return;
        }

        Vector3 destination =
        target.position;

        if ((destination - lastDestination).sqrMagnitude > 0.04f)
        {
            agent.SetDestination(destination);
            lastDestination = destination;
        }
    }

    private void RotateTowardsMovement()
    {
        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
        {
            return;
        }

        Vector3 direction = agent.velocity;
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
            (transform.position - target.position).sqrMagnitude;

        float squaredTeleportDistance =
            teleportDistance * teleportDistance;

        if (squaredDistance <= squaredTeleportDistance)
            return;

        Vector3 destination =
            target.position -
            target.forward * stoppingDistance;

        if (!NavMesh.SamplePosition(
                destination,
                out NavMeshHit hit,
                navMeshSearchRadius,
                NavMesh.AllAreas))
        {
            return;
        }

        if (agent != null &&
            agent.enabled)
        {
            agent.Warp(hit.position);
            lastDestination = Vector3.positiveInfinity;
        }

        if (body != null)
        {
            body.position = hit.position;
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
            return false;
        }

        if (!agent.enabled)
        {
            agent.enabled = true;
        }

        bool warped = agent.Warp(hit.position);

        if (warped &&
            body != null)
        {
            body.position = hit.position;
        }

        return warped;
    }

    #endregion

    #region Grounding And Animation

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
            (capsule.bounds.extents.y - groundCheckDistance);

        isGrounded =
            Physics.CheckSphere(
                checkPosition,
                groundCheckRadius,
                groundMask,
                QueryTriggerInteraction.Ignore);

        if (isGrounded &&
            isExternallyMoving &&
            body != null &&
            body.linearVelocity.y <= 0f)
        {
            EndExternalMovement();
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

        if (hasSpeedParameter)
        {
            animator.SetFloat(SpeedHash, speed);
        }

        if (hasGroundedParameter)
        {
            animator.SetBool(GroundedHash, isGrounded);
        }
    }

    private bool HasParameter(int parameterHash)
    {
        if (animator == null ||
            animator.runtimeAnimatorController == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == parameterHash)
                return true;
        }

        return false;
    }

    #endregion

    #region Validation And Cleanup

    private bool ValidateConfiguration()
    {
        bool valid = true;

        valid &= ValidateReference(agent, nameof(NavMeshAgent));
        valid &= ValidateReference(body, nameof(Rigidbody));
        valid &= ValidateReference(capsule, nameof(CapsuleCollider));

        if (animator == null)
        {
            Debug.LogWarning(
                "FollowerNavigation could not find an Animator.",
                this);
        }

        return valid;
    }

    private bool ValidateReference(Object reference, string displayName)
    {
        if (reference != null)
            return true;

        Debug.LogError(
            $"FollowerNavigation requires {displayName}.",
            this);

        return false;
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

        agent = null;
        animator = null;
        body = null;
        capsule = null;

        hasSpeedParameter = false;
        hasGroundedParameter = false;
        lastDestination = Vector3.zero;
    }

    #endregion
}
