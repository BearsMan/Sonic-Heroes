using UnityEngine;

[CreateAssetMenu(
    fileName = "Character Animator Profile",
    menuName = "Sonic Heroes/Characters/Animator Profile")]
public sealed class CharacterAnimatorProfile : ScriptableObject
{
    [Header("Controller")]
    public RuntimeAnimatorController animatorController;
    public Avatar avatar;

    [Header("Parameter Names")]
    public string stateParameter = "State";
    public string groundedParameter = "Grounded";
    public string speedParameter = "Speed";
    public string verticalSpeedParameter = "VerticalSpeed";

    [Header("Optional Trigger Names")]
    public string jumpTrigger = "Jump";
    public string rollTrigger = "Roll";
    public string brakingTrigger = "Brake";
    public string hurtTrigger = "Hurt";
    public string homingAttackTrigger = "HomingAttack";

    public int StateHash =>
        Animator.StringToHash(
            stateParameter);

    public int GroundedHash =>
        Animator.StringToHash(
            groundedParameter);

    public int SpeedHash =>
        Animator.StringToHash(
            speedParameter);

    public int VerticalSpeedHash =>
        Animator.StringToHash(
            verticalSpeedParameter);

    public int JumpHash =>
        Animator.StringToHash(
            jumpTrigger);

    public int RollHash =>
        Animator.StringToHash(
            rollTrigger);

    public int BrakingHash =>
        Animator.StringToHash(
            brakingTrigger);

    public int HurtHash =>
        Animator.StringToHash(
            hurtTrigger);

    public int HomingAttackHash =>
        Animator.StringToHash(
            homingAttackTrigger);
}