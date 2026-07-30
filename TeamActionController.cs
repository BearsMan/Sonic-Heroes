using System;
using UnityEngine;
[RequireComponent (typeof(Rigidbody))]
[RequireComponent(typeof(UltimatePlayerMovement))]
[RequireComponent (typeof(FollowerNavigation))]
public class TeamActionController : MonoBehaviour
{
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

    [Header("Current Team State")]
    [SerializeField] private TeamFormation currentFormation = TeamFormation.Speed;
    [SerializeField] private TeamAction currentAction = TeamAction.None;
    [SerializeField] private FollowerNavigation leftFollower;
    [SerializeField] private FollowerNavigation rightFollower;

    [Header("Team References")]
    [SerializeField] private GameObject speedCharacter;
    [SerializeField] private GameObject flyCharacter;
    [SerializeField] private GameObject powerCharacter;
    [SerializeField] private UltimatePlayerMovement movement;
    [SerializeField] private RailGrinding railGrinding;

    [Header("Optional Formation Input")]
    [SerializeField] private bool readFormationInput = true;
    [SerializeField] private KeyCode speedFormationKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode flyFormationKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode powerFormationKey = KeyCode.Alpha3;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    private bool actionLocked;

    public TeamFormation CurrentFormation => currentFormation;
    public TeamAction CurrentAction => currentAction;
    public bool IsPerformingAction => currentAction != TeamAction.None;
    public bool ActionLocked => actionLocked;
    public UltimatePlayerMovement Movement => movement;

    public GameObject SpeedCharacter => speedCharacter;
    public GameObject FlyCharacter => flyCharacter;
    public GameObject PowerCharacter => powerCharacter;

    public event Action<TeamFormation> FormationChanged;
    public event Action<TeamAction> ActionStarted;
    public event Action<TeamAction> ActionEnded;

    private void Awake()
    {
        if (movement == null)
        {
            movement = GetComponent<UltimatePlayerMovement>();
        }

        if (movement == null)
        {
            movement = GetComponentInParent<UltimatePlayerMovement>();
        }

        if (railGrinding == null)
        {
            railGrinding = GetComponent<RailGrinding>();
        }

        if (railGrinding == null)
        {
            railGrinding = GetComponentInParent<RailGrinding>();
        }

        ApplyFormationToSystems();
    }

    private void Start()
    {
        ApplyFormationToSystems();
        FormationChanged?.Invoke(currentFormation);
    }

    private void Update()
    {
        if (!readFormationInput || IsPerformingAction || actionLocked)
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

    public bool SetFormation(TeamFormation newFormation)
    {
        if (actionLocked || IsPerformingAction)
        {
            return false;
        }

        if (currentFormation == newFormation)
        {
            ApplyFormationToSystems();
            return true;
        }

        currentFormation = newFormation;

        ApplyFormationToSystems();

        if (logStateChanges)
        {
            Debug.Log(
                "Formation changed to " +
                currentFormation);
        }

        FormationChanged?.Invoke(currentFormation);

        return true;
    }

    private void ApplyFormationToSystems()
    {
        if (railGrinding == null)
        {
            return;
        }

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

        railGrinding.SetTeamType(railTeamType);
    }

    public bool CanBeginAction(TeamAction action, TeamFormation requiredFormation, bool mustBeGrounded = false, bool mustBeAirborne = false)
    {
        if (action == TeamAction.None)
        {
            return false;
        }

        if (actionLocked)
        {
            return false;
        }

        if (IsPerformingAction)
        {
            return false;
        }

        if (currentFormation != requiredFormation)
        {
            return false;
        }

        if (movement != null)
        {
            if (mustBeGrounded && !movement.IsGrounded)
            {
                return false;
            }

            if (mustBeAirborne && movement.IsGrounded)
            {
                return false;
            }
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
        if (!CanBeginAction(action, requiredFormation, mustBeGrounded, mustBeAirborne))
        {
            return false;
        }

        currentAction = action;

        if (surrenderMovementControl && movement != null)
        {
            movement.DisableMovement();
        }

        if (logStateChanges)
        {
            Debug.Log("Started Action: " + currentAction);
        }

        ActionStarted?.Invoke(currentAction);

        return true;
    }

    public void EndAction(bool restoreMovementControl = true)
    {
        if (restoreMovementControl && movement != null)
        {
            movement.EnableMovement();
        }

        TeamAction endedAction = currentAction;

        currentAction = TeamAction.None;

        if (logStateChanges)
        {
            Debug.Log("Finished Action: " + endedAction);
        }

        ActionEnded?.Invoke(endedAction);
    }

    public void CancelCurrentAction()
    {
        EndAction(true);
    }

    public void SetActionLock(bool locked)
    {
        actionLocked = locked;

        if (locked && IsPerformingAction)
        {
            CancelCurrentAction();
        }
    }

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
    public GameObject GetFormationLeader()
    {
        switch (currentFormation)
        {
            case TeamFormation.Speed:
                return speedCharacter;

            case TeamFormation.Fly:
                return flyCharacter;

            case TeamFormation.Power:
                return powerCharacter;

            default:
                return speedCharacter;
        }
    }

    private void OnDisable()
    {
        if (IsPerformingAction)
        {
            currentAction = TeamAction.None;

            if (movement != null)
            {
                movement.EnableMovement();
            }
        }
    }
}