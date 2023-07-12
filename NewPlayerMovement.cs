using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewPlayerMovement : MonoBehaviour
{
    public CharacterController controller;
    public GameObject teamSetup;
    public enum STATE
    {
        NORMAL,
        HURT,
        DASHING,
        SPECIAL,
        ATTACK,
        DEAD,

    }
    public bool isSuper = false;
    private const float maxSpeed = 8;
    private const float maxSuperSpeed = 12;

    public float CurrentMaxSpeed
    {
        get
        {
            if (isSuper) return maxSuperSpeed;
            return maxSpeed;
        }
    }

    private const float acceleration = 5;
    private const float superAcceleration = 7;
    public float CurrentAcceleration
    {
        get
        {
            if (isSuper) return superAcceleration;
            return acceleration;
        }
    }

    private const float jumpHeight = 3;
    public float currentJumpHeight = 3;

    private Vector3 gravity;

    private Vector3 velocity = Vector3.zero;
    private Vector3 previousPostition;
    private Vector2 movement = Vector2.zero;
    public LayerMask groundMask;
    private bool isJumping;
    public bool isGrounded;
    public bool inAir
    {
        get
        {
            return (!isGrounded || velocity.y > 0);
        }
    }
    private bool jump;
    public float currentSpeed;
    public float maxRunSpeed;
    public float maxWalkSpeed;
    public GameObject Leftpos;
    public GameObject Rightpos;
    public GameObject LeftTeamMember;
    public GameObject RightTeamMember;
    public Rigidbody body;
    public Camera MainCamera;


    public float ToZero(float initialNumber, float deltavalue)
    {
        if (initialNumber > 0)
        {
            initialNumber -= deltavalue;
            initialNumber = Mathf.Clamp(initialNumber, 0, Mathf.Infinity);
        }
        if (initialNumber < 0)
        {
            initialNumber += deltavalue;
            initialNumber = Mathf.Clamp(initialNumber, Mathf.NegativeInfinity, 0);
        }
        return initialNumber;
    }

    // Start is called before the first frame update
    void Start()
    {
        MainCamera = Camera.main;
        previousPostition = transform.position;
        gravity = Physics.gravity;
    }

    // Update is called once per frame
    void Update()
    {
        if (controllable == true)
        {


            if (surrendered) //If surrended count down timer and restore control when set to 0
            {
                surrenderDuration -= Time.deltaTime;
                if (surrenderDuration <= 0) surrendered = false;
            }
            else Inputs();
            Movement();

        }
        if (Input.GetKeyDown(KeyCode.Home))
        {
            BecomeSuper();
        }
    }
    public void BecomeSuper()
    {
        GameInstance.greenEmerald = true;
        GameInstance.blueEmerald = true;
        GameInstance.yellowEmerald = true;
        GameInstance.whiteEmerald = true;
        GameInstance.lightBlueEmerald = true;
        GameInstance.purpleEmerald = true;
        GameInstance.redEmerald = true;

        teamSetup.GetComponent<TeamSetup>().SwapForSuper();
    }

    private void LateUpdate()
    {
        transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward, Vector3.up);

        RaycastHit groundHit;

        if (Physics.Raycast(transform.position + (transform.up * 0.1f), -transform.up, out groundHit, 0.3f, groundMask) && velocity.y < 0)
        {
            Vector3 velocityXZ = velocity;
            velocityXZ.y = 0;
            if (velocityXZ.sqrMagnitude > 0.3f)
                transform.localRotation = Quaternion.LookRotation(velocity, transform.up);


            velocity.y = -0.5f;


            Vector3 hitAngle = groundHit.normal;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(Vector3.Cross(hitAngle, -transform.right)), 360 * Time.deltaTime);
            transform.position = groundHit.point + (transform.up * 0.08f);

        }
        else
        {
            transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward, Vector3.up);

        }
    }

    public void Inputs()
    {
        Vector2 mov = Vector2.zero;
        mov.x = Input.GetAxis("Horizontal");
        mov.y = Input.GetAxis("Vertical");
        if (mov.x == 0)
        {
            movement.x = ToZero(movement.x, Time.deltaTime * acceleration);
        }
        else
        {
            if ((mov.x > 0 && movement.x > 0) || (mov.x < 0 && movement.x < 0))
            {
                movement.x += mov.x * acceleration * Time.deltaTime;
            }
            else
            {
                movement.x += mov.x * acceleration * Time.deltaTime * 3;
            }

        }
        if (mov.y == 0)
        {
            movement.y = ToZero(movement.y, Time.deltaTime * acceleration);
        }
        else
        {
            if ((mov.y > 0 && movement.y > 0) || (mov.y < 0 && movement.y < 0))
            {
                movement.y += mov.y * acceleration * Time.deltaTime;
            }
            else
            {
                movement.y += mov.y * acceleration * Time.deltaTime * 3;
            }

        }


        movement.x = Mathf.Clamp(movement.x, -CurrentMaxSpeed, CurrentMaxSpeed);
        movement.y = Mathf.Clamp(movement.y, -CurrentMaxSpeed, CurrentMaxSpeed);

        float rot = Input.GetAxis("Mouse X");
        transform.Rotate(transform.up * rot);

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded) isJumping = true;
    }
    
    public void Movement()
    {
        isGrounded = Physics.CheckSphere(transform.position + transform.up * 0.45f, 0.6f, groundMask);


        Transform cam = Camera.main.transform;
        Vector3 mov = cam.forward * movement.y + cam.right * movement.x;
        if (isJumping)
        {
            isJumping = false;



            mov += Mathf.Sqrt(currentJumpHeight * -2 * gravity.y) * transform.up;
            velocity = mov;


            LeftTeamMember.GetComponent<FollowerNavigation>().Jump(velocity);
            RightTeamMember.GetComponent<FollowerNavigation>().Jump(velocity);


        }
        if (!isGrounded || velocity.y > 0)
        {
            mov = velocity + mov * Time.deltaTime;
            velocity = mov;


        }
        velocity += gravity * Time.deltaTime;







        controller.Move(mov * Time.deltaTime);

        RotateLeader(mov);

    }


    public void RotateLeader(Vector3 dir)
    {
        if (dir != Vector3.zero)
        {
            dir = new Vector3(dir.x, 0, dir.z);
            transform.rotation = Quaternion.LookRotation(dir);

        }
    }
    public void Boost(float newSpeed, Vector3 newDirection)
    {
        currentSpeed = newSpeed; //Force current speed to launch speed
        maxRunSpeed = newSpeed; //Allow max speed to become launch speed
        controller.Move(newDirection * newSpeed);
        transform.rotation = Quaternion.LookRotation(newDirection); //Force foward rotation to launch direction
    }

    public void Launch(Vector3 direction, float height)
    {
        velocity = Mathf.Sqrt(height * -2 * Physics.gravity.y) * direction;

    }

    private float surrenderDuration;
    private bool surrendered = false;
    public static bool controllable = true;
    public bool trickZone;
    internal bool isSwinging;

    /// <summary>
    /// Remove player control for specified time
    /// Force player direction for duration
    /// Use vector2.up for forward movement
    /// </summary>
    /// <param name="direction"></param>
    /// <param name="duration"></param>
    public void SurrenderControl(Vector2 direction, float duration)
    {
        movement = direction; //Direction of movement

        surrenderDuration = duration;

        surrendered = true;
    }


}



