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
    [SerializeField] private Transform leaderSlot;
    [SerializeField] private Transform leftFollowerSlot;
    [SerializeField] private Transform rightFollowerSlot;

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

    public Transform LeaderSlot => leaderSlot;
    public Transform LeftFollowerSlot => leftFollowerSlot;
    public Transform RightFollowerSlot => rightFollowerSlot;

    public Transform GroundCheck => groundCheck;
    public Transform LeftFollowTarget => leftFollowTarget;
    public Transform RightFollowTarget => rightFollowTarget;

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

    public bool InitializeTeam()
    {
        if (initialized)
            return true;

        CacheComponents();
        ResolveReferences();
        RebuildRingDrainWait();

        if (!ValidateConfiguration())
            return false;

        if (!ResolveSlotHelpers())
        {
            Debug.LogError(
                "TeamSetup cannot initialize because the leader-slot helpers are missing.",
                this);

            return false;
        }

        ClearFormationSlots();

        if (!ResolveSlotHelpers())
        {
            Debug.LogError(
                "TeamSetup lost one or more permanent helpers while clearing the formation slots.",
                this);

            return false;
        }

        Transform speedCharacter =
            SpawnCharacter(
                team.SpeedCharacterPrefab,
                leaderSlot);

        Transform flyingCharacter =
            SpawnCharacter(
                team.FlyingCharacterPrefab,
                leftFollowerSlot);

        Transform powerCharacter =
            SpawnCharacter(
                team.PowerCharacterPrefab,
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

        RefreshHud();

        initialized = true;

        LogStateChange(
            $"Initialized team: {team.PlayableTeam}.");

        return true;
    }

    public bool RebuildTeam()
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
        Transform slot)
    {
        if (prefab == null)
        {
            Debug.LogError(
                "TeamSetup cannot spawn a null prefab.",
                this);

            return null;
        }

        if (slot == null)
        {
            Debug.LogError(
                $"TeamSetup cannot spawn '{prefab.name}' into a null slot.",
                this);

            return null;
        }

        GameObject character =
            Instantiate(
                prefab,
                slot);

        if (character == null)
        {
            Debug.LogError(
                $"TeamSetup failed to instantiate '{prefab.name}'.",
                this);

            return null;
        }

        Transform characterTransform =
            character.transform;

        characterTransform.SetLocalPositionAndRotation(
            Vector3.zero,
            Quaternion.Euler(
                0f,
                180f,
                0f));

        if (characterTransform.parent == slot)
            return characterTransform;

        Debug.LogError(
            $"'{character.name}' was not parented to '{slot.name}'.",
            character);

        Destroy(character);

        return null;
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
    }

    private void ResolveReferences()
    {
        hud ??=
            FindAnyObjectByType<HUD>(
                FindObjectsInactive.Include);
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

        Transform[] descendants =
            root.GetComponentsInChildren<Transform>(
                includeInactive: true);

        foreach (Transform descendant in descendants)
        {
            if (descendant != null &&
                descendant.name == objectName)
            {
                return descendant;
            }
        }

        return null;
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

        valid &=
            ValidateReference(
                leaderSlot,
                "Leader Slot");

        valid &=
            ValidateReference(
                leftFollowerSlot,
                "Left Follower Slot");

        valid &=
            ValidateReference(
                rightFollowerSlot,
                "Right Follower Slot");

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
            $"TeamSetup {displayName} is not assigned.",
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
