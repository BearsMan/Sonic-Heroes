using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SonicHeroesMainMenu : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void MainMenu()
    {
        SceneManager.LoadScene("Opening Movie");
    }

    public void SelectGame()
    {
        SceneManager.LoadScene("Menu (Selector)");
    }
    public void LoadStorySelect()
    {
        SceneManager.LoadScene("Main Story (Team Selection)");
    }
    public void Load1PlayerMenuSelection()
    {
        SceneManager.LoadScene("1 Player");
    }
    public void Load2PlayerGame()
    {
        SceneManager.LoadScene("2 Players");
    }
    public void LoadAudioRoom()
    {
        SceneManager.LoadScene("Audio Room Menu");
    }
    public void LoadAudioTheater()
    {
        SceneManager.LoadScene("Audio Theater");
    }
    public void LoadBGM()
    {
        SceneManager.LoadScene("BGM Selection");
    }
    public void LoadBGMSelection()
    {
        SceneManager.LoadScene("Challege Screen (Sonic Heroes)");

    }
    public void LoadCGSelection()
    {
        SceneManager.LoadScene("CG Theater");
    }
    public void LoadLastSavedGame()
    {
        SceneManager.LoadScene("Continue Game");
    }
    public void LoadExtrasOptions()
    {
        SceneManager.LoadScene("Options Menu");
    }
    public void LoadMainMenu()
    {
        SceneManager.LoadScene("Main Menu");
    }
    public void LoadVibrationSettings()
    {
        SceneManager.LoadScene("Vibration Settings");
    }
    public void LoadStages()
    {
        SceneManager.LoadScene("Stage 00");
        SceneManager.LoadScene("Stage 01");
        SceneManager.LoadScene("Stage 02");
        SceneManager.LoadScene("Team - Boss Battle - Egg Hawk");
        SceneManager.LoadScene("Stage 03");
        SceneManager.LoadScene("Stage 04");
        SceneManager.LoadScene("Team - Boss Battle - Team 1");
        SceneManager.LoadScene("Stage 05");
        SceneManager.LoadScene("Stage 06");
        SceneManager.LoadScene("Stage 07");
        SceneManager.LoadScene("Stage 08");
        SceneManager.LoadScene("Team - Boss Battle - Egg Albatross");
        SceneManager.LoadScene("Stage 09");
        SceneManager.LoadScene("Stage 10");
        SceneManager.LoadScene("Team - Boss Battle - Team 2");
        SceneManager.LoadScene("Stage 11");
        SceneManager.LoadScene("Stage 12");
        SceneManager.LoadScene("Team - Boss Battle - Robot Carnaval and Robot Storm");
        SceneManager.LoadScene("Stage 13");
        SceneManager.LoadScene("Stage 14");
        SceneManager.LoadScene("Team - Boss Battle - Egg Emperor");
        SceneManager.LoadScene("Last Story - Start");
        SceneManager.LoadScene("Last Story - Metal Madness");
        SceneManager.LoadScene("Last Story - Metal Overlord");
    }
}

