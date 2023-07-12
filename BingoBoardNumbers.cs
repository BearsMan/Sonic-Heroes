using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BingoBoardNumbers : MonoBehaviour
{
    public GameObject boardNumberMesh;
    public GameObject boardNumberCollidable;
    public int boardNumber;
    public bool triggered;
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
        if (other.CompareTag("Player"))
        {
            triggered = true;
        }
    }
}
