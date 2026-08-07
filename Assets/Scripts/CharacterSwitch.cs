using System;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class CharacterSwitch : MonoBehaviour
{
    #region Constants

    private const string LeaderSlotName = "Team Leader";
    private const string LeftFollowerSlotName = "Left Team Member";
    private const string RightFollowerSlotName = "Right Team Member";
    private const string LeftFollowTargetName = "LeftPos";
    private const string RightFollowTargetName = "RightPos";

    #endregion

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

    [Header("Resolved Characters")]
    [SerializeField, HideInInspector] private Transform speedCharacter;
    [SerializeField, HideInInspector] private Transform flyingCharacter;
    [SerializeField, HideInInspector] private Transform powerCharacter;

    [Header("Speed Character Forms")]
    [FormerlySerializedAs("sonic")]
    [SerializeField] private GameObject normalSpeedPrefab;

    [FormerlySerializedAs("superSonic")]
    [SerializeField] private GameObject superSpeedPrefab;
    private const int RequiredSuperRings = 50; // This is only for Super Sonic, not for the other characters.
    [SerializeField] private bool superFormRequiresRings = true;

    [SerializeField] private bool dieWhenSuperRingsReachZero = true;

    [Header("Dependencies")]
    [SerializeField] private TeamSetup teamSetup;
    [SerializeField] private TeamActionController teamActionController;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private HUD hud;

    [Header("Placement")]
    [SerializeField] private Vector3 localPosition = Vector3.zero;
    [SerializeField] private Vector3 localEulerAngles = new(0f, -180f, 0f);

    [Header("Automatic Ground Placement")]
    [SerializeField] private bool snapCharactersToGround = true;
    [SerializeField, Min(0.1f)] private float groundCheckHeight = 2f;
    [SerializeField, Min(0.1f)] private float groundCheckDistance = 6f;
    [SerializeField] private float groundOffset = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private CharacterDefinition speedDefinition;
    private CharacterDefinition flyingDefinition;
    private CharacterDefinition powerDefinition;
    private CharacterDefinition superSpeedDefinition;

    private UltimatePlayerMovement leaderMovement;
    private FollowerNavigation leftFollowerNavigation;
    private FollowerNavigation rightFollowerNavigation;

    private LayerMask groundMask;
    private CHARACTERTYPES currentLeaderType;

    private bool isSuperForm;
    private bool isInitialized;
    private bool isChangingFormation;
    private bool isShuttingDown;

    private readonly struct MovementSnapshot
    {
        public MovementSnapshot(
            Vector3 linearVelocity,
            Vector3 angularVelocity)
        {
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
        }

        public Vector3 LinearVelocity { get; }
        public Vector3 AngularVelocity { get; }
    }

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

        ResolveAllReferences();
    }

    private void OnEnable()
    {
        if (isShuttingDown)
            return;

        ResolveAllReferences();

        if (isInitialized)
        {
            RefreshFormation();
        }
    }

    private void Start()
    {
        ResolveDependencies();

        if (teamSetup == null &&
            !isInitialized)
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

        UpdateSuperForm();
    }

    private void UpdateSuperForm()
    {
        if (!isSuperForm ||
            !superFormRequiresRings)
        {
            return;
        }

        if (GameInstance.currentRings > 0)
        {
            return;
        }

        if (dieWhenSuperRingsReachZero)
        {
            KillSuperCharacter();
        }
        else
        {
            ToggleSuperForm();
        }
    }

    private void KillSuperCharacter()
    {
        Debug.Log(
            "Super Sonic ran out of rings and died.",
            this);

        // TODO:
        // Play death animation.
        // Disable player input.
        // Notify GameManager.
        // Reload checkpoint or restart the level.
    }

    private void OnDisable()
    {
        isChangingFormation = false;
    }

    private void OnDestroy()
    {
        isShuttingDown = true;

        LeaderChanged = null;
        SuperFormChanged = null;

        ClearCachedReferences();
    }

    private void OnValidate()
    {
        if (!IsSupportedType(initialLeaderType))
        {
            initialLeaderType =
                CHARACTERTYPES.Speed;
        }

        groundCheckHeight =
            Mathf.Max(
                0.1f,
                groundCheckHeight);

        groundCheckDistance =
            Mathf.Max(
                0.1f,
                groundCheckDistance);

        if (acceptPlayerInput &&
            (previousLeaderKey == nextLeaderKey ||
             previousLeaderKey == toggleSuperFormKey ||
             nextLeaderKey == toggleSuperFormKey))
        {
            Debug.LogWarning(
                "CharacterSwitch has duplicate input keys.",
                this);
        }
    }

    #endregion

    #region Initialization

    public bool InitializeCharacterSwitch()
    {
        if (isInitialized)
            return true;

        ResolveAllReferences();

        if (!ValidateConfiguration() ||
            !ValidateCharacterInstances())
        {
            return false;
        }

        currentLeaderType =
            IsSupportedType(currentLeaderType)
                ? currentLeaderType
                : initialLeaderType;

        isInitialized = true;

        if (!ApplyLeader(
                currentLeaderType,
                notifyListeners: false))
        {
            isInitialized = false;
            return false;
        }

        return true;
    }

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

        isInitialized = false;
        isChangingFormation = false;
        isSuperForm = false;

        speedCharacter = speed;
        flyingCharacter = flying;
        powerCharacter = power;

        normalSpeedPrefab = normalSpeed;
        superSpeedPrefab = superSpeed;

        currentLeaderType = initialLeader;

        ResolveAllReferences();

        if (!ValidateConfiguration() ||
            !ValidateCharacterInstances())
        {
            return false;
        }

        isInitialized = true;

        if (!ApplyLeader(
                currentLeaderType,
                notifyListeners: false))
        {
            isInitialized = false;
            return false;
        }

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
        bool valid = true;

        if (speed == null ||
            flying == null ||
            power == null)
        {
            Debug.LogError(
                "CharacterSwitch received an incomplete team.",
                this);

            valid = false;
        }

        if (normalSpeed == null ||
            superSpeed == null)
        {
            Debug.LogError(
                "CharacterSwitch requires both normal and super Speed prefabs.",
                this);

            valid = false;
        }

        if (!IsSupportedType(initialLeader))
        {
            Debug.LogError(
                $"Unsupported initial leader: {initialLeader}.",
                this);

            valid = false;
        }

        return valid;
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
        if (!CanSwitch ||
            !IsSupportedType(leaderType))
        {
            return false;
        }

        return ApplyLeader(
            leaderType,
            notifyListeners: true);
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

    private bool ApplyLeader(
        CHARACTERTYPES leaderType,
        bool notifyListeners)
    {
        if (isChangingFormation)
            return false;

        isChangingFormation = true;
        bool applied = false;

        try
        {
            ResolveAllReferences();

            if (!IsSupportedType(leaderType) ||
                !ValidateCharacterInstances() ||
                !ValidateFormationReferences())
            {
                return false;
            }

            GetFormation(
                leaderType,
                out Transform leader,
                out Transform leftCharacter,
                out Transform rightCharacter);

            if (!AssignToSlot(
                    leader,
                    leaderSlot) ||
                !AssignToSlot(
                    leftCharacter,
                    leftFollowerSlot) ||
                !AssignToSlot(
                    rightCharacter,
                    rightFollowerSlot))
            {
                Debug.LogError(
                    "CharacterSwitch failed to assign the complete formation.",
                    this);

                return false;
            }

            currentLeaderType = leaderType;

            SnapFormationToGround();

            if (!RefreshControllers())
            {
                Debug.LogError(
                    "CharacterSwitch could not finish configuring the formation controllers.",
                    this);

                return false;
            }

            RefreshDependentSystems();
            applied = true;
        }
        finally
        {
            isChangingFormation = false;
        }

        if (!applied)
            return false;

        LogStateChange(
            $"Leader changed to {currentLeaderType}.");

        if (notifyListeners)
        {
            LeaderChanged?.Invoke(
                currentLeaderType);
        }

        return true;
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
                isSuperForm &&
                superSpeedDefinition != null
                    ? superSpeedDefinition
                    : speedDefinition,

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
        if (!CanSwitch ||
        speedCharacter == null)
        {
            return false;
        }

        if (!isSuperForm &&
            GameInstance.currentRings <
            RequiredSuperRings)
        {
            Debug.Log(
                $"Super Sonic requires at least {RequiredSuperRings} rings. " +
                $"Current rings: {GameInstance.currentRings}.",
                this);

            return false;
        }

        GameObject replacementPrefab =
            isSuperForm
                ? normalSpeedPrefab
                : superSpeedPrefab;

        if (replacementPrefab == null)
        {
            Debug.LogError(
                "CharacterSwitch cannot toggle Super form because the replacement prefab is missing.",
                this);

            return false;
        }

        MovementSnapshot movementSnapshot =
            CaptureMovementSnapshot();

        Transform previousSpeedCharacter =
            speedCharacter;

        Transform currentParent =
            previousSpeedCharacter.parent;

        int siblingIndex =
            previousSpeedCharacter.GetSiblingIndex();

        GameObject replacement =
            Instantiate(
                replacementPrefab,
                currentParent,
                false);

        if (replacement == null)
            return false;

        Transform replacementTransform =
            replacement.transform;

        replacementTransform.SetSiblingIndex(
            siblingIndex);

        ResetLocalTransform(
            replacementTransform);

        speedCharacter =
            replacementTransform;

        isSuperForm =
            !isSuperForm;

        Destroy(
            previousSpeedCharacter.gameObject);

        if (!ApplyLeader(
        currentLeaderType,
        notifyListeners: false))
        {
            Debug.LogError(
                "CharacterSwitch replaced the Speed character but could not refresh the formation.",
                this);

            return false;
        }

        RestoreMovementSnapshot(
            movementSnapshot);

        SuperFormChanged?.Invoke(
            isSuperForm);

        LogStateChange(
            isSuperForm
                ? "Super form enabled."
                : "Super form disabled.");

        return true;
    }

    private MovementSnapshot CaptureMovementSnapshot()
    {
        Rigidbody playerRigidbody =
            leaderMovement != null
                ? leaderMovement.GetComponent<Rigidbody>()
                : leaderSlot != null
                    ? leaderSlot.GetComponent<Rigidbody>()
                    : null;

        if (playerRigidbody == null)
        {
            return new MovementSnapshot(
                Vector3.zero,
                Vector3.zero);
        }

        return new MovementSnapshot(
            playerRigidbody.linearVelocity,
            playerRigidbody.angularVelocity);
    }

    private void RestoreMovementSnapshot(
        MovementSnapshot snapshot)
    {
        Rigidbody playerRigidbody =
            leaderMovement != null
                ? leaderMovement.GetComponent<Rigidbody>()
                : leaderSlot != null
                    ? leaderSlot.GetComponent<Rigidbody>()
                    : null;

        if (playerRigidbody == null ||
            playerRigidbody.isKinematic)
        {
            return;
        }

        playerRigidbody.linearVelocity =
            snapshot.LinearVelocity;

        playerRigidbody.angularVelocity =
            snapshot.AngularVelocity;

        playerRigidbody.WakeUp();
    }

    #endregion

    #region Formation Slots

    private bool AssignToSlot(
        Transform character,
        Transform slot)
    {
        if (character == null ||
            slot == null ||
            character == slot)
        {
            return false;
        }

        if (character.parent != slot)
        {
            character.SetParent(
                slot,
                false);
        }

        ResetLocalTransform(
            character);

        return character.parent == slot;
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

        if (snapCharactersToGround)
        {
            SnapCharacterToGround(
                character);
        }
    }

    private void SnapFormationToGround()
    {
        SnapCharacterToGround(
            speedCharacter);

        SnapCharacterToGround(
            flyingCharacter);

        SnapCharacterToGround(
            powerCharacter);
    }

    private void SnapCharacterToGround(
        Transform character)
    {
        if (!snapCharactersToGround ||
            character == null)
        {
            return;
        }

        Collider characterCollider =
            character.GetComponentInChildren<Collider>(
                includeInactive: true);

        float bottomOffset =
            characterCollider != null
                ? character.position.y -
                  characterCollider.bounds.min.y
                : 0f;

        Vector3 origin =
            character.position +
            Vector3.up *
            groundCheckHeight;

        if (!Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                groundCheckHeight +
                groundCheckDistance,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        Vector3 groundedPosition =
            character.position;

        groundedPosition.y =
            hit.point.y +
            bottomOffset +
            groundOffset;

        character.position =
            groundedPosition;
    }

    #endregion

    #region Controller Synchronization

    private bool RefreshControllers()
    {
        ResolveDefinitionsFromTeamSetup();
        CacheControllers();

        bool valid = true;

        if (leaderMovement == null)
        {
            Debug.LogError(
                "CharacterSwitch could not find UltimatePlayerMovement on Team Leader.",
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
                    $"CharacterSwitch has no CharacterDefinition for {currentLeaderType}.",
                    this);

                valid = false;
            }
            else if (!leaderMovement.SetCharacterDefinition(
             definition))
            {
                valid = false;
            }
        }

        if (leftFollowerNavigation == null)
        {
            Debug.LogError(
                "CharacterSwitch could not find FollowerNavigation on Left Team Member.",
                this);

            valid = false;
        }
        else
        {
            valid &=
                leftFollowerNavigation.Setup(
                    leftFollowTarget);
        }

        if (rightFollowerNavigation == null)
        {
            Debug.LogError(
                "CharacterSwitch could not find FollowerNavigation on Right Team Member.",
                this);

            valid = false;
        }
        else
        {
            valid &=
                rightFollowerNavigation.Setup(
                    rightFollowTarget);
        }

        return valid;
    }

    private void ApplyAnimatorController(
    Transform character,
    CharacterDefinition definition)
    {
        if (character == null ||
            definition == null)
        {
            return;
        }

        Animator animator =
            character.GetComponentInChildren<Animator>(
                includeInactive: true);

        if (animator == null)
        {
            Debug.LogError(
                $"CharacterSwitch could not find an Animator on '{character.name}'.",
                character);

            return;
        }

        if (definition.animatorProfile == null)
        {
            Debug.LogError(
                $"CharacterSwitch could not find an AnimatorProfile for '{character.name}'.",
                character);

            return;
        }

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

        animator.enabled = true;
        animator.speed = 1f;

        animator.Rebind();
        animator.Update(0f);
    }

    private void CacheControllers()
    {
        leaderMovement =
            leaderSlot != null
                ? leaderSlot.GetComponent<UltimatePlayerMovement>()
                : null;

        leftFollowerNavigation =
            GetOrCreateFollowerNavigation(
                leftFollowerSlot,
                "Left Team Member");

        rightFollowerNavigation =
            GetOrCreateFollowerNavigation(
                rightFollowerSlot,
                "Right Team Member");
    }

    private FollowerNavigation GetOrCreateFollowerNavigation(
    Transform followerSlot,
    string displayName)
    {
        if (followerSlot == null)
        {
            Debug.LogError(
                $"CharacterSwitch cannot repair {displayName} because its slot is missing.",
                this);

            return null;
        }

        FollowerNavigation navigation =
            followerSlot.GetComponent<FollowerNavigation>();

        if (navigation == null)
        {
            navigation =
                followerSlot.gameObject.AddComponent<FollowerNavigation>();

            if (navigation != null)
            {
                Debug.Log(
                    $"CharacterSwitch automatically added FollowerNavigation to '{followerSlot.name}'.",
                    followerSlot);
            }
        }

        if (navigation == null)
        {
            Debug.LogError(
                $"CharacterSwitch could not create FollowerNavigation on '{followerSlot.name}'.",
                followerSlot);

            return null;
        }

        if (!navigation.enabled)
        {
            navigation.enabled = true;
        }

        return navigation;
    }

    private void RefreshDependentSystems()
    {
        if (leaderMovement != null)
        {
            cameraController?.SetTarget(
                leaderMovement.transform,
                snapImmediately: true);
        }

        hud?.SetCharacter(
            currentLeaderType);
    }

    #endregion

    #region Reference Resolution

    private void ResolveAllReferences()
    {
        ResolveDependencies();
        ResolveGroundSettings();
        ResolveFormationReferences();
        ResolveDefinitionsFromTeamSetup();
        CacheControllers();
        EnsureFormationRootsActive();
    }

    private void ResolveDependencies()
    {
        teamSetup ??=
            GetComponent<TeamSetup>();

        teamSetup ??=
            GetComponentInParent<TeamSetup>();

        teamSetup ??=
            TeamSetup.Instance;

        teamSetup ??=
            FindAnyObjectByType<TeamSetup>(
                FindObjectsInactive.Include);

        teamActionController ??=
            GetComponent<TeamActionController>();

        teamActionController ??=
            GetComponentInParent<TeamActionController>();

        teamActionController ??=
            FindAnyObjectByType<TeamActionController>(
                FindObjectsInactive.Include);

        cameraController ??=
            FindAnyObjectByType<CameraController>(
                FindObjectsInactive.Include);

        hud ??=
            FindAnyObjectByType<HUD>(
                FindObjectsInactive.Include);
    }

    private void ResolveGroundSettings()
    {
        int groundLayer =
            LayerMask.NameToLayer(
                "Ground");

        groundMask =
            groundLayer >= 0
                ? 1 << groundLayer
                : Physics.DefaultRaycastLayers;
    }

    private void ResolveFormationReferences()
    {
        Transform searchRoot =
            teamSetup != null
                ? teamSetup.transform
                : transform.root;

        leaderSlot =
            ResolveSlot(
                leaderSlot,
                searchRoot,
                LeaderSlotName);

        leftFollowerSlot =
            ResolveSlot(
                leftFollowerSlot,
                searchRoot,
                LeftFollowerSlotName);

        rightFollowerSlot =
            ResolveSlot(
                rightFollowerSlot,
                searchRoot,
                RightFollowerSlotName);

        leftFollowTarget =
            ResolveSlot(
                leftFollowTarget,
                leaderSlot,
                LeftFollowTargetName);

        rightFollowTarget =
            ResolveSlot(
                rightFollowTarget,
                leaderSlot,
                RightFollowTargetName);
    }

    private void ResolveDefinitionsFromTeamSetup()
    {
        ResolveDependencies();

        TeamComposition composition =
            teamSetup != null
                ? teamSetup.Team
                : null;

        if (composition == null)
            return;

        speedDefinition =
            composition.SpeedCharacterDefinition;

        flyingDefinition =
            composition.FlyingCharacterDefinition;

        powerDefinition =
            composition.PowerCharacterDefinition;

        superSpeedDefinition =
            composition.SuperCharacterDefinition;

        normalSpeedPrefab ??=
            composition.SpeedCharacterPrefab;

        superSpeedPrefab ??=
            composition.SuperCharacterPrefab;
    }

    private static Transform ResolveSlot(
        Transform current,
        Transform root,
        string targetName)
    {
        if (current != null)
            return current;

        return FindDescendantByName(
            root,
            targetName);
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

        string normalizedTarget =
            NormalizeObjectName(
                objectName);

        Transform[] descendants =
            root.GetComponentsInChildren<Transform>(
                includeInactive: true);

        foreach (Transform descendant in descendants)
        {
            if (descendant == null)
                continue;

            if (NormalizeObjectName(descendant.name) ==
                normalizedTarget)
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

    private void EnsureFormationRootsActive()
    {
        SetTransformActive(
            leaderSlot);

        SetTransformActive(
            leftFollowerSlot);

        SetTransformActive(
            rightFollowerSlot);
    }

    private static void SetTransformActive(
        Transform target)
    {
        if (target != null &&
            !target.gameObject.activeSelf)
        {
            target.gameObject.SetActive(true);
        }
    }

    #endregion

    #region Validation

    private bool ValidateConfiguration()
    {
        ResolveAllReferences();

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

        valid &=
            ValidateRequiredReference(
                normalSpeedPrefab,
                "Normal Speed Prefab");

        valid &=
            ValidateRequiredReference(
                superSpeedPrefab,
                "Super Speed Prefab");

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

    #region Cleanup

    private void ClearCachedReferences()
    {
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
        superSpeedDefinition = null;

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
