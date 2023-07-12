using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LoadMenu : MainMenu
{
    public SaveSlot[] saveSlots;

    void Start()
    {
        for (int i = 0; i < saveSlots.Length; i++)
        {
            saveSlots[i].SetSlot(i);
        }


    }
}