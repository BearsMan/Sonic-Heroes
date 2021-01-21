using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ring : MonoBehaviour
{
    GameObject source;
    public AudioClip clip;
    // Start is called before the first frame update
    void Start()
    {
        source = Resources.Load<GameObject>("Audio Object");
    }



    private void OnTriggerEnter(Collider other)
    {


        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player)
        {
            GameObject ao = Instantiate(source, transform.position, Quaternion.identity);
            ao.GetComponent<AudioObject>().Setup(clip, transform);

            Destroy(gameObject);
        }

    }
}