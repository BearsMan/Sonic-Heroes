using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spring : MonoBehaviour
{
    GameObject source;
    public AudioClip clip;
    public int springForce = 1400;
    private float surrenderTime = 0.8f;

    // Start is called before the first frame update
    void Start()
    {
        source = Resources.Load<GameObject>("Audio Object");
        //Using the Resources folder in the Assests folder you can load up any kind of object
        //With this above line of code. It loads up a GameObject named "Audio Object" From the Resources folder 
        //And applies it to the source variable
    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnTriggerEnter(Collider other)
    {
        
        if (other.CompareTag("Player"))
        

        
        {
            other.transform.position = other.transform.position + transform.up * 0.5f;
            Rigidbody body = other.GetComponent<Rigidbody>();
            body.velocity = Vector3.zero;
            body.AddForce(transform.up * springForce);
            
            GameObject ao = Instantiate(source, transform.position, Quaternion.identity);
            ao.GetComponent<AudioObject>().Setup(clip, transform);
            
            UltimatePlayerMovement player = other.GetComponent<UltimatePlayerMovement>();
            //player.SurrenderControl(surrenderTime);
        }

        


        
    }

    private void OnTriggerExit(Collider other)
    {

    }
}