using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HomingAttack : MonoBehaviour
{
    public bool homingAttackAvailable;
    public bool homingAttackUsed = false;
    private Animator anim;
    public UltimatePlayerMovement movement;
    // Start is called before the first frame update
    void Start()
    {
        movement = GetComponentInParent<UltimatePlayerMovement>();
        anim = gameObject.GetComponentInChildren<Animator>();
    }

    // Update is called once per frame
    void Update()
    {


        
        if (movement.isGrounded)
        {
            homingAttackUsed = false;
            homingAttackAvailable = true;

        }
        if (movement.isGrounded == false && Input.GetKeyDown(KeyCode.Space) && homingAttackAvailable == true && homingAttackUsed == false)
        {
            homingAttackUsed = true;
            anim.SetBool("Spin", true);
        }

        if (movement.isGrounded)
        {
            anim.SetBool("Dive Roll", false);
        }

    }
    public void ResetHomingAttack()
    {
        anim.SetBool("Spin", false);

        homingAttackUsed = false;

    }
}
