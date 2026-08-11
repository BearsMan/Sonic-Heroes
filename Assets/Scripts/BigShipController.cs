using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BigShipController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)]
    private float moveSpeed = 15f;

    [Header("Forward Axis")]
    [SerializeField]
    private bool useLocalUpAsForward = true;

    [Header("Altitude")]
    [SerializeField]
    private bool lockStartingAltitude = true;

    private Rigidbody body;

    private float startingAltitude;
    private Quaternion startingRotation;

    private bool initialized;

    public float MoveSpeed => moveSpeed;

    private void Awake()
    {
        body =
            GetComponent<Rigidbody>();

        ConfigurePhysics();

        startingAltitude =
            body.position.y;

        startingRotation =
            body.rotation;

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

        MoveForward();
    }

    private void MoveForward()
    {
        Vector3 forward =
    useLocalUpAsForward
        ? startingRotation * Vector3.down
        : startingRotation * Vector3.forward;

        if (!IsFinite(forward) ||
            forward.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        Vector3 nextPosition =
            body.position +
            forward.normalized *
            moveSpeed *
            Time.fixedDeltaTime;

        if (lockStartingAltitude)
        {
            nextPosition.y =
                startingAltitude;
        }

        if (!IsFinite(nextPosition))
        {
            return;
        }

        body.MovePosition(
            nextPosition);

        body.MoveRotation(
            startingRotation);
    }

    private void ConfigurePhysics()
    {
        if (body == null)
        {
            return;
        }

        body.useGravity =
            false;

        body.isKinematic =
            true;

        body.linearVelocity =
            Vector3.zero;

        body.angularVelocity =
            Vector3.zero;

        body.interpolation =
            RigidbodyInterpolation.Interpolate;

        body.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;
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