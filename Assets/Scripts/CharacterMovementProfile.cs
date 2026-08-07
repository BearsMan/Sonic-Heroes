using UnityEngine;

[CreateAssetMenu(
    fileName = "Character Movement Profile",
    menuName = "Sonic Heroes/Characters/Movement Profile")]
public sealed class CharacterMovementProfile : ScriptableObject
{
    [Header("Ground Movement")]
    [Min(0f)] public float runSpeed = 20f;
    [Min(0f)] public float acceleration = 40f;
    [Min(0f)] public float deceleration = 50f;
    [Min(0f)] public float turnSpeed = 15f;
    [Min(0f)] public float brakingForce = 60f;

    [Header("Jumping")]
    [Min(0f)] public float jumpForce = 10f;
    [Min(0f)] public float coyoteTime = 0.15f;
    [Min(0f)] public float jumpBufferTime = 0.1f;

    [Header("Air Movement")]
    [Min(0f)] public float airSpeed = 15f;
    [Min(0f)] public float airControl = 20f;

    [Header("Slopes")]
    [Min(0f)] public float slopeAcceleration = 15f;

    [Range(0f, 89f)]
    public float maximumSlopeAngle = 55f;

    [Header("Rolling")]
    [Min(0f)] public float rollingSpeed = 25f;
    [Min(0f)] public float rollingTurnSpeed = 6f;

    [Header("Homing Attack")]
    [Min(0f)] public float homingAttackSpeed = 35f;
    [Min(0f)] public float homingAttackDuration = 0.5f;
    [Min(0f)] public float homingRange = 20f;

    [Header("Flying")]
    [Min(0f)] public float flyingSpeed = 15f;
    [Min(0f)] public float flyingVerticalSpeed = 10f;

    [Header("Damage Response")]
    [Min(0f)] public float defaultHurtDuration = 1f;

    private void OnValidate()
    {
        runSpeed = Mathf.Max(0f, runSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        deceleration = Mathf.Max(0f, deceleration);
        turnSpeed = Mathf.Max(0f, turnSpeed);
        brakingForce = Mathf.Max(0f, brakingForce);

        jumpForce = Mathf.Max(0f, jumpForce);
        coyoteTime = Mathf.Max(0f, coyoteTime);
        jumpBufferTime = Mathf.Max(0f, jumpBufferTime);

        airSpeed = Mathf.Max(0f, airSpeed);
        airControl = Mathf.Max(0f, airControl);

        slopeAcceleration =
            Mathf.Max(
                0f,
                slopeAcceleration);

        maximumSlopeAngle =
            Mathf.Clamp(
                maximumSlopeAngle,
                0f,
                89f);

        rollingSpeed = Mathf.Max(0f, rollingSpeed);
        rollingTurnSpeed = Mathf.Max(0f, rollingTurnSpeed);

        homingAttackSpeed =
            Mathf.Max(
                0f,
                homingAttackSpeed);

        homingAttackDuration =
            Mathf.Max(
                0f,
                homingAttackDuration);

        homingRange = Mathf.Max(0f, homingRange);

        flyingSpeed = Mathf.Max(0f, flyingSpeed);

        flyingVerticalSpeed =
            Mathf.Max(
                0f,
                flyingVerticalSpeed);

        defaultHurtDuration =
            Mathf.Max(
                0f,
                defaultHurtDuration);
    }
}