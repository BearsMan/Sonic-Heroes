using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CheckPoint : MonoBehaviour
{
    // Level-up UI panels shown per team when a checkpoint is hit
    public GameObject teamSonicLevelCheck;
    public GameObject teamDarkLevelCheck;
    public GameObject teamRoseLevelCheck;
    public GameObject teamChaotixLevelCheck;

    public AudioClip levelUp;
    public bool levelUpCharacter;

    private AudioSource audioSource;
    private bool activated = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {

    }

    // Called when the player's collider enters the checkpoint trigger
    private void OnTriggerEnter(Collider other)
    {
        if (activated) return;

        if (other.CompareTag("Player"))
        {
            activated = true;
            levelUpCharacter = true;

            // Play the level-up jingle
            if (levelUp != null)
                audioSource.PlayOneShot(levelUp);

            // Show the correct team's level-up UI based on active team tag
            ShowTeamLevelUpUI(other.gameObject);

            // Increment all three character type level counters
            CharacterLevelUp();

            // Trigger the follower-reveal coroutine for the speed character
            UltimatePlayerMovement playerMovement = other.GetComponent<UltimatePlayerMovement>();
            if (playerMovement != null)
                StartCoroutine(LevelUpCharacters(other.gameObject));
        }
    }

    // Increments the level-up stats for all three formation roles (Speed, Fly, Power)
    public void CharacterLevelUp()
    {
        // Find the LevelUpCore in the scene and deactivate it (the core pickup object)
        LevelUpCore core = FindFirstObjectByType<LevelUpCore>();
        if (core != null && core.CompareTag("Player"))
        {
            core.gameObject.SetActive(false);
        }

        GameInstance.speedCoreLevelUp += 1;
        GameInstance.flyCoreLevelUp += 1;
        GameInstance.powerLevelUpCore += 1;  // Was wrongly assigned (= 1) instead of incrementing
    }

    // Activates the left/right followers on the speed character after a level-up
    public IEnumerator LevelUpCharacters(GameObject speedCharacter)
    {
        if (speedCharacter == null) yield break;

        UltimatePlayerMovement playerMovement = speedCharacter.GetComponent<UltimatePlayerMovement>();
        if (playerMovement != null)
        {
            if (playerMovement.leftFollower != null)
                playerMovement.leftFollower.SetActive(true);

            if (playerMovement.rightFollower != null)
                playerMovement.rightFollower.SetActive(true);
        }

        yield return null;
    }

    // Shows the appropriate team's level-up indicator UI
    private void ShowTeamLevelUpUI(GameObject player)
    {
        // Deactivate all panels first
        if (teamSonicLevelCheck != null) teamSonicLevelCheck.SetActive(false);
        if (teamDarkLevelCheck != null) teamDarkLevelCheck.SetActive(false);
        if (teamRoseLevelCheck != null) teamRoseLevelCheck.SetActive(false);
        if (teamChaotixLevelCheck != null) teamChaotixLevelCheck.SetActive(false);

        // Activate only the panel matching the current team
        if (player.CompareTag("TeamSonic") && teamSonicLevelCheck != null)
            teamSonicLevelCheck.SetActive(true);
        else if (player.CompareTag("TeamDark") && teamDarkLevelCheck != null)
            teamDarkLevelCheck.SetActive(true);
        else if (player.CompareTag("TeamRose") && teamRoseLevelCheck != null)
            teamRoseLevelCheck.SetActive(true);
        else if (player.CompareTag("TeamChaotix") && teamChaotixLevelCheck != null)
            teamChaotixLevelCheck.SetActive(true);
    }
}