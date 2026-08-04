using System;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class CharacterSwitch : MonoBehaviour
{
    #region Inspector

    [Header("Input")]
    [SerializeField] private bool acceptPlayerInput = true;
    [SerializeField] private KeyCode previousLeaderKey = KeyCode.LeftBracket;
    [SerializeField] private KeyCode nextLeaderKey = KeyCode.RightBracket;
    [SerializeField] private KeyCode toggleSuperFormKey = KeyCode.Home;

    [Header("Initial State")]
    [FormerlySerializedAs("currentCharacterType")]
    [SerializeField] private CHARACTERTYPES initialLeaderType = CHARACTERTYPES.Speed;

    [Header("Formation Slots")]
    [FormerlySerializedAs("player")]
    [SerializeField] private Transform leaderSlot;

    [FormerlySerializedAs("leftSlot")]
    [SerializeField] private Transform leftFollowerSlot;

    [FormerlySerializedAs("rightSlot")]
    [SerializeField] private Transform rightFollowerSlot;

    [Header("Follower Targets")]
    [SerializeField] private Transform leftFollowTarget;
    [SerializeField] private Transform rightFollowTarget;

    [Header("Character Instances")]
    [FormerlySerializedAs("speedCharacter")]
    [SerializeField] private Transform speedCharacter;

    [FormerlySerializedAs("flyingCharacter")]
    [SerializeField] private Transform flyingCharacter;

    [FormerlySerializedAs("powerCharacter")]
    [SerializeField] private Transform powerCharacter;

    [Header("Character Definitions")]
    [SerializeField] private CharacterDefinition speedDefinition;
    [SerializeField] private CharacterDefinition flyingDefinition;
    [SerializeField] private CharacterDefinition powerDefinition;

    [Header("Speed Character Forms")]
    [FormerlySerializedAs("sonic")]
    [SerializeField] private GameObject normalSpeedPrefab;

    [FormerlySerializedAs("superSonic")]
    [SerializeField] private GameObject superSpeedPrefab;

    [Header("Dependencies")]
    [SerializeField] private TeamSetup teamSetup;
    [SerializeField] private TeamActionController teamActionController;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private HUD hud;

    [Header("Placement")]
    [SerializeField] private Vector3 localPosition = Vector3.zero;
    [SerializeField] private Vector3 localEulerAngles = new(0f, 180f, 0f);

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private UltimatePlayerMovement leaderMovement;
    private FollowerNavigation leftFollowerNavigation;
    private FollowerNavigation rightFollowerNavigation;

    private CHARACTERTYPES currentLeaderType;

    private bool isSuperForm;
    private bool isInitialized;
    private bool isChangingFormation;
    private bool isShuttingDown;

    #endregion

    #region Public API

    public event Action<CHARACTERTYPES> LeaderChanged;
    public event Action<bool> SuperFormChanged;

    public CHARACTERTYPES CurrentLeaderType => currentLeaderType;

    public Transform SpeedCharacter => speedCharacter;
    public Transform FlyingCharacter => flyingCharacter;
    public Transform PowerCharacter => powerCharacter;

    public Transform CurrentLeader => GetCurrentLeader();

    public bool IsSuperForm => isSuperForm;
    public bool IsInitialized => isInitialized;
    public bool CanSwitch => isInitialized && !isChangingFormation;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        currentLeaderType =
            IsSupportedType(initialLeaderType)
                ? initialLeaderType
                : CHARACTERTYPES.Speed;

        isSuperForm = false;
        isInitialized = false;

        ResolveDependencies();
        ResolveFormationReferences();
        EnsureFormationRootsActive();
        CacheControllers();
    }

    private void OnEnable()
    {
        if (isShuttingDown)
            return;

        ResolveDependencies();
        ResolveFormationReferences();
        EnsureFormationRootsActive();
        CacheControllers();

        if (isInitialized)
        {
            RefreshFormation();
        }
    }

    private void Start()
    {
        if (!isInitialized)
        {
            InitializeCharacterSwitch();
        }
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
        {
            ToggleSuperForm();
        }
    }

    private void OnDisable()
    {
        CleanupRuntimeState();
    }

    private void OnDestroy()
    {
        isShuttingDown = true;
        CleanupDestroyedState();
    }

    private void OnValidate()
    {
        ValidateSerializedState();
        ValidateInputKeys();
    }

    #endregion

    #region Initialization

    public bool ConfigureTeam(
    Transform speed,
    Transform flying,
    Transform power,
    GameObject normalSpeed,
    GameObject superSpeed,
    CHARACTERTYPES initialLeader)
    {
        if (!ValidateTeamArguments(
                speed,
                flying,
                power,
                normalSpeed,
                superSpeed,
                initialLeader))
        {
            return false;
        }

        speedCharacter = speed;
        flyingCharacter = flying;
        powerCharacter = power;

        normalSpeedPrefab = normalSpeed;
        superSpeedPrefab = superSpeed;

        UltimatePlayerMovement speedMovement =
            speedCharacter.GetComponentInChildren<UltimatePlayerMovement>(
                includeInactive: true);

        UltimatePlayerMovement flyingMovement =
            flyingCharacter.GetComponentInChildren<UltimatePlayerMovement>(
                includeInactive: true);

        UltimatePlayerMovement powerMovement =
            powerCharacter.GetComponentInChildren<UltimatePlayerMovement>(
                includeInactive: true);

        speedDefinition =
            speedMovement != null
                ? speedMovement.CharacterDefinition
                : null;

        flyingDefinition =
            flyingMovement != null
                ? flyingMovement.CharacterDefinition
                : null;

        powerDefinition =
            powerMovement != null
                ? powerMovement.CharacterDefinition
                : null;

        currentLeaderType = initialLeader;
        isSuperForm = false;

        ResolveDependencies();
        ResolveFormationReferences();
        EnsureFormationRootsActive();
        CacheControllers();

        if (!ValidateConfiguration())
        {
            isInitialized = false;
            return false;
        }

        isInitialized = true;

        ApplyLeader(
            currentLeaderType,
            notifyListeners: false);

        return true;
    }

    public bool InitializeCharacterSwitch()
    {
        if (isInitialized)
            return true;

        ResolveDependencies();
        ResolveFormationReferences();
        EnsureFormationRootsActive();
        CacheControllers();

        if (!ValidateConfiguration() ||
            !ValidateCharacterInstances())
        {
            isInitialized = false;
            return false;
        }

        currentLeaderType =
            IsSupportedType(currentLeaderType)
                ? currentLeaderType
                : initialLeaderType;

        isInitialized = true;

        ApplyLeader(
            currentLeaderType,
            notifyListeners: false);

        return true;
    }

    private bool ValidateTeamArguments(
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

        if (normalSpeed == null ||
            superSpeed == null)
        {
            Debug.LogError(
                "CharacterSwitch requires both Speed prefabs.",
                this);

            return false;
        }

        if (!IsSupportedType(initialLeader))
        {
            Debug.LogError(
                $"Unsupported initial leader: {initialLeader}.",
                this);

            return false;
        }

        return true;
    }

    #endregion

    #region Leader Switching

    public void SelectPreviousLeader()
    {
        CHARACTERTYPES nextType =
            currentLeaderType switch
            {
                CHARACTERTYPES.Speed => CHARACTERTYPES.Fly,
                CHARACTERTYPES.Fly => CHARACTERTYPES.Power,
                CHARACTERTYPES.Power => CHARACTERTYPES.Speed,
                _ => CHARACTERTYPES.Speed
            };

        SetLeader(nextType);
    }

    public void SelectNextLeader()
    {
        CHARACTERTYPES nextType =
            currentLeaderType switch
            {
                CHARACTERTYPES.Speed => CHARACTERTYPES.Power,
                CHARACTERTYPES.Power => CHARACTERTYPES.Fly,
                CHARACTERTYPES.Fly => CHARACTERTYPES.Speed,
                _ => CHARACTERTYPES.Speed
            };

        SetLeader(nextType);
    }

    public bool SetLeader(
        CHARACTERTYPES leaderType)
    {
        if (!CanSwitch)
            return false;

        if (!IsSupportedType(leaderType))
        {
            Debug.LogWarning(
                $"Unsupported leader type: {leaderType}.",
                this);

            return false;
        }

        ApplyLeader(
            leaderType,
            notifyListeners: true);

        return true;
    }

    public void RefreshFormation()
    {
        if (!isInitialized ||
            isChangingFormation)
        {
            return;
        }

        ApplyLeader(
            currentLeaderType,
            notifyListeners: false);
    }

    private void ApplyLeader(
        CHARACTERTYPES leaderType,
        bool notifyListeners)
    {
        if (isChangingFormation)
            return;

        isChangingFormation = true;
        bool formationApplied = false;

        try
        {
            ResolveFormationReferences();
            EnsureFormationRootsActive();

            if (!IsSupportedType(leaderType) ||
                !ValidateCharacterInstances() ||
                !ValidateFormationReferences())
            {
                return;
            }

            GetFormation(
                leaderType,
                out Transform leader,
                out Transform leftCharacter,
                out Transform rightCharacter);

            EnsureCharacterActive(leader);
            EnsureCharacterActive(leftCharacter);
            EnsureCharacterActive(rightCharacter);

            bool assigned =
                AssignToSlot(
                    leader,
                    leaderSlot);

            assigned &=
                AssignToSlot(
                    leftCharacter,
                    leftFollowerSlot);

            assigned &=
                AssignToSlot(
                    rightCharacter,
                    rightFollowerSlot);

            if (!assigned)
            {
                Debug.LogError(
                    "CharacterSwitch failed to assign the complete formation.",
                    this);

                return;
            }

            currentLeaderType = leaderType;

            if (!RefreshControllers())
            {
                Debug.LogError(
                    "CharacterSwitch could not finish configuring the formation controllers.",
                    this);

                return;
            }

            RefreshDependentSystems();
            formationApplied = true;
        }
        finally
        {
            isChangingFormation = false;
        }

        if (!formationApplied)
            return;

        LogStateChange(
            $"Leader changed to {currentLeaderType}.");

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

    private CharacterDefinition GetCharacterDefinition(
    CHARACTERTYPES characterType)
    {
        return characterType switch
        {
            CHARACTERTYPES.Speed =>
                speedDefinition,

            CHARACTERTYPES.Fly =>
                flyingDefinition,

            CHARACTERTYPES.Power =>
                powerDefinition,

            _ =>
                null
        };
    }

    #endregion

    #region Super Form

    public bool ToggleSuperForm()
    {
        if (!CanSwitch)
            return false;

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
        bool replacementSucceeded = false;

        try
        {
            Transform previousSpeedCharacter = speedCharacter;
            Transform currentParent = previousSpeedCharacter.parent;
            int siblingIndex = previousSpeedCharacter.GetSiblingIndex();

            GameObject replacement =
                Instantiate(
                    replacementPrefab,
                    currentParent);

            Transform replacementTransform = replacement.transform;

            replacementTransform.SetSiblingIndex(
                siblingIndex);

            ResetLocalTransform(
                replacementTransform);

            speedCharacter = replacementTransform;
            isSuperForm = !isSuperForm;

            Destroy(
                previousSpeedCharacter.gameObject);

            replacementSucceeded = true;
        }
        finally
        {
            isChangingFormation = false;
        }

        if (!replacementSucceeded)
            return false;

        RefreshFormation();

        LogStateChange(
            isSuperForm
                ? "Super form enabled."
                : "Super form disabled.");

        SuperFormChanged?.Invoke(
            isSuperForm);

        return true;
    }

    #endregion

    #region Formation Slots

    private bool AssignToSlot(
        Transform character,
        Transform slot)
    {
        if (character == null)
        {
            Debug.LogError(
                "CharacterSwitch attempted to assign a null character.",
                this);

            return false;
        }

        if (slot == null)
        {
            Debug.LogError(
                $"CharacterSwitch attempted to assign '{character.name}' to a null slot.",
                this);

            return false;
        }

        character.SetParent(
            slot,
            worldPositionStays: false);

        ResetLocalTransform(
            character);

        if (character.parent == slot)
            return true;

        Debug.LogError(
            $"CharacterSwitch failed to parent '{character.name}' to '{slot.name}'.",
            character);

        return false;
    }

    private void ResetLocalTransform(
        Transform character)
    {
        if (character == null)
            return;

        character.localPosition = localPosition;
        character.localRotation =
            Quaternion.Euler(
                localEulerAngles);
    }

    private void EnsureFormationRootsActive()
    {
        SetTransformActive(leaderSlot);
        SetTransformActive(leftFollowerSlot);
        SetTransformActive(rightFollowerSlot);
    }

    private static void EnsureCharacterActive(
        Transform character)
    {
        SetTransformActive(character);
    }

    private static void SetTransformActive(
        Transform targetTransform)
    {
        if (targetTransform == null)
            return;

        if (!targetTransform.gameObject.activeSelf)
        {
            targetTransform.gameObject.SetActive(true);
        }
    }

    #endregion

    #region Controller Synchronization

    private bool RefreshControllers()
    {
        CacheControllers();

        bool valid = true;

        if (leaderMovement == null)
        {
            Debug.LogError(
                "CharacterSwitch could not find UltimatePlayerMovement in the leader slot.",
                this);

            valid = false;
        }
        else
        {
            CharacterDefinition definition =
                GetCharacterDefinition(
                    currentLeaderType);

            if (definition == null)
            {
                Debug.LogError(
                    $"CharacterSwitch has no CharacterDefinition assigned for {currentLeaderType}.",
                    this);

                valid = false;
            }
            else if (!leaderMovement.SetCharacterDefinition(
                         definition))
            {
                Debug.LogError(
                    $"CharacterSwitch failed to apply the CharacterDefinition for {currentLeaderType}.",
                    this);

                valid = false;
            }
            else
            {
                leaderMovement.SetupAnimation();
            }
        }

        if (leftFollowerNavigation == null)
        {
            Debug.LogError(
                "CharacterSwitch could not find FollowerNavigation in the left follower slot.",
                this);

            valid = false;
        }
        else
        {
            valid &=
                leftFollowerNavigation.Initialize(
                    leftFollowTarget);
        }

        if (rightFollowerNavigation == null)
        {
            Debug.LogError(
                "CharacterSwitch could not find FollowerNavigation in the right follower slot.",
                this);

            valid = false;
        }
        else
        {
            valid &=
                rightFollowerNavigation.Initialize(
                    rightFollowTarget);
        }

        return valid;
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

    private void RefreshDependentSystems()
    {
        if (leaderMovement != null)
        {
            cameraController?.SetTarget(
                leaderMovement.transform,
                snapImmediately: true);
        }

        RefreshHud();
    }

    private static T GetComponentFromSlot<T>(
        Transform slot)
        where T : Component
    {
        if (slot == null)
            return null;

        T component = slot.GetComponent<T>();

        return
            component != null
                ? component
                : slot.GetComponentInChildren<T>(
                    includeInactive: true);
    }

    #endregion

    #region State And Input

    public void SetInputEnabled(
        bool enabled)
    {
        acceptPlayerInput = enabled;
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
        {
            ToggleSuperForm();
        }

        SetLeader(
            state.LeaderType);
    }

    #endregion

    #region Reference Resolution

    private void ResolveDependencies()
    {
        teamSetup ??=
            GetComponent<TeamSetup>();

        teamSetup ??=
            TeamSetup.Instance;

        teamActionController ??=
            GetComponent<TeamActionController>();

        teamActionController ??=
            GetComponentInParent<TeamActionController>();

        cameraController ??=
            FindAnyObjectByType<CameraController>();

        hud ??=
            FindAnyObjectByType<HUD>(
                FindObjectsInactive.Include);
    }

    private void ResolveFormationReferences()
    {
        Transform searchRoot =
            teamSetup != null
                ? teamSetup.transform
                : transform.root;

        leaderSlot ??=
            FindDescendantByName(
                searchRoot,
                "Test Player");

        leftFollowerSlot ??=
            FindDescendantByName(
                searchRoot,
                "Left Team Member");

        rightFollowerSlot ??=
            FindDescendantByName(
                searchRoot,
                "Right Team Member");

        if (leaderSlot == null)
            return;

        leftFollowTarget ??=
            FindDescendantByName(
                leaderSlot,
                "LeftPos");

        rightFollowTarget ??=
            FindDescendantByName(
                leaderSlot,
                "RightPos");
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
        hud?.SetCharacter(
            currentLeaderType);
    }

    #endregion

    #region Validation

    private void ValidateSerializedState()
    {
        if (!IsSupportedType(initialLeaderType))
        {
            initialLeaderType =
                CHARACTERTYPES.Speed;
        }
    }

    private void ValidateInputKeys()
    {
        if (!acceptPlayerInput)
            return;

        if (previousLeaderKey == nextLeaderKey ||
            previousLeaderKey == toggleSuperFormKey ||
            nextLeaderKey == toggleSuperFormKey)
        {
            Debug.LogWarning(
                "CharacterSwitch has duplicate input keys.",
                this);
        }
    }

    private bool ValidateConfiguration()
    {
        bool valid = true;

        valid &=
            ValidateRequiredReference(
                leaderSlot,
                "Leader Slot");

        valid &=
            ValidateRequiredReference(
                leftFollowerSlot,
                "Left Follower Slot");

        valid &=
            ValidateRequiredReference(
                rightFollowerSlot,
                "Right Follower Slot");

        valid &=
            ValidateRequiredReference(
                leftFollowTarget,
                "Left Follow Target");

        valid &=
            ValidateRequiredReference(
                rightFollowTarget,
                "Right Follow Target");

        valid &=
    ValidateRequiredReference(
        speedDefinition,
        "Speed Character Definition");

        valid &=
            ValidateRequiredReference(
                flyingDefinition,
                "Flying Character Definition");

        valid &=
            ValidateRequiredReference(
                powerDefinition,
                "Power Character Definition");

        if (teamSetup == null)
        {
            Debug.LogWarning(
                "CharacterSwitch could not find TeamSetup.",
                this);
        }

        if (teamActionController == null)
        {
            Debug.LogWarning(
                "CharacterSwitch could not find TeamActionController.",
                this);
        }

        if (cameraController == null)
        {
            Debug.LogWarning(
                "CharacterSwitch could not find CameraController.",
                this);
        }

        if (hud == null)
        {
            Debug.LogWarning(
                "CharacterSwitch could not find HUD.",
                this);
        }

        return valid;
    }

    private bool ValidateCharacterInstances()
    {
        bool valid = true;

        valid &=
            ValidateRequiredReference(
                speedCharacter,
                "Speed Character");

        valid &=
            ValidateRequiredReference(
                flyingCharacter,
                "Flying Character");

        valid &=
            ValidateRequiredReference(
                powerCharacter,
                "Power Character");

        return valid;
    }

    private bool ValidateFormationReferences()
    {
        return
            leaderSlot != null &&
            leftFollowerSlot != null &&
            rightFollowerSlot != null &&
            leftFollowTarget != null &&
            rightFollowTarget != null;
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

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        isChangingFormation = false;
    }

    private void CleanupDestroyedState()
    {
        CleanupRuntimeState();

        isInitialized = false;

        LeaderChanged = null;
        SuperFormChanged = null;

        leaderMovement = null;
        leftFollowerNavigation = null;
        rightFollowerNavigation = null;

        teamSetup = null;
        teamActionController = null;
        cameraController = null;
        hud = null;

        leaderSlot = null;
        leftFollowerSlot = null;
        rightFollowerSlot = null;

        leftFollowTarget = null;
        rightFollowTarget = null;

        speedCharacter = null;
        flyingCharacter = null;
        powerCharacter = null;

        speedDefinition = null;
        flyingDefinition = null;
        powerDefinition = null;

        normalSpeedPrefab = null;
        superSpeedPrefab = null;
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

[Serializable]
public readonly struct CharacterSwitchState
{
    public CharacterSwitchState(
        CHARACTERTYPES leaderType,
        bool isSuperForm)
    {
        LeaderType = leaderType;
        IsSuperForm = isSuperForm;
    }

    public CHARACTERTYPES LeaderType { get; }
    public bool IsSuperForm { get; }
}
