using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemBalloon : MonoBehaviour
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
        AudioSource audio = GetComponent<AudioSource>();
        if (audio != null) audio.Play();
    }
    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && levelingup == false)
        {
            levelingup = true;
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
            GameInstance.speedLevelUp += 1;
            GameInstance.flyLevelUp += 1;
            GameInstance.powerLevelUp += 1;
            if (levelUpPrefab != null && levelUpParent != null)
                Instantiate(levelUpPrefab, levelUpParent.transform);
            StartCoroutine(PlayLevelUpSFX());
            var hud = Object.FindAnyObjectByType<HUD>();
            if (hud != null) hud.AddPower(5);
        }
    }

    public IEnumerator PlayLevelUpSFX()
    {
        AudioSource audio = GetComponent<AudioSource>();
        if (audio == null)
        {
            Destroy(gameObject);
            yield break;
        }

        audio.Play();
        while (audio.isPlaying)
        {
            yield return null;
        }

        if (characterSFX != null &&
            GameInstance.currentTeam >= 0 &&
            GameInstance.currentTeam < characterSFX.Count &&
            characterSFX[GameInstance.currentTeam] != null)
        {
            audio.clip = characterSFX[GameInstance.currentTeam];
            audio.Play();

            while (audio.isPlaying)
            {
                yield return null;
            }
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
