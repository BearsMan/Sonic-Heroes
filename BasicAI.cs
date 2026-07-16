using UnityEngine;
using UnityEngine.AI;
[RequireComponent(typeof(NavMeshAgent))]
public class BasicAI : MonoBehaviour
{
    [Header("AI Settings")]
    public float chaseRange = 20f;
    public float attackRange = 2f;

    [Header("References")]
    public Transform targetposition;
    public NavMeshAgent agent;
    public enum STATE
    {
        Idle,
        Move,
        Special,
        Attack,
        Dead,
        Inair
    }

    public STATE curState = STATE.Move;

    private void Awake()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
    }
    // Find the player automatically if no target has been assigned
    private void Start()
    {
        if (targetposition != null)
        {
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null )
        {
            targetposition = player.transform;
        }

        else
        {
            Debug.LogWarning("Basic AI could not find an object tagged player", this);
        }
    }

    // Update is called once per frame
    private void Update()
    {
        switch (curState)
        {
            case STATE.Idle:
                IdleState();
                break;
            case STATE.Move:
                MoveState();
                break;
            case STATE.Special:
                SpecialState();
                break;
            case STATE.Attack:
                AttackState();
                break;
            case STATE.Inair:
                InairState();
                break;
            case STATE.Dead:
                DeadState();
                break;


        }
    }

    public void SetState(STATE newState)
    {
        curState = newState;
    }

    #region states
    public void IdleState()
    {
        if (targetposition == null)
        {
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, targetposition.position);

        if (distanceToTarget <= chaseRange)
        {
            SetState(STATE.Move);
        }
    }

    public void MoveState()
    {
        if (agent == null || targetposition == null)
        {
            return;
        }
            agent.SetDestination(targetposition.position);

        float distanceToTarget = Vector3.Distance(transform.position, targetposition.position);
        if (distanceToTarget <= chaseRange)
        {
            agent.isStopped = false;
        }

        else
        {
            agent.isStopped = true;
            SetState(STATE.Idle);
        }
    }
    public void SpecialState()
    {

    }
    public void AttackState()
    {
        
    }
    public void InairState()
    {

    }
    public void DeadState()
    {

    }
    #endregion
}
