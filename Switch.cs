using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Switch : MonoBehaviour
{
    public GameObject switches;
    public GameObject pullSwitches;
    public GameObject switchHandle;
    public GameObject door;
    public bool activateSwitch;
    public float pullSwitchOpenTime = 2f;
    public float pullDoorOpenTime = 2f;
    // Start is called before the first frame update
    void Start()
    {

    }
    // Update is called once per frame
    void Update()
    {

    }
    public void PullSwitch()
    {
        PullSwitch();
        door.GetComponent<BoxCollider2D>().enabled = false;
    }
    public void OpenDoor()
    {
        BeepSwitch();
        door.GetComponent<BoxCollider2D>().enabled = false;
    }
    public void BeepSwitch()
    {
        GetComponent<AudioSource>().Play();
    }
    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            OpenDoor();
        }
    }
    
    
}
     