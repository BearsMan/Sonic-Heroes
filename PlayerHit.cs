using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHit : MonoBehaviour
{
    public AudioClip clip;
    public GameObject ringPrefab;
    GameObject source;

    private void Awake()
    {
        source = Resources.Load<GameObject>("Audio Object");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            Hit();
        }
    }

    public void Hit()
    {
        
        int rings = GameInstance.RemoveRings();
        if (rings > 0)
        {
            GetComponent<UltimatePlayerMovement>().SurrenderControl(Vector2.zero, 1.5f);
            GameObject ao = Instantiate(source, transform.position, Quaternion.identity);
            ao.GetComponent<AudioObject>().Setup(clip, transform);
            for (int i = 0; i < rings; i++)
            {
                ThrowRing(i);
            }
        }
        else if (rings == 0)
        {
            GetComponent<UltimatePlayerMovement>().enabled = false;
        }
    }

    public void ThrowRing(int i)
    {
        int force = (i > 10 ? 3 : 12);

        float x = Random.Range(-1, 1);
        float z = Random.Range(-1, 1);

        GameObject newRing = Instantiate(ringPrefab, transform.position+transform.up, Quaternion.identity);
        newRing.GetComponent<Ring>().StartPhase();
        Rigidbody ringbody = newRing.GetComponent<Rigidbody>();
        ringbody.velocity = new Vector3(x, 1, z) * force;
        ringbody.useGravity = true;
    }
}
