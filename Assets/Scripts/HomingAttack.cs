using UnityEngine;

public class HomingAttack : MonoBehaviour
{
    #region Homing Attack State

    public bool homingAttackAvailable = false;
    public bool homingAttackUsed = false;

    #endregion

    #region References

    [Header("References")]

    public UltimatePlayerMovement movement;

    [SerializeField]
    private Rigidbody body;

    [SerializeField]
    private Animator anim;

    #endregion

    #region Input

    [Header("Input")]

    [SerializeField]
    private KeyCode homingAttackKey =
        KeyCode.Space;

    #endregion

    #region Target Detection

    [Header("Target Detection")]

    [SerializeField]
    private LayerMask targetLayers = ~0;

    [SerializeField, Min(0.1f)]
    private float targetRange = 15f;

    [SerializeField, Range(0f, 180f)]
    private float targetAngle = 90f;

    [SerializeField, Min(1)]
    private int maximumTargets = 32;

    #endregion

    #region Homing Movement

    [Header("Homing Movement")]

    [SerializeField, Min(1)]
    private int homingAttackDamage = 1;

    [SerializeField, Min(0.1f)]
    private float homingSpeed = 35f;

    [SerializeField, Min(0.01f)]
    private float hitDistance = 1f;

    [SerializeField, Min(0.01f)]
    private float maximumHomingTime = 0.6f;

    [SerializeField, Min(0f)]
    private float upwardBounceForce = 6f;

    [SerializeField]
    private bool allowChainAttack = true;

    #endregion

    #region Animation

    [Header("Animation")]

    [SerializeField]
    private string spinParameter =
        "Spin";

    #endregion

    #region Runtime State

    private readonly Collider[] targetResults =
        new Collider[32];

    private Transform currentTarget;

    private float homingTimer;

    private bool isHoming;

    #endregion

    #region Properties

    public bool IsHoming =>
        isHoming;

    public Transform CurrentTarget =>
        currentTarget;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheReferences();
    }

    private void Start()
    {
        CacheReferences();
    }

    private void Update()
    {
        if (movement == null ||
            body == null)
        {
            return;
        }

        if (movement.IsGrounded)
        {
            ResetOnGround();

            return;
        }

        if (isHoming)
        {
            UpdateHomingAttack();

            return;
        }

        if (Input.GetKeyDown(
                homingAttackKey) &&
            homingAttackAvailable &&
            !homingAttackUsed)
        {
            TryStartHomingAttack();
        }
    }

    private void OnDisable()
    {
        CancelHomingAttack();
    }

    private void OnValidate()
    {
        targetRange =
            Mathf.Max(
                0.1f,
                targetRange);

        targetAngle =
            Mathf.Clamp(
                targetAngle,
                0f,
                180f);

        maximumTargets =
            Mathf.Max(
                1,
                maximumTargets);

        homingSpeed =
            Mathf.Max(
                0.1f,
                homingSpeed);

        hitDistance =
            Mathf.Max(
                0.01f,
                hitDistance);

        maximumHomingTime =
            Mathf.Max(
                0.01f,
                maximumHomingTime);

        upwardBounceForce =
            Mathf.Max(
                0f,
                upwardBounceForce);
    }

    #endregion

    #region References

    private void CacheReferences()
    {
        movement ??=
            GetComponentInParent<
                UltimatePlayerMovement>();

        body ??=
            GetComponentInParent<
                Rigidbody>();

        anim ??=
            GetComponentInChildren<
                Animator>();
    }

    #endregion

    #region Homing Attack

    private bool TryStartHomingAttack()
    {
        if (movement == null ||
            body == null ||
            movement.IsGrounded ||
            homingAttackUsed)
        {
            return false;
        }

        currentTarget =
            FindBestTarget();

        if (currentTarget == null)
        {
            return false;
        }

        homingAttackUsed =
            true;

        homingAttackAvailable =
            false;

        isHoming =
            true;

        homingTimer =
            maximumHomingTime;

        body.useGravity =
            false;

        body.linearVelocity =
            Vector3.zero;

        body.angularVelocity =
            Vector3.zero;

        SetSpinAnimation(
            true);

        return true;
    }

    private void UpdateHomingAttack()
    {
        if (!isHoming)
        {
            return;
        }

        if (currentTarget == null ||
            !currentTarget.gameObject.activeInHierarchy)
        {
            FinishHomingAttack(
                false);

            return;
        }

        homingTimer -=
            Time.deltaTime;

        if (homingTimer <= 0f)
        {
            FinishHomingAttack(
                false);

            return;
        }

        Vector3 targetPosition =
            GetTargetPosition(
                currentTarget);

        Vector3 direction =
            targetPosition -
            body.position;

        if (!IsFiniteVector(
                direction))
        {
            FinishHomingAttack(
                false);

            return;
        }

        float distance =
            direction.magnitude;

        if (!float.IsFinite(
                distance))
        {
            FinishHomingAttack(
                false);

            return;
        }

        if (distance <=
            hitDistance)
        {
            HandleTargetHit();

            return;
        }

        if (direction.sqrMagnitude <=
            0.0001f)
        {
            HandleTargetHit();

            return;
        }

        direction.Normalize();

        body.linearVelocity =
            direction *
            homingSpeed;

        FaceDirection(
            direction);
    }

    #endregion

    #region Target Detection

    private Transform FindBestTarget()
    {
        int resultCount =
            Physics.OverlapSphereNonAlloc(
                transform.position,
                targetRange,
                targetResults,
                targetLayers,
                QueryTriggerInteraction.Collide);

        Transform bestTarget =
            null;

        float bestScore =
            float.PositiveInfinity;

        Vector3 forward =
            transform.forward;

        for (int index = 0;
            index < resultCount;
            index++)
        {
            Collider candidate =
                targetResults[index];

            targetResults[index] =
                null;

            if (!IsValidTarget(
                    candidate))
            {
                continue;
            }

            Vector3 targetPosition =
                candidate.bounds.center;

            Vector3 direction =
                targetPosition -
                transform.position;

            float distance =
                direction.magnitude;

            if (!float.IsFinite(
                    distance) ||
                distance <= 0f ||
                distance >
                    targetRange)
            {
                continue;
            }

            float angle =
                Vector3.Angle(
                    forward,
                    direction);

            if (!float.IsFinite(
                    angle) ||
                angle >
                    targetAngle)
            {
                continue;
            }

            float score =
                distance +
                angle *
                0.05f;

            if (score >=
                bestScore)
            {
                continue;
            }

            bestScore =
                score;

            bestTarget =
                candidate.attachedRigidbody != null
                    ? candidate.attachedRigidbody.transform
                    : candidate.transform;
        }

        return bestTarget;
    }

    private bool IsValidTarget(
    Collider candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (body != null &&
            candidate.attachedRigidbody ==
                body)
        {
            return false;
        }

        AIController enemy =
            candidate.GetComponent<AIController>();

        enemy ??=
            candidate.GetComponentInParent<AIController>();

        enemy ??=
            candidate.GetComponentInChildren<AIController>();

        if (enemy == null ||
            enemy.IsDead ||
            !enemy.isActiveAndEnabled)
        {
            return false;
        }

        return true;
    }

    private static Vector3 GetTargetPosition(
        Transform target)
    {
        if (target == null)
        {
            return
                Vector3.zero;
        }

        Collider targetCollider =
            target.GetComponentInChildren<
                Collider>();

        return
            targetCollider != null
                ? targetCollider.bounds.center
                : target.position;
    }

    #endregion

    #region Hit Handling

    private void HandleTargetHit()
    {
        Transform hitTarget =
            currentTarget;

        if (hitTarget != null)
        {
            AIController enemy =
                hitTarget.GetComponent<AIController>();

            enemy ??=
                hitTarget.GetComponentInParent<AIController>();

            enemy ??=
                hitTarget.GetComponentInChildren<AIController>();

            if (enemy != null &&
                !enemy.IsDead)
            {
                enemy.TakeDamage(
                    homingAttackDamage);
            }
        }

        if (body != null)
        {
            body.useGravity =
                true;

            body.linearVelocity =
                Vector3.up *
                upwardBounceForce;
        }

        isHoming =
            false;

        currentTarget =
            null;

        SetSpinAnimation(
            false);

        if (allowChainAttack)
        {
            homingAttackUsed =
                false;

            homingAttackAvailable =
                true;
        }
    }

    private void FinishHomingAttack(
        bool resetAttack)
    {
        isHoming =
            false;

        currentTarget =
            null;

        if (body != null)
        {
            body.useGravity =
                true;
        }

        SetSpinAnimation(
            false);

        if (resetAttack)
        {
            homingAttackUsed =
                false;

            homingAttackAvailable =
                true;
        }
    }

    #endregion

    #region Rotation

    private void FaceDirection(
        Vector3 direction)
    {
        if (!IsFiniteVector(
                direction) ||
            direction.sqrMagnitude <=
                0.0001f)
        {
            return;
        }

        Vector3 forward =
            direction;

        forward.y =
            0f;

        if (forward.sqrMagnitude <=
            0.0001f)
        {
            return;
        }

        transform.rotation =
            Quaternion.LookRotation(
                forward.normalized,
                Vector3.up);
    }

    #endregion

    #region Reset

    private void ResetOnGround()
    {
        if (isHoming)
        {
            isHoming =
                false;

            currentTarget =
                null;

            if (body != null)
            {
                body.useGravity =
                    true;
            }
        }

        homingAttackUsed =
            false;

        homingAttackAvailable =
            true;

        SetSpinAnimation(
            false);
    }

    public void ResetHomingAttack()
    {
        isHoming =
            false;

        currentTarget =
            null;

        homingAttackUsed =
            false;

        homingAttackAvailable =
            true;

        if (body != null)
        {
            body.useGravity =
                true;
        }

        SetSpinAnimation(
            false);
    }

    private void CancelHomingAttack()
    {
        isHoming =
            false;

        currentTarget =
            null;

        if (body != null)
        {
            body.useGravity =
                true;
        }

        SetSpinAnimation(
            false);
    }

    #endregion

    #region Animation

    private void SetSpinAnimation(
        bool value)
    {
        if (anim == null ||
            !anim.isActiveAndEnabled ||
            string.IsNullOrWhiteSpace(
                spinParameter))
        {
            return;
        }

        anim.SetBool(
            spinParameter,
            value);
    }

    #endregion

    #region Validation

    private static bool IsFiniteVector(
        Vector3 value)
    {
        return
            float.IsFinite(
                value.x) &&
            float.IsFinite(
                value.y) &&
            float.IsFinite(
                value.z);
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            targetRange);

        if (currentTarget != null)
        {
            Gizmos.DrawLine(
                transform.position,
                GetTargetPosition(
                    currentTarget));
        }
    }

    #endregion
}