using UnityEngine;

/// <summary>
/// Trolley controller inspired by Sonic Heroes rail/vehicle movement.
/// Features: speed-based acceleration, ground alignment, jumping, and boost drive.
/// </summary>
public class Trolley : MonoBehaviour
{
    [Header("References")]
    public GameObject trolleyCar;
    public Rigidbody body;
    public LayerMask roadCheckMask;

    [Header("Movement")]
    public float maxSpeed = 20f;
    public float accelerationRate = 8f;
    public float decelerationRate = 5f;
    public float boostSpeed = 40f;

    [Header("Jump")]
    public float jumpForce = 10f;
    public float gravityScale = 2.5f;

    [Header("Turning")]
    public float turnSpeed = 120f;

    [Header("Ground Check")]
    public float groundRayLength = 1.2f;
    public float groundAlignSpeed = 10f;
    public float maxSlopeAngle = 45f;

    // Internal state
    private float currentSpeed = 0f;
    private bool isGrounded = false;
    private bool isBoosting = false;
    private Vector3 moveDirection = Vector3.forward;
    private Vector3 groundNormal = Vector3.up;

    void Start()
    {
        if (body == null)
            body = GetComponent<Rigidbody>();

        // Disable Unity's default gravity — we apply custom gravity below
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.freezeRotation = true;
    }

    void Update()
    {
        CheckGrounded();
        HandleInput();
        AlignToGround();
        ApplyGravity();
        ApplyMovement();
    }

    // ─────────────────────────────────────────────
    // Ground Detection
    // ─────────────────────────────────────────────
    private void CheckGrounded()
    {
        RaycastHit hit;
        Vector3 origin = transform.position + transform.up * 0.3f;

        if (Physics.Raycast(origin, -transform.up, out hit, groundRayLength, roadCheckMask))
        {
            isGrounded = true;
            groundNormal = hit.normal;

            // Snap to ground surface to avoid floating
            Vector3 snapPos = hit.point + hit.normal * 0.1f;
            transform.position = Vector3.Lerp(transform.position, snapPos, Time.deltaTime * 15f);
        }
        else
        {
            isGrounded = false;
            groundNormal = Vector3.up;
        }
    }

    // ─────────────────────────────────────────────
    // Input Handling
    // ─────────────────────────────────────────────
    private void HandleInput()
    {
        float verticalInput = Input.GetAxis("Vertical");   // W/S or Up/Down
        float horizontalInput = Input.GetAxis("Horizontal"); // A/D or Left/Right

        // ── Turning ──
        if (Mathf.Abs(horizontalInput) > 0.05f)
        {
            float turnAmount = horizontalInput * turnSpeed * Time.deltaTime;
            transform.Rotate(Vector3.up, turnAmount, Space.Self);
        }

        // ── Acceleration / Deceleration (Sonic-style) ──
        if (verticalInput > 0.05f)
        {
            // Accelerate forward
            float targetSpeed = isBoosting ? boostSpeed : maxSpeed;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed * verticalInput, accelerationRate * Time.deltaTime);
        }
        else if (verticalInput < -0.05f)
        {
            // Brake / reverse
            currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed * verticalInput * 0.5f, decelerationRate * Time.deltaTime);
        }
        else
        {
            // Natural deceleration when no input
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, decelerationRate * Time.deltaTime);
        }

        // ── Boost (like Sonic Heroes' speed type boost) ──
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
            isBoosting = true;
        if (Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift))
            isBoosting = false;

        // ── Jump ──
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            Jump();
        }
    }

    // ─────────────────────────────────────────────
    // Jump
    // ─────────────────────────────────────────────
    private void Jump()
    {
        // Apply upward impulse along world up, preserving horizontal velocity
        Vector3 velocity = body.linearVelocity;
        velocity.y = jumpForce;
        body.linearVelocity = velocity;
        isGrounded = false;
    }

    // ─────────────────────────────────────────────
    // Apply Movement
    // ─────────────────────────────────────────────
    private void ApplyMovement()
    {
        // Move along the trolley's forward direction at current speed
        Vector3 forwardMove = transform.forward * currentSpeed;

        // Preserve vertical velocity (gravity / jump)
        Vector3 newVelocity = new Vector3(forwardMove.x, body.linearVelocity.y, forwardMove.z);
        body.linearVelocity = newVelocity;
    }

    // ─────────────────────────────────────────────
    // Custom Gravity (heavier fall, Sonic-style)
    // ─────────────────────────────────────────────
    private void ApplyGravity()
    {
        if (!isGrounded)
        {
            body.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);
        }
        else
        {
            // Pin downward velocity when grounded to avoid bouncing
            Vector3 vel = body.linearVelocity;
            if (vel.y < 0f)
            {
                vel.y = 0f;
                body.linearVelocity = vel;
            }
        }
    }

    // ─────────────────────────────────────────────
    // Ground Alignment (tilts trolley to slope)
    // ─────────────────────────────────────────────
    private void AlignToGround()
    {
        if (!isGrounded) return;

        float slopeAngle = Vector3.Angle(Vector3.up, groundNormal);
        if (slopeAngle > maxSlopeAngle) return;

        // Compute new forward direction projected along the slope
        Vector3 slopeForward = Vector3.Cross(transform.right, groundNormal);

        if (slopeForward == Vector3.zero) return;

        Quaternion targetRotation = Quaternion.LookRotation(slopeForward, groundNormal);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * groundAlignSpeed);
    }

    // ─────────────────────────────────────────────
    // Debug Gizmos
    // ─────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        // Ground ray
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Vector3 origin = transform.position + transform.up * 0.3f;
        Gizmos.DrawLine(origin, origin - transform.up * groundRayLength);

        // Speed indicator
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * currentSpeed * 0.1f);
    }
}