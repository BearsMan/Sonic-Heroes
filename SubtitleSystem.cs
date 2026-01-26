// SubtitleSystem.cs
// Drop on a UI GameObject with a TextMeshProUGUI and optional background Panel.
// Requires: TextMeshPro package.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SubtitleSystem : MonoBehaviour
{
    // Public fields
    [Header("UI Components")]
    public TextMeshProUGUI subtitleText;
    public Image backgroundPanel;

    [Header("Subtitle Settings")]
    public float fadeInDuration = 0.5f;
    public float fadeOutDuration = 0.5f;
    public float displayDuration = 3.0f;
    public Vector2 padding = new Vector2(10, 10);
    public bool autoSizeBackground = true;
    public Color backgroundColor = new Color(0, 0, 0, 0.5f);
    public Color textColor = new();
    public int fontSize = 24;
    public TMP_FontAsset fontAsset;
    public TextAlignmentOptions textAlignment = TextAlignmentOptions.Center;
    public bool enableWordWrapping = true;
    public float maxWidthPercentage = 0.8f; // Max width as a percentage of screen width
    public float maxHeightPercentage = 0.3f; // Max height as a percentage of screen height
    public List<string> prohibitedWords = new() { "badword1", "badword2" };
    public string censorReplacement = "***";
    public bool enableCensorship = true;

    // Private fields
    private CanvasGroup canvasGroup;
    private Coroutine currentCoroutine;
    private RectTransform backgroundRect;
    private RectTransform textRect;
    private static readonly Regex wordBoundaryRegex = new Regex(@"\b", RegexOptions.Compiled);
    private int screenWidth = 0;
    private int screenHeight = 0;
    private int screenWidthPixels = 0;
    private int screenHeightPixels = 0;

    private void Awake()
    {
        // Initialize components
        if (subtitleText == null)
        {
            Debug.LogError("SubtitleSystem requires a TextMeshProUGUI component.");
            enabled = false;
            return;
        }
        canvasGroup = subtitleText.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = subtitleText.gameObject.AddComponent<CanvasGroup>();
        }
        if (backgroundPanel != null)
        {
            backgroundRect = backgroundPanel.GetComponent<RectTransform>();
            backgroundPanel.color = backgroundColor;
        }
        textRect = subtitleText.GetComponent<RectTransform>();
        // Apply initial settings
        ApplyTextSettings();
        UpdateScreenDimensions();
    }

    private void Update()
    {
        // Check for screen size changes
        if (Screen.width != screenWidthPixels || Screen.height != screenHeightPixels)
        {
            UpdateScreenDimensions();
            if (autoSizeBackground)
            {
                ResizeBackground();
            }
        }
    }

    private void UpdateScreenDimensions()
    {
        screenWidthPixels = Screen.width;
        screenHeightPixels = Screen.height;
        screenWidth = (int)(screenWidthPixels * maxWidthPercentage);
        screenHeight = (int)(screenHeightPixels * maxHeightPercentage);
    }

    private void ApplyTextSettings()
    {
        subtitleText.fontSize = fontSize;
        subtitleText.color = textColor;
        subtitleText.alignment = textAlignment;
        subtitleText.textWrappingMode = enableWordWrapping ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        if (fontAsset != null)
        {
            subtitleText.font = fontAsset;
        }
    }

    private void ResizeBackground()
    {
        if (backgroundRect == null) return;
        Vector2 textSize = subtitleText.GetPreferredValues(subtitleText.text, screenWidth, screenHeight);
        backgroundRect.sizeDelta = textSize + padding * 2;
    }

    public void ShowSubtitle(string message)
    {
        if (enableCensorship)
        {
            message = CensorMessage(message);
        }
        subtitleText.text = message;
        ApplyTextSettings();
        if (autoSizeBackground)
        {
            ResizeBackground();
        }
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }
        currentCoroutine = StartCoroutine(SubtitleRoutine());
    }

    private string CensorMessage(string message)
    {
        foreach (var word in prohibitedWords)
        {
            var pattern = $@"\b{Regex.Escape(word)}\b";
            message = Regex.Replace(message, pattern, censorReplacement, RegexOptions.IgnoreCase);
        }
        return message;
    }

    private System.Collections.IEnumerator SubtitleRoutine()
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
        // Display duration
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
    }
}
