using System;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class CharacterSwitch : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private bool acceptPlayerInput = true;
    [SerializeField] private KeyCode previousLeaderKey = KeyCode.LeftBracket;
    [SerializeField] private KeyCode nextLeaderKey = KeyCode.RightBracket;
    [SerializeField] private KeyCode toggleSuperFormKey = KeyCode.Home;

    [Header("Initial State")]
    [FormerlySerializedAs("currentCharacterType")]
    [SerializeField]
    private CHARACTERTYPES initialLeaderType =
        CHARACTERTYPES.Speed;

    [Header("Formation Slots")]
    [FormerlySerializedAs("player")]
    [SerializeField] private Transform leaderSlot;

    [FormerlySerializedAs("leftSlot")]
    [SerializeField] private Transform leftFollowerSlot;

    [FormerlySerializedAs("rightSlot")]
    [SerializeField] private Transform rightFollowerSlot;

    [Header("Character Instances")]
    [FormerlySerializedAs("speedCharacter")]
    [SerializeField] private Transform speedCharacter;

    [FormerlySerializedAs("flyingCharacter")]
    [SerializeField] private Transform flyingCharacter;

    [FormerlySerializedAs("powerCharacter")]
    [SerializeField] private Transform powerCharacter;

    [Header("Speed Character Forms")]
    [FormerlySerializedAs("sonic")]
    [SerializeField] private GameObject normalSpeedPrefab;

    [FormerlySerializedAs("superSonic")]
    [SerializeField] private GameObject superSpeedPrefab;

    [Header("Dependencies")]
    [SerializeField] private TeamSetup teamSetup;
    [SerializeField] private HUD hud;

    [Header("Placement")]
    [SerializeField] private Vector3 localPosition = Vector3.zero;
    [SerializeField]
    private Vector3 localEulerAngles =
        new(0f, 180f, 0f);

    private UltimatePlayerMovement leaderMovement;
    private FollowerNavigation leftFollowerNavigation;
    private FollowerNavigation rightFollowerNavigation;

    private CHARACTERTYPES currentLeaderType;
    private bool isSuperForm;
    private bool isInitialized;
    private bool isChangingFormation;

    public event Action<CHARACTERTYPES> LeaderChanged;
    public event Action<bool> SuperFormChanged;

    public CHARACTERTYPES CurrentLeaderType =>
        currentLeaderType;

    public Transform SpeedCharacter =>
        speedCharacter;

    public Transform FlyingCharacter =>
        flyingCharacter;

    public Transform PowerCharacter =>
        powerCharacter;

    public bool IsSuperForm =>
        isSuperForm;

    public bool CanSwitch => isInitialized && !isChangingFormation;

    public bool ConfigureTeam(
    Transform speed,
    Transform flying,
    Transform power,
    GameObject normalSpeed,
    GameObject superSpeed,
    CHARACTERTYPES initialLeader)
    {
        if (speed == null ||
            flying == null ||
            power == null)
        {
            Debug.LogError(
                "CharacterSwitch received an incomplete team.",
                this);

            return false;
        }

        speedCharacter = speed;
        flyingCharacter = flying;
        powerCharacter = power;

        if (normalSpeed == null || superSpeed == null)
        {
            Debug.LogError(
                "CharacterSwitch requires both Speed prefabs.",
                this);

            return false;
        }

        normalSpeedPrefab = normalSpeed;
        superSpeedPrefab = superSpeed;

        ResolveDependencies();
        CacheControllers();

        if (!ValidateConfiguration())
        {
            return false;
        }

        if (!IsSupportedType(initialLeader))
        {
            Debug.LogError(
                $"Unsupported initial leader: {initialLeader}.",
                this);

            return false;
        }

        currentLeaderType = initialLeader;

        isSuperForm = false;
        isInitialized = true;

        ApplyLeader(
            currentLeaderType,
            notifyListeners: false);

        return true;
    }

    private void Awake()
    {
        currentLeaderType =
            IsSupportedType(initialLeaderType)
                ? initialLeaderType
                : CHARACTERTYPES.Speed;

        isSuperForm = false;
        isInitialized = false;
    }

    private void Update()
    {
        if (!acceptPlayerInput ||
            !CanSwitch)
        {
            return;
        }

        if (Input.GetKeyDown(previousLeaderKey))
        {
            SelectPreviousLeader();
            return;
        }

        if (Input.GetKeyDown(nextLeaderKey))
        {
            SelectNextLeader();
            return;
        }

        if (Input.GetKeyDown(toggleSuperFormKey))
            ToggleSuperForm();
    }

    public void SelectPreviousLeader()
    {
        CHARACTERTYPES nextType =
            currentLeaderType switch
            {
                CHARACTERTYPES.Speed =>
                    CHARACTERTYPES.Fly,

                CHARACTERTYPES.Fly =>
                    CHARACTERTYPES.Power,

                CHARACTERTYPES.Power =>
                    CHARACTERTYPES.Speed,

                _ =>
                    CHARACTERTYPES.Speed
            };

        SetLeader(nextType);
    }

    public void SelectNextLeader()
    {
        CHARACTERTYPES nextType =
            currentLeaderType switch
            {
                CHARACTERTYPES.Speed =>
                    CHARACTERTYPES.Power,

                CHARACTERTYPES.Power =>
                    CHARACTERTYPES.Fly,

                CHARACTERTYPES.Fly =>
                    CHARACTERTYPES.Speed,

                _ =>
                    CHARACTERTYPES.Speed
            };

        SetLeader(nextType);
    }

    public bool SetLeader(
        CHARACTERTYPES leaderType)
    {
        if (!isInitialized)
            return false;

        if (!IsSupportedType(leaderType))
        {
            Debug.LogWarning(
                $"Unsupported leader type: {leaderType}.",
                this);

            return false;
        }

        if (isChangingFormation)
            return false;

        ApplyLeader(
            leaderType,
            notifyListeners: true);

        return true;
    }

    public void RefreshFormation()
    {
        if (!isInitialized)
            return;

        ApplyLeader(
            currentLeaderType,
            notifyListeners: false);
    }

    public bool ToggleSuperForm()
    {
        if (!isInitialized || isChangingFormation)
        {
            return false;
        }

        GameObject replacementPrefab =
            isSuperForm
                ? normalSpeedPrefab
                : superSpeedPrefab;

        if (replacementPrefab == null)
        {
            Debug.LogWarning(
                isSuperForm
                    ? "Normal Speed prefab is not assigned."
                    : "Super Speed prefab is not assigned.",
                this);

            return false;
        }

        if (speedCharacter == null)
        {
            Debug.LogError(
                "The Speed character instance is missing.",
                this);

            return false;
        }

        isChangingFormation = true;

        try
        {
            Transform previousSpeedCharacter =
                speedCharacter;

            Transform currentParent =
                previousSpeedCharacter.parent;

            int siblingIndex =
                previousSpeedCharacter.GetSiblingIndex();

            GameObject replacement =
                Instantiate(
                    replacementPrefab,
                    currentParent);

            Transform replacementTransform =
                replacement.transform;

            replacementTransform.SetSiblingIndex(
                siblingIndex);

            ResetLocalTransform(
                replacementTransform);

            Destroy(
                previousSpeedCharacter.gameObject);

            speedCharacter =
                replacementTransform;

            isSuperForm =
                !isSuperForm;

            ApplyLeader(
                currentLeaderType,
                notifyListeners: false);
        }
        finally
        {
            isChangingFormation = false;
        }

        SuperFormChanged?.Invoke(
            isSuperForm);

        return true;
    }

    public void SetInputEnabled(
        bool enabled)
    {
        acceptPlayerInput =
            enabled;
    }

    public CharacterSwitchState CaptureState()
    {
        return new CharacterSwitchState(
            currentLeaderType,
            isSuperForm);
    }

    public void RestoreState(
    CharacterSwitchState state)
    {
        if (!isInitialized)
            return;

        if (state.IsSuperForm != isSuperForm)
            ToggleSuperForm();

        SetLeader(state.LeaderType);
    }

    private void ApplyLeader(
        CHARACTERTYPES leaderType,
        bool notifyListeners)
    {
        isChangingFormation = true;

        currentLeaderType =
            leaderType;

        GetFormation(
            leaderType,
            out Transform leader,
            out Transform leftCharacter,
            out Transform rightCharacter);

        AssignToSlot(
            leader,
            leaderSlot);

        AssignToSlot(
            leftCharacter,
            leftFollowerSlot);

        AssignToSlot(
            rightCharacter,
            rightFollowerSlot);

        RefreshControllers();
        RefreshHud();

        isChangingFormation = false;

        if (notifyListeners)
        {
            LeaderChanged?.Invoke(
                currentLeaderType);
        }
    }

    private void GetFormation(
        CHARACTERTYPES leaderType,
        out Transform leader,
        out Transform leftCharacter,
        out Transform rightCharacter)
    {
        switch (leaderType)
        {
            case CHARACTERTYPES.Fly:
                leader = flyingCharacter;
                leftCharacter = powerCharacter;
                rightCharacter = speedCharacter;
                break;

            case CHARACTERTYPES.Power:
                leader = powerCharacter;
                leftCharacter = speedCharacter;
                rightCharacter = flyingCharacter;
                break;

            case CHARACTERTYPES.Speed:
            default:
                leader = speedCharacter;
                leftCharacter = flyingCharacter;
                rightCharacter = powerCharacter;
                break;
        }
    }

    private Transform GetCurrentLeader()
    {
        return currentLeaderType switch
        {
            CHARACTERTYPES.Speed => speedCharacter,
            CHARACTERTYPES.Fly => flyingCharacter,
            CHARACTERTYPES.Power => powerCharacter,
            _ => speedCharacter
        };
    }
    private void AssignToSlot(
        Transform character,
        Transform slot)
    {
        if (character == null ||
            slot == null)
        {
            return;
        }

        character.SetParent(
            slot,
            worldPositionStays: false);

        ResetLocalTransform(
            character);
    }

    private void ResetLocalTransform(
        Transform character)
    {
        if (character == null)
            return;

        character.localPosition =
            localPosition;

        character.localRotation =
            Quaternion.Euler(
                localEulerAngles);
    }

    private void RefreshControllers()
    {
        CacheControllers();

        Transform leader = GetCurrentLeader();

        if (leaderMovement != null)
            leaderMovement.SetupAnimation();

        if (leftFollowerNavigation != null)
        {
            leftFollowerNavigation.Initialize(
                leader);
        }

        if (rightFollowerNavigation != null)
        {
            rightFollowerNavigation.Initialize(
                leader);
        }
    }

    private void CacheControllers()
    {
        leaderMovement =
            GetComponentFromSlot<UltimatePlayerMovement>(
                leaderSlot);

        leftFollowerNavigation =
            GetComponentFromSlot<FollowerNavigation>(
                leftFollowerSlot);

        rightFollowerNavigation =
            GetComponentFromSlot<FollowerNavigation>(
                rightFollowerSlot);
    }

    private static T GetComponentFromSlot<T>(
        Transform slot)
        where T : Component
    {
        if (slot == null)
            return null;

        T component =
            slot.GetComponent<T>();

        return component != null
            ? component
            : slot.GetComponentInChildren<T>();
    }

    private void RefreshHud()
    {
        if (hud == null)
            return;

        hud.SetCharacter(
            currentLeaderType);
    }

    private void ResolveDependencies()
    {
        if (teamSetup == null)
            teamSetup = GetComponent<TeamSetup>();

        if (teamSetup == null)
            teamSetup = TeamSetup.Instance;

        if (hud == null)
        {
            hud = UnityEngine.Object.FindAnyObjectByType<HUD>(FindObjectsInactive.Include);
        }
    }

    private bool ValidateConfiguration()
    {
        bool isValid = true;

        isValid &=
            ValidateRequiredReference(
                leaderSlot,
                "Leader Slot");

        isValid &=
            ValidateRequiredReference(
                leftFollowerSlot,
                "Left Follower Slot");

        isValid &=
            ValidateRequiredReference(
                rightFollowerSlot,
                "Right Follower Slot");

        if (teamSetup == null)
        {
            Debug.LogWarning(
                "CharacterSwitch could not find TeamSetup.",
                this);
        }

        if (hud == null)
        {
            Debug.LogWarning(
                "CharacterSwitch could not find HUD.",
                this);
        }

        return isValid;
    }

    private bool ValidateRequiredReference(
        UnityEngine.Object reference,
        string displayName)
    {
        if (reference != null)
            return true;

        Debug.LogError(
            $"CharacterSwitch {displayName} is not assigned.",
            this);

        return false;
    }

    private static bool IsSupportedType(
        CHARACTERTYPES type)
    {
        return
            type == CHARACTERTYPES.Speed ||
            type == CHARACTERTYPES.Fly ||
            type == CHARACTERTYPES.Power;
    }

    private void OnValidate()
    {
        if (!IsSupportedType(
                initialLeaderType))
        {
            initialLeaderType =
                CHARACTERTYPES.Speed;
        }
    }
}

[Serializable]
public readonly struct CharacterSwitchState
{
    public CharacterSwitchState(
        CHARACTERTYPES leaderType,
        bool isSuperForm)
    {
        LeaderType =
            leaderType;

        IsSuperForm =
            isSuperForm;
    }

    public CHARACTERTYPES LeaderType { get; }

    public bool IsSuperForm { get; }
}