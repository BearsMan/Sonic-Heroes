using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameInstance
{
    public static int currentRings = 0;
    public static int goalring = 0;
    public static SaveData currentSave;
    public static int character = 0;
    public static float timeSeconds = 0f;
    public static int scoreCount;
    public static int currentTeam;
    public static int livesCount;
    public static int nextEmeraldIndex = 0;
    public static bool[] emerald = new bool [7];
    public static TeamComposition TeamComp;
    public static int speedScore, flyScore, powerScore, bonusScore;
    public static int speedLevelUp, flyLevelUp, powerLevelUp;
    private static int saveSlot;
    public static float teamBlastMeter = 0f;
    public static float maxTeamBlastMeter = 100f;
    public static bool hasSpecialKey = false;
    public static bool lastStoryUnlocked = false;
    public static string returnScene;
    public static int LevelScore
    {
        get
        {
            return (speedScore + flyScore + powerScore + bonusScore);
        }
    }

    public static int speedCoreLevelUp { get; internal set; }
    public static int flyCorelevelUp { get; internal set; }
    public static int powerLevelUpCore { get; internal set; }

    public delegate void BasicDelegate();
    public static BasicDelegate UpdateData;

    public static void SetTime(int levelNumber, float timeSeconds)
    {
        currentSave.Times[levelNumber] = timeSeconds;
    }
    public static void CreateSave()
    {
        currentSave = new SaveData
        {
            Score = scoreCount,
            TeamUsed = currentTeam,
            ChaosEmerald = emerald,
            Lives = livesCount,
            CurrentLevel = SceneManager.GetActiveScene().name
        };

        SaveLoad.SaveGame(currentSave, saveSlot);

    }

    public static void LoadSave(int Slot)
    {
        if (SaveLoad.Load(Slot))
        {
            currentSave = SaveLoad.SavedGame;
            saveSlot = Slot;
        }
    }

    public static void AddRings(CHARACTERTYPES type, int amount = 1)
    {
        currentRings += amount;
        AddScore(10 * amount, type);
    }

    public static void AddScore(int points, CHARACTERTYPES type)
    {
        switch (type)
        {
            case CHARACTERTYPES.Speed:
                speedScore += points;
                break;
            case CHARACTERTYPES.Fly:
                flyScore += points;
                break;
            case CHARACTERTYPES.Power:
                powerScore += points;
                break;
        }

        scoreCount += points;
        UpdateData?.Invoke();
    }

    public static int RemoveRings()
    {
        int rings = currentRings;
        currentRings = 0;
        UpdateData?.Invoke();
        return rings;
    }
}
