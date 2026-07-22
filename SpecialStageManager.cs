using UnityEngine;
using UnityEngine.SceneManagement;

public class SpecialStageManager : MonoBehaviour
{
    [Header("Special Stage")]

    [SerializeField] private float stageTime = 90f;
    [SerializeField] private string returnScene;
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
        if (finished)
        {
            return;
        }

        finished = true;

        GameInstance.emerald[GameInstance.nextEmeraldIndex] = true;
        Debug.Log("Collected" + rewardEmerald + "Chaos Emerald!");

        Debug.Log("Collected Emerald: " + GameInstance.nextEmeraldIndex);

        if (GameInstance.nextEmeraldIndex < GameInstance.emerald.Length - 1)
        {
            GameInstance.nextEmeraldIndex++;
        }

        GameInstance.hasSpecialKey = false;

        CheckLastStory();

        SceneManager.LoadScene(returnScene);
    }

    public void FailStage()
    {
        if (finished)
        {
            return;
        }

        finished = true;

        GameInstance.hasSpecialKey = false;

        Debug.Log("Special Stage Failed");

        SceneManager.LoadScene(returnScene);
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