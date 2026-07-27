using UnityEngine;

/// <summary>
/// Sonic Heroes-style Triangle Dive.
///
/// Attach this component only to Test Player:
/// - Test Player must have a Rigidbody.
/// - Test Player must have UltimatePlayerMovement.
/// - Test Player should have TeamActionController.
///
/// The action is available only in Power Formation.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class TriangleDive : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private UltimatePlayerMovement movement;
    [SerializeField] private TeamActionController actionController;
    [SerializeField] private Rigidbody body;
    [SerializeField] private Transform cameraTransform;

    [Header("Team References")]
    [Tooltip("The visible Power character in the scene.")]
    [SerializeField] private Transform powerCharacter;

    [Tooltip("The visible Speed character in the scene.")]
    [SerializeField] private Transform speedCharacter;

    [Tooltip("The visible Fly character in the scene.")]
    [SerializeField] private Transform flyCharacter;

    [Header("Input")]
    [SerializeField] private KeyCode diveKey = KeyCode.Space;

    [Tooltip("Minimum time the player must be airborne before Triangle Dive can start.")]
    [SerializeField, Min(0f)]
    private float minimumAirTime = 0.1f;

    [Tooltip("End Triangle Dive when Space is released.")]
    [SerializeField]
    private bool requireButtonHeld = true;

    [Header("Dive Movement")]
    [SerializeField, Min(0f)]
    private float forwardSpeed = 10f;

    [SerializeField, Min(0f)]
    private float maximumForwardSpeed = 24f;

    [SerializeField, Min(0f)]
    private float fallSpeed = 2.5f;

    [SerializeField, Min(0f)]
    private float verticalAcceleration = 16f;

    [SerializeField, Min(0f)]
    private float steeringAcceleration = 16f;

    [SerializeField, Min(0f)]
    private float maximumSideSpeed = 7f;

    [SerializeField, Min(0f)]
    private float turningSharpness = 10f;

    [Header("Updraft")]
    [SerializeField] private LayerMask updraftLayers;

    [SerializeField, Min(0f)]
    private float updraftRiseSpeed = 12f;

    [SerializeField, Min(0f)]
    private float updraftAcceleration = 22f;

    [SerializeField, Min(0f)]
    private float updraftGraceTime = 0.08f;

    [Header("Triangle Formation")]
    [SerializeField]
    private Vector3 speedFormationOffset =
        new Vector3(-1.35f, 0.15f, -0.15f);

    [SerializeField]
    private Vector3 flyFormationOffset =
        new Vector3(1.35f, 0.15f, -0.15f);

    [SerializeField, Min(0f)]
    private float teammatePositionSharpness = 20f;

    [SerializeField, Min(0f)]
    private float teammateRotationSharpness = 15f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string triangleDiveBool = "Triangle Dive";

    [Header("Runtime Debug")]
    [SerializeField] private bool diving;
    [SerializeField] private bool insideUpdraft;
    [SerializeField] private bool jumpWasReleased;
    [SerializeField] private float airborneTime;

    private float lastUpdraftTime = float.NegativeInfinity;

    public bool IsDiving => diving;

    private bool UpdraftActive =>
        insideUpdraft ||
        Time.time - lastUpdraftTime <= updraftGraceTime;

    private void Awake()
    {
        if (movement == null)
            movement = GetComponent<UltimatePlayerMovement>();

        if (actionController == null)
            actionController = GetComponent<TeamActionController>();

        if (body == null)
            body = GetComponent<Rigidbody>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (animator == null && powerCharacter != null)
            animator = powerCharacter.GetComponentInChildren<Animator>();

        jumpWasReleased = true;
    }

    private void Update()
    {
        if (movement == null || body == null)
            return;

        UpdateAirborneState();

        if (!diving)
        {
            ReadStartInput();
            return;
        }

        if (ShouldStopDive())
            StopTriangleDive();
    }

    private void FixedUpdate()
    {
        if (!diving || body == null)
            return;

        ApplyDiveMovement();
        UpdateTriangleFormation();
    }

    private void UpdateAirborneState()
    {
        if (movement.isGrounded)
        {
            airborneTime = 0f;

            // While grounded, prepare for the normal jump.
            jumpWasReleased = !Input.GetKey(diveKey);
            return;
        }

        airborneTime += Time.deltaTime;

        // Triangle Dive cannot begin until Space has been released
        // after the initial jump.
        if (!Input.GetKey(diveKey))
            jumpWasReleased = true;
    }

    private void ReadStartInput()
    {
        if (movement.isGrounded)
            return;

        if (airborneTime < minimumAirTime)
            return;

        if (!jumpWasReleased)
            return;

        if (!Input.GetKeyDown(diveKey))
            return;

        TryStartTriangleDive();
    }

    public bool TryStartTriangleDive()
    {
        if (diving)
            return false;

        if (movement == null || body == null)
            return false;

        if (movement.isGrounded)
            return false;

        if (actionController != null)
        {
            bool accepted = actionController.TryBeginAction(
                TeamActionController.TeamAction.TriangleDive,
                TeamActionController.TeamFormation.Power,
                mustBeGrounded: false,
                mustBeAirborne: true,
                surrenderMovementControl: true);

            if (!accepted)
                return false;
        }
        else
        {
            UltimatePlayerMovement.Controllable = false;
        }

        diving = true;
        insideUpdraft = false;
        jumpWasReleased = false;
        lastUpdraftTime = float.NegativeInfinity;

        SetAnimation(true);

        return true;
    }

    private bool ShouldStopDive()
    {
        if (movement.isGrounded)
            return true;

        if (requireButtonHeld && !Input.GetKey(diveKey))
            return true;

        return false;
    }

    private void ApplyDiveMovement()
    {
        Vector3 currentVelocity = body.linearVelocity;

        Vector3 facingDirection =
            Vector3.ProjectOnPlane(transform.forward, Vector3.up);

        if (facingDirection.sqrMagnitude < 0.001f)
            facingDirection = Vector3.forward;

        facingDirection.Normalize();

        Vector3 horizontalVelocity =
            Vector3.ProjectOnPlane(currentVelocity, Vector3.up);

        float retainedForwardSpeed =
            Vector3.Dot(horizontalVelocity, facingDirection);

        retainedForwardSpeed = Mathf.Clamp(
            retainedForwardSpeed,
            forwardSpeed,
            maximumForwardSpeed);

        Vector3 cameraForward = facingDirection;
        Vector3 cameraRight = transform.right;

        if (cameraTransform != null)
        {
            cameraForward = Vector3.ProjectOnPlane(
                cameraTransform.forward,
                Vector3.up);

            cameraRight = Vector3.ProjectOnPlane(
                cameraTransform.right,
                Vector3.up);

            if (cameraForward.sqrMagnitude > 0.001f)
                cameraForward.Normalize();

            if (cameraRight.sqrMagnitude > 0.001f)
                cameraRight.Normalize();
        }

        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");

        Vector3 steeringDirection =
            cameraRight * horizontalInput +
            cameraForward * Mathf.Max(0f, verticalInput);

        if (steeringDirection.sqrMagnitude > 1f)
            steeringDirection.Normalize();

        Vector3 desiredHorizontalVelocity =
            facingDirection * retainedForwardSpeed +
            steeringDirection * maximumSideSpeed;

        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            desiredHorizontalVelocity,
            steeringAcceleration * Time.fixedDeltaTime);

        float targetVerticalSpeed =
            UpdraftActive ? updraftRiseSpeed : -fallSpeed;

        float acceleration =
            UpdraftActive
                ? updraftAcceleration
                : verticalAcceleration;

        float verticalSpeed = Mathf.MoveTowards(
            currentVelocity.y,
            targetVerticalSpeed,
            acceleration * Time.fixedDeltaTime);

        body.linearVelocity =
            horizontalVelocity +
            Vector3.up * verticalSpeed;

        RotateTowardMovement(horizontalVelocity);
    }

    private void RotateTowardMovement(Vector3 horizontalVelocity)
    {
        if (horizontalVelocity.sqrMagnitude < 0.01f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(
            horizontalVelocity.normalized,
            Vector3.up);

        float rotationAmount =
            1f - Mathf.Exp(
                -turningSharpness * Time.fixedDeltaTime);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationAmount);
    }

    private void UpdateTriangleFormation()
    {
        Transform center =
            powerCharacter != null
                ? powerCharacter
                : transform;

        float positionAmount =
            1f - Mathf.Exp(
                -teammatePositionSharpness *
                Time.fixedDeltaTime);

        float rotationAmount =
            1f - Mathf.Exp(
                -teammateRotationSharpness *
                Time.fixedDeltaTime);

        UpdateTeammate(
            speedCharacter,
            center,
            speedFormationOffset,
            positionAmount,
            rotationAmount);

        UpdateTeammate(
            flyCharacter,
            center,
            flyFormationOffset,
            positionAmount,
            rotationAmount);
    }

    private static void UpdateTeammate(
        Transform teammate,
        Transform center,
        Vector3 localOffset,
        float positionAmount,
        float rotationAmount)
    {
        if (teammate == null || center == null)
            return;

        Vector3 targetPosition =
            center.TransformPoint(localOffset);

        teammate.position = Vector3.Lerp(
            teammate.position,
            targetPosition,
            positionAmount);

        teammate.rotation = Quaternion.Slerp(
            teammate.rotation,
            center.rotation,
            rotationAmount);
    }

    public void StopTriangleDive()
    {
        if (!diving)
            return;

        diving = false;
        insideUpdraft = false;
        jumpWasReleased = false;

        SetAnimation(false);

        if (actionController != null &&
            actionController.CurrentAction ==
            TeamActionController.TeamAction.TriangleDive)
        {
            actionController.EndAction();
        }
        else
        {
            UltimatePlayerMovement.Controllable = true;
        }
    }

    public void CancelTriangleDive()
    {
        StopTriangleDive();
    }

    private void SetAnimation(bool active)
    {
        if (animator == null)
            return;

        if (string.IsNullOrWhiteSpace(triangleDiveBool))
            return;

        animator.SetBool(triangleDiveBool, active);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!diving || other == null)
            return;

        int otherLayer = 1 << other.gameObject.layer;

        if ((updraftLayers.value & otherLayer) == 0)
            return;

        insideUpdraft = true;
        lastUpdraftTime = Time.time;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null)
            return;

        int otherLayer = 1 << other.gameObject.layer;

        if ((updraftLayers.value & otherLayer) == 0)
            return;

        insideUpdraft = false;
        lastUpdraftTime = Time.time;
    }

    private void OnDisable()
    {
        if (diving)
            StopTriangleDive();
    }

    private void OnDestroy()
    {
        UltimatePlayerMovement.Controllable = true;
    }
}