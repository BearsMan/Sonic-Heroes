using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Crate : MonoBehaviour
{
    public GameObject[] spawnable = new GameObject[0];
    public AudioClip breakSound;
    public void DestroyCrate()
    {
        int num = Random.Range(0, spawnable.Length);
        Instantiate(spawnable[num], transform.position, transform.rotation);

        GameObject source = Resources.Load<GameObject>("Audio Object");
        source = Instantiate(source, transform.position, transform.rotation);
        source.GetComponent<AudioObject>().Setup(breakSound, transform);
        Destroy(gameObject);//Replace with broken crate model
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.GetComponent<UltimatePlayerMovement>())
        {
            DestroyCrate();
        }
    }
}
