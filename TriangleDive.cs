using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriangleDive : MonoBehaviour
{
    public GameObject teamForm;
    public bool diving = false;
    public bool triangleColorForm = false;
    public float radius = 0.0f;
    public float triangleSpeedControl = 0.0f;
    // Start is called before the first frame update
    void Start()
    {
        Rigidbody Rigidbody = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void StartTriangleDive(GameObject player)
    {
        diving = true;
        player.GetComponentInChildren<CharacterAnimationController>().anim.Play("Triangle Dive");
    }

    public void TriangleForm(PowerCharacter GameObject)
    {
        SpeedCharacter.Instantiate(gameObject);
        FlyCharacter.Instantiate(gameObject);
    }
    public void OnTriggerStay(Collider other)
    {
        if (other.GetComponent<UltimatePlayerMovement>() == true)
        {
            if (Input.GetKeyDown(KeyCode.Space) && diving == false)
            {
                StartTriangleDive(other.gameObject);
            }
        }
    }
}
