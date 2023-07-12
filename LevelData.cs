using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelData : MonoBehaviour
{
    public static LevelData levelData;
    public int levelNumber = 0;

    void Start()
    {
        levelData = this;
    }
}
