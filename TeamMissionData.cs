using System;
using UnityEngine;

[Serializable]
public class TeamMissionData
{
    [SerializeField] private string missionLabel = "MISSION";
    [SerializeField, TextArea] private string missionText = "Reach the goal!";

    public string MissionLabel => missionLabel;
    public string MissionText => missionText;
}