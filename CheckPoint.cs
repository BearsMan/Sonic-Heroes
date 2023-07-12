using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CheckPoint : MonoBehaviour
{
    public GameObject teamSonicLevelCheck, TeamDarkLevelCheck, TeamRoseLevelCheck, TeamChaotixLevelCheck;
    public AudioClip levelUp;
    public bool levelUpCharacter;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void CharacterLevelUp()
    {
        FindFirstObjectByType<LevelUpCore>().CompareTag("Player");

        GetComponent<LevelUpCore>().gameObject.SetActive(false);

        GameInstance.speedCoreLevelUp += 1;
        GameInstance.flyCorelevelUp += 1;
        GameInstance.powerLevelUpCore = 1;
    }
    public IEnumerator LevelUpCharacters(GameObject speedCharacter)
    {
        GetComponent<UltimatePlayerMovement>().leftFollower.SetActive(true);
        GetComponent<UltimatePlayerMovement>().rightFollower.SetActive(true);

        yield return null;
    }
}