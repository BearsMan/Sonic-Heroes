using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterSwitch))]
public sealed class TeamSetup : MonoBehaviour
{
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
    [SerializeField, Min(0.1f)]
    private float ringDrainInterval = 3f;

    [Header("Debugging")]
    [SerializeField]
    private bool preserveFailedTeamForDebugging = true;

    [SerializeField, Min(1)]
    private int ringsDrainedPerInterval = 1;

    private const string GroundCheckName = "GroundCheck";

    private const string LeftFollowTargetName = "LeftPos";

    private const string RightFollowTargetName = "RightPos";

    private Transform groundCheck;
    private Transform leftFollowTarget;
    private Transform rightFollowTarget;

    private CharacterSwitch shCharacterSwitch;
    private Coroutine ringDrainRoutine;
    private WaitForSeconds ringDrainWait;

    private bool initialized;
    private bool shuttingDown;

    public static TeamSetup Instance { get; private set; }

    public TeamComposition Team => team;

    public PlayableTeam PlayableTeam =>
        team != null
            ? team.PlayableTeam
            : default;

    public bool IsInitialized => initialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"Duplicate TeamSetup detected. Existing object: " +
                $"'{Instance.gameObject.name}'. Duplicate object: " +
                $"'{gameObject.name}'.",
                this);

            enabled = false;
            return;
        }

        Instance = this;

        shCharacterSwitch =
            GetComponent<CharacterSwitch>();

        ringDrainWait =
            new WaitForSeconds(ringDrainInterval);

        ResolveReferences();
    }

    private void Start()
    {
        if (!ValidateConfiguration())
        {
            enabled = false;
            return;
        }

        InitializeTeam();
    }

    private void OnEnable()
    {
        if (shCharacterSwitch != null)
        {
            shCharacterSwitch.SuperFormChanged +=
                HandleSuperFormChanged;
        }
    }

    private void OnDisable()
    {
        if (shCharacterSwitch != null)
        {
            shCharacterSwitch.SuperFormChanged -=
                HandleSuperFormChanged;
        }

        StopRingDrain();
    }

    private void OnDestroy()
    {
        shuttingDown = true;

        if (Instance == this)
            Instance = null;
    }

    public bool InitializeTeam()
    {
        if (initialized)
            return true;

        if (!ValidateConfiguration())
            return false;

        if (!ResolveSlotHelpers())
        {
            Debug.LogError(
                "TeamSetup cannot initialize because permanent " +
                "leader-slot helpers are missing.",
                this);

            return false;
        }

        ClearSlot(leaderSlot);
        ClearSlot(leftFollowerSlot);
        ClearSlot(rightFollowerSlot);

        if (!ResolveSlotHelpers())
        {
            Debug.LogError(
                "TeamSetup lost one or more permanent helpers " +
                "while clearing the slots.",
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

            CleanupFailedTeam(speedCharacter, flyingCharacter, powerCharacter);

            return false;
        }

        bool configured = 
            shCharacterSwitch.ConfigureTeam
            (speedCharacter, flyingCharacter, powerCharacter, 
            team.SpeedCharacterPrefab, team.SuperCharacterPrefab, startingLeader);

        if (!configured)
        {
            Debug.LogError(
                "CharacterSwitch rejected the spawned team.",
                shCharacterSwitch);

            CleanupFailedTeam(
                speedCharacter,
                flyingCharacter,
                powerCharacter);

            return false;
        }

        GameInstance.currentTeam =
            (int)team.PlayableTeam;

        if (hud != null)
        {
            hud.Setup(team);
            hud.UpdateRings();
        }

        initialized = true;

        return true;
    }

    public bool RebuildTeam()
    {
        StopRingDrain();

        initialized = false;

        return InitializeTeam();
    }

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
                $"TeamSetup cannot spawn '{prefab.name}' " +
                "into a null slot.",
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

        if (characterTransform.parent != slot)
        {
            Debug.LogError(
                $"'{character.name}' was not parented to " +
                $"'{slot.name}'.",
                character);

            Destroy(character);
            return null;
        }

        return characterTransform;
    }

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
            shuttingDown)
        {
            return;
        }

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
               shCharacterSwitch != null &&
               shCharacterSwitch.IsSuperForm)
        {
            if (GameInstance.currentRings <= 0)
                break;

            yield return ringDrainWait;

            if (shCharacterSwitch == null ||
                !shCharacterSwitch.IsSuperForm)
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
            shCharacterSwitch == null ||
            !shCharacterSwitch.IsSuperForm)
        {
            yield break;
        }

        shCharacterSwitch.ToggleSuperForm();
    }

    private void RefreshRingDisplay()
    {
        hud?.UpdateRings();
    }

    private void ResolveReferences()
    {
        if (hud == null)
        {
            hud = UnityEngine.Object.FindAnyObjectByType<HUD>(
                FindObjectsInactive.Include);
        }
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

        if (groundCheck == null)
        {
            Debug.LogError(
                $"TeamSetup could not find '{GroundCheckName}' " +
                $"under '{leaderSlot.name}'.",
                this);

            valid = false;
        }

        if (leftFollowTarget == null)
        {
            Debug.LogError(
                $"TeamSetup could not find '{LeftFollowTargetName}' " +
                $"under '{leaderSlot.name}'.",
                this);

            valid = false;
        }

        if (rightFollowTarget == null)
        {
            Debug.LogError(
                $"TeamSetup could not find '{RightFollowTargetName}' " +
                $"under '{leaderSlot.name}'.",
                this);

            valid = false;
        }

        return valid;
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

    private void CleanupFailedTeam(
    Transform speedCharacter,
    Transform flyingCharacter,
    Transform powerCharacter)
    {
        if (preserveFailedTeamForDebugging)
        {
            Debug.LogWarning(
                "The failed team was preserved in the Hierarchy " +
                "for debugging.",
                this);

            return;
        }

        DestroyCharacter(speedCharacter);
        DestroyCharacter(flyingCharacter);
        DestroyCharacter(powerCharacter);
    }
    private bool ValidateConfiguration()
    {
        bool valid = true;

        valid &=
            ValidateReference(
                team,
                "Team Composition");

        valid &=
            ValidateReference(
                shCharacterSwitch,
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

    private static void ClearSlot(
    Transform slot)
    {
        if (slot == null)
        {
            Debug.LogWarning(
                "TeamSetup tried to clear a null formation slot.");

            return;
        }

        for (int i = slot.childCount - 1;
             i >= 0;
             i--)
        {
            Transform child =
                slot.GetChild(i);

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

    private static void DestroyCharacter(
        Transform character)
    {
        if (character != null)
            Destroy(character.gameObject);
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
}