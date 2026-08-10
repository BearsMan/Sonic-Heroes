using UnityEngine;

/// <summary>
/// BobSleigh controller modelled after Sonic Heroes' bobsled/bobsled-rail sections.
/// - Ground-hugging via raycast rotation (RotateToGround)
/// - Acceleration / top-speed model with a speed-type team boost
/// - Left/Right turning that banks the sled
/// - Jump with upward impulse
/// - Boost pickup support via AddBoost()
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BobSleigh : MonoBehaviour
{
    [Header("References")]
    public GameObject player;           // The player/character riding the sled
    public Transform spawnSpot;         // World-space spawn point

    [Header("State")]
    public bool isDriving = false;
    public bool isGrounded = false;

    [Header("Movement")]
    public float acceleration = 15f;   // Units/s^2
    public float brakeStrength = 20f;   // Deceleration when braking
    public float maxSpeed = 40f;   // Normal top speed (units/s)
    public float boostMaxSpeed = 70f;   // Top speed while boosted
    public float boostDuration = 3f;    // How long a boost lasts
    public float turnSpeed = 80f;   // Degrees per second
    public float bankAngle = 25f;   // Visual Z-roll when turning

    [Header("Jump")]
    public float jumpForce = 8f;

    [Header("Ground Check")]
    public float groundCheckDistance = 1.2f;
    public LayerMask groundCheckMask;

    // ── Private state ──────────────────────────────────────────────
    private Rigidbody _body;
    private float _currentSpeed = 0f;
    private float _boostTimer = 0f;
    private bool _isBoosting = false;
    private float _currentBank = 0f;   // Smoothed Z-roll
    private Vector3 _groundNormal = Vector3.up;

    // ── Unity Lifecycle ────────────────────────────────────────────

    void Start()
    {
        // FIX: assign to the class field, not a new local variable
        _body = GetComponent<Rigidbody>();
        _body.useGravity = true;
        _body.constraints = RigidbodyConstraints.FreezeRotation; // We rotate manually
    }

    void Update()
    {
        if (!isDriving) return;

        UpdateGroundCheck();
        RotateToGround();
        HandleAcceleration();
        HandleTurning();
        HandleJump();
        HandleBoostTimer();
    }

    void FixedUpdate()
    {
        if (!isDriving) return;

        // Drive the sled forward along its local forward axis
        Vector3 desiredVelocity = transform.forward * _currentSpeed;

        // Preserve existing Y velocity so gravity still acts (arc over bumps)
        desiredVelocity.y = _body.linearVelocity.y;

        _body.linearVelocity = desiredVelocity;
    }

    // ── Ground Detection ───────────────────────────────────────────

    void UpdateGroundCheck()
    {
        Vector3 origin = transform.position + transform.up * 0.3f;
        isGrounded = Physics.Raycast(origin, -transform.up, out RaycastHit hit,
                                     groundCheckDistance, groundCheckMask);
        if (isGrounded)
            _groundNormal = hit.normal;
        else
            _groundNormal = Vector3.up;
    }

    // ── Ground Alignment (Sonic Heroes rail-hug feel) ──────────────

    void RotateToGround()
    {
        Vector3 targetUp = _groundNormal;
        float angle = Vector3.Angle(transform.up, targetUp);

        // Only align when the slope change is reasonable (avoid snapping on walls)
        if (angle > 60f) return;

        Vector3 forward = Vector3.Cross(transform.right, targetUp).normalized;
        Quaternion target = Quaternion.LookRotation(forward, targetUp);

        // Apply smoothed bank (Z-roll) on top of the ground-aligned rotation
        Quaternion bank = Quaternion.AngleAxis(_currentBank, transform.forward);

        transform.rotation = Quaternion.Lerp(transform.rotation,
                                             bank * target,
                                             Time.deltaTime * 10f);
    }

    // ── Acceleration / Braking ─────────────────────────────────────

    void HandleAcceleration()
    {
        float topSpeed = _isBoosting ? boostMaxSpeed : maxSpeed;

        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W))
        {
            // Accelerate
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, topSpeed,
                                               acceleration * Time.deltaTime);
        }
        else if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S))
        {
            // Brake / reverse
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, -topSpeed * 0.3f,
                                               brakeStrength * Time.deltaTime);
        }
        else
        {
            // Passive deceleration (slight drag on a bobsled track)
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f,
                                               2f * Time.deltaTime);
        }
    }

    // ── Turning ────────────────────────────────────────────────────

    void HandleTurning()
    {
        float input = 0f;

        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) input = -1f;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) input = 1f;

        // Rotate around the sled's local up axis (yaw)
        // Turn radius tightens with speed (more arcade feel at low speed)
        float speedFactor = Mathf.Clamp01(_currentSpeed / maxSpeed);
        float yaw = input * turnSpeed * (0.5f + 0.5f * speedFactor) * Time.deltaTime;
        transform.Rotate(0f, yaw, 0f, Space.Self);

        // Smooth banking visual
        float targetBank = -input * bankAngle;
        _currentBank = Mathf.Lerp(_currentBank, targetBank, Time.deltaTime * 6f);
    }

    // ── Jump ───────────────────────────────────────────────────────

    void HandleJump()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            // FIX: apply an upward impulse via Rigidbody, not position teleport
            _body.AddForce(transform.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
        }
    }

    // ── Boost System (Speed-type team power in Sonic Heroes) ───────

    void HandleBoostTimer()
    {
        if (_isBoosting)
        {
            _boostTimer -= Time.deltaTime;
            if (_boostTimer <= 0f)
                _isBoosting = false;
        }
    }

    /// <summary>
    /// Call this from a boost pad trigger or power-up pickup.
    /// </summary>
    public void AddBoost(float duration = 0f)
    {
        _isBoosting = true;
        _boostTimer = (duration > 0f) ? duration : boostDuration;
        // Give an instant speed kick like Sonic Heroes' boost pads
        _currentSpeed = Mathf.Max(_currentSpeed, maxSpeed * 1.3f);
    }

    // ── Enter / Exit Sled ──────────────────────────────────────────

    /// <summary>Attach the player character to the sled.</summary>
    public void EnterSled()
    {
        if (player == null) return;
        isDriving = true;
        player.transform.SetParent(transform);
        player.transform.localPosition = Vector3.zero;
        player.transform.localRotation = Quaternion.identity;

        // Disable the character's own movement script while riding
        var movement = player.GetComponent<UltimatePlayerMovement>();
        if (movement != null) movement.enabled = false;
    }

    /// <summary>Detach the player and spawn them at the exit point.</summary>
    public void ExitSled()
    {
        if (player == null) return;
        isDriving = false;

        player.transform.SetParent(null);

        if (spawnSpot != null)
            player.transform.position = spawnSpot.position;

        var movement = player.GetComponent<UltimatePlayerMovement>();
        if (movement != null) movement.enabled = true;

        _currentSpeed = 0f;
    }

    // ── Collision ──────────────────────────────────────────────────

    void OnCollisionEnter(Collision collision)
    {
        // Scrub speed on hard impacts (hitting a wall in Sonic Heroes kills momentum)
        if (collision.relativeVelocity.magnitude > 5f)
        {
            _currentSpeed *= 0.4f;
        }
    }

    // ── Debug Gizmos ───────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Vector3 origin = transform.position + transform.up * 0.3f;
        Gizmos.DrawLine(origin, origin - transform.up * groundCheckDistance);
    }
}