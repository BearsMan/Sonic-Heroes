using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
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
    public Rigidbody body;
    public Transform cam;
    public LayerMask groundMask;
    public Vector3 moveForce;
    public bool tutorialPlaying = false;
    #endregion

    #region private variables
    public GameObject currentCharacter;
    public bool useGravity;
    private Quaternion cameraWorldFoward
    {
        get
        {
            return Quaternion.LookRotation(cam.forward, transform.up);
        }
    }

    public bool isGrounded;
    private float jumpSustainTime = 0f;
    private bool isSurrendered = false;

    private float currentMaxSpeed
    {
        get
        {
            return isGrounded ? currentSpeed : airSpeed;
        }
    }

    public static bool Controllable { get; internal set; }
    public bool TrickZone { get; internal set; }
    public object LeftTeamMember { get; internal set; }
    public object RightTeamMember { get; internal set; }
    public object TeamSetup { get; private set; }
    #endregion


    private void Awake()
    {
        cam = FindFirstObjectByType<CameraController>().transform;
    }
    private void Update()
    {
        isGrounded = Physics.CheckSphere(transform.position + transform.up * 0.1f, 0.49f, groundMask);
        if (tutorialPlaying) return;

        if (anim != null)
        {
            anim.SetBool("InAir", !isGrounded);
        }
        if (isGrounded == true)
        {
            leftFollower.GetComponent<FollowerNavigation>().agent.enabled = true;
            rightFollower.GetComponent<FollowerNavigation>().agent.enabled = true;
        }
        //RotateToGround();
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


    public void OldMove()
    {
        Vector3 right = Vector3.Cross(transform.up, cam.forward);
        Vector3 forward = Vector3.Cross(right, transform.up);

        if (tutorialPlaying)
        {
            right = Vector3.zero;
            forward = Vector3.zero;
        }

        Vector3 mov = Vector3.zero;



        if (!isSurrendered)
        {
            mov = Input.GetAxis("Vertical") * forward + Input.GetAxis("Horizontal") * right;
            Jump();
            Turn();
        }


        Vector3 velocityXZ = body.velocity;
        velocityXZ.y = 0;
        direction = mov * currentSpeed;
        if (isGrounded)
        {
            body.useGravity = false;
            //body.velocity = mov * runSpeed;
            body.MovePosition(transform.position + (mov * currentSpeed * Time.fixedDeltaTime));
        }
        else
        {
            body.useGravity = true;
            //body.AddForce(Vector3.down * 90);
            body.AddForce(mov * 50);

            if (velocityXZ.magnitude > currentSpeed)
            {
                velocityXZ = velocityXZ.normalized * currentSpeed;
                velocityXZ.y = body.velocity.y;
                body.velocity = velocityXZ;
            }
        }
        if (anim != null)
        {
            anim.SetFloat("Speed", velocityXZ.magnitude);
        }

    }

    public void Move()
    {
        Vector3 right = Vector3.Cross(transform.up, cam.forward);
        Vector3 forward = Vector3.Cross(right, transform.up);


        if (tutorialPlaying)
        {
            right = Vector3.zero;
            forward = Vector3.zero;
        }

        Vector3 mov = Vector3.zero;
        if (!isSurrendered)
        {
            mov = Input.GetAxis("Vertical") * forward + Input.GetAxis("Horizontal") * right;
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
        maxAirSpeed = currentSpeed;
        direction = mov * currentSpeed;
        if (direction.magnitude > currentSpeed)
        {
            direction = direction.normalized * currentSpeed;
        }

        body.MovePosition(transform.position + transform.TransformDirection(direction) * Time.fixedDeltaTime);
    }
    private float maxAirSpeed;
    private float airControl = 20f;
    private void AirMovement(Vector3 mov)
    {
        body.AddForce(transform.TransformDirection(mov * airControl));
        Vector3 veloXZ = body.velocity;
        veloXZ.y = 0;
        if (veloXZ.magnitude > maxAirSpeed)
        {
            veloXZ = veloXZ.normalized * maxAirSpeed;
            veloXZ.y = body.velocity.y;
            body.velocity = veloXZ;
        }
    }
    private float jumpHeight = 6f;
    private Vector3 direction;

    public void Jump()
    {
        if (Input.GetKey(KeyCode.Space) && isGrounded)
        {
            Vector3 velo = transform.TransformDirection(direction * 0.8f);
            velo.y = Mathf.Sqrt(jumpHeight * -2 * Physics.gravity.y);
            body.velocity = velo;
        }


    }


    public void Turn()
    {
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
        if (Physics.Raycast(origin, -transform.up, out hit, groundMask))//initial raycast to see if the ground is close enough to snap to
        {

            Vector3 newup = hit.normal;//angle of the initial hit
            float angle = Vector3.Angle(transform.up, newup);

            if (angle > 30)
            {
                return;
            }



            Vector3 cross = Vector3.Cross(transform.right, newup);//new foward direction


            Quaternion newrot = Quaternion.LookRotation(cross);

            transform.rotation = Quaternion.LerpUnclamped(transform.rotation, newrot, Time.deltaTime * 100f);

            //transform.rotation = Quaternion.FromToRotation(transform.up, angle);

            //transform.position = hit.point + transform.up * 0.01f;


        }
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
        GetComponent<Rigidbody>().velocity = Mathf.Sqrt(height * -2 * Physics.gravity.y) * direction;

    }
}