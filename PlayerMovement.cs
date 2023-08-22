using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]

    public int healthPoint = 100;
    private float currentSpeed = 0f;
    private float maxRunSpeed = 12f;
    private float maxWalkSpeed = 7f;
    private bool running;
    private float CurrentMaxSpeed
    {
        get
        {
            if (running) return maxRunSpeed;
            else return maxWalkSpeed;
        }
    }
    private float acceleration = 5f;
    private float jumpHeight = 10f;
    public bool isJumping = false;
    public bool grounded = false;
    public bool trickZone = false;
    public Vector3 spawnpoint;
    private Vector3 velocity = new Vector3();
    public Vector2 movement = new Vector2();
    public GameObject HUDobject;
    public GameObject Leftpos;
    public GameObject Rightpos;

    [Header("Utility")]
    public Rigidbody body;
    public LayerMask groundMask;
    public Transform eyes;
    public Transform characterbody;

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("GameObject");
        body = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown("v"))
        {
            DebugkillSwitch();
        }

        if (!surrendered) //If surrended control disable inputs
        {
            movement.x = Input.GetAxis("Horizontal");
            movement.y = Input.GetAxis("Vertical");



        }


        running = Input.GetKey(KeyCode.LeftShift);




        if (Input.GetKeyDown(KeyCode.Space) && grounded)

        {
            isJumping = true;

        }
        if (currentSpeed > CurrentMaxSpeed)
        {
            currentSpeed = CurrentMaxSpeed;
        }

        if (surrendered) //If surrended count down timer and restore control when set to 0
        {
            surrenderDuration -= Time.deltaTime;
            if (surrenderDuration <= 0) surrendered = false;
        }

        if (trickZone)
        {
            RaycastHit groundHit;
            if (Physics.Raycast(transform.position, -transform.up, out groundHit, 5))
            {

                Vector3 hitAngle = groundHit.normal;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(Vector3.Cross(hitAngle, -transform.right)), 360 * Time.deltaTime);

            }
            else
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, new Quaternion(0, transform.rotation.y, 0, transform.rotation.w), 360 * Time.deltaTime);
            }
        }
        else
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, new Quaternion(0, transform.rotation.y, 0, transform.rotation.w), 360 * Time.deltaTime);
        }

    }


    void FixedUpdate()
    {

        MovePlayer(movement.x, movement.y);

    }
    void MovePlayer(float moveRight, float moveForward)
    {
        grounded = Physics.CheckSphere(transform.position + (-transform.up * 0.55f), 0.48f, groundMask);
        Vector3 moveDir = transform.forward * moveForward + transform.right * moveRight;
        if (moveDir.sqrMagnitude > 0)
        {
            currentSpeed += acceleration * Time.fixedDeltaTime;
        }
        else
        {
            currentSpeed -= acceleration * Time.fixedDeltaTime * 1.2f;
        }

        currentSpeed = Mathf.Clamp(currentSpeed, 0, CurrentMaxSpeed);

        moveDir *= currentSpeed;

        if (isJumping)
        {

            velocity = Mathf.Sqrt(jumpHeight * -2 * Physics.gravity.y) * transform.up;
        }

        if (!grounded)
        {
            Leftpos.GetComponent<FollowerNavigation>().Jump(velocity);
            Rightpos.GetComponent<FollowerNavigation>().Jump(velocity);

        }

        velocity += Physics.gravity.y * Time.fixedDeltaTime * transform.up;

        if (!grounded || velocity.y > 0)
        {
            body.velocity = velocity + moveDir * Time.fixedDeltaTime;
        }
        else
        {
            body.velocity = moveDir;
        }

        velocity = body.velocity;

        Vector3 velocityXZ = velocity;
        velocityXZ.y = 0;
        if (velocityXZ.sqrMagnitude > 0.3f)
            characterbody.rotation = Quaternion.LookRotation(velocityXZ);

        if (isJumping)
        {

            isJumping = false;

        }
    }



    public void Launch(Vector3 direction, float height)
    {
        isJumping = false;
        velocity = Mathf.Sqrt(height * -2 * Physics.gravity.y) * direction;
        body.velocity = velocity;

    }


    public void Boost(float newSpeed, Vector3 newDirection)
    {
        currentSpeed = newSpeed; //Force current speed to launch speed
        maxRunSpeed = newSpeed; //Allow max speed to become launch speed
        transform.rotation = Quaternion.LookRotation(newDirection); //Force foward rotation to launch direction
    }
    private float surrenderDuration;
    private bool surrendered = false;

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

    public void RestoreControl()
    {
        surrendered = false;
    }

    void DebugkillSwitch()
    {
        RespawnPlayer();
    }

    public void RespawnPlayer()
    {
        transform.position = spawnpoint;
    }

}