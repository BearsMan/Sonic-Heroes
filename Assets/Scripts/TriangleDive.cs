using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TriangleDive : MonoBehaviour
{
    #region References

    [Header("Core References")]
    [SerializeField]
    private UltimatePlayerMovement movement;

    [SerializeField]
    private TeamActionController actionController;

    [SerializeField]
    private Rigidbody body;

    [SerializeField]
    private Transform cameraTransform;

    [Header("Team")]
    [SerializeField]
    private Transform powerCharacter;

    [SerializeField]
    private Transform speedCharacter;

    [SerializeField]
    private Transform flyCharacter;

    #endregion

    #region Input

    [Header("Input")]
    [SerializeField]
    private KeyCode diveKey = KeyCode.Space;

    [SerializeField, Min(0f)]
    private float minimumAirTime = 0.1f;

    [SerializeField]
    private bool requireButtonHeld = true;

    #endregion

    #region Dive Movement

    [Header("Dive Movement")]
    [SerializeField, Min(0f)]
    private float minimumForwardSpeed = 10f;

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

    #endregion

    #region Updraft

    [Header("Updraft")]
    [SerializeField]
    private LayerMask updraftLayers;

    [SerializeField, Min(0f)]
    private float updraftRiseSpeed = 12f;

    [SerializeField, Min(0f)]
    private float updraftAcceleration = 22f;

    [SerializeField, Min(0f)]
    private float updraftGraceTime = 0.08f;

    #endregion

    #region Triangle Formation

    [Header("Triangle Formation")]
    [SerializeField, Min(0.1f)]
    private float triangleRadius = 1.8f;

    [SerializeField]
    private float powerAngle = 0f;

    [SerializeField]
    private float speedAngle = 120f;

    [SerializeField]
    private float flyAngle = 240f;

    [SerializeField]
    private Vector3 formationTilt =
        new Vector3(0f, 0f, 0f);

    [SerializeField, Min(0f)]
    private float teammatePositionSharpness = 20f;

    [SerializeField, Min(0f)]
    private float teammateRotationSharpness = 15f;

    [SerializeField, Min(0f)]
    private float centerHeightOffset = 0.15f;

    #endregion

    #region Pose

    [Header("Pose")]
    [SerializeField]
    private Animator powerAnimator;

    [SerializeField]
    private Animator speedAnimator;

    [SerializeField]
    private Animator flyAnimator;

    [SerializeField]
    private string triangleDiveState = "Triangle Dive";

    [SerializeField]
    private bool playTriangleDiveAnimation = true;

    #endregion

    #region Runtime

    [Header("Runtime")]
    [SerializeField]
    private bool diving;

    [SerializeField]
    private bool insideUpdraft;

    private bool jumpWasReleased;
    private float airborneTime;
    private float lastUpdraftTime =
        float.NegativeInfinity;

    #endregion

    #region Properties

    public bool IsDiving =>
        diving;

    private bool UpdraftActive =>
        insideUpdraft ||
        Time.time - lastUpdraftTime <= updraftGraceTime;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheReferences();

        jumpWasReleased = true;
    }

    private void Update()
    {
        if (!CanOperate())
        {
            return;
        }

        UpdateAirborneState();

        if (!diving)
        {
            ReadStartInput();
            return;
        }

        if (ShouldStopDive())
        {
            StopTriangleDive();
        }
    }

    private void FixedUpdate()
    {
        if (!diving ||
            body == null)
        {
            return;
        }

        ApplyDiveMovement();
        UpdateTriangleFormation();
    }

    private void OnDisable()
    {
        if (diving)
        {
            StopTriangleDive();
        }
    }

    #endregion

    #region Setup

    private void CacheReferences()
    {
        if (movement == null)
        {
            movement =
                GetComponent<UltimatePlayerMovement>();
        }

        if (actionController == null)
        {
            actionController =
                GetComponent<TeamActionController>();
        }

        if (body == null)
        {
            body =
                GetComponent<Rigidbody>();
        }

        if (cameraTransform == null &&
            Camera.main != null)
        {
            cameraTransform =
                Camera.main.transform;
        }

        if (powerAnimator == null &&
            powerCharacter != null)
        {
            powerAnimator =
                powerCharacter.GetComponentInChildren<Animator>();
        }

        if (speedAnimator == null &&
            speedCharacter != null)
        {
            speedAnimator =
                speedCharacter.GetComponentInChildren<Animator>();
        }

        if (flyAnimator == null &&
            flyCharacter != null)
        {
            flyAnimator =
                flyCharacter.GetComponentInChildren<Animator>();
        }
    }

    private bool CanOperate()
    {
        if (movement == null ||
            body == null)
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

    #endregion

    #region Input

    private void UpdateAirborneState()
    {
        if (movement.isGrounded)
        {
            airborneTime = 0f;

            jumpWasReleased =
                !Input.GetKey(diveKey);

            return;
        }

        airborneTime +=
            Time.deltaTime;

        if (!Input.GetKey(diveKey))
        {
            jumpWasReleased =
                true;
        }
    }

    private void ReadStartInput()
    {
        if (movement.isGrounded)
        {
            return;
        }

        if (airborneTime <
            minimumAirTime)
        {
            return;
        }

        if (!jumpWasReleased)
        {
            return;
        }

        if (!Input.GetKeyDown(
            diveKey))
        {
            return;
        }

        TryStartTriangleDive();
    }

    #endregion

    #region Start / Stop

    public bool TryStartTriangleDive()
    {
        if (diving)
        {
            return false;
        }

        if (!CanOperate())
        {
            return false;
        }

        if (movement.isGrounded)
        {
            return false;
        }

        if (actionController != null)
        {
            bool accepted =
                actionController.TryBeginAction(
                    TeamActionController.TeamAction.TriangleDive,
                    TeamActionController.TeamFormation.Power,
                    mustBeGrounded: false,
                    mustBeAirborne: true,
                    surrenderMovementControl: true);

            if (!accepted)
            {
                return false;
            }
        }
        else
        {
            movement.DisableMovement();
        }

        diving = true;
        insideUpdraft = false;
        jumpWasReleased = false;
        lastUpdraftTime =
            float.NegativeInfinity;

        PlayPose();

        return true;
    }

    public void StopTriangleDive()
    {
        if (!diving)
        {
            return;
        }

        diving = false;
        insideUpdraft = false;
        jumpWasReleased = false;

        StopPose();

        if (actionController != null &&
            actionController.CurrentAction ==
            TeamActionController.TeamAction.TriangleDive)
        {
            actionController.EndAction();
        }
        else if (movement != null)
        {
            movement.EnableMovement();
        }
    }

    public void CancelTriangleDive()
    {
        StopTriangleDive();
    }

    private bool ShouldStopDive()
    {
        if (movement.isGrounded)
        {
            return true;
        }

        if (requireButtonHeld &&
            !Input.GetKey(diveKey))
        {
            return true;
        }

        return false;
    }

    #endregion

    #region Movement

    private void ApplyDiveMovement()
    {
        Vector3 currentVelocity =
            body.linearVelocity;

        Vector3 facingDirection =
            Vector3.ProjectOnPlane(
                transform.forward,
                Vector3.up);

        if (facingDirection.sqrMagnitude <
            0.001f)
        {
            facingDirection =
                Vector3.forward;
        }

        facingDirection.Normalize();

        Vector3 horizontalVelocity =
            Vector3.ProjectOnPlane(
                currentVelocity,
                Vector3.up);

        float retainedForwardSpeed =
            Vector3.Dot(
                horizontalVelocity,
                facingDirection);

        retainedForwardSpeed =
            Mathf.Clamp(
                retainedForwardSpeed,
                minimumForwardSpeed,
                maximumForwardSpeed);

        Vector3 cameraForward =
            facingDirection;

        Vector3 cameraRight =
            transform.right;

        if (cameraTransform != null)
        {
            cameraForward =
                Vector3.ProjectOnPlane(
                    cameraTransform.forward,
                    Vector3.up);

            cameraRight =
                Vector3.ProjectOnPlane(
                    cameraTransform.right,
                    Vector3.up);

            if (cameraForward.sqrMagnitude >
                0.001f)
            {
                cameraForward.Normalize();
            }

            if (cameraRight.sqrMagnitude >
                0.001f)
            {
                cameraRight.Normalize();
            }
        }

        float horizontalInput =
            Input.GetAxisRaw("Horizontal");

        float verticalInput =
            Input.GetAxisRaw("Vertical");

        Vector3 steeringDirection =
            cameraRight *
            horizontalInput +
            cameraForward *
            Mathf.Max(
                0f,
                verticalInput);

        if (steeringDirection.sqrMagnitude >
            1f)
        {
            steeringDirection.Normalize();
        }

        Vector3 desiredHorizontalVelocity =
            facingDirection *
            retainedForwardSpeed +
            steeringDirection *
            maximumSideSpeed;

        horizontalVelocity =
            Vector3.MoveTowards(
                horizontalVelocity,
                desiredHorizontalVelocity,
                steeringAcceleration *
                Time.fixedDeltaTime);

        float targetVerticalSpeed =
            UpdraftActive
                ? updraftRiseSpeed
                : -fallSpeed;

        float acceleration =
            UpdraftActive
                ? updraftAcceleration
                : verticalAcceleration;

        float verticalSpeed =
            Mathf.MoveTowards(
                currentVelocity.y,
                targetVerticalSpeed,
                acceleration *
                Time.fixedDeltaTime);

        Vector3 velocity =
            horizontalVelocity +
            Vector3.up *
            verticalSpeed;

        if (!IsFinite(velocity))
        {
            return;
        }

        body.linearVelocity =
            velocity;

        RotateTowardMovement(
            horizontalVelocity);
    }

    private void RotateTowardMovement(
        Vector3 horizontalVelocity)
    {
        if (horizontalVelocity.sqrMagnitude <
            0.01f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                horizontalVelocity.normalized,
                Vector3.up);

        float amount =
            1f -
            Mathf.Exp(
                -turningSharpness *
                Time.fixedDeltaTime);

        Quaternion nextRotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                amount);

        if (IsFinite(nextRotation))
        {
            transform.rotation =
                nextRotation;
        }
    }

    #endregion

    #region Triangle Formation

    private void UpdateTriangleFormation()
    {
        Vector3 center =
            CalculateFormationCenter();

        UpdateFormationCharacter(
            powerCharacter,
            center,
            powerAngle);

        UpdateFormationCharacter(
            speedCharacter,
            center,
            speedAngle);

        UpdateFormationCharacter(
            flyCharacter,
            center,
            flyAngle);
    }

    private Vector3 CalculateFormationCenter()
    {
        Vector3 center =
            transform.position +
            transform.up *
            centerHeightOffset;

        return center;
    }

    private void UpdateFormationCharacter(
        Transform character,
        Vector3 center,
        float angle)
    {
        if (character == null)
        {
            return;
        }

        Quaternion formationRotation =
            transform.rotation *
            Quaternion.Euler(
                formationTilt);

        Vector3 radialDirection =
            formationRotation *
            Quaternion.Euler(
                0f,
                angle,
                0f) *
            Vector3.forward;

        Vector3 targetPosition =
            center +
            radialDirection *
            triangleRadius;

        float positionAmount =
            1f -
            Mathf.Exp(
                -teammatePositionSharpness *
                Time.fixedDeltaTime);

        character.position =
            Vector3.Lerp(
                character.position,
                targetPosition,
                positionAmount);

        Vector3 towardCenter =
            center -
            character.position;

        if (towardCenter.sqrMagnitude <=
            Mathf.Epsilon)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                towardCenter.normalized,
                transform.up);

        float rotationAmount =
            1f -
            Mathf.Exp(
                -teammateRotationSharpness *
                Time.fixedDeltaTime);

        character.rotation =
            Quaternion.Slerp(
                character.rotation,
                targetRotation,
                rotationAmount);
    }

    #endregion

    #region Pose

    private void PlayPose()
    {
        if (!playTriangleDiveAnimation)
        {
            return;
        }

        PlayAnimatorState(
            powerAnimator);

        PlayAnimatorState(
            speedAnimator);

        PlayAnimatorState(
            flyAnimator);
    }

    private void StopPose()
    {
        /*
         * Normal movement/character state systems
         * should resume their normal animation.
         */
    }

    private void PlayAnimatorState(
        Animator animator)
    {
        if (animator == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(
            triangleDiveState))
        {
            return;
        }

        animator.Play(
            triangleDiveState,
            0,
            0f);
    }

    #endregion

    #region Updraft Detection

    private void OnTriggerStay(
        Collider other)
    {
        if (!diving ||
            other == null)
        {
            return;
        }

        int layer =
            1 <<
            other.gameObject.layer;

        if ((updraftLayers.value &
            layer) == 0)
        {
            return;
        }

        insideUpdraft =
            true;

        lastUpdraftTime =
            Time.time;
    }

    private void OnTriggerExit(
        Collider other)
    {
        if (other == null)
        {
            return;
        }

        int layer =
            1 <<
            other.gameObject.layer;

        if ((updraftLayers.value &
            layer) == 0)
        {
            return;
        }

        insideUpdraft =
            false;

        lastUpdraftTime =
            Time.time;
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
}