using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    private enum PauseSelection
    {
        Continue = 0,
        Restart = 1,
        Quit = 2
    }

    private const string PauseCanvasName = "(Pause) Canvas";
    private const string PauseMenuRootName = "Image";
    private const string ContinueName = "Continue";
    private const string RestartName = "Restart";
    private const string QuitName = "Quit";
    private const string SelectionHighlightName = "Selection Highlight";
    private const string SelectionArrowLeftName = "Selection Arrow Left";
    private const string SelectionArrowRightName = "Selection Arrow Right";

    [Header("Input")]
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
    [SerializeField] private KeyCode pauseControllerButton = KeyCode.JoystickButton7;
    [SerializeField] private KeyCode confirmKey = KeyCode.Return;
    [SerializeField] private KeyCode confirmControllerButton = KeyCode.JoystickButton0;
    [SerializeField] private string verticalAxis = "Vertical";
    [SerializeField, Min(0.01f)] private float navigationResetThreshold = 0.25f;
    [SerializeField, Range(0.1f, 1f)] private float navigationTriggerThreshold = 0.5f;

    [Header("Scenes")]
    [SerializeField] private string titleSceneName = "Main Menu";

    private Canvas pauseCanvas;
    private RectTransform pauseMenuRoot;
    private CanvasGroup pauseMenuGroup;

    private RectTransform continueOption;
    private RectTransform restartOption;
    private RectTransform quitOption;

    private RectTransform selectionHighlight;
    private RectTransform selectionArrowLeft;
    private RectTransform selectionArrowRight;

    private Button continueButton;
    private Button restartButton;
    private Button quitButton;

    private PauseSelection currentSelection = PauseSelection.Continue;

    private bool paused;
    private bool initialized;
    private bool navigationReady = true;
    private bool sceneTransitioning;
    private bool buttonListenersConfigured;

    private float previousTimeScale = 1f;

    public bool IsPaused => paused;

    private void Awake()
    {
        Initialize();
    }

    private void Update()
    {
        if (!initialized || sceneTransitioning)
        {
            return;
        }

        if (PausePressed())
        {
            TogglePause();
            return;
        }

        if (!paused)
        {
            return;
        }

        UpdateNavigation();

        if (ConfirmPressed())
        {
            ConfirmSelection();
        }
    }

    private void OnDisable()
    {
        if (paused)
        {
            paused = false;
            RestoreGlobalState();
        }
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();

        if (paused)
        {
            RestoreGlobalState();
        }
    }

    private void OnValidate()
    {
        navigationResetThreshold =
            Mathf.Max(0.01f, navigationResetThreshold);

        navigationTriggerThreshold =
            Mathf.Clamp(navigationTriggerThreshold, 0.1f, 1f);

        if (navigationResetThreshold > navigationTriggerThreshold)
        {
            navigationResetThreshold = navigationTriggerThreshold;
        }
    }

    private void Initialize()
    {
        if (!RefreshReferences())
        {
            enabled = false;
            return;
        }

        ConfigureButtonListeners();

        currentSelection = PauseSelection.Continue;
        navigationReady = true;
        sceneTransitioning = false;
        paused = false;
        previousTimeScale = GetSafeTimeScale();

        SetMenuVisible(false);

        initialized = true;
    }

    private bool RefreshReferences()
    {
        pauseCanvas = FindPauseCanvas();

        if (pauseCanvas == null)
        {
            Debug.LogError(
                $"{nameof(PauseMenu)} could not find the pause Canvas.",
                this);

            return false;
        }

        Transform searchRoot = pauseCanvas.transform;

        pauseMenuRoot =
            FindRectTransform(
                searchRoot,
                PauseMenuRootName);

        if (pauseMenuRoot == null)
        {
            Debug.LogError(
                $"{nameof(PauseMenu)} could not find '{PauseMenuRootName}' under '{pauseCanvas.name}'.",
                this);

            return false;
        }

        pauseMenuGroup =
            pauseMenuRoot.GetComponent<CanvasGroup>();

        if (pauseMenuGroup == null)
        {
            pauseMenuGroup =
                pauseMenuRoot.gameObject.AddComponent<CanvasGroup>();
        }

        continueOption =
            FindRectTransform(
                pauseMenuRoot,
                ContinueName);

        restartOption =
            FindRectTransform(
                pauseMenuRoot,
                RestartName);

        quitOption =
            FindRectTransform(
                pauseMenuRoot,
                QuitName);

        selectionHighlight =
            FindRectTransform(
                pauseMenuRoot,
                SelectionHighlightName);

        selectionArrowLeft =
            FindRectTransform(
                pauseMenuRoot,
                SelectionArrowLeftName);

        selectionArrowRight =
            FindRectTransform(
                pauseMenuRoot,
                SelectionArrowRightName);

        if (continueOption == null ||
            restartOption == null ||
            quitOption == null)
        {
            Debug.LogError(
                $"{nameof(PauseMenu)} could not automatically find Continue, Restart, and Quit.",
                this);

            return false;
        }

        continueButton = GetButton(continueOption);
        restartButton = GetButton(restartOption);
        quitButton = GetButton(quitOption);

        return true;
    }

    private Canvas FindPauseCanvas()
    {
        Canvas localCanvas = GetComponent<Canvas>();

        if (localCanvas != null)
        {
            return localCanvas;
        }

        localCanvas = GetComponentInParent<Canvas>(true);

        if (localCanvas != null)
        {
            return localCanvas;
        }

        Canvas[] canvases =
            FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        Scene ownScene = gameObject.scene;
        Canvas fallback = null;

        foreach (Canvas canvas in canvases)
        {
            if (canvas == null ||
                !canvas.gameObject.scene.IsValid() ||
                canvas.gameObject.scene != ownScene)
            {
                continue;
            }

            if (canvas.name.Equals(
                PauseCanvasName,
                StringComparison.OrdinalIgnoreCase))
            {
                return canvas;
            }

            fallback ??= canvas;
        }

        return fallback;
    }

    private void ConfigureButtonListeners()
    {
        RemoveButtonListeners();

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(ResumeGame);
        }

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartStage);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(QuitToTitle);
        }

        buttonListenersConfigured = true;
    }

    private void RemoveButtonListeners()
    {
        if (!buttonListenersConfigured)
        {
            return;
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(ResumeGame);
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartStage);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitToTitle);
        }

        buttonListenersConfigured = false;
    }

    public void TogglePause()
    {
        if (!initialized || sceneTransitioning)
        {
            return;
        }

        if (paused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        if (!initialized || paused || sceneTransitioning)
        {
            return;
        }

        if (!ReferencesAreValid() && !RefreshReferences())
        {
            return;
        }

        ConfigureButtonListeners();

        previousTimeScale = GetSafeTimeScale();

        paused = true;
        navigationReady = true;
        currentSelection = PauseSelection.Continue;

        SetMenuVisible(true);

        Time.timeScale = 0f;
        AudioListener.pause = true;

        UpdateSelectionVisuals();
    }

    public void ResumeGame()
    {
        if (!paused || sceneTransitioning)
        {
            return;
        }

        paused = false;

        SetMenuVisible(false);

        AudioListener.pause = false;
        Time.timeScale =
            previousTimeScale > 0f
                ? previousTimeScale
                : 1f;
    }

    private void SetMenuVisible(bool visible)
    {
        if (pauseMenuRoot == null ||
            pauseMenuGroup == null)
        {
            return;
        }

        if (!pauseMenuRoot.gameObject.activeSelf)
        {
            pauseMenuRoot.gameObject.SetActive(true);
        }

        pauseMenuGroup.alpha = visible ? 1f : 0f;
        pauseMenuGroup.interactable = visible;
        pauseMenuGroup.blocksRaycasts = visible;

        SetButtonInteractable(continueButton, visible);
        SetButtonInteractable(restartButton, visible);
        SetButtonInteractable(quitButton, visible);
    }

    private static void SetButtonInteractable(
        Button button,
        bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private void UpdateNavigation()
    {
        float vertical = ReadVerticalInput();

        if (Mathf.Abs(vertical) <= navigationResetThreshold)
        {
            navigationReady = true;
            return;
        }

        if (!navigationReady ||
            Mathf.Abs(vertical) < navigationTriggerThreshold)
        {
            return;
        }

        navigationReady = false;

        MoveSelection(
            vertical > 0f
                ? -1
                : 1);
    }

    private float ReadVerticalInput()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) ||
            Input.GetKeyDown(KeyCode.W))
        {
            return 1f;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow) ||
            Input.GetKeyDown(KeyCode.S))
        {
            return -1f;
        }

        if (string.IsNullOrWhiteSpace(verticalAxis))
        {
            return 0f;
        }

        try
        {
            return Input.GetAxisRaw(verticalAxis);
        }
        catch (ArgumentException)
        {
            return 0f;
        }
    }

    private void MoveSelection(int direction)
    {
        const int selectionCount = 3;

        int index =
            ((int)currentSelection + direction) %
            selectionCount;

        if (index < 0)
        {
            index += selectionCount;
        }

        currentSelection =
            (PauseSelection)index;

        UpdateSelectionVisuals();
    }

    private void ConfirmSelection()
    {
        switch (currentSelection)
        {
            case PauseSelection.Continue:
                ResumeGame();
                break;

            case PauseSelection.Restart:
                RestartStage();
                break;

            case PauseSelection.Quit:
                QuitToTitle();
                break;
        }
    }

    private void UpdateSelectionVisuals()
    {
        RectTransform target = GetSelectedOption();

        if (target == null)
        {
            return;
        }

        float targetY = target.anchoredPosition.y;

        MoveSelectionVisual(selectionHighlight, targetY);
        MoveSelectionVisual(selectionArrowLeft, targetY);
        MoveSelectionVisual(selectionArrowRight, targetY);
    }

    private RectTransform GetSelectedOption()
    {
        return currentSelection switch
        {
            PauseSelection.Continue => continueOption,
            PauseSelection.Restart => restartOption,
            PauseSelection.Quit => quitOption,
            _ => null
        };
    }

    private static void MoveSelectionVisual(
        RectTransform visual,
        float targetY)
    {
        if (visual == null)
        {
            return;
        }

        Vector2 position = visual.anchoredPosition;
        position.y = targetY;
        visual.anchoredPosition = position;
    }

    private bool PausePressed()
    {
        return
            Input.GetKeyDown(pauseKey) ||
            Input.GetKeyDown(pauseControllerButton);
    }

    private bool ConfirmPressed()
    {
        return
            Input.GetKeyDown(confirmKey) ||
            Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(confirmControllerButton);
    }

    public void RestartStage()
    {
        if (!initialized || sceneTransitioning)
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() ||
            string.IsNullOrWhiteSpace(scene.name))
        {
            return;
        }

        PrepareForSceneChange();
        SceneManager.LoadScene(scene.name);
    }

    public void QuitToTitle()
    {
        if (!initialized ||
            sceneTransitioning ||
            string.IsNullOrWhiteSpace(titleSceneName))
        {
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(titleSceneName))
        {
            Debug.LogError(
                $"{nameof(PauseMenu)} cannot load scene '{titleSceneName}'. Add it to the active Build Profile scene list.",
                this);

            return;
        }

        PrepareForSceneChange();
        SceneManager.LoadScene(titleSceneName);
    }

    private void PrepareForSceneChange()
    {
        sceneTransitioning = true;
        paused = false;

        SetMenuVisible(false);
        RestoreGlobalState();
    }

    private bool ReferencesAreValid()
    {
        return
            pauseCanvas != null &&
            pauseMenuRoot != null &&
            pauseMenuGroup != null &&
            continueOption != null &&
            restartOption != null &&
            quitOption != null;
    }

    private static float GetSafeTimeScale()
    {
        float timeScale = Time.timeScale;

        if (!float.IsFinite(timeScale) ||
            timeScale <= 0f)
        {
            return 1f;
        }

        return timeScale;
    }

    private static void RestoreGlobalState()
    {
        AudioListener.pause = false;
        Time.timeScale = 1f;
    }

    private static RectTransform FindRectTransform(
        Transform root,
        string objectName)
    {
        if (root == null ||
            string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        RectTransform[] rectTransforms =
            root.GetComponentsInChildren<RectTransform>(true);

        foreach (RectTransform rectTransform in rectTransforms)
        {
            if (rectTransform == null)
            {
                continue;
            }

            if (rectTransform.name.Equals(
                objectName,
                StringComparison.OrdinalIgnoreCase))
            {
                return rectTransform;
            }
        }

        return null;
    }

    private static Button GetButton(
        RectTransform option)
    {
        if (option == null)
        {
            return null;
        }

        return option.GetComponent<Button>();
    }
}
