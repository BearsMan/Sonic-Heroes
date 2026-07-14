using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BigShipController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 15f;
    

    [Header("Path Settings")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float wayPointReachDistance = 5f;
    [SerializeField] private float steeringSpeed = 20f;

    [Header("Targeting")]
    [SerializeField] private Transform player;
    [SerializeField] private float detectionRange = 150f;
    [SerializeField] private float attackRange = 60f;
    [SerializeField] private float loseTargetRange = 220f;

    [Header("Combat")]
    [SerializeField] private GameObject prejectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireCoolDown = 2f;
    [SerializeField] private float prejectileSpeed = 80f;

    private int currentWayPointIndex = 0;
    private Rigidbody body;

    private ShipState currentState = ShipState.Patrol;

    private enum ShipState
    {
        Patrol,
        Chase,
        Attack,
        ReturnToPatrol
    }

    void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (player == null)
        {
            Patrol();
            return;
        }

        float distanceToPlayer = Vector3.Distance(body.position, player.position);

        switch (currentState)
        {
            case ShipState.Patrol:
                Patrol();

                if (distanceToPlayer <= detectionRange)
                {
                    currentState = ShipState.Chase;
                }

                break;

            case ShipState.Chase:
                MoveTowards(player.position);

                if (distanceToPlayer <= attackRange)
                {
                    currentState = ShipState.Attack;
                }
                else if (distanceToPlayer >= loseTargetRange)
                {
                    currentState = ShipState.ReturnToPatrol;
                }

                break;

            case ShipState.Attack:
                MoveTowards(player.position);

                if (distanceToPlayer > attackRange)
                {
                    currentState = ShipState.Chase;
                }

                break;

            case ShipState.ReturnToPatrol:
                Patrol();

                if (distanceToPlayer <= detectionRange)
                {
                    currentState = ShipState.Chase;
                }

                break;
        }
    }

    private void Patrol()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            return;
        }

        Transform waypoint = waypoints[currentWayPointIndex];

        if (waypoint == null)
        {
            return;
        }

        MoveTowards(waypoint.position);

        float distance = Vector3.Distance(body.position, waypoint.position);

        if (distance <= wayPointReachDistance)
        {
            currentWayPointIndex = (currentWayPointIndex + 1) % waypoints.Length;
        }
    }

    private void MoveTowards(Vector3 target)
    {
        Vector3 targetDirection = (target - body.position).normalized;

        Quaternion targetRotation = Quaternion.LookRotation(targetDirection, Vector3.up);
        Quaternion nextRotation = Quaternion.RotateTowards(body.rotation, targetRotation, steeringSpeed * Time.fixedDeltaTime);

        Vector3 nextPosition = body.position + (nextRotation * Vector3.forward * moveSpeed * Time.fixedDeltaTime);

        body.MovePosition(nextPosition);
        body.MoveRotation(nextRotation);

    }
}
