using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterSwitch))]
public sealed class TeamSetup : MonoBehaviour
{
    #region Types

    private enum TeamRole
    {
        Speed,
        Fly,
        Power,
        Special
    }

    #endregion

    #region Constants

    private const string DefinitionsResourcePath =
        "Teams";

    private const string LeaderSlotName =
        "Team Leader";

    private const string LeftFollowerSlotName =
        "Left Team Member";

    private const string RightFollowerSlotName =
        "Right Team Member";

    private const string GroundCheckName =
        "GroundCheck";

    private const string LeftFollowTargetName =
        "LeftPos";

    private const string RightFollowTargetName =
        "RightPos";

    #endregion

    #region Inspector

    [Header("Team")]
    [SerializeField]
    private TeamComposition team;

    [Header("Starting Formation")]
    [SerializeField]
    private CHARACTERTYPES startingLeader =
        CHARACTERTYPES.Speed;

    [Header("Automatic Formation")]
    [SerializeField]
    private bool createMissingFormationObjects = true;

    [SerializeField]
    private Vector3 leftFollowerOffset =
        new Vector3(
            -1.5f,
            0f,
            -1.5f);

    [SerializeField]
    private Vector3 rightFollowerOffset =
        new Vector3(
            1.5f,
            0f,
            -1.5f);

    [SerializeField]
    private Vector3 groundCheckOffset =
        new Vector3(
            0f,
            0.1f,
            0f);

    [Header("HUD")]
    [SerializeField]
    private HUD hud;

    [Header("Super Form")]
    [SerializeField, Min(0.1f)]
    private float ringDrainInterval =
        3f;

    [SerializeField, Min(1)]
    private int ringsDrainedPerInterval =
        1;

    [Header("Recovery")]
    [SerializeField]
    private bool preserveFailedTeamForDebugging = true;

    [Header("Debug")]
    [SerializeField]
    private bool logStateChanges;

    #endregion

    #region Runtime References

    private Transform leaderSlot;
    private Transform leftFollowerSlot;
    private Transform rightFollowerSlot;

    private Transform groundCheck;
    private Transform leftFollowTarget;
    private Transform rightFollowTarget;

    private CharacterSwitch characterSwitch;

    #endregion

    #region Definition Cache

    private readonly Dictionary<string, CharacterDefinition>
        definitionCache =
            new Dictionary<string, CharacterDefinition>();

    private CharacterDefinition speedDefinition;
    private CharacterDefinition flyDefinition;
    private CharacterDefinition powerDefinition;
    private CharacterDefinition superDefinition;

    private bool definitionsCached;

    #endregion

    #region Runtime State

    private Coroutine ringDrainRoutine;
    private WaitForSeconds ringDrainWait;

    private bool initialized;
    private bool initializing;
    private bool shuttingDown;
    private bool superFormEventSubscribed;

    #endregion

    #region Public API

    public static TeamSetup Instance
    {
        get;
        private set;
    }

    public TeamComposition Team =>
        team;

    public PlayableTeam PlayableTeam =>
        team != null
            ? team.PlayableTeam
            : default;

    public bool IsInitialized =>
        initialized;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!RegisterInstance())
        {
            enabled = false;

            return;
        }

        CacheComponents();
        ResolveSceneReferences();
        RebuildRingDrainWait();
    }

    private void OnEnable()
    {
        if (shuttingDown)
            return;

        CacheComponents();
        ResolveSceneReferences();
        SubscribeToEvents();
    }

    private void Start()
    {
        if (!InitializeTeam())
        {
            Debug.LogError(
                "TeamSetup failed to initialize.",
                this);

            enabled = false;
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
        StopRingDrain();
    }

    private void OnDestroy()
    {
        shuttingDown = true;

        UnsubscribeFromEvents();
        CleanupDestroyedState();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        ringDrainInterval =
            Mathf.Max(
                0.1f,
                ringDrainInterval);

        ringsDrainedPerInterval =
            Mathf.Max(
                1,
                ringsDrainedPerInterval);

        if (!IsSupportedLeader(
                startingLeader))
        {
            startingLeader =
                CHARACTERTYPES.Speed;
        }
    }

    #endregion

    #region Initialization

    private bool InitializeTeam()
    {
        if (initialized)
            return true;

        if (initializing ||
            shuttingDown)
        {
            return false;
        }

        initializing = true;

        try
        {
            CacheComponents();
            ResolveSceneReferences();
            RebuildRingDrainWait();

            if (!ValidateBaseConfiguration())
                return false;

            if (!EnsureFormationStructure())
                return false;

            if (!RefreshCharacterDefinitions())
                return false;

            ClearFormationSlots();

            if (!EnsureFormationStructure())
                return false;

            if (!RepairFormationControllers())
                return false;

            Transform speedCharacter =
                SpawnCharacter(
                    team.SpeedCharacterPrefab,
                    speedDefinition,
                    leaderSlot);

            Transform flyCharacter =
                SpawnCharacter(
                    team.FlyingCharacterPrefab,
                    flyDefinition,
                    leftFollowerSlot);

            Transform powerCharacter =
                SpawnCharacter(
                    team.PowerCharacterPrefab,
                    powerDefinition,
                    rightFollowerSlot);

            if (!ValidateSpawnedTeam(
                    speedCharacter,
                    flyCharacter,
                    powerCharacter))
            {
                CleanupFailedTeam(
                    speedCharacter,
                    flyCharacter,
                    powerCharacter);

                return false;
            }

            if (!ConfigureCharacterSwitch(
                    speedCharacter,
                    flyCharacter,
                    powerCharacter))
            {
                CleanupFailedTeam(
                    speedCharacter,
                    flyCharacter,
                    powerCharacter);

                return false;
            }

            GameInstance.currentTeam =
                (int)team.PlayableTeam;

            initialized = true;

            RefreshHud();

            LogStateChange(
                $"Initialized team '{team.PlayableTeam}'.");

            return true;
        }
        finally
        {
            initializing = false;
        }
    }

    private bool RebuildTeam()
    {
        if (shuttingDown)
            return false;

        StopRingDrain();

        initialized = false;

        InvalidateDefinitionCache();

        return InitializeTeam();
    }

    private bool RegisterInstance()
    {
        if (Instance == null ||
            Instance == this)
        {
            Instance = this;

            return true;
        }

        Debug.LogError(
            $"Duplicate TeamSetup detected. " +
            $"Existing: '{Instance.name}'. " +
            $"Duplicate: '{name}'.",
            this);

        return false;
    }

    #endregion

    #region Definition Resolution

    private bool RefreshCharacterDefinitions()
    {
        InvalidateRoleDefinitions();

        if (!BuildDefinitionCache())
            return false;

        speedDefinition =
            ResolveDefinition(
                team.SpeedCharacterPrefab,
                TeamRole.Speed);

        flyDefinition =
            ResolveDefinition(
                team.FlyingCharacterPrefab,
                TeamRole.Fly);

        powerDefinition =
            ResolveDefinition(
                team.PowerCharacterPrefab,
                TeamRole.Power);

        if (team.SuperCharacterPrefab != null)
        {
            superDefinition =
                ResolveDefinition(
                    team.SuperCharacterPrefab,
                    TeamRole.Special);
        }

        bool valid =
            ValidateResolvedDefinition(
                speedDefinition,
                "Speed") &
            ValidateResolvedDefinition(
                flyDefinition,
                "Fly") &
            ValidateResolvedDefinition(
                powerDefinition,
                "Power");

        if (team.SuperCharacterPrefab != null &&
            superDefinition == null)
        {
            Debug.LogWarning(
                $"TeamSetup could not resolve a Special CharacterDefinition " +
                $"for '{team.SuperCharacterPrefab.name}'.",
                this);
        }

        return valid;
    }

    private bool BuildDefinitionCache()
    {
        if (definitionsCached &&
            definitionCache.Count > 0)
        {
            return true;
        }

        definitionCache.Clear();

        CharacterDefinition[] definitions =
            Resources.LoadAll<CharacterDefinition>(
                DefinitionsResourcePath);

        if (definitions == null ||
            definitions.Length == 0)
        {
            Debug.LogError(
                $"TeamSetup found no CharacterDefinition assets under " +
                $"'Resources/{DefinitionsResourcePath}'.",
                this);

            definitionsCached = false;

            return false;
        }

        foreach (CharacterDefinition definition
                 in definitions)
        {
            if (!PrepareDefinition(
                    definition))
            {
                continue;
            }

            string id =
                NormalizeId(
                    definition.characterId);

            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (definitionCache.TryGetValue(
                    id,
                    out CharacterDefinition existing))
            {
                definitionCache[id] =
                    ChoosePreferredDefinition(
                        existing,
                        definition);

                continue;
            }

            definitionCache.Add(
                id,
                definition);
        }

        definitionsCached =
            definitionCache.Count > 0;

        if (!definitionsCached)
        {
            Debug.LogError(
                "TeamSetup did not find any valid CharacterDefinition assets.",
                this);
        }

        return definitionsCached;
    }

    private static bool PrepareDefinition(
        CharacterDefinition definition)
    {
        if (definition == null)
            return false;

        if (!definition.RepairRuntimeIdentity())
            return false;

        return definition.IsValid();
    }

    private CharacterDefinition ResolveDefinition(
        GameObject prefab,
        TeamRole expectedRole)
    {
        if (prefab == null)
            return null;

        if (CharacterDefinition.TryResolveIdentityFromName(
                prefab.name,
                out CharacterDefinition.CharacterIdentity identity))
        {
            CharacterDefinition exact =
                FindDefinitionById(
                    identity.CharacterId);

            if (IsDefinitionCompatible(
                    exact,
                    expectedRole))
            {
                return exact;
            }
        }

        return FindDefinitionByRole(
            expectedRole);
    }

    private CharacterDefinition FindDefinitionById(
        string characterId)
    {
        if (string.IsNullOrWhiteSpace(
                characterId))
        {
            return null;
        }

        definitionCache.TryGetValue(
            NormalizeId(
                characterId),
            out CharacterDefinition definition);

        return definition;
    }

    private CharacterDefinition FindDefinitionByRole(
        TeamRole expectedRole)
    {
        CharacterDefinition result = null;

        foreach (CharacterDefinition definition
                 in definitionCache.Values)
        {
            if (!IsDefinitionCompatible(
                    definition,
                    expectedRole))
            {
                continue;
            }

            if (!MatchesCurrentTeam(
                    definition))
            {
                continue;
            }

            if (result != null)
            {
                Debug.LogError(
                    $"TeamSetup found multiple CharacterDefinitions for " +
                    $"role '{expectedRole}' on team '{team.PlayableTeam}'.",
                    this);

                return null;
            }

            result =
                definition;
        }

        return result;
    }

    private bool MatchesCurrentTeam(
        CharacterDefinition definition)
    {
        if (definition == null ||
            team == null)
        {
            return false;
        }

        if (definition.team ==
            CharacterDefinition.Team.Special)
        {
            return false;
        }

        string definitionTeam =
            NormalizeTeamName(
                definition.team.ToString());

        string playableTeam =
            NormalizeTeamName(
                team.PlayableTeam.ToString());

        return definitionTeam ==
            playableTeam;
    }

    private static bool IsDefinitionCompatible(
        CharacterDefinition definition,
        TeamRole expectedRole)
    {
        if (definition == null ||
            !definition.IsValid())
        {
            return false;
        }

        return expectedRole switch
        {
            TeamRole.Speed =>
                definition.characterType ==
                CharacterDefinition.CharacterType.Speed,

            TeamRole.Fly =>
                definition.characterType ==
                CharacterDefinition.CharacterType.Fly,

            TeamRole.Power =>
                definition.characterType ==
                CharacterDefinition.CharacterType.Power,

            TeamRole.Special =>
                definition.characterType ==
                CharacterDefinition.CharacterType.Special,

            _ =>
                false
        };
    }

    private static CharacterDefinition ChoosePreferredDefinition(
        CharacterDefinition first,
        CharacterDefinition second)
    {
        if (first == null)
            return second;

        if (second == null)
            return first;

        int firstScore =
            ScoreDefinitionName(
                first);

        int secondScore =
            ScoreDefinitionName(
                second);

        return secondScore > firstScore
            ? second
            : first;
    }

    private static int ScoreDefinitionName(
        CharacterDefinition definition)
    {
        if (definition == null)
            return 0;

        if (!CharacterDefinition.TryGetIdentity(
                definition.characterId,
                out CharacterDefinition.CharacterIdentity identity))
        {
            return 0;
        }

        string expected =
            NormalizeObjectName(
                $"{identity.ShortName} Character Definition");

        string actual =
            NormalizeObjectName(
                definition.name);

        if (actual == expected)
            return 100;

        if (actual.Contains(
                NormalizeObjectName(
                    identity.ShortName)))
        {
            return 50;
        }

        return 1;
    }

    private bool ValidateResolvedDefinition(
        CharacterDefinition definition,
        string roleName)
    {
        if (definition == null)
        {
            Debug.LogError(
                $"TeamSetup could not automatically resolve the {roleName} CharacterDefinition.",
                this);

            return false;
        }

        if (!definition.IsValid())
        {
            Debug.LogError(
                $"TeamSetup resolved an invalid {roleName} CharacterDefinition " +
                $"'{definition.name}'.",
                definition);

            return false;
        }

        return true;
    }

    private void InvalidateDefinitionCache()
    {
        definitionsCached = false;

        definitionCache.Clear();

        InvalidateRoleDefinitions();
    }

    private void InvalidateRoleDefinitions()
    {
        speedDefinition = null;
        flyDefinition = null;
        powerDefinition = null;
        superDefinition = null;
    }

    #endregion

    #region Formation Structure

    private bool EnsureFormationStructure()
    {
        ResolveFormationSlots();

        if (!createMissingFormationObjects)
        {
            return ValidateFormationStructure();
        }

        leaderSlot ??=
            CreateFormationObject(
                transform,
                LeaderSlotName,
                Vector3.zero);

        leftFollowerSlot ??=
            CreateFormationObject(
                transform,
                LeftFollowerSlotName,
                leftFollowerOffset);

        rightFollowerSlot ??=
            CreateFormationObject(
                transform,
                RightFollowerSlotName,
                rightFollowerOffset);

        if (leaderSlot == null ||
            leftFollowerSlot == null ||
            rightFollowerSlot == null)
        {
            return false;
        }

        groundCheck ??=
            CreateFormationObject(
                leaderSlot,
                GroundCheckName,
                groundCheckOffset);

        leftFollowTarget ??=
            CreateFormationObject(
                leaderSlot,
                LeftFollowTargetName,
                leftFollowerOffset);

        rightFollowTarget ??=
            CreateFormationObject(
                leaderSlot,
                RightFollowTargetName,
                rightFollowerOffset);

        return ValidateFormationStructure();
    }

    private void ResolveFormationSlots()
    {
        leaderSlot ??=
            FindDescendantByName(
                transform,
                LeaderSlotName);

        leftFollowerSlot ??=
            FindDescendantByName(
                transform,
                LeftFollowerSlotName);

        rightFollowerSlot ??=
            FindDescendantByName(
                transform,
                RightFollowerSlotName);

        if (leaderSlot == null)
            return;

        groundCheck ??=
            FindDescendantByName(
                leaderSlot,
                GroundCheckName);

        leftFollowTarget ??=
            FindDescendantByName(
                leaderSlot,
                LeftFollowTargetName);

        rightFollowTarget ??=
            FindDescendantByName(
                leaderSlot,
                RightFollowTargetName);
    }

    private bool ValidateFormationStructure()
    {
        bool valid = true;

        valid &=
            ValidateReference(
                leaderSlot,
                LeaderSlotName);

        valid &=
            ValidateReference(
                leftFollowerSlot,
                LeftFollowerSlotName);

        valid &=
            ValidateReference(
                rightFollowerSlot,
                RightFollowerSlotName);

        valid &=
            ValidateReference(
                groundCheck,
                GroundCheckName);

        valid &=
            ValidateReference(
                leftFollowTarget,
                LeftFollowTargetName);

        valid &=
            ValidateReference(
                rightFollowTarget,
                RightFollowTargetName);

        return valid;
    }

    private static Transform CreateFormationObject(
        Transform parent,
        string objectName,
        Vector3 localPosition)
    {
        if (parent == null ||
            string.IsNullOrWhiteSpace(
                objectName))
        {
            return null;
        }

        GameObject created =
            new GameObject(
                objectName);

        Transform createdTransform =
            created.transform;

        createdTransform.SetParent(
            parent,
            false);

        createdTransform.SetLocalPositionAndRotation(
            localPosition,
            Quaternion.identity);

        createdTransform.localScale =
            Vector3.one;

        return createdTransform;
    }

    #endregion

    #region Character Spawning

    private Transform SpawnCharacter(
        GameObject prefab,
        CharacterDefinition definition,
        Transform slot)
    {
        if (prefab == null ||
            definition == null ||
            slot == null)
        {
            return null;
        }

        if (!definition.IsValid())
        {
            Debug.LogError(
                $"TeamSetup refused to spawn '{prefab.name}' with invalid " +
                $"CharacterDefinition '{definition.name}'.",
                definition);

            return null;
        }

        GameObject characterObject =
            Instantiate(
                prefab,
                slot,
                false);

        if (characterObject == null)
            return null;

        Transform character =
            characterObject.transform;

        character.SetLocalPositionAndRotation(
            Vector3.zero,
            Quaternion.identity);

        if (!IsFiniteVector(
                prefab.transform.localScale) ||
            IsZeroScale(
                prefab.transform.localScale))
        {
            character.localScale =
                Vector3.one;
        }
        else
        {
            character.localScale =
                prefab.transform.localScale;
        }

        if (!RepairVisualCharacter(
                character,
                definition))
        {
            DestroyCharacter(
                character);

            return null;
        }

        return character;
    }

    private bool RepairVisualCharacter(
        Transform character,
        CharacterDefinition definition)
    {
        if (character == null ||
            definition == null ||
            !definition.IsValid())
        {
            return false;
        }

        RemoveVisualPhysicsComponent<UltimatePlayerMovement>(
            character);

        RemoveVisualPhysicsComponent<Rigidbody>(
            character);

        Animator animator =
            character.GetComponentInChildren<Animator>(
                includeInactive: true);

        if (animator == null)
        {
            Debug.LogError(
                $"Visual character '{character.name}' has no Animator.",
                character);

            return false;
        }

        animator.enabled = true;

        if (definition.animatorProfile != null)
        {
            if (definition.animatorProfile.animatorController != null)
            {
                animator.runtimeAnimatorController =
                    definition.animatorProfile.animatorController;
            }

            if (definition.animatorProfile.avatar != null)
            {
                animator.avatar =
                    definition.animatorProfile.avatar;
            }
        }

        CharacterType characterType =
            character.GetComponent<CharacterType>();

        if (characterType == null)
        {
            characterType =
                character.gameObject.AddComponent<CharacterType>();
        }

        return characterType != null;
    }

    private bool ValidateSpawnedTeam(
        Transform speedCharacter,
        Transform flyCharacter,
        Transform powerCharacter)
    {
        bool valid = true;

        valid &=
            ValidateVisualCharacter(
                speedCharacter,
                "Speed Character");

        valid &=
            ValidateVisualCharacter(
                flyCharacter,
                "Fly Character");

        valid &=
            ValidateVisualCharacter(
                powerCharacter,
                "Power Character");

        return valid;
    }

    private bool ValidateVisualCharacter(
        Transform character,
        string displayName)
    {
        if (character == null)
        {
            Debug.LogError(
                $"TeamSetup cannot validate a null {displayName}.",
                this);

            return false;
        }

        Animator animator =
            character.GetComponentInChildren<Animator>(
                includeInactive: true);

        Renderer renderer =
            character.GetComponentInChildren<Renderer>(
                includeInactive: true);

        bool valid = true;

        if (animator == null)
        {
            Debug.LogError(
                $"TeamSetup could not find an Animator on " +
                $"'{character.name}'.",
                character);

            valid = false;
        }

        if (renderer == null)
        {
            Debug.LogError(
                $"TeamSetup could not find a Renderer on " +
                $"'{character.name}'.",
                character);

            valid = false;
        }

        return valid;
    }

    #endregion

    #region Controller Repair

    private bool RepairFormationControllers()
    {
        if (!ValidateFormationStructure())
            return false;

        if (!RepairLeaderController())
            return false;

        if (!RepairFollowerController(
                leftFollowerSlot))
        {
            return false;
        }

        if (!RepairFollowerController(
                rightFollowerSlot))
        {
            return false;
        }

        return true;
    }

    private bool RepairLeaderController()
    {
        if (leaderSlot == null)
            return false;

        Rigidbody leaderRigidbody =
            GetOrAddComponent<Rigidbody>(
                leaderSlot.gameObject);

        CapsuleCollider leaderCollider =
            GetOrAddComponent<CapsuleCollider>(
                leaderSlot.gameObject);

        UltimatePlayerMovement movement =
            GetOrAddComponent<UltimatePlayerMovement>(
                leaderSlot.gameObject);

        if (leaderRigidbody == null ||
            leaderCollider == null ||
            movement == null)
        {
            return false;
        }

        ConfigureLeaderRigidbody(
            leaderRigidbody);

        ConfigureGameplayCollider(
            leaderCollider);

        CharacterDefinition startingDefinition =
            ResolveStartingDefinition();

        if (startingDefinition == null ||
            !startingDefinition.IsValid())
        {
            Debug.LogError(
                "TeamSetup could not resolve a valid starting CharacterDefinition.",
                this);

            return false;
        }

        if (!movement.SetCharacterDefinition(
                startingDefinition))
        {
            Debug.LogError(
                $"TeamSetup could not assign " +
                $"'{startingDefinition.name}' to Team Leader.",
                movement);

            return false;
        }

        return movement.InitializeMovement();
    }

    private bool RepairFollowerController(
        Transform followerSlot)
    {
        if (followerSlot == null)
            return false;

        Rigidbody followerRigidbody =
            GetOrAddComponent<Rigidbody>(
                followerSlot.gameObject);

        CapsuleCollider followerCollider =
            GetOrAddComponent<CapsuleCollider>(
                followerSlot.gameObject);

        if (followerRigidbody == null ||
            followerCollider == null)
        {
            return false;
        }

        ConfigureFollowerRigidbody(
            followerRigidbody);

        ConfigureGameplayCollider(
            followerCollider);

        return true;
    }

    private CharacterDefinition ResolveStartingDefinition()
    {
        return startingLeader switch
        {
            CHARACTERTYPES.Speed =>
                speedDefinition,

            CHARACTERTYPES.Fly =>
                flyDefinition,

            CHARACTERTYPES.Power =>
                powerDefinition,

            _ =>
                speedDefinition
        };
    }

    private static T GetOrAddComponent<T>(
        GameObject target)
        where T : Component
    {
        if (target == null)
            return null;

        T component =
            target.GetComponent<T>();

        if (component != null)
            return component;

        return target.AddComponent<T>();
    }

    private static void ConfigureLeaderRigidbody(
        Rigidbody target)
    {
        if (target == null)
            return;

        target.useGravity = true;
        target.isKinematic = false;

        target.interpolation =
            RigidbodyInterpolation.Interpolate;

        target.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;

        target.constraints =
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationY |
            RigidbodyConstraints.FreezeRotationZ;

        SanitizeRigidbody(
            target);
    }

    private static void ConfigureFollowerRigidbody(
        Rigidbody target)
    {
        if (target == null)
            return;

        target.useGravity = false;
        target.isKinematic = true;

        target.interpolation =
            RigidbodyInterpolation.Interpolate;

        target.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;

        target.constraints =
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationY |
            RigidbodyConstraints.FreezeRotationZ;

        SanitizeRigidbody(
            target);
    }

    private static void ConfigureGameplayCollider(
        CapsuleCollider target)
    {
        if (target == null)
            return;

        target.enabled = true;
        target.isTrigger = false;
        target.direction = 1;

        target.center =
            new Vector3(
                0f,
                1f,
                0f);

        target.radius =
            Mathf.Max(
                0.1f,
                target.radius);

        target.height =
            Mathf.Max(
                target.radius * 2f,
                target.height);
    }

    private static void SanitizeRigidbody(
        Rigidbody target)
    {
        if (target == null)
            return;

        if (!IsFiniteVector(
                target.linearVelocity))
        {
            target.linearVelocity =
                Vector3.zero;
        }

        if (!IsFiniteVector(
                target.angularVelocity))
        {
            target.angularVelocity =
                Vector3.zero;
        }
    }

    #endregion

    #region Character Switch

    private bool ConfigureCharacterSwitch(
        Transform speedCharacter,
        Transform flyCharacter,
        Transform powerCharacter)
    {
        if (characterSwitch == null)
            return false;

        bool configured =
            characterSwitch.ConfigureTeam(
                speedCharacter,
                flyCharacter,
                powerCharacter,
                team.SpeedCharacterPrefab,
                team.SuperCharacterPrefab,
                startingLeader);

        if (!configured)
        {
            Debug.LogError(
                "CharacterSwitch rejected the automatically built team.",
                characterSwitch);
        }

        return configured;
    }

    #endregion

    #region Formation Cleanup

    private void ClearFormationSlots()
    {
        ClearSlot(
            leaderSlot);

        ClearSlot(
            leftFollowerSlot);

        ClearSlot(
            rightFollowerSlot);
    }

    private static void ClearSlot(
        Transform slot)
    {
        if (slot == null)
            return;

        for (int index =
                 slot.childCount - 1;
             index >= 0;
             index--)
        {
            Transform child =
                slot.GetChild(
                    index);

            if (child == null ||
                IsPermanentSlotHelper(
                    child))
            {
                continue;
            }

            child.gameObject.SetActive(
                false);

            Destroy(
                child.gameObject);
        }
    }

    private static bool IsPermanentSlotHelper(
        Transform child)
    {
        if (child == null)
            return false;

        return
            NamesMatch(
                child.name,
                GroundCheckName) ||
            NamesMatch(
                child.name,
                LeftFollowTargetName) ||
            NamesMatch(
                child.name,
                RightFollowTargetName);
    }

    private static void RemoveVisualPhysicsComponent<T>(
        Transform visualRoot)
        where T : Component
    {
        if (visualRoot == null)
            return;

        T[] components =
            visualRoot.GetComponentsInChildren<T>(
                includeInactive: true);

        foreach (T component
                 in components)
        {
            if (component == null)
                continue;

            component.gameObject.SetActive(
                false);

            Destroy(
                component);
        }
    }

    #endregion

    #region Super Form

    private void HandleSuperFormChanged(
        bool isSuperForm)
    {
        if (isSuperForm)
        {
            StartRingDrain();

            return;
        }

        StopRingDrain();
        RefreshRingDisplay();
    }

    private void StartRingDrain()
    {
        StopRingDrain();

        if (!initialized ||
            shuttingDown ||
            characterSwitch == null)
        {
            return;
        }

        RebuildRingDrainWait();

        ringDrainRoutine =
            StartCoroutine(
                DrainSuperFormRings());
    }

    private void StopRingDrain()
    {
        if (ringDrainRoutine == null)
            return;

        StopCoroutine(
            ringDrainRoutine);

        ringDrainRoutine = null;
    }

    private IEnumerator DrainSuperFormRings()
    {
        while (!shuttingDown &&
               characterSwitch != null &&
               characterSwitch.IsSuperForm)
        {
            if (GameInstance.currentRings <= 0)
                break;

            yield return ringDrainWait;

            if (shuttingDown ||
                characterSwitch == null ||
                !characterSwitch.IsSuperForm)
            {
                break;
            }

            GameInstance.currentRings =
                Mathf.Max(
                    0,
                    GameInstance.currentRings -
                    ringsDrainedPerInterval);

            RefreshRingDisplay();
        }

        ringDrainRoutine = null;

        if (shuttingDown ||
            characterSwitch == null ||
            !characterSwitch.IsSuperForm)
        {
            yield break;
        }

        characterSwitch.ToggleSuperForm();
    }

    private void RebuildRingDrainWait()
    {
        ringDrainWait =
            new WaitForSeconds(
                Mathf.Max(
                    0.1f,
                    ringDrainInterval));
    }

    #endregion

    #region Event Management

    private void SubscribeToEvents()
    {
        if (superFormEventSubscribed)
            return;

        CacheComponents();

        if (characterSwitch == null)
            return;

        characterSwitch.SuperFormChanged +=
            HandleSuperFormChanged;

        superFormEventSubscribed = true;
    }

    private void UnsubscribeFromEvents()
    {
        if (!superFormEventSubscribed)
            return;

        if (characterSwitch != null)
        {
            characterSwitch.SuperFormChanged -=
                HandleSuperFormChanged;
        }

        superFormEventSubscribed = false;
    }

    #endregion

    #region Reference Resolution

    private void CacheComponents()
    {
        characterSwitch ??=
            GetComponent<CharacterSwitch>();
    }

    private void ResolveSceneReferences()
    {
        hud ??=
            FindAnyObjectByType<HUD>(
                FindObjectsInactive.Include);

        ResolveFormationSlots();
    }

    private static Transform FindDescendantByName(
        Transform root,
        string objectName)
    {
        if (root == null ||
            string.IsNullOrWhiteSpace(
                objectName))
        {
            return null;
        }

        Transform[] descendants =
            root.GetComponentsInChildren<Transform>(
                includeInactive: true);

        foreach (Transform descendant
                 in descendants)
        {
            if (descendant == null)
                continue;

            if (NamesMatch(
                    descendant.name,
                    objectName))
            {
                return descendant;
            }
        }

        return null;
    }

    private static bool NamesMatch(
        string first,
        string second)
    {
        return NormalizeObjectName(
                   first) ==
               NormalizeObjectName(
                   second);
    }

    private static string NormalizeObjectName(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace("(", string.Empty)
            .Replace(")", string.Empty)
            .Trim()
            .ToLowerInvariant();
    }

    private static string NormalizeId(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .Trim()
            .ToLowerInvariant()
            .Replace("_", "-")
            .Replace(" ", "-");
    }

    private static string NormalizeTeamName(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return NormalizeObjectName(
                value)
            .Replace(
                "team",
                string.Empty);
    }

    #endregion

    #region HUD

    private void RefreshHud()
    {
        if (hud == null ||
            team == null)
        {
            return;
        }

        hud.Setup(
            team);

        hud.UpdateRings();
    }

    private void RefreshRingDisplay()
    {
        hud?.UpdateRings();
    }

    #endregion

    #region Validation

    private bool ValidateBaseConfiguration()
    {
        bool valid = true;

        valid &=
            ValidateReference(
                team,
                "Team Composition");

        valid &=
            ValidateReference(
                characterSwitch,
                "Character Switch");

        if (team != null)
        {
            valid &=
                ValidateReference(
                    team.SpeedCharacterPrefab,
                    "Speed Character Prefab");

            valid &=
                ValidateReference(
                    team.FlyingCharacterPrefab,
                    "Fly Character Prefab");

            valid &=
                ValidateReference(
                    team.PowerCharacterPrefab,
                    "Power Character Prefab");

            if (team.SuperCharacterPrefab == null)
            {
                Debug.LogWarning(
                    "TeamSetup has no Super Character Prefab. " +
                    "Normal team initialization can continue.",
                    this);
            }
        }

        if (!IsSupportedLeader(
                startingLeader))
        {
            Debug.LogError(
                $"Unsupported starting leader '{startingLeader}'.",
                this);

            valid = false;
        }

        if (hud == null)
        {
            Debug.LogWarning(
                "TeamSetup HUD reference is missing.",
                this);
        }

        return valid;
    }

    private bool ValidateReference(
        Object reference,
        string displayName)
    {
        if (reference != null)
            return true;

        Debug.LogError(
            $"TeamSetup: '{displayName}' is missing on '{name}'.",
            this);

        return false;
    }

    private static bool IsSupportedLeader(
        CHARACTERTYPES type)
    {
        return
            type == CHARACTERTYPES.Speed ||
            type == CHARACTERTYPES.Fly ||
            type == CHARACTERTYPES.Power;
    }

    private static bool IsFiniteVector(
        Vector3 value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }

    private static bool IsZeroScale(
        Vector3 scale)
    {
        return
            Mathf.Abs(scale.x) <= Mathf.Epsilon ||
            Mathf.Abs(scale.y) <= Mathf.Epsilon ||
            Mathf.Abs(scale.z) <= Mathf.Epsilon;
    }

    #endregion

    #region Cleanup

    private void CleanupFailedTeam(
        Transform speedCharacter,
        Transform flyCharacter,
        Transform powerCharacter)
    {
        if (preserveFailedTeamForDebugging)
        {
            Debug.LogWarning(
                "Failed team preserved for debugging.",
                this);

            return;
        }

        DestroyCharacter(
            speedCharacter);

        DestroyCharacter(
            flyCharacter);

        DestroyCharacter(
            powerCharacter);
    }

    private static void DestroyCharacter(
        Transform character)
    {
        if (character == null)
            return;

        character.gameObject.SetActive(
            false);

        Destroy(
            character.gameObject);
    }

    private void CleanupDestroyedState()
    {
        StopRingDrain();

        initialized = false;
        initializing = false;

        leaderSlot = null;
        leftFollowerSlot = null;
        rightFollowerSlot = null;

        groundCheck = null;
        leftFollowTarget = null;
        rightFollowTarget = null;

        characterSwitch = null;
        hud = null;

        ringDrainRoutine = null;
        ringDrainWait = null;

        InvalidateDefinitionCache();
    }

    #endregion

    #region Debug

    private void LogStateChange(
        string message)
    {
        if (!logStateChanges)
            return;

        Debug.Log(
            message,
            this);
    }

    #endregion
}