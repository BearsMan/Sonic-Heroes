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

    [SerializeField, Min(1)]
    private int ringsDrainedPerInterval = 1;

    private CharacterSwitch characterSwitch;
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

        characterSwitch =
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
        if (characterSwitch != null)
        {
            characterSwitch.SuperFormChanged +=
                HandleSuperFormChanged;
        }
    }

    private void OnDisable()
    {
        if (characterSwitch != null)
        {
            characterSwitch.SuperFormChanged -=
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

        ClearSlot(leaderSlot);
        ClearSlot(leftFollowerSlot);
        ClearSlot(rightFollowerSlot);

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

            DestroyCharacter(speedCharacter);
            DestroyCharacter(flyingCharacter);
            DestroyCharacter(powerCharacter);

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

            DestroyCharacter(speedCharacter);
            DestroyCharacter(flyingCharacter);
            DestroyCharacter(powerCharacter);

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
        if (prefab == null ||
            slot == null)
        {
            return null;
        }

        GameObject character =
            Instantiate(
                prefab,
                slot);

        character.transform.SetLocalPositionAndRotation(
            Vector3.zero,
            Quaternion.Euler(
                0f,
                180f,
                0f));

        return character.transform;
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
               characterSwitch != null &&
               characterSwitch.IsSuperForm)
        {
            if (GameInstance.currentRings <= 0)
                break;

            yield return ringDrainWait;

            if (characterSwitch == null ||
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

    private void RefreshRingDisplay()
    {
        hud?.UpdateRings();
    }

    private void ResolveReferences()
    {
        if (hud == null)
        {
            hud =
                UnityEngine.Object.FindAnyObjectByType<HUD>(
                    FindObjectsInactive.Include);
        }
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

    private static void ClearSlot(Transform slot)
    {
        if (slot == null)
            return;

        for (int i = slot.childCount - 1; i >= 0; i--)
        {
            Transform child = slot.GetChild(i);

            if (child == null)
                continue;

            if (child.name == "GroundCheck" ||
                child.name == "LeftPos" ||
                child.name == "RightPos")
            {
                continue;
            }

            Destroy(child.gameObject);
        }
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