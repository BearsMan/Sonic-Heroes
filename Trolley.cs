using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Trolley : MonoBehaviour
{
    public GameObject trolleyCar;
    public bool isGrounded;
    public bool useGravity;
    public bool acceleration;
    public bool deacceleration;
    public float speed = 0.0f;
    public float carJump = 0.0f;
    public bool driveForward = false;
    public bool turnLeft = false;
    public bool turnRight = false;
    public int turnRotation = 0;
    public Rigidbody body;
    public LayerMask roadCheckMask;
    public Vector3 turn;
    public Vector3 moveDirection;
    public Vector3 jump;
    // Start is called before the first frame update
    void Start()
    {
        Rigidbody rigidbody = GetComponent<Rigidbody>();
        moveDirection = body.velocity;
    }

    // Update is called once per frame
    void Update()
    {
        RotateToGround();
        Move();
        Jump();
        Vector3 velocity = moveDirection * Time.deltaTime;
        body.MovePosition(velocity);
    }

    public void FixedUpdate()
    {
        {

            float horizontalInput = Input.GetAxis("Horizontal");
            float verticalInput = Input.GetAxis("Vertical");


            Vector3 moveDirection = new Vector3(horizontalInput, 0, verticalInput);


            float magnitude = Mathf.Clamp01(moveDirection.magnitude) * Time.deltaTime;


            moveDirection.Normalize();
        }
    }
    public void Drive()
    {
        if (driveForward == false && Input.GetKeyDown(KeyCode.A))
        {
            transform.position = Vector3.MoveTowards(moveDirection, Vector3.up, Time.deltaTime);
            transform.position = Vector3.Cross(jump, transform.position);
        } 
    }

    public void Move()
    {
        transform.position = Vector3.MoveTowards(moveDirection, Vector3.right, Time.deltaTime);
        transform.position = Vector3.LerpUnclamped(moveDirection, Vector3.up, Time.deltaTime);

    }

    public void Jump()
    {
        if (driveForward == false && Input.GetKeyDown(KeyCode.Space))
        {
            transform.position = transform.right * Time.deltaTime;
        }
    }

    public void RotateToGround()
        {
            RaycastHit hit;
            Vector3 origin = transform.position + transform.up * 0.5f;
            if (Physics.Raycast(origin, -transform.up, out hit, roadCheckMask))
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
}
