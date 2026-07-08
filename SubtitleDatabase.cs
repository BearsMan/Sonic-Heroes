// SubtitleDatabase.cs

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Sonic Heroes/Subtitles/Subtitle Database")]
public class SubtitleDatabase : ScriptableObject
{
    public List<CharacterSubtitleEntry> lines = new();

    public List<CharacterSubtitleEntry> GetLinesForCharacter(SonicHeroesCharacter character)
    {
        return lines.FindAll(entry => entry.character == character);
    }

    public List<CharacterSubtitleEntry> GetLinesForTeam(SonicHeroesTeam team)
    {
        return lines.FindAll(entry => CharacterBelongsToTeam(entry.character, team));
    }

    public static bool CharacterBelongsToTeam(SonicHeroesCharacter character, SonicHeroesTeam team)
    {
        return team switch
        {
            SonicHeroesTeam.Sonic => character is
                SonicHeroesCharacter.Sonic or
                SonicHeroesCharacter.Tails or
                SonicHeroesCharacter.Knuckles,

            SonicHeroesTeam.Dark => character is
                SonicHeroesCharacter.Shadow or
                SonicHeroesCharacter.Rouge or
                SonicHeroesCharacter.Omega,

            SonicHeroesTeam.Rose => character is
                SonicHeroesCharacter.Amy or
                SonicHeroesCharacter.Cream or
                SonicHeroesCharacter.Big,

            SonicHeroesTeam.Chaotix => character is
                SonicHeroesCharacter.Espio or
                SonicHeroesCharacter.Charmy or
                SonicHeroesCharacter.Vector,

            _ => false
        };
    }
}