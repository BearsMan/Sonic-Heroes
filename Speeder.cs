using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Speeder : MonoBehaviour
{

    private float force = 20000;
    GameObject source;
    public AudioClip clip;


    public void OnTriggerEnter(Collider other)
    {



        if (other.TryGetComponent(out Rigidbody body))
        {
            body.velocity = Vector3.zero;
            body.AddForce(transform.forward * force);
            body.transform.rotation = transform.rotation;

            GameObject ao = Instantiate(source, transform.position, Quaternion.identity);
            ao.GetComponent<AudioObject>().Setup(clip, transform);


            if (other.TryGetComponent(out UltimatePlayerMovement player))
            {
                //player.SurrenderControl(0.3f);
            }
        }
    }



    // Start is called before the first frame update
    void Start()
    {
        source = Resources.Load<GameObject>("Audio Object");
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void OnTriggerExit(Collider other)
    {

    }
}
