// SonicHeroesSubtitleData.cs

using System;
using UnityEngine;

public enum SonicHeroesTeam
{
    Sonic,
    Dark,
    Rose,
    Chaotix
}

public enum SonicHeroesCharacter
{
    Sonic, Tails, Knuckles,
    Shadow, Rouge, Omega,
    Amy, Cream, Big,
    Espio, Charmy, Vector
}

[Serializable]
public class CharacterSubtitleEntry
{
    public SonicHeroesTeam team;

    public SonicHeroesCharacter character;

    public StageID stageID;

    public SubtitleEventID eventID;

    [TextArea(2, 5)]
    public string line;

    public AudioClip voiceClip;
}