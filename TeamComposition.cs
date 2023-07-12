using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "New Team", menuName = "Team Composition", order = 0)]
public class TeamComposition : ScriptableObject
{
    public GameObject SpeedCharacter;
    public GameObject FlyingCharacter;
    public GameObject PowerCharacter;
    public GameObject SuperCharacter;
    public Character speedCharacter, flyingCharacter,  powerCharacter, superCharacter;
    public VideoClip teamBlast;
}
