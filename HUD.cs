using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUD : MonoBehaviour
{
    public TMP_Text scoreText, timeText, ringText, livesText;
    public GameObject playerprefab;
    public List<Image> icons, faces;
    public List<Character> teamSprites;
    public List<Image> lightsSpeed, lightsFly, lightsPower;
    public Image livesImage;
    TeamComposition curTeam;
    public static float timer;
    public Sprite speedSprite, flySprite, powerSprite;
    public Slider powerUpGauge;
    private int powerUpLevel = 0;
    public GameObject teamBlastPrompt;
    public Image teamBlastFill;
    public Image itemPickUp;

    public bool swap;
    public CHARACTERTYPES newType;

    // Start is called before the first frame update
    [System.Obsolete]
    void Start()
    {
        GameInstance.UpdateData += UpdateRings;
        UpdateHUD();
    }

    public void Setup(TeamComposition currentTeam)
    {
        curTeam = currentTeam;
        teamSprites.Add(curTeam.speedCharacter);
        teamSprites.Add(curTeam.flyingCharacter);
        teamSprites.Add(curTeam.powerCharacter);
        livesImage.sprite = curTeam.speedCharacter.face;
        SetCharacter(0);
    }

    // Update is called once per frame
    [System.Obsolete]
    void Update()
    {
        timer += Time.deltaTime;
        UpdateHUD();
    }
    [System.Obsolete]
    public void UpdateHUD()
    {

        int ms = Mathf.RoundToInt((timer - Mathf.Floor(timer)) * 100);
        if (ms >= 100) ms -= 100;
        string s = Mathf.Floor(timer % 60).ToString("00");
        string m = Mathf.Floor(timer / 60).ToString("00");
        timeText.text = ($"{m}:{s}:{ms:00}");

        scoreText.text = GameInstance.scoreCount.ToString("00000000");
        livesText.text = GameInstance.livesCount.ToString("00");

       
        UpdateRings();
        UpdateTeamBlastMeter();
    }
    [System.Obsolete]
    public void UpdateTeamBlastMeter()
    {
        powerUpGauge.value = powerUpLevel;
        if (powerUpLevel == 100)
        {
            teamBlastFill.color = Color.yellow;
            teamBlastPrompt.SetActive(true);
            if (Input.GetKeyDown(KeyCode.Z))
            {
                FindFirstObjectByType<TeamBlastVideos>().SpecialAttack();
                powerUpLevel = 0;
            }
        }
        else
        {
            teamBlastPrompt.SetActive(false);
            teamBlastFill.color = Color.blue;
        }
    }

    public void AddPower(int value)
    {
        powerUpLevel = Mathf.Min(100, powerUpLevel + value);
    }

    public void UpdateRings()
    {
        ringText.text = GameInstance.currentRings.ToString("000");
    }

    public void SetCharacter(CHARACTERTYPES type)
    {
        teamSprites[0] = curTeam.speedCharacter;
        teamSprites[1] = curTeam.flyingCharacter;
        teamSprites[2] = curTeam.powerCharacter;

        switch (type)
        {
            case (CHARACTERTYPES.Speed):
                SetCharacter(0);
                break;
            case (CHARACTERTYPES.Fly):
                SetCharacter(-1);
                break;
            case (CHARACTERTYPES.Power):
                SetCharacter(2);
                break;
        } 
    
    }
    public void UpdateCharacterLevels()
    {
        GameInstance.speedLevelUp = Mathf.Clamp(GameInstance.speedLevelUp, 0, 3);
        GameInstance.flyLevelUp = Mathf.Clamp(GameInstance.flyLevelUp, 0, 3);
        GameInstance.powerLevelUp = Mathf.Clamp(GameInstance.powerLevelUp, 0, 3);
        int counter = 0;
        while (counter < GameInstance.speedLevelUp)
        {
            lightsSpeed[counter].enabled = true;
            counter += 1;
        }
        counter = 0;
        while (counter < GameInstance.flyLevelUp)
        {
            lightsFly[counter].enabled = true;
            counter += 1;
        } counter = 0;
        while (counter < GameInstance.powerLevelUp)
        {
            lightsPower[counter].enabled = true;
            counter += 1;
        }
    }

    public void SetCharacter(int direction)
    {
        if (direction >  0)
        {
            Character s = teamSprites[teamSprites.Count - 1];
            teamSprites.RemoveAt(teamSprites.Count - 1);
            teamSprites.Insert(0, s);
        }
        else if(direction < 0)
        {
            Character s = teamSprites[0];
            teamSprites.RemoveAt(0);
            teamSprites.Add(s);
        }
        livesImage.sprite = teamSprites[0].face;
        for (int i = 0; i < 3; i++)
        {
            faces[i].sprite = teamSprites[i].face;
            icons[i].sprite = teamSprites[i].icon;
        }
    }

    public void ShowPickUp(Sprite item)
    {
        itemPickUp.sprite = item;
        StartCoroutine(ShowItem());
    }

    private IEnumerator ShowItem()
    {
        itemPickUp.enabled = true;
        yield return new WaitForSeconds(3);
        itemPickUp.enabled = false;
    }
}