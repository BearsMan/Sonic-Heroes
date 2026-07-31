using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(menuName = "Teams/Team Composition")]
public sealed class TeamComposition : ScriptableObject
{
    [SerializeField] private PlayableTeam playableTeam;

    [Header("Characters")]
    [SerializeField] private GameObject speedCharacterPrefab;
    [SerializeField] private GameObject flyingCharacterPrefab;
    [SerializeField] private GameObject powerCharacterPrefab;
    [SerializeField] private GameObject superCharacterPrefab;

    [Header("Presentation")]
    [SerializeField] private Sprite teamIcon;
    [SerializeField] private Color accentColor;
    [SerializeField] private VideoClip teamBlast;

    public PlayableTeam PlayableTeam => playableTeam;

    public GameObject SpeedCharacterPrefab => speedCharacterPrefab;
    public GameObject FlyingCharacterPrefab => flyingCharacterPrefab;
    public GameObject PowerCharacterPrefab => powerCharacterPrefab;
    public GameObject SuperCharacterPrefab => superCharacterPrefab;

    public Sprite TeamIcon => teamIcon;
    public Color AccentColor => accentColor;
    public VideoClip TeamBlast => teamBlast;
}