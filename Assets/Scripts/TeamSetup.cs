using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterSwitch))]
public sealed class TeamSetup : MonoBehaviour
{
    #region Inspector

    [Header("Team")]
    [SerializeField] private TeamComposition team;

    [Header("Formation Slots")]
    private const string LeaderSlotName = "Team Leader";
    private const string LeftFollowerSlotName = "Left Team Member";
    private const string RightFollowerSlotName = "Right Team Member";

    private Transform leaderSlot;
    private Transform leftFollowerSlot;
    private Transform rightFollowerSlot;

    [Header("Starting Formation")]
    [SerializeField]
    private CHARACTERTYPES startingLeader =
        CHARACTERTYPES.Speed;

    [Header("HUD")]
    [SerializeField] private HUD hud;

    [Header("Super Form")]
    [SerializeField, Min(0.1f)] private float ringDrainInterval = 3f;
    [SerializeField, Min(1)] private int ringsDrainedPerInterval = 1;

    [Header("Debugging")]
    [SerializeField] private bool preserveFailedTeamForDebugging = true;
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Constants

    private const string GroundCheckName = "GroundCheck";
    private const string LeftFollowTargetName = "LeftPos";
    private const string RightFollowTargetName = "RightPos";

    #endregion

    #region Runtime State

    private Transform groundCheck;
    private Transform leftFollowTarget;
    private Transform rightFollowTarget;

    private CharacterSwitch characterSwitch;

    private Coroutine ringDrainRoutine;
    private WaitForSeconds ringDrainWait;

    private bool initialized;
    private bool shuttingDown;
    private bool superFormEventSubscribed;

    #endregion

    #region Public API

    public static TeamSetup Instance { get; private set; }

    public TeamComposition Team => team;

    public PlayableTeam PlayableTeam =>
        team != null
            ? team.PlayableTeam
            : default;

    public bool IsInitialized => initialized;

    private Transform LeaderSlot => leaderSlot;
    private Transform LeftFollowerSlot => leftFollowerSlot;
    private Transform RightFollowerSlot => rightFollowerSlot;

    private Transform GroundCheck => groundCheck;
    private Transform LeftFollowTarget => leftFollowTarget;
    private Transform RightFollowTarget => rightFollowTarget;

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
        ResolveReferences();
        RebuildRingDrainWait();
    }

    private void OnEnable()
    {
        if (shuttingDown)
            return;

        CacheComponents();
        SubscribeToEvents();
    }

    private void Start()
    {
        if (!InitializeTeam())
        {
            enabled = false;
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
        CleanupRuntimeState();
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

        if (!IsSupportedLeader(startingLeader))
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

        CacheComponents();
        ResolveReferences();
        RebuildRingDrainWait();

        if (!ValidateConfiguration())
        {
            Debug.LogError(
                "TeamSetup initialization stopped because the configuration is invalid.",
                this);

            return false;
        }

        if (!ResolveSlotHelpers())
        {
            Debug.LogError(
                "TeamSetup initialization stopped because the leader-slot helpers are missing.",
                this);

            return false;
        }

        ClearFormationSlots();

        if (!ResolveSlotHelpers())
        {
            Debug.LogError(
                "TeamSetup lost one or more permanent leader-slot helpers after clearing the formation.",
                this);

            return false;
        }

        if (!RepairFormationControllers())
        {
            Debug.LogError(
                "TeamSetup could not repair the formation controllers.",
                this);

            return false;
        }

        Transform speedCharacter =
            SpawnCharacter(
                team.SpeedCharacterPrefab,
                team.SpeedCharacterDefinition,
                leaderSlot);

        Transform flyingCharacter =
            SpawnCharacter(
                team.FlyingCharacterPrefab,
                team.FlyingCharacterDefinition,
                leftFollowerSlot);

        Transform powerCharacter =
            SpawnCharacter(
                team.PowerCharacterPrefab,
                team.PowerCharacterDefinition,
                rightFollowerSlot);

        if (speedCharacter == null ||
            flyingCharacter == null ||
            powerCharacter == null)
        {
            Debug.LogError(
                "TeamSetup failed to spawn the complete team.",
                this);

            CleanupFailedTeam(
                speedCharacter,
                flyingCharacter,
                powerCharacter);

            return false;
        }

        if (!ResolveSlotHelpers())
        {
            Debug.LogError(
                "TeamSetup could not restore the permanent helper references after spawning the team.",
                this);

            CleanupFailedTeam(
                speedCharacter,
                flyingCharacter,
                powerCharacter);

            return false;
        }

        if (!ValidateSpawnedCharacters(
                speedCharacter,
                flyingCharacter,
                powerCharacter))
        {
            Debug.LogError(
                "TeamSetup rejected one or more spawned characters.",
                this);

            CleanupFailedTeam(
                speedCharacter,
                flyingCharacter,
                powerCharacter);

            return false;
        }

        bool configured =
            characterSwitch.ConfigureTeam(
                speedCharacter,
                flyingCharacter,
                powerCharacter,
                team.SpeedCharacterPrefab,
                team.SuperCharacterPrefab,
                startingLeader);

        if (!configured)
        {
            Debug.LogError(
                "CharacterSwitch rejected the spawned team.",
                characterSwitch);

            CleanupFailedTeam(
                speedCharacter,
                flyingCharacter,
                powerCharacter);

            return false;
        }

        GameInstance.currentTeam =
            (int)team.PlayableTeam;

        initialized = true;

        RefreshHud();

        LogStateChange(
            $"Initialized team: {team.PlayableTeam}.");

        return true;
    }

    private bool RebuildTeam()
    {
        CleanupRuntimeState();

        initialized = false;

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
            $"Duplicate TeamSetup detected. Existing object: " +
            $"'{Instance.gameObject.name}'. Duplicate object: " +
            $"'{gameObject.name}'.",
            this);

        return false;
    }

    #endregion

    #region Character Spawning

    private Transform SpawnCharacter(
     GameObject prefab,
     CharacterDefinition definition,
     Transform slot)
    {
        if (prefab == null)
        {
            Debug.LogError(
                "TeamSetup cannot spawn a null character prefab.",
                this);

            return null;
        }

        if (definition == null)
        {
            Debug.LogError(
                $"TeamSetup could not resolve a CharacterDefinition for '{prefab.name}'.",
                prefab);

            return null;
        }

        if (slot == null)
        {
            Debug.LogError(
                $"TeamSetup cannot spawn '{prefab.name}' because its formation slot is missing.",
                this);

            return null;
        }

        GameObject characterObject =
            Instantiate(
                prefab,
                slot,
                false);

        if (characterObject == null)
        {
            Debug.LogError(
                $"TeamSetup failed to instantiate '{prefab.name}'.",
                this);

            return null;
        }

        Transform characterTransform =
            characterObject.transform;

        characterTransform.SetLocalPositionAndRotation(
            Vector3.zero,
            Quaternion.identity);

        characterTransform.localScale =
            prefab.transform.localScale;

        if (!RepairVisualCharacter(
                characterTransform,
                definition))
        {
            Debug.LogError(
                $"TeamSetup could not repair the visual character '{characterTransform.name}'.",
                characterTransform);

            Destroy(characterObject);

            return null;
        }

        return characterTransform;
    }

    private bool ValidateSpawnedCharacters(
    Transform speedCharacter,
    Transform flyingCharacter,
    Transform powerCharacter)
    {
        bool valid = true;

        valid &=
            ValidateVisualCharacter(
                speedCharacter,
                "Speed Character");

        valid &=
            ValidateVisualCharacter(
                flyingCharacter,
                "Flying Character");

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
                $"TeamSetup could not find an Animator on the spawned {displayName} '{character.name}'.",
                character);

            valid = false;
        }

        if (renderer == null)
        {
            Debug.LogError(
                $"TeamSetup could not find a Renderer on the spawned {displayName} '{character.name}'.",
                character);

            valid = false;
        }

        return valid;
    }

    private bool RepairFormationControllers()
    {
        if (leaderSlot == null ||
            leftFollowerSlot == null ||
            rightFollowerSlot == null)
        {
            return false;
        }

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
            ResolveStartingCharacterDefinition();

        if (startingDefinition == null)
        {
            Debug.LogError(
                "TeamSetup could not resolve the starting CharacterDefinition.",
                this);

            return false;
        }

        if (!movement.SetCharacterDefinition(
                startingDefinition))
        {
            Debug.LogError(
                "TeamSetup could not assign the starting CharacterDefinition to Team Leader.",
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

    private bool RepairVisualCharacter(
        Transform character,
        CharacterDefinition definition)
    {
        if (character == null ||
            definition == null)
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
                $"The visual character '{character.name}' has no Animator.",
                character);

            return false;
        }

        if (!animator.enabled)
        {
            animator.enabled = true;
        }

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

        characterType ??=
            character.gameObject.AddComponent<CharacterType>();

        return characterType != null;
    }

    private CharacterDefinition ResolveStartingCharacterDefinition()
    {
        return startingLeader switch
        {
            CHARACTERTYPES.Speed =>
                team.SpeedCharacterDefinition,

            CHARACTERTYPES.Fly =>
                team.FlyingCharacterDefinition,

            CHARACTERTYPES.Power =>
                team.PowerCharacterDefinition,

            _ =>
                team.SpeedCharacterDefinition
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
        Rigidbody targetRigidbody)
    {
        if (targetRigidbody == null)
            return;

        targetRigidbody.useGravity = true;
        targetRigidbody.isKinematic = false;

        targetRigidbody.interpolation =
            RigidbodyInterpolation.Interpolate;

        targetRigidbody.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;

        targetRigidbody.constraints =
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationY |
            RigidbodyConstraints.FreezeRotationZ;
    }

    private static void ConfigureFollowerRigidbody(
        Rigidbody targetRigidbody)
    {
        if (targetRigidbody == null)
            return;

        targetRigidbody.useGravity = false;
        targetRigidbody.isKinematic = true;

        targetRigidbody.interpolation =
            RigidbodyInterpolation.Interpolate;

        targetRigidbody.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;

        targetRigidbody.constraints =
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationY |
            RigidbodyConstraints.FreezeRotationZ;
    }

    private static void ConfigureGameplayCollider(
        CapsuleCollider targetCollider)
    {
        if (targetCollider == null)
            return;

        targetCollider.enabled = true;
        targetCollider.isTrigger = false;

        targetCollider.direction = 1;

        targetCollider.center =
            new Vector3(
                0f,
                1f,
                0f);

        targetCollider.radius =
            Mathf.Max(
                0.1f,
                targetCollider.radius);

        targetCollider.height =
            Mathf.Max(
                targetCollider.radius * 2f,
                targetCollider.height);
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

        foreach (T component in components)
        {
            if (component == null)
                continue;

            Destroy(component);
        }
    }

    private bool ValidateCharacterMovement(
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

        UltimatePlayerMovement movement =
            character.GetComponentInChildren<UltimatePlayerMovement>(
                includeInactive: true);

        if (movement != null)
            return true;

        Debug.LogError(
            $"TeamSetup could not find UltimatePlayerMovement on the spawned {displayName} '{character.name}'.",
            character);

        return false;
    }

    private void ClearFormationSlots()
    {
        ClearSlot(leaderSlot);
        ClearSlot(leftFollowerSlot);
        ClearSlot(rightFollowerSlot);
    }

    private static void ClearSlot(
        Transform slot)
    {
        if (slot == null)
        {
            Debug.LogWarning(
                "TeamSetup tried to clear a null formation slot.");

            return;
        }

        for (int index = slot.childCount - 1;
             index >= 0;
             index--)
        {
            Transform child =
                slot.GetChild(index);

            if (child == null ||
                IsPermanentSlotHelper(child))
            {
                continue;
            }

            Destroy(child.gameObject);
        }
    }

    private static bool IsPermanentSlotHelper(
        Transform child)
    {
        if (child == null)
            return false;

        return
            child.name == GroundCheckName ||
            child.name == LeftFollowTargetName ||
            child.name == RightFollowTargetName;
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
                ringDrainInterval);
    }

    #endregion

    #region Event Management

    private void SubscribeToEvents()
    {
        if (superFormEventSubscribed ||
            characterSwitch == null)
        {
            return;
        }

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

        LogStateChange(
            $"Character Switch: {(characterSwitch != null ? "Resolved" : "Missing")}");
    }

    private void ResolveReferences()
    {
        hud ??=
            FindAnyObjectByType<HUD>(
                FindObjectsInactive.Include);

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

        LogStateChange(
            $"Leader Slot: {(leaderSlot != null ? "Resolved" : "Missing")}");

        LogStateChange(
            $"Left Follower Slot: {(leftFollowerSlot != null ? "Resolved" : "Missing")}");

        LogStateChange(
            $"Right Follower Slot: {(rightFollowerSlot != null ? "Resolved" : "Missing")}");

        LogStateChange(
            $"HUD: {(hud != null ? "Resolved" : "Missing")}");
    }

    private bool ResolveSlotHelpers()
    {
        if (leaderSlot == null)
        {
            groundCheck = null;
            leftFollowTarget = null;
            rightFollowTarget = null;

            return false;
        }

        groundCheck =
            FindDescendantByName(
                leaderSlot,
                GroundCheckName);

        leftFollowTarget =
            FindDescendantByName(
                leaderSlot,
                LeftFollowTargetName);

        rightFollowTarget =
            FindDescendantByName(
                leaderSlot,
                RightFollowTargetName);

        bool valid = true;

        valid &=
            ValidateResolvedHelper(
                groundCheck,
                GroundCheckName);

        valid &=
            ValidateResolvedHelper(
                leftFollowTarget,
                LeftFollowTargetName);

        valid &=
            ValidateResolvedHelper(
                rightFollowTarget,
                RightFollowTargetName);

        LogStateChange(
            $"Ground Check: {(groundCheck != null ? "Resolved" : "Missing")}");

        LogStateChange(
            $"Left Follow Target: {(leftFollowTarget != null ? "Resolved" : "Missing")}");

        LogStateChange(
            $"Right Follow Target: {(rightFollowTarget != null ? "Resolved" : "Missing")}");

        return valid;
    }

    private bool ValidateResolvedHelper(
        Transform helper,
        string helperName)
    {
        if (helper != null)
            return true;

        Debug.LogError(
            $"TeamSetup could not find '{helperName}' under '{leaderSlot.name}'.",
            this);

        return false;
    }

    private static Transform FindDescendantByName(
        Transform root,
        string objectName)
    {
        if (root == null ||
            string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        string normalizedObjectName =
            NormalizeObjectName(
                objectName);

        Transform[] descendants =
            root.GetComponentsInChildren<Transform>(
                includeInactive: true);

        foreach (Transform descendant in descendants)
        {
            if (descendant == null)
                continue;

            string normalizedDescendantName =
                NormalizeObjectName(
                    descendant.name);

            if (normalizedDescendantName ==
                normalizedObjectName)
            {
                return descendant;
            }
        }

        return null;
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
            .Trim()
            .ToLowerInvariant();
    }

    #endregion

    #region HUD

    private void RefreshHud()
    {
        if (hud == null)
            return;

        hud.Setup(team);
        hud.UpdateRings();
    }

    private void RefreshRingDisplay()
    {
        hud?.UpdateRings();
    }

    #endregion

    #region Validation

    private bool ValidateConfiguration()
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

        if (!ValidateReference(
                leaderSlot,
                "Leader Slot"))
        {
            Debug.LogError(
                $"Expected to find '{LeaderSlotName}' under '{transform.name}'.",
                this);

            valid = false;
        }

        if (!ValidateReference(
                leftFollowerSlot,
                "Left Follower Slot"))
        {
            Debug.LogError(
                $"Expected to find '{LeftFollowerSlotName}' under '{transform.name}'.",
                this);

            valid = false;
        }

        if (!ValidateReference(
                rightFollowerSlot,
                "Right Follower Slot"))
        {
            Debug.LogError(
                $"Expected to find '{RightFollowerSlotName}' under '{transform.name}'.",
                this);

            valid = false;
        }

        if (team != null)
        {
            valid &=
                ValidateReference(
                    team.SpeedCharacterPrefab,
                    "Speed Character Prefab");

            valid &=
                ValidateReference(
                    team.FlyingCharacterPrefab,
                    "Flying Character Prefab");

            valid &=
                ValidateReference(
                    team.PowerCharacterPrefab,
                    "Power Character Prefab");

            valid &=
                ValidateReference(
                    team.SuperCharacterPrefab,
                    "Super Character Prefab");

            valid &=
                ValidateReference(
                    team.SpeedCharacterDefinition,
                    "Speed Character Definition");

            valid &=
                ValidateReference(
                    team.FlyingCharacterDefinition,
                    "Flying Character Definition");

            valid &=
                ValidateReference(
                    team.PowerCharacterDefinition,
                    "Power Character Definition");

            valid &=
                ValidateReference(
                    team.SuperCharacterDefinition,
                    "Super Character Definition");
        }

        if (!IsSupportedLeader(startingLeader))
        {
            Debug.LogError(
                $"Unsupported starting leader: {startingLeader}.",
                this);

            valid = false;
        }

        if (hud == null)
        {
            Debug.LogWarning(
                "TeamSetup HUD reference is not assigned.",
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
            $"TeamSetup: '{displayName}' is missing on '{gameObject.name}'.",
            this);

        return false;
    }

    private static bool IsSupportedLeader(
        CHARACTERTYPES characterType)
    {
        return
            characterType == CHARACTERTYPES.Speed ||
            characterType == CHARACTERTYPES.Fly ||
            characterType == CHARACTERTYPES.Power;
    }

    #endregion

    #region Cleanup

    private void CleanupFailedTeam(
        Transform speedCharacter,
        Transform flyingCharacter,
        Transform powerCharacter)
    {
        if (preserveFailedTeamForDebugging)
        {
            Debug.LogWarning(
                "The failed team was preserved in the Hierarchy for debugging.",
                this);

            return;
        }

        DestroyCharacter(speedCharacter);
        DestroyCharacter(flyingCharacter);
        DestroyCharacter(powerCharacter);
    }

    private void CleanupRuntimeState()
    {
        StopRingDrain();
    }

    private void CleanupDestroyedState()
    {
        CleanupRuntimeState();

        initialized = false;

        groundCheck = null;
        leftFollowTarget = null;
        rightFollowTarget = null;

        characterSwitch = null;

        ringDrainRoutine = null;
        ringDrainWait = null;
    }

    private static void DestroyCharacter(
        Transform character)
    {
        if (character != null)
        {
            Destroy(
                character.gameObject);
        }
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
