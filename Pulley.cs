using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pulley : MonoBehaviour
{
    public GameObject pullTarget;
    public bool timePull;
    public float timeOfPull = 1f;
    // Start is called before the first frame update
    void Start()
    {
        Rigidbody body = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    public void PullCharacterUp(GameObject speedCharacter, GameObject flyCharacter, GameObject powerCharacter)
    {
        transform.position = transform.transform.position + speedCharacter.transform.forward * Time.deltaTime;
        transform.position = transform.transform.position + flyCharacter.transform.forward * Time.deltaTime;
        transform.position = transform.transform.position + powerCharacter.transform.forward * Time.deltaTime;
        transform.transform.LookAt(transform.position);

    }
    public void SpeedPullFoward()
    {
        
    }
    public IEnumerator PullPushFoward(GameObject speedCharacter)
    {
        GetComponentInChildren<UltimatePlayerMovement>().leftFollower.SetActive(false);
        GetComponentInChildren<UltimatePlayerMovement>().rightFollower.SetActive(false);

        yield return null;

    }
}
