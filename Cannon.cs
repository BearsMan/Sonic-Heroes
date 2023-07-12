using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cannon : MonoBehaviour
{
    GameObject source;
    public AudioClip clip;
    public bool cannon1Activated = false;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnTriggerEnter(Collider other)
    {
        if (cannon1Activated == false)
        {
            UltimatePlayerMovement player = other.GetComponent<UltimatePlayerMovement>();

            if (player && cannon1Activated == false)
            {
                cannon1Activated = true;
                source = Resources.Load<GameObject>("Audio Object");
                GameObject ao = Instantiate(source, transform.position, Quaternion.identity);
                ao.GetComponent<AudioObject>().Setup(clip, transform);
            }
        }

    }
}
