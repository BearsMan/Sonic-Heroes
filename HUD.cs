using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HUD : MonoBehaviour
{
    #region Inspector

    [Header("Main HUD Text")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text ringText;
    [SerializeField] private TMP_Text livesText;

    [Header("Team Display")]
    [SerializeField] private List<Image> icons = new();
    [SerializeField] private List<Image> faces = new();
    [SerializeField] private Image livesImage;
    [SerializeField] private TeamComposition currentTeam;

    [Header("Character Level Lights")]
    [SerializeField] private List<Image> speedLevelLights = new();
    [SerializeField] private List<Image> flyLevelLights = new();
    [SerializeField] private List<Image> powerLevelLights = new();

    [Header("Team Blast")]
    [SerializeField] private TeamBlast teamBlast;
    [SerializeField] private Slider teamBlastGauge;
    [SerializeField] private GameObject teamBlastPrompt;
    [SerializeField] private Image teamBlastFill;
    [SerializeField] private Color chargingColor = Color.blue;
    [SerializeField] private Color readyColor = Color.yellow;
    [SerializeField] private TeamBlastVideos teamBlastVideos;

    [Header("Item Pickup")]
    [SerializeField] private Image itemPickup;
    [SerializeField, Min(0f)] private float pickupDisplayDuration = 3f;

    [Header("Dependencies")]
    [SerializeField] private StageSession stageSession;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private readonly List<Character> teamSprites =
        new();

    private Coroutine pickupRoutine;

    private int displayedScore =
        int.MinValue;

    private int displayedRings =
        int.MinValue;

    private int displayedLives =
        int.MinValue;

    private int displayedTime =
        int.MinValue;

    private float displayedTeamBlastGauge =
        float.MinValue;

    private CHARACTERTYPES currentLeaderType =
        CHARACTERTYPES.Speed;

    private bool isInitialized;
    private bool eventsSubscribed;
    private bool isShuttingDown;

    #endregion

    #region Public API

    public bool IsInitialized =>
        isInitialized;

    public int PowerUpLevel =>
        teamBlast != null
            ? Mathf.RoundToInt(
                teamBlast.CurrentGauge)
            : 0;

    public int MaximumPower =>
        teamBlast != null
            ? Mathf.RoundToInt(
                teamBlast.MaxGauge)
            : 100;

    public bool TeamBlastReady =>
        teamBlast != null &&
        teamBlast.BlastReady;

    public bool Setup(
        TeamComposition team)
    {
        if (team == null)
        {
            Debug.LogError(
                "HUD Setup received no TeamComposition.",
                this);

            return false;
        }

        currentTeam =
            team;

        CacheComponents();
        ResolveReferences();
        ConfigureComponents();

        if (!BuildTeamDisplayData())
        {
            Debug.LogError(
                "HUD could not build the team display.",
                this);

            isInitialized = false;
            return false;
        }

        isInitialized = true;

        SetCharacter(
            currentLeaderType);

        RefreshAll();

        LogStateChange(
            $"HUD configured for {currentTeam.PlayableTeam}.");

        return true;
    }

    public void UpdateHUD()
    {
        RefreshAll();
    }

    public void RefreshAll()
    {
        RefreshTimer(
            force: true);

        RefreshScore(
            force: true);

        RefreshRings(
            force: true);

        RefreshLives(
            force: true);

        RefreshCharacterLevels();

        RefreshTeamBlastMeter(
            force: true);

        RefreshTeamDisplay();
    }

    public void UpdateRings()
    {
        RefreshRings(
            force: false);
    }

    public void SetCharacter(
        CHARACTERTYPES characterType)
    {
        currentLeaderType =
            characterType;

        if (currentTeam == null)
        {
            Debug.LogWarning(
                "HUD cannot change character display because no team is configured.",
                this);

            return;
        }

        if (teamSprites.Count != 3 &&
            !BuildTeamDisplayData())
        {
            return;
        }

        int leaderIndex =
            characterType switch
            {
                CHARACTERTYPES.Speed => 0,
                CHARACTERTYPES.Fly => 1,
                CHARACTERTYPES.Power => 2,
                _ => -1
            };

        if (leaderIndex < 0 ||
            leaderIndex >= teamSprites.Count)
        {
            Debug.LogWarning(
                $"HUD received unsupported character type: {characterType}.",
                this);

            return;
        }

        ArrangeTeamWithLeader(
            teamSprites[leaderIndex]);

        RefreshTeamDisplay();
    }

    public void SetCharacter(
        int direction)
    {
        if (teamSprites.Count != 3 &&
            !BuildTeamDisplayData())
        {
            return;
        }

        if (direction > 0)
        {
            Character lastCharacter =
                teamSprites[^1];

            teamSprites.RemoveAt(
                teamSprites.Count - 1);

            teamSprites.Insert(
                0,
                lastCharacter);
        }
        else if (direction < 0)
        {
            Character firstCharacter =
                teamSprites[0];

            teamSprites.RemoveAt(
                0);

            teamSprites.Add(
                firstCharacter);
        }

        RefreshTeamDisplay();
    }

    public void UpdateCharacterLevels()
    {
        RefreshCharacterLevels();
    }

    public void AddPower(
        int value)
    {
        if (value <= 0)
            return;

        ResolveTeamBlast();

        if (teamBlast == null)
        {
            Debug.LogWarning(
                "HUD could not add Team Blast gauge because TeamBlast is missing.",
                this);

            return;
        }

        teamBlast.AddGauge(
            value);

        RefreshTeamBlastMeter(
            force: true);
    }

    public void UpdateTeamBlastMeter()
    {
        RefreshTeamBlastMeter(
            force: false);
    }

    public void ActivateTeamBlast()
    {
        ResolveTeamBlast();

        if (teamBlast == null)
        {
            Debug.LogWarning(
                "HUD could not activate Team Blast because TeamBlast is missing.",
                this);

            return;
        }

        if (!teamBlast.TryActivateTeamBlast())
            return;

        teamBlastVideos?.PlayTeamBlast();

        RefreshTeamBlastMeter(
            force: true);
    }

    public void ResetPower()
    {
        ResolveTeamBlast();

        if (teamBlast == null)
            return;

        teamBlast.ResetGauge();

        RefreshTeamBlastMeter(
            force: true);
    }

    public void ShowPickUp(
        Sprite item)
    {
        if (itemPickup == null ||
            item == null)
        {
            return;
        }

        if (pickupRoutine != null)
        {
            StopCoroutine(
                pickupRoutine);
        }

        itemPickup.sprite =
            item;

        pickupRoutine =
            StartCoroutine(
                ShowItemRoutine());
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheComponents();
        ResolveReferences();
        ConfigureComponents();

        if (currentTeam != null)
        {
            BuildTeamDisplayData();
        }
    }

    private void Start()
    {
        InitializeHud();
    }

    private void OnEnable()
    {
        if (isShuttingDown)
            return;

        CacheComponents();
        ResolveReferences();
        ConfigureComponents();
        SubscribeToEvents();

        if (isInitialized)
        {
            RefreshAll();
        }
    }

    private void Update()
    {
        if (!isInitialized)
            return;

        RefreshTimer(
            force: false);

        RefreshScore(
            force: false);

        RefreshRings(
            force: false);

        RefreshLives(
            force: false);

        RefreshTeamBlastMeter(
            force: false);
    }

    private void OnDisable()
    {
        CleanupRuntimeState();
    }

    private void OnDestroy()
    {
        isShuttingDown = true;
        CleanupDestroyedState();
    }

    private void OnValidate()
    {
        pickupDisplayDuration =
            Mathf.Max(
                0f,
                pickupDisplayDuration);

        ConfigureTeamBlastGauge();
    }

    #endregion

    #region Initialization

    private bool InitializeHud()
    {
        if (isInitialized)
            return true;

        CacheComponents();
        ResolveReferences();
        ConfigureComponents();

        if (!ValidateConfiguration())
        {
            Debug.LogError(
                $"HUD failed to initialize on '{name}'.",
                this);

            isInitialized = false;
            return false;
        }

        if (currentTeam != null)
        {
            BuildTeamDisplayData();
        }

        isInitialized = true;

        SubscribeToEvents();
        RefreshAll();

        LogStateChange(
            "HUD initialized.");

        return true;
    }

    private void CacheComponents()
    {
        if (teamBlastGauge == null)
        {
            teamBlastGauge =
                GetComponentInChildren<Slider>(
                    includeInactive: true);
        }
    }

    private void ResolveReferences()
    {
        ResolveStageSession();
        ResolveTeamBlast();
        ResolveTeamBlastVideos();

        if (currentTeam == null &&
            TeamSetup.Instance != null)
        {
            currentTeam =
                TeamSetup.Instance.Team;
        }
    }

    private void ResolveStageSession()
    {
        stageSession ??=
            StageSession.Instance;

        stageSession ??=
            FindAnyObjectByType<StageSession>(
                FindObjectsInactive.Include);
    }

    private void ResolveTeamBlast()
    {
        teamBlast ??=
            GetComponent<TeamBlast>();

        teamBlast ??=
            GetComponentInParent<TeamBlast>();

        if (teamBlast == null &&
            TeamSetup.Instance != null)
        {
            teamBlast =
                TeamSetup.Instance
                    .GetComponentInChildren<TeamBlast>(
                        includeInactive: true);
        }

        teamBlast ??=
            FindAnyObjectByType<TeamBlast>(
                FindObjectsInactive.Include);
    }

    private void ResolveTeamBlastVideos()
    {
        teamBlastVideos ??=
            GetComponent<TeamBlastVideos>();

        teamBlastVideos ??=
            GetComponentInParent<TeamBlastVideos>();

        teamBlastVideos ??=
            FindAnyObjectByType<TeamBlastVideos>(
                FindObjectsInactive.Include);
    }

    private void ConfigureComponents()
    {
        if (itemPickup != null &&
            !Application.isPlaying)
        {
            itemPickup.enabled =
                false;
        }

        ConfigureTeamBlastGauge();
    }

    private void ConfigureTeamBlastGauge()
    {
        if (teamBlastGauge == null)
            return;

        float maximumGauge =
            teamBlast != null
                ? teamBlast.MaxGauge
                : 100f;

        teamBlastGauge.minValue =
            0f;

        teamBlastGauge.maxValue =
            Mathf.Max(
                1f,
                maximumGauge);

        teamBlastGauge.wholeNumbers =
            false;

        teamBlastGauge.value =
            teamBlast != null
                ? teamBlast.CurrentGauge
                : 0f;
    }

    #endregion

    #region Events

    private void SubscribeToEvents()
    {
        if (eventsSubscribed)
            return;

        GameInstance.UpdateData +=
            UpdateRings;

        eventsSubscribed =
            true;
    }

    private void UnsubscribeFromEvents()
    {
        if (!eventsSubscribed)
            return;

        GameInstance.UpdateData -=
            UpdateRings;

        eventsSubscribed =
            false;
    }

    #endregion

    #region Team Display

    private bool BuildTeamDisplayData()
    {
        teamSprites.Clear();

        if (currentTeam == null)
            return false;

        AddCharacterData(
            currentTeam.SpeedCharacterData);

        AddCharacterData(
            currentTeam.FlyingCharacterData);

        AddCharacterData(
            currentTeam.PowerCharacterData);

        return
            teamSprites.Count == 3;
    }

    private void AddCharacterData(
        Character character)
    {
        if (character == null)
        {
            Debug.LogWarning(
                "HUD received missing character display data.",
                this);

            return;
        }

        if (!teamSprites.Contains(
                character))
        {
            teamSprites.Add(
                character);
        }
    }

    private void ArrangeTeamWithLeader(
        Character leader)
    {
        if (leader == null ||
            teamSprites.Count == 0)
        {
            return;
        }

        int leaderIndex =
            teamSprites.IndexOf(
                leader);

        if (leaderIndex < 0)
            return;

        for (int index = 0;
             index < leaderIndex;
             index++)
        {
            Character firstCharacter =
                teamSprites[0];

            teamSprites.RemoveAt(
                0);

            teamSprites.Add(
                firstCharacter);
        }
    }

    private void RefreshTeamDisplay()
    {
        if (teamSprites.Count == 0)
            return;

        Character leader =
            teamSprites[0];

        if (livesImage != null &&
            leader != null)
        {
            livesImage.sprite =
                leader.face;
        }

        int displayCount =
            Mathf.Min(
                teamSprites.Count,
                Mathf.Min(
                    faces.Count,
                    icons.Count));

        for (int index = 0;
             index < displayCount;
             index++)
        {
            Character character =
                teamSprites[index];

            if (character == null)
                continue;

            if (faces[index] != null)
            {
                faces[index].sprite =
                    character.face;
            }

            if (icons[index] != null)
            {
                icons[index].sprite =
                    character.icon;
            }
        }
    }

    #endregion

    #region Main Values

    private void RefreshScore(
        bool force)
    {
        int currentScore =
            GameInstance.LevelScore;

        if (!force &&
            displayedScore == currentScore)
        {
            return;
        }

        displayedScore =
            currentScore;

        if (scoreText != null)
        {
            scoreText.text =
                currentScore.ToString(
                    "00000000");
        }
    }

    private void RefreshRings(
        bool force)
    {
        int currentRings =
            GameInstance.currentRings;

        if (!force &&
            displayedRings == currentRings)
        {
            return;
        }

        displayedRings =
            currentRings;

        if (ringText != null)
        {
            ringText.text =
                currentRings.ToString(
                    "000");
        }
    }

    private void RefreshLives(
        bool force)
    {
        int currentLives =
            GameInstance.livesCount;

        if (!force &&
            displayedLives == currentLives)
        {
            return;
        }

        displayedLives =
            currentLives;

        if (livesText != null)
        {
            livesText.text =
                currentLives.ToString(
                    "00");
        }
    }

    private void RefreshTimer(
        bool force)
    {
        if (timeText == null ||
            stageSession == null)
        {
            return;
        }

        int totalCentiseconds =
            Mathf.FloorToInt(
                stageSession.ElapsedTime *
                100f);

        if (!force &&
            displayedTime == totalCentiseconds)
        {
            return;
        }

        displayedTime =
            totalCentiseconds;

        int minutes =
            totalCentiseconds /
            6000;

        int seconds =
            totalCentiseconds /
            100 %
            60;

        int centiseconds =
            totalCentiseconds %
            100;

        timeText.text =
            $"{minutes:00}:" +
            $"{seconds:00}:" +
            $"{centiseconds:00}";
    }

    #endregion

    #region Character Levels

    private void RefreshCharacterLevels()
    {
        int speedLevel =
            Mathf.Clamp(
                GameInstance.speedLevelUp,
                0,
                speedLevelLights.Count);

        int flyLevel =
            Mathf.Clamp(
                GameInstance.flyLevelUp,
                0,
                flyLevelLights.Count);

        int powerLevel =
            Mathf.Clamp(
                GameInstance.powerLevelUp,
                0,
                powerLevelLights.Count);

        SetLevelLights(
            speedLevelLights,
            speedLevel);

        SetLevelLights(
            flyLevelLights,
            flyLevel);

        SetLevelLights(
            powerLevelLights,
            powerLevel);
    }

    private static void SetLevelLights(
        List<Image> lights,
        int activeCount)
    {
        if (lights == null)
            return;

        activeCount =
            Mathf.Clamp(
                activeCount,
                0,
                lights.Count);

        for (int index = 0;
             index < lights.Count;
             index++)
        {
            if (lights[index] != null)
            {
                lights[index].enabled =
                    index < activeCount;
            }
        }
    }

    #endregion

    #region Team Blast Display

    private void RefreshTeamBlastMeter(
        bool force)
    {
        ResolveTeamBlast();

        float currentGauge =
            teamBlast != null
                ? teamBlast.CurrentGauge
                : 0f;

        float maximumGauge =
            teamBlast != null
                ? teamBlast.MaxGauge
                : 100f;

        bool ready =
            teamBlast != null &&
            teamBlast.BlastReady;

        if (!force &&
            Mathf.Approximately(
                displayedTeamBlastGauge,
                currentGauge))
        {
            return;
        }

        displayedTeamBlastGauge =
            currentGauge;

        if (teamBlastGauge != null)
        {
            teamBlastGauge.maxValue =
                Mathf.Max(
                    1f,
                    maximumGauge);

            teamBlastGauge.value =
                currentGauge;
        }

        if (teamBlastPrompt != null)
        {
            teamBlastPrompt.SetActive(
                ready);
        }

        if (teamBlastFill != null)
        {
            teamBlastFill.color =
                ready
                    ? readyColor
                    : chargingColor;
        }
    }

    #endregion

    #region Item Pickup

    private IEnumerator ShowItemRoutine()
    {
        if (itemPickup == null)
            yield break;

        itemPickup.enabled =
            true;

        if (pickupDisplayDuration > 0f)
        {
            yield return
                new WaitForSeconds(
                    pickupDisplayDuration);
        }
        else
        {
            yield return null;
        }

        itemPickup.enabled =
            false;

        pickupRoutine =
            null;
    }

    #endregion

    #region Validation

    private bool ValidateConfiguration()
    {
        bool valid = true;

        valid &=
            ValidateReference(
                scoreText,
                "Score Text");

        valid &=
            ValidateReference(
                timeText,
                "Time Text");

        valid &=
            ValidateReference(
                ringText,
                "Ring Text");

        valid &=
            ValidateReference(
                livesText,
                "Lives Text");

        if (teamBlastGauge == null)
        {
            Debug.LogWarning(
                "HUD could not find the Team Blast Gauge.",
                this);
        }

        if (teamBlastPrompt == null)
        {
            Debug.LogWarning(
                "HUD could not find the Team Blast Prompt.",
                this);
        }

        if (teamBlastFill == null)
        {
            Debug.LogWarning(
                "HUD could not find the Team Blast Fill image.",
                this);
        }

        if (teamBlast == null)
        {
            Debug.LogWarning(
                "HUD could not find TeamBlast.",
                this);
        }

        if (stageSession == null)
        {
            Debug.LogWarning(
                "HUD could not find StageSession.",
                this);
        }

        return valid;
    }

    private bool ValidateReference(
        Object reference,
        string displayName)
    {
        if (reference != null)
            return true;

        Debug.LogError(
            $"HUD requires {displayName}.",
            this);

        return false;
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        UnsubscribeFromEvents();

        if (pickupRoutine != null)
        {
            StopCoroutine(
                pickupRoutine);

            pickupRoutine =
                null;
        }

        if (itemPickup != null)
        {
            itemPickup.enabled =
                false;
        }
    }

    private void CleanupDestroyedState()
    {
        CleanupRuntimeState();

        isInitialized =
            false;

        teamSprites.Clear();

        currentTeam =
            null;

        stageSession =
            null;

        teamBlast =
            null;

        teamBlastVideos =
            null;

        scoreText =
            null;

        timeText =
            null;

        ringText =
            null;

        livesText =
            null;

        livesImage =
            null;

        teamBlastGauge =
            null;

        teamBlastPrompt =
            null;

        teamBlastFill =
            null;

        itemPickup =
            null;
    }

    #endregion

    #region Debug

    private void LogStateChange(
        string message)
    {
        if (!logStateChanges)
            return;

        Debug.Log(
            message,
            this);
    }

    #endregion
}