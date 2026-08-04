using UnityEngine;

[CreateAssetMenu(
    fileName = "Character Definition",
    menuName = "Sonic Heroes/Characters/Character Definition")]
public sealed class CharacterDefinition : ScriptableObject
{
    public enum CharacterType
    {
        Speed,
        Fly,
        Power,
        Special
    }

    [Header("Identity")]
    public string characterId;
    public string displayName;

    public CharacterType characterType =
        CharacterType.Speed;

    [Header("Profiles")]
    public CharacterMovementProfile movementProfile;
    public CharacterAbilityProfile abilityProfile;
    public CharacterAnimatorProfile animatorProfile;
    public CharacterPresentationProfile presentationProfile;

    public bool IsValid()
    {
        return
            !string.IsNullOrWhiteSpace(characterId) &&
            movementProfile != null &&
            abilityProfile != null;
    }
}