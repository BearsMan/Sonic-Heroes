using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TriangleJump : MonoBehaviour
{
    #region Animator Hashes

    private static readonly int TriangleJumpHash =
        Animator.StringToHash("Triangle Jump");

    private static readonly int JumpHash =
        Animator.StringToHash("Jump");

    #endregion

    #region Inspector

    [Header("Triangle Jump")]
    [SerializeField, Min(0f)] private float launchForce = 18f;
    [SerializeField, Range(0f, 90f)] private float launchAngle = 60f;
    [SerializeField, Min(0f)] private float stickDuration = 0.25f;
    [SerializeField, Min(0.1f)] private float inputTimeout = 3f;
    [SerializeField] private CharacterSwitch characterSwitch;
    [SerializeField] private TeamActionController actionController;

    [Header("Input")]
    [SerializeField] private string jumpButton = "Jump";

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private GameObject stuckCharacter;
    private UltimatePlayerMovement movement;
    private Rigidbody stuckRigidbody;
    private Animator stuckAnimator;
    private Coroutine stickRoutine;

    private Vector3 wallNormal;

    private bool triangleJumpReady;
    private bool isJumping;
    private bool isInitialized;
    private bool isShuttingDown;

    #endregion

    #region Public API

    public bool TriangleJumpReady => triangleJumpReady;
    public bool IsJumping => isJumping;
    public bool IsInitialized => isInitialized;
    public GameObject StuckCharacter => stuckCharacter;

    public bool Stick(
        GameObject speedCharacter,
        Vector3 contactNormal)
    {
        if (!CanUseTriangleJump() ||
            !isInitialized ||
            speedCharacter == null ||
            stuckCharacter != null)
        {
            return false;
        }

        if (!ResolveCharacterReferences(speedCharacter))
            return false;

        CancelStickRoutine();

        stuckCharacter = speedCharacter;
        wallNormal =
            contactNormal.sqrMagnitude > 0.001f
                ? contactNormal.normalized
                : -transform.forward;

        stickRoutine =
            StartCoroutine(StickRoutine());

        return true;
    }

    public void CancelTriangleJump()
    {
        if (stuckCharacter != null)
            DetachCharacter();
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveSceneReferences();
        isInitialized = true;
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
        launchForce = Mathf.Max(0f, launchForce);
        launchAngle = Mathf.Clamp(launchAngle, 0f, 90f);
        stickDuration = Mathf.Max(0f, stickDuration);
        inputTimeout = Mathf.Max(0.1f, inputTimeout);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isInitialized ||
            stuckCharacter != null ||
            other == null)
        {
            return;
        }

        HomingAttack homingAttack =
            other.GetComponent<HomingAttack>();

        homingAttack ??=
            other.GetComponentInParent<HomingAttack>();

        if (homingAttack == null ||
            !homingAttack.HomingAttackUsed)
        {
            return;
        }

        Vector3 contactNormal =
            (other.transform.position -
             transform.position)
            .normalized;

        Stick(other.gameObject, contactNormal);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null ||
            stuckCharacter != other.gameObject ||
            !triangleJumpReady)
        {
            return;
        }

        DetachCharacter();
    }

    #endregion

    #region Triangle Jump State

    private bool CanUseTriangleJump()
    {
        return
            TeamSetup.Instance != null &&
            TeamSetup.Instance.PlayableTeam == PlayableTeam.TeamSonic &&
            characterSwitch != null &&
            characterSwitch.CurrentLeaderType == CHARACTERTYPES.Speed &&
            actionController != null &&
            actionController.CurrentFormation ==
                TeamActionController.TeamFormation.Speed;
    }

    private IEnumerator StickRoutine()
    {
        FreezeCharacter();
        PlayAnimation(TriangleJumpHash);

        triangleJumpReady = false;

        if (stickDuration > 0f)
            yield return new WaitForSeconds(stickDuration);

        triangleJumpReady = true;

        float elapsed = 0f;

        while (elapsed < inputTimeout)
        {
            if (Input.GetButtonDown(jumpButton))
            {
                LaunchCharacter();
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        DetachCharacter();
    }

    private void ResolveSceneReferences()
    {
        characterSwitch ??= GetComponentInParent<CharacterSwitch>();

        actionController ??=
            GetComponentInParent<TeamActionController>();
    }

    private void FreezeCharacter()
    {
        movement?.DisableMovement();

        if (stuckRigidbody == null)
            return;

        stuckRigidbody.useGravity = false;
        stuckRigidbody.linearVelocity = Vector3.zero;
        stuckRigidbody.angularVelocity = Vector3.zero;
    }

    private void LaunchCharacter()
    {
        if (stuckCharacter == null ||
            stuckRigidbody == null)
        {
            DetachCharacter();
            return;
        }

        triangleJumpReady = false;
        isJumping = true;

        Vector3 outward =
            Vector3.ProjectOnPlane(
                wallNormal,
                Vector3.up)
            .normalized;

        if (outward.sqrMagnitude <= 0.001f)
            outward = -transform.forward;

        float radians =
            launchAngle * Mathf.Deg2Rad;

        Vector3 launchDirection =
            (outward * Mathf.Cos(radians) +
             Vector3.up * Mathf.Sin(radians))
            .normalized;

        stuckRigidbody.useGravity = true;
        stuckRigidbody.linearVelocity = Vector3.zero;
        stuckRigidbody.angularVelocity = Vector3.zero;

        stuckRigidbody.AddForce(
            launchDirection * launchForce,
            ForceMode.VelocityChange);

        PlayAnimation(JumpHash);
        movement?.EnableMovement();

        LogStateChange("Triangle Jump launched.");

        ClearCharacterReferences();
        StartCoroutine(ResetJumpFlag());
    }

    private void DetachCharacter()
    {
        CancelStickRoutine();

        triangleJumpReady = false;
        isJumping = false;

        if (stuckRigidbody != null)
            stuckRigidbody.useGravity = true;

        movement?.EnableMovement();

        LogStateChange("Triangle Jump detached.");
        ClearCharacterReferences();
    }

    private IEnumerator ResetJumpFlag()
    {
        yield return new WaitForFixedUpdate();
        isJumping = false;
    }

    #endregion

    #region Reference Resolution

    private bool ResolveCharacterReferences(GameObject character)
    {
        movement =
            character.GetComponent<UltimatePlayerMovement>();

        movement ??=
            character.GetComponentInParent<UltimatePlayerMovement>();

        stuckRigidbody =
            character.GetComponent<Rigidbody>();

        stuckRigidbody ??=
            character.GetComponentInParent<Rigidbody>();

        stuckAnimator =
            character.GetComponent<Animator>();

        stuckAnimator ??=
            character.GetComponentInChildren<Animator>(includeInactive: true);

        bool valid = true;

        valid &=
            ValidateReference(
                movement,
                nameof(UltimatePlayerMovement));

        valid &=
            ValidateReference(
                stuckRigidbody,
                nameof(Rigidbody));

        valid &=
            ValidateReference(
                characterSwitch,
                nameof(CharacterSwitch));

        valid &=
            ValidateReference(
                actionController,
                nameof(TeamActionController));

        if (stuckAnimator == null)
        {
            Debug.LogWarning(
                "TriangleJump could not find an Animator.",
                this);
        }

        return valid;
    }

    #endregion

    #region Animation

    private void PlayAnimation(int animationHash)
    {
        if (stuckAnimator != null)
            stuckAnimator.Play(animationHash);
    }

    #endregion

    #region Validation

    private bool ValidateReference(
        Object reference,
        string displayName)
    {
        if (reference != null)
            return true;

        Debug.LogError(
            $"TriangleJump requires {displayName} on the incoming character.",
            this);

        return false;
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        if (stuckCharacter != null)
            DetachCharacter();
        else
            CancelStickRoutine();
    }

    private void CleanupDestroyedState()
    {
        CleanupRuntimeState();

        isInitialized = false;

        stuckCharacter = null;
        movement = null;
        stuckRigidbody = null;
        stuckAnimator = null;
        characterSwitch = null;
        actionController = null;
    }

    private void CancelStickRoutine()
    {
        if (stickRoutine == null)
            return;

        StopCoroutine(stickRoutine);
        stickRoutine = null;
    }

    private void ClearCharacterReferences()
    {
        stuckCharacter = null;
        movement = null;
        stuckRigidbody = null;
        stuckAnimator = null;
        stickRoutine = null;
        wallNormal = Vector3.zero;
    }

    #endregion

    #region Debug

    private void LogStateChange(string message)
    {
        if (!logStateChanges)
            return;

        Debug.Log(message, this);
    }

    #endregion
}
