// SubtitleSystem.cs

using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
[DisallowMultipleComponent]

[RequireComponent(typeof(CanvasGroup))]
public class SubtitleSystem : MonoBehaviour
{
    [Header("UI Components")]
    public TextMeshProUGUI subtitleText;
    public Image backgroundPanel;
    public CanvasGroup canvasGroup;

    [Header("Database")]
    public SubtitleDatabase database;

    [Header("Optional Audio")]
    public AudioSource voiceSource;

    [Header("Subtitle Timing")]
    public float fadeInDuration = 0.3f;
    public float fadeOutDuration = 0.5f;
    public float displayDuration = 3.0f;

    [Header("Subtitle Layout")]
    public Vector2 padding = new Vector2(16, 8);
    public bool autoSizeBackground = true;
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.6f);
    public Color textColor = Color.white;
    public int fontSize = 26;
    public TMP_FontAsset fontAsset;
    public TextAlignmentOptions textAlignment = TextAlignmentOptions.Center;
    public bool enableWordWrapping = true;

    [Range(0.1f, 1f)]
    public float maxWidthPercentage = 0.8f;

    [Range(0.1f, 1f)]
    public float maxHeightPercentage = 0.3f;

    [Header("Behavior")]
    public bool queueSubtitles = true;
    public bool prefixCharacterName = true;
    public bool useTypewriterEffect = false;
    public float typewriterCharactersPerSecond = 45f;

    [Header("Censorship")]
    public bool enableCensorship = true;
    public List<string> prohibitedWords = new();
    public string censorReplacement = "***";

    private readonly Queue<QueuedSubtitle> subtitleQueue = new();

    private Coroutine currentCoroutine;
    private RectTransform backgroundRect;
    private RectTransform textRect;

    private int screenWidthPixels = 0;
    private int screenHeightPixels = 0;
    private int maxTextWidth = 0;
    private int maxTextHeight = 0;
    private bool isPlaying = false;

    private struct QueuedSubtitle
    {
        public string Message;
        public AudioClip VoiceClip;

        public QueuedSubtitle(string message, AudioClip voiceClip)
        {
            Message = message;
            VoiceClip = voiceClip;
        }
    }

    private void Awake()
    {
        if (subtitleText == null)
        {
            Debug.LogError("SubtitleSystem: subtitleText is not assigned.");
            enabled = false;
            return;
        }

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;

        textRect = subtitleText.GetComponent<RectTransform>();

        if (backgroundPanel != null)
        {
            backgroundRect = backgroundPanel.GetComponent<RectTransform>();
            backgroundPanel.color = backgroundColor;
        }

        ApplyTextSettings();
        UpdateScreenDimensions();

        if (autoSizeBackground)
            ResizeBackground();
    }

    private void Update()
    {
        if (Screen.width != screenWidthPixels || Screen.height != screenHeightPixels)
        {
            UpdateScreenDimensions();

            if (autoSizeBackground)
                ResizeBackground();
        }
    }

    public void ShowSubtitle(string message)
    {
        ShowSubtitle(message, null);
    }

    public void ShowSubtitle(string message, AudioClip voiceClip)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (enableCensorship)
            message = CensorMessage(message);

        var queuedSubtitle = new QueuedSubtitle(message, voiceClip);

        if (queueSubtitles && isPlaying)
        {
            subtitleQueue.Enqueue(queuedSubtitle);
            return;
        }

        StartSubtitle(queuedSubtitle);
    }

    public void ShowCharacterSubtitle(SonicHeroesCharacter character, string line)
    {
        string message = prefixCharacterName
            ? $"[{character}]\n{line}"
            : line;

        ShowSubtitle(message, null);
    }

    public void ShowCharacterSubtitle(CharacterSubtitleEntry entry)
    {
        if (entry == null)
            return;

        string message = prefixCharacterName
            ? $"[{entry.character}]\n{entry.line}"
            : entry.line;

        ShowSubtitle(message, entry.voiceClip);
    }

    public void ShowRandomLine(SonicHeroesCharacter character)
    {
        if (database == null)
        {
            Debug.LogWarning("SubtitleSystem: No SubtitleDatabase assigned.");
            return;
        }

        var pool = database.GetLinesForCharacter(character);

        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning($"SubtitleSystem: No lines registered for {character}.");
            return;
        }

        ShowCharacterSubtitle(pool[Random.Range(0, pool.Count)]);
    }

    public void ShowRandomTeamLine(SonicHeroesTeam team)
    {
        if (database == null)
        {
            Debug.LogWarning("SubtitleSystem: No SubtitleDatabase assigned.");
            return;
        }

        var pool = database.GetLinesForTeam(team);

        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning($"SubtitleSystem: No lines registered for Team {team}.");
            return;
        }

        ShowCharacterSubtitle(pool[Random.Range(0, pool.Count)]);
    }

    public void ClearSubtitleQueue()
    {
        subtitleQueue.Clear();
    }
    public void PlayEvent(SonicHeroesTeam team, SubtitleEventID eventID)
    {
        if (database == null)
        {
            Debug.LogWarning("SubtitleSystem: No SubtitleDatabase assigned.");
            return;
        }

        var pool = database.GetByEvent(team, eventID);

        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning($"SubtitleSystem: No subtitle found for {team} / {eventID}.");
            return;
        }

        var entry = pool[Random.Range(0, pool.Count)];

        string message = prefixCharacterName
            ? $"[{entry.character}]\n{entry.line}"
            : entry.line;

        ShowSubtitle(message, entry.voiceClip);
    }

    public void PlayStageEvent(SonicHeroesTeam team, StageID stageID, SubtitleEventID eventID)
    {
        if (database == null)
        {
            Debug.LogWarning("SubtitleSystem: No SubtitleDatabase assigned.");
            return;
        }

        var pool = database.GetByStageEvent(team, stageID, eventID);

        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning($"SubtitleSystem: No subtitle found for {team} / {stageID} / {eventID}.");
            return;
        }

        var entry = pool[Random.Range(0, pool.Count)];

        string message = prefixCharacterName
            ? $"[{entry.character}]\n{entry.line}"
            : entry.line;

        ShowSubtitle(message, entry.voiceClip);
    }
    public void HideImmediately()
    {
        if (currentCoroutine != null)
            StopCoroutine(currentCoroutine);

        currentCoroutine = null;
        isPlaying = false;
        subtitleQueue.Clear();

        if (voiceSource != null)
            voiceSource.Stop();

        canvasGroup.alpha = 0f;
        subtitleText.text = "";
    }

    private void StartSubtitle(QueuedSubtitle subtitle)
    {
        if (currentCoroutine != null)
            StopCoroutine(currentCoroutine);

        currentCoroutine = StartCoroutine(SubtitleRoutine(subtitle));
    }

    private IEnumerator SubtitleRoutine(QueuedSubtitle subtitle)
    {
        isPlaying = true;

        if (voiceSource != null)
        {
            voiceSource.Stop();

            if (subtitle.VoiceClip != null)
                voiceSource.PlayOneShot(subtitle.VoiceClip);
        }

        ApplyTextSettings();

        if (useTypewriterEffect)
            yield return TypewriterRoutine(subtitle.Message);
        else
            SetSubtitleText(subtitle.Message);

        yield return FadeCanvas(0f, 1f, fadeInDuration);
        yield return new WaitForSeconds(displayDuration);
        yield return FadeCanvas(1f, 0f, fadeOutDuration);

        currentCoroutine = null;
        isPlaying = false;

        if (queueSubtitles && subtitleQueue.Count > 0)
            StartSubtitle(subtitleQueue.Dequeue());
    }

    private IEnumerator TypewriterRoutine(string fullMessage)
    {
        subtitleText.text = "";

        if (autoSizeBackground)
        {
            subtitleText.text = fullMessage;
            subtitleText.ForceMeshUpdate();
            ResizeBackground();
            subtitleText.text = "";
        }

        if (typewriterCharactersPerSecond <= 0f)
        {
            SetSubtitleText(fullMessage);
            yield break;
        }

        float secondsPerCharacter = 1f / typewriterCharactersPerSecond;

        for (int i = 0; i <= fullMessage.Length; i++)
        {
            subtitleText.text = fullMessage.Substring(0, i);
            yield return new WaitForSeconds(secondsPerCharacter);
        }
    }

    private IEnumerator FadeCanvas(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            canvasGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        canvasGroup.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = to;
    }

    private void SetSubtitleText(string message)
    {
        subtitleText.text = message;
        subtitleText.ForceMeshUpdate();

        if (autoSizeBackground)
            ResizeBackground();
    }

    private void ApplyTextSettings()
    {
        subtitleText.fontSize = fontSize;
        subtitleText.color = textColor;
        subtitleText.alignment = textAlignment;
        subtitleText.textWrappingMode = enableWordWrapping
            ? TextWrappingModes.Normal
            : TextWrappingModes.NoWrap;

        if (fontAsset != null)
            subtitleText.font = fontAsset;

        if (backgroundPanel != null)
            backgroundPanel.color = backgroundColor;
    }

    private void UpdateScreenDimensions()
    {
        screenWidthPixels = Screen.width;
        screenHeightPixels = Screen.height;

        maxTextWidth = Mathf.RoundToInt(screenWidthPixels * maxWidthPercentage);
        maxTextHeight = Mathf.RoundToInt(screenHeightPixels * maxHeightPercentage);
    }

    private void ResizeBackground()
    {
        if (backgroundRect == null || subtitleText == null)
            return;

        Vector2 textSize = subtitleText.GetPreferredValues(
            subtitleText.text,
            maxTextWidth,
            maxTextHeight
        );

        backgroundRect.sizeDelta = textSize + padding * 2f;
    }

    private string CensorMessage(string message)
    {
        if (prohibitedWords == null || prohibitedWords.Count == 0)
            return message;

        foreach (string word in prohibitedWords)
        {
            if (string.IsNullOrWhiteSpace(word))
                continue;

            string pattern = $@"\b{Regex.Escape(word)}\b";

            message = Regex.Replace(
                message,
                pattern,
                censorReplacement,
                RegexOptions.IgnoreCase
            );
        }

        return message;
    }
}