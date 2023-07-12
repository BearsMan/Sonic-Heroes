using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
public class MetalOverlord : MonoBehaviour
{
    public GameObject metalOverlordPrefab;
    public Transform player;
    public bool isFlying = false;
    public bool increasePlayerPowerMeter = false;
    public bool shootingCrystals = false;
    public float attackTimer = 0.0f;
    public int numberOfCrystalsShot = 0;
    public int hitsUntilDeath = 0;
    private Animator anim;
    private Health myHealth;
    private Vector3 origin;

    // Start is called before the first frame update
    void Start()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        origin = transform.position;
        player = GameObject.FindWithTag("Player").transform;
        myHealth = GetComponent<Health>();
        anim.Play("Flying");
        anim.SetBool("Flying", true);

    }

    // Update is called once per frame
    void Update()
    {
        anim = GetComponent<Animator>();
    }


    public void CrystalsShot()
    {
        shootingCrystals = true;

        
    }
    public void TimeTillDeath()
    {
        if (shootingCrystals && increasePlayerPowerMeter == false)
        {
            hitsUntilDeath++;
            if (hitsUntilDeath < 5)
            {
                hitsUntilDeath = 0;
            }
        }
    }
}
