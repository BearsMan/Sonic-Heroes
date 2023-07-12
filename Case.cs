using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Case : MonoBehaviour
{
    public GameObject[] spawnable = new GameObject[0];
    public AudioClip findSwitchSound;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void TryToFindNeededSwitch()
    {
        FindFirstObjectByType<Switch>();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            GetComponent<AudioSource>().Play();
        }
        
            
        
    }
}
