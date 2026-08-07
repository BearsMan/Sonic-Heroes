using UnityEngine;

[CreateAssetMenu(
    fileName = "Character Presentation Profile",
    menuName = "Sonic Heroes/Characters/Presentation Profile")]
public sealed class CharacterPresentationProfile : ScriptableObject
{
    [Header("Model")]
    public GameObject characterModelPrefab;

    [Header("Companion")]
    public GameObject companionPrefab;
    public string companionAnchorName = "CompanionAnchor";

    [Header("Audio")]
    public AudioClip jumpSound;
    public AudioClip rollSound;
    public AudioClip brakeSound;
    public AudioClip hurtSound;
    public AudioClip homingAttackSound;

    [Header("Effects")]
    public GameObject jumpEffectPrefab;
    public GameObject rollEffectPrefab;
    public GameObject brakeEffectPrefab;
    public GameObject hurtEffectPrefab;
    public GameObject homingAttackEffectPrefab;

    [Header("Attachment Names")]
    public string modelRootName = "ModelRoot";
    public string footEffectPointName = "FootEffectPoint";
    public string attackEffectPointName = "AttackEffectPoint";
}