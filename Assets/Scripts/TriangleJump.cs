using System.Collections;
using UnityEngine;

public class TriangleJump : MonoBehaviour
{
    #region Surface

    [Header("Triangle Jump Surface")]
    [SerializeField]
    private bool triangleJumpSurface = true;

    [SerializeField, Min(0f)]
    private float stickDelay = 0.12f;

    [SerializeField, Min(0.1f)]
    private float maximumStickTime = 1.25f;

    #endregion

    #region Launch

    [Header("Wall-to-Wall Launch")]
    [SerializeField, Min(0f)]
    private float horizontalSpeed = 20f;

    [SerializeField, Min(0f)]
    private float forwardSpeed = 10f;

    [SerializeField, Min(0f)]
    private float verticalLift = 0.75f;

    [SerializeField, Min(0f)]
    private float directionalInfluence = 7f;

    #endregion

    #region Input

    [Header("Input")]
    [SerializeField]
    private string jumpButton = "Jump";

    [SerializeField]
    private string verticalAxis = "Vertical";

    #endregion

    #region Animation

    [Header("Animation")]
    [SerializeField]
    private Animator animator;

    [SerializeField]
    private string wallClingState = "Triangle Jump";

    [SerializeField]
    private string spinParameter = "Spin";

    [SerializeField]
    private bool playWallAnimation = true;

    #endregion

    #region Runtime

    private UltimatePlayerMovement movement;
    private TeamActionController actionController;
    private HomingAttack homingAttack;
    private Rigidbody body;
    private Transform speedCharacter;

    private Coroutine jumpRoutine;

    private Vector3 wallNormal;

    private bool attached;
    private bool ready;
    private bool launching;
    private bool initialized;

    #endregion

    #region Properties

    public bool IsAttached =>
        attached;

    public bool TriangleJumpReady =>
        ready;

    public bool IsLaunching =>
        launching;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        initialized = true;
    }

    private void OnTriggerEnter(
        Collider other)
    {
        if (!initialized ||
            !triangleJumpSurface ||
            attached ||
            other == null)
        {
            return;
        }

        TryAttach(other);
    }

    private void OnDisable()
    {
        CancelTriangleJump();
    }

    private void OnDestroy()
    {
        CancelTriangleJump();

        initialized = false;
    }

    #endregion

    #region Detection

    private void TryAttach(
        Collider other)
    {
        UltimatePlayerMovement foundMovement =
            other.GetComponentInParent<UltimatePlayerMovement>();

        if (foundMovement == null)
        {
            return;
        }

        TeamActionController foundActions =
            foundMovement.GetComponent<TeamActionController>();

        if (foundActions == null)
        {
            foundActions =
                foundMovement.GetComponentInParent<TeamActionController>();
        }

        if (foundActions == null)
        {
            return;
        }

        if (foundActions.CurrentFormation !=
            TeamActionController.TeamFormation.Speed)
        {
            return;
        }

        Transform expectedSpeedCharacter =
            foundActions.SpeedCharacter;

        if (expectedSpeedCharacter == null)
        {
            return;
        }

        if (!IsCharacterCollider(
            other.transform,
            expectedSpeedCharacter))
        {
            return;
        }

        HomingAttack foundHoming =
            other.GetComponentInParent<HomingAttack>();

        if (foundHoming == null)
        {
            return;
        }

        /*
         * Triangle Jump must be entered from
         * the Homing Attack action.
         */
        if (foundActions.CurrentAction !=
            TeamActionController.TeamAction.HomingAttack)
        {
            return;
        }

        Vector3 normal =
            CalculateWallNormal(
                other);

        if (!IsFinite(normal) ||
            normal.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        BeginTriangleJump(
            foundMovement,
            foundActions,
            foundHoming,
            expectedSpeedCharacter,
            normal.normalized);
    }

    private Vector3 CalculateWallNormal(
        Collider other)
    {
        Vector3 point =
            other.ClosestPoint(
                transform.position);

        Vector3 normal =
            other.transform.position -
            point;

        normal =
            Vector3.ProjectOnPlane(
                normal,
                Vector3.up);

        if (normal.sqrMagnitude <=
            Mathf.Epsilon)
        {
            normal =
                other.transform.position -
                transform.position;
        }

        if (normal.sqrMagnitude <=
            Mathf.Epsilon)
        {
            normal =
                -transform.forward;
        }

        return normal.normalized;
    }

    private static bool IsCharacterCollider(
        Transform candidate,
        Transform character)
    {
        if (candidate == null ||
            character == null)
        {
            return false;
        }

        return
            candidate == character ||
            candidate.IsChildOf(character);
    }

    #endregion

    #region Begin

    private void BeginTriangleJump(
        UltimatePlayerMovement foundMovement,
        TeamActionController foundActions,
        HomingAttack foundHoming,
        Transform foundSpeedCharacter,
        Vector3 contactNormal)
    {
        /*
         * End Homing Attack first.
         * TeamActionController only allows
         * one team action at a time.
         */
        if (foundActions.CurrentAction !=
    TeamActionController.TeamAction.HomingAttack)
        {
            return;
        }

        movement =
            foundMovement;

        actionController =
            foundActions;

        homingAttack =
            foundHoming;

        speedCharacter =
            foundSpeedCharacter;

        body =
            movement.GetComponent<Rigidbody>();

        if (body == null)
        {
            EndAction();
            ClearRuntime();
            return;
        }

        if (animator == null &&
            speedCharacter != null)
        {
            animator =
                speedCharacter.GetComponentInChildren<Animator>();
        }

        wallNormal =
            contactNormal;

        attached =
            true;

        ready =
            false;

        launching =
            false;

        EnterWallCling();

        jumpRoutine =
            StartCoroutine(
                WallRoutine());
    }

    #endregion

    #region Wall Cling

    private void EnterWallCling()
    {
        body.useGravity =
            false;

        body.linearVelocity =
            Vector3.zero;

        body.angularVelocity =
            Vector3.zero;

        SetSpin(false);

        FaceAwayFromWall();

        if (playWallAnimation &&
            animator != null &&
            !string.IsNullOrWhiteSpace(
                wallClingState))
        {
            animator.Play(
                wallClingState,
                0,
                0f);
        }
    }

    private void FaceAwayFromWall()
    {
        if (speedCharacter == null)
        {
            return;
        }

        Vector3 horizontalNormal =
            Vector3.ProjectOnPlane(
                wallNormal,
                Vector3.up);

        if (horizontalNormal.sqrMagnitude <=
            Mathf.Epsilon)
        {
            return;
        }

        Quaternion rotation =
            Quaternion.LookRotation(
                horizontalNormal.normalized,
                Vector3.up);

        if (IsFinite(rotation))
        {
            speedCharacter.rotation =
                rotation;
        }
    }

    private IEnumerator WallRoutine()
    {
        float delay = 0f;

        while (delay < stickDelay)
        {
            if (!CanRemainAttached())
            {
                Detach();
                yield break;
            }

            HoldToWall();

            delay +=
                Time.deltaTime;

            yield return null;
        }

        ready = true;

        float timer = 0f;

        while (timer < maximumStickTime)
        {
            if (!CanRemainAttached())
            {
                Detach();
                yield break;
            }

            HoldToWall();

            if (Input.GetButtonDown(
                jumpButton))
            {
                Launch();
                yield break;
            }

            timer +=
                Time.deltaTime;

            yield return null;
        }

        Detach();
    }

    private bool CanRemainAttached()
    {
        return
            attached &&
            movement != null &&
            body != null &&
            speedCharacter != null &&
            movement.gameObject.activeInHierarchy &&
            IsFinite(body.position) &&
            IsFinite(body.rotation);
    }

    private void HoldToWall()
    {
        body.linearVelocity =
            Vector3.zero;

        body.angularVelocity =
            Vector3.zero;

        body.useGravity =
            false;
    }

    #endregion

    #region Launch

    private void Launch()
    {
        if (!attached ||
            body == null)
        {
            return;
        }

        attached =
            false;

        ready =
            false;

        launching =
            true;

        Vector3 outward =
            Vector3.ProjectOnPlane(
                wallNormal,
                Vector3.up);

        if (outward.sqrMagnitude <=
            Mathf.Epsilon)
        {
            outward =
                -transform.forward;
        }

        outward.Normalize();

        Vector3 forward =
            speedCharacter != null
                ? Vector3.ProjectOnPlane(
                    speedCharacter.forward,
                    Vector3.up)
                : transform.forward;

        if (forward.sqrMagnitude >
            Mathf.Epsilon)
        {
            forward.Normalize();
        }

        /*
         * Sonic Heroes allows the player
         * to influence forward/backward travel
         * during the Triangle Jump.
         */
        float forwardInput =
            Input.GetAxisRaw(
                verticalAxis);

        Vector3 inputAdjustment =
            forward *
            forwardInput *
            directionalInfluence;

        Vector3 velocity =
            outward *
            horizontalSpeed +
            forward *
            forwardSpeed +
            inputAdjustment +
            Vector3.up *
            verticalLift;

        if (!IsFinite(velocity))
        {
            Detach();
            return;
        }

        body.useGravity =
            true;

        body.linearVelocity =
            velocity;

        body.angularVelocity =
            Vector3.zero;

        /*
         * The reference shows Sonic curled
         * into the ball during transfer.
         */
        SetSpin(true);

        EndAction();

        jumpRoutine =
            null;
    }

    #endregion

    #region Detach

    private void Detach()
    {
        ready =
            false;

        attached =
            false;

        launching =
            false;

        if (body != null)
        {
            body.useGravity =
                true;

            body.linearVelocity =
                Vector3.zero;

            body.angularVelocity =
                Vector3.zero;
        }

        SetSpin(false);

        EndAction();

        ClearRuntime();
    }

    public void CancelTriangleJump()
    {
        if (jumpRoutine != null)
        {
            StopCoroutine(
                jumpRoutine);

            jumpRoutine =
                null;
        }

        if (attached)
        {
            Detach();
            return;
        }

        ClearRuntime();
    }

    #endregion

    #region Animation

    private void SetSpin(
        bool value)
    {
        if (animator == null ||
            string.IsNullOrWhiteSpace(
                spinParameter))
        {
            return;
        }

        animator.SetBool(
            spinParameter,
            value);
    }

    #endregion

    #region Team Action

    private void EndAction()
    {
        if (actionController == null)
        {
            movement?.EnableMovement();
            return;
        }

        if (actionController.CurrentAction ==
            TeamActionController.TeamAction.TriangleJump)
        {
            actionController.EndAction(
                restoreMovementControl: true);
        }
    }

    #endregion

    #region Cleanup

    private void ClearRuntime()
    {
        jumpRoutine =
            null;

        movement =
            null;

        actionController =
            null;

        homingAttack =
            null;

        body =
            null;

        speedCharacter =
            null;

        wallNormal =
            Vector3.zero;
    }

    #endregion

    #region Safety

    private static bool IsFinite(
        Vector3 value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }

    private static bool IsFinite(
        Quaternion value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z) &&
            float.IsFinite(value.w);
    }

    #endregion
}