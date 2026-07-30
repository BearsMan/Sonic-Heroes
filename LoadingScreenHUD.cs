using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingScreenHUD : MonoBehaviour
{
    public static LoadingScreenHUD Instance { get; private set; }

    [Header("Canvas")]
    [SerializeField] private CanvasGroup LoadingScreenCanvas;

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

        if (LoadingScreenCanvas == null)
            LoadingScreenCanvas = GetComponentInChildren<CanvasGroup>(true);

        if (LoadingScreenCanvas == null)
        {
            Debug.LogError("LoadingScreenHUD requires a CanvasGroup.", this);
            enabled = false;
            return;
        }

        SetVisible(false);
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
            Debug.LogError($"Scene '{sceneName}' is not available in the build.", this);
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
            Debug.LogError($"Scene build index {buildIndex} is not available.", this);
            return;
        }

        StartCoroutine(LoadSceneRoutine(buildIndex));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        isLoading = true;
        yield return FadeTo(1f);

        float shownAt = Time.unscaledTime;
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        while (!operation.isDone)
            yield return null;

        yield return WaitForMinimumDisplayTime(shownAt);
        yield return FadeTo(0f);

        isLoading = false;
    }

    private IEnumerator LoadSceneRoutine(int buildIndex)
    {
        isLoading = true;
        yield return FadeTo(1f);

        float shownAt = Time.unscaledTime;
        AsyncOperation operation = SceneManager.LoadSceneAsync(buildIndex);

        while (!operation.isDone)
            yield return null;

        yield return WaitForMinimumDisplayTime(shownAt);
        yield return FadeTo(0f);

        isLoading = false;
    }

    private IEnumerator WaitForMinimumDisplayTime(float shownAt)
    {
        float remainingTime =
            minimumDisplayTime - (Time.unscaledTime - shownAt);

        if (remainingTime > 0f)
            yield return new WaitForSecondsRealtime(remainingTime);
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        float startAlpha = LoadingScreenCanvas.alpha;
        bool becomingVisible = targetAlpha > startAlpha;

        if (becomingVisible)
            UpdateInteraction(true);

        if (fadeDuration <= 0f)
        {
            LoadingScreenCanvas.alpha = targetAlpha;
            UpdateInteraction(targetAlpha > 0f);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / fadeDuration);

            LoadingScreenCanvas.alpha =
                Mathf.Lerp(startAlpha, targetAlpha, progress);

            yield return null;
        }

        LoadingScreenCanvas.alpha = targetAlpha;
        UpdateInteraction(targetAlpha > 0f);
    }

    private void SetVisible(bool visible)
    {
        LoadingScreenCanvas.alpha = visible ? 1f : 0f;
        UpdateInteraction(visible);
    }

    private void UpdateInteraction(bool visible)
    {
        LoadingScreenCanvas.blocksRaycasts = visible;
        LoadingScreenCanvas.interactable = visible;
    }

    private void OnValidate()
    {
        fadeDuration = Mathf.Max(0f, fadeDuration);
        minimumDisplayTime = Mathf.Max(0f, minimumDisplayTime);
    }
}