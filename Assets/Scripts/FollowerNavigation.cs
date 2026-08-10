using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CapsuleCollider))]
public class FollowerNavigation : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    public Transform target;
    public NavMeshAgent agent;
    public Animator anim;
    private CapsuleCollider capsule;
    public Rigidbody body;
    public bool grounded = false;
    public LayerMask groundMask;
    private bool isSetup = false;
    private Vector3 formationOffset;
    public bool hasFormationOffset = false;
    private bool agentDisabled = false;
    private TeamActionController teamController;

    // Start is called before the first frame update
    void Start()
    {
        Setup();
    }

    public void SetTeamController(
    TeamActionController controller)
    {
        teamController =
            controller;
    }

    public void Setup()
    {
        if (anim == null)
        {
            anim = GetComponentInChildren<Animator>();
        }

        if (anim != null && anim.runtimeAnimatorController != null)
        {
            Debug.Log($"Animator object: {anim.gameObject.name}, " + $"Controller: {anim.runtimeAnimatorController.name}");
        }
        else
        {
            Debug.LogWarning("FollowerNavigation has no valid Animator/controller.");
        }

        agent = GetComponent<NavMeshAgent>();
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 20f,NavMesh.AllAreas))
        {
            transform.position = hit.position;
        }
        else
        {
            Debug.LogWarning("Follower could not find a NavMesh nearby.");
        }

        isSetup = true;

        EnableAgent();

        if (body != null)
        {
            body.linearVelocity =
                Vector3.zero;

            body.angularVelocity =
                Vector3.zero;

            body.useGravity =
                false;

            body.isKinematic =
                true;
        }

        agent.updatePosition =
            true;

        agent.updateRotation =
            true;

        agent.autoBraking =
            true;

        if (agent.speed <= 0f)
        {
            agent.speed =
                8f;
        }

        if (agent.acceleration <= 0f)
        {
            agent.acceleration =
                20f;
        }

        if (agent.angularSpeed <= 0f)
        {
            agent.angularSpeed =
                360f;
        }

        if (!agent.enabled)
        {
            agent.enabled =
                true;
        }

        if (teamController != null)
        {
            Transform leader =
                teamController.GetFormationLeader();

            if (leader != null &&
                leader != transform)
            {
                target =
                    leader;
            }
        }

        if (target != null)
            {
                formationOffset =
                    target.InverseTransformPoint(
                        transform.position);

                formationOffset.y =
                    0f;

                hasFormationOffset =
                    true;
            }
        }

    public void EnableAgent()
    {
        agentDisabled = false;

        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (agent == null || !gameObject.activeInHierarchy)
        {
            return;
        }

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;

            body.angularVelocity = Vector3.zero;

            body.useGravity = false;

            body.isKinematic = true;
        }

        agent.updatePosition = true;

        agent.updateRotation = true;

        agent.autoBraking = true;

        if (agent.speed <= 0f)
        {
            agent.speed = 8f;
        }

        if (agent.acceleration <= 0f)
        {
            agent.acceleration = 20f;
        }

        if (agent.angularSpeed <= 0f)
        {
            agent.angularSpeed = 360f;
        }

        if (!agent.enabled)
        {
            agent.enabled = true;
        }

        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;

            return;
        }

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);

            agent.isStopped = false;
        }
    }

    public void DisableAgent()
    {
        agentDisabled =
            true;

        if (agent != null)
        {
            if (agent.enabled &&
                agent.isOnNavMesh)
            {
                agent.isStopped =
                    true;

                agent.ResetPath();
            }

            if (agent.enabled)
            {
                agent.enabled =
                    false;
            }
        }

        if (body != null)
        {
            body.isKinematic =
                false;

            body.useGravity =
                true;
        }
    }

    private bool HasParameter(int hash)
    {
        if (anim == null)
            return false;

        foreach (AnimatorControllerParameter parameter in anim.parameters)
        {
            if (parameter.nameHash == hash)
                return true;
        }

        return false;
    }

    // Update is called once per frame
    void Update()
    {
        if (teamController != null)
        {
            Transform leader = teamController.GetFormationLeader();

            if (leader != null && leader != transform)
            {
                target = leader;
            }

            if (!isSetup || target == null)
            {
                return;
            }

            Vector3 groundCheckPosition = capsule.bounds.center - Vector3.up * (capsule.bounds.extents.y - 0.1f);

            Debug.DrawRay(groundCheckPosition, Vector3.down * 0.5f, Color.red);

            grounded = Physics.CheckSphere(
                groundCheckPosition,
                0.35f,
                groundMask,
                QueryTriggerInteraction.Ignore
            );

            if (agent != null &&
                grounded &&
                !agent.enabled &&
                !agentDisabled)
            {
                EnableAgent();
            }

            if (anim != null)
            {
                float speed = agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : body.linearVelocity.magnitude;

                if (HasParameter(SpeedHash))
                {
                    anim.SetFloat(SpeedHash, speed);
                }

                if (HasParameter(GroundedHash))
                {
                    anim.SetBool(GroundedHash, grounded);
                }

            }

            RotateToPos();

            if (agent != null &&
                agent.enabled &&
                agent.isOnNavMesh)
            {
                Vector3 destination =
                    hasFormationOffset
                        ? target.TransformPoint(
                            formationOffset)
                        : target.position;

                agent.SetDestination(
                    destination);
            }
        }
    }
    public void RotateToPos()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        if (agent.velocity.magnitude < 0.5f)
        {
            transform.rotation = target.rotation;
        }
    }
    public void Jump(Vector3 velocity)
    {
        if (agent != null && agent.enabled)
        {
            agent.enabled = false;
        }

        if (body == null)
        {
            return;
        }

        body.isKinematic = false;

        body.useGravity = true;

        body.linearVelocity = velocity;
    }

    public bool PathValid(Vector3 dest)
    {
        float dis = Vector3.Distance(transform.position, dest);

        if (dis > 10)
        {
            return false;
        }

        NavMeshPath path = new NavMeshPath();
        NavMesh.CalculatePath(transform.position, dest, NavMesh.AllAreas, path);

        return path.status == NavMeshPathStatus.PathComplete;
    }
}
