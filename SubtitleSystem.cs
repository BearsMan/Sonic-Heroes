// SubtitleSystem.cs
// Drop on a UI GameObject with a TextMeshProUGUI and optional background Panel.
// Requires: TextMeshPro package.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class SubtitleSystem : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private TextMeshProUGUI speakerText;
    [SerializeField] private Image backgroundPanel;
    [SerializeField] private bool showBackgroundPanel = true;

    [Header("Appearance")]
    [SerializeField] private float maxLines = 2;
    [SerializeField] private TextAlignmentOptions alignment = TextAlignmentOptions.Center;
    [SerializeField] private bool useRichText = true;
    [SerializeField] private bool upperCaseCaptions = false; // For [SFX] style captions

    [Header("Reveal")]
    [SerializeField] private bool characterByCharacter = false;
    [SerializeField] private float charsPerSecond = 60f;

    [Header("Audio Sync")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private bool syncToAudio = true; // If true, uses audio time to drive subtitles

    [Header("Localization")]
    [Tooltip("Override text via localization key -> localized string mapping.")]
    [SerializeField] private TextAsset localizationJson; // Simple key/value JSON
    private Dictionary<string, string> localizationMap;

    [Header("Occlusion")]
    [Tooltip("Hide subtitles when player is far or inaudible.")]
    [SerializeField] private Transform listener;
    [SerializeField] private Transform source;
    [SerializeField] private float audibleDistance = 25f;
    [SerializeField] private bool hideWhenInaudible = true;

    [Header("Debug")]
    [SerializeField] private bool logWarnings = true;

    // Internal state
    private List<SubtitleEntry> timeline = new List<SubtitleEntry>();
    private int currentIndex = -1;
    private float revealCounter = 0f;
    private string targetText = string.Empty;
    private bool isRunning = false;
    private float manualClock = 0f;

    // SRT pattern: index, time range, text (supports multi-line)
    private static readonly Regex srtBlockRegex = new Regex(
        @"(\d+)\s*\r?\n(\d{2}:\d{2}:\d{2},\d{3})\s*-->\s*(\d{2}:\d{2}:\d{2},\d{3})(?:.*)\r?\n([\s\S]*?)(?=\r?\n\r?\n|\z)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    [Serializable]
    private class SubtitleEntry
    {
        public int index;
        public double start;
        public double end;
        public string text;
        public string speaker; // Optional: parsed from {SPEAKER:Name} or [SPEAKER:Name]
        public string key;     // Optional localization key
        public bool isCaption; // True if looks like [SFX] or <CAPTION>
    }

    private void Awake()
    {
        if (subtitleText == null)
        {
            subtitleText = GetComponentInChildren<TextMeshProUGUI>();
        }
        if (speakerText != null) speakerText.text = string.Empty;
        if (subtitleText != null)
        {
            subtitleText.richText = useRichText;
            subtitleText.alignment = alignment;
        }
        if (backgroundPanel != null) backgroundPanel.enabled = showBackgroundPanel;

        LoadLocalizationMap();
    }

    private void Update()
    {
        if (!isRunning || timeline.Count == 0 || subtitleText == null) return;

        // Determine clock source
        float t = syncToAudio && audioSource != null && audioSource.clip != null
            ? audioSource.time
            : manualClock += Time.deltaTime;

        // Occlusion: hide if inaudible
        if (hideWhenInaudible && listener != null && source != null)
        {
            float dist = Vector3.Distance(listener.position, source.position);
            if (dist > audibleDistance)
            {
                SetSubtitleVisible(false);
                return;
            }
            else
            {
                SetSubtitleVisible(true);
            }
        }

        // Advance index based on time
        int nextIndex = currentIndex;
        if (currentIndex < 0 || t >= (float)timeline[currentIndex].end)
        {
            nextIndex = FindActiveIndex(t);
            if (nextIndex != currentIndex)
            {
                currentIndex = nextIndex;
                if (currentIndex >= 0)
                {
                    ApplyEntry(timeline[currentIndex]);
                }
                else
                {
                    ClearSubtitle();
                }
            }
        }

        // Per-character reveal
        if (characterByCharacter && !string.IsNullOrEmpty(targetText))
        {
            revealCounter += Time.deltaTime * charsPerSecond;
            int count = Mathf.Clamp(Mathf.FloorToInt(revealCounter), 0, targetText.Length);
            subtitleText.text = targetText.Substring(0, count);
        }
    }

    private int FindActiveIndex(float t)
    {
        for (int i = 0; i < timeline.Count; i++)
        {
            if (t >= (float)timeline[i].start && t < (float)timeline[i].end)
                return i;
        }
        return -1;
    }

    private void ApplyEntry(SubtitleEntry entry)
    {
        // Localization override
        string displayText = entry.text;
        if (!string.IsNullOrEmpty(entry.key) && localizationMap != null && localizationMap.TryGetValue(entry.key, out var localized))
        {
            displayText = localized;
        }

        // Closed captions formatting
        if (entry.isCaption && upperCaseCaptions)
            displayText = displayText.ToUpperInvariant();

        // Speaker
        if (speakerText != null)
            speakerText.text = string.IsNullOrEmpty(entry.speaker) ? string.Empty : entry.speaker;

        // Reveal handling
        revealCounter = 0f;
        targetText = displayText;
        if (characterByCharacter)
        {
            subtitleText.text = string.Empty;
        }
        else
        {
            subtitleText.text = targetText;
        }
        SetSubtitleVisible(true);
    }

    private void ClearSubtitle()
    {
        subtitleText.text = string.Empty;
        if (speakerText != null) speakerText.text = string.Empty;
        SetSubtitleVisible(false);
    }

    private void SetSubtitleVisible(bool visible)
    {
        subtitleText.enabled = visible;
        if (backgroundPanel != null) backgroundPanel.enabled = showBackgroundPanel && visible;
        if (speakerText != null) speakerText.enabled = visible && !string.IsNullOrEmpty(speakerText.text);
    }

    // Public API

    /// <summary>
    /// Load subtitles from an SRT-formatted string. Supports {SPEAKER:Name} and {KEY:localization_key} on the first line, optional.
    /// Closed caption detection: lines like [SFX], (SFX), or <SFX>.
    /// </summary>
    public void LoadSrt(string srtContent)
    {
        timeline.Clear();
        foreach (Match m in srtBlockRegex.Matches(srtContent))
        {
            var entry = new SubtitleEntry
            {
                index = int.Parse(m.Groups[1].Value),
                start = ParseSrtTime(m.Groups[2].Value),
                end = ParseSrtTime(m.Groups[3].Value),
                text = NormalizeText(m.Groups[4].Value, out string speaker, out string key, out bool isCaption),
                speaker = speaker,
                key = key,
                isCaption = isCaption
            };
            timeline.Add(entry);
        }
        timeline.Sort((a, b) => a.start.CompareTo(b.start));
        currentIndex = -1;
        isRunning = true;
        manualClock = 0f;
    }

    /// <summary>
    /// Programmatically queue a single subtitle entry (useful for barks).
    /// </summary>
    public void QueueLine(string text, float startTime, float duration, string speaker = null, string key = null, bool isCaption = false)
    {
        var entry = new SubtitleEntry
        {
            index = timeline.Count + 1,
            start = startTime,
            end = startTime + duration,
            text = text,
            speaker = speaker,
            key = key,
            isCaption = isCaption
        };
        timeline.Add(entry);
        timeline.Sort((a, b) => a.start.CompareTo(b.start));
        isRunning = true;
    }

    /// <summary>
    /// Bind to an AudioSource and begin playback with subtitle sync.
    /// </summary>
    public void PlayWithAudio(AudioClip clip, bool loop = false)
    {
        if (audioSource == null)
        {
            if (logWarnings) Debug.LogWarning("SubtitleSystem: No AudioSource assigned.");
            return;
        }
        audioSource.clip = clip;
        audioSource.loop = loop;
        audioSource.time = 0f;
        audioSource.Play();
        isRunning = true;
    }

    /// <summary>
    /// Stop subtitles (and audio if syncing).
    /// </summary>
    public void Stop()
    {
        isRunning = false;
        if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
        ClearSubtitle();
    }

    // Helpers

    private static double ParseSrtTime(string t)
    {
        // Format: HH:MM:SS,mmm
        // Convert to seconds
        var parts = t.Split(':', ',');
        int hh = int.Parse(parts[0]);
        int mm = int.Parse(parts[1]);
        int ss = int.Parse(parts[2]);
        int ms = int.Parse(parts[3]);
        return hh * 3600 + mm * 60 + ss + ms / 1000.0;
    }

    private static string NormalizeText(string raw, out string speaker, out string key, out bool isCaption)
    {
        // Extract optional metadata on first line:
        // {SPEAKER:Name} and/or {KEY:loc_key}
        speaker = null;
        key = null;
        isCaption = false;

        var lines = raw.Replace("\r", "").Split('\n');
        if (lines.Length > 0)
        {
            string first = lines[0].Trim();
            // Metadata tags
            var metaMatches = Regex.Matches(first, @"\{(\w+):\s*([^\}]+)\}");
            foreach (Match mm in metaMatches)
            {
                string tag = mm.Groups[1].Value.ToUpperInvariant();
                string val = mm.Groups[2].Value.Trim();
                if (tag == "SPEAKER") speaker = val;
                else if (tag == "KEY") key = val;
            }
            // Remove metadata line if it was only metadata
            if (metaMatches.Count > 0 && first == BuildMetaString(metaMatches))
            {
                lines = lines.Length > 1 ? lines[1..] : Array.Empty<string>();
            }
        }

        string text = string.Join("\n", lines).Trim();

        // Closed caption detection
        if (Regex.IsMatch(text, @"^\s*(

\[[^\]

]+\]

|\([^\)]+\)|<[^>]+>)\s*$"))
            isCaption = true;

        return text;
    }

    private static string BuildMetaString(MatchCollection metaMatches)
    {
        // Recompose to compare if the first line is entirely metadata
        List<string> parts = new List<string>();
        foreach (Match mm in metaMatches)
            parts.Add($"{{{mm.Groups[1].Value}:{mm.Groups[2].Value}}}");
        return string.Join(" ", parts);
    }

    private void LoadLocalizationMap()
    {
        localizationMap = null;
        if (localizationJson == null) return;
        try
        {
            // Minimal JSON parser for key/value pairs: {"key":"value", ...}
            localizationMap = new Dictionary<string, string>();
            var text = localizationJson.text.Trim();
            text = Regex.Replace(text, @"^\s*\{|\}\s*$", "");
            var kvs = Regex.Matches(text, @"""([^""]+)""\s*:\s*""([^""]*)""");
            foreach (Match m in kvs)
            {
                localizationMap[m.Groups[1].Value] = m.Groups[2].Value.Replace("\\n", "\n");
            }
        }
        catch (Exception e)
        {
            if (logWarnings) Debug.LogWarning($"SubtitleSystem: Failed to parse localization JSON: {e.Message}");
        }
    }
}
