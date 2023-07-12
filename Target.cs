using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Target : MonoBehaviour
{
    public GameObject targetSwitch;
    public bool teamThrow;
    public bool switchActive;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void FlyingThrowBall(GameObject flyCharacter)
    {

    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Follower"))
        {
            switchActive = true;
        }
    }
    
}
