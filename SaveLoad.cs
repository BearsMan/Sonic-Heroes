using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using UnityEngine;
public static class SaveLoad
{
    public static int slotSelected;
    public static SaveData SavedGame;

    private static string SaveSlotName(int saveSlot)
    {
        return Application.persistentDataPath + "/Slot" + saveSlot + ".sav";
    }
    public static void SaveGame(SaveData dataToSave, int saveSlot)
    {
        BinaryFormatter bf = new BinaryFormatter();
        FileStream file = File.Create(SaveSlotName(saveSlot));
        bf.Serialize(file, dataToSave);
        file.Close();
        Load(saveSlot);

    }

    public static bool Load(int saveSlot)
    {
        if (File.Exists(SaveSlotName(saveSlot)))
        {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Open(SaveSlotName(saveSlot), FileMode.Open);

            SavedGame = (SaveData)bf.Deserialize(file);
            file.Close();
            return true;
        }
        else return false;
    }
}

[System.Serializable]
public class SaveData
{
    public float[] Times = new float[21];

    public int Score = 0;
    public string CurrentLevel = "Sea Gate";
    public bool[] ChaosEmerald = new bool[7];
    public int TeamUsed = 0;
    public int Lives = 3;
    public int EmeraldsCount
    {
        get
        {
            int count = 0;
            foreach (bool b in ChaosEmerald)
            {
                if (b) count++;
            }
            return count;
        }
    }

    public float TotalTime
    {
        get
        {
            float seconds = 0;
            foreach (float time in Times)
            {
                seconds += time;
            }
            return seconds;
        }
    }

}

public enum TEAMS
{
    Sonic,
    Dark,
    Rose,
    Chaotix
}
