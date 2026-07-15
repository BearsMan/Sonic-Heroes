using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject pauseMenu;

    [Header("Scene Navigation")]
    [SerializeField] private string normalStageReturnScene = "Menu Select";
    [SerializeField] private string teamBattleReturnScene = "2 Players";



    [Header("Settings")]
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
    [SerializeField] private bool pauseAudio = true;
    [SerializeField] private bool pauseDialogs = true;

    private readonly List<AudioSource> pausedSounds = new();

    private bool isPaused;
    private bool isChangingScene;
    private float previousTimeScale = 1f;

    public bool IsPaused => isPaused;

    private void Awake()
    {
        if (pauseMenu == null)
        {
            Debug.LogWarning($"{nameof(PauseMenu)} on '{name}' has no pause menu assigned.", this);
            return;
        }

        if (pauseMenu.activeSelf)
        {
            pauseMenu.SetActive(false);
        }
    }

    private void Start()
    {
        // Protect against entering this scene while another scene left
        // the global time scale at zero.
        if (Time.timeScale <= 0f)
        {
            Time.timeScale = 1f;
        }

        previousTimeScale = Time.timeScale;
    }

    private void Update()
    {
        if (!isChangingScene && Input.GetKeyDown(pauseKey))
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    // Keeps existing Unity button events working if they use SwitchPause.
    public void SwitchPause()
    {
        TogglePause();
    }

    public void PauseGame()
    {
        if (isPaused || isChangingScene)
        {
            return;
        }

        isPaused = true;

        previousTimeScale =
            Time.timeScale > 0f ? Time.timeScale : 1f;

        if (pauseAudio)
        {
            PausePlayingAudio();
        }

        if (pauseDialogs)
        {
            SetDialogsPaused(true);
        }

        if (pauseMenu != null)
        {
            pauseMenu.SetActive(true);
        }

        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        if (!isPaused || isChangingScene)
        {
            return;
        }

        isPaused = false;

        if (pauseAudio)
        {
            ResumePausedAudio();
        }

        if (pauseDialogs)
        {
            SetDialogsPaused(false);
        }

        if (pauseMenu != null && pauseMenu.activeSelf)
        {
            pauseMenu.SetActive(false);
        }

        RestoreTimeScale();
    }

    // Keeps existing Unity button events working if they use UnpauseGame.
    public void UnpauseGame()
    {
        ResumeGame();
    }

    private void PausePlayingAudio()
    {
        pausedSounds.Clear();

        AudioSource[] audioSources = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude);

        foreach (AudioSource source in audioSources)
        {
            if (source == null || !source.isActiveAndEnabled || !source.isPlaying)
            {
                continue;
            }

            source.Pause();
            pausedSounds.Add(source);
        }
    }

    private void ResumePausedAudio()
    {
        foreach (AudioSource source in pausedSounds)
        {
            // The object may have been destroyed while the game was paused.
            if (source != null && source.isActiveAndEnabled)
            {
                source.UnPause();
            }
        }

        pausedSounds.Clear();
    }

    private void SetDialogsPaused(bool paused)
    {
        TriggerDialog[] dialogs = FindObjectsByType<TriggerDialog>(FindObjectsInactive.Exclude);

        foreach (TriggerDialog dialog in dialogs)
        {
            if (dialog != null)
            {
                dialog.pause = paused;
            }
        }
    }

    public void RestartLevel()
    {
        if (isChangingScene)
        {
            return;
        }

        Scene gameplayScene = GetGameplayScene();

        if (!gameplayScene.IsValid() || gameplayScene.buildIndex < 0)
        {
            Debug.LogError("The gameplay scene could not be restarted.", this);
            return;
        }

        isChangingScene = true;
        PrepareForSceneChange();

        // This is only correct if loading this scene normally reconstructs
        // HUD, Event, and all required additive scenes.
        SceneManager.LoadScene(gameplayScene.buildIndex, LoadSceneMode.Single);
    }

    // Keeps an existing button wired to the old capitalization working.
    public void Restartlevel()
    {
        RestartLevel();
    }

    public void QuitGame()
    {
        if (isChangingScene)
        {
            return;
        }

        Scene gameplayScene = GetGameplayScene();

        if (!gameplayScene.IsValid())
        {
            Debug.LogError("Could not identify the current gameplay scene.", this);
            return;
        }

        bool isTeamBattle = IsTeamBattleScene(gameplayScene.name);

        string destination = isTeamBattle ? teamBattleReturnScene : normalStageReturnScene;

        if (string.IsNullOrWhiteSpace(destination))
        {
            Debug.LogError("No return scene has been assigned.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(destination))
        {
            Debug.LogError($"Return scene '{destination}' cannot be loaded. " + "Check its exact name and include it in the Build Profile.", this);
            return;
        }

        isChangingScene = true;
        PrepareForSceneChange();

        SceneManager.LoadScene(destination, LoadSceneMode.Single);
    }

    private Scene GetGameplayScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (IsGameplayScene(activeScene))
        {
            return activeScene;
        }

        // Search backward because the gameplay scene is often loaded after
        // shared/bootstrap scenes.
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (IsGameplayScene(scene))
            {
                return scene;
            }
        }

        return default;
    }

    private static bool IsTeamBattleScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        return sceneName.StartsWith("Team ", System.StringComparison.OrdinalIgnoreCase) || sceneName.StartsWith("2P ", System.StringComparison.OrdinalIgnoreCase);
    }
    private static bool IsGameplayScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return false;
        }

        string sceneName = scene.name;

            return  
            sceneName.StartsWith("Stage ", System.StringComparison.OrdinalIgnoreCase) || sceneName.StartsWith("Team ", System.StringComparison.OrdinalIgnoreCase) || sceneName.StartsWith("2P ", System.StringComparison.OrdinalIgnoreCase);
    }

    private void PrepareForSceneChange()
    {
        isPaused = false;

        ResumePausedAudio();

        if (pauseDialogs)
        {
            SetDialogsPaused(false);
        }

        if (pauseMenu != null && pauseMenu.activeSelf)
        {
            pauseMenu.SetActive(false);
        }

        Time.timeScale = 1f;
    }

    private void RestoreTimeScale()
    {
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
    }

    private void OnDisable()
    {
        if (isChangingScene || !isPaused)
        {
            return;
        }

        isPaused = false;

        RestoreTimeScale();

        ResumePausedAudio();

        if (pauseDialogs)
        {
            SetDialogsPaused(false);
        }

        if (pauseMenu != null && pauseMenu.activeSelf)
        {
            pauseMenu.SetActive(false);
        }
    }
}