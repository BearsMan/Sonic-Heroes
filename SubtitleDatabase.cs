// SubtitleDatabase.cs

using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

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
        return lines.FindAll(entry => entry.team == team);
    }

    public List<CharacterSubtitleEntry> GetByEvent(SonicHeroesTeam team, SubtitleEventID eventID)
    {
        return lines.FindAll(entry =>
            entry.team == team &&
            entry.eventID == eventID);
    }


    public List<CharacterSubtitleEntry> GetByStageEvent(
    SonicHeroesTeam team,
    StageID stageID,
    SubtitleEventID eventID)
    {
        return lines.FindAll(entry =>
            entry.team == team &&
            entry.stageID == stageID &&
            entry.eventID == eventID);
    }
    // Utility method kept for future use.
    // Not currently used by the subtitle system because entries store their team directly.
    public static bool CharacterBelongsToTeam(SonicHeroesCharacter character, SonicHeroesTeam team)
    {
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
}