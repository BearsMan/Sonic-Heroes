using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RainbowRing : MonoBehaviour
{
    GameObject source;
    public AudioClip clip;
    // Start is called before the first frame update
    void Start()
    {
        source = Resources.Load<GameObject>("Audio Object");
        //Using the Resources folder in the Assests folder you can load up any kind of object
        //With this above line of code. It loads up a GameObject named "Audio Object" From the Resources folder 
        //And applies it to the source variable
    }



    private void OnTriggerEnter(Collider other)
    {


        UltimatePlayerMovement player = other.GetComponent<UltimatePlayerMovement>();
        if (player)
        {
            GameObject ao = Instantiate(source, transform.position, Quaternion.identity);
            ao.GetComponent<AudioObject>().Setup(clip, transform);

            player.Launch(transform.forward, 100);
            player.SurrenderControl(Vector2.up, 2);
        }

    }

}
