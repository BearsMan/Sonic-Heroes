using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriangleJump : MonoBehaviour
{
    public bool triangleJump;
    public bool isJumping;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void stick(GameObject speedCharacter)
    {
        UltimatePlayerMovement.Controllable = false;
        speedCharacter.GetComponent<Rigidbody>().useGravity = false;
        speedCharacter.GetComponentInParent <UltimatePlayerMovement>().body.velocity = Vector3.zero;
        speedCharacter.GetComponent<Animator>().Play("Triangle Jump");
    }

    public void Jump()
    {
        
    }
    public IEnumerator WallJump()
    {
        yield return null;
    }
    public void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<HomingAttack>().homingAttackUsed==true)
        {
            stick(other.gameObject);
        }
    }
}
