using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ScoreSystem : MonoBehaviour
{
    private const string RankingResourcePath = "Level Rankings";
    private const int GradeCount = 5;
    private const int RankingThresholdCount = GradeCount - 1;

    [Header("Panels")]
    [FormerlySerializedAs("NormalHUD")]
    [SerializeField] private GameObject normalHUD;

    [FormerlySerializedAs("scoreSystem")]
    [SerializeField] private GameObject scorePanel;

    [Header("Character Scores")]
    [FormerlySerializedAs("SpeedCharacterScore")]
    [SerializeField] private TMP_Text speedCharacterScoreText;

    [FormerlySerializedAs("FlyingCharacterScore")]
    [SerializeField] private TMP_Text flyingCharacterScoreText;

    [FormerlySerializedAs("PowerCharacterScore")]
    [SerializeField] private TMP_Text powerCharacterScoreText;

    [Header("Stage Results")]
    [FormerlySerializedAs("TimeScore")]
    [SerializeField] private TMP_Text timeScoreText;

    [FormerlySerializedAs("RingCountScore")]
    [SerializeField] private TMP_Text ringCountScoreText;

    [FormerlySerializedAs("TimeBonusScore")]
    [SerializeField] private TMP_Text timeBonusScoreText;

    [FormerlySerializedAs("FinalScore")]
    [SerializeField] private TMP_Text finalScoreText;

    [FormerlySerializedAs("FinalGrade")]
    [SerializeField] private Image finalGradeImage;

    [Header("Grade Sprites")]
    [FormerlySerializedAs("gradeA")]
    [SerializeField] private Sprite gradeASprite;

    [FormerlySerializedAs("gradeB")]
    [SerializeField] private Sprite gradeBSprite;

    [FormerlySerializedAs("gradeC")]
    [SerializeField] private Sprite gradeCSprite;

    [FormerlySerializedAs("gradeD")]
    [SerializeField] private Sprite gradeDSprite;

    [FormerlySerializedAs("gradeE")]
    [SerializeField] private Sprite gradeESprite;

    [Header("Score Settings")]
    [SerializeField, Min(0f)]
    private float maximumBonusTime = 625f;

    [SerializeField, Min(0)]
    private int pointsPerRemainingSecond = 80;

    [SerializeField, Min(0)]
    private int pointsPerRing = 10;

    public static int FinalScore { get; private set; }
    public static int SpeedScore { get; private set; }
    public static int FlyScore { get; private set; }
    public static int PowerScore { get; private set; }

    public static int finalScore
    {
        get => FinalScore;
        set => FinalScore = Mathf.Max(0, value);
    }

    private Sprite[] gradeSprites;
    private UltimatePlayerMovement playerMovement;
    private Ranking ranking;
    private TeamSetup teamSetup;
    private StageSession stageSession;
    private int ringCount;
    private int ringScore;
    private int timeBonus;
    private float elapsedTime;
    private bool sequenceStarted;

    private void Awake()
    {
        ResetResults();

        ranking = LoadRanking();

        CacheReferences();
        CacheGradeSprites();
    }

    public void StartEndLevelSequence()
    {
        if (!ValidateSetup())
        {
            return;
        }

        if (sequenceStarted)
        {
            return;
        }

        sequenceStarted = true;

        ShowScorePanel();
        DisablePlayerMovement();
        CalculateScore();
        UpdateScoreDisplay();
        AssignGrade();
    }

    private void CacheReferences()
    {
        teamSetup = TeamSetup.Instance;
        playerMovement = Object.FindAnyObjectByType<UltimatePlayerMovement>();
        stageSession = StageSession.Instance;

        if (stageSession == null)
        {
            stageSession = Object.FindAnyObjectByType<StageSession>();
        }
    }

    private void CacheGradeSprites()
    {
        gradeSprites ??= new Sprite[GradeCount];

        gradeSprites[0] = gradeASprite;
        gradeSprites[1] = gradeBSprite;
        gradeSprites[2] = gradeCSprite;
        gradeSprites[3] = gradeDSprite;
        gradeSprites[4] = gradeESprite;
    }

    private void CalculateScore()
    {
        SpeedScore =
            Mathf.Max(0, GameInstance.speedScore);

        FlyScore =
            Mathf.Max(0, GameInstance.flyScore);

        PowerScore =
            Mathf.Max(0, GameInstance.powerScore);

        ringCount =
            Mathf.Max(0, GameInstance.currentRings);

        ringScore =
            ringCount * pointsPerRing;

        elapsedTime = stageSession != null? Mathf.Max(0f, stageSession.ElapsedTime): 0f;

        timeBonus =
            CalculateTimeBonus(elapsedTime);

        FinalScore =
            SpeedScore +
            FlyScore +
            PowerScore +
            ringScore +
            timeBonus;
    }

    private int CalculateTimeBonus(
        float stageTime)
    {
        float remainingTime =
            Mathf.Max(
                0f,
                maximumBonusTime - stageTime);

        return Mathf.RoundToInt(
            remainingTime *
            pointsPerRemainingSecond);
    }

    private void UpdateScoreDisplay()
    {
        SetText(
            speedCharacterScoreText,
            SpeedScore);

        SetText(
            flyingCharacterScoreText,
            FlyScore);

        SetText(
            powerCharacterScoreText,
            PowerScore);

        if (timeScoreText != null)
            timeScoreText.text = FormatTime(elapsedTime);

        SetText(
            ringCountScoreText,
            ringCount);

        SetText(
            timeBonusScoreText,
            timeBonus);

        SetText(
            finalScoreText,
            FinalScore);
    }

    private void AssignGrade()
    {
        Sprite gradeSprite =
            gradeESprite;

        if (TryGetRankingScores(
            out int[] rankingScores))
        {
            gradeSprite =
                GetGradeSprite(rankingScores);
        }

        if (finalGradeImage != null)
        {
            finalGradeImage.sprite =
                gradeSprite;
        }
    }

    private Sprite GetGradeSprite(
        int[] rankingScores)
    {
        for (int i = 0;
            i < RankingThresholdCount;
            i++)
        {
            if (FinalScore >= rankingScores[i])
                return gradeSprites[i];
        }

        return gradeSprites[GradeCount - 1];
    }

    private bool TryGetRankingScores(
        out int[] rankingScores)
    {
        rankingScores = null;

        if (ranking == null)
            return false;

        rankingScores =
            GetTeamRankingScores(ranking);

        if (rankingScores == null)
            return false;

        if (rankingScores.Length <
            RankingThresholdCount)
        {
            Debug.LogError(
                $"The ranking table requires at least {RankingThresholdCount} score thresholds.",
                ranking);

            rankingScores = null;
            return false;
        }

        if (!AreRankingScoresOrdered(
            rankingScores))
        {
            Debug.LogError(
                "Ranking thresholds must be ordered from A to D, highest to lowest.",
                ranking);

            rankingScores = null;
            return false;
        }

        return true;
    }

    private Ranking LoadRanking()
    {
        string sceneName =
            SceneManager.GetActiveScene().name;

        string resourcePath =
            $"{RankingResourcePath}/{sceneName}";

        Ranking loadedRanking =
            Resources.Load<Ranking>(
                resourcePath);

        if (loadedRanking == null)
        {
            Debug.LogError(
                $"No Ranking asset was found at Resources/{resourcePath}.",
                this);
        }

        return loadedRanking;
    }

    private int[] GetTeamRankingScores(Ranking rankingAsset)
    {
        if (teamSetup == null)
        {
            Debug.LogError(
                "TeamSetup was not found.",
                this);

            return null;
        }

        if (teamSetup.CurrentTeam == null)
        {
            Debug.LogError(
                "TeamSetup does not have a Team Composition assigned.",
                teamSetup);

            return null;
        }

        int[] rankingScores = teamSetup.CurrentPlayableTeam switch
        {
            PlayableTeam.TeamSonic => rankingAsset.teamSonic,
            PlayableTeam.TeamDark => rankingAsset.teamDark,
            PlayableTeam.TeamRose => rankingAsset.teamRose,
            PlayableTeam.TeamChaotix => rankingAsset.teamChaotix,
            _ => throw new System.ArgumentOutOfRangeException(
                    nameof(teamSetup.CurrentPlayableTeam))
        };

        if (rankingScores == null)
        {
            Debug.LogError(
                $"No ranking table is configured for {teamSetup.CurrentPlayableTeam}.",
                this);
        }

        return rankingScores;
    }

    private static bool AreRankingScoresOrdered(
        int[] rankingScores)
    {
        for (int i = 0;
            i < RankingThresholdCount - 1;
            i++)
        {
            if (rankingScores[i] <
                rankingScores[i + 1])
            {
                return false;
            }
        }

        return true;
    }

    private void ShowScorePanel()
    {
        if (normalHUD != null)
        {
            normalHUD.SetActive(false);
        }
        else
        {
            Debug.LogWarning(
                "The Normal HUD is not assigned.",
                this);
        }

        if (scorePanel != null)
        {
            scorePanel.SetActive(true);
        }
        else
        {
            Debug.LogError(
                "The Score Panel is not assigned.",
                this);
        }
    }

    private void DisablePlayerMovement()
    {
        if (playerMovement != null)
        {
            playerMovement.enabled = false;
            return;
        }

        Debug.LogWarning(
            "UltimatePlayerMovement was not found.",
            this);
    }

    private static void SetText(
        TMP_Text textComponent,
        int value)
    {
        if (textComponent != null)
            textComponent.text = value.ToString();
    }

    private static string FormatTime(
        float stageTime)
    {
        int totalCentiseconds =
            Mathf.Max(
                0,
                Mathf.RoundToInt(stageTime * 100f));

        int minutes =
            totalCentiseconds / 6000;

        int seconds =
            totalCentiseconds / 100 % 60;

        int centiseconds =
            totalCentiseconds % 100;

        return
            $"{minutes:00}:" +
            $"{seconds:00}:" +
            $"{centiseconds:00}";
    }

    private void ResetResults()
    {
        FinalScore = 0;
        SpeedScore = 0;
        FlyScore = 0;
        PowerScore = 0;

        ringCount = 0;
        ringScore = 0;
        timeBonus = 0;
        elapsedTime = 0f;
        sequenceStarted = false;
    }

    private bool ValidateSetup()
    {
        bool isValid = true;

        if (ranking == null)
        {
            Debug.LogError(
                "Ranking asset failed to load.",
                this);

            isValid = false;
        }

        if (scorePanel == null)
        {
            Debug.LogError(
                "The Score Panel is not assigned.",
                this);

            isValid = false;
        }

        if (finalGradeImage == null)
        {
            Debug.LogError(
                "The Final Grade Image is not assigned.",
                this);

            isValid = false;
        }

        if (gradeASprite == null ||
            gradeBSprite == null ||
            gradeCSprite == null ||
            gradeDSprite == null ||
            gradeESprite == null)
        {
            Debug.LogError(
                "One or more Grade Sprites are not assigned.",
                this);

            isValid = false;
        }

        if (stageSession == null)
        {
            Debug.LogError(
                "StageSession was not found.",
                this);

            isValid = false;
        }

        return isValid;
    }

    private void OnValidate()
    {
        maximumBonusTime =
            Mathf.Max(0f, maximumBonusTime);

        pointsPerRemainingSecond =
            Mathf.Max(0, pointsPerRemainingSecond);

        pointsPerRing =
            Mathf.Max(0, pointsPerRing);

        CacheGradeSprites();
    }
}