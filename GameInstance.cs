using System.Collections;
using System.Collections.Generic;
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
    public static bool[] emerald;
    public static bool greenEmerald, blueEmerald, yellowEmerald, whiteEmerald, lightBlueEmerald, purpleEmerald, redEmerald;
    public static TeamComposition TeamComp;
    public static int speedScore, flyScore, powerScore, bonusScore;
    public static int speedLevelUp, flyLevelUp, powerLevelUp;
    private static int saveSlot;
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
            lives = livesCount,
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

    public static void AddRings(CHARACTERTYPES type)
    {
        if (type == CHARACTERTYPES.Speed) speedScore += 10;
        if (type == CHARACTERTYPES.Fly) flyScore += 10;
        if (type == CHARACTERTYPES.Power) powerScore += 10;
        currentRings += 1;
        UpdateData?.Invoke();
    }

    public static void AddScore(int points, CHARACTERTYPES type)
    {
        if (type == CHARACTERTYPES.Speed) speedScore += points;
        if (type == CHARACTERTYPES.Fly) flyScore += points;
        if (type == CHARACTERTYPES.Power) powerScore += points;
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
