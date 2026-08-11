using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BigShipController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)]
    private float moveSpeed = 15f;

    [SerializeField, Min(0f)]
    private float turnSpeed = 2f;

    [SerializeField]
    private bool useLocalUpAsForward = true;

    [Header("Path")]
    [SerializeField]
    private Transform[] waypoints;

    [SerializeField, Min(0.01f)]
    private float waypointReachDistance = 5f;

    [SerializeField]
    private bool loopWaypoints = true;

    [SerializeField]
    private bool moveWithoutWaypoints = true;

    [Header("Altitude")]
    [SerializeField]
    private bool lockAltitude = true;

    [SerializeField]
    private bool useStartingAltitude = true;

    [SerializeField]
    private float fixedAltitude;

    [Header("Stability")]
    [SerializeField]
    private bool forceKinematic = true;

    [SerializeField]
    private bool disableGravity = true;

    [SerializeField]
    private bool useInterpolation = true;

    private Rigidbody body;
    private int waypointIndex;
    private float startingAltitude;
    private bool initialized;

    public float MoveSpeed => moveSpeed;

    public float TurnSpeed => turnSpeed;

    public bool IsMoving =>
        enabled &&
        initialized;

    private void Awake()
    {
        CacheComponents();
        ConfigurePhysics();
        CaptureStartingAltitude();

        initialized = true;
    }

    private void OnEnable()
    {
        if (!initialized)
        {
            return;
        }

        ConfigurePhysics();
    }

    private void FixedUpdate()
    {
        if (!CanMove())
        {
            return;
        }

        Vector3 direction =
            GetDesiredDirection();

        if (!IsFinite(direction) ||
            direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        Quaternion nextRotation =
            CalculateRotation(direction);

        Vector3 nextPosition =
            CalculatePosition(nextRotation);

        if (!IsFinite(nextPosition) ||
            !IsFinite(nextRotation))
        {
            return;
        }

        body.MovePosition(nextPosition);
        body.MoveRotation(nextRotation);
    }

    public void SetWaypoints(
        Transform[] newWaypoints,
        bool loop)
    {
        waypoints = newWaypoints;
        loopWaypoints = loop;
        waypointIndex = 0;
    }

    public void ResetPath()
    {
        waypointIndex = 0;
    }

    public void StopMovement()
    {
        if (body == null)
        {
            return;
        }

        body.linearVelocity =
            Vector3.zero;

        body.angularVelocity =
            Vector3.zero;
    }

    private void CacheComponents()
    {
        body =
            GetComponent<Rigidbody>();
    }

    private void ConfigurePhysics()
    {
        if (body == null)
        {
            return;
        }

        body.useGravity =
            !disableGravity;

        if (forceKinematic)
        {
            body.isKinematic =
                true;
        }

        body.interpolation =
            useInterpolation
                ? RigidbodyInterpolation.Interpolate
                : RigidbodyInterpolation.None;

        body.collisionDetectionMode =
            body.isKinematic
                ? CollisionDetectionMode.ContinuousSpeculative
                : CollisionDetectionMode.ContinuousDynamic;

        body.linearVelocity =
            Vector3.zero;

        body.angularVelocity =
            Vector3.zero;
    }

    private void CaptureStartingAltitude()
    {
        startingAltitude =
            body != null
                ? body.position.y
                : transform.position.y;
    }

    private bool CanMove()
    {
        if (body == null)
        {
            return false;
        }

        if (!gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!IsFinite(body.position) ||
            !IsFinite(body.rotation))
        {
            return false;
        }

        return true;
    }

    private Vector3 GetDesiredDirection()
    {
        if (waypoints == null ||
            waypoints.Length == 0)
        {
            return
                moveWithoutWaypoints
                    ? GetForwardAxis()
                    : Vector3.zero;
        }

        if (waypointIndex < 0 ||
            waypointIndex >=
            waypoints.Length)
        {
            waypointIndex = 0;
        }

        Transform waypoint =
            waypoints[waypointIndex];

        if (waypoint == null)
        {
            AdvanceWaypoint();
            return GetForwardAxis();
        }

        Vector3 direction =
            waypoint.position -
            body.position;

        if (!IsFinite(direction))
        {
            return Vector3.zero;
        }

        if (direction.magnitude <=
            waypointReachDistance)
        {
            AdvanceWaypoint();
            return GetForwardAxis();
        }

        if (lockAltitude)
        {
            direction.y = 0f;
        }

        return direction.normalized;
    }

    private void AdvanceWaypoint()
    {
        if (waypoints == null ||
            waypoints.Length == 0)
        {
            return;
        }

        waypointIndex++;

        if (waypointIndex <
            waypoints.Length)
        {
            return;
        }

        waypointIndex =
            loopWaypoints
                ? 0
                : waypoints.Length - 1;
    }

    private Vector3 GetForwardAxis()
    {
        Vector3 forward =
            useLocalUpAsForward
                ? transform.up
                : transform.forward;

        if (lockAltitude)
        {
            forward.y = 0f;
        }

        return
            forward.sqrMagnitude > Mathf.Epsilon
                ? forward.normalized
                : Vector3.zero;
    }

    private Quaternion CalculateRotation(
        Vector3 direction)
    {
        if (direction.sqrMagnitude <=
            Mathf.Epsilon)
        {
            return body.rotation;
        }

        Quaternion desiredRotation;

        if (useLocalUpAsForward)
        {
            Quaternion look =
                Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up);

            desiredRotation =
                look *
                Quaternion.Euler(
                    90f,
                    0f,
                    0f);
        }
        else
        {
            desiredRotation =
                Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up);
        }

        Quaternion nextRotation =
            Quaternion.RotateTowards(
                body.rotation,
                desiredRotation,
                turnSpeed *
                Time.fixedDeltaTime);

        return
            IsFinite(nextRotation)
                ? nextRotation
                : body.rotation;
    }

    private Vector3 CalculatePosition(
        Quaternion rotation)
    {
        Vector3 forward =
            useLocalUpAsForward
                ? rotation * Vector3.up
                : rotation * Vector3.forward;

        if (!IsFinite(forward) ||
            forward.sqrMagnitude <= Mathf.Epsilon)
        {
            return body.position;
        }

        Vector3 nextPosition =
            body.position +
            forward.normalized *
            moveSpeed *
            Time.fixedDeltaTime;

        if (lockAltitude)
        {
            nextPosition.y =
                useStartingAltitude
                    ? startingAltitude
                    : fixedAltitude;
        }

        return nextPosition;
    }

    private static bool IsFinite(
        Vector3 value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }

    private static bool IsFinite(
        Quaternion value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z) &&
            float.IsFinite(value.w);
    }
}