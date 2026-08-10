using System;
using UnityEngine;

public class TeamActionController : MonoBehaviour
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
        AmyHammerAttack,
        TornadoJump,
        TriangleDive,
        TriangleJump,
        RocketAccel,
        ThunderShoot,
        FireDunk,
        LightDash,
        TeamBlast,
        SonicOverdrive,
        SuperSonicPower
    }

    #endregion

    #region Team

    [Header("Team")]

    [SerializeField]
    private Transform speedCharacter;

    [SerializeField]
    private Transform flyCharacter;

    [SerializeField]
    private Transform powerCharacter;

    [SerializeField]
    private TeamFormation currentFormation =
        TeamFormation.Speed;

    #endregion

    #region Controllers

    [Header("Controllers")]

    [SerializeField]
    private UltimatePlayerMovement movement;

    [SerializeField]
    private CharacterSwitch characterSwitch;

    [SerializeField]
    private FollowerNavigation leftFollower;

    [SerializeField]
    private FollowerNavigation rightFollower;

    [SerializeField]
    private CameraController cameraController;

    [SerializeField]
    private RailGrinding railGrinding;

    [SerializeField]
    private HUD hud;

    #endregion

    #region Input

    [Header("Formation Input")]

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

    #endregion

    #region Runtime

    private TeamAction currentAction =
        TeamAction.None;

    private bool actionLocked;
    private bool initialized;

    #endregion

    #region Events

    public event Action<TeamFormation> FormationChanged;
    public event Action<TeamAction> ActionStarted;
    public event Action<TeamAction> ActionEnded;

    #endregion

    #region Properties

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
        initialized;

    public UltimatePlayerMovement Movement =>
        movement;

    public Transform SpeedCharacter =>
        speedCharacter;

    public Transform FlyCharacter =>
        flyCharacter;

    public Transform PowerCharacter =>
        powerCharacter;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        InitializeController();
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        SynchronizeFormation();

        if (CanReadFormationInput())
        {
            ReadFormationInput();
        }
    }

    private void OnDisable()
    {
        currentAction =
            TeamAction.None;

        if (movement != null)
        {
            movement.EnableMovement();
        }

        EnableFollowers();
    }

    private void OnDestroy()
    {
        FormationChanged =
            null;

        ActionStarted =
            null;

        ActionEnded =
            null;
    }

    private void OnValidate()
    {
        if (!Enum.IsDefined(
                typeof(TeamFormation),
                currentFormation))
        {
            currentFormation =
                TeamFormation.Speed;
        }
    }

    #endregion

    #region Initialization

    public bool Setup(
        Transform speed,
        Transform fly,
        Transform power)
    {
        SetCharacters(
            speed,
            fly,
            power);

        ResolveReferences();

        return
            InitializeController();
    }

    public void SetCharacters(
        Transform speed,
        Transform fly,
        Transform power)
    {
        speedCharacter =
            speed;

        flyCharacter =
            fly;

        powerCharacter =
            power;

        ResolveMovement();
        RefreshFormationSystems();
    }

    public void SetFollowers(
        FollowerNavigation left,
        FollowerNavigation right)
    {
        leftFollower =
            left;

        rightFollower =
            right;

        leftFollower?.SetTeamController(this);
        rightFollower?.SetTeamController(this);
    }

    private bool InitializeController()
    {
        if (initialized)
        {
            return true;
        }

        ResolveReferences();
        ResolveCharacters();

        if (movement == null ||
            speedCharacter == null ||
            flyCharacter == null ||
            powerCharacter == null)
        {
            return false;
        }

        currentAction =
            TeamAction.None;

        actionLocked =
            false;

        initialized =
            true;

        SynchronizeFormation(
            forceRefresh: true);

        return true;
    }

    #endregion

    #region References

    private void ResolveReferences()
    {
        characterSwitch ??=
            GetComponent<CharacterSwitch>();

        characterSwitch ??=
            GetComponentInParent<CharacterSwitch>();

        leftFollower ??=
            FindFollower(
                0);

        rightFollower ??=
            FindFollower(
                1);

        leftFollower.SetTeamController(this);
        rightFollower.SetTeamController(this);

        cameraController ??=
            FindAnyObjectByType<
                CameraController>();

        railGrinding ??=
            GetComponent<RailGrinding>();

        railGrinding ??=
            GetComponentInParent<RailGrinding>();

        hud ??=
            FindAnyObjectByType<HUD>();

        ResolveCharacters();
        ResolveMovement();
    }

    private void ResolveCharacters()
    {
        if (characterSwitch == null)
        {
            return;
        }

        speedCharacter ??=
            characterSwitch.speedCharacter;

        flyCharacter ??=
            characterSwitch.flyingCharacter;

        powerCharacter ??=
            characterSwitch.powerCharacter;
    }

    private void ResolveMovement()
    {
        movement ??=
            GetComponent<
                UltimatePlayerMovement>();

        movement ??=
            GetComponentInParent<
                UltimatePlayerMovement>();

        if (movement != null)
        {
            return;
        }

        Transform leader =
            GetFormationLeader();

        if (leader == null)
        {
            return;
        }

        movement =
            leader.GetComponentInParent<
                UltimatePlayerMovement>();

        movement ??=
            leader.GetComponentInChildren<
                UltimatePlayerMovement>(
                    includeInactive: true);
    }

    private FollowerNavigation FindFollower(
        int index)
    {
        FollowerNavigation[] followers =
            GetComponentsInChildren<
                FollowerNavigation>(
                    includeInactive: true);

        if (followers == null ||
            index < 0 ||
            index >= followers.Length)
        {
            return null;
        }

        return followers[index];
    }

    #endregion

    #region Formation

    public bool SetFormation(
        TeamFormation formation)
    {
        if (!initialized ||
            actionLocked ||
            IsPerformingAction ||
            !Enum.IsDefined(
                typeof(TeamFormation),
                formation))
        {
            return false;
        }

        if (currentFormation ==
            formation)
        {
            return true;
        }

        currentFormation =
            formation;

        ApplyFormationToCharacterSwitch();

        RefreshFormationSystems();

        FormationChanged?.Invoke(
            currentFormation);

        return true;
    }

    private void SynchronizeFormation(
        bool forceRefresh = false)
    {
        if (characterSwitch == null)
        {
            return;
        }

        TeamFormation expected =
            characterSwitch.CurrentLeaderType switch
            {
                CHARACTERTYPES.Fly =>
                    TeamFormation.Fly,

                CHARACTERTYPES.Power =>
                    TeamFormation.Power,

                _ =>
                    TeamFormation.Speed
            };

        if (!forceRefresh &&
            expected ==
                currentFormation)
        {
            return;
        }

        currentFormation =
            expected;

        ResolveCharacters();
        ResolveMovement();

        RefreshFormationSystems();

        FormationChanged?.Invoke(
            currentFormation);
    }

    private void ApplyFormationToCharacterSwitch()
    {
        if (characterSwitch == null)
        {
            return;
        }

        CHARACTERTYPES type =
            currentFormation switch
            {
                TeamFormation.Fly =>
                    CHARACTERTYPES.Fly,

                TeamFormation.Power =>
                    CHARACTERTYPES.Power,

                _ =>
                    CHARACTERTYPES.Speed
            };

        if (characterSwitch.CurrentLeaderType !=
            type)
        {
            characterSwitch.SetCharacter(
                type);
        }

        ResolveCharacters();
    }

    public Transform GetFormationLeader()
    {
        return currentFormation switch
        {
            TeamFormation.Fly =>
                flyCharacter,

            TeamFormation.Power =>
                powerCharacter,

            _ =>
                speedCharacter
        };
    }

    private void RefreshFormationSystems()
    {
        RefreshRailGrinding();
        RefreshCamera();
        RefreshHUD();
    }

    private void RefreshRailGrinding()
    {
        if (railGrinding == null)
        {
            return;
        }

        RailGrinding.TeamType type =
            currentFormation switch
            {
                TeamFormation.Fly =>
                    RailGrinding.TeamType.Fly,

                TeamFormation.Power =>
                    RailGrinding.TeamType.Power,

                _ =>
                    RailGrinding.TeamType.Speed
            };

        railGrinding.SetTeamType(
            type);
    }

    private void RefreshCamera()
    {
        if (cameraController == null)
        {
            return;
        }

        Transform leader =
            GetFormationLeader();

        if (leader != null)
        {
            cameraController.SetTarget(
                leader);
        }
    }

    private void RefreshHUD()
    {
        if (hud == null)
        {
            return;
        }

        CHARACTERTYPES type =
            currentFormation switch
            {
                TeamFormation.Fly =>
                    CHARACTERTYPES.Fly,

                TeamFormation.Power =>
                    CHARACTERTYPES.Power,

                _ =>
                    CHARACTERTYPES.Speed
            };

        hud.SetCharacter(
            type);

        hud.UpdateHUD();
    }

    #endregion

    #region Actions

    public bool CanBeginAction(
        TeamAction action,
        TeamFormation requiredFormation,
        bool mustBeGrounded = false,
        bool mustBeAirborne = false)
    {
        if (!initialized ||
            actionLocked ||
            IsPerformingAction ||
            action ==
                TeamAction.None ||
            currentFormation !=
                requiredFormation)
        {
            return false;
        }

        if (movement == null)
        {
            return false;
        }

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
            movement.DisableMovement();
        }

        DisableFollowers();
       
        ActionStarted?.Invoke(
            currentAction);

        return true;
    }

    public void EndAction(
        bool restoreMovementControl = true)
    {
        if (!IsPerformingAction)
        {
            return;
        }

        TeamAction completed =
            currentAction;

        currentAction =
            TeamAction.None;

        if (restoreMovementControl &&
            movement != null)
        {
            movement.EnableMovement();
        }

        EnableFollowers();
        
        ActionEnded?.Invoke(
            completed);
    }

    public void CancelCurrentAction()
    {
        if (IsPerformingAction)
        {
            EndAction(
                restoreMovementControl: true);
        }
    }

    public void SetActionLock(
        bool locked)
    {
        actionLocked =
            locked;

        if (actionLocked)
        {
            CancelCurrentAction();
        }
    }

    #endregion

    #region Followers

    public void EnableFollowers()
    {
        leftFollower?.EnableAgent();
        rightFollower?.EnableAgent();
    }

    public void DisableFollowers()
    {
        leftFollower?.DisableAgent();
        rightFollower?.DisableAgent();
    }

    #endregion

    #region Input

    private bool CanReadFormationInput()
    {
        return
            readFormationInput &&
            !actionLocked &&
            !IsPerformingAction;
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
}