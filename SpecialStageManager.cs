using UnityEngine;
using UnityEngine.SceneManagement;

public class SpecialStageManager : MonoBehaviour
{
    [Header("Special Stage")]

    [SerializeField] private float stageTime = 90f;
    public ItemBalloon.EmeraldType rewardEmerald;

    private bool finished;

    void Update()
    {
        if (finished)
        {
            return;
        }

        stageTime -= Time.deltaTime;

        if (stageTime <= 0f)
        {
            FailStage();
        }
    }

    public void CompleteStage()
    {
        // ...

        GameInstance.hasSpecialKey = false;

        CheckLastStory();

        SceneManager.LoadScene(GameInstance.returnScene);
    }

    public void FailStage()
    {
        if (finished)
            return;

        finished = true;

        GameInstance.hasSpecialKey = false;

        Debug.Log("Special Stage Failed");

        SceneManager.LoadScene(GameInstance.returnScene);
    }

    void CheckLastStory()
    {
        foreach (bool emerald in GameInstance.emerald)
        {
            if (!emerald)
            {
                return;
            }
        }

        GameInstance.lastStoryUnlocked = true;

        Debug.Log("Last Story Unlocked!");
    }
}