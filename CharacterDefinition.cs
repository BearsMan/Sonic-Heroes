using System;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
#endif

[CreateAssetMenu(
    fileName = "Character Definition",
    menuName = "Sonic Heroes/Characters/Character Definition")]
public sealed class CharacterDefinition : ScriptableObject
{
    #region Types

    public enum CharacterType
    {
        Speed,
        Fly,
        Power,
        Special
    }

    #endregion

    #region Constants

    private const string DefinitionSuffix =
        "Character Definition";

    private const string MovementFolderName =
        "Movement Profiles";

    private const string AbilityFolderName =
        "Ability Profiles";

    private const string AnimatorFolderName =
        "Animator Profiles";

    private const string PresentationFolderName =
        "Presentation Profiles";

    #endregion

    #region Inspector

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

    [Header("Automatic Setup")]
    [SerializeField] private bool automaticSetup = true;
    [SerializeField] private bool applyTypePresets = true;
    [SerializeField] private bool autoAssignProjectAssets = true;

    #endregion

    #region Editor State

#if UNITY_EDITOR
    [NonSerialized] private bool setupScheduled;
    [NonSerialized] private bool applyingSetup;
#endif

    #endregion

    #region Public API

    public bool IsValid()
    {
        return
            !string.IsNullOrWhiteSpace(characterId) &&
            !string.IsNullOrWhiteSpace(displayName) &&
            movementProfile != null &&
            abilityProfile != null;
    }

#if UNITY_EDITOR
    public void ConfigureAutomatically()
    {
        RunAutomaticSetup();
    }
#endif

    #endregion

    #region Unity Lifecycle

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (!automaticSetup ||
            applyingSetup ||
            setupScheduled)
        {
            return;
        }

        setupScheduled = true;

        EditorApplication.delayCall +=
            RunScheduledSetup;
#endif
    }

    #endregion

#if UNITY_EDITOR

    #region Automatic Setup

    [ContextMenu("Run Automatic Setup")]
    private void RunAutomaticSetup()
    {
        if (applyingSetup ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        string definitionPath =
            AssetDatabase.GetAssetPath(this);

        if (string.IsNullOrWhiteSpace(definitionPath))
        {
            return;
        }

        applyingSetup = true;

        try
        {
            ConfigureIdentity();
            CreateAndAssignProfiles();

            if (applyTypePresets)
            {
                ApplyMovementPreset();
                ApplyAbilityPreset();
            }

            ConfigureAnimatorDefaults();
            ConfigurePresentationDefaults();

            if (autoAssignProjectAssets)
            {
                AutoAssignProjectAssets();
            }

            MarkAssetsDirty();
            AssetDatabase.SaveAssets();
        }
        finally
        {
            applyingSetup = false;
            setupScheduled = false;
        }
    }

    private void RunScheduledSetup()
    {
        setupScheduled = false;

        if (this == null ||
            !automaticSetup)
        {
            return;
        }

        RunAutomaticSetup();
    }

    private void ConfigureIdentity()
    {
        CharacterSetupData setupData =
            GetCharacterSetupData(
                NormalizeAssetName(name));

        characterId =
            setupData.characterId;

        displayName =
            setupData.displayName;

        characterType =
            setupData.characterType;
    }

    private void CreateAndAssignProfiles()
    {
        string definitionPath =
            AssetDatabase.GetAssetPath(this);

        string parentFolder =
            Path.GetDirectoryName(definitionPath)
            ?.Replace("\\", "/");

        if (string.IsNullOrWhiteSpace(parentFolder))
        {
            return;
        }

        string movementFolder =
            EnsureFolder(
                parentFolder,
                MovementFolderName);

        string abilityFolder =
            EnsureFolder(
                parentFolder,
                AbilityFolderName);

        string animatorFolder =
            EnsureFolder(
                parentFolder,
                AnimatorFolderName);

        string presentationFolder =
            EnsureFolder(
                parentFolder,
                PresentationFolderName);

        movementProfile ??=
            LoadOrCreateAsset<CharacterMovementProfile>(
                movementFolder,
                $"{displayName} Movement Profile");

        abilityProfile ??=
            LoadOrCreateAsset<CharacterAbilityProfile>(
                abilityFolder,
                $"{displayName} Ability Profile");

        animatorProfile ??=
            LoadOrCreateAsset<CharacterAnimatorProfile>(
                animatorFolder,
                $"{displayName} Animator Profile");

        presentationProfile ??=
            LoadOrCreateAsset<CharacterPresentationProfile>(
                presentationFolder,
                $"{displayName} Presentation Profile");
    }

    #endregion

    #region Movement Presets

    private void ApplyMovementPreset()
    {
        if (movementProfile == null)
            return;

        switch (characterType)
        {
            case CharacterType.Speed:
                ApplySpeedMovementPreset();
                break;

            case CharacterType.Fly:
                ApplyFlyMovementPreset();
                break;

            case CharacterType.Power:
                ApplyPowerMovementPreset();
                break;

            case CharacterType.Special:
                ApplySpecialMovementPreset();
                break;
        }
    }

    private void ApplySpeedMovementPreset()
    {
        movementProfile.runSpeed = 24f;
        movementProfile.acceleration = 48f;
        movementProfile.deceleration = 54f;
        movementProfile.turnSpeed = 16f;
        movementProfile.brakingForce = 64f;
        movementProfile.jumpForce = 10f;
        movementProfile.airSpeed = 17f;
        movementProfile.airControl = 24f;
        movementProfile.rollingSpeed = 30f;
        movementProfile.rollingTurnSpeed = 7f;
        movementProfile.homingAttackSpeed = 38f;
        movementProfile.homingAttackDuration = 0.5f;
        movementProfile.homingRange = 22f;
        movementProfile.flyingSpeed = 0f;
        movementProfile.flyingVerticalSpeed = 0f;
    }

    private void ApplyFlyMovementPreset()
    {
        movementProfile.runSpeed = 18f;
        movementProfile.acceleration = 36f;
        movementProfile.deceleration = 46f;
        movementProfile.turnSpeed = 14f;
        movementProfile.brakingForce = 54f;
        movementProfile.jumpForce = 9f;
        movementProfile.airSpeed = 16f;
        movementProfile.airControl = 26f;
        movementProfile.rollingSpeed = 22f;
        movementProfile.rollingTurnSpeed = 6f;
        movementProfile.homingAttackSpeed = 30f;
        movementProfile.homingAttackDuration = 0.45f;
        movementProfile.homingRange = 18f;
        movementProfile.flyingSpeed = 17f;
        movementProfile.flyingVerticalSpeed = 11f;
    }

    private void ApplyPowerMovementPreset()
    {
        movementProfile.runSpeed = 16f;
        movementProfile.acceleration = 32f;
        movementProfile.deceleration = 44f;
        movementProfile.turnSpeed = 12f;
        movementProfile.brakingForce = 72f;
        movementProfile.jumpForce = 8.5f;
        movementProfile.airSpeed = 13f;
        movementProfile.airControl = 16f;
        movementProfile.rollingSpeed = 21f;
        movementProfile.rollingTurnSpeed = 5f;
        movementProfile.homingAttackSpeed = 27f;
        movementProfile.homingAttackDuration = 0.45f;
        movementProfile.homingRange = 16f;
        movementProfile.flyingSpeed = 0f;
        movementProfile.flyingVerticalSpeed = 0f;
    }

    private void ApplySpecialMovementPreset()
    {
        ApplySpeedMovementPreset();
        movementProfile.runSpeed = 28f;
        movementProfile.acceleration = 56f;
        movementProfile.airSpeed = 21f;
        movementProfile.rollingSpeed = 34f;
        movementProfile.homingAttackSpeed = 44f;
        movementProfile.homingRange = 26f;
    }

    #endregion

    #region Ability Presets

    private void ApplyAbilityPreset()
    {
        if (abilityProfile == null)
            return;

        ResetAbilityProfile();

        switch (characterType)
        {
            case CharacterType.Speed:
                ConfigureSpeedAbilities();
                break;

            case CharacterType.Fly:
                ConfigureFlyAbilities();
                break;

            case CharacterType.Power:
                ConfigurePowerAbilities();
                break;

            case CharacterType.Special:
                ConfigureSpecialAbilities();
                break;
        }

        ConfigureCharacterSpecificAbilities();
    }

    private void ResetAbilityProfile()
    {
        abilityProfile.canJump = true;
        abilityProfile.canRoll = true;
        abilityProfile.canHomingAttack = false;
        abilityProfile.canGrind = true;
        abilityProfile.canFly = false;
        abilityProfile.canGlide = false;
        abilityProfile.canPowerAction = false;
        abilityProfile.canBrake = true;
        abilityProfile.usesSlopeMomentum = true;
        abilityProfile.canLightDash = false;
        abilityProfile.canWallJump = false;
        abilityProfile.canTriangleJump = false;
        abilityProfile.canRocketAccel = false;
        abilityProfile.canInvisibility = false;
        abilityProfile.canThunderShoot = false;
        abilityProfile.canTeamFlight = false;
        abilityProfile.canFireDunk = false;
        abilityProfile.canBodySlam = false;
        abilityProfile.canHammerAttack = false;
        abilityProfile.canChaosControl = false;
        abilityProfile.canChaosAttack = false;
        abilityProfile.canSuperForm = false;
        abilityProfile.canTeamBlast = true;
    }

    private void ConfigureSpeedAbilities()
    {
        abilityProfile.canHomingAttack = true;
        abilityProfile.canLightDash = true;
        abilityProfile.canWallJump = true;
        abilityProfile.canTriangleJump = true;
    }

    private void ConfigureFlyAbilities()
    {
        abilityProfile.canFly = true;
        abilityProfile.canThunderShoot = true;
        abilityProfile.canTeamFlight = true;
    }

    private void ConfigurePowerAbilities()
    {
        abilityProfile.canPowerAction = true;
        abilityProfile.canFireDunk = true;
        abilityProfile.canBodySlam = true;
    }

    private void ConfigureSpecialAbilities()
    {
        abilityProfile.canHomingAttack = true;
        abilityProfile.canLightDash = true;
        abilityProfile.canChaosControl = true;
        abilityProfile.canChaosAttack = true;
        abilityProfile.canSuperForm = true;
    }

    private void ConfigureCharacterSpecificAbilities()
    {
        switch (characterId)
        {
            case "sonic":
                abilityProfile.canRocketAccel = true;
                break;

            case "shadow":
                abilityProfile.canChaosControl = true;
                abilityProfile.canChaosAttack = true;
                break;

            case "amy":
                abilityProfile.canHammerAttack = true;
                break;

            case "espio":
                abilityProfile.canInvisibility = true;
                break;

            case "knuckles":
            case "rouge":
                abilityProfile.canGlide = true;
                break;

            case "super-sonic":
                abilityProfile.canSuperForm = true;
                abilityProfile.canChaosControl = true;
                abilityProfile.canChaosAttack = true;
                break;
        }
    }

    #endregion

    #region Animator Setup

    private void ConfigureAnimatorDefaults()
    {
        if (animatorProfile == null)
            return;

        animatorProfile.stateParameter = "State";
        animatorProfile.groundedParameter = "Grounded";
        animatorProfile.speedParameter = "Speed";
        animatorProfile.verticalSpeedParameter = "VerticalSpeed";
        animatorProfile.jumpTrigger = "Jump";
        animatorProfile.rollTrigger = "Roll";
        animatorProfile.brakingTrigger = "Brake";
        animatorProfile.hurtTrigger = "Hurt";
        animatorProfile.homingAttackTrigger = "HomingAttack";
    }

    #endregion

    #region Presentation Setup

    private void ConfigurePresentationDefaults()
    {
        if (presentationProfile == null)
            return;

        presentationProfile.modelRootName = "ModelRoot";
        presentationProfile.footEffectPointName = "FootEffectPoint";
        presentationProfile.attackEffectPointName = "AttackEffectPoint";
    }

    #endregion

    #region Project Asset Assignment

    private void AutoAssignProjectAssets()
    {
        string[] searchTerms =
            GetSearchTerms();

        if (animatorProfile != null)
        {
            animatorProfile.animatorController ??=
                FindBestAsset<RuntimeAnimatorController>(
                    searchTerms);

            animatorProfile.avatar ??=
                FindBestAsset<Avatar>(
                    searchTerms);
        }

        if (presentationProfile != null)
        {
            presentationProfile.characterModelPrefab ??=
                FindBestAsset<GameObject>(
                    searchTerms);

            presentationProfile.jumpSound ??=
                FindBestAudioClip("jump");

            presentationProfile.rollSound ??=
                FindBestAudioClip("roll");

            presentationProfile.brakeSound ??=
                FindBestAudioClip("brake");

            presentationProfile.hurtSound ??=
                FindBestAudioClip("hurt");

            presentationProfile.homingAttackSound ??=
                FindBestAudioClip("homing");
        }
    }

    private AudioClip FindBestAudioClip(
        string actionTerm)
    {
        string[] searchTerms =
            GetSearchTerms()
                .Concat(
                    new[] { actionTerm })
                .ToArray();

        return FindBestAsset<AudioClip>(
            searchTerms);
    }

    private string[] GetSearchTerms()
    {
        return new[]
        {
            displayName,
            characterId,
            name.Replace(
                DefinitionSuffix,
                string.Empty)
        }
        .Where(
            value =>
                !string.IsNullOrWhiteSpace(value))
        .Distinct(
            StringComparer.OrdinalIgnoreCase)
        .ToArray();
    }

    private static T FindBestAsset<T>(
        string[] searchTerms)
        where T : UnityEngine.Object
    {
        string[] guids =
            AssetDatabase.FindAssets(
                $"t:{typeof(T).Name}");

        foreach (string searchTerm in searchTerms)
        {
            string normalizedTerm =
                NormalizeSearchText(searchTerm);

            foreach (string guid in guids)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(guid);

                string fileName =
                    NormalizeSearchText(
                        Path.GetFileNameWithoutExtension(path));

                if (!fileName.Contains(normalizedTerm))
                    continue;

                T asset =
                    AssetDatabase.LoadAssetAtPath<T>(
                        path);

                if (asset != null)
                {
                    return asset;
                }
            }
        }

        return null;
    }

    private static string NormalizeSearchText(
        string value)
    {
        return value
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty)
            .ToLowerInvariant();
    }

    #endregion

    #region Character Mapping

    private static CharacterSetupData GetCharacterSetupData(
        string normalizedName)
    {
        switch (normalizedName)
        {
            case "sonic":
            case "sonic the hedgehog":
                return new("sonic", "Sonic the Hedgehog", CharacterType.Speed);

            case "tails":
            case "miles tails prower":
                return new("tails", "Miles Tails Prower", CharacterType.Fly);

            case "knuckles":
            case "knuckles the echidna":
                return new("knuckles", "Knuckles the Echidna", CharacterType.Power);

            case "shadow":
            case "shadow the hedgehog":
                return new("shadow", "Shadow the Hedgehog", CharacterType.Speed);

            case "rouge":
            case "rouge the bat":
                return new("rouge", "Rouge the Bat", CharacterType.Fly);

            case "omega":
            case "e-123 omega":
            case "e123 omega":
                return new("omega", "E-123 Omega", CharacterType.Power);

            case "amy":
            case "amy rose":
                return new("amy", "Amy Rose", CharacterType.Speed);

            case "cream":
            case "cream the rabbit":
                return new("cream", "Cream the Rabbit", CharacterType.Fly);

            case "big":
            case "big the cat":
                return new("big", "Big the Cat", CharacterType.Power);

            case "espio":
            case "espio the chameleon":
                return new("espio", "Espio the Chameleon", CharacterType.Speed);

            case "charmy":
            case "charmy bee":
                return new("charmy", "Charmy Bee", CharacterType.Fly);

            case "vector":
            case "vector the crocodile":
                return new("vector", "Vector the Crocodile", CharacterType.Power);

            case "super sonic":
                return new("super-sonic", "Super Sonic", CharacterType.Special);

            default:
                return CreateFallbackSetupData(
                    normalizedName);
        }
    }

    private static CharacterSetupData CreateFallbackSetupData(
        string normalizedName)
    {
        string fallbackId =
            normalizedName
                .Replace(
                    " character definition",
                    string.Empty)
                .Trim()
                .Replace(" ", "-");

        string fallbackName =
            ObjectNames.NicifyVariableName(
                fallbackId.Replace("-", " "));

        return new(
            fallbackId,
            fallbackName,
            CharacterType.Speed);
    }

    private static string NormalizeAssetName(
        string assetName)
    {
        return assetName
            .Replace(
                DefinitionSuffix,
                string.Empty)
            .Replace(
                "Definition",
                string.Empty)
            .Trim()
            .ToLowerInvariant();
    }

    #endregion

    #region Asset Creation

    private static string EnsureFolder(
        string parentFolder,
        string folderName)
    {
        string folderPath =
            $"{parentFolder}/{folderName}";

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder(
                parentFolder,
                folderName);
        }

        return folderPath;
    }

    private static T LoadOrCreateAsset<T>(
        string folderPath,
        string assetName)
        where T : ScriptableObject
    {
        string assetPath =
            $"{folderPath}/{assetName}.asset";

        T existingAsset =
            AssetDatabase.LoadAssetAtPath<T>(
                assetPath);

        if (existingAsset != null)
        {
            return existingAsset;
        }

        T newAsset =
            CreateInstance<T>();

        newAsset.name =
            assetName;

        AssetDatabase.CreateAsset(
            newAsset,
            assetPath);

        return newAsset;
    }

    private void MarkAssetsDirty()
    {
        EditorUtility.SetDirty(this);

        if (movementProfile != null)
        {
            EditorUtility.SetDirty(
                movementProfile);
        }

        if (abilityProfile != null)
        {
            EditorUtility.SetDirty(
                abilityProfile);
        }

        if (animatorProfile != null)
        {
            EditorUtility.SetDirty(
                animatorProfile);
        }

        if (presentationProfile != null)
        {
            EditorUtility.SetDirty(
                presentationProfile);
        }
    }

    #endregion

    #region Batch Setup

    [MenuItem(
        "Sonic Heroes/Characters/Configure All Definitions")]
    private static void ConfigureAllDefinitions()
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:CharacterDefinition");

        int configuredCount = 0;

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            CharacterDefinition definition =
                AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                    path);

            if (definition == null)
                continue;

            definition.RunAutomaticSetup();
            configuredCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Configured {configuredCount} CharacterDefinition assets.");
    }

    #endregion

    #region Editor Types

    private readonly struct CharacterSetupData
    {
        public readonly string characterId;
        public readonly string displayName;
        public readonly CharacterType characterType;

        public CharacterSetupData(
            string characterId,
            string displayName,
            CharacterType characterType)
        {
            this.characterId =
                characterId;

            this.displayName =
                displayName;

            this.characterType =
                characterType;
        }
    }

    #endregion

#endif
}
