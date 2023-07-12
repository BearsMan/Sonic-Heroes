using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TeamSelections : MainMenu
{

    public void SelectTeams(TeamComposition selectedTeam)
    {
        SceneManager.LoadScene(9);
    }


}
