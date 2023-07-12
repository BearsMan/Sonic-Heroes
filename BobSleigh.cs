using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BobSleigh : MonoBehaviour
{
    public GameObject bobsleigh;
    public GameObject player;
    public GameObject sonic;
    public bool isDriving = false;
    public bool isGrounded;
    public bool useGravity;
    public bool jump = false;
    public bool driveForward = false;
    public bool turnLeft = false;
    public bool turnRight = false;
    public float groundCheckDistance = 0.0f;
    public float acceleration = 0.0f;
    private float currentMaxSpeed = 0.0f;
    public float speed = 5f;
    public int turn = 0;
    public LayerMask groundCheckMask;
    public Rigidbody body;
    public Transform spawnSpot;
    public Vector3 moveDirection;
    public Vector3 direction;
    public Vector3 rotation;

    // Start is called before the first frame update
    void Start()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        moveDirection = body.velocity;
    }

    // Update is called once per frame
    void Update()
    {
        if (player.transform.parent == bobsleigh.transform)
        {
            Debug.Log("object is attached to wall");
        }

        //if (Input.GetKeyDown(KeyCode.S))
        //{
        //    if (FindObjectOfType<UltimatePlayerMovement>(player.gameObject))
        //    {
        //        Vector3 spawnSpot = Camera.main.ScreenToWorldPoint(player.transform.position);
        //        GameObject objectinstance = Instantiate(bobsleigh, spawnSpot, Quaternion.Euler(new Vector3(0, 0, 0)));
        //        if (bobsleigh .transform.parent != objectinstance)
        //        {
        //            //Vector3.MoveTowards(new Vector3(2252.521f, 200.001f, -6767.733f);
        //        }
        //    }
        //}

        RotateToGround();
        Move();
        Jump();
        Turn();
        Vector3 velocity = moveDirection * Time.deltaTime;
        body.MovePosition(velocity);
    }
    public void FixedUpdate()
    {
        
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");


        Vector3 moveDirection = new Vector3(horizontalInput, 0, verticalInput);


        float magnitude = Mathf.Clamp01(moveDirection.magnitude) * Time.deltaTime;


        moveDirection.Normalize();
    }

    public void OnCollisionEnter(Collision collision)
    {
        if (isGrounded == true)
        {
            float horizational = 0;
            float vertical = 0;
            Vector3 moveDirection = new Vector3 (horizational, 0, vertical);
            useGravity = true;
        }
    }
    

    public void Drive()
    {
        if (driveForward == true)
        {
            transform.position = transform.up = Vector3.zero;
            if (bobsleigh == acceleration > 0.0f)
            {
                body.MovePosition(body.velocity * speed * Time.deltaTime);
            }

        }

    }

    public void Move()
    {
        Vector3 right = Vector3.Cross(transform.up, transform.forward);
        Vector3 forward = Vector3.Cross(right, transform.up);
        if (Input.GetKeyDown(KeyCode.A) && isGrounded)
        {
            transform.position += Vector3.forward * acceleration * Time.deltaTime;
        }



    }
    public void RotateToGround()
    {
        RaycastHit hit;
        Vector3 origin = transform.position + transform.up * 0.5f;
        if (Physics.Raycast(origin, -transform.up, out hit, groundCheckDistance, groundCheckMask))
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
        }

        
    }

    public void Driving()
    {
        if (isDriving && Input.GetKeyDown(KeyCode.UpArrow))
        {
            transform.position = Input.mousePosition;
        }
    }

    public void Jump()
    {
        if (isDriving && Input.GetKeyDown(KeyCode.Space))
        {
            transform.position = Vector3.MoveTowards(transform.position, transform.forward, + Time.deltaTime);
            transform.transform.rotation = Quaternion.LookRotation(transform.right);
        }
    }

    public void Turn()
    {
        if (turnLeft == false && Input.GetKeyDown(KeyCode.LeftArrow))
        {
            transform.position = transform.up * Time.deltaTime;
            transform.rotation *= Quaternion.LookRotation(transform.right);
            direction = new Vector3(currentMaxSpeed, + acceleration);
        }

        if (turnRight == false && Input.GetKeyDown(KeyCode.RightArrow))
        {
            transform.position = transform.right * Time.deltaTime;
            transform.rotation *= Quaternion.LookRotation(-transform.forward);
            direction = new Vector3(currentMaxSpeed, - acceleration);
        }
    }
}
    

