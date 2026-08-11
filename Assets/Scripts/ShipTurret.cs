using UnityEngine;

public class ShipTurret : MonoBehaviour
{
    [Header("Turret")]
    [SerializeField]
    private Transform rotatingBase;

    [SerializeField]
    private ShipCannon cannon;

    [Header("Targeting")]
    [SerializeField, Min(0f)]
    private float detectionRange = 100f;

    [SerializeField, Min(0f)]
    private float rotationSpeed = 60f;

    [SerializeField, Range(0f, 90f)]
    private float firingAngle = 10f;

    [SerializeField]
    private Transform targetOverride;

    private Transform target;

    private void Awake()
    {
        if (rotatingBase == null)
        {
            rotatingBase = transform;
        }

        if (cannon == null)
        {
            cannon = GetComponentInChildren<ShipCannon>();
        }

        AcquireTarget();
    }

    private void Update()
    {
        ValidateTarget();

        if (target == null || !TargetInRange())
        {
            return;
        }

        RotateTowardTarget();
        TryFire();
    }

    private void ValidateTarget()
    {
        if (targetOverride != null)
        {
            target = targetOverride;
            return;
        }

        if (target != null && target.gameObject.activeInHierarchy)
        {
            return;
        }

        AcquireTarget();
    }

    private void AcquireTarget()
    {
        if (targetOverride != null)
        {
            target = targetOverride;
            return;
        }

        UltimatePlayerMovement player =
            Object.FindAnyObjectByType<UltimatePlayerMovement>(
                FindObjectsInactive.Exclude);

        if (player != null)
        {
            target = player.transform;
        }
    }

    private bool TargetInRange()
    {
        if (target == null || rotatingBase == null)
        {
            return false;
        }

        Vector3 difference = target.position - rotatingBase.position;

        if (!IsFinite(difference))
        {
            return false;
        }

        return difference.sqrMagnitude <= detectionRange * detectionRange;
    }

    private void RotateTowardTarget()
    {
        Vector3 direction = target.position - rotatingBase.position;

        if (!IsFinite(direction) || direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        direction.Normalize();

        Quaternion desiredRotation =
            Quaternion.LookRotation(direction, rotatingBase.up);

        rotatingBase.rotation =
            Quaternion.RotateTowards(
                rotatingBase.rotation,
                desiredRotation,
                rotationSpeed * Time.deltaTime);
    }

    private void TryFire()
    {
        if (cannon == null || target == null || rotatingBase == null)
        {
            return;
        }

        Vector3 direction = target.position - rotatingBase.position;

        if (!IsFinite(direction) || direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        float angle = Vector3.Angle(
            rotatingBase.forward,
            direction.normalized);

        if (angle > firingAngle)
        {
            return;
        }

        cannon.FireAt(target);
    }

    private static bool IsFinite(Vector3 value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }
}
