using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager currentGame;
    public static bool ringscheck = false;

    GameObject source;
    public AudioClip clip;
    public void Awake()
    {
        currentGame = this;
        source = Resources.Load<GameObject>("Audio Object");
    }
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void AddRing(int count)
    {
        GameInstance.currentRings += count;
        if (GameInstance.currentRings % 100 == 0)

        { 
            GameInstance.livesCount++;
            GameObject ao = Instantiate(source);
            ao.GetComponent<AudioObject>().Setup(clip, transform);
        }
    }
}
