using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CameraController))]
public class UltimatePlayerMovement : MonoBehaviour
{
    #region Constants

    public const float acceleration = 5f;
    public const float deceleration = 3f;
    public const float runSpeed = 20f;
    public const float airSpeed = 15f;

    private const float groundCheckRadius = 0.3f;

    #endregion

    #region Inspector

    public float currentSpeed = runSpeed;

    public GameObject leftFollower;
    public GameObject rightFollower;
    public GameObject sonic;
    public GameObject superSonic;

    [SerializeField] private Rigidbody body;
    [SerializeField] private Transform cam;
    [SerializeField] private Transform groundCheck;

    [SerializeField] private LayerMask groundMask;
    [SerializeField] private bool tutorialPlaying;

    [Header("Runtime Debug")]
    [SerializeField] private bool movementEnabledDebug;

    #endregion

    #region Runtime State

    public GameObject currentCharacter;
    public bool isGrounded;

    private bool isSurrendered;
    private Animator anim;

    private float maxAirSpeed;
    private float airControl = 20f;
    private float jumpHeight = 6f;

    private Vector3 direction;

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
            CameraController camCtrl =
                FindAnyObjectByType<CameraController>();

            if (camCtrl != null)
            {
                cam = camCtrl.transform;
            }
        }
    }

    private void Update()
    {
        movementEnabledDebug = MovementEnabled;

        CheckGrounded();

        if (tutorialPlaying)
        {
            return;
        }

        if (anim != null)
        {
            anim.SetBool("InAir", !isGrounded);
        }

        if (isGrounded)
        {
            EnableFollowerAgents();
        }
    }

    private void FixedUpdate()
    {
        Move();
    }

    private void CheckGrounded()
    {
        if (groundCheck == null)
        {
            isGrounded = false;
            return;
        }

        isGrounded = Physics.CheckSphere(
            groundCheck.position,
            groundCheckRadius,
            groundMask,
            QueryTriggerInteraction.Ignore);
    }

    private void EnableFollowerAgents()
    {
        if (leftFollower != null &&
            leftFollower.TryGetComponent(
                out FollowerNavigation leftNavigation) &&
            leftNavigation.agent != null)
        {
            leftNavigation.agent.enabled = true;
        }

        if (rightFollower != null &&
            rightFollower.TryGetComponent(
                out FollowerNavigation rightNavigation) &&
            rightNavigation.agent != null)
        {
            rightNavigation.agent.enabled = true;
        }
    }

    public void SetupAnimation()
    {
        anim = GetComponentInChildren<Animator>();
    }

    public void Move()
    {
        if (!MovementEnabled || cam == null || body == null)
        {
            return;
        }

        Vector3 forward =
            Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;

        Vector3 right =
            Vector3.Cross(Vector3.up, forward).normalized;

        if (tutorialPlaying)
        {
            forward = Vector3.zero;
            right = Vector3.zero;
        }

        Vector3 movementInput = Vector3.zero;

        if (!isSurrendered)
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            movementInput =
                vertical * forward +
                horizontal * right;

            movementInput =
                Vector3.ClampMagnitude(movementInput, 1f);

            Jump();
            Turn();
        }

        if (isGrounded)
        {
            GroundMovement(movementInput);
        }
        else
        {
            AirMovement(movementInput);
        }
    }

    private void GroundMovement(Vector3 movementInput)
    {
        movementInput.y = 0f;

        maxAirSpeed = currentSpeed;
        direction = movementInput * currentSpeed;

        Vector3 velocity = body.linearVelocity;

        if (velocity.y < 0f)
        {
            velocity.y = 0f;
            body.linearVelocity = velocity;
        }

        Vector3 targetPosition =
            body.position +
            direction * Time.fixedDeltaTime;

        body.MovePosition(targetPosition);
    }

    private void AirMovement(Vector3 movementInput)
    {
        body.AddForce(
            movementInput * airControl,
            ForceMode.Acceleration);

        Vector3 horizontalVelocity = body.linearVelocity;
        horizontalVelocity.y = 0f;

        if (horizontalVelocity.magnitude > maxAirSpeed)
        {
            horizontalVelocity =
                horizontalVelocity.normalized * maxAirSpeed;

            horizontalVelocity.y = body.linearVelocity.y;
            body.linearVelocity = horizontalVelocity;
        }
    }

    public void Jump()
    {
        if (!MovementEnabled)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            Vector3 jumpVelocity = direction * 0.8f;

            jumpVelocity.y =
                Mathf.Sqrt(
                    jumpHeight *
                    -2f *
                    Physics.gravity.y);

            body.linearVelocity = jumpVelocity;
        }
    }

    public void Turn()
    {
        if (!MovementEnabled)
        {
            return;
        }

        transform.Rotate(
            transform.up,
            Input.GetAxis("Mouse X"));
    }

    public void RotateToGround()
    {
        if (!isGrounded)
        {
            Vector3 cross =
                Vector3.Cross(transform.right, Vector3.up);

            Quaternion airRotation =
                Quaternion.LookRotation(cross);

            transform.rotation =
                Quaternion.LerpUnclamped(
                    transform.rotation,
                    airRotation,
                    Time.deltaTime * 100f);

            return;
        }

        Vector3 origin =
            transform.position +
            transform.up * 0.5f;

        if (!Physics.Raycast(
                origin,
                -transform.up,
                out RaycastHit hit,
                2f,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        Vector3 newUp = hit.normal;

        float angle =
            Vector3.Angle(transform.up, newUp);

        if (angle > 30f)
        {
            return;
        }

        Vector3 groundDirection =
            Vector3.Cross(transform.right, newUp);

        Quaternion groundRotation =
            Quaternion.LookRotation(groundDirection);

        transform.rotation =
            Quaternion.LerpUnclamped(
                transform.rotation,
                groundRotation,
                Time.deltaTime * 100f);
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

    public void SurrenderControl(
        Vector2 up,
        float newSurrenderTime)
    {
        StopAllCoroutines();
        StartCoroutine(
            Surrender(newSurrenderTime));
    }

    private IEnumerator Surrender(float time)
    {
        isSurrendered = true;

        yield return new WaitForSeconds(time);

        isSurrendered = false;
    }

    public void Launch(
        Vector3 launchDirection,
        float height)
    {
        if (body == null)
        {
            return;
        }

        body.linearVelocity =
            Mathf.Sqrt(
                height *
                -2f *
                Physics.gravity.y) *
            launchDirection;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundCheckRadius);
    }
}