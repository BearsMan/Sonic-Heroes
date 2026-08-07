using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestSpring : MonoBehaviour
{
    private int springForce = 6500;
    public float surrenderTime = 1.8f;
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
        if(other.TryGetComponent(out Rigidbody body))
        {
            body.linearVelocity = Vector3.zero;
            body.AddForce(transform.up * springForce);
        }

        if (other.TryGetComponent(out UltimatePlayerMovement player))
        {
            //player.SurrenderControl(surrenderTime);
        }
        
    }
}
