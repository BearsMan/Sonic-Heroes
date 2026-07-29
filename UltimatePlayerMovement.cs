using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CameraController))]
public class UltimatePlayerMovement : MonoBehaviour
{
    #region Constants

    public const float acceleration = 5f;
    public const float deceleration = 3f;
    private const float groundCheckRadius = 0.3f;

    #region Animation States
    private static readonly int StateHash = Animator.StringToHash("State");

    private static readonly int GroundedHash = Animator.StringToHash("Grounded");

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
    #endregion



    #region Inspector
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundMask;

    [SerializeField] private TeamActionController teamController;

    #endregion

    #region Runtime State
    [SerializeField] private bool isGrounded;

    public bool IsGrounded => isGrounded;
    private bool isSurrendered;
    [SerializeField] private Animator playerAnimator;
    #endregion

    private float maxAirSpeed;

    [Header("Movement")]
    [SerializeField] private float runSpeed = 20f;
    [SerializeField] private float airSpeed = 15f;
    [SerializeField] private float turnSpeed = 15f;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float airControl = 20f;

    [Header("Rolling")]
    [SerializeField] private float rollingSpeed = 25f;
    [SerializeField] private float rollingTurnSpeed = 6f;

    [Header("Homing Attack")]
    [SerializeField] private float homingAttackSpeed = 35f;
    [SerializeField] private float homingAttackDuration = 0.5f;

    [Header("Grinding")]
    [SerializeField] private float grindingSpeed = 30f;

    [Header("Flying")]
    [SerializeField] private float flyingSpeed = 15f;
    [SerializeField] private float flyingVerticalSpeed = 10f;

    [Header("Hurt")]
    [SerializeField] private float defaultHurtDuration = 1f;

    [Header("Player States")]
    [SerializeField] private PlayerState currentState = PlayerState.Ground;

    public PlayerState CurrentState => currentState;

    public bool TrickZone { get; internal set; }

    public bool MovementEnabled { get; private set; }

    public object LeftTeamMember { get; internal set; }
    public object RightTeamMember { get; internal set; }
    public object TeamSetup { get; private set; }

    #endregion

    public enum PlayerState
    {
        Ground,
        Air,
        Rolling,
        HomingAttack,
        Grinding,
        Spring,
        Hurt,
        Flying,
        PowerAction
    }
    private void Awake()
    {
        MovementEnabled = true;

        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponent<Rigidbody>();
        }

        if (cameraTransform == null)
        {
            CameraController camCtrl = FindAnyObjectByType<CameraController>();

            if (camCtrl != null)
            {
                cameraTransform = camCtrl.transform;
            }
        }

        if (teamController == null)
        {
            teamController = GetComponent<TeamActionController>();

            if (teamController == null)
            {
                teamController = GetComponentInParent<TeamActionController>();
            }
        }

        if (playerAnimator == null)
        {
            playerAnimator = GetComponentInChildren<Animator>();
        }

        UpdateAnimatorState();
    }
    private bool isGroundLastFrame;

    private Vector3 homingDirection;
    private Vector3 grindingDirection;
    private float stateTimer;
    private Vector3 GetMovementInput()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 movementInput = cameraForward * vertical + cameraRight * horizontal;

        return Vector3.ClampMagnitude(movementInput, 1f);
    }
    private void FixedUpdate()
    {
        isGroundLastFrame = isGrounded;
        isGrounded = CheckGrounded();

        if (!isGroundLastFrame && isGrounded)
        {
            OnLanded();
        }

        if (isGroundLastFrame && !isGrounded)
        {
            OnLeftGround();
        }

        if (!CanMove())
        {
            return;
        }

        Vector3 movementInput = GetMovementInput();

        switch (currentState)
        {
            case PlayerState.Ground:
                UpdateGroundState(movementInput);
                break;

            case PlayerState.Air:
                UpdateAirState(movementInput);
                break;

            case PlayerState.Rolling:
                UpdateRollingState(movementInput);
                break;

            case PlayerState.HomingAttack:
                UpdateHomingAttackState();
                break;

            case PlayerState.Grinding:
                UpdateGrindingState();
                break;

            case PlayerState.Spring:
                UpdateSpringState(movementInput);
                break;

            case PlayerState.Hurt:
                UpdateHurtState();
                break;

            case PlayerState.Flying:
                UpdateFlyingState(movementInput);
                break;

            case PlayerState.PowerAction:
                UpdatePowerActionState(movementInput);
                break;

        }
        RotateToGround();
    }
    private void Update()
    {
        Jump();
        UpdateAnimation();
    }

    private bool CanMove()
    {
        return MovementEnabled && !isSurrendered && HasRequiredComponents();
    }

    private bool HasRequiredComponents()
    {
        return playerRigidbody != null && cameraTransform != null;
    }
    private bool CheckGrounded()
    {
        if (groundCheck == null)
        {
            return false;
        }

        return Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);
    }

    private void OnLanded()
    {
        ChangeState(PlayerState.Ground);

        /*
         * TODO:
         * Play landing sound
         * Spawn landing particles
         * Reset jump state
         */
    }

    private void OnLeftGround()
    {
        if (currentState == PlayerState.Ground)
        {
            ChangeState(PlayerState.Air);
        }
    }

    private void UpdateGroundState(Vector3 movementInput)
    {
        GroundMovement(movementInput);
        Turn(movementInput);
    }

    private void UpdateAirState(Vector3 movementInput)
    {
        AirMovement(movementInput);
    }

    private void ChangeState(PlayerState newState)
    {
        if (currentState == newState)
        {
            return;
        }

        ExitState(currentState);

        currentState = newState;

        UpdateAnimatorState();
        EnterState(currentState);
    }

    private void EnterState(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Ground:
                teamController?.EnableFollowers();
                break;

            case PlayerState.Rolling:
                break;

            case PlayerState.HomingAttack:
                stateTimer = homingAttackDuration;
                break;

            case PlayerState.Hurt:
                stateTimer = defaultHurtDuration;
                break;
        }
    }

    private void ExitState(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.HomingAttack:
                homingDirection = Vector3.zero;
                break;

            case PlayerState.Grinding:
                grindingDirection = Vector3.zero;
                break;
        }
    }
    private void UpdateRollingState(Vector3 movementInput)
    {
        Vector3 forward = transform.forward;

        Vector3 rollingVelocity = forward * rollingSpeed;
        rollingVelocity.y = playerRigidbody.linearVelocity.y;

        playerRigidbody.linearVelocity = rollingVelocity;

        if (movementInput.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(movementInput, transform.up);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.fixedDeltaTime * rollingTurnSpeed);
        }

        if (!isGrounded)
        {
            ChangeState(PlayerState.Air);
        }
    }

    public void StartRolling()
    {
        if (!isGrounded || currentState != PlayerState.Ground)
        {
            return;
        }

        ChangeState(PlayerState.Rolling);
    }

    public void StopRolling()
    {
        if (currentState != PlayerState.Rolling)
        {
            return;
        }

        ChangeState(isGrounded
            ? PlayerState.Ground
            : PlayerState.Air);
    }

    public void StartHomingAttack(Vector3 targetPosition)
    {
        if (currentState == PlayerState.Hurt)
        {
            return;
        }

        homingDirection =
            (targetPosition - playerRigidbody.position).normalized;

        if (homingDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        ChangeState(PlayerState.HomingAttack);
    }

    private void UpdateHomingAttackState()
    {
        stateTimer -= Time.fixedDeltaTime;

        playerRigidbody.linearVelocity =
            homingDirection * homingAttackSpeed;

        if (homingDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(homingDirection, Vector3.up);

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * turnSpeed);
        }

        if (stateTimer <= 0f)
        {
            ChangeState(isGrounded? PlayerState.Ground: PlayerState.Air);
        }
    }

    public void StartGrinding(Vector3 railDirection)
    {
        if (railDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        grindingDirection = railDirection.normalized;

        if (Vector3.Dot(grindingDirection, transform.forward) < 0f)
        {
            grindingDirection = -grindingDirection;
        }

        ChangeState(PlayerState.Grinding);
    }

    private void UpdateGrindingState()
    {
        playerRigidbody.linearVelocity = grindingDirection * grindingSpeed;

        Quaternion targetRotation = Quaternion.LookRotation(grindingDirection, Vector3.up);

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * turnSpeed);
    }

    public void StopGrinding()
    {
        if (currentState != PlayerState.Grinding)
        {
            return;
        }

        ChangeState(PlayerState.Air);
    }

    public void LaunchFromSpring(Vector3 launchDirection, float height)
    {
        if (playerRigidbody == null)
        {
            return;
        }

        Vector3 direction = launchDirection.normalized;

        float launchSpeed =
            Mathf.Sqrt(height * -2f * Physics.gravity.y);

        playerRigidbody.linearVelocity = direction * launchSpeed;

        ChangeState(PlayerState.Spring);
    }

    private void UpdateSpringState(Vector3 movementInput)
    {
        AirMovement(movementInput);

        if (playerRigidbody.linearVelocity.y <= 0f)
        {
            ChangeState(PlayerState.Air);
        }
    }

    public void EnterHurtState(Vector3 knockbackVelocity, float duration)
    {
        playerRigidbody.linearVelocity = knockbackVelocity;

        stateTimer = Mathf.Max(0f, duration);

        ChangeState(PlayerState.Hurt);
    }

    private void UpdateHurtState()
    {
        stateTimer -= Time.fixedDeltaTime;

        if (stateTimer > 0f)
        {
            return;
        }

        ChangeState(isGrounded? PlayerState.Ground: PlayerState.Air);
    }

    private void UpdateFlyingState(Vector3 movementInput)
    {
        float verticalInput = 0f;

        if (Input.GetKey(KeyCode.Space))
        {
            verticalInput += 1f;
        }

        if (Input.GetKey(KeyCode.LeftControl))
        {
            verticalInput -= 1f;
        }

        Vector3 flyingVelocity = movementInput * flyingSpeed;

        flyingVelocity.y = verticalInput * flyingVerticalSpeed;

        playerRigidbody.linearVelocity = flyingVelocity;

        Turn(movementInput);
    }

    public void StartFlying()
    {
        ChangeState(PlayerState.Flying);
    }

    public void StopFlying()
    {
        ChangeState(isGrounded ? PlayerState.Ground : PlayerState.Air);
    }

    private void UpdatePowerActionState(Vector3 movementInput)
    {
        if (isGrounded)
        {
            GroundMovement(movementInput);
            Turn(movementInput);
        }
        else
        {
            AirMovement(movementInput);
        }
    }

    public void StartPowerAction()
    {
        ChangeState(PlayerState.PowerAction);
    }

    public void StopPowerAction()
    {
        ChangeState(isGrounded ? PlayerState.Ground : PlayerState.Air);
    }

    private void UpdateAnimation()
    {
        if (playerAnimator == null || playerRigidbody == null)
        {
            return;
        }

        Vector3 horizontalVelocity = playerRigidbody.linearVelocity;

        horizontalVelocity.y = 0f;

        playerAnimator.SetBool(GroundedHash, isGrounded);

        playerAnimator.SetFloat(SpeedHash, horizontalVelocity.magnitude);

        playerAnimator.SetFloat(VerticalSpeedHash, playerRigidbody.linearVelocity.y);
    }

    private void UpdateAnimatorState()
    {
        if (playerAnimator == null)
        {
            return;
        }

        playerAnimator.SetInteger(
            StateHash,
            (int)currentState);
    }
    public void SetupAnimation()
    {
        playerAnimator = GetComponentInChildren<Animator>();

        UpdateAnimatorState();
    }
    private void GroundMovement(Vector3 movementInput)
    {
        movementInput.y = 0f;

        maxAirSpeed = airSpeed;

        Vector3 velocity = playerRigidbody.linearVelocity;

        if (velocity.y < 0f)
        {
            velocity.y = 0f;
            playerRigidbody.linearVelocity = velocity;
        }

        Vector3 targetPosition = playerRigidbody.position + movementInput * runSpeed * Time.fixedDeltaTime;

        playerRigidbody.MovePosition(targetPosition);
    }

    private void AirMovement(Vector3 movementInput)
    {
        playerRigidbody.AddForce(movementInput * airControl, ForceMode.Acceleration);

        Vector3 horizontalVelocity = playerRigidbody.linearVelocity;
        horizontalVelocity.y = 0f;

        if (horizontalVelocity.magnitude > maxAirSpeed)
        {
            horizontalVelocity = horizontalVelocity.normalized * maxAirSpeed;

            horizontalVelocity.y = playerRigidbody.linearVelocity.y;
            playerRigidbody.linearVelocity = horizontalVelocity;
        }
    }

    public void RotateToGround()
    {
        if (!isGrounded)
        {
            Vector3 cross = Vector3.Cross(transform.right, Vector3.up);

            Quaternion airRotation = Quaternion.LookRotation(cross);

            transform.rotation = Quaternion.LerpUnclamped(transform.rotation, airRotation, Time.deltaTime * 100f);

            return;
        }

        Vector3 origin = transform.position + transform.up * 0.5f;

        if (!Physics.Raycast(origin, -transform.up, out RaycastHit hit, 2f, groundMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        Vector3 newUp = hit.normal;

        float angle = Vector3.Angle(transform.up, newUp);

        if (angle > 30f)
        {
            return;
        }

        Vector3 groundDirection = Vector3.Cross(transform.right, newUp);

        Quaternion groundRotation = Quaternion.LookRotation(groundDirection);

        transform.rotation = Quaternion.LerpUnclamped(transform.rotation, groundRotation, Time.deltaTime * 100f);
    }
    private void Jump()
    {
        if (!isGrounded)
        {
            return;
        }

        if (!Input.GetKeyDown(KeyCode.Space))
        {
            return;
        }

        playerRigidbody.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);

        isGrounded = false;

        ChangeState(PlayerState.Air);
    }

    private void Turn(Vector3 movementInput)
    {
        if (movementInput.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(movementInput);

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * turnSpeed);
    }

    public void StopMovement()
    {
        if (playerRigidbody == null)
        {
            return;
        }

        playerRigidbody.linearVelocity = Vector3.zero;
        playerRigidbody.angularVelocity = Vector3.zero;
    }

    public void EnableMovement()
    {
        MovementEnabled = true;
    }

    public void DisableMovement()
    {
        MovementEnabled = false;
    }

    public void SurrenderControl(Vector2 up, float newSurrenderTime)
    {
        StopAllCoroutines();
        StartCoroutine(Surrender(newSurrenderTime));
    }

    private IEnumerator Surrender(float time)
    {
        isSurrendered = true;

        yield return new WaitForSeconds(time);

        isSurrendered = false;
    }

    public void Launch(Vector3 launchDirection, float height)
    {
        if (playerRigidbody == null)
        {
            return;
        }

        playerRigidbody.linearVelocity = Mathf.Sqrt(height * -2f * Physics.gravity.y) * launchDirection;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}