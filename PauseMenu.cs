using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenu;
    public List<AudioSource> pausedSounds;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SwitchPause();
        }
    }

    [System.Obsolete]
    public void PauseGame()
    {
        pausedSounds = new List<AudioSource>();
        AudioSource[] allSounds = FindObjectsOfType<AudioSource>();
        foreach(AudioSource source in allSounds)
        {
            if (source.isPlaying)
            {
                source.Pause();
                pausedSounds.Add(source);
            }
        }

        foreach(TriggerDialog td in FindObjectsOfType<TriggerDialog>())
        {
            td.pause = true;
        }
        pauseMenu.SetActive(true);
        Time.timeScale = 0;
    }

    [System.Obsolete]
    public void UnpauseGame()
    {
        foreach(AudioSource source in pausedSounds)
        {
            source.UnPause();
        }
        foreach (TriggerDialog td in FindObjectsOfType<TriggerDialog>())
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
