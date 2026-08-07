using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestSpeeder : MonoBehaviour
{
    public float force = 2000;
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
        if (other.TryGetComponent(out Rigidbody body))
        {
            body.AddForce(transform.forward * force);
        }
    }
}
