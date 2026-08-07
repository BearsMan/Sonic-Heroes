using UnityEngine;

public class LoadingScreenManager : MonoBehaviour
{
    [Header("Stage Selection")]
    [SerializeField] private StageDatabase stageDatabase;
    [SerializeField] private StageData selectedStage;
    [SerializeField] private PlayableTeam selectedTeam;

    [Header("Scene Override")]
    [Tooltip("Leave empty to use the scene name stored in Stage Data.")]
    [SerializeField] private string sceneNameOverride;

    [Header("Mission Override")]
    [SerializeField] private bool overrideMission;
    [SerializeField] private string missionLabelOverride = "MISSION";
    [SerializeField, TextArea] private string missionTextOverride;

    public StageData SelectedStage => selectedStage;
    public PlayableTeam SelectedTeam => selectedTeam;

    public void Load()
    {
        LoadStage(selectedStage, selectedTeam);
    }

    public void LoadStage(StageData stageData)
    {
        LoadStage(stageData, selectedTeam);
    }

    public void LoadStage(StageData stageData, PlayableTeam team)
    {
        LoadingScreenHUD loadingScreenHUD = LoadingScreenHUD.Instance;

        if (loadingScreenHUD == null)
        {
            Debug.LogError(
                "LoadingScreenHUD instance was not found.",
                this);

            return;
        }

        if (loadingScreenHUD.IsLoading)
        {
            Debug.LogWarning(
                "A scene is already loading.",
                this);

            return;
        }

        if (stageData == null)
        {
            Debug.LogWarning(
                "No Stage Data has been assigned.",
                this);

            return;
        }

        string targetSceneName =
            string.IsNullOrWhiteSpace(sceneNameOverride)
                ? stageData.SceneName
                : sceneNameOverride.Trim();

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning(
                $"Stage Data '{stageData.name}' has no scene name.",
                stageData);

            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogError(
                $"Scene '{targetSceneName}' is not available in the build.",
                this);

            return;
        }

        TeamMissionData mission = stageData.GetMission(team);

        string missionLabel = GetMissionLabel(mission);
        string missionText = GetMissionText(mission);

        selectedStage = stageData;
        selectedTeam = team;

        loadingScreenHUD.SetStageInfo(
            stageData.StageNumber,
            stageData.StageName,
            missionLabel,
            missionText);

        loadingScreenHUD.SetLoadingAssets(
            stageData.StageArtwork,
            stageData.BackgroundArtwork,
            stageData.GetTeamLogo(team),
            stageData.LoadingMusic,
            stageData.LoadingMusicVolume,
            stageData.AccentColor,
            stageData.GetRandomLoadingTip());

        loadingScreenHUD.LoadScene(targetSceneName);
    }

    public void LoadStageByID(string stageID)
    {
        if (!TryGetDatabase(out StageDatabase database))
            return;

        StageData stageData = database.GetStage(stageID);

        if (stageData == null)
        {
            Debug.LogWarning(
                $"Stage ID '{stageID}' was not found.",
                this);

            return;
        }

        LoadStage(stageData, selectedTeam);
    }

    public void LoadStageByIndex(int stageIndex)
    {
        if (!TryGetDatabase(out StageDatabase database))
            return;

        StageData stageData = database.GetStage(stageIndex);

        if (stageData == null)
        {
            Debug.LogWarning(
                $"Stage index {stageIndex} is not valid.",
                this);

            return;
        }

        LoadStage(stageData, selectedTeam);
    }

    public void SetStage(StageData stageData)
    {
        if (stageData == null)
        {
            Debug.LogWarning(
                "Cannot assign a null Stage Data asset.",
                this);

            return;
        }

        selectedStage = stageData;
    }

    public void SetTeam(PlayableTeam team)
    {
        selectedTeam = team;
    }

    public void SetTeamByIndex(int teamIndex)
    {
        if (!System.Enum.IsDefined(
            typeof(PlayableTeam),
            teamIndex))
        {
            Debug.LogWarning(
                $"Team index {teamIndex} is not valid.",
                this);

            return;
        }

        selectedTeam = (PlayableTeam)teamIndex;
    }

    public void SetTeamSonic()
    {
        SetTeam(PlayableTeam.TeamSonic);
    }

    public void SetTeamDark()
    {
        SetTeam(PlayableTeam.TeamDark);
    }

    public void SetTeamRose()
    {
        SetTeam(PlayableTeam.TeamRose);
    }

    public void SetTeamChaotix()
    {
        SetTeam(PlayableTeam.TeamChaotix);
    }

    private string GetMissionLabel(TeamMissionData mission)
    {
        if (overrideMission &&
            !string.IsNullOrWhiteSpace(missionLabelOverride))
        {
            return missionLabelOverride;
        }

        if (mission == null)
            return "MISSION";

        return string.IsNullOrWhiteSpace(mission.MissionLabel)
            ? "MISSION"
            : mission.MissionLabel;
    }

    private string GetMissionText(TeamMissionData mission)
    {
        if (overrideMission &&
            !string.IsNullOrWhiteSpace(missionTextOverride))
        {
            return missionTextOverride;
        }

        if (mission == null)
            return string.Empty;

        return mission.MissionText ?? string.Empty;
    }

    private bool TryGetDatabase(out StageDatabase database)
    {
        database = stageDatabase;

        if (database != null)
            return true;

        Debug.LogError(
            "Stage Database is not assigned.",
            this);

        return false;
    }

    private void OnValidate()
    {
        sceneNameOverride = sceneNameOverride?.Trim();
        missionLabelOverride = missionLabelOverride?.Trim();
    }
}