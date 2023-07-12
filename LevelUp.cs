using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelUp : MonoBehaviour
{
    public GameObject levelUpHUD;
    public GameObject levelUpParent;
    public GameObject levelUpPrefab;
    public List<AudioClip> characterSFX = new List<AudioClip>();
    public bool levelingup;
    public float speedCoreLevelUp;
    public float powerCoreLevelUp;
    public float flyCoreLevelUp;
    public bool isSpeed, isFlying, isPower;
    public Sprite bluePowerCore, redPowerCore, yellowPowerCore;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void LevelUpSound()
    {
        GetComponent<AudioSource>().Play();
    }
    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && levelingup == false)
        {
            levelingup = true;
            GetComponent<SpriteRenderer>().enabled = false;
            GameInstance.speedLevelUp += 1;
            GameInstance.flyLevelUp += 1;
            GameInstance.powerLevelUp += 1;
            Instantiate(levelUpPrefab, levelUpParent.transform);
            StartCoroutine(PlayLevelUpSFX());
            FindFirstObjectByType<HUD>().AddPower(5);
        }
    }
    
    public IEnumerator PlayLevelUpSFX()
    {
        GetComponent<AudioSource>().Play();
        while (GetComponent<AudioSource>().isPlaying == true)
        {
            yield return new WaitForSeconds(Time.deltaTime);
        }
        GetComponent<AudioSource>().clip = characterSFX[GameInstance.currentTeam];
        GetComponent<AudioSource>().Play();

        while (GetComponent<AudioSource>().isPlaying == true)
        {
            yield return new WaitForSeconds(Time.deltaTime);
        }
        Destroy(gameObject);
    }
    public IEnumerator LevelUpCharacter(GameObject speedCharacter)
    {
        speedCharacter.GetComponent<UltimatePlayerMovement>().leftFollower.SetActive(true);
        speedCharacter.GetComponent<UltimatePlayerMovement>().rightFollower.SetActive(true);
        

        yield return null;
    }

    //This is the team selections setup from LevelUpCore.cs for Character Level Up Sound Effects to be played in correct team order
    //Team Sonic = 0
    //Team Dark = 1
    //Team Rose = 2
    //Team Chaotix = 3
    
   
}
