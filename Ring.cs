using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ring : MonoBehaviour
{
    GameObject source;
    public AudioClip clip;
    public Collider physicalCollider;
    private bool phased;
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
        if (phased)
        {
            return;
        }

        UltimatePlayerMovement player = other.GetComponent<UltimatePlayerMovement>();
        FollowerNavigation ai = other.GetComponent<FollowerNavigation>();
        if (player || ai)
        {
            GameObject ao = Instantiate(source, transform.position, Quaternion.identity);
            ao.GetComponent<AudioObject>().Setup(clip, transform);

            GameInstance.AddRings(other.GetComponentInChildren<CharacterType>().type);
            FindFirstObjectByType<HUD>().AddPower(1);
            Destroy(gameObject);
        }

    }

    private void EndPhase()
    {
        phased = false;
    }

    public void StartPhase()
    {
        physicalCollider.isTrigger = true;
        phased = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out UltimatePlayerMovement player) && phased)
        {
            physicalCollider.isTrigger = false;
            Invoke("EndPhase", 1);
        }
    }
}