using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingScreenHUD : MonoBehaviour
{
    public static LoadingScreenHUD Instance { get; private set; }
    public float LoadingProgress { get; private set; }

    [Header("Loading Areas")]
    [SerializeField] private TMP_Text stageNumberText;
    [SerializeField] private TMP_Text stageNameText;
    [SerializeField] private TMP_Text missionLabelText;
    [SerializeField] private TMP_Text missionText;
    [SerializeField] private TMP_Text loadingTipText;

    [Header("Loading Images")]
    [SerializeField] private Image stageArtworkImage;
    [SerializeField] private Image backgroundArtworkImage;
    [SerializeField] private Image teamLogoImage;
    [SerializeField] private Image accentImage;

    [Header("Audio")]
    [SerializeField] private AudioSource loadingMusicSource;

    [Header("Canvas")]
    [SerializeField] private CanvasGroup loadingScreenCanvas;

    [Header("Transition")]
    [SerializeField, Min(0f)] private float fadeDuration = 0.25f;
    [SerializeField, Min(0f)] private float minimumDisplayTime = 0.2f;

    private bool isLoading;

    public bool IsLoading => isLoading;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (loadingScreenCanvas == null)
            loadingScreenCanvas = GetComponentInChildren<CanvasGroup>(true);

        if (loadingScreenCanvas == null)
        {
            Debug.LogError("LoadingScreenHUD requires a CanvasGroup.", this);
            enabled = false;
            return;
        }

        if (loadingMusicSource == null)
            Debug.LogWarning("Loading Music Source is not assigned.", this);

        if (stageArtworkImage == null)
            Debug.LogWarning("Stage Artwork Image is not assigned.", this);

        if (backgroundArtworkImage == null)
            Debug.LogWarning("Background Artwork Image is not assigned.", this);

        if (teamLogoImage == null)
            Debug.LogWarning("Team Logo Image is not assigned.", this);

        if (loadingTipText == null)
            Debug.LogWarning("Loading Tip Text is not assigned.", this);

        SetVisible(false);
        ValidateReferences();
    }

    public void SetStageInfo(
        string stageNumber,
        string stageName,
        string missionLabel,
        string missionText)
    {
        if (stageNumberText != null)
            stageNumberText.text = stageNumber ?? string.Empty;

        if (stageNameText != null)
            stageNameText.text = stageName ?? string.Empty;

        if (missionLabelText != null)
            missionLabelText.text = missionLabel ?? string.Empty;

        if (this.missionText != null)
            this.missionText.text = missionText ?? string.Empty;
    }

    public void SetLoadingAssets(
        Sprite stageArtwork,
        Sprite backgroundArtwork,
        Sprite teamLogo,
        AudioClip loadingMusic,
        float loadingMusicVolume,
        Color accentColor,
        string loadingTip)
    {
        SetImage(stageArtworkImage, stageArtwork);
        SetImage(backgroundArtworkImage, backgroundArtwork);
        SetImage(teamLogoImage, teamLogo);

        if (accentImage != null)
            accentImage.color = accentColor;

        if (loadingTipText != null)
            loadingTipText.text = loadingTip ?? string.Empty;

        SetLoadingMusic(loadingMusic, loadingMusicVolume);
    }

    public void LoadScene(string sceneName)
    {
        if (isLoading)
            return;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("A valid scene name is required.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"Scene '{sceneName}' is not available in the build.",
                this);

            return;
        }

        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    public void LoadScene(int buildIndex)
    {
        if (isLoading)
            return;

        if (!Application.CanStreamedLevelBeLoaded(buildIndex))
        {
            Debug.LogError(
                $"Scene build index {buildIndex} is not available.",
                this);

            return;
        }

        StartCoroutine(LoadSceneRoutine(buildIndex));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        isLoading = true;
        yield return FadeTo(1f);

        float shownAt = Time.unscaledTime;
        AsyncOperation operation =
            SceneManager.LoadSceneAsync(sceneName);

        if (operation == null)
        {
            yield return HandleLoadingFailure();
            yield break;
        }

        LoadingProgress = 0f;

        while (!operation.isDone)
        {
            LoadingProgress = operation.progress;
            yield return null;
        }

        LoadingProgress = 1f;
        yield return CompleteLoading(shownAt);
    }

    private IEnumerator LoadSceneRoutine(int buildIndex)
    {
        isLoading = true;
        LoadingProgress = 0f;

        yield return FadeTo(1f);

        float shownAt = Time.unscaledTime;

        AsyncOperation operation =
            SceneManager.LoadSceneAsync(buildIndex);

        if (operation == null)
        {
            yield return HandleLoadingFailure();
            yield break;
        }

        LoadingProgress = 0f;

        while (!operation.isDone)
        {
            LoadingProgress = operation.progress;
            yield return null;
        }

        LoadingProgress = 1f;

        yield return CompleteLoading(shownAt);
    }

    private IEnumerator CompleteLoading(float shownAt)
    {
        yield return WaitForMinimumDisplayTime(shownAt);
        yield return FadeTo(0f);

        ClearStageInfo();
        ClearLoadingAssets();

        isLoading = false;
        LoadingProgress = 0f;
    }

    private IEnumerator HandleLoadingFailure()
    {
        Debug.LogError("Failed to start loading the scene.", this);

        yield return FadeTo(0f);

        ClearStageInfo();
        ClearLoadingAssets();

        isLoading = false;
    }

    private IEnumerator WaitForMinimumDisplayTime(float shownAt)
    {
        float remainingTime =
            minimumDisplayTime -
            (Time.unscaledTime - shownAt);

        if (remainingTime > 0f)
            yield return new WaitForSecondsRealtime(remainingTime);
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        float startAlpha = loadingScreenCanvas.alpha;
        bool becomingVisible = targetAlpha > startAlpha;

        if (becomingVisible)
            UpdateInteraction(true);

        if (fadeDuration <= 0f)
        {
            loadingScreenCanvas.alpha = targetAlpha;
            UpdateInteraction(targetAlpha > 0f);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / fadeDuration);

            loadingScreenCanvas.alpha =
                Mathf.Lerp(startAlpha, targetAlpha, progress);

            yield return null;
        }

        loadingScreenCanvas.alpha = targetAlpha;
        UpdateInteraction(targetAlpha > 0f);
    }

    private void SetLoadingMusic(AudioClip loadingMusic, float volume)
    {
        if (loadingMusicSource == null)
            return;

        loadingMusicSource.Stop();
        loadingMusicSource.clip = loadingMusic;
        loadingMusicSource.volume = Mathf.Clamp01(volume);
        loadingMusicSource.loop = true;

        if (loadingMusic != null)
            loadingMusicSource.Play();
    }

    private void SetImage(Image image, Sprite sprite)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private void ClearStageInfo()
    {
        if (stageNumberText != null)
            stageNumberText.text = string.Empty;

        if (stageNameText != null)
            stageNameText.text = string.Empty;

        if (missionLabelText != null)
            missionLabelText.text = string.Empty;

        if (missionText != null)
            missionText.text = string.Empty;
    }

    private void ClearLoadingAssets()
    {
        SetImage(stageArtworkImage, null);
        SetImage(backgroundArtworkImage, null);
        SetImage(teamLogoImage, null);

        if (accentImage != null)
        {
            accentImage.color = Color.white;
            accentImage.sprite = null;
        }

        if (loadingTipText != null)
            loadingTipText.text = string.Empty;

        if (loadingMusicSource != null)
        {
            loadingMusicSource.Stop();
            loadingMusicSource.clip = null;
            loadingMusicSource.volume = 1f;
        }
    }

    private void SetVisible(bool visible)
    {
        loadingScreenCanvas.alpha = visible ? 1f : 0f;
        UpdateInteraction(visible);
    }

    private void UpdateInteraction(bool visible)
    {
        loadingScreenCanvas.blocksRaycasts = visible;
        loadingScreenCanvas.interactable = visible;
    }

    private void ValidateReferences()
    {
        if (stageNumberText == null)
            Debug.LogWarning("Stage Number Text is not assigned.", this);

        if (stageNameText == null)
            Debug.LogWarning("Stage Name Text is not assigned.", this);

        if (missionLabelText == null)
            Debug.LogWarning("Mission Label Text is not assigned.", this);

        if (missionText == null)
            Debug.LogWarning("Mission Text is not assigned.", this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        fadeDuration = Mathf.Max(0f, fadeDuration);
        minimumDisplayTime = Mathf.Max(0f, minimumDisplayTime);
    }
}