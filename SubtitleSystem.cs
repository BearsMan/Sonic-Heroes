// SubtitleSystem.cs
// Drop on a UI GameObject with a TextMeshProUGUI and optional background Panel.
// Requires: TextMeshPro package.
//
// Sonic Heroes subtitle system — supports all 4 teams:
//   Team Sonic  : Sonic, Tails, Knuckles
//   Team Dark   : Shadow, Rouge, Omega
//   Team Rose   : Amy, Cream, Big
//   Team Chaotix: Espio, Charmy, Vector

using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────
//  Data
// ─────────────────────────────────────────────

public enum SonicHeroesTeam { Sonic, Dark, Rose, Chaotix }

public enum SonicHeroesCharacter
{
    // Team Sonic
    Sonic, Tails, Knuckles,
    // Team Dark
    Shadow, Rouge, Omega,
    // Team Rose
    Amy, Cream, Big,
    // Team Chaotix
    Espio, Charmy, Vector
}

[System.Serializable]
public class CharacterSubtitleEntry
{
    public SonicHeroesCharacter character;
    [TextArea] public string line;
}

// ─────────────────────────────────────────────
//  Component
// ─────────────────────────────────────────────

[DisallowMultipleComponent]
public class SubtitleSystem : MonoBehaviour
{
    // ── UI ──────────────────────────────────────
    [Header("UI Components")]
    public TextMeshProUGUI subtitleText;
    public Image backgroundPanel;

    // ── Timing & Layout ─────────────────────────
    [Header("Subtitle Settings")]
    public float fadeInDuration = 0.3f;
    public float fadeOutDuration = 0.5f;
    public float displayDuration = 3.0f;
    public Vector2 padding = new Vector2(16, 8);
    public bool autoSizeBackground = true;
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.6f);

    // BUG FIX: was `new Color()` which defaults to (0,0,0,0) — fully transparent black.
    public Color textColor = Color.white;

    public int fontSize = 26;
    public TMP_FontAsset fontAsset;
    public TextAlignmentOptions textAlignment = TextAlignmentOptions.Center;
    public bool enableWordWrapping = true;
    [Range(0.1f, 1f)] public float maxWidthPercentage = 0.8f;
    [Range(0.1f, 1f)] public float maxHeightPercentage = 0.3f;

    // ── Censorship ──────────────────────────────
    [Header("Censorship")]
    public List<string> prohibitedWords = new List<string>();
    public string censorReplacement = "***";
    public bool enableCensorship = true;

    // ── Sonic Heroes Lines ──────────────────────
    [Header("Sonic Heroes — Team Voice Lines")]
    public List<CharacterSubtitleEntry> teamSonicLines = new List<CharacterSubtitleEntry>();
    public List<CharacterSubtitleEntry> teamDarkLines = new List<CharacterSubtitleEntry>();
    public List<CharacterSubtitleEntry> teamRoseLines = new List<CharacterSubtitleEntry>();
    public List<CharacterSubtitleEntry> teamChaotixLines = new List<CharacterSubtitleEntry>();

    // ── Private ─────────────────────────────────
    private CanvasGroup canvasGroup;
    private Coroutine currentCoroutine;
    private RectTransform backgroundRect;
    private RectTransform textRect;
    private int screenWidthPixels;
    private int screenHeightPixels;
    private int maxTextWidth;
    private int maxTextHeight;

    // ─────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────

    private void Awake()
    {
        if (subtitleText == null)
        {
            Debug.LogError("SubtitleSystem: subtitleText is not assigned.");
            enabled = false;
            return;
        }

        // CanvasGroup lives on the text GameObject for alpha control.
        canvasGroup = subtitleText.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = subtitleText.gameObject.AddComponent<CanvasGroup>();

        // BUG FIX: alpha must start at 0 so the subtitle is invisible until shown.
        canvasGroup.alpha = 0f;

        if (backgroundPanel != null)
        {
            // BUG FIX: get the RectTransform from backgroundPanel, not from this GameObject.
            backgroundRect = backgroundPanel.GetComponent<RectTransform>();
            backgroundPanel.color = backgroundColor;
        }

        textRect = subtitleText.GetComponent<RectTransform>();

        ApplyTextSettings();
        UpdateScreenDimensions();
        PopulateDefaultLines();
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

    // ─────────────────────────────────────────────
    //  Screen helpers
    // ─────────────────────────────────────────────

    private void UpdateScreenDimensions()
    {
        screenWidthPixels = Screen.width;
        screenHeightPixels = Screen.height;
        // BUG FIX: these are the *max pixel dimensions* for text layout, not a second
        // copy of the raw screen size. Renamed to avoid the previous naming confusion.
        maxTextWidth = Mathf.RoundToInt(screenWidthPixels * maxWidthPercentage);
        maxTextHeight = Mathf.RoundToInt(screenHeightPixels * maxHeightPercentage);
    }

    // ─────────────────────────────────────────────
    //  Text / Background
    // ─────────────────────────────────────────────

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
    }

    private void ResizeBackground()
    {
        // BUG FIX: backgroundRect can be null when backgroundPanel is not assigned;
        // guard before touching it. Never add a RectTransform to this root GameObject
        // as a fallback — that was wrong.
        if (backgroundRect == null) return;

        Vector2 textSize = subtitleText.GetPreferredValues(
            subtitleText.text, maxTextWidth, maxTextHeight);

        backgroundRect.sizeDelta = textSize + padding * 2f;
    }

    // ─────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────

    /// <summary>Show an arbitrary subtitle string.</summary>
    public void ShowSubtitle(string message)
    {
        if (enableCensorship)
            message = CensorMessage(message);

        subtitleText.text = message;
        ApplyTextSettings();

        if (autoSizeBackground)
            ResizeBackground();

        if (currentCoroutine != null)
            StopCoroutine(currentCoroutine);

        currentCoroutine = StartCoroutine(SubtitleRoutine());
    }

    /// <summary>Show a subtitle for a specific Sonic Heroes character.</summary>
    public void ShowCharacterSubtitle(SonicHeroesCharacter character, string line)
    {
        // Optionally prefix with the character name in the style of the game's HUD.
        ShowSubtitle($"[{character}]\n{line}");
    }

    /// <summary>
    /// Play a random voice line for the given character from the pre-filled lists.
    /// </summary>
    public void ShowRandomLine(SonicHeroesCharacter character)
    {
        var pool = GetLinesForCharacter(character);
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning($"SubtitleSystem: No lines registered for {character}.");
            return;
        }
        ShowCharacterSubtitle(character, pool[Random.Range(0, pool.Count)]);
    }

    /// <summary>Play a random line from any character on the given team.</summary>
    public void ShowRandomTeamLine(SonicHeroesTeam team)
    {
        var teamList = GetTeamList(team);
        if (teamList == null || teamList.Count == 0) return;

        var entry = teamList[Random.Range(0, teamList.Count)];
        ShowCharacterSubtitle(entry.character, entry.line);
    }

    // ─────────────────────────────────────────────
    //  Censorship
    // ─────────────────────────────────────────────

    private string CensorMessage(string message)
    {
        foreach (var word in prohibitedWords)
        {
            if (string.IsNullOrEmpty(word)) continue;
            var pattern = $@"\b{Regex.Escape(word)}\b";
            message = Regex.Replace(message, pattern, censorReplacement,
                RegexOptions.IgnoreCase);
        }
        return message;
    }

    // ─────────────────────────────────────────────
    //  Coroutine
    // ─────────────────────────────────────────────

    private IEnumerator SubtitleRoutine()
    {
        // Fade in
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // Hold
        yield return new WaitForSeconds(displayDuration);

        // Fade out
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
        currentCoroutine = null;
    }

    // ─────────────────────────────────────────────
    //  Sonic Heroes default lines
    // ─────────────────────────────────────────────

    /// <summary>
    /// Fills all four team lists with authentic Sonic Heroes in-game quotes.
    /// Only adds entries if the lists are empty so designer-authored entries
    /// in the Inspector take precedence.
    /// </summary>
    private void PopulateDefaultLines()
    {
        // ── Team Sonic ───────────────────────────────────────────────────────────
        if (teamSonicLines.Count == 0)
        {
            AddLines(teamSonicLines, SonicHeroesCharacter.Sonic, new[]
            {
                "We can do this! All right!",
                "Piece of cake!",
                "Yeah! That's the way to do it!",
                "All right, let's do this!",
                "Tails, Knuckles — follow me!",
                "We're the fastest team around!",
                "No time to waste. Let's go!",
                "Gotta go fast!",
                "This is like taking candy from a baby!",
                "Now we're cooking!"
            });
            AddLines(teamSonicLines, SonicHeroesCharacter.Tails, new[]
            {
                "Roger that, Sonic!",
                "I've got your back!",
                "Leave it to me!",
                "I'm all fired up!",
                "My tails are spinning — watch out!",
                "Hang on, I'm calculating a flight path!",
                "Yahoo! Full throttle!",
                "Don't worry, I won't let you down!",
                "Here I come!",
                "Two tails are better than one!"
            });
            AddLines(teamSonicLines, SonicHeroesCharacter.Knuckles, new[]
            {
                "Leave it to me!",
                "Nobody's tougher than me!",
                "Bring it on!",
                "My fists are the best weapons!",
                "You mess with Team Sonic, you mess with me!",
                "I'm pumped and ready to go!",
                "That all you got?",
                "Don't push your luck!",
                "I'll crush anything that gets in my way!",
                "Hmph. Too easy."
            });
        }

        // ── Team Dark ────────────────────────────────────────────────────────────
        if (teamDarkLines.Count == 0)
        {
            AddLines(teamDarkLines, SonicHeroesCharacter.Shadow, new[]
            {
                "I am the Ultimate Life Form.",
                "Pathetic.",
                "I'll show you the true power of Chaos!",
                "Chaos Control!",
                "Know your place.",
                "This is who I am.",
                "Stay out of my way.",
                "Shadow the Hedgehog — that is who I am.",
                "You're going to have to do better than that.",
                "Hmph. Don't waste my time."
            });
            AddLines(teamDarkLines, SonicHeroesCharacter.Rouge, new[]
            {
                "Leave the jewels to me, darling.",
                "Ha! Too easy.",
                "Shadow, try to keep up.",
                "Don't underestimate me.",
                "I always get what I'm after.",
                "Every jewel in the world will be mine!",
                "Now THIS is my kind of mission.",
                "You really think you can match me?",
                "Better luck next time, sweetheart.",
                "I fight for no one — except myself."
            });
            AddLines(teamDarkLines, SonicHeroesCharacter.Omega, new[]
            {
                "All enemies: eliminated.",
                "Initiating maximum firepower.",
                "Threat level: unacceptable. Engaging.",
                "I am E-123 Omega. Resistance is futile.",
                "Prepare for total annihilation.",
                "My power output: MAXIMUM.",
                "Destruction sequence: initiated.",
                "Inferior models detected. Terminating.",
                "Shadow — your orders?",
                "No unit surpasses E-123 Omega."
            });
        }

        // ── Team Rose ────────────────────────────────────────────────────────────
        if (teamRoseLines.Count == 0)
        {
            AddLines(teamRoseLines, SonicHeroesCharacter.Amy, new[]
            {
                "Sonic, wait for me!",
                "I'll never give up!",
                "My hammer is all I need!",
                "I'm Amy Rose, and I'm fabulous!",
                "Don't think you can just run from me!",
                "I'll find you, Sonic — I promise!",
                "Here I come, ready or not!",
                "No one can stop a girl in love!",
                "Take this! Amy's Hammer!",
                "We won't lose!"
            });
            AddLines(teamRoseLines, SonicHeroesCharacter.Cream, new[]
            {
                "Please don't hurt anyone.",
                "Cheese, let's do our best!",
                "I'll try my hardest!",
                "Chao! Chao!",
                "Amy, I'll help however I can!",
                "I don't want to fight, but I will if I have to.",
                "Cheese and I make a great team!",
                "We can do it together!",
                "I believe in everyone!",
                "Thank you for your help!"
            });
            AddLines(teamRoseLines, SonicHeroesCharacter.Big, new[]
            {
                "Froggy! Where are you?",
                "I'm gonna catch ya, Froggy!",
                "Heh heh, here I come!",
                "Big's gonna help!",
                "Don't worry — Big is here!",
                "Ribbit? Is that you, Froggy?",
                "Wow, that was something!",
                "I like fishing better, but okay.",
                "Let's go, Froggy!",
                "Heh, Big smash!"
            });
        }

        // ── Team Chaotix ─────────────────────────────────────────────────────────
        if (teamChaotixLines.Count == 0)
        {
            AddLines(teamChaotixLines, SonicHeroesCharacter.Vector, new[]
            {
                "We never back down from a job!",
                "The Chaotix Detective Agency is on the case!",
                "Money, money, money — I can almost smell it!",
                "Nobody leaves until I say so!",
                "We ain't taking no for an answer!",
                "Espio, Charmy — let's move it!",
                "A job's a job. Let's get paid!",
                "Leave it to the Chaotix!",
                "I'll crush you with my bare hands!",
                "We're the best detective team around!"
            });
            AddLines(teamChaotixLines, SonicHeroesCharacter.Espio, new[]
            {
                "Silence is a ninja's greatest weapon.",
                "I sense danger nearby.",
                "A true ninja leaves no trace.",
                "I will handle this.",
                "My shuriken never miss.",
                "Stay focused, Vector.",
                "I move like the wind.",
                "The enemy won't see me coming.",
                "Stealth is everything.",
                "Hmm… this requires careful thought."
            });
            AddLines(teamChaotixLines, SonicHeroesCharacter.Charmy, new[]
            {
                "Let's do it! Buzz buzz!",
                "I'm the fastest bee in the world!",
                "Yay! Let's go!",
                "Don't underestimate me 'cause I'm small!",
                "Bzzzz! Here I come!",
                "This is so exciting!",
                "Can we get ice cream after?",
                "I found something! I found something!",
                "Nobody can catch me!",
                "Wheee!"
            });
        }
    }

    // ─────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────

    private static void AddLines(List<CharacterSubtitleEntry> list,
        SonicHeroesCharacter character, string[] lines)
    {
        foreach (var line in lines)
            list.Add(new CharacterSubtitleEntry { character = character, line = line });
    }

    /// <summary>Returns a flat list of line strings for a specific character
    /// searched across all four team lists.</summary>
    private List<string> GetLinesForCharacter(SonicHeroesCharacter character)
    {
        var results = new List<string>();
        foreach (var teamList in new[]
            { teamSonicLines, teamDarkLines, teamRoseLines, teamChaotixLines })
        {
            foreach (var entry in teamList)
                if (entry.character == character)
                    results.Add(entry.line);
        }
        return results;
    }

    private List<CharacterSubtitleEntry> GetTeamList(SonicHeroesTeam team)
    {
        return team switch
        {
            SonicHeroesTeam.Sonic => teamSonicLines,
            SonicHeroesTeam.Dark => teamDarkLines,
            SonicHeroesTeam.Rose => teamRoseLines,
            SonicHeroesTeam.Chaotix => teamChaotixLines,
            _ => null
        };
    }
}