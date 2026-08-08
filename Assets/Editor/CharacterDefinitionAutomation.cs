#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CharacterDefinitionAutomation
{
    #region Constants

    private const string TeamsRoot =
        "Assets/Resources/Teams";

    private const string DefinitionsFolder =
        "Character Definitions";

    private const string MovementProfilesFolder =
        "Movement Profiles";

    private const string AbilityProfilesFolder =
        "Ability Profiles";

    private const string AnimatorProfilesFolder =
        "Animator Profiles";

    private const string PresentationProfilesFolder =
        "Presentation Profiles";

    #endregion

    #region Character Registry

    private static readonly string[] SupportedCharacterIds =
    {
        "sonic",
        "tails",
        "knuckles",

        "shadow",
        "rouge",
        "omega",

        "amy",
        "cream",
        "big",

        "espio",
        "charmy",
        "vector",

        "super-sonic"
    };

    #endregion

    #region Menu Commands

    [MenuItem(
        "Sonic Heroes/Characters/Rebuild Everything")]
    private static void RebuildEverything()
    {
        if (!CanRunAutomation())
            return;

        bool confirmed =
            EditorUtility.DisplayDialog(
                "Rebuild Character Definitions",
                "All generated CharacterDefinition and profile assets " +
                "inside Assets/Resources/Teams will be removed and rebuilt.\n\n" +
                "Character models, prefabs, animation controllers, scenes, " +
                "audio, textures, materials, scripts, and unrelated assets " +
                "will not be deleted.",
                "Rebuild",
                "Cancel");

        if (!confirmed)
            return;

        try
        {
            EnsureRootFolder();

            DeleteGeneratedAssets();

            CreateAllDefinitions();

            SaveAndRefresh();

            Debug.Log(
                "CharacterDefinition automation rebuilt all supported characters.");
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception);
        }
    }

    [MenuItem(
        "Sonic Heroes/Characters/Repair Existing Definitions")]
    private static void RepairExistingDefinitions()
    {
        if (!CanRunAutomation())
            return;

        try
        {
            EnsureRootFolder();

            int repaired =
                RepairAllDefinitions();

            SaveAndRefresh();

            Debug.Log(
                $"CharacterDefinition automation repaired {repaired} definitions.");
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception);
        }
    }

    [MenuItem(
        "Sonic Heroes/Characters/Validate Definitions")]
    private static void ValidateDefinitions()
    {
        if (!CanRunAutomation())
            return;

        string[] guids =
            FindDefinitionGuids();

        int validCount = 0;
        int invalidCount = 0;

        foreach (string guid in guids)
        {
            CharacterDefinition definition =
                LoadDefinition(
                    guid);

            if (definition == null)
                continue;

            if (definition.IsValid())
            {
                validCount++;

                continue;
            }

            invalidCount++;

            Debug.LogWarning(
                $"CharacterDefinition '{definition.name}' is invalid.",
                definition);
        }

        Debug.Log(
            $"CharacterDefinition validation complete. " +
            $"Valid: {validCount}. Invalid: {invalidCount}.");
    }

    #endregion

    #region Automation Validation

    private static bool CanRunAutomation()
    {
        if (EditorApplication.isPlaying ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning(
                "CharacterDefinition automation cannot run while entering or using Play Mode.");

            return false;
        }

        if (EditorApplication.isCompiling)
        {
            Debug.LogWarning(
                "CharacterDefinition automation cannot run while Unity is compiling.");

            return false;
        }

        return true;
    }

    #endregion

    #region Rebuild

    private static void CreateAllDefinitions()
    {
        foreach (string characterId
                 in SupportedCharacterIds)
        {
            if (!CharacterDefinition.TryGetIdentity(
                    characterId,
                    out CharacterDefinition.CharacterIdentity identity))
            {
                Debug.LogError(
                    $"Character registry could not resolve '{characterId}'.");

                continue;
            }

            CreateDefinition(
                identity);
        }
    }

    private static CharacterDefinition CreateDefinition(
        CharacterDefinition.CharacterIdentity identity)
    {
        string teamFolder =
            GetTeamFolder(
                identity.Team);

        string definitionsFolder =
            EnsureFolder(
                teamFolder,
                DefinitionsFolder);

        string assetName =
            $"{identity.ShortName} Character Definition";

        string assetPath =
            $"{definitionsFolder}/{assetName}.asset";

        CharacterDefinition existing =
            AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                assetPath);

        CharacterDefinition definition =
            existing;

        if (definition == null)
        {
            definition =
                ScriptableObject.CreateInstance<CharacterDefinition>();

            definition.name =
                assetName;

            AssetDatabase.CreateAsset(
                definition,
                assetPath);
        }

        if (!definition.ConfigureKnownCharacter(
                identity.CharacterId))
        {
            Debug.LogError(
                $"Could not configure '{identity.DisplayName}'.",
                definition);

            return definition;
        }

        ConfigureDefinition(
            definition,
            identity);

        EditorUtility.SetDirty(
            definition);

        return definition;
    }

    #endregion

    #region Repair

    private static int RepairAllDefinitions()
    {
        string[] guids =
            FindDefinitionGuids();

        int repairedCount = 0;

        foreach (string guid in guids)
        {
            CharacterDefinition definition =
                LoadDefinition(
                    guid);

            if (definition == null)
                continue;

            if (!definition.RepairRuntimeIdentity())
            {
                Debug.LogWarning(
                    $"CharacterDefinition '{definition.name}' could not resolve a supported identity.",
                    definition);

                continue;
            }

            if (!CharacterDefinition.TryGetIdentity(
                    definition.characterId,
                    out CharacterDefinition.CharacterIdentity identity))
            {
                continue;
            }

            ConfigureDefinition(
                definition,
                identity);

            RenameDefinitionAsset(
                definition,
                identity);

            EditorUtility.SetDirty(
                definition);

            repairedCount++;
        }

        return repairedCount;
    }

    #endregion

    #region Definition Configuration

    private static void ConfigureDefinition(
        CharacterDefinition definition,
        CharacterDefinition.CharacterIdentity identity)
    {
        if (definition == null)
            return;

        CreateAndAssignProfiles(
            definition,
            identity);

        ApplyMovementPreset(
            definition);

        ApplyAbilityPreset(
            definition);

        ConfigureAnimatorProfile(
            definition.animatorProfile);

        ConfigurePresentationProfile(
            definition.presentationProfile);

        ResolveProjectAssets(
            definition,
            identity);

        MarkDefinitionAssetsDirty(
            definition);
    }

    #endregion

    #region Profile Creation

    private static void CreateAndAssignProfiles(
        CharacterDefinition definition,
        CharacterDefinition.CharacterIdentity identity)
    {
        string teamFolder =
            GetTeamFolder(
                identity.Team);

        string movementFolder =
            EnsureFolder(
                teamFolder,
                MovementProfilesFolder);

        string abilityFolder =
            EnsureFolder(
                teamFolder,
                AbilityProfilesFolder);

        string animatorFolder =
            EnsureFolder(
                teamFolder,
                AnimatorProfilesFolder);

        string presentationFolder =
            EnsureFolder(
                teamFolder,
                PresentationProfilesFolder);

        definition.movementProfile =
            LoadOrCreateProfile<CharacterMovementProfile>(
                movementFolder,
                $"{identity.ShortName} Movement Profile");

        definition.abilityProfile =
            LoadOrCreateProfile<CharacterAbilityProfile>(
                abilityFolder,
                $"{identity.ShortName} Ability Profile");

        definition.animatorProfile =
            LoadOrCreateProfile<CharacterAnimatorProfile>(
                animatorFolder,
                $"{identity.ShortName} Animator Profile");

        definition.presentationProfile =
            LoadOrCreateProfile<CharacterPresentationProfile>(
                presentationFolder,
                $"{identity.ShortName} Presentation Profile");
    }

    private static T LoadOrCreateProfile<T>(
        string folder,
        string assetName)
        where T : ScriptableObject
    {
        string assetPath =
            $"{folder}/{assetName}.asset";

        T existing =
            AssetDatabase.LoadAssetAtPath<T>(
                assetPath);

        if (existing != null)
            return existing;

        T profile =
            ScriptableObject.CreateInstance<T>();

        profile.name =
            assetName;

        AssetDatabase.CreateAsset(
            profile,
            assetPath);

        return profile;
    }

    #endregion

    #region Movement Presets

    private static void ApplyMovementPreset(
        CharacterDefinition definition)
    {
        if (definition == null ||
            definition.movementProfile == null)
        {
            return;
        }

        switch (definition.characterType)
        {
            case CharacterDefinition.CharacterType.Speed:
                ApplySpeedMovement(
                    definition.movementProfile);
                break;

            case CharacterDefinition.CharacterType.Fly:
                ApplyFlyMovement(
                    definition.movementProfile);
                break;

            case CharacterDefinition.CharacterType.Power:
                ApplyPowerMovement(
                    definition.movementProfile);
                break;

            case CharacterDefinition.CharacterType.Special:
                ApplySpecialMovement(
                    definition.movementProfile);
                break;
        }

        EditorUtility.SetDirty(
            definition.movementProfile);
    }

    private static void ApplySpeedMovement(
        CharacterMovementProfile profile)
    {
        if (profile == null)
            return;

        profile.runSpeed = 24f;
        profile.acceleration = 48f;
        profile.deceleration = 54f;
        profile.turnSpeed = 16f;
        profile.brakingForce = 64f;

        profile.jumpForce = 10f;

        profile.airSpeed = 17f;
        profile.airControl = 24f;

        profile.rollingSpeed = 30f;
        profile.rollingTurnSpeed = 7f;

        profile.homingAttackSpeed = 38f;
        profile.homingAttackDuration = 0.5f;
        profile.homingRange = 22f;

        profile.flyingSpeed = 0f;
        profile.flyingVerticalSpeed = 0f;
    }

    private static void ApplyFlyMovement(
        CharacterMovementProfile profile)
    {
        if (profile == null)
            return;

        profile.runSpeed = 18f;
        profile.acceleration = 36f;
        profile.deceleration = 46f;
        profile.turnSpeed = 14f;
        profile.brakingForce = 54f;

        profile.jumpForce = 9f;

        profile.airSpeed = 16f;
        profile.airControl = 26f;

        profile.rollingSpeed = 22f;
        profile.rollingTurnSpeed = 6f;

        profile.homingAttackSpeed = 30f;
        profile.homingAttackDuration = 0.45f;
        profile.homingRange = 18f;

        profile.flyingSpeed = 17f;
        profile.flyingVerticalSpeed = 11f;
    }

    private static void ApplyPowerMovement(
        CharacterMovementProfile profile)
    {
        if (profile == null)
            return;

        profile.runSpeed = 16f;
        profile.acceleration = 32f;
        profile.deceleration = 44f;
        profile.turnSpeed = 12f;
        profile.brakingForce = 72f;

        profile.jumpForce = 8.5f;

        profile.airSpeed = 13f;
        profile.airControl = 16f;

        profile.rollingSpeed = 21f;
        profile.rollingTurnSpeed = 5f;

        profile.homingAttackSpeed = 27f;
        profile.homingAttackDuration = 0.45f;
        profile.homingRange = 16f;

        profile.flyingSpeed = 0f;
        profile.flyingVerticalSpeed = 0f;
    }

    private static void ApplySpecialMovement(
        CharacterMovementProfile profile)
    {
        if (profile == null)
            return;

        ApplySpeedMovement(
            profile);

        profile.runSpeed = 28f;
        profile.acceleration = 56f;

        profile.airSpeed = 21f;

        profile.rollingSpeed = 34f;

        profile.homingAttackSpeed = 44f;
        profile.homingRange = 26f;
    }

    #endregion

    #region Ability Presets

    private static void ApplyAbilityPreset(
        CharacterDefinition definition)
    {
        if (definition == null ||
            definition.abilityProfile == null)
        {
            return;
        }

        CharacterAbilityProfile profile =
            definition.abilityProfile;

        ResetAbilities(
            profile);

        switch (definition.characterType)
        {
            case CharacterDefinition.CharacterType.Speed:
                ApplySpeedAbilities(
                    profile);
                break;

            case CharacterDefinition.CharacterType.Fly:
                ApplyFlyAbilities(
                    profile);
                break;

            case CharacterDefinition.CharacterType.Power:
                ApplyPowerAbilities(
                    profile);
                break;

            case CharacterDefinition.CharacterType.Special:
                ApplySpecialAbilities(
                    profile);
                break;
        }

        ApplyCharacterSpecificAbilities(
            definition,
            profile);

        EditorUtility.SetDirty(
            profile);
    }

    private static void ResetAbilities(
        CharacterAbilityProfile profile)
    {
        if (profile == null)
            return;

        profile.canJump = true;
        profile.canRoll = true;
        profile.canHomingAttack = false;
        profile.canGrind = true;

        profile.canFly = false;
        profile.canGlide = false;
        profile.canPowerAction = false;
        profile.canBrake = true;

        profile.usesSlopeMomentum = true;

        profile.canLightDash = false;
        profile.canWallJump = false;
        profile.canTriangleJump = false;
        profile.canRocketAccel = false;
        profile.canInvisibility = false;

        profile.canThunderShoot = false;
        profile.canTeamFlight = false;

        profile.canFireDunk = false;
        profile.canBodySlam = false;

        profile.canHammerAttack = false;

        profile.canChaosControl = false;
        profile.canChaosAttack = false;

        profile.canSuperForm = false;

        profile.canTeamBlast = true;
    }

    private static void ApplySpeedAbilities(
        CharacterAbilityProfile profile)
    {
        if (profile == null)
            return;

        profile.canHomingAttack = true;
        profile.canLightDash = true;
        profile.canWallJump = true;
        profile.canTriangleJump = true;
    }

    private static void ApplyFlyAbilities(
        CharacterAbilityProfile profile)
    {
        if (profile == null)
            return;

        profile.canFly = true;
        profile.canThunderShoot = true;
        profile.canTeamFlight = true;
    }

    private static void ApplyPowerAbilities(
        CharacterAbilityProfile profile)
    {
        if (profile == null)
            return;

        profile.canPowerAction = true;
        profile.canFireDunk = true;
        profile.canBodySlam = true;
    }

    private static void ApplySpecialAbilities(
        CharacterAbilityProfile profile)
    {
        if (profile == null)
            return;

        profile.canHomingAttack = true;
        profile.canLightDash = true;

        profile.canChaosControl = true;
        profile.canChaosAttack = true;

        profile.canSuperForm = true;
    }

    private static void ApplyCharacterSpecificAbilities(
        CharacterDefinition definition,
        CharacterAbilityProfile profile)
    {
        if (definition == null ||
            profile == null)
        {
            return;
        }

        switch (definition.characterId)
        {
            case "sonic":
                profile.canRocketAccel = true;
                break;

            case "shadow":
                profile.canChaosControl = true;
                profile.canChaosAttack = true;
                break;

            case "amy":
                profile.canHammerAttack = true;
                break;

            case "espio":
                profile.canInvisibility = true;
                break;

            case "knuckles":
                profile.canGlide = true;
                break;

            case "rouge":
                profile.canGlide = true;
                break;

            case "super-sonic":
                profile.canSuperForm = true;
                profile.canChaosControl = true;
                profile.canChaosAttack = true;
                break;
        }
    }

    #endregion

    #region Animator Configuration

    private static void ConfigureAnimatorProfile(
        CharacterAnimatorProfile profile)
    {
        if (profile == null)
            return;

        profile.stateParameter =
            "State";

        profile.groundedParameter =
            "Grounded";

        profile.speedParameter =
            "Speed";

        profile.verticalSpeedParameter =
            "VerticalSpeed";

        profile.jumpTrigger =
            "Jump";

        profile.rollTrigger =
            "Roll";

        profile.brakingTrigger =
            "Brake";

        profile.hurtTrigger =
            "Hurt";

        profile.homingAttackTrigger =
            "HomingAttack";

        EditorUtility.SetDirty(
            profile);
    }

    #endregion

    #region Presentation Configuration

    private static void ConfigurePresentationProfile(
        CharacterPresentationProfile profile)
    {
        if (profile == null)
            return;

        profile.modelRootName =
            "ModelRoot";

        profile.footEffectPointName =
            "FootEffectPoint";

        profile.attackEffectPointName =
            "AttackEffectPoint";

        EditorUtility.SetDirty(
            profile);
    }

    #endregion

    #region Asset Resolution

    private static void ResolveProjectAssets(
        CharacterDefinition definition,
        CharacterDefinition.CharacterIdentity identity)
    {
        if (definition == null)
            return;

        ResolveAnimatorAssets(
            definition.animatorProfile,
            identity);

        ResolvePresentationAssets(
            definition.presentationProfile,
            identity);
    }

    private static void ResolveAnimatorAssets(
        CharacterAnimatorProfile profile,
        CharacterDefinition.CharacterIdentity identity)
    {
        if (profile == null)
            return;

        profile.animatorController ??=
            FindBestAsset<RuntimeAnimatorController>(
                identity);

        profile.avatar ??=
            FindBestAsset<Avatar>(
                identity);

        EditorUtility.SetDirty(
            profile);
    }

    private static void ResolvePresentationAssets(
        CharacterPresentationProfile profile,
        CharacterDefinition.CharacterIdentity identity)
    {
        if (profile == null)
            return;

        profile.characterModelPrefab ??=
            FindBestAsset<GameObject>(
                identity);

        if (identity.CharacterId == "cream")
        {
            profile.companionPrefab ??=
                FindBestAssetByName<GameObject>(
                    "Cheese");

            profile.companionAnchorName =
                "CompanionAnchor";
        }

        profile.jumpSound ??=
            FindBestAudioClip(
                identity,
                "jump");

        profile.rollSound ??=
            FindBestAudioClip(
                identity,
                "roll");

        profile.brakeSound ??=
            FindBestAudioClip(
                identity,
                "brake");

        profile.hurtSound ??=
            FindBestAudioClip(
                identity,
                "hurt");

        profile.homingAttackSound ??=
            FindBestAudioClip(
                identity,
                "homing");

        EditorUtility.SetDirty(
            profile);
    }

    private static T FindBestAssetByName<T>(
    string searchName)
    where T : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(searchName))
            return null;

        string normalizedSearchName =
            Normalize(
                searchName);

        string[] guids =
            AssetDatabase.FindAssets(
                $"t:{typeof(T).Name}");

        T bestAsset = null;
        int bestScore = 0;

        foreach (string guid in guids)
        {
            string assetPath =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            T asset =
                AssetDatabase.LoadAssetAtPath<T>(
                    assetPath);

            if (asset == null)
                continue;

            string normalizedName =
                Normalize(
                    asset.name);

            int score = 0;

            if (normalizedName ==
                normalizedSearchName)
            {
                score = 10000;
            }
            else if (normalizedName.StartsWith(
                normalizedSearchName,
                StringComparison.Ordinal))
            {
                score = 7000;
            }
            else if (normalizedName.Contains(
                normalizedSearchName,
                StringComparison.Ordinal))
            {
                score = 5000;
            }

            if (score <= bestScore)
                continue;

            bestScore =
                score;

            bestAsset =
                asset;
        }

        return bestAsset;
    }

    private static T FindBestAsset<T>(
        CharacterDefinition.CharacterIdentity identity)
        where T : UnityEngine.Object
    {
        string[] guids =
            AssetDatabase.FindAssets(
                $"t:{typeof(T).Name}");

        T bestAsset = null;

        int bestScore =
            0;

        foreach (string guid in guids)
        {
            string assetPath =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            T asset =
                AssetDatabase.LoadAssetAtPath<T>(
                    assetPath);

            if (asset == null)
                continue;

            int score =
                ScoreAssetCandidate(
                    asset.name,
                    assetPath,
                    identity);

            if (score <= bestScore)
                continue;

            bestScore =
                score;

            bestAsset =
                asset;
        }

        return bestAsset;
    }

    private static AudioClip FindBestAudioClip(
        CharacterDefinition.CharacterIdentity identity,
        string action)
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:AudioClip");

        AudioClip bestClip = null;

        int bestScore =
            0;

        string normalizedAction =
            Normalize(
                action);

        foreach (string guid in guids)
        {
            string assetPath =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            AudioClip clip =
                AssetDatabase.LoadAssetAtPath<AudioClip>(
                    assetPath);

            if (clip == null)
                continue;

            string normalizedName =
                Normalize(
                    clip.name);

            if (!normalizedName.Contains(
                    normalizedAction))
            {
                continue;
            }

            int score =
                ScoreAssetCandidate(
                    clip.name,
                    assetPath,
                    identity);

            score += 1000;

            if (score <= bestScore)
                continue;

            bestScore =
                score;

            bestClip =
                clip;
        }

        return bestClip;
    }

    #endregion

    #region Asset Scoring

    private static int ScoreAssetCandidate(
        string assetName,
        string assetPath,
        CharacterDefinition.CharacterIdentity identity)
    {
        string normalizedName =
            Normalize(
                assetName);

        string normalizedPath =
            Normalize(
                assetPath);

        string normalizedShortName =
            Normalize(
                identity.ShortName);

        string normalizedDisplayName =
            Normalize(
                identity.DisplayName);

        string normalizedId =
            Normalize(
                identity.CharacterId);

        if (ContainsAnotherCharacter(
                normalizedName,
                identity))
        {
            return 0;
        }

        int score =
            0;

        if (normalizedName ==
            normalizedShortName)
        {
            score += 10000;
        }

        if (normalizedName ==
            normalizedDisplayName)
        {
            score += 10000;
        }

        if (normalizedName.Contains(
                normalizedDisplayName))
        {
            score += 6000;
        }

        if (normalizedName.Contains(
                normalizedShortName))
        {
            score += 5000;
        }

        if (normalizedName.Contains(
                normalizedId))
        {
            score += 4000;
        }

        if (normalizedPath.Contains(
                normalizedDisplayName))
        {
            score += 2500;
        }

        if (normalizedPath.Contains(
                normalizedShortName))
        {
            score += 2000;
        }

        if (normalizedPath.Contains(
                normalizedId))
        {
            score += 1500;
        }

        if (ContainsTypeKeyword(
                normalizedName,
                identity.CharacterType))
        {
            score += 500;
        }

        return score;
    }

    private static bool ContainsAnotherCharacter(
        string normalizedName,
        CharacterDefinition.CharacterIdentity identity)
    {
        string[] knownCharacters =
        {
            "sonic",
            "supersonic",
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

        string expectedShortName =
            Normalize(
                identity.ShortName);

        foreach (string knownCharacter
                 in knownCharacters)
        {
            if (knownCharacter ==
                expectedShortName)
            {
                continue;
            }

            if (identity.CharacterId ==
                    "super-sonic" &&
                knownCharacter ==
                    "sonic")
            {
                continue;
            }

            if (normalizedName.Contains(
                    knownCharacter))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsTypeKeyword(
        string normalizedName,
        CharacterDefinition.CharacterType type)
    {
        return type switch
        {
            CharacterDefinition.CharacterType.Speed =>
                normalizedName.Contains(
                    "speed"),

            CharacterDefinition.CharacterType.Fly =>
                normalizedName.Contains(
                    "fly"),

            CharacterDefinition.CharacterType.Power =>
                normalizedName.Contains(
                    "power"),

            CharacterDefinition.CharacterType.Special =>
                normalizedName.Contains(
                    "special") ||
                normalizedName.Contains(
                    "super"),

            _ =>
                false
        };
    }

    #endregion

    #region Generated Asset Cleanup

    private static void DeleteGeneratedAssets()
    {
        DeleteAssets<CharacterDefinition>();
        DeleteAssets<CharacterMovementProfile>();
        DeleteAssets<CharacterAbilityProfile>();
        DeleteAssets<CharacterAnimatorProfile>();
        DeleteAssets<CharacterPresentationProfile>();
    }

    private static void DeleteAssets<T>()
        where T : UnityEngine.Object
    {
        if (!AssetDatabase.IsValidFolder(
                TeamsRoot))
        {
            return;
        }

        string[] guids =
            AssetDatabase.FindAssets(
                $"t:{typeof(T).Name}",
                new[]
                {
                    TeamsRoot
                });

        foreach (string guid in guids)
        {
            string assetPath =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            if (!IsGeneratedAssetPath(
                    assetPath,
                    typeof(T)))
            {
                continue;
            }

            AssetDatabase.DeleteAsset(
                assetPath);
        }
    }

    private static bool IsGeneratedAssetPath(
    string assetPath,
    Type assetType)
    {
        if (string.IsNullOrWhiteSpace(
                assetPath))
        {
            return false;
        }

        if (!assetPath.StartsWith(
                TeamsRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        /*
        CharacterDefinitions anywhere beneath TeamsRoot
        are owned by the automatic character system.
        
        This deliberately removes both:
        
        Legacy:
        Team Sonic/
             01 Team Sonic - 01 Sonic Character Definition.asset
        
         New:
         Team Sonic/
            Character Definitions/
                 Sonic Character Definition.asset
        */
        if (assetType ==
            typeof(CharacterDefinition))
        {
            return true;
        }

        if (assetType ==
            typeof(CharacterMovementProfile))
        {
            return assetPath.Contains(
                $"/{MovementProfilesFolder}/",
                StringComparison.OrdinalIgnoreCase);
        }

        if (assetType ==
            typeof(CharacterAbilityProfile))
        {
            return assetPath.Contains(
                $"/{AbilityProfilesFolder}/",
                StringComparison.OrdinalIgnoreCase);
        }

        if (assetType ==
            typeof(CharacterAnimatorProfile))
        {
            return assetPath.Contains(
                $"/{AnimatorProfilesFolder}/",
                StringComparison.OrdinalIgnoreCase);
        }

        if (assetType ==
            typeof(CharacterPresentationProfile))
        {
            return assetPath.Contains(
                $"/{PresentationProfilesFolder}/",
                StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    #endregion

    #region Asset Naming

    private static void RenameDefinitionAsset(
        CharacterDefinition definition,
        CharacterDefinition.CharacterIdentity identity)
    {
        if (definition == null)
            return;

        string assetPath =
            AssetDatabase.GetAssetPath(
                definition);

        if (string.IsNullOrWhiteSpace(
                assetPath))
        {
            return;
        }

        string expectedName =
            $"{identity.ShortName} Character Definition";

        if (string.Equals(
                definition.name,
                expectedName,
                StringComparison.Ordinal))
        {
            return;
        }

        string error =
            AssetDatabase.RenameAsset(
                assetPath,
                expectedName);

        if (!string.IsNullOrWhiteSpace(
                error))
        {
            Debug.LogError(
                $"Could not rename CharacterDefinition '{definition.name}' " +
                $"to '{expectedName}': {error}",
                definition);

            return;
        }

        definition.name =
            expectedName;
    }

    #endregion

    #region Folder Management

    private static void EnsureRootFolder()
    {
        EnsureFolderPath(
            TeamsRoot);
    }

    private static string GetTeamFolder(
        CharacterDefinition.Team team)
    {
        string folderName =
            team switch
            {
                CharacterDefinition.Team.TeamSonic =>
                    "Team Sonic",

                CharacterDefinition.Team.TeamDark =>
                    "Team Dark",

                CharacterDefinition.Team.TeamRose =>
                    "Team Rose",

                CharacterDefinition.Team.TeamChaotix =>
                    "Team Chaotix",

                CharacterDefinition.Team.Special =>
                    "Special",

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(team),
                        team,
                        null)
            };

        return EnsureFolder(
            TeamsRoot,
            folderName);
    }

    private static string EnsureFolder(
        string parentFolder,
        string folderName)
    {
        if (!AssetDatabase.IsValidFolder(
                parentFolder))
        {
            EnsureFolderPath(
                parentFolder);
        }

        string folderPath =
            $"{parentFolder}/{folderName}";

        if (!AssetDatabase.IsValidFolder(
                folderPath))
        {
            AssetDatabase.CreateFolder(
                parentFolder,
                folderName);
        }

        return folderPath;
    }

    private static void EnsureFolderPath(
        string folderPath)
    {
        if (AssetDatabase.IsValidFolder(
                folderPath))
        {
            return;
        }

        string[] segments =
            folderPath.Split('/');

        if (segments.Length == 0 ||
            segments[0] != "Assets")
        {
            throw new InvalidOperationException(
                $"Invalid Unity asset folder path: '{folderPath}'.");
        }

        string currentPath =
            segments[0];

        for (int index = 1;
                index < segments.Length;
                index++)
        {
            string nextPath =
                $"{currentPath}/{segments[index]}";

            if (!AssetDatabase.IsValidFolder(
                    nextPath))
            {
                AssetDatabase.CreateFolder(
                    currentPath,
                    segments[index]);
            }

            currentPath =
                nextPath;
        }
    }

    #endregion

    #region Asset Lookup

    private static string[] FindDefinitionGuids()
    {
        if (!AssetDatabase.IsValidFolder(
                TeamsRoot))
        {
            return Array.Empty<string>();
        }

        return AssetDatabase.FindAssets(
            "t:CharacterDefinition",
            new[]
            {
                TeamsRoot
            });
    }

    private static CharacterDefinition LoadDefinition(
        string guid)
    {
        if (string.IsNullOrWhiteSpace(guid))
            return null;

        string assetPath =
            AssetDatabase.GUIDToAssetPath(
                guid);

        if (string.IsNullOrWhiteSpace(
                assetPath))
        {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
            assetPath);
    }

    #endregion

    #region Dirty State

    private static void MarkDefinitionAssetsDirty(
        CharacterDefinition definition)
    {
        if (definition == null)
            return;

        EditorUtility.SetDirty(
            definition);

        if (definition.movementProfile != null)
        {
            EditorUtility.SetDirty(
                definition.movementProfile);
        }

        if (definition.abilityProfile != null)
        {
            EditorUtility.SetDirty(
                definition.abilityProfile);
        }

        if (definition.animatorProfile != null)
        {
            EditorUtility.SetDirty(
                definition.animatorProfile);
        }

        if (definition.presentationProfile != null)
        {
            EditorUtility.SetDirty(
                definition.presentationProfile);
        }
    }

    private static void SaveAndRefresh()
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    #endregion

    #region Helpers

    private static string Normalize(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .ToLowerInvariant()
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("(", string.Empty)
            .Replace(")", string.Empty)
            .Replace(".", string.Empty);
    }

    #endregion
}

#endif