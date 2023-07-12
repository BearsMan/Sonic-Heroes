using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class SaveSlot : MonoBehaviour
{
    public TMP_Text timeText, livesText, scoreText, emeraldsText;
    public Image teamImage;
    int slotNumber = 0;

    public void SetSlot(int slot)
    {
        if (SaveLoad.Load(slot))
        {
            slotNumber = slot;

            SaveData data = SaveLoad.SavedGame;
            float minutes = Mathf.Floor(data.TotalTime / 60);
            float seconds = (data.TotalTime % 60);
            float hours = Mathf.Floor(data.TotalTime / 3600);
            float miliseconds = data.TotalTime - Mathf.Floor(data.TotalTime);
            miliseconds = Mathf.RoundToInt(miliseconds * 100);

            if (hours > 0)
            {
                timeText.text = hours.ToString("00") + minutes.ToString("00") + seconds.ToString("00");

            }
            else
            {
                timeText.text = minutes.ToString("00") + seconds.ToString("00") + miliseconds.ToString("00");
            }

            livesText.text = data.Lives.ToString("000");
            scoreText.text = data.Score.ToString("000");
            emeraldsText.text = data.EmeraldsCount.ToString("0");

        }
        else GetComponent<Button>().interactable = false;
    }

    public void SelectSlot()
    {

    }
}
