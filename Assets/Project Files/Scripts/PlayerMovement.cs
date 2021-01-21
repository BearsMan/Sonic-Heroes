using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public int healthpoint = 100;
    public float speed = 10;
    public float jumpHeight = 10;
    public bool isJumping = false;
    public bool grounded = false;
    public Vector3 velocity = new Vector3();
    public Vector2 movement = new Vector2();
    public Rigidbody body;
    public LayerMask groundMask;
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("GameObject");
        body = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {

        movement.x = Input.GetAxis("Vertical");
        movement.y = Input.GetAxis("Horizontal");

        if (Input.GetKeyDown(KeyCode.Space) && grounded)
        {
            isJumping = true;
        }
    }


    void FixedUpdate()
    {
        MovePlayer(movement.y, movement.x);

    }
    void MovePlayer(float moveRight, float moveForward)
    {
        grounded = Physics.CheckSphere(transform.position + (Vector3.down * 0.5f), 0.6f, groundMask);
        Vector3 moveDir = transform.forward * moveForward + transform.right * moveRight;

        moveDir *= speed;

        if (isJumping)
        {
            isJumping = false;
            velocity.y = Mathf.Sqrt(jumpHeight * -2 * Physics.gravity.y);

        }

        velocity.y += Physics.gravity.y * Time.fixedDeltaTime;

        if (!grounded || velocity.y > 0)
        {
            body.velocity = velocity + moveDir * Time.fixedDeltaTime;
        }
        else
        {
            body.velocity = moveDir;
        }
        velocity = body.velocity;
    }


    public void Launch(Vector3 direction, float height)
    {
        velocity = direction.normalized * height;
    }





}