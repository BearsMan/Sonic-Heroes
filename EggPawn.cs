using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
public class EggPawn : MonoBehaviour
{
    public Transform player;
    private Health myHealth;
    public NavMeshAgent agent;
    private Vector3 origin;
    private float attackTimer = 0;
    public Animator anim;
    // Start is called before the first frame update
    void Start()
    {
        origin = transform.position;
        player = GameObject.FindWithTag("Player").transform;
        myHealth = GetComponent<Health>();
        anim = GetComponent<Animator>();
        anim.SetBool("Sleep", true);
    }

    // Update is called once per frame
    void Update()
    {
        if (myHealth.dead) return;
        if (anim.GetBool("Sleep"))
        {
            return;
        }
        if (Vector3.Distance(origin, player.position) < 30)
        {
            agent.SetDestination(player.position);
            LookAt(player);
            AttackPlayer();
        }
        else
        {
            anim.ResetTrigger("Punch");
            attackTimer = 0;
            agent.SetDestination(origin);
        }
    }

    public void AttackPlayer()
    {
        attackTimer += Time.deltaTime;

        if (Vector3.Distance(transform.position, player.position) < 3 && attackTimer > 2)
        {
            attackTimer = 0;
            anim.SetTrigger("Punch");
        }
    }

    public void DamagePlayer()
    {
        Debug.Log("Damage Player");
    }

    public void LookAt(Transform target)
    {
        Vector3 targetPosition = target.position;
        targetPosition.y = transform.position.y;
        Vector3 direction = targetPosition - transform.position;
        direction.Normalize();
        transform.rotation = Quaternion.LookRotation(direction);
    }
}
