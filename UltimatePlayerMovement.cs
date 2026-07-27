using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CameraController))]
public class UltimatePlayerMovement : MonoBehaviour
{
    #region constant variables
    public const float acceleration = 5f;
    public const float deceleration = 3f;
    public const float runSpeed = 20f;
    public const float airSpeed = 15f;
    #endregion
    public float currentSpeed = runSpeed;
    #region public variables
    public GameObject leftFollower;
    public GameObject rightFollower;
    public GameObject sonic;
    public GameObject superSonic;
    [SerializeField] private Rigidbody body;
    [SerializeField] private Transform cam;
    [SerializeField] private Transform groundCheck;
    public LayerMask groundMask;
    public bool tutorialPlaying = false;
    [Header("Runtime Debug")]
    [SerializeField] private bool movementEnabledDebug;
    #endregion

    #region private variables
    public GameObject currentCharacter;

    public bool isGrounded = false;
    private bool isSurrendered = false;
    public bool TrickZone { get; internal set; }

    public bool MovementEnabled {  get; internal set; }
    public object LeftTeamMember { get; internal set; }
    public object RightTeamMember { get; internal set; }
    public object TeamSetup { get; private set; }

    private void Awake()
    {
        MovementEnabled = true;

        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }

        if (cam == null)
        {
            var camCtrl = FindAnyObjectByType<CameraController>();
            if (camCtrl != null)
            {
                cam = camCtrl.transform;
            }
        }
    }
    private void Update()
    {
        movementEnabledDebug = MovementEnabled;
        if (groundCheck != null)
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, 0.2f, groundMask);

            if (tutorialPlaying)
            {
                return;
            }

            if (anim != null)
            {
                anim.SetBool("InAir", !isGrounded);
            }
            if (isGrounded == true)
            {
                if (leftFollower != null && leftFollower.TryGetComponent<FollowerNavigation>(out var lfNav) && lfNav.agent != null)
                {
                    lfNav.agent.enabled = true;
                }

                if (rightFollower != null && rightFollower.TryGetComponent<FollowerNavigation>(out var rfNav) && rfNav.agent != null)
                {
                    rfNav.agent.enabled = true;
                }
            }

            RotateToGround();
        }
    }

    private void FixedUpdate()
    {
        Move();
    }
    private Animator anim;
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

        Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        if (tutorialPlaying)
        {
            right = Vector3.zero;
            forward = Vector3.zero;
        }

        Vector3 mov = Vector3.zero;

        if (!isSurrendered)
        {
            mov = Input.GetAxisRaw("Vertical") * forward + Input.GetAxisRaw("Horizontal") * right;

            mov = Vector3.ClampMagnitude(mov, 1f);

            Jump();
            Turn();
        }

        if (isGrounded)
        {
            GroundMovement(mov);
        }
        else
        {
            AirMovement(mov);
        }
    }

    private void GroundMovement(Vector3 mov)
    {
        mov.y = 0f;

        maxAirSpeed = currentSpeed;
        direction = mov * currentSpeed;

        if (direction.magnitude > currentSpeed)
        {
            direction = direction.normalized * currentSpeed;
        }

        // Stop any remaining downward velocity after reaching the ground.
        Vector3 velocity = body.linearVelocity;

        if (velocity.y < 0f)
        {
            velocity.y = 0f;
            body.linearVelocity = velocity;
        }

        Vector3 targetPosition = body.position + direction * Time.fixedDeltaTime;

        body.MovePosition(targetPosition);
    }
    private float maxAirSpeed;
    private float airControl = 20f;
    private void AirMovement(Vector3 mov)
    {
        body.AddForce(mov * airControl);

        Vector3 veloXZ = body.linearVelocity;
        veloXZ.y = 0f;

        if (veloXZ.magnitude > maxAirSpeed)
        {
            veloXZ = veloXZ.normalized * maxAirSpeed;
            veloXZ.y = body.linearVelocity.y;
            body.linearVelocity = veloXZ;
        }
    }
    private float jumpHeight = 6f;
    private Vector3 direction;

    public void Jump()
    {
        if (!MovementEnabled)
        {
            return;
        }
        
        if (Input.GetKey(KeyCode.Space) && isGrounded)
        {
            Vector3 velo = direction * 0.8f;
            velo.y = Mathf.Sqrt(jumpHeight * -2 * Physics.gravity.y);
            body.linearVelocity = velo;
        }


    }
    public void Turn()
    {
        if (!MovementEnabled)
        {
            return;
        }
        
        transform.Rotate(transform.up, Input.GetAxis("Mouse X"));
    }
    public void RotateToGround()
    {
        if (!isGrounded)
        {
            Vector3 cross = Vector3.Cross(transform.right, Vector3.up);
            Quaternion newrot = Quaternion.LookRotation(cross);
            transform.rotation = Quaternion.LerpUnclamped(transform.rotation, newrot, Time.deltaTime * 100);
            return;
        }

        RaycastHit hit;
        Vector3 origin = transform.position + transform.up * 0.5f;
        if (Physics.Raycast(origin, -transform.up, out hit, groundMask)) // initial raycast to see if the ground is close enough to snap to
        {

            Vector3 newup = hit.normal;//angle of the initial hit
            float angle = Vector3.Angle(transform.up, newup);

            if (angle > 30)
            {
                return;
            }

            Vector3 cross = Vector3.Cross(transform.right, newup); // new foward direction


            Quaternion newrot = Quaternion.LookRotation(cross);

            transform.rotation = Quaternion.LerpUnclamped(transform.rotation, newrot, Time.deltaTime * 100f);

            // transform.rotation = Quaternion.FromToRotation(transform.up, angle);

            // transform.position = hit.point + transform.up * 0.01f;


        }
    }

    public void StopMovement()
    {
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
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
        StopCoroutine(Surrender(0));
        StartCoroutine(Surrender(newSurrenderTime));
    }

    private IEnumerator Surrender(float time)
    {
        isSurrendered = true;
        yield return new WaitForSeconds(time);
        isSurrendered = false;
    }

    public void Launch(Vector3 direction, float height)
    {
        body.linearVelocity = Mathf.Sqrt(height * -2 * Physics.gravity.y) * direction;

    }
}
#endregion
