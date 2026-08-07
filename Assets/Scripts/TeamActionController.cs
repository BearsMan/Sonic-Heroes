using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(UltimatePlayerMovement))]
public sealed class TeamActionController : MonoBehaviour
{
    #region Types

    public enum TeamFormation
    {
        Speed,
        Fly,
        Power
    }

    public enum TeamAction
    {
        None,
        HomingAttack,
        ChaosControl,
        ShadowChaosAttack,
        AmyHammerAttack,
        TornadoJump,
        TriangleDive,
        TriangleJump,
        RocketAccel,
        ThunderShoot,
        FireDunk,
        LightDash,
        TeamBlast
    }

    #endregion

    #region Inspector

    [Header("Current Team State")]
    [SerializeField]
    private TeamFormation currentFormation =
        TeamFormation.Speed;

    [SerializeField]
    private TeamAction currentAction =
        TeamAction.None;

    [Header("Followers")]
    [SerializeField]
    private FollowerNavigation leftFollower;

    [SerializeField]
    private FollowerNavigation rightFollower;

    [Header("Team References")]
    [SerializeField]
    private Transform speedCharacter;

    [SerializeField]
    private Transform flyCharacter;

    [SerializeField]
    private Transform powerCharacter;

    [SerializeField]
    private UltimatePlayerMovement movement;

    [SerializeField]
    private RailGrinding railGrinding;

    [SerializeField]
    private CharacterSwitch characterSwitch;

    [SerializeField]
    private CameraController cameraController;

    [SerializeField]
    private HUD hud;

    [SerializeField]
    private TeamBlast teamBlast;

    [Header("Optional Formation Input")]
    [SerializeField]
    private bool readFormationInput = true;

    [SerializeField]
    private KeyCode speedFormationKey =
        KeyCode.Alpha1;

    [SerializeField]
    private KeyCode flyFormationKey =
        KeyCode.Alpha2;

    [SerializeField]
    private KeyCode powerFormationKey =
        KeyCode.Alpha3;

    [Header("Debug")]
    [SerializeField]
    private bool logStateChanges;

    #endregion

    #region Runtime State

    private bool actionLocked;
    private bool isInitialized;

    private UltimatePlayerMovement[] cachedCharacterMovements;

    #endregion

    #region Public API

    public TeamFormation CurrentFormation =>
        currentFormation;

    public TeamAction CurrentAction =>
        currentAction;

    public bool IsPerformingAction =>
        currentAction !=
        TeamAction.None;

    public bool ActionLocked =>
        actionLocked;

    public bool IsInitialized =>
        isInitialized;

    public UltimatePlayerMovement Movement =>
        movement;

    public Transform SpeedCharacter =>
        speedCharacter;

    public Transform FlyCharacter =>
        flyCharacter;

    public Transform PowerCharacter =>
        powerCharacter;

    public event Action<TeamFormation>
        FormationChanged;

    public event Action<TeamAction>
        ActionStarted;

    public event Action<TeamAction>
        ActionEnded;

    public bool Setup(
        Transform speed,
        Transform fly,
        Transform power)
    {
        SetCharacters(
            speed,
            fly,
            power);

        ResolveDependencies();

        return InitializeController();
    }

    public void SetCharacters(
        Transform speed,
        Transform fly,
        Transform power)
    {
        speedCharacter = speed;
        flyCharacter = fly;
        powerCharacter = power;

        if (!isInitialized)
            return;

        RefreshControllers();
    }

    public void SetFollowers(
        FollowerNavigation left,
        FollowerNavigation right)
    {
        leftFollower = left;
        rightFollower = right;

        EnsureFollowerRootsActive();
    }

    public bool SetFormation(
        TeamFormation newFormation)
    {
        if (!CanChangeFormation(
                newFormation))
        {
            return false;
        }

        if (currentFormation ==
            newFormation)
        {
            return true;
        }

        currentFormation =
            newFormation;

        RefreshControllers();

        LogStateChange(
            $"Formation changed to {currentFormation}.");

        FormationChanged?.Invoke(
            currentFormation);

        return true;
    }

    public Transform GetFormationLeader()
    {
        return currentFormation switch
        {
            TeamFormation.Speed =>
                speedCharacter,

            TeamFormation.Fly =>
                flyCharacter,

            TeamFormation.Power =>
                powerCharacter,

            _ =>
                speedCharacter
        };
    }

    public bool CanBeginAction(
        TeamAction action,
        TeamFormation requiredFormation,
        bool mustBeGrounded = false,
        bool mustBeAirborne = false)
    {
        if (!isInitialized ||
            action ==
                TeamAction.None ||
            actionLocked ||
            IsPerformingAction ||
            currentFormation !=
                requiredFormation)
        {
            return false;
        }

        if (mustBeGrounded &&
            (movement == null ||
             !movement.IsGrounded))
        {
            return false;
        }

        if (mustBeAirborne &&
            (movement == null ||
             movement.IsGrounded))
        {
            return false;
        }

        return true;
    }

    public bool TryBeginAction(
        TeamAction action,
        TeamFormation requiredFormation,
        bool mustBeGrounded = false,
        bool mustBeAirborne = false,
        bool surrenderMovementControl = true)
    {
        if (!CanBeginAction(
                action,
                requiredFormation,
                mustBeGrounded,
                mustBeAirborne))
        {
            return false;
        }

        currentAction =
            action;

        if (surrenderMovementControl)
        {
            movement?.DisableMovement();
        }

        LogStateChange(
            $"Started action: {currentAction}.");

        ActionStarted?.Invoke(
            currentAction);

        return true;
    }

    public void EndAction(
        bool restoreMovementControl = true)
    {
        if (!IsPerformingAction)
            return;

        TeamAction endedAction =
            currentAction;

        currentAction =
            TeamAction.None;

        if (restoreMovementControl)
        {
            movement?.EnableMovement();
        }

        LogStateChange(
            $"Finished action: {endedAction}.");

        ActionEnded?.Invoke(
            endedAction);
    }

    public void CancelCurrentAction()
    {
        if (!IsPerformingAction)
            return;

        EndAction(
            restoreMovementControl: true);
    }

    public void SetActionLock(
        bool locked)
    {
        actionLocked =
            locked;

        if (actionLocked &&
            IsPerformingAction)
        {
            CancelCurrentAction();
        }

        RefreshTeamBlast();
    }

    public void EnableFollowers()
    {
        EnsureFollowerRootsActive();

        leftFollower?.EnableAgent();
        rightFollower?.EnableAgent();
    }

    public void DisableFollowers()
    {
        leftFollower?.DisableAgent();
        rightFollower?.DisableAgent();
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveDependencies();
        ConfigureComponents();
    }

    private void Start()
    {
        if (!InitializeController())
        {
            enabled = false;
        }
    }

    private void OnEnable()
    {
        ResolveDependencies();
        ConfigureComponents();

        if (!isInitialized)
            return;

        SubscribeToCharacterSwitch();
        RestoreRuntimeState();
    }

    private void Update()
    {
        if (!CanReadFormationInput())
            return;

        ReadFormationInput();
    }

    private void OnDisable()
    {
        UnsubscribeFromCharacterSwitch();
        CleanupRuntimeState();
    }

    private void OnDestroy()
    {
        UnsubscribeFromCharacterSwitch();
        CleanupDestroyedState();
    }

    private void OnValidate()
    {
        ValidateSerializedState();
        ValidateFormationInputKeys();
    }

    #endregion

    #region Initialization

    private bool InitializeController()
    {
        if (isInitialized)
            return true;

        ResolveDependencies();
        ConfigureComponents();

        if (!ValidateConfiguration())
        {
            isInitialized = false;

            Debug.LogError(
                $"TeamActionController failed to initialize on '{name}'.",
                this);

            return false;
        }

        ResetRuntimeState();

        isInitialized = true;

        SubscribeToCharacterSwitch();
        RefreshControllers();

        FormationChanged?.Invoke(
            currentFormation);

        return true;
    }

    private void ResolveDependencies()
    {
        ResolveMovement();
        ResolveRailGrinding();
        ResolveCharacterSwitch();
        ResolveCharacters();
        ResolveFollowers();
        ResolveCameraController();
        ResolveHUD();
        ResolveTeamBlast();
    }

    private void ConfigureComponents()
    {
        EnsureFollowerRootsActive();
    }

    private void ResolveMovement()
    {
        movement ??=
            GetComponent<UltimatePlayerMovement>();

        movement ??=
            GetComponentInParent<UltimatePlayerMovement>();
    }

    private void ResolveRailGrinding()
    {
        railGrinding ??=
            GetComponent<RailGrinding>();

        railGrinding ??=
            GetComponentInParent<RailGrinding>();
    }

    private void ResolveCharacterSwitch()
    {
        characterSwitch ??=
            GetComponent<CharacterSwitch>();

        characterSwitch ??=
            GetComponentInParent<CharacterSwitch>();
    }

    private void ResolveCharacters()
    {
        if (speedCharacter != null &&
            flyCharacter != null &&
            powerCharacter != null)
        {
            return;
        }

        RefreshCharacterCache();

        if (cachedCharacterMovements == null ||
            cachedCharacterMovements.Length == 0)
        {
            return;
        }

        foreach (UltimatePlayerMovement characterMovement
                 in cachedCharacterMovements)
        {
            if (characterMovement == null)
                continue;

            CharacterDefinition definition =
                characterMovement.CharacterDefinition;

            if (definition == null ||
                !definition.IsValid())
            {
                continue;
            }

            Transform character =
                characterMovement.transform;

            if (character == null)
                continue;

            switch (definition.characterType)
            {
                case CharacterDefinition.CharacterType.Speed:
                    speedCharacter ??=
                        character;
                    break;

                case CharacterDefinition.CharacterType.Fly:
                    flyCharacter ??=
                        character;
                    break;

                case CharacterDefinition.CharacterType.Power:
                    powerCharacter ??=
                        character;
                    break;
            }

            if (speedCharacter != null &&
                flyCharacter != null &&
                powerCharacter != null)
            {
                return;
            }
        }
    }

    private void RefreshCharacterCache()
    {
        if (cachedCharacterMovements != null &&
            cachedCharacterMovements.Length > 0 &&
            HasValidCharacterCache())
        {
            return;
        }

        Transform searchRoot =
            transform.root != null
                ? transform.root
                : transform;

        cachedCharacterMovements =
            searchRoot.GetComponentsInChildren<UltimatePlayerMovement>(
                includeInactive: true);
    }

    private bool HasValidCharacterCache()
    {
        if (cachedCharacterMovements == null ||
            cachedCharacterMovements.Length == 0)
        {
            return false;
        }

        foreach (UltimatePlayerMovement characterMovement
                 in cachedCharacterMovements)
        {
            if (characterMovement != null)
                return true;
        }

        return false;
    }

    private void ResolveFollowers()
    {
        if (leftFollower != null &&
            rightFollower != null)
        {
            return;
        }

        FollowerNavigation[] followers =
            GetComponentsInChildren<FollowerNavigation>(
                includeInactive: true);

        foreach (FollowerNavigation follower in followers)
        {
            if (follower == null)
                continue;

            if (leftFollower == null)
            {
                leftFollower =
                    follower;

                continue;
            }

            if (rightFollower == null &&
                follower != leftFollower)
            {
                rightFollower =
                    follower;

                return;
            }
        }
    }

    private void ResolveCameraController()
    {
        if (cameraController != null)
            return;

        cameraController =
            GetComponentInParent<CameraController>();

        cameraController ??=
            GetComponentInChildren<CameraController>(
                includeInactive: true);

        cameraController ??=
            FindAnyObjectByType<CameraController>(
                FindObjectsInactive.Include);
    }

    private void ResolveHUD()
    {
        if (hud != null)
            return;

        hud =
            GetComponentInParent<HUD>();

        hud ??=
            GetComponentInChildren<HUD>(
                includeInactive: true);

        hud ??=
            FindAnyObjectByType<HUD>(
                FindObjectsInactive.Include);
    }

    private void ResolveTeamBlast()
    {
        if (teamBlast != null)
            return;

        teamBlast =
            GetComponent<TeamBlast>();

        teamBlast ??=
            GetComponentInParent<TeamBlast>();

        teamBlast ??=
            GetComponentInChildren<TeamBlast>(
                includeInactive: true);

        teamBlast ??=
            FindAnyObjectByType<TeamBlast>(
                FindObjectsInactive.Include);
    }

    private void ResetRuntimeState()
    {
        actionLocked = false;

        if (!IsValidTeamAction(
                currentAction))
        {
            currentAction =
                TeamAction.None;
        }

        if (!IsValidFormation(
                currentFormation))
        {
            currentFormation =
                TeamFormation.Speed;
        }
    }

    private void RestoreRuntimeState()
    {
        RefreshControllers();
    }

    #endregion

    #region Formation

    private bool CanChangeFormation(
        TeamFormation formation)
    {
        return
            isInitialized &&
            IsValidFormation(
                formation) &&
            !actionLocked &&
            !IsPerformingAction;
    }

    private void RefreshControllers()
    {
        ApplyFormationToSystems();
        RefreshCamera();
        RefreshHUD();
        RefreshTeamBlast();
        EnsureFollowerRootsActive();
    }

    private void ApplyFormationToSystems()
    {
        if (railGrinding == null)
            return;

        RailGrinding.TeamType railTeam =
            currentFormation switch
            {
                TeamFormation.Speed =>
                    RailGrinding.TeamType.Speed,

                TeamFormation.Fly =>
                    RailGrinding.TeamType.Fly,

                TeamFormation.Power =>
                    RailGrinding.TeamType.Power,

                _ =>
                    RailGrinding.TeamType.Speed
            };

        railGrinding.SetTeamType(
            railTeam);
    }

    private void RefreshCamera()
    {
        if (cameraController == null)
            return;

        Transform leader =
            GetFormationLeader();

        if (leader == null)
            return;

        cameraController.SetTarget(
            leader,
            snapImmediately: true);
    }

    private void RefreshHUD()
    {
        if (hud == null)
            return;

        CHARACTERTYPES leaderType =
            currentFormation switch
            {
                TeamFormation.Speed =>
                    CHARACTERTYPES.Speed,

                TeamFormation.Fly =>
                    CHARACTERTYPES.Fly,

                TeamFormation.Power =>
                    CHARACTERTYPES.Power,

                _ =>
                    CHARACTERTYPES.Speed
            };

        hud.SetCharacter(
            leaderType);

        hud.UpdateHUD();
    }

    private void RefreshTeamBlast()
    {
        if (teamBlast == null)
            return;

        teamBlast.SetInputEnabled(
            isInitialized &&
            !actionLocked &&
            !IsPerformingAction);
    }

    #endregion

    #region Character Switching

    private void SubscribeToCharacterSwitch()
    {
        if (characterSwitch == null)
            return;

        characterSwitch.LeaderChanged -=
            HandleLeaderChanged;

        characterSwitch.LeaderChanged +=
            HandleLeaderChanged;
    }

    private void UnsubscribeFromCharacterSwitch()
    {
        if (characterSwitch == null)
            return;

        characterSwitch.LeaderChanged -=
            HandleLeaderChanged;
    }

    private void HandleLeaderChanged(
        CHARACTERTYPES leaderType)
    {
        TeamFormation formation =
            leaderType switch
            {
                CHARACTERTYPES.Speed =>
                    TeamFormation.Speed,

                CHARACTERTYPES.Fly =>
                    TeamFormation.Fly,

                CHARACTERTYPES.Power =>
                    TeamFormation.Power,

                _ =>
                    TeamFormation.Speed
            };

        SetFormation(
            formation);
    }

    #endregion

    #region Followers

    private void EnsureFollowerRootsActive()
    {
        SetFollowerRootActive(
            leftFollower);

        SetFollowerRootActive(
            rightFollower);
    }

    private static void SetFollowerRootActive(
        FollowerNavigation follower)
    {
        if (follower == null)
            return;

        Transform followerTransform =
            follower.transform;

        if (followerTransform == null)
            return;

        if (!followerTransform.gameObject.activeSelf)
        {
            followerTransform.gameObject.SetActive(
                true);
        }
    }

    #endregion

    #region Input

    private bool CanReadFormationInput()
    {
        return
            isInitialized &&
            readFormationInput &&
            !IsPerformingAction &&
            !actionLocked;
    }

    private void ReadFormationInput()
    {
        if (Input.GetKeyDown(
                speedFormationKey))
        {
            SetFormation(
                TeamFormation.Speed);

            return;
        }

        if (Input.GetKeyDown(
                flyFormationKey))
        {
            SetFormation(
                TeamFormation.Fly);

            return;
        }

        if (Input.GetKeyDown(
                powerFormationKey))
        {
            SetFormation(
                TeamFormation.Power);
        }
    }

    #endregion

    #region Validation

    private void ValidateSerializedState()
    {
        if (!IsValidFormation(
                currentFormation))
        {
            currentFormation =
                TeamFormation.Speed;
        }

        if (!IsValidTeamAction(
                currentAction))
        {
            currentAction =
                TeamAction.None;
        }
    }

    private void ValidateFormationInputKeys()
    {
        if (!readFormationInput)
            return;

        if (speedFormationKey ==
                flyFormationKey ||
            speedFormationKey ==
                powerFormationKey ||
            flyFormationKey ==
                powerFormationKey)
        {
            Debug.LogWarning(
                "TeamActionController has duplicate formation input keys.",
                this);
        }
    }

    private bool ValidateConfiguration()
    {
        bool valid = true;

        valid &=
            ValidateReference(
                movement,
                nameof(UltimatePlayerMovement));

        valid &=
            ValidateReference(
                speedCharacter,
                "Speed Character");

        valid &=
            ValidateReference(
                flyCharacter,
                "Fly Character");

        valid &=
            ValidateReference(
                powerCharacter,
                "Power Character");

        if (railGrinding == null)
        {
            Debug.LogWarning(
                "TeamActionController could not find RailGrinding.",
                this);
        }

        if (leftFollower == null)
        {
            Debug.LogWarning(
                "TeamActionController could not find the left follower.",
                this);
        }

        if (rightFollower == null)
        {
            Debug.LogWarning(
                "TeamActionController could not find the right follower.",
                this);
        }

        if (cameraController == null)
        {
            Debug.LogWarning(
                "TeamActionController could not find CameraController.",
                this);
        }

        if (hud == null)
        {
            Debug.LogWarning(
                "TeamActionController could not find HUD.",
                this);
        }

        if (teamBlast == null)
        {
            Debug.LogWarning(
                "TeamActionController could not find TeamBlast.",
                this);
        }

        return valid;
    }

    private bool ValidateReference(
        UnityEngine.Object reference,
        string displayName)
    {
        if (reference != null)
            return true;

        Debug.LogError(
            $"TeamActionController requires {displayName}.",
            this);

        return false;
    }

    private static bool IsValidFormation(
        TeamFormation formation)
    {
        return Enum.IsDefined(
            typeof(TeamFormation),
            formation);
    }

    private static bool IsValidTeamAction(
        TeamAction action)
    {
        return Enum.IsDefined(
            typeof(TeamAction),
            action);
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        if (IsPerformingAction)
        {
            TeamAction endedAction =
                currentAction;

            currentAction =
                TeamAction.None;

            movement?.EnableMovement();

            ActionEnded?.Invoke(
                endedAction);
        }

        actionLocked = false;

        RefreshTeamBlast();
    }

    private void CleanupDestroyedState()
    {
        CleanupRuntimeState();

        isInitialized = false;
        actionLocked = false;
        currentAction = TeamAction.None;

        FormationChanged = null;
        ActionStarted = null;
        ActionEnded = null;

        leftFollower = null;
        rightFollower = null;

        speedCharacter = null;
        flyCharacter = null;
        powerCharacter = null;

        movement = null;
        railGrinding = null;
        characterSwitch = null;

        cameraController = null;
        hud = null;
        teamBlast = null;

        cachedCharacterMovements = null;
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