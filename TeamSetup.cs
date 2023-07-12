using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TeamSetup : MonoBehaviour
{
    public TeamComposition CurrentTeam;
    public GameObject pos1;
    public GameObject pos2;
    public Transform leftTeamMember;
    public Transform rightTeamMember;
    public Transform player;
    public GameObject HUD;
    private Coroutine superFormRingCountdown;



    public static TeamSetup pc;

    // Start is called before the first frame update
    void Start()
    {
        pc = this;
        //Setup Player
        Transform body = Instantiate(CurrentTeam.SpeedCharacter, player).transform;
        GetComponent<CharacterSwitch>().speedCharacter = body;
        body.localPosition = Vector3.zero;
        body.rotation = Quaternion.Euler(0,180,0);
        CharacterSwitch switcher = GetComponent<CharacterSwitch>();
        player.GetComponent<UltimatePlayerMovement>().SetupAnimation();

        switcher.TeamMembers.Add(body.gameObject);

        //Setup Left Team Member
        GameObject ai = Instantiate(CurrentTeam.FlyingCharacter, leftTeamMember);
        GetComponent<CharacterSwitch>().flyingCharacter = ai.transform;
        ai.transform.localPosition = Vector3.zero;
        ai.transform.localRotation = Quaternion.Euler(0, 180, 0); 
        switcher.TeamMembers.Add(ai);
        leftTeamMember.GetComponent<FollowerNavigation>().Setup();

        //Setup Right Team Member
        ai = Instantiate(CurrentTeam.PowerCharacter, rightTeamMember);
        GetComponent<CharacterSwitch>().powerCharacter = ai.transform;
        ai.transform.localPosition = Vector3.zero;
        ai.transform.localRotation = Quaternion.Euler(0, 180, 0);
        switcher.TeamMembers.Add(ai);
        rightTeamMember.GetComponent<FollowerNavigation>().Setup();


        player.parent = null;
        leftTeamMember.parent = null;
        rightTeamMember.parent = null;

        HUD = FindFirstObjectByType<HUD>().gameObject;
        HUD.GetComponent<HUD>().Setup(CurrentTeam);
        if (CurrentTeam.name == "Team Sonic")
        {
            GameInstance.currentTeam = 0;
        }
        if (CurrentTeam.name == "Team Dark")
        {
            GameInstance.currentTeam = 1;
        }
        if (CurrentTeam.name == "Team Rose")
        {
            GameInstance.currentTeam = 2;
        }
        if (CurrentTeam.name == "Team Chaotix")
        {
            GameInstance.currentTeam = 3;
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
    public void SwapForSuper()
    {
        CharacterSwitch switcher = GetComponent<CharacterSwitch>();
        Transform SpeedCharacter = GetComponent<CharacterSwitch>().speedCharacter;
        switcher.TeamMembers.Remove(SpeedCharacter.gameObject);
        Destroy(SpeedCharacter.gameObject);

        Transform body = Instantiate(CurrentTeam.SuperCharacter, player).transform;
        GetComponent<CharacterSwitch>().speedCharacter = body;
        body.localPosition = Vector3.zero;
        body.localRotation = Quaternion.Euler(0, 180, 0);

        switcher.TeamMembers.Add(body.gameObject);
        superFormRingCountdown = StartCoroutine(SuperCountDown());
        
    }
    public IEnumerator SuperCountDown()
    {
        while(GameInstance.currentRings > 0)
        {
            HUD.GetComponent<HUD>().UpdateRings();
            GameInstance.currentRings -= 1;
            yield return new WaitForSeconds(3);
        }

        superFormRingCountdown = null;
        SwapForSonic();
    }
    public void SwapForSonic()
    {
        if (superFormRingCountdown != null)
        {
            StopCoroutine(superFormRingCountdown);
            superFormRingCountdown = null;
        }
        HUD.GetComponent<HUD>().UpdateRings();
        CharacterSwitch switcher = GetComponent<CharacterSwitch>();
        Transform SpeedCharacter = GetComponent<CharacterSwitch>().speedCharacter;
        switcher.TeamMembers.Remove(SpeedCharacter.gameObject);
        Destroy(SpeedCharacter.gameObject);

        Transform body = Instantiate(CurrentTeam.SpeedCharacter, player).transform;
        GetComponent<CharacterSwitch>().speedCharacter = body;
        body.localPosition = Vector3.zero;
        body.localRotation = Quaternion.Euler(0, 180, 0);

        switcher.TeamMembers.Add(body.gameObject);
    }
}
