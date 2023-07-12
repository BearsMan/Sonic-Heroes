using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Orca : MonoBehaviour
{
    public GameObject orca;
    public float speed = 0.0f;
    public float jumpSpeed = 0.0f;
    public bool isSwimming = false;
    public bool reachTop = false;
    public float swimmingSpeed = 0.0f;
    public bool isJumping = false;
    public float floatingPosition;
    public float floatStrength;
    public float randomRotationStrength;
    public int jumpHeight;
    public Vector3 topPosition, bottomPosition, targetPosition;
    // Start is called before the first frame update
    void Start()
    {
        Rigidbody body = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        if (isJumping)
        {
            Jump();
        }

        if (isJumping == false && Input.GetKeyDown(KeyCode.O))
        {
            topPosition = new Vector3(transform.position.x, transform.position.y + jumpHeight, transform.position.z);
            bottomPosition = transform.position;
            isJumping = true;
            targetPosition = topPosition;
        }
    }

        private void FixedUpdate()
    {

        if (transform.position.y < floatingPosition)
        {
            //force being applied upwards to move object up
            transform.GetComponent<Rigidbody>().AddForce(Vector3.up * floatStrength);
            transform.Rotate(randomRotationStrength, randomRotationStrength, randomRotationStrength);
        }
        if (transform.position.y >= floatingPosition)
        {
            //force applied is less than the gravitational force so that the object comes down. Here mass of object is 2.
            transform.GetComponent<Rigidbody>().AddForce(Vector3.up * 11.0f);
            transform.Rotate(randomRotationStrength, randomRotationStrength, randomRotationStrength);
        }
    }

    public void Jump()
    {
        
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, jumpSpeed * Time.deltaTime);
        if (Vector3.Distance(transform.position, topPosition) < 0.001f)
        {
            targetPosition = bottomPosition;
            reachTop = true;
        }

        if (reachTop == true && Vector3.Distance(transform.position, bottomPosition) < 0.001f)
        {
            isJumping = false;
        }
    }
}


