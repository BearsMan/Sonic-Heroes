using System.Collections;
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

    public static TeamSetup instance => pc;

    public PlayableTeam PlayableTeam
    {
        get
        {
            if (CurrentTeam == null)
                return default;

            return CurrentTeam.name switch
            {
                "Team Sonic" => PlayableTeam.TeamSonic,
                "Team Dark" => PlayableTeam.TeamDark,
                "Team Rose" => PlayableTeam.TeamRose,
                "Team Chaotix" => PlayableTeam.TeamChaotix,
                _ => default
            };
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        pc = this;
        //Setup Player
        Transform body = Instantiate(CurrentTeam.SpeedCharacter, player).transform;
        var cs = GetComponent<CharacterSwitch>();
        if (cs != null) cs.speedCharacter = body;
        body.localPosition = Vector3.zero;
        body.rotation = Quaternion.Euler(0, 180, 0);
        CharacterSwitch switcher = GetComponent<CharacterSwitch>();
        if (player != null && player.TryGetComponent<UltimatePlayerMovement>(out var up)) up.SetupAnimation();

        switcher.TeamMembers.Add(body.gameObject);

        //Setup Left Team Member
        GameObject ai = Instantiate(CurrentTeam.FlyingCharacter, leftTeamMember);
        if (GetComponent<CharacterSwitch>() is CharacterSwitch cs2)
            cs2.flyingCharacter = ai.transform;
        ai.transform.localPosition = Vector3.zero;
        ai.transform.localRotation = Quaternion.Euler(0, 180, 0);
        switcher.TeamMembers.Add(ai);
        if (leftTeamMember != null && leftTeamMember.TryGetComponent<FollowerNavigation>(out var lnav)) lnav.Setup();

        //Setup Right Team Member
        ai = Instantiate(CurrentTeam.PowerCharacter, rightTeamMember);
        if (GetComponent<CharacterSwitch>() is CharacterSwitch cs3)
            cs3.powerCharacter = ai.transform;
        ai.transform.localPosition = Vector3.zero;
        ai.transform.localRotation = Quaternion.Euler(0, 180, 0);
        switcher.TeamMembers.Add(ai);
        if (rightTeamMember != null && rightTeamMember.TryGetComponent<FollowerNavigation>(out var rnav)) rnav.Setup();


        player.parent = null;
        leftTeamMember.parent = null;
        rightTeamMember.parent = null;

        var hudObj = Object.FindAnyObjectByType<HUD>();
        if (hudObj != null)
            HUD = hudObj.gameObject;
        if (HUD != null && HUD.TryGetComponent<HUD>(out var hudComp)) hudComp.Setup(CurrentTeam);
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
        while (GameInstance.currentRings > 0)
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
