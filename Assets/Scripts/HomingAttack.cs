using UnityEngine;

public class HomingAttack : MonoBehaviour
{
    #region References

    [Header("References")]
    [SerializeField]
    private UltimatePlayerMovement movement;

    [SerializeField]
    private TeamActionController actionController;

    [SerializeField]
    private Rigidbody body;

    [SerializeField]
    private Animator animator;

    #endregion

    #region Input

    [Header("Input")]
    [SerializeField]
    private KeyCode homingAttackKey =
        KeyCode.Space;

    [SerializeField]
    private bool readPlayerInput = true;

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

    [SerializeField]
    private Vector3 detectionOffset =
        new Vector3(
            0f,
            1f,
            0f);

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

    #region Rotation

    [Header("Rotation")]
    [SerializeField, Min(0f)]
    private float rotationSharpness = 20f;

    #endregion

    #region Animation

    [Header("Animation")]
    [SerializeField]
    private string spinParameter =
        "Spin";

    #endregion

    #region Runtime

    private readonly Collider[] targetResults =
        new Collider[32];

    private Transform currentTarget;

    private float homingTimer;

    private bool homingAttackAvailable = true;
    private bool homingAttackUsed;
    private bool isHoming;
    private bool initialized;
    private bool shuttingDown;

    #endregion

    #region Properties

    public bool HomingAttackAvailable =>
        homingAttackAvailable;

    public bool HomingAttackUsed =>
        homingAttackUsed;

    public bool IsHoming =>
        isHoming;

    public Transform CurrentTarget =>
        currentTarget;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheReferences();

        initialized =
            ValidateReferences();

        ResetRuntimeState();
    }

    private void OnEnable()
    {
        if (shuttingDown)
        {
            return;
        }

        CacheReferences();

        if (!initialized)
        {
            initialized =
                ValidateReferences();
        }
    }

    private void Update()
    {
        if (!initialized)
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
            return;
        }

        if (!readPlayerInput)
        {
            return;
        }

        if (Input.GetKeyDown(
            homingAttackKey))
        {
            TryStartHomingAttack();
        }
    }

    private void FixedUpdate()
    {
        if (!initialized ||
            !isHoming)
        {
            return;
        }

        UpdateHomingAttack();
    }

    private void OnDisable()
    {
        if (shuttingDown)
        {
            return;
        }

        CancelHomingAttack();
    }

    private void OnDestroy()
    {
        shuttingDown = true;

        CleanupDestroyedState();
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
            Mathf.Clamp(
                maximumTargets,
                1,
                targetResults.Length);

        homingAttackDamage =
            Mathf.Max(
                1,
                homingAttackDamage);

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

        rotationSharpness =
            Mathf.Max(
                0f,
                rotationSharpness);
    }

    #endregion

    #region Public API

    public bool TryStartHomingAttack()
    {
        if (!CanStartHomingAttack())
        {
            return false;
        }

        Transform target =
            FindBestTarget();

        if (target == null)
        {
            return false;
        }

        bool continuingChain =
            actionController.CurrentAction ==
            TeamActionController.TeamAction.HomingAttack;

        if (!continuingChain)
        {
            bool accepted =
                actionController.TryBeginAction(
                    TeamActionController.TeamAction.HomingAttack,
                    TeamActionController.TeamFormation.Speed,
                    mustBeGrounded: false,
                    mustBeAirborne: true,
                    surrenderMovementControl: true);

            if (!accepted)
            {
                return false;
            }
        }

        currentTarget =
            target;

        homingAttackUsed =
            true;

        homingAttackAvailable =
            false;

        isHoming =
            true;

        homingTimer =
            maximumHomingTime;

        PrepareHomingPhysics();

        SetSpinAnimation(
            true);

        return true;
    }

    public void ResetHomingAttack()
    {
        FinishHomingAttack(
            restoreAvailability: true,
            endTeamAction: true,
            restoreMovementControl: true);
    }

    public void CancelHomingAttack()
    {
        FinishHomingAttack(
            restoreAvailability: false,
            endTeamAction: true,
            restoreMovementControl: true);
    }

    public void SetInputEnabled(
        bool enabled)
    {
        readPlayerInput =
            enabled;
    }

    #endregion

    #region Start Validation

    private bool CanStartHomingAttack()
    {
        if (!initialized ||
            movement == null ||
            actionController == null ||
            body == null)
        {
            return false;
        }

        if (movement.IsGrounded)
        {
            return false;
        }

        if (isHoming ||
            !homingAttackAvailable ||
            homingAttackUsed)
        {
            return false;
        }

        if (actionController.CurrentFormation !=
            TeamActionController.TeamFormation.Speed)
        {
            return false;
        }

        TeamActionController.TeamAction action =
            actionController.CurrentAction;

        if (action !=
                TeamActionController.TeamAction.None &&
            action !=
                TeamActionController.TeamAction.HomingAttack)
        {
            return false;
        }

        if (!IsFinite(
            body.position) ||
            !IsFinite(
                body.rotation))
        {
            return false;
        }

        return true;
    }

    #endregion

    #region Homing Movement

    private void PrepareHomingPhysics()
    {
        if (body == null)
        {
            return;
        }

        body.useGravity =
            false;

        body.linearVelocity =
            Vector3.zero;

        body.angularVelocity =
            Vector3.zero;
    }

    private void UpdateHomingAttack()
    {
        if (body == null)
        {
            CancelHomingAttack();

            return;
        }

        if (currentTarget == null ||
            !currentTarget.gameObject.activeInHierarchy)
        {
            FinishHomingAttack(
                restoreAvailability: false,
                endTeamAction: true,
                restoreMovementControl: true);

            return;
        }

        homingTimer -=
            Time.fixedDeltaTime;

        if (!float.IsFinite(
                homingTimer) ||
            homingTimer <= 0f)
        {
            FinishHomingAttack(
                restoreAvailability: false,
                endTeamAction: true,
                restoreMovementControl: true);

            return;
        }

        Vector3 targetPosition =
            GetTargetPosition(
                currentTarget);

        Vector3 direction =
            targetPosition -
            body.position;

        if (!IsFinite(
                direction))
        {
            CancelHomingAttack();

            return;
        }

        float distance =
            direction.magnitude;

        if (!float.IsFinite(
                distance))
        {
            CancelHomingAttack();

            return;
        }

        if (distance <=
            hitDistance)
        {
            HandleTargetHit();

            return;
        }

        if (direction.sqrMagnitude <=
            Mathf.Epsilon)
        {
            HandleTargetHit();

            return;
        }

        direction.Normalize();

        Vector3 velocity =
            direction *
            homingSpeed;

        if (!IsFinite(
                velocity))
        {
            CancelHomingAttack();

            return;
        }

        body.linearVelocity =
            velocity;

        RotateTowardDirection(
            direction);
    }

    #endregion

    #region Rotation

    private void RotateTowardDirection(
        Vector3 direction)
    {
        Vector3 horizontal =
            Vector3.ProjectOnPlane(
                direction,
                Vector3.up);

        if (!IsFinite(
                horizontal) ||
            horizontal.sqrMagnitude <=
            Mathf.Epsilon)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                horizontal.normalized,
                Vector3.up);

        float amount =
            1f -
            Mathf.Exp(
                -rotationSharpness *
                Time.fixedDeltaTime);

        Quaternion rotation =
            Quaternion.Slerp(
                body.rotation,
                targetRotation,
                amount);

        if (!IsFinite(
                rotation))
        {
            return;
        }

        body.MoveRotation(
            rotation);
    }

    #endregion

    #region Target Detection

    private Transform FindBestTarget()
    {
        Vector3 origin =
            transform.position +
            transform.TransformDirection(
                detectionOffset);

        if (!IsFinite(
                origin))
        {
            return null;
        }

        int requestedCount =
            Mathf.Min(
                maximumTargets,
                targetResults.Length);

        int resultCount =
            Physics.OverlapSphereNonAlloc(
                origin,
                targetRange,
                targetResults,
                targetLayers,
                QueryTriggerInteraction.Collide);

        resultCount =
            Mathf.Min(
                resultCount,
                requestedCount);

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

            if (!TryEvaluateTarget(
                candidate,
                origin,
                forward,
                out Transform target,
                out float score))
            {
                continue;
            }

            if (score >=
                bestScore)
            {
                continue;
            }

            bestScore =
                score;

            bestTarget =
                target;
        }

        return bestTarget;
    }

    private bool TryEvaluateTarget(
        Collider candidate,
        Vector3 origin,
        Vector3 forward,
        out Transform target,
        out float score)
    {
        target =
            null;

        score =
            float.PositiveInfinity;

        if (!IsValidTarget(
            candidate))
        {
            return false;
        }

        Vector3 targetPosition =
            candidate.bounds.center;

        Vector3 direction =
            targetPosition -
            origin;

        if (!IsFinite(
                direction))
        {
            return false;
        }

        float distance =
            direction.magnitude;

        if (!float.IsFinite(
                distance) ||
            distance <= Mathf.Epsilon ||
            distance > targetRange)
        {
            return false;
        }

        float angle =
            Vector3.Angle(
                forward,
                direction);

        if (!float.IsFinite(
                angle) ||
            angle > targetAngle)
        {
            return false;
        }

        score =
            distance +
            angle *
            0.05f;

        target =
            candidate.attachedRigidbody != null
                ? candidate.attachedRigidbody.transform
                : candidate.transform;

        return target != null;
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

        Transform candidateTransform =
            candidate.transform;

        if (candidateTransform == transform ||
            candidateTransform.IsChildOf(
                transform) ||
            transform.IsChildOf(
                candidateTransform))
        {
            return false;
        }

        AIController enemy =
            candidate.GetComponent<AIController>();

        if (enemy == null)
        {
            enemy =
                candidate.GetComponentInParent<
                    AIController>();
        }

        if (enemy == null)
        {
            enemy =
                candidate.GetComponentInChildren<
                    AIController>();
        }

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
            return Vector3.zero;
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
        DamageCurrentTarget();

        if (body != null)
        {
            body.useGravity =
                true;

            body.linearVelocity =
                Vector3.up *
                upwardBounceForce;

            body.angularVelocity =
                Vector3.zero;
        }

        isHoming =
            false;

        currentTarget =
            null;

        homingTimer =
            0f;

        SetSpinAnimation(
            false);

        if (allowChainAttack)
        {
            /*
             * Keep TeamAction.HomingAttack active
             * between chained targets.
             *
             * A Triangle Jump surface may call
             * CancelHomingAttack(), which releases
             * this action before taking ownership.
             */
            homingAttackUsed =
                false;

            homingAttackAvailable =
                true;

            return;
        }

        homingAttackUsed =
            true;

        homingAttackAvailable =
            false;

        EndTeamAction(
            restoreMovementControl: true);
    }

    private void DamageCurrentTarget()
    {
        if (currentTarget == null)
        {
            return;
        }

        AIController enemy =
            currentTarget.GetComponent<
                AIController>();

        if (enemy == null)
        {
            enemy =
                currentTarget.GetComponentInParent<
                    AIController>();
        }

        if (enemy == null)
        {
            enemy =
                currentTarget.GetComponentInChildren<
                    AIController>();
        }

        if (enemy == null ||
            enemy.IsDead)
        {
            return;
        }

        enemy.TakeDamage(
            homingAttackDamage);
    }

    #endregion

    #region Finish / Reset

    private void FinishHomingAttack(
        bool restoreAvailability,
        bool endTeamAction,
        bool restoreMovementControl)
    {
        isHoming =
            false;

        currentTarget =
            null;

        homingTimer =
            0f;

        if (body != null)
        {
            body.useGravity =
                true;

            body.angularVelocity =
                Vector3.zero;
        }

        SetSpinAnimation(
            false);

        if (restoreAvailability)
        {
            homingAttackUsed =
                false;

            homingAttackAvailable =
                true;
        }

        if (endTeamAction)
        {
            EndTeamAction(
                restoreMovementControl);
        }
    }

    private void ResetOnGround()
    {
        if (isHoming ||
            actionController.CurrentAction ==
                TeamActionController.TeamAction.HomingAttack)
        {
            FinishHomingAttack(
                restoreAvailability: true,
                endTeamAction: true,
                restoreMovementControl: true);

            return;
        }

        homingAttackUsed =
            false;

        homingAttackAvailable =
            true;

        currentTarget =
            null;

        homingTimer =
            0f;

        SetSpinAnimation(
            false);
    }

    private void ResetRuntimeState()
    {
        currentTarget =
            null;

        homingTimer =
            0f;

        isHoming =
            false;

        homingAttackAvailable =
            true;

        homingAttackUsed =
            false;

        SetSpinAnimation(
            false);
    }

    #endregion

    #region Team Action

    private void EndTeamAction(
        bool restoreMovementControl)
    {
        if (actionController == null)
        {
            if (restoreMovementControl)
            {
                movement?.EnableMovement();
            }

            return;
        }

        if (actionController.CurrentAction !=
            TeamActionController.TeamAction.HomingAttack)
        {
            return;
        }

        actionController.EndAction(
            restoreMovementControl);
    }

    #endregion

    #region Animation

    private void SetSpinAnimation(
        bool value)
    {
        if (animator == null ||
            !animator.isActiveAndEnabled ||
            string.IsNullOrWhiteSpace(
                spinParameter))
        {
            return;
        }

        animator.SetBool(
            spinParameter,
            value);
    }

    #endregion

    #region References

    private void CacheReferences()
    {
        if (movement == null)
        {
            movement =
                GetComponentInParent<
                    UltimatePlayerMovement>();
        }

        if (actionController == null)
        {
            actionController =
                GetComponentInParent<
                    TeamActionController>();
        }

        if (body == null)
        {
            body =
                GetComponentInParent<
                    Rigidbody>();
        }

        if (animator == null)
        {
            animator =
                GetComponentInChildren<
                    Animator>();
        }
    }

    private bool ValidateReferences()
    {
        return
            movement != null &&
            actionController != null &&
            body != null;
    }

    #endregion

    #region Cleanup

    private void CleanupDestroyedState()
    {
        isHoming =
            false;

        homingAttackAvailable =
            false;

        homingAttackUsed =
            false;

        currentTarget =
            null;

        homingTimer =
            0f;

        movement =
            null;

        actionController =
            null;

        body =
            null;

        animator =
            null;

        initialized =
            false;
    }

    #endregion

    #region Safety

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

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Vector3 origin =
            transform.position +
            transform.TransformDirection(
                detectionOffset);

        Gizmos.DrawWireSphere(
            origin,
            targetRange);

        if (currentTarget != null)
        {
            Gizmos.DrawLine(
                origin,
                GetTargetPosition(
                    currentTarget));
        }
    }

    #endregion
}