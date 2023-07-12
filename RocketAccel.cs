using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RocketAccel : MonoBehaviour
{
    public GameObject speedCharacterAcceleration, flyCharacterAcceleration, powerCharacterAcceleration;
    public AudioClip rocketAccelSound;
    public bool speedRollAcceleration;
    public float teamAcceleration = 20.0f;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.B))
        {

        }
    }
    public void SpinTeamAcceleration()
    {
        GetComponent<AudioSource>().Play();
    }
    

    public IEnumerator SpeedRocketAcceleration(GameObject speedCharacter)
    {
        speedCharacter.GetComponent<UltimatePlayerMovement>().leftFollower.SetActive(false);
        speedCharacter.GetComponent<UltimatePlayerMovement>().rightFollower.SetActive(false);
        


        yield return null;
    }
}
