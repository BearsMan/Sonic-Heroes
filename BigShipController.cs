using UnityEngine;
[RequireComponent(typeof(Rigidbody))]
[RequireComponent (typeof(ShipWeapons))]
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

    [Header("Banking")]
    [SerializeField] private Transform visualModel;
    [SerializeField] private float maxBankAngle = 12f;
    [SerializeField] private float bankSpeed = 4f;

    private ShipWeapons shipWeapons;
    private float currentBank = 0;
    private int currentWayPointIndex = 0;
    private Rigidbody body;
    private Quaternion visualStartRotation;

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
        shipWeapons = GetComponent<ShipWeapons>();
        if (visualModel != null )
        {
            visualStartRotation = visualModel.localRotation;
        }
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

                if (shipWeapons != null)
                {
                    shipWeapons.Fire(player);
                }

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
        Vector3 offset = target - body.position;
        if (offset.sqrMagnitude < 0.001f)
        {
            return;
        }
        Vector3 targetDirection = offset.normalized;
        float turnDirection = Vector3.Dot(transform.right, targetDirection);
        UpdateBank(turnDirection);

        Quaternion targetRotation = Quaternion.LookRotation(targetDirection, Vector3.up);
        Quaternion nextRotation = Quaternion.RotateTowards(body.rotation, targetRotation, steeringSpeed * Time.fixedDeltaTime);

        Vector3 nextPosition = body.position + (nextRotation * Vector3.forward * moveSpeed * Time.fixedDeltaTime);

        body.MovePosition(nextPosition);
        body.MoveRotation(nextRotation);

    }

    private void UpdateBank(float turnDirection)
    {
        if (visualModel == null)
        {
            return;
        }

        float targetBank = turnDirection * maxBankAngle;
        currentBank = Mathf.Lerp(currentBank, targetBank, bankSpeed * Time.fixedDeltaTime);

        visualModel.localRotation = visualStartRotation * Quaternion.Euler(0f, 0f, currentBank);
    }
}