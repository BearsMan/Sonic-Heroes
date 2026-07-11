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
    public bool grounded;
    public LayerMask groundMask;
    private bool isSetup;

    // Start is called before the first frame update
    void Start()
    {
        Setup();
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

            if (agent != null && grounded && !agent.enabled)
            {
                agent.enabled = true;
            }

                if (anim != null)
                {
                    float speed = agent.enabled && agent.isOnNavMesh? agent.velocity.magnitude: body.linearVelocity.magnitude;

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

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.SetDestination(target.position);
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
        agent.enabled = false;
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
