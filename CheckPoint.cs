using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class CheckPoint : MonoBehaviour
{
    [Header("Team Level-Up UI Panels")]
    public GameObject teamSonicLevelCheck;
    public GameObject teamDarkLevelCheck;
    public GameObject teamRoseLevelCheck;
    public GameObject teamChaotixLevelCheck;

    [Header("Audio")]
    public AudioClip levelUp;

    private AudioSource audioSource;
    private bool activated = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (activated)
        {
            return;
        }
        if (!other.CompareTag("Player"))
        {
            return;
        }

        activated = true;

        // Play Sonic Heroes level-up jingle
        if (levelUp != null)
            audioSource.PlayOneShot(levelUp);

        // Show correct team UI
        ShowTeamLevelUpUI(other.gameObject);

        // Restore HP (Sonic Heroes restores character vitality)
        Health hp = other.GetComponent<Health>();
        if (hp != null)
        {
            hp.HealthValue = 100;
        }

        // Level up Speed, Fly, Power simultaneously
        CharacterLevelManager.Instance.LevelUpAll();

        // Activate Speed followers (Sonic Heroes behavior)
        UltimatePlayerMovement move = other.GetComponent<UltimatePlayerMovement>();
        if (move != null)
            StartCoroutine(ActivateFollowers(move));
    }

    private IEnumerator ActivateFollowers(UltimatePlayerMovement move)
    {
        yield return null;
    }

    private void ShowTeamLevelUpUI(GameObject player)
    {
        // Turn off all panels
        if (teamSonicLevelCheck) teamSonicLevelCheck.SetActive(false);
        if (teamDarkLevelCheck) teamDarkLevelCheck.SetActive(false);
        if (teamRoseLevelCheck) teamRoseLevelCheck.SetActive(false);
        if (teamChaotixLevelCheck) teamChaotixLevelCheck.SetActive(false);

        // Turn on correct one
        if (player.CompareTag("TeamSonic") && teamSonicLevelCheck)
            teamSonicLevelCheck.SetActive(true);

        else if (player.CompareTag("TeamDark") && teamDarkLevelCheck)
            teamDarkLevelCheck.SetActive(true);

        else if (player.CompareTag("TeamRose") && teamRoseLevelCheck)
            teamRoseLevelCheck.SetActive(true);

        else if (player.CompareTag("TeamChaotix") && teamChaotixLevelCheck)
            teamChaotixLevelCheck.SetActive(true);
    }
}

public class CharacterLevelManager
{
    private static CharacterLevelManager instance;
    public static CharacterLevelManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new CharacterLevelManager();
            }

            return instance;
        }
    }

    private int speedLevel;
    private int flyLevel;
    private int powerLevel;

    public void LevelUpAll()
    {
        speedLevel++;
        flyLevel++;
        powerLevel++;
    }

    public int SpeedLevel => speedLevel;
    public int FlyLevel => flyLevel;
    public int PowerLevel => powerLevel;
}
