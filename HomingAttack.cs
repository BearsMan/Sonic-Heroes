using UnityEngine;

[DisallowMultipleComponent]
public sealed class HomingAttack : MonoBehaviour
{
    #region Animator Hashes

    private static readonly int SpinHash =
        Animator.StringToHash("Spin");

    private static readonly int DiveRollHash =
        Animator.StringToHash("Dive Roll");

    #endregion

    #region Inspector

    [Header("References")]
    [SerializeField] private UltimatePlayerMovement movement;
    [SerializeField] private TeamActionController actionController;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Animator animator;

    [Header("Input")]
    [SerializeField] private KeyCode homingAttackKey = KeyCode.Space;
    [SerializeField] private bool readPlayerInput = true;

    [Header("Target Detection")]
    [SerializeField] private LayerMask targetLayerMask;
    [SerializeField, Min(0.1f)] private float targetDetectionRadius = 18f;
    [SerializeField, Range(0f, 180f)] private float targetDetectionAngle = 75f;
    [SerializeField]
    private Vector3 targetDetectionOffset =
        new(0f, 1f, 0f);

    [Header("Attack Movement")]
    [SerializeField, Min(0f)] private float homingSpeed = 32f;
    [SerializeField, Min(0f)] private float fallbackDashSpeed = 22f;
    [SerializeField, Min(0f)] private float rotationSmoothness = 18f;
    [SerializeField, Min(0.01f)] private float targetReachDistance = 1.25f;
    [SerializeField, Min(0.05f)] private float attackDuration = 0.8f;

    [Header("Reset")]
    [SerializeField] private bool resetOnGrounded = true;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    [SerializeField]
    private CharacterSwitch characterSwitch;

    #endregion

    #region Runtime State

    private readonly Collider[] targetResults = new Collider[32];

    private Transform currentTarget;

    private bool homingAttackAvailable = true;
    private bool homingAttackUsed;
    private bool isAttacking;
    private bool isInitialized;
    private bool isShuttingDown;
    private bool wasGrounded;

    private float attackEndTime;

    #endregion

    #region Public API

    public bool HomingAttackAvailable => homingAttackAvailable;
    public bool HomingAttackUsed => homingAttackUsed;
    public bool IsAttacking => isAttacking;
    public bool IsInitialized => isInitialized;
    public Transform CurrentTarget => currentTarget;

    public bool TryStartHomingAttack()
    {
        if (!CanStartHomingAttack())
            return false;

        bool actionAccepted =
            actionController == null ||
            actionController.TryBeginAction(
                TeamActionController.TeamAction.HomingAttack,
                TeamActionController.TeamFormation.Speed,
                mustBeGrounded: false,
                mustBeAirborne: true,
                surrenderMovementControl: true);

        if (!actionAccepted)
            return false;

        currentTarget =
            FindBestTarget();

        homingAttackUsed = true;
        homingAttackAvailable = false;
        isAttacking = true;

        attackEndTime =
            Time.time +
            attackDuration;

        playerRigidbody.useGravity = false;

        SetAttackAnimation(
            active: true);

        LogStateChange(
            currentTarget != null
                ? $"Started Homing Attack toward '{currentTarget.name}'."
                : "Started fallback Homing Attack dash.");

        return true;
    }

    public void ResetHomingAttack()
    {
        EndHomingAttack(
            restoreAvailability: true,
            restoreMovementControl: true);
    }

    public void CancelHomingAttack()
    {
        EndHomingAttack(
            restoreAvailability: false,
            restoreMovementControl: true);
    }

    public void SetInputEnabled(
        bool enabled)
    {
        readPlayerInput = enabled;
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheComponents();
        ResolveReferences();
    }

    private void Start()
    {
        if (!InitializeHomingAttack())
        {
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (isShuttingDown)
            return;

        CacheComponents();
        ResolveReferences();

        if (!isInitialized)
            return;

        RestoreRuntimeState();
    }

    private void Update()
    {
        if (!isInitialized)
            return;

        UpdateGroundedReset();

        if (!readPlayerInput ||
            isAttacking)
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
        if (!isInitialized ||
            !isAttacking)
        {
            return;
        }

        UpdateHomingMovement();
    }

    private void OnDisable()
    {
        CleanupRuntimeState();
    }

    private void OnDestroy()
    {
        isShuttingDown = true;
        CleanupDestroyedState();
    }

    private void OnValidate()
    {
        targetDetectionRadius =
            Mathf.Max(
                0.1f,
                targetDetectionRadius);

        targetDetectionAngle =
            Mathf.Clamp(
                targetDetectionAngle,
                0f,
                180f);

        homingSpeed =
            Mathf.Max(
                0f,
                homingSpeed);

        fallbackDashSpeed =
            Mathf.Max(
                0f,
                fallbackDashSpeed);

        rotationSmoothness =
            Mathf.Max(
                0f,
                rotationSmoothness);

        targetReachDistance =
            Mathf.Max(
                0.01f,
                targetReachDistance);

        attackDuration =
            Mathf.Max(
                0.05f,
                attackDuration);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin =
            transform.position +
            transform.TransformDirection(
                targetDetectionOffset);

        Gizmos.DrawWireSphere(
            origin,
            targetDetectionRadius);

        if (currentTarget != null)
        {
            Gizmos.DrawLine(
                origin,
                currentTarget.position);
        }
    }

    #endregion

    #region Initialization

    public bool InitializeHomingAttack()
    {
        if (isInitialized)
            return true;

        CacheComponents();
        ResolveReferences();

        if (!ValidateConfiguration())
        {
            Debug.LogError(
                $"HomingAttack failed to initialize on '{name}'.",
                this);

            isInitialized = false;
            return false;
        }

        ResetRuntimeState();

        wasGrounded =
            movement.IsGrounded;

        isInitialized = true;
        return true;
    }

    private void CacheComponents()
    {
        if (movement == null)
        {
            movement =
                GetComponent<UltimatePlayerMovement>();
        }

        if (movement == null)
        {
            movement =
                GetComponentInParent<UltimatePlayerMovement>();
        }

        if (movement == null)
        {
            movement =
                GetComponentInChildren<UltimatePlayerMovement>(
                    includeInactive: true);
        }

        if (playerRigidbody == null)
        {
            playerRigidbody =
                GetComponent<Rigidbody>();
        }

        if (playerRigidbody == null)
        {
            playerRigidbody =
                GetComponentInParent<Rigidbody>();
        }

        if (playerRigidbody == null)
        {
            playerRigidbody =
                GetComponentInChildren<Rigidbody>(
                    includeInactive: true);
        }
    }

    private void ResolveReferences()
    {
        animator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        if (animator == null &&
            movement != null)
        {
            animator =
                movement.GetComponentInChildren<Animator>(
                    includeInactive: true);
        }
    }

    private void RestoreRuntimeState()
    {
        if (!isAttacking)
        {
            SetAttackAnimation(
                active: false);
        }
    }

    #endregion

    #region State

    private bool CanStartHomingAttack()
    {
        if (!CanUseHomingAttack())
        {
            return false;
        }

        return
            isInitialized &&
            homingAttackAvailable &&
            !homingAttackUsed &&
            !isAttacking &&
            movement != null &&
            !movement.IsGrounded &&
            playerRigidbody != null;
    }

    private bool CanUseHomingAttack()
    {
        return
            TeamSetup.Instance != null &&
            TeamSetup.Instance.PlayableTeam == PlayableTeam.TeamSonic &&
            characterSwitch != null &&
            characterSwitch.CurrentLeaderType == CHARACTERTYPES.Speed &&
            actionController != null &&
            actionController.CurrentFormation ==
                TeamActionController.TeamFormation.Speed;
    }

    private void UpdateGroundedReset()
    {
        bool isGrounded =
            movement != null &&
            movement.IsGrounded;

        if (resetOnGrounded &&
            isGrounded &&
            !wasGrounded)
        {
            ResetHomingAttack();
        }

        wasGrounded = isGrounded;
    }

    private void EndHomingAttack(
        bool restoreAvailability,
        bool restoreMovementControl)
    {
        bool wasActive =
            isAttacking ||
            homingAttackUsed;

        isAttacking = false;
        currentTarget = null;

        if (playerRigidbody != null)
        {
            playerRigidbody.useGravity = true;
        }

        SetAttackAnimation(
            active: false);

        if (restoreAvailability)
        {
            homingAttackUsed = false;
            homingAttackAvailable = true;
        }

        if (restoreMovementControl)
        {
            actionController?.EndAction(
                restoreMovementControl: true);
        }

        if (wasActive)
        {
            LogStateChange(
                restoreAvailability
                    ? "Homing Attack reset."
                    : "Homing Attack ended.");
        }
    }

    private void ResetRuntimeState()
    {
        currentTarget = null;

        homingAttackAvailable = true;
        homingAttackUsed = false;
        isAttacking = false;

        attackEndTime = 0f;

        SetAttackAnimation(
            active: false);
    }

    #endregion

    #region Target Detection

    private Transform FindBestTarget()
    {
        if (targetLayerMask.value == 0)
            return null;

        Vector3 origin =
            playerRigidbody.position +
            transform.TransformDirection(
                targetDetectionOffset);

        int resultCount =
            Physics.OverlapSphereNonAlloc(
                origin,
                targetDetectionRadius,
                targetResults,
                targetLayerMask,
                QueryTriggerInteraction.Ignore);

        Transform bestTarget = null;
        float bestScore = float.MaxValue;

        for (int index = 0;
             index < resultCount;
             index++)
        {
            Collider candidateCollider =
                targetResults[index];

            targetResults[index] = null;

            if (!TryEvaluateTarget(
                    candidateCollider,
                    origin,
                    out Transform candidate,
                    out float score))
            {
                continue;
            }

            if (score >= bestScore)
                continue;

            bestScore = score;
            bestTarget = candidate;
        }

        return bestTarget;
    }

    private bool TryEvaluateTarget(
        Collider candidateCollider,
        Vector3 origin,
        out Transform candidate,
        out float score)
    {
        candidate = null;
        score = float.MaxValue;

        if (candidateCollider == null)
            return false;

        Transform candidateTransform =
            candidateCollider.transform;

        if (candidateTransform == transform ||
            candidateTransform.IsChildOf(transform) ||
            transform.IsChildOf(candidateTransform))
        {
            return false;
        }

        Vector3 toCandidate =
            candidateCollider.bounds.center -
            origin;

        float distance =
            toCandidate.magnitude;

        if (distance <= Mathf.Epsilon)
            return false;

        float angle =
            Vector3.Angle(
                transform.forward,
                toCandidate);

        if (angle > targetDetectionAngle)
            return false;

        candidate = candidateTransform;

        score =
            distance +
            angle /
            Mathf.Max(
                1f,
                targetDetectionAngle);

        return true;
    }

    #endregion

    #region Movement

    private void UpdateHomingMovement()
    {
        if (Time.time >= attackEndTime)
        {
            EndHomingAttack(
                restoreAvailability: false,
                restoreMovementControl: true);

            return;
        }

        if (currentTarget != null)
        {
            MoveTowardTarget();
            return;
        }

        MoveForward();
    }

    private void MoveTowardTarget()
    {
        Vector3 targetPosition =
            currentTarget.position;

        Vector3 toTarget =
            targetPosition -
            playerRigidbody.position;

        float distance =
            toTarget.magnitude;

        if (distance <= targetReachDistance)
        {
            EndHomingAttack(
                restoreAvailability: false,
                restoreMovementControl: true);

            return;
        }

        if (distance <= Mathf.Epsilon)
            return;

        Vector3 direction =
            toTarget /
            distance;

        ApplyAttackVelocity(
            direction,
            homingSpeed);

        RotateToward(
            direction);
    }

    private void MoveForward()
    {
        Vector3 direction =
            transform.forward;

        ApplyAttackVelocity(
            direction,
            fallbackDashSpeed);

        RotateToward(
            direction);
    }

    private void ApplyAttackVelocity(
        Vector3 direction,
        float speed)
    {
        playerRigidbody.linearVelocity =
            direction *
            speed;
    }

    private void RotateToward(
        Vector3 direction)
    {
        if (direction.sqrMagnitude <=
            0.001f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction,
                Vector3.up);

        if (rotationSmoothness <= 0f)
        {
            playerRigidbody.MoveRotation(
                targetRotation);

            return;
        }

        float blend =
            1f -
            Mathf.Exp(
                -rotationSmoothness *
                Time.fixedDeltaTime);

        playerRigidbody.MoveRotation(
            Quaternion.Slerp(
                playerRigidbody.rotation,
                targetRotation,
                blend));
    }

    #endregion

    #region Animation

    private void SetAttackAnimation(
        bool active)
    {
        if (animator == null)
            return;

        animator.SetBool(
            SpinHash,
            active);

        if (!active)
        {
            animator.SetBool(
                DiveRollHash,
                false);
        }
    }

    #endregion

    #region Validation

    private bool ValidateConfiguration()
    {
        bool valid = true;

        valid &=
            ValidateReference(
                movement,
                nameof(UltimatePlayerMovement));

        valid &=
            ValidateReference(
                playerRigidbody,
                nameof(Rigidbody));

        valid &=
            ValidateReference(
                characterSwitch,
                nameof(CharacterSwitch));

        if (actionController == null)
        {
            Debug.LogWarning(
                "HomingAttack could not find TeamActionController. " +
                "Formation and action locking will not be enforced.",
                this);
        }

        if (animator == null)
        {
            Debug.LogWarning(
                "HomingAttack could not find an Animator.",
                this);
        }

        if (targetLayerMask.value == 0)
        {
            Debug.LogWarning(
                "HomingAttack target layer mask is empty. " +
                "The attack will use its forward dash fallback.",
                this);
        }

        return valid;
    }

    private bool ValidateReference(
        Object reference,
        string displayName)
    {
        if (reference != null)
            return true;

        Debug.LogError(
            $"HomingAttack requires {displayName}.",
            this);

        return false;
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        if (isAttacking)
        {
            EndHomingAttack(
                restoreAvailability: false,
                restoreMovementControl: true);
        }

        SetAttackAnimation(
            active: false);
    }

    private void CleanupDestroyedState()
    {
        CleanupRuntimeState();

        isInitialized = false;

        currentTarget = null;
        movement = null;
        actionController = null;
        playerRigidbody = null;
        animator = null;
        characterSwitch = null;
    }

    #endregion

    #region Debug

    private void LogStateChange(
        string message)
    {
        if (!logStateChanges)
            return;

        Debug.Log(
            message,
            this);
    }

    #endregion
}
