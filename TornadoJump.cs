using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TornadoJump : MonoBehaviour
{
    public GameObject pole;
    public GameObject ExitPole;
    public GameObject JumpDirection;
    public bool tornadoJump;
    public bool teamSwing;
    public float startingSpeed = 5.0f;
    public float maxStartingSpeed = 10.0f;
    public float jumpForce;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void RotateToSwing(GameObject speedCharacter)
    {

    }
    public void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player") && Input.GetKeyDown(KeyCode.B))
        {
            StartCoroutine(Swinging(other.gameObject));
        }
    }
    public void Swing()
    {

    }
    public IEnumerator Swinging(GameObject speedCharacter)
    {
        UltimatePlayerMovement.Controllable = false;
        speedCharacter.GetComponent<Rigidbody>().useGravity = false;
        speedCharacter.GetComponentInParent<UltimatePlayerMovement>().body.velocity = Vector3.zero;
        speedCharacter.GetComponent<UltimatePlayerMovement>().leftFollower.SetActive(false);
        speedCharacter.GetComponent<UltimatePlayerMovement>().rightFollower.SetActive(false);

        speedCharacter.transform.position = transform.position;
        //speedCharacter.GetComponentInChildren<Animator>().Play("Tornado Jump");
        

        while (speedCharacter.transform.position.y < ExitPole.transform.position.y)
        {
            //speedCharacter.GetComponent<Rigidbody>().AddForce(new Vector3(0, startingSpeed, 0));
            speedCharacter.transform.Translate(new Vector3(0, startingSpeed, 0));
            if (speedCharacter.GetComponent<Rigidbody>().velocity.y > maxStartingSpeed)
            {
                speedCharacter.GetComponent<Rigidbody>().velocity = new Vector3(0, maxStartingSpeed, 0);
            }

            yield return new WaitForSeconds(Time.deltaTime);


        }

        speedCharacter.transform.position = ExitPole.transform.position;

        Vector3 jumpDirection = ExitPole.transform.position + JumpDirection.transform.position;
        jumpDirection = jumpDirection.normalized;
        jumpDirection *= jumpForce;
        speedCharacter.GetComponent<CharacterController>().Move(jumpDirection);

        
        yield return null;
    }

    
}

