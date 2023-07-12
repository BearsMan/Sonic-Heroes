using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Aligator : MonoBehaviour
{
    public GameObject aligator;
    public bool isBiting = false;
    public bool isJumping = false;
    public bool aimToBite = false;
    public float xDirection, yDirection, zDirection = 0.0f;
    public float swimmingSpeed = 0.0f;
    public float rotationMovement = 0.0f;
    public float jumpSpeed = 0.0f;
    public int jumpHeight = 0;
    private Animator anim;
    public Vector3 topPosition, bottomPosition, targetPosition;
    // Start is called before the first frame update
    void Start()
    {
        Rigidbody body = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        GetComponent<Rigidbody>().velocity = Vector3.zero;
        GetComponent<UltimatePlayerMovement>().isGrounded = false;
        GetComponent<Animator>().enabled = isBiting;

        if (isJumping)
        {
            Jump();
        }

        if (isJumping == false && Input.GetKeyDown(KeyCode.S))
        {
            topPosition = new Vector3(transform.position.x, transform.position.y + jumpHeight, transform.position.z);
            bottomPosition = transform.position;
            isJumping = true;
            targetPosition = topPosition;
        }
    }

        public void Jump()
    {

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, jumpSpeed * Time.deltaTime);
        if (Vector3.Distance(transform.position, topPosition) < 0.001f)
        {
            targetPosition = bottomPosition;
            isJumping = true;
        }

        if (isJumping == true && Vector3.Distance(transform.position, bottomPosition) < 0.001f)
        {
            isJumping = false;
        }

        }
}
