using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
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

    private const string DefinitionSuffix = "Character Definition";
    private const string MovementFolderName = "Movement Profiles";
    private const string AbilityFolderName = "Ability Profiles";
    private const string AnimatorFolderName = "Animator Profiles";
    private const string PresentationFolderName = "Presentation Profiles";

    #endregion

    #region Inspector

    [Header("Identity")]
    public string characterId;
    public string displayName;
    public CharacterType characterType = CharacterType.Speed;

    [Header("Profiles")]
    public CharacterMovementProfile movementProfile;
    public CharacterAbilityProfile abilityProfile;
    public CharacterAnimatorProfile animatorProfile;
    public CharacterPresentationProfile presentationProfile;

    [Header("Automatic Setup")]
    [SerializeField] private bool automaticSetup = true;
    [SerializeField] private bool applyTypePresets = true;
    [SerializeField] private bool autoAssignProjectAssets = true;
    [SerializeField] private bool replaceInvalidAssignments = true;
    [SerializeField] private bool logAutomaticAssignments = true;

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
            abilityProfile != null &&
            animatorProfile != null &&
            presentationProfile != null;
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
            setupScheduled ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        setupScheduled = true;
        EditorApplication.delayCall += RunScheduledSetup;
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
            return;

        applyingSetup = true;

        try
        {
            ConfigureIdentity();
            ApplyAutomaticAssetName();
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

            ValidateAssignedAssets();
            MarkAssetsDirty();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
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

    private static string NormalizeAssetName(
    string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return string.Empty;

        string normalizedName =
            assetName
                .Replace(
                    DefinitionSuffix,
                    string.Empty)
                .Replace(
                    "Definition",
                    string.Empty)
                .Trim();

        int separatorIndex =
            normalizedName.LastIndexOf(
                " - ",
                StringComparison.Ordinal);

        if (separatorIndex >= 0 &&
            separatorIndex + 3 < normalizedName.Length)
        {
            normalizedName =
                normalizedName.Substring(
                    separatorIndex + 3);
        }

        int firstSpaceIndex =
            normalizedName.IndexOf(' ');

        if (firstSpaceIndex > 0)
        {
            string possibleNumber =
                normalizedName.Substring(
                    0,
                    firstSpaceIndex);

            if (int.TryParse(
                    possibleNumber,
                    out _))
            {
                normalizedName =
                    normalizedName.Substring(
                        firstSpaceIndex + 1);
            }
        }

        return normalizedName
            .Trim()
            .ToLowerInvariant();
    }

    #endregion

    #region Identity

    private void ConfigureIdentity()
    {
        CharacterSetupData setupData =
            GetCharacterSetupData(
                NormalizeAssetName(name));

        characterId = setupData.characterId;
        displayName = setupData.displayName;
        characterType = setupData.characterType;
    }


    private void ApplyAutomaticAssetName()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        string assetPath =
            AssetDatabase.GetAssetPath(this);

        if (string.IsNullOrWhiteSpace(assetPath))
            return;

        string sortedAssetName =
            GetSortedAssetName();

        if (string.IsNullOrWhiteSpace(sortedAssetName) ||
            name == sortedAssetName)
        {
            return;
        }

        string renameError =
            AssetDatabase.RenameAsset(
                assetPath,
                sortedAssetName);

        if (!string.IsNullOrWhiteSpace(renameError))
        {
            Debug.LogError(
                $"CharacterDefinition could not rename '{name}' to " +
                $"'{sortedAssetName}'. Unity reported: {renameError}",
                this);

            return;
        }

        name = sortedAssetName;

        if (logAutomaticAssignments)
        {
            Debug.Log(
                $"CharacterDefinition automatically sorted asset as '{sortedAssetName}'.",
                this);
        }
    }

    private string GetSortedAssetName()
    {
        return characterId switch
        {
            "sonic" =>
                "01 Team Sonic - 01 Sonic Character Definition",

            "tails" =>
                "01 Team Sonic - 02 Tails Character Definition",

            "knuckles" =>
                "01 Team Sonic - 03 Knuckles Character Definition",

            "super-sonic" =>
                "01 Team Sonic - 04 Super Sonic Character Definition",

            "shadow" =>
                "02 Team Dark - 01 Shadow Character Definition",

            "rouge" =>
                "02 Team Dark - 02 Rouge Character Definition",

            "omega" =>
                "02 Team Dark - 03 Omega Character Definition",

            "amy" =>
                "03 Team Rose - 01 Amy Character Definition",

            "cream" =>
                "03 Team Rose - 02 Cream Character Definition",

            "big" =>
                "03 Team Rose - 03 Big Character Definition",

            "espio" =>
                "04 Team Chaotix - 01 Espio Character Definition",

            "charmy" =>
                "04 Team Chaotix - 02 Charmy Character Definition",

            "vector" =>
                "04 Team Chaotix - 03 Vector Character Definition",

            _ =>
                $"99 Unsorted - 99 {displayName} Character Definition"
        };
    }

    #endregion

    #region Profile Creation

    private void CreateAndAssignProfiles()
    {
        string definitionPath =
            AssetDatabase.GetAssetPath(this);

        string parentFolder =
            Path.GetDirectoryName(definitionPath)
            ?.Replace("\\", "/");

        if (string.IsNullOrWhiteSpace(parentFolder))
            return;

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

        movementProfile =
            LoadOrCreateAsset<CharacterMovementProfile>(
                movementFolder,
                $"{displayName} Movement Profile");

        abilityProfile =
            LoadOrCreateAsset<CharacterAbilityProfile>(
                abilityFolder,
                $"{displayName} Ability Profile");

        animatorProfile =
            LoadOrCreateAsset<CharacterAnimatorProfile>(
                animatorFolder,
                $"{displayName} Animator Profile");

        presentationProfile =
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

    #region Animator And Presentation Defaults

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

    private void ConfigurePresentationDefaults()
    {
        if (presentationProfile == null)
            return;

        presentationProfile.modelRootName = "ModelRoot";
        presentationProfile.footEffectPointName = "FootEffectPoint";
        presentationProfile.attackEffectPointName = "AttackEffectPoint";
    }

    #endregion

    #region Deterministic Asset Assignment

    private void AutoAssignProjectAssets()
    {
        string definitionPath =
            AssetDatabase.GetAssetPath(this);

        string definitionFolder =
            Path.GetDirectoryName(definitionPath)
            ?.Replace("\\", "/");

        AssetSearchContext context =
            new(
                characterId,
                displayName,
                characterType,
                definitionFolder);

        if (animatorProfile != null)
        {
            RuntimeAnimatorController controller =
                ResolveAnimatorController(context);

            Avatar avatar =
                ResolveCharacterAsset<Avatar>(
                    context,
                    GetExpectedAvatarNames());

            AssignAsset(
                ref animatorProfile.animatorController,
                controller,
                "Animator Controller");

            AssignAsset(
                ref animatorProfile.avatar,
                avatar,
                "Avatar");
        }

        if (presentationProfile != null)
        {
            GameObject modelPrefab =
                ResolveCharacterAsset<GameObject>(
                    context,
                    GetExpectedModelNames());

            AssignAsset(
                ref presentationProfile.characterModelPrefab,
                modelPrefab,
                "Character Model Prefab");

            AssignAsset(
                ref presentationProfile.jumpSound,
                ResolveAudioClip(context, "jump"),
                "Jump Sound");

            AssignAsset(
                ref presentationProfile.rollSound,
                ResolveAudioClip(context, "roll"),
                "Roll Sound");

            AssignAsset(
                ref presentationProfile.brakeSound,
                ResolveAudioClip(context, "brake"),
                "Brake Sound");

            AssignAsset(
                ref presentationProfile.hurtSound,
                ResolveAudioClip(context, "hurt"),
                "Hurt Sound");

            AssignAsset(
                ref presentationProfile.homingAttackSound,
                ResolveAudioClip(context, "homing"),
                "Homing Attack Sound");
        }
    }

    private RuntimeAnimatorController ResolveAnimatorController(
        AssetSearchContext context)
    {
        string[] expectedNames =
            GetExpectedAnimatorControllerNames();

        RuntimeAnimatorController controller =
            ResolveCharacterAsset<RuntimeAnimatorController>(
                context,
                expectedNames);

        if (controller == null)
        {
            Debug.LogWarning(
                $"CharacterDefinition '{name}' could not resolve an Animator Controller. " +
                $"Expected one of: {string.Join(", ", expectedNames)}.",
                this);

            return null;
        }

        if (!IsAnimatorControllerValidForCharacter(
                controller,
                context))
        {
            Debug.LogError(
                $"CharacterDefinition '{name}' rejected Animator Controller " +
                $"'{controller.name}' because it belongs to another character or type.",
                this);

            return null;
        }

        return controller;
    }

    private T ResolveCharacterAsset<T>(
        AssetSearchContext context,
        IReadOnlyList<string> expectedNames)
        where T : UnityEngine.Object
    {
        List<AssetCandidate<T>> candidates =
            CollectCandidates<T>();

        if (candidates.Count == 0)
            return null;

        AssetCandidate<T> bestCandidate =
            default;

        int bestScore =
            int.MinValue;

        foreach (AssetCandidate<T> candidate in candidates)
        {
            int score =
                ScoreCandidate(
                    candidate,
                    context,
                    expectedNames);

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestCandidate = candidate;
        }

        return bestScore > 0
            ? bestCandidate.asset
            : null;
    }

    private static List<AssetCandidate<T>> CollectCandidates<T>()
        where T : UnityEngine.Object
    {
        string[] guids =
            AssetDatabase.FindAssets(
                $"t:{typeof(T).Name}");

        List<AssetCandidate<T>> candidates =
            new();

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            T asset =
                AssetDatabase.LoadAssetAtPath<T>(
                    path);

            if (asset == null)
                continue;

            candidates.Add(
                new AssetCandidate<T>(
                    asset,
                    path));
        }

        return candidates;
    }

    private int ScoreCandidate<T>(
        AssetCandidate<T> candidate,
        AssetSearchContext context,
        IReadOnlyList<string> expectedNames)
        where T : UnityEngine.Object
    {
        string normalizedName =
            NormalizeSearchText(
                candidate.asset.name);

        string normalizedPath =
            NormalizeSearchText(
                candidate.path);

        if (IsRejectedCandidate(
                normalizedName,
                normalizedPath,
                context))
        {
            return int.MinValue;
        }

        int score = 0;

        for (int index = 0;
             index < expectedNames.Count;
             index++)
        {
            string expected =
                NormalizeSearchText(
                    expectedNames[index]);

            if (normalizedName == expected)
            {
                score = Mathf.Max(
                    score,
                    10000 - index * 100);
            }
            else if (normalizedName.StartsWith(expected))
            {
                score = Mathf.Max(
                    score,
                    7000 - index * 100);
            }
            else if (normalizedName.Contains(expected))
            {
                score = Mathf.Max(
                    score,
                    5000 - index * 100);
            }
        }

        if (!string.IsNullOrWhiteSpace(context.definitionFolder) &&
            candidate.path.StartsWith(
                context.definitionFolder,
                StringComparison.OrdinalIgnoreCase))
        {
            score += 2000;
        }

        string normalizedDisplayName =
            NormalizeSearchText(
                context.displayName);

        string normalizedCharacterId =
            NormalizeSearchText(
                context.characterId);

        if (!string.IsNullOrWhiteSpace(normalizedDisplayName) &&
            normalizedName.Contains(normalizedDisplayName))
        {
            score += 1200;
        }

        if (!string.IsNullOrWhiteSpace(normalizedCharacterId) &&
            normalizedName.Contains(normalizedCharacterId))
        {
            score += 1000;
        }

        if (MatchesCharacterTypeKeyword(
                normalizedName,
                context.characterType))
        {
            score += 800;
        }

        return score;
    }

    private bool IsRejectedCandidate(
        string normalizedName,
        string normalizedPath,
        AssetSearchContext context)
    {
        if (context.characterId == "sonic")
        {
            if (normalizedName.Contains("supersonic") ||
                normalizedPath.Contains("supersonic"))
            {
                return true;
            }
        }

        if (context.characterId == "super-sonic")
        {
            if (!normalizedName.Contains("supersonic") &&
                !normalizedPath.Contains("supersonic"))
            {
                return true;
            }
        }

        string[] otherCharacterTokens =
        {
            "tails",
            "milestailsprower",
            "knuckles",
            "shadow",
            "rouge",
            "omega",
            "amy",
            "cream",
            "big",
            "espio",
            "charmy",
            "vector"
        };

        string normalizedCharacterId =
            NormalizeSearchText(
                context.characterId);

        foreach (string token in otherCharacterTokens)
        {
            if (token == normalizedCharacterId)
                continue;

            if (normalizedName.Contains(token))
                return true;
        }

        return false;
    }

    private bool IsAnimatorControllerValidForCharacter(
        RuntimeAnimatorController controller,
        AssetSearchContext context)
    {
        if (controller == null)
            return false;

        string normalizedName =
            NormalizeSearchText(
                controller.name);

        if (context.characterId == "sonic")
        {
            return
                !normalizedName.Contains("supersonic") &&
                (normalizedName.Contains("speed") ||
                 normalizedName.Contains("sonic"));
        }

        if (context.characterId == "super-sonic")
        {
            return normalizedName.Contains("supersonic");
        }

        return !IsRejectedCandidate(
            normalizedName,
            string.Empty,
            context);
    }

    private AudioClip ResolveAudioClip(
        AssetSearchContext context,
        string actionTerm)
    {
        List<string> expectedNames =
            new();

        foreach (string characterName in GetCharacterNameVariants())
        {
            expectedNames.Add(
                $"{characterName} {actionTerm}");

            expectedNames.Add(
                $"{actionTerm} {characterName}");
        }

        expectedNames.Add(actionTerm);

        return ResolveCharacterAsset<AudioClip>(
            context,
            expectedNames);
    }

    private void AssignAsset<T>(
        ref T destination,
        T resolvedAsset,
        string displayLabel)
        where T : UnityEngine.Object
    {
        if (resolvedAsset == null)
            return;

        bool shouldReplace =
            destination == null ||
            replaceInvalidAssignments &&
            !IsExistingAssignmentValid(
                destination);

        if (!shouldReplace)
            return;

        if (destination == resolvedAsset)
            return;

        destination = resolvedAsset;

        if (logAutomaticAssignments)
        {
            Debug.Log(
                $"CharacterDefinition '{name}' assigned {displayLabel}: '{resolvedAsset.name}'.",
                this);
        }
    }

    private bool IsExistingAssignmentValid<T>(
        T existingAsset)
        where T : UnityEngine.Object
    {
        if (existingAsset == null)
            return false;

        if (existingAsset is RuntimeAnimatorController controller)
        {
            AssetSearchContext context =
                new(
                    characterId,
                    displayName,
                    characterType,
                    GetDefinitionFolder());

            return IsAnimatorControllerValidForCharacter(
                controller,
                context);
        }

        string normalizedName =
            NormalizeSearchText(
                existingAsset.name);

        if (characterId == "sonic" &&
            normalizedName.Contains("supersonic"))
        {
            return false;
        }

        if (characterId == "super-sonic" &&
            !normalizedName.Contains("supersonic"))
        {
            return false;
        }

        return true;
    }

    #endregion

    #region Expected Asset Names

    private string[] GetExpectedAnimatorControllerNames()
    {
        return characterId switch
        {
            "sonic" => new[]
            {
                "Speed Animator",
                "Speed",
                "Sonic Animator",
                "Sonic"
            },

            "super-sonic" => new[]
            {
                "Super Sonic (Fly)",
                "Super Sonic Fly",
                "Super Sonic Animator",
                "Super Sonic"
            },

            "tails" => new[]
            {
                "Fly Animator",
                "Fly",
                "Tails Animator",
                "Miles Tails Prower Animator"
            },

            "knuckles" => new[]
            {
                "Power Animator",
                "Power",
                "Knuckles Animator",
                "Knuckles the Echidna Animator"
            },

            _ => new[]
            {
                $"{displayName} Animator",
                $"{characterType} Animator",
                characterType.ToString(),
                displayName
            }
        };
    }

    private string[] GetExpectedAvatarNames()
    {
        return GetCharacterNameVariants()
            .SelectMany(
                value => new[]
                {
                    $"{value} Avatar",
                    value
                })
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string[] GetExpectedModelNames()
    {
        return GetCharacterNameVariants()
            .SelectMany(
                value => new[]
                {
                    $"{value} Model",
                    $"{value} Prefab",
                    value
                })
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string[] GetCharacterNameVariants()
    {
        return new[]
        {
            displayName,
            characterId,
            name.Replace(
                DefinitionSuffix,
                string.Empty),
            characterId?.Replace("-", " ")
        }
        .Where(
            value =>
                !string.IsNullOrWhiteSpace(value))
        .Distinct(
            StringComparer.OrdinalIgnoreCase)
        .ToArray();
    }

    #endregion

    #region Validation

    private void ValidateAssignedAssets()
    {
        bool valid = true;

        if (movementProfile == null)
        {
            Debug.LogError(
                $"CharacterDefinition '{name}' has no Movement Profile.",
                this);

            valid = false;
        }

        if (abilityProfile == null)
        {
            Debug.LogError(
                $"CharacterDefinition '{name}' has no Ability Profile.",
                this);

            valid = false;
        }

        if (animatorProfile == null)
        {
            Debug.LogError(
                $"CharacterDefinition '{name}' has no Animator Profile.",
                this);

            valid = false;
        }
        else if (animatorProfile.animatorController == null)
        {
            Debug.LogWarning(
                $"CharacterDefinition '{name}' has no Animator Controller assigned.",
                this);
        }
        else
        {
            AssetSearchContext context =
                new(
                    characterId,
                    displayName,
                    characterType,
                    GetDefinitionFolder());

            if (!IsAnimatorControllerValidForCharacter(
                    animatorProfile.animatorController,
                    context))
            {
                Debug.LogError(
                    $"CharacterDefinition '{name}' has an invalid Animator Controller: " +
                    $"'{animatorProfile.animatorController.name}'.",
                    this);

                valid = false;
            }
        }

        if (presentationProfile == null)
        {
            Debug.LogError(
                $"CharacterDefinition '{name}' has no Presentation Profile.",
                this);

            valid = false;
        }

        if (valid &&
            logAutomaticAssignments)
        {
            Debug.Log(
                $"CharacterDefinition '{name}' configured successfully.",
                this);
        }
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
                return new(
                    "sonic",
                    "Sonic the Hedgehog",
                    CharacterType.Speed);

            case "tails":
            case "miles tails prower":
                return new(
                    "tails",
                    "Miles Tails Prower",
                    CharacterType.Fly);

            case "knuckles":
            case "knuckles the echidna":
                return new(
                    "knuckles",
                    "Knuckles the Echidna",
                    CharacterType.Power);

            case "shadow":
            case "shadow the hedgehog":
                return new(
                    "shadow",
                    "Shadow the Hedgehog",
                    CharacterType.Speed);

            case "rouge":
            case "rouge the bat":
                return new(
                    "rouge",
                    "Rouge the Bat",
                    CharacterType.Fly);

            case "omega":
            case "e-123 omega":
            case "e123 omega":
                return new(
                    "omega",
                    "E-123 Omega",
                    CharacterType.Power);

            case "amy":
            case "amy rose":
                return new(
                    "amy",
                    "Amy Rose",
                    CharacterType.Speed);

            case "cream":
            case "cream the rabbit":
                return new(
                    "cream",
                    "Cream the Rabbit",
                    CharacterType.Fly);

            case "big":
            case "big the cat":
                return new(
                    "big",
                    "Big the Cat",
                    CharacterType.Power);

            case "espio":
            case "espio the chameleon":
                return new(
                    "espio",
                    "Espio the Chameleon",
                    CharacterType.Speed);

            case "charmy":
            case "charmy bee":
                return new(
                    "charmy",
                    "Charmy Bee",
                    CharacterType.Fly);

            case "vector":
            case "vector the crocodile":
                return new(
                    "vector",
                    "Vector the Crocodile",
                    CharacterType.Power);

            case "super sonic":
                return new(
                    "super-sonic",
                    "Super Sonic",
                    CharacterType.Special);

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
    

    #endregion

    #region Asset Utilities

    private string GetDefinitionFolder()
    {
        string definitionPath =
            AssetDatabase.GetAssetPath(this);

        return Path.GetDirectoryName(
                definitionPath)
            ?.Replace("\\", "/");
    }

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
            return existingAsset;

        T newAsset =
            CreateInstance<T>();

        newAsset.name =
            assetName;

        AssetDatabase.CreateAsset(
            newAsset,
            assetPath);

        return newAsset;
    }

    private static bool MatchesCharacterTypeKeyword(
        string normalizedName,
        CharacterType type)
    {
        return type switch
        {
            CharacterType.Speed =>
                normalizedName.Contains("speed"),

            CharacterType.Fly =>
                normalizedName.Contains("fly"),

            CharacterType.Power =>
                normalizedName.Contains("power"),

            CharacterType.Special =>
                normalizedName.Contains("special") ||
                normalizedName.Contains("super"),

            _ =>
                false
        };
    }

    private static string NormalizeSearchText(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("(", string.Empty)
            .Replace(")", string.Empty)
            .ToLowerInvariant();
    }

    private void MarkAssetsDirty()
    {
        EditorUtility.SetDirty(this);

        if (movementProfile != null)
            EditorUtility.SetDirty(movementProfile);

        if (abilityProfile != null)
            EditorUtility.SetDirty(abilityProfile);

        if (animatorProfile != null)
            EditorUtility.SetDirty(animatorProfile);

        if (presentationProfile != null)
            EditorUtility.SetDirty(presentationProfile);
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
            this.characterId = characterId;
            this.displayName = displayName;
            this.characterType = characterType;
        }
    }

    private readonly struct AssetSearchContext
    {
        public readonly string characterId;
        public readonly string displayName;
        public readonly CharacterType characterType;
        public readonly string definitionFolder;

        public AssetSearchContext(
            string characterId,
            string displayName,
            CharacterType characterType,
            string definitionFolder)
        {
            this.characterId = characterId;
            this.displayName = displayName;
            this.characterType = characterType;
            this.definitionFolder = definitionFolder;
        }
    }

    private readonly struct AssetCandidate<T>
        where T : UnityEngine.Object
    {
        public readonly T asset;
        public readonly string path;

        public AssetCandidate(
            T asset,
            string path)
        {
            this.asset = asset;
            this.path = path;
        }
    }

    #endregion

#endif
}
