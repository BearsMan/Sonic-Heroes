using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(UltimatePlayerMovement))]
public sealed class TeamActionController : MonoBehaviour
{
    #region Enums

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
    [SerializeField] private TeamFormation currentFormation = TeamFormation.Speed;
    [SerializeField] private TeamAction currentAction = TeamAction.None;

    [Header("Followers")]
    [SerializeField] private FollowerNavigation leftFollower;
    [SerializeField] private FollowerNavigation rightFollower;

    [Header("Team References")]
    [SerializeField] private Transform speedCharacter;
    [SerializeField] private Transform flyCharacter;
    [SerializeField] private Transform powerCharacter;
    [SerializeField] private UltimatePlayerMovement movement;
    [SerializeField] private RailGrinding railGrinding;

    [Header("Optional Formation Input")]
    [SerializeField] private bool readFormationInput = true;
    [SerializeField] private KeyCode speedFormationKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode flyFormationKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode powerFormationKey = KeyCode.Alpha3;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private bool actionLocked;
    private bool isInitialized;
    private bool isShuttingDown;

    #endregion

    #region Public API

    public TeamFormation CurrentFormation => currentFormation;
    public TeamAction CurrentAction => currentAction;

    public bool IsPerformingAction =>
        currentAction != TeamAction.None;

    public bool ActionLocked => actionLocked;
    public bool IsInitialized => isInitialized;

    public UltimatePlayerMovement Movement => movement;

    public Transform SpeedCharacter => speedCharacter;
    public Transform FlyCharacter => flyCharacter;
    public Transform PowerCharacter => powerCharacter;

    public event Action<TeamFormation> FormationChanged;
    public event Action<TeamAction> ActionStarted;
    public event Action<TeamAction> ActionEnded;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheComponents();
        ResolveReferences();
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
        if (isShuttingDown)
            return;

        CacheComponents();
        ResolveReferences();

        if (!isInitialized)
            return;

        EnsureFollowerRootsActive();
        ApplyFormationToSystems();
    }

    private void Update()
    {
        if (!CanReadFormationInput())
            return;

        if (Input.GetKeyDown(speedFormationKey))
        {
            SetFormation(TeamFormation.Speed);
        }
        else if (Input.GetKeyDown(flyFormationKey))
        {
            SetFormation(TeamFormation.Fly);
        }
        else if (Input.GetKeyDown(powerFormationKey))
        {
            SetFormation(TeamFormation.Power);
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
        ValidateFormationInputKeys();
    }

    #endregion

    #region Initialization

    public bool InitializeController()
    {
        if (isInitialized)
            return true;

        CacheComponents();
        ResolveReferences();

        if (!ValidateConfiguration())
        {
            isInitialized = false;

            Debug.LogError(
                $"TeamActionController failed to initialize on '{name}'.",
                this);

            return false;
        }

        EnsureFollowerRootsActive();
        ApplyFormationToSystems();

        isInitialized = true;

        FormationChanged?.Invoke(
            currentFormation);

        return true;
    }

    private void CacheComponents()
    {
        movement ??=
            GetComponent<UltimatePlayerMovement>();

        movement ??=
            GetComponentInParent<UltimatePlayerMovement>();

        railGrinding ??=
            GetComponent<RailGrinding>();

        railGrinding ??=
            GetComponentInParent<RailGrinding>();
    }

    private void ResolveReferences()
    {
        ResolveFollowers();
        CacheCharacters();
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
                leftFollower = follower;
                continue;
            }

            if (rightFollower == null &&
                follower != leftFollower)
            {
                rightFollower = follower;
                break;
            }
        }
    }

    #endregion

    #region Character References

    private void CacheCharacters()
    {
        if (speedCharacter != null &&
            flyCharacter != null &&
            powerCharacter != null)
        {
            return;
        }

        Transform[] children =
            GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child == null)
                continue;

            string childName = child.name;

            if (speedCharacter == null &&
                childName.Contains("Speed", StringComparison.OrdinalIgnoreCase))
            {
                speedCharacter = child;
                continue;
            }

            if (flyCharacter == null &&
                (childName.Contains("Fly", StringComparison.OrdinalIgnoreCase) ||
                 childName.Contains("Flying", StringComparison.OrdinalIgnoreCase)))
            {
                flyCharacter = child;
                continue;
            }

            if (powerCharacter == null &&
                childName.Contains("Power", StringComparison.OrdinalIgnoreCase))
            {
                powerCharacter = child;
            }
        }
    }

    #endregion

    #region Formation

    public bool SetFormation(
        TeamFormation newFormation)
    {
        if (!isInitialized ||
            actionLocked ||
            IsPerformingAction)
        {
            return false;
        }

        EnsureFollowerRootsActive();

        if (currentFormation == newFormation)
        {
            ApplyFormationToSystems();
            return true;
        }

        currentFormation =
            newFormation;

        ApplyFormationToSystems();

        LogStateChange(
            $"Formation changed to {currentFormation}.");

        FormationChanged?.Invoke(
            currentFormation);

        return true;
    }

    private void ApplyFormationToSystems()
    {
        if (railGrinding == null)
            return;

        RailGrinding.TeamType railTeamType =
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
            railTeamType);
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

    public void SetCharacters(
        Transform speed,
        Transform fly,
        Transform power)
    {
        speedCharacter = speed;
        flyCharacter = fly;
        powerCharacter = power;
    }

    #endregion

    #region Team Actions

    public bool CanBeginAction(
        TeamAction action,
        TeamFormation requiredFormation,
        bool mustBeGrounded = false,
        bool mustBeAirborne = false)
    {
        if (!isInitialized ||
            action == TeamAction.None ||
            actionLocked ||
            IsPerformingAction ||
            currentFormation != requiredFormation)
        {
            return false;
        }

        if (movement == null)
            return true;

        if (mustBeGrounded &&
            !movement.IsGrounded)
        {
            return false;
        }

        if (mustBeAirborne &&
            movement.IsGrounded)
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
            $"Started Action: {currentAction}.");

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
            $"Finished Action: {endedAction}.");

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
    }

    #endregion

    #region Followers

    public void SetFollowers(
        FollowerNavigation left,
        FollowerNavigation right)
    {
        leftFollower = left;
        rightFollower = right;

        EnsureFollowerRootsActive();
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

        GameObject followerRoot =
            follower.gameObject;

        if (!followerRoot.activeSelf)
        {
            followerRoot.SetActive(true);
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

    #endregion

    #region Validation

    private void ValidateSerializedState()
    {
        if (!Enum.IsDefined(
                typeof(TeamFormation),
                currentFormation))
        {
            currentFormation =
                TeamFormation.Speed;
        }

        if (!Enum.IsDefined(
                typeof(TeamAction),
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

        if (speedFormationKey == flyFormationKey ||
            speedFormationKey == powerFormationKey ||
            flyFormationKey == powerFormationKey)
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

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        if (!IsPerformingAction)
            return;

        currentAction =
            TeamAction.None;

        movement?.EnableMovement();
    }

    private void CleanupDestroyedState()
    {
        CleanupRuntimeState();

        isInitialized = false;
        actionLocked = false;

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
