using UnityEngine;

[CreateAssetMenu(
    fileName = "New Stage Data", menuName = "Sonic Heroes/Loading Screen/Stage Data")]
public class StageData : ScriptableObject
{
    public enum StageType
    {
        ActionStage,
        TeamBattle,
        RobotBattle,
        MiniBoss,
        Boss,
        FinalBoss,
        SpecialStage,
        Custom
    }

    [Header("Identity")]
    [SerializeField] private string stageID;
    [SerializeField] private StageType stageType;
    [SerializeField] private string sceneName;
    [SerializeField] private string stageNumber = "STAGE 01";
    [SerializeField] private string stageName = "Seaside Hill";

    [Header("Team Sonic Mission")]
    [SerializeField]
    private TeamMissionData teamSonicMission =
        new TeamMissionData();

    [Header("Team Dark Mission")]
    [SerializeField]
    private TeamMissionData teamDarkMission = new TeamMissionData();

    [Header("Team Rose Mission")]
    [SerializeField]
    private TeamMissionData teamRoseMission = new TeamMissionData();

    [Header("Team Chaotix Mission")]
    [SerializeField]
    private TeamMissionData teamChaotixMission = new TeamMissionData();

    [Header("Loading Artwork")]
    [SerializeField] private Sprite stageArtwork;
    [SerializeField] private Sprite backgroundArtwork;

    [Header("Team Logos")]
    [SerializeField] private Sprite teamSonicLogo;
    [SerializeField] private Sprite teamDarkLogo;
    [SerializeField] private Sprite teamRoseLogo;
    [SerializeField] private Sprite teamChaotixLogo;

    [Header("Audio")]
    [SerializeField] private AudioClip loadingMusic;
    [SerializeField, Range(0f, 1f)] private float loadingMusicVolume = 1f;

    [Header("Visual Style")]
    [SerializeField] private Color accentColor = Color.white;

    [Header("Loading Tips")]
    [SerializeField, TextArea] private string[] loadingTips;

    public string StageID => stageID;
    public StageType Type => stageType;
    public string SceneName => sceneName;
    public string StageNumber => stageNumber;
    public string StageName => stageName;
    public Sprite StageArtwork => stageArtwork;
    public Sprite BackgroundArtwork => backgroundArtwork;
    public AudioClip LoadingMusic => loadingMusic;
    public float LoadingMusicVolume => loadingMusicVolume;
    public Color AccentColor => accentColor;

    public TeamMissionData GetMission(PlayableTeam team)
    {
        switch (team)
        {
            case PlayableTeam.TeamSonic:
                return teamSonicMission;

            case PlayableTeam.TeamDark:
                return teamDarkMission;

            case PlayableTeam.TeamRose:
                return teamRoseMission;

            case PlayableTeam.TeamChaotix:
                return teamChaotixMission;

            default:
                return teamSonicMission;
        }
    }

    public Sprite GetTeamLogo(PlayableTeam team)
    {
        switch (team)
        {
            case PlayableTeam.TeamSonic:
                return teamSonicLogo;

            case PlayableTeam.TeamDark:
                return teamDarkLogo;

            case PlayableTeam.TeamRose:
                return teamRoseLogo;

            case PlayableTeam.TeamChaotix:
                return teamChaotixLogo;

            default:
                return null;
        }
    }

    public string GetRandomLoadingTip()
    {
        if (loadingTips == null || loadingTips.Length == 0)
            return string.Empty;

        int index = Random.Range(0, loadingTips.Length);
        return loadingTips[index] ?? string.Empty;
    }

    private void OnValidate()
    {
        stageID = stageID?.Trim();
        sceneName = sceneName?.Trim();
        stageNumber = stageNumber?.Trim();
        stageName = stageName?.Trim();
        loadingMusicVolume = Mathf.Clamp01(loadingMusicVolume);
    }
}