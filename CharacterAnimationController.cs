using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterAnimationController : MonoBehaviour
{
    public Animator anim;
    private bool wasGrounded = true;
    private Rigidbody body;
    private bool rolling;
    private LayerMask groundMask;
    // Start is called before the first frame update
    void Awake()
    {
        body = transform.parent.GetComponent<Rigidbody>();
        groundMask = 8;
    }

    // Update is called once per frame
    void Update()
    {
        bool grounded = Physics.CheckSphere(body.transform.position + (-body.transform.up * 0.55f), 0.48f, groundMask);
        anim.SetFloat("Speed", body.velocity.magnitude);

        if (wasGrounded && !grounded)
        {
            wasGrounded = false;
            anim.SetTrigger("Launch");
            anim.SetBool("InAir", true);
            
            

        }

        if (!wasGrounded && grounded)
        {
            wasGrounded = true;

            anim.SetBool("InAir", false);
            
            
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            rolling = !rolling;
            if (rolling) anim.SetTrigger("RollStart");
            else anim.SetTrigger("RollEnd");
        }


    }
}