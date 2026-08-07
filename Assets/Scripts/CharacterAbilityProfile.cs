using UnityEngine;

[CreateAssetMenu(
    fileName = "Character Ability Profile",
    menuName = "Sonic Heroes/Characters/Ability Profile")]
public sealed class CharacterAbilityProfile : ScriptableObject
{
    [Header("Shared Movement Abilities")]
    public bool canJump = true;
    public bool canRoll = true;
    public bool canHomingAttack = true;
    public bool canGrind = true;

    [Header("Formation Abilities")]
    public bool canFly;
    public bool canGlide;
    public bool canPowerAction;

    [Header("Advanced Movement")]
    public bool canBrake = true;
    public bool usesSlopeMomentum = true;

    [Header("Speed Abilities")]
    public bool canLightDash;
    public bool canWallJump;
    public bool canTriangleJump;
    public bool canRocketAccel;
    public bool canInvisibility;

    [Header("Fly Abilities")]
    public bool canThunderShoot;
    public bool canTeamFlight;

    [Header("Power Abilities")]
    public bool canFireDunk;
    public bool canBodySlam;
    public bool canHammerAttack;

    [Header("Special Abilities")]
    public bool canChaosControl;
    public bool canChaosAttack;
    public bool canSuperForm;
    public bool canTeamBlast = true;
}