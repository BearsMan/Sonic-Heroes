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

    #endregion

    #region Inspector
    [SerializeField] private Rigidbody body;
    [SerializeField] private Transform cam;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundMask;

    [SerializeField] private TeamActionController teamController;

    #endregion

    #region Runtime State
    [SerializeField] private bool isGrounded;

    public bool IsGrounded => isGrounded;
    private bool isSurrendered;
    private Animator anim;

    private float maxAirSpeed;

    [Header("Movement")]
    [SerializeField] private float runSpeed = 20f;
    [SerializeField] private float airSpeed = 15f;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float airControl = 20f;

    public bool TrickZone { get; internal set; }

    public bool MovementEnabled { get; private set; }

    public object LeftTeamMember { get; internal set; }
    public object RightTeamMember { get; internal set; }
    public object TeamSetup { get; private set; }

    #endregion

    private void Awake()
    {
        MovementEnabled = true;

        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }

        if (cam == null)
        {
            CameraController camCtrl = FindAnyObjectByType<CameraController>();

            if (camCtrl != null)
            {
                cam = camCtrl.transform;
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
    }
    private bool wasGrounded = false;


    private Vector3 GetMovementInput()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 cameraForward = cam.forward;
        Vector3 cameraRight = cam.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 movementInput =
            cameraForward * vertical +
            cameraRight * horizontal;

        return Vector3.ClampMagnitude(movementInput, 1f);
    }
    private void FixedUpdate()
    {
        CheckGrounded();

        if (!CanMove())
        {
            return;
        }

        Vector3 movementInput = GetMovementInput();

        if (isGrounded)
        {
            GroundMovement(movementInput);
        }
        else
        {
            AirMovement(movementInput);
        }

        Turn(movementInput);
        RotateToGround();
    }
    private void Update()
    {
        Jump();

        if (anim != null)
        {
            anim.SetBool("InAir", !isGrounded);
        }

        if (isGrounded && !wasGrounded)
        {
            teamController?.EnableFollowers();
        }

        wasGrounded = isGrounded;
    }

    private bool CanMove()
    {
        return MovementEnabled && !isSurrendered && HasRequiredComponents();
    }

    private bool HasRequiredComponents()
    {
        return body != null && cam != null;
    }
    private void CheckGrounded()
    {
        if (groundCheck == null)
        {
            isGrounded = false;
            return;
        }

        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);
    }

    public void SetupAnimation()
    {
        anim = GetComponentInChildren<Animator>();
    }
    private void GroundMovement(Vector3 movementInput)
    {
        movementInput.y = 0f;

        maxAirSpeed = airSpeed;

        Vector3 velocity = body.linearVelocity;

        if (velocity.y < 0f)
        {
            velocity.y = 0f;
            body.linearVelocity = velocity;
        }

        Vector3 targetPosition = body.position + movementInput * runSpeed * Time.fixedDeltaTime;

        body.MovePosition(targetPosition);
    }

    private void AirMovement(Vector3 movementInput)
    {
        body.AddForce(movementInput * airControl, ForceMode.Acceleration);

        Vector3 horizontalVelocity = body.linearVelocity;
        horizontalVelocity.y = 0f;

        if (horizontalVelocity.magnitude > maxAirSpeed)
        {
            horizontalVelocity = horizontalVelocity.normalized * maxAirSpeed;

            horizontalVelocity.y = body.linearVelocity.y;
            body.linearVelocity = horizontalVelocity;
        }
    }

    public void RotateToGround()
    {
        if (!isGrounded)
        {
            Vector3 cross =
                Vector3.Cross(transform.right, Vector3.up);

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

        body.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);

        isGrounded = false;
    }

    private void Turn(Vector3 movementInput)
    {
        if (movementInput.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(movementInput);

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * 15f);
    }

    public void StopMovement()
    {
        if (body == null)
        {
            return;
        }

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
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
        if (body == null)
        {
            return;
        }

        body.linearVelocity = Mathf.Sqrt(height *-2f * Physics.gravity.y) * launchDirection;
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