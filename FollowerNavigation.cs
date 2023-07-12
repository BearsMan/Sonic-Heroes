using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class FollowerNavigation : MonoBehaviour
{
    public Transform target;
    public NavMeshAgent agent;
    public Animator anim;
    public Rigidbody body;
    public bool grounded;
    public LayerMask groundMask;
    private bool isSetup;

    // Start is called before the first frame update
    public void Setup()
    {
        anim = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        body = GetComponent<Rigidbody>();
        isSetup = true;
    }

    // Update is called once per frame
    void Update()
    {
        grounded = Physics.CheckSphere(transform.position + (-transform.up * 0.55f), 0.48f, groundMask);
        
        if (!isSetup) return;
        if (anim)
        {
            anim.SetFloat("Speed", agent.velocity.magnitude);
        }
        
        
            RotateToPos();

        agent.SetDestination(target.position);

    }
    public void RotateToPos()
    {
        if (agent.velocity.magnitude < 0.5f)
        {
            transform.rotation = target.rotation;
        }

    }
    public void Jump(Vector3 velocity)
    {
        
        agent.enabled = false;
        body.velocity = velocity;
    }

    public bool PathValid(Vector3 dest)
    {
        float dis = Vector3.Distance(transform.position, dest);
        if (dis > 10)
        {
            return false;
        }
        else return true;
        /*
        NavMeshPath path = new NavMeshPath();
        
        NavMesh.CalculatePath(transform.position, dest, NavMesh.AllAreas, path); //The Path to the wall will only work if the distance returns a valid path
        return path.status == NavMeshPathStatus.PathComplete;
        */
    }
}
