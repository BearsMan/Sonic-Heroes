using System.Collections;
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
        AudioSource audio = GetComponent<AudioSource>();
        if (audio != null) audio.Play();
    }


    public IEnumerator SpeedRocketAcceleration(GameObject speedCharacter)
    {
        if (speedCharacter != null && speedCharacter.TryGetComponent<UltimatePlayerMovement>(out var spUp))
        {
            
        }



        yield return null;
    }
}
