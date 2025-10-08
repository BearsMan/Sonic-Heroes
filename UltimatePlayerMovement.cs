using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UltimatePlayerMovement : MonoBehaviour
{
    /*
    This script handles player movement, jumping, and rotation based on camera orientation.
     It includes settings for acceleration, speed, and air control.
     It also manages grounded state and integrates with an animator for character animations.
     The script allows for temporary disabling of player control and launching the player in a specific direction.
     Movement constants
    */
    #region constant variables
    public const float acceleration = 5f;
    public const float deceleration = 3f;
    public const float runSpeed = 20f;
    public const float airSpeed = 15f;
    #endregion

    // Movement settings
    #region public variables
    public float currentSpeed = runSpeed;
    public GameObject leftFollower;
    public GameObject rightFollower;
    public GameObject sonic;
    public GameObject superSonic;
    public Rigidbody body;
    public Transform cam;
    public LayerMask groundMask;
    public Vector3 moveForce;
    public bool tutorialPlaying = false;
    #endregion

    // State variables
    public GameObject currentCharacter;
    public bool useGravity = false;
    #region private variables
    
    // Cached camera forward direction
    private Quaternion CameraWorldFoward
    {
        get
        {
            return Quaternion.LookRotation(cam.forward, transform.up);
        }
    }
    // Grounded state
    public bool isGrounded = false;
    private float jumpSustainTime = 0f;
    private bool isSurrendered = false;

    // Current maximum speed based on grounded state
    private float CurrentMaxSpeed
    {
        get
        {
            return isGrounded ? currentSpeed : airSpeed;
        }
    }

    // Static and instance properties for control and team setup
    public static bool Controllable { get; internal set; }
    public bool TrickZone { get; internal set; }
    public object LeftTeamMember { get; internal set; }
    public object RightTeamMember { get; internal set; }
    public object TeamSetup { get; private set; }

    // Start is called before the first frame update
    private void Awake()
    {
        // Initialize references
        cam = Object.FindFirstObjectByType<CameraController>().transform;
    }

    // Update is called once per frame
    private void Update()
    {
        // Check if the player is grounded
        isGrounded = Physics.CheckSphere(transform.position + transform.up * 0.1f, 0.49f, groundMask);
        // Disable movement during tutorials
        if (tutorialPlaying)
        {
            return;
        }

        // Update animator

        if (anim != null)
        {
            anim.SetBool("InAir", !isGrounded);
        }
        if (isGrounded == true)
        {
            leftFollower.GetComponent<FollowerNavigation>().agent.enabled = true;
            rightFollower.GetComponent<FollowerNavigation>().agent.enabled = true;
        }
        // RotateToGround();
    }
    // FixedUpdate is called at a fixed interval and is independent of frame rate
    private void FixedUpdate()
    {
        Move();
    }

    // Animator component for handling animations
    private Animator anim;

    // Setup the animator component
    public void SetupAnimation()
    {
        anim = GetComponentInChildren<Animator>();
    }

    // Old movement system, kept for reference
    public void OldMove()
    {
        // Get camera-relative directions
        Vector3 right = Vector3.Cross(transform.up, cam.forward);
        Vector3 forward = Vector3.Cross(right, transform.up);

        // Disable movement during tutorials
        if (tutorialPlaying)
        {
            right = Vector3.zero; 
            forward = Vector3.zero;
        }

        // Calculate movement vector based on input
        Vector3 mov = Vector3.zero;


        // Disable movement during tutorials
        if (!isSurrendered)
        {
            // Calculate movement vector based on input
            mov = Input.GetAxis("Vertical") * forward + Input.GetAxis("Horizontal") * right;
            Jump();
            Turn();
        }

        // Apply movement based on grounded state
        Vector3 velocityXZ = body.linearVelocity;
        velocityXZ.y = 0;
        direction = mov * currentSpeed;

        // Limit the movement direction to the current speed
        if (isGrounded)
        {
            body.useGravity = false;
            // body.velocity = mov * runSpeed;
            body.MovePosition(transform.position + (mov * currentSpeed * Time.fixedDeltaTime));
        }
        else
        {
            body.useGravity = true;
            // body.AddForce(Vector3.down * 90);
            body.AddForce(mov * 50);

            // Limit air speed
            if (velocityXZ.magnitude > currentSpeed)
            {
                velocityXZ = velocityXZ.normalized * currentSpeed;
                velocityXZ.y = body.linearVelocity.y;
                body.linearVelocity = velocityXZ;
            }
        }
        // Update animator with movement speed
        if (anim != null)
        {
            anim.SetFloat("Speed", velocityXZ.magnitude);
        }

    }

    // Old movement system, kept for reference
    public void Move()
    {
        Vector3 right = Vector3.Cross(transform.up, cam.forward);
        Vector3 forward = Vector3.Cross(right, transform.up);

        // Disable movement during tutorials
        if (tutorialPlaying)
        {
            right = Vector3.zero; 
            forward = Vector3.zero;
        }
        // Calculate movement vector based on input
        Vector3 mov = Vector3.zero;
        if (!isSurrendered)
        {
            // Calculate movement vector based on input
            mov = Input.GetAxis("Vertical") * forward + Input.GetAxis("Horizontal") * right;
            Jump();
            Turn();
        }

        // Apply movement based on grounded state
        if (isGrounded)
        {
            GroundMovement(mov);
        }
        else
        {
            AirMovement(mov);
        }

    }

    // Ground movement logic
    private void GroundMovement(Vector3 mov)
    {
        maxAirSpeed = currentSpeed;
        direction = mov * currentSpeed;
        if (direction.magnitude > currentSpeed)
        {
            direction = direction.normalized * currentSpeed;
        }

        body.MovePosition(transform.position + transform.TransformDirection(direction) * Time.fixedDeltaTime);
    }

    // Air movement settings
    private float maxAirSpeed = 20f;
    private float airControl = 20f;

    // Air movement logic
    private void AirMovement(Vector3 mov)
    {
        body.AddForce(transform.TransformDirection(mov * airControl));
        Vector3 veloXZ = body.linearVelocity;
        veloXZ.y = 0;
        if (veloXZ.magnitude > maxAirSpeed)
        {
            veloXZ = veloXZ.normalized * maxAirSpeed;
            veloXZ.y = body.linearVelocity.y;
            body.linearVelocity = veloXZ;
        }
    }

    // Jump settings
    private float jumpHeight = 6f;
    private Vector3 direction;

    // Jump logic
    public void Jump()
    {
        if (Input.GetKey(KeyCode.Space) && isGrounded)
        {
            Vector3 velo = transform.TransformDirection(direction * 0.8f);
            velo.y = Mathf.Sqrt(jumpHeight * -2 * Physics.gravity.y);
            body.linearVelocity = velo;
        }
    }

    // Turning logic
    public void Turn()
    {
        transform.Rotate(transform.up, Input.GetAxis("Mouse X"));
    }

    // Rotate the player to align with the ground normal
    public void RotateToGround()
    {
        if (!isGrounded)
        {
            Vector3 cross = Vector3.Cross(transform.right, Vector3.up);
            Quaternion newrot = Quaternion.LookRotation(cross);
            transform.rotation = Quaternion.LerpUnclamped(transform.rotation, newrot, Time.deltaTime * 100);
            return;
        }

        // if not grounded, rotate to upright position
        RaycastHit hit;
        Vector3 origin = transform.position + transform.up * 0.5f;
        if (Physics.Raycast(origin, -transform.up, out hit, groundMask)) // initial raycast to see if the ground is close enough to snap to
        {
            // Get the normal of the ground surface
            Vector3 newup = hit.normal; // angle of the initial hit
            float angle = Vector3.Angle(transform.up, newup);

            // limit the angle to prevent extreme rotations
            if (angle > 30)
            {
                return;
            }


            // Calculate the new forward direction based on the ground normal
            Vector3 cross = Vector3.Cross(transform.right, newup); // new foward direction

            // Smoothly rotate towards the new orientation
            Quaternion newrot = Quaternion.LookRotation(cross);

            // Apply the rotation
            transform.rotation = Quaternion.LerpUnclamped(transform.rotation, newrot, Time.deltaTime * 100f);

            // transform.rotation = Quaternion.FromToRotation(transform.up, angle);

            // transform.position = hit.point + transform.up * 0.01f;


        }
    }
    // Temporarily disable player control
    public void SurrenderControl(Vector2 up, float newSurrenderTime)
    {
        StopCoroutine(Surrender(0));
        StartCoroutine(Surrender(newSurrenderTime));
    }

    // Coroutine to handle surrendering control
    private IEnumerator Surrender(float time)
    {
        isSurrendered = true;
        yield return new WaitForSeconds(time);
        isSurrendered = false;
    }

    // Launch the player in a specific direction with a given height
    public void Launch(Vector3 direction, float height)
    {
        GetComponent<Rigidbody>().linearVelocity = Mathf.Sqrt(height * -2 * Physics.gravity.y) * direction;

    }
}
#endregion