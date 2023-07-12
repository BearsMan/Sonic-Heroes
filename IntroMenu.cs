using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IntroMenu : MainMenu
{
    protected override void Update()
    {
        base.Update();

        if (Input.anyKey)
        {
            CancelInvoke("LoadScene");
            LoadScene(1);
        }
    }

    private void Start()
    {
        Invoke("LoadScene", 117);
    }
}
