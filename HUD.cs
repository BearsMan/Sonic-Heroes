using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
[DisallowMultipleComponent]
public sealed class HUD : MonoBehaviour
{
    [Header("Main HUD Text")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text ringText;
    [SerializeField] private TMP_Text livesText;

    [Header("Team Display")]
    [SerializeField] private List<Image> icons = new();
    [SerializeField] private List<Image> faces = new();
    [SerializeField] private List<Character> teamSprites = new();
    [SerializeField] private Image livesImage;
    [SerializeField] private TeamComposition curTeam;

    [Header("Character Level Lights")]
    [SerializeField] private List<Image> lightsSpeed = new();
    [SerializeField] private List<Image> lightsFly = new();
    [SerializeField] private List<Image> lightsPower = new();

    [Header("Team Blast")]
    [SerializeField] private Slider powerUpGauge;
    [SerializeField] private GameObject teamBlastPrompt;
    [SerializeField] private Image teamBlastFill;
    [SerializeField] private KeyCode teamBlastKey = KeyCode.Z;
    [SerializeField, Min(1)] private int maximumPower = 100;
    [SerializeField] private Color chargingColor = Color.blue;
    [SerializeField] private Color readyColor = Color.yellow;
    [SerializeField] private TeamBlastVideos teamBlastVideos;

    [Header("Item Pickup")]
    [SerializeField] private Image itemPickUp;
    [SerializeField, Min(0f)] private float pickupDisplayDuration = 3f;

    [SerializeField] private StageSession stageSession;

    private int powerUpLevel;
    private int displayedScore = int.MinValue;
    private int displayedRings = int.MinValue;
    private int displayedLives = int.MinValue;
    private int displayedPower = int.MinValue;
    private int displayedTime = int.MinValue;

    private Coroutine pickupRoutine;
    private bool teamBlastReady;
    private bool eventsSubscribed;

    public int PowerUpLevel => powerUpLevel;
    public int MaximumPower => maximumPower;
    public bool TeamBlastReady => teamBlastReady;

    private void Awake()
    {
        ValidateReferences();

        if (itemPickUp != null)
            itemPickUp.enabled = false;

        ConfigurePowerGauge();
        if (teamBlastVideos == null)
        {
            teamBlastVideos = Object.FindAnyObjectByType<TeamBlastVideos>();
        }

        if (teamBlastVideos == null)
        {
            Debug.LogWarning("HUD TeamBlastVideos reference is not assigned.", this);
        }

        if (stageSession == null)
        {
            stageSession = StageSession.Instance;
        }

        if (stageSession == null)
        {
            stageSession = Object.FindAnyObjectByType<StageSession>();
        }

        if (stageSession == null)
        {
            Debug.LogWarning("HUD StageSession reference is not assigned.", this);
        }
    }

    private void OnEnable()
    {
        SubscribeToEvents();
        RefreshAll();
    }

    private void Start()
    {
        RefreshAll();
    }

    private void Update()
    {
        UpdateTimer();
        RefreshChangingValues();
        HandleTeamBlastInput();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();

        if (pickupRoutine != null)
        {
            StopCoroutine(pickupRoutine);
            pickupRoutine = null;
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        if (eventsSubscribed)
            return;

        GameInstance.UpdateData += UpdateRings;
        eventsSubscribed = true;
    }

    private void UnsubscribeFromEvents()
    {
        if (!eventsSubscribed)
            return;

        GameInstance.UpdateData -= UpdateRings;
        eventsSubscribed = false;
    }

    private void ConfigurePowerGauge()
    {
        maximumPower = Mathf.Max(1, maximumPower);
        powerUpLevel = Mathf.Clamp(powerUpLevel, 0, maximumPower);

        if (powerUpGauge == null)
            return;

        powerUpGauge.minValue = 0f;
        powerUpGauge.maxValue = maximumPower;
        powerUpGauge.wholeNumbers = true;
        powerUpGauge.value = powerUpLevel;
    }

    private void UpdateTimer()
    {
        if (stageSession == null)
            return;

        int centiseconds =
            Mathf.FloorToInt(
                stageSession.ElapsedTime * 100f);

        if (centiseconds == displayedTime)
            return;

        displayedTime = centiseconds;
        RefreshTimer();
    }

    private void RefreshTimer()
    {
        if (timeText == null ||
            stageSession == null)
        {
            return;
        }

        float elapsedTime =
            stageSession.ElapsedTime;

        int totalCentiseconds =
            Mathf.FloorToInt(
                elapsedTime * 100f);

        int minutes =
            totalCentiseconds / 6000;

        int seconds =
            totalCentiseconds / 100 % 60;

        int centiseconds =
            totalCentiseconds % 100;

        timeText.text =
            $"{minutes:00}:" +
            $"{seconds:00}:" +
            $"{centiseconds:00}";
    }

    private void RefreshChangingValues()
    {
        RefreshScore();
        UpdateRings();
        RefreshLives();
        UpdateTeamBlastMeter();
    }

    public void UpdateHUD()
    {
        RefreshAll();
    }

    public void RefreshAll()
    {
        RefreshTimer();
        RefreshScore(true);
        UpdateRings(true);
        RefreshLives(true);
        UpdateCharacterLevels();
        UpdateTeamBlastMeter(true);
        RefreshTeamDisplay();
    }

    private void RefreshScore(bool force = false)
    {
        int currentScore = GameInstance.LevelScore;

        if (!force && displayedScore == currentScore)
            return;

        displayedScore = currentScore;

        if (scoreText != null)
            scoreText.text = currentScore.ToString("00000000");
    }

    public void UpdateRings()
    {
        UpdateRings(false);
    }

    private void UpdateRings(bool force)
    {
        int currentRings = GameInstance.currentRings;

        if (!force && displayedRings == currentRings)
            return;

        displayedRings = currentRings;

        if (ringText != null)
            ringText.text = currentRings.ToString("000");
    }

    private void RefreshLives(bool force = false)
    {
        int currentLives = GameInstance.livesCount;

        if (!force && displayedLives == currentLives)
            return;

        displayedLives = currentLives;

        if (livesText != null)
            livesText.text = currentLives.ToString("00");
    }

    public void Setup(TeamComposition currentTeam)
    {
        if (currentTeam == null)
        {
            Debug.LogWarning("HUD Setup received no TeamComposition.", this);
            return;
        }

        curTeam = currentTeam;

        teamSprites.Clear();

        AddCharacterFromPrefab(
            curTeam.SpeedCharacterPrefab);

        AddCharacterFromPrefab(
            curTeam.FlyingCharacterPrefab);

        AddCharacterFromPrefab(
            curTeam.PowerCharacterPrefab);

        RefreshTeamDisplay();
        UpdateCharacterLevels();
    }

    private void AddCharacterFromPrefab(GameObject characterPrefab)
    {
        if (characterPrefab == null)
        {
            Debug.LogWarning(
                "HUD received an unassigned character prefab.",
                this);

            return;
        }

        Character character =
            characterPrefab.GetComponent<Character>();

        if (character == null)
        {
            character =
                characterPrefab.GetComponentInChildren<Character>();
        }

        if (character == null)
        {
            Debug.LogWarning(
                $"The prefab '{characterPrefab.name}' does not contain a Character component.",
                characterPrefab);

            return;
        }

        teamSprites.Add(character);
    }

    public void SetCharacter(CHARACTERTYPES type)
    {
        if (!TryInitializeTeam())
            return;

        int leaderIndex = type switch
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
                $"HUD received unsupported character type: {type}.",
                this);

            return;
        }

        ArrangeTeamWithLeader(
            teamSprites[leaderIndex]);

        RefreshTeamDisplay();
    }

    public void SetCharacter(int direction)
    {
        if (!TryInitializeTeam() || teamSprites.Count < 2)
            return;

        if (direction > 0)
        {
            Character lastCharacter = teamSprites[^1];
            teamSprites.RemoveAt(teamSprites.Count - 1);
            teamSprites.Insert(0, lastCharacter);
        }
        else if (direction < 0)
        {
            Character firstCharacter = teamSprites[0];
            teamSprites.RemoveAt(0);
            teamSprites.Add(firstCharacter);
        }

        RefreshTeamDisplay();
    }

    private void ArrangeTeamWithLeader(Character leader)
    {
        if (leader == null || teamSprites.Count == 0)
            return;

        int leaderIndex = teamSprites.IndexOf(leader);

        if (leaderIndex < 0)
            return;

        for (int i = 0; i < leaderIndex; i++)
        {
            Character firstCharacter = teamSprites[0];
            teamSprites.RemoveAt(0);
            teamSprites.Add(firstCharacter);
        }
    }

    private bool TryInitializeTeam()
    {
        if (teamSprites.Count >= 3)
            return true;

        if (curTeam == null)
        {
            Debug.LogWarning("HUD cannot initialize its team without a TeamComposition.", this);
            return false;
        }

        teamSprites.Clear();

        AddCharacterFromPrefab(
            curTeam.SpeedCharacterPrefab);

        AddCharacterFromPrefab(
            curTeam.FlyingCharacterPrefab);

        AddCharacterFromPrefab(
            curTeam.PowerCharacterPrefab);

        return teamSprites.Count == 3;
    }

    private void RefreshTeamDisplay()
    {
        if (teamSprites.Count == 0)
            return;

        Character leader = teamSprites[0];

        if (livesImage != null && leader != null)
            livesImage.sprite = leader.face;

        int displayCount = Mathf.Min(
            teamSprites.Count,
            Mathf.Min(faces.Count, icons.Count));

        for (int i = 0; i < displayCount; i++)
        {
            Character character = teamSprites[i];

            if (character == null)
                continue;

            if (faces[i] != null)
                faces[i].sprite = character.face;

            if (icons[i] != null)
                icons[i].sprite = character.icon;
        }
    }

    public void UpdateCharacterLevels()
    {
        GameInstance.speedLevelUp =
            Mathf.Clamp(GameInstance.speedLevelUp, 0, lightsSpeed.Count);

        GameInstance.flyLevelUp =
            Mathf.Clamp(GameInstance.flyLevelUp, 0, lightsFly.Count);

        GameInstance.powerLevelUp =
            Mathf.Clamp(GameInstance.powerLevelUp, 0, lightsPower.Count);

        SetLevelLights(lightsSpeed, GameInstance.speedLevelUp);
        SetLevelLights(lightsFly, GameInstance.flyLevelUp);
        SetLevelLights(lightsPower, GameInstance.powerLevelUp);
    }

    private static void SetLevelLights(List<Image> lights, int activeCount)
    {
        if (lights == null)
            return;

        activeCount = Mathf.Clamp(activeCount, 0, lights.Count);

        for (int i = 0; i < lights.Count; i++)
        {
            if (lights[i] != null)
                lights[i].enabled = i < activeCount;
        }
    }

    public void AddPower(int value)
    {
        if (value <= 0 || teamBlastReady)
            return;

        powerUpLevel = Mathf.Clamp(
            powerUpLevel + value,
            0,
            maximumPower);

        UpdateTeamBlastMeter(true);
    }

    public void UpdateTeamBlastMeter()
    {
        UpdateTeamBlastMeter(false);
    }

    private void UpdateTeamBlastMeter(bool force)
    {
        powerUpLevel = Mathf.Clamp(powerUpLevel, 0, maximumPower);

        if (!force && displayedPower == powerUpLevel)
            return;

        displayedPower = powerUpLevel;
        teamBlastReady = powerUpLevel >= maximumPower;

        if (powerUpGauge != null)
            powerUpGauge.value = powerUpLevel;

        if (teamBlastPrompt != null)
            teamBlastPrompt.SetActive(teamBlastReady);

        if (teamBlastFill != null)
        {
            teamBlastFill.color = teamBlastReady
                ? readyColor
                : chargingColor;
        }
    }

    private void HandleTeamBlastInput()
    {
        if (!teamBlastReady || !Input.GetKeyDown(teamBlastKey))
            return;

        ActivateTeamBlast();
    }

    public void ActivateTeamBlast()
    {
        if (!teamBlastReady)
            return;

        if (teamBlastVideos == null)
        {
            Debug.LogWarning("HUD could not find TeamBlastVideos.", this);

            return;
        }

        teamBlastVideos.PlayTeamBlast();
        ResetPower();
    }

    public void ResetPower()
    {
        powerUpLevel = 0;
        teamBlastReady = false;
        UpdateTeamBlastMeter(true);
    }

    public void ShowPickUp(Sprite item)
    {
        if (itemPickUp == null || item == null)
            return;

        if (pickupRoutine != null)
            StopCoroutine(pickupRoutine);

        itemPickUp.sprite = item;
        pickupRoutine = StartCoroutine(ShowItem());
    }

    private IEnumerator ShowItem()
    {
        itemPickUp.enabled = true;

        if (pickupDisplayDuration > 0f)
            yield return new WaitForSeconds(pickupDisplayDuration);
        else
            yield return null;

        itemPickUp.enabled = false;
        pickupRoutine = null;
    }

    private void ValidateReferences()
    {
        if (scoreText == null)
            Debug.LogWarning("HUD Score Text is not assigned.", this);

        if (timeText == null)
            Debug.LogWarning("HUD Time Text is not assigned.", this);

        if (ringText == null)
            Debug.LogWarning("HUD Ring Text is not assigned.", this);

        if (livesText == null)
            Debug.LogWarning("HUD Lives Text is not assigned.", this);

        if (powerUpGauge == null)
            Debug.LogWarning("HUD Power Up Gauge is not assigned.", this);

        if (teamBlastPrompt == null)
        {
            Debug.LogWarning(
                "HUD Team Blast Prompt is not assigned.",
                this);
        }

        if (teamBlastFill == null)
        {
            Debug.LogWarning("HUD Team Blast Fill is not assigned.", this);
        }
    }

    private void OnValidate()
    {
        maximumPower = Mathf.Max(1, maximumPower);
        pickupDisplayDuration = Mathf.Max(0f, pickupDisplayDuration);

        if (powerUpGauge != null)
        {
            powerUpGauge.minValue = 0f;
            powerUpGauge.maxValue = maximumPower;
            powerUpGauge.wholeNumbers = true;
        }
    }
}