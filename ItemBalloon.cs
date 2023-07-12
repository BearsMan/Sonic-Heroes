using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemBalloon : MonoBehaviour
{
    
    public GameObject[] pickables;
    public bool popped = false;
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
        if (other.gameObject.CompareTag("Player") && popped == false)
        {
            int pick = Random.Range(0, pickables.Length);
            Instantiate(pickables[pick], other.transform.position,Quaternion.identity);
            GetComponent<AudioSource>().Play();
            FindFirstObjectByType<HUD>().AddPower(1);
            popped = true;
            Destroy(gameObject);
        }
    }
}
