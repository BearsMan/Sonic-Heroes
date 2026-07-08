using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenu;
    public List<AudioSource> pausedSounds = new List<AudioSource>();

    void Start()
    {

    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SwitchPause();
        }
    }

    public void PauseGame()
    {
        pausedSounds = new List<AudioSource>();

        AudioSource[] allSounds = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude);
        foreach (AudioSource source in allSounds)
        {
            if (source.isPlaying)
            {
                source.Pause();
                pausedSounds.Add(source);
            }
        }

        foreach (TriggerDialog td in FindObjectsByType<TriggerDialog>(FindObjectsInactive.Exclude))
        {
            td.pause = true;
        }

        pauseMenu.SetActive(true);
        Time.timeScale = 0;
    }

    public void UnpauseGame()
    {
        foreach (AudioSource source in pausedSounds)
        {
            source.UnPause();
        }

        foreach (TriggerDialog td in FindObjectsByType<TriggerDialog>(FindObjectsInactive.Exclude))
        {
            td.pause = false;
        }

        Time.timeScale = 1;
        pauseMenu.SetActive(false);
    }

    public void SwitchPause()
    {
        if (pauseMenu.activeSelf)
        {
            UnpauseGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void Restartlevel()
    {
        Scene myscene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(myscene.name);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}