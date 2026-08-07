using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class TriangleJump : MonoBehaviour
{
    #region Types

    public enum TriangleJumpState
    {
        Uninitialized,
        Ready,
        Sticking,
        WaitingForInput,
        Launching,
        Cooldown,
        Disabled
    }

    #endregion

    #region Constants

    private const string DefaultJumpButton =
        "Jump";

    private const string DefaultStickAnimation =
        "Triangle Jump";

    private const string DefaultJumpAnimation =
        "Jump";

    #endregion

    #region Inspector

    [Header("Triangle Jump")]
    [SerializeField, Min(0f)] private float launchSpeed = 18f;
    [SerializeField, Range(0f, 90f)] private float launchAngle = 60f;
    [SerializeField, Min(0f)] private float stickDuration = 0.25f;
    [SerializeField, Min(0.1f)] private float inputTimeout = 3f;
    [SerializeField, Min(0f)] private float launchCooldown = 0.15f;

    [Header("Wall Placement")]
    [SerializeField, Min(0f)] private float wallOffset = 0.05f;
    [SerializeField] private bool faceAwayFromWall = true;
    [SerializeField] private bool clearVelocityWhenSticking = true;
    [SerializeField] private bool clearVelocityWhenLaunching = true;

    [Header("Requirements")]
    [SerializeField] private bool requireTeamSonic = true;
    [SerializeField] private bool requireSpeedLeader = true;
    [SerializeField] private bool requireSpeedFormation = true;
    [SerializeField] private bool requireHomingAttackActivation = true;

    [Header("Scene References")]
    [SerializeField] private CharacterSwitch characterSwitch;
    [SerializeField] private TeamActionController actionController;
    [SerializeField] private Collider triggerCollider;

    [Header("Input")]
    [SerializeField]
    private string jumpButton =
        DefaultJumpButton;

    [Header("Animation")]
    [SerializeField]
    private string stickAnimation =
        DefaultStickAnimation;
    [SerializeField]
    private string jumpAnimation =
        DefaultJumpAnimation;

    [Header("Runtime Safety")]
    [SerializeField] private bool enableRuntimeSafety = true;
    [SerializeField, Min(0.1f)] private float safetyCheckInterval = 0.5f;
    [SerializeField, Min(1f)] private float maximumLaunchSpeed = 100f;
    [SerializeField, Min(0.01f)] private float minimumValidScale = 0.01f;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;
    [SerializeField]
    private TriangleJumpState currentState =
        TriangleJumpState.Uninitialized;

    #endregion

    #region Runtime State

    private GameObject stuckCharacter;
    private UltimatePlayerMovement movement;
    private Rigidbody stuckRigidbody;
    private Animator stuckAnimator;
    private HomingAttack homingAttack;

    private Coroutine triangleJumpRoutine;
    private Coroutine launchResetRoutine;

    private Vector3 wallNormal =
        Vector3.forward;

    private RigidbodyConstraints originalConstraints;
    private bool originalUseGravity;
    private bool originalIsKinematic;
    private bool originalMovementEnabled;

    private float safetyCheckTimer;

    private int stickAnimationHash;
    private int jumpAnimationHash;

    private bool triangleJumpReady;
    private bool isJumping;
    private bool rigidbodyStateCached;
    private bool initialized;
    private bool shuttingDown;
    private bool applicationQuitting;

    #endregion

    #region Events

    public event Action<TriangleJump, GameObject> CharacterStuck;
    public event Action<TriangleJump, GameObject> CharacterLaunched;
    public event Action<TriangleJump, GameObject> CharacterDetached;

    #endregion

    #region Public API

    public bool TriangleJumpReady =>
        triangleJumpReady;

    public bool IsJumping =>
        isJumping;

    public bool IsInitialized =>
        initialized;

    public bool HasStuckCharacter =>
        stuckCharacter != null;

    public GameObject StuckCharacter =>
        stuckCharacter;

    public TriangleJumpState CurrentState =>
        currentState;

    public bool Stick(
        GameObject speedCharacter,
        Vector3 contactNormal)
    {
        if (!CanBeginTriangleJump(
                speedCharacter))
        {
            return false;
        }

        if (!ResolveCharacterReferences(
                speedCharacter))
        {
            ClearCharacterReferences();
            return false;
        }

        Vector3 resolvedNormal =
            ResolveWallNormal(
                contactNormal,
                speedCharacter.transform.position);

        if (!IsFiniteVector(
                resolvedNormal))
        {
            ClearCharacterReferences();
            return false;
        }

        CancelTriangleJumpRoutine();
        CancelLaunchResetRoutine();

        stuckCharacter =
            speedCharacter;

        wallNormal =
            resolvedNormal;

        CacheCharacterState();
        PositionCharacterAgainstWall();

        triangleJumpRoutine =
            StartCoroutine(
                TriangleJumpRoutine());

        return true;
    }

    public bool LaunchNow()
    {
        if (!triangleJumpReady ||
            stuckCharacter == null ||
            stuckRigidbody == null)
        {
            return false;
        }

        LaunchCharacter();

        return true;
    }

    public void CancelTriangleJump()
    {
        if (shuttingDown ||
            applicationQuitting)
        {
            return;
        }

        DetachCharacter(
            notifyListeners: true);
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (shuttingDown ||
            applicationQuitting)
        {
            return;
        }

        if (!initialized)
        {
            Initialize();
        }

        if (initialized &&
            stuckCharacter == null)
        {
            currentState =
                TriangleJumpState.Ready;
        }
    }

    private void Update()
    {
        if (!initialized ||
            shuttingDown ||
            applicationQuitting)
        {
            return;
        }

        if (!enableRuntimeSafety)
            return;

        safetyCheckTimer -=
            Time.deltaTime;

        if (safetyCheckTimer > 0f)
            return;

        safetyCheckTimer =
            safetyCheckInterval;

        RunRuntimeSafetyChecks();
    }

    private void OnDisable()
    {
        if (shuttingDown ||
            applicationQuitting)
        {
            return;
        }

        CleanupRuntimeState();

        currentState =
            TriangleJumpState.Disabled;
    }

    private void OnApplicationQuit()
    {
        applicationQuitting = true;
    }

    private void OnDestroy()
    {
        shuttingDown = true;
        initialized = false;

        CleanupRuntimeState();

        CharacterStuck = null;
        CharacterLaunched = null;
        CharacterDetached = null;

        characterSwitch = null;
        actionController = null;
        triggerCollider = null;

        currentState =
            TriangleJumpState.Disabled;
    }

    private void OnValidate()
    {
        launchSpeed =
            Mathf.Clamp(
                launchSpeed,
                0f,
                maximumLaunchSpeed);

        launchAngle =
            Mathf.Clamp(
                launchAngle,
                0f,
                90f);

        stickDuration =
            Mathf.Max(
                0f,
                stickDuration);

        inputTimeout =
            Mathf.Max(
                0.1f,
                inputTimeout);

        launchCooldown =
            Mathf.Max(
                0f,
                launchCooldown);

        wallOffset =
            Mathf.Max(
                0f,
                wallOffset);

        safetyCheckInterval =
            Mathf.Max(
                0.1f,
                safetyCheckInterval);

        maximumLaunchSpeed =
            Mathf.Max(
                1f,
                maximumLaunchSpeed);

        minimumValidScale =
            Mathf.Max(
                0.01f,
                minimumValidScale);

        CacheAnimationHashes();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ResolveSceneReferences();
            ConfigureTrigger();
        }
#endif
    }

    private void OnTriggerEnter(
        Collider other)
    {
        if (!initialized ||
            shuttingDown ||
            applicationQuitting ||
            stuckCharacter != null ||
            other == null)
        {
            return;
        }

        GameObject character =
            ResolveCharacterObject(
                other);

        if (character == null)
            return;

        if (requireHomingAttackActivation &&
            !HasUsedHomingAttack(
                character))
        {
            return;
        }

        Vector3 contactNormal =
            character.transform.position -
            transform.position;

        Stick(
            character,
            contactNormal);
    }

    private void OnTriggerExit(
        Collider other)
    {
        if (other == null ||
            stuckCharacter == null ||
            !triangleJumpReady)
        {
            return;
        }

        GameObject exitingCharacter =
            ResolveCharacterObject(
                other);

        if (exitingCharacter !=
            stuckCharacter)
        {
            return;
        }

        DetachCharacter(
            notifyListeners: true);
    }

    #endregion

    #region Initialization

    private bool Initialize()
    {
        if (initialized)
            return true;

        ResolveSceneReferences();
        ConfigureTrigger();
        CacheAnimationHashes();

        safetyCheckTimer =
            safetyCheckInterval;

        if (!ValidateConfiguration())
        {
            initialized = false;

            currentState =
                TriangleJumpState.Uninitialized;

            enabled = false;

            return false;
        }

        initialized = true;

        currentState =
            TriangleJumpState.Ready;

        return true;
    }

    private void ResolveSceneReferences()
    {
        triggerCollider ??=
            GetComponent<Collider>();

        characterSwitch ??=
            GetComponent<CharacterSwitch>();

        characterSwitch ??=
            GetComponentInParent<CharacterSwitch>();

        characterSwitch ??=
            FindAnyObjectByType<CharacterSwitch>();

        actionController ??=
            GetComponent<TeamActionController>();

        actionController ??=
            GetComponentInParent<TeamActionController>();

        actionController ??=
            FindAnyObjectByType<TeamActionController>();
    }

    private void ConfigureTrigger()
    {
        if (triggerCollider == null)
            return;

        triggerCollider.isTrigger = true;
    }

    private void CacheAnimationHashes()
    {
        stickAnimationHash =
            string.IsNullOrWhiteSpace(
                stickAnimation)
                ? 0
                : Animator.StringToHash(
                    stickAnimation);

        jumpAnimationHash =
            string.IsNullOrWhiteSpace(
                jumpAnimation)
                ? 0
                : Animator.StringToHash(
                    jumpAnimation);
    }

    #endregion

    #region Requirements

    private bool CanBeginTriangleJump(
        GameObject character)
    {
        if (!initialized ||
            shuttingDown ||
            applicationQuitting ||
            character == null ||
            stuckCharacter != null ||
            isJumping ||
            !isActiveAndEnabled)
        {
            return false;
        }

        if (!ValidateTeamRequirements())
            return false;

        return
            ValidateTransform(
                character.transform);
    }

    private bool ValidateTeamRequirements()
    {
        if (requireTeamSonic)
        {
            if (TeamSetup.Instance == null ||
                TeamSetup.Instance.PlayableTeam !=
                    PlayableTeam.TeamSonic)
            {
                return false;
            }
        }

        if (requireSpeedLeader)
        {
            if (characterSwitch == null ||
                characterSwitch.CurrentLeaderType !=
                    CHARACTERTYPES.Speed)
            {
                return false;
            }
        }

        if (requireSpeedFormation)
        {
            if (actionController == null ||
                actionController.CurrentFormation !=
                    TeamActionController.TeamFormation.Speed)
            {
                return false;
            }
        }

        return true;
    }

    #endregion

    #region Triangle Jump State

    private IEnumerator TriangleJumpRoutine()
    {
        currentState =
            TriangleJumpState.Sticking;

        triangleJumpReady = false;
        isJumping = false;

        FreezeCharacter();
        PlayAnimation(
            stickAnimationHash);

        CharacterStuck?.Invoke(
            this,
            stuckCharacter);

        LogStateChange(
            $"'{stuckCharacter.name}' attached to Triangle Jump.");

        if (stickDuration > 0f)
        {
            yield return new WaitForSeconds(
                stickDuration);
        }

        if (!ValidateActiveCharacter())
        {
            DetachCharacter(
                notifyListeners: true);

            yield break;
        }

        triangleJumpReady = true;

        currentState =
            TriangleJumpState.WaitingForInput;

        float elapsed = 0f;

        while (elapsed <
               inputTimeout)
        {
            if (!ValidateActiveCharacter())
            {
                DetachCharacter(
                    notifyListeners: true);

                yield break;
            }

            if (ReadJumpInput())
            {
                LaunchCharacter();
                yield break;
            }

            elapsed +=
                Time.deltaTime;

            yield return null;
        }

        DetachCharacter(
            notifyListeners: true);
    }

    private bool ReadJumpInput()
    {
        if (string.IsNullOrWhiteSpace(
                jumpButton))
        {
            return false;
        }

        return Input.GetButtonDown(
            jumpButton);
    }

    private void FreezeCharacter()
    {
        if (!ValidateActiveCharacter())
            return;

        movement.DisableMovement();

        if (clearVelocityWhenSticking)
        {
            StopRigidbodyMotion();
        }

        stuckRigidbody.useGravity = false;
        stuckRigidbody.isKinematic = true;

        stuckRigidbody.constraints =
            originalConstraints |
            RigidbodyConstraints.FreezePosition;
    }

    private void PositionCharacterAgainstWall()
    {
        if (stuckRigidbody == null ||
            stuckCharacter == null)
        {
            return;
        }

        Vector3 position =
            stuckRigidbody.position +
            wallNormal *
            wallOffset;

        if (IsFiniteVector(
                position))
        {
            stuckRigidbody.position =
                position;
        }

        if (!faceAwayFromWall)
            return;

        Vector3 forward =
            Vector3.ProjectOnPlane(
                wallNormal,
                Vector3.up);

        if (forward.sqrMagnitude <=
            0.001f)
        {
            return;
        }

        stuckRigidbody.rotation =
            Quaternion.LookRotation(
                forward.normalized,
                Vector3.up);
    }

    private void LaunchCharacter()
    {
        if (!ValidateActiveCharacter())
        {
            DetachCharacter(
                notifyListeners: true);

            return;
        }

        triangleJumpReady = false;
        isJumping = true;

        currentState =
            TriangleJumpState.Launching;

        Vector3 launchDirection =
            CalculateLaunchDirection();

        if (!IsFiniteVector(
                launchDirection) ||
            launchDirection.sqrMagnitude <=
                0.001f)
        {
            DetachCharacter(
                notifyListeners: true);

            return;
        }

        RestoreRigidbodyState();

        stuckRigidbody.isKinematic = false;
        stuckRigidbody.useGravity = true;

        if (clearVelocityWhenLaunching)
        {
            StopRigidbodyMotion();
        }

        Vector3 launchVelocity =
            launchDirection *
            Mathf.Min(
                launchSpeed,
                maximumLaunchSpeed);

        stuckRigidbody.linearVelocity =
            launchVelocity;

        stuckRigidbody.WakeUp();

        movement.EnableMovement();

        PlayAnimation(
            jumpAnimationHash);

        GameObject launchedCharacter =
            stuckCharacter;

        CharacterLaunched?.Invoke(
            this,
            launchedCharacter);

        LogStateChange(
            $"'{launchedCharacter.name}' launched from Triangle Jump.");

        ClearCharacterReferences();

        launchResetRoutine =
            StartCoroutine(
                ResetLaunchStateRoutine());
    }

    private Vector3 CalculateLaunchDirection()
    {
        Vector3 outward =
            Vector3.ProjectOnPlane(
                wallNormal,
                Vector3.up);

        if (outward.sqrMagnitude <=
            0.001f)
        {
            outward =
                -transform.forward;
        }

        outward.Normalize();

        float radians =
            launchAngle *
            Mathf.Deg2Rad;

        Vector3 direction =
            outward *
            Mathf.Cos(radians) +
            Vector3.up *
            Mathf.Sin(radians);

        return direction.normalized;
    }

    private IEnumerator ResetLaunchStateRoutine()
    {
        currentState =
            TriangleJumpState.Cooldown;

        if (launchCooldown > 0f)
        {
            yield return new WaitForSeconds(
                launchCooldown);
        }
        else
        {
            yield return new WaitForFixedUpdate();
        }

        isJumping = false;

        currentState =
            initialized
                ? TriangleJumpState.Ready
                : TriangleJumpState.Disabled;

        launchResetRoutine = null;
    }

    private void DetachCharacter(
        bool notifyListeners)
    {
        CancelTriangleJumpRoutine();

        triangleJumpReady = false;
        isJumping = false;

        GameObject detachedCharacter =
            stuckCharacter;

        RestoreCharacterState();

        if (notifyListeners &&
            detachedCharacter != null)
        {
            CharacterDetached?.Invoke(
                this,
                detachedCharacter);
        }

        LogStateChange(
            "Triangle Jump character detached.");

        ClearCharacterReferences();

        currentState =
            initialized
                ? TriangleJumpState.Ready
                : TriangleJumpState.Disabled;
    }

    #endregion

    #region Character Resolution

    private GameObject ResolveCharacterObject(
        Collider other)
    {
        if (other == null)
            return null;

        UltimatePlayerMovement resolvedMovement =
            other.GetComponent<UltimatePlayerMovement>();

        resolvedMovement ??=
            other.GetComponentInParent<UltimatePlayerMovement>();

        resolvedMovement ??=
            other.GetComponentInChildren<UltimatePlayerMovement>(
                includeInactive: true);

        if (resolvedMovement != null)
        {
            return resolvedMovement.gameObject;
        }

        if (other.attachedRigidbody != null)
        {
            return other
                .attachedRigidbody
                .gameObject;
        }

        return null;
    }

    private bool ResolveCharacterReferences(
        GameObject character)
    {
        if (character == null)
            return false;

        movement =
            character.GetComponent<UltimatePlayerMovement>();

        movement ??=
            character.GetComponentInParent<UltimatePlayerMovement>();

        movement ??=
            character.GetComponentInChildren<UltimatePlayerMovement>(
                includeInactive: true);

        stuckRigidbody =
            character.GetComponent<Rigidbody>();

        stuckRigidbody ??=
            character.GetComponentInParent<Rigidbody>();

        stuckRigidbody ??=
            character.GetComponentInChildren<Rigidbody>(
                includeInactive: true);

        stuckAnimator =
            character.GetComponent<Animator>();

        stuckAnimator ??=
            character.GetComponentInChildren<Animator>(
                includeInactive: true);

        stuckAnimator ??=
            character.GetComponentInParent<Animator>();

        homingAttack =
            character.GetComponent<HomingAttack>();

        homingAttack ??=
            character.GetComponentInParent<HomingAttack>();

        return
            movement != null &&
            stuckRigidbody != null &&
            movement.isActiveAndEnabled &&
            stuckRigidbody.gameObject.activeInHierarchy;
    }

    private bool HasUsedHomingAttack(
        GameObject character)
    {
        if (character == null)
            return false;

        HomingAttack resolvedAttack =
            character.GetComponent<HomingAttack>();

        resolvedAttack ??=
            character.GetComponentInParent<HomingAttack>();

        resolvedAttack ??=
            character.GetComponentInChildren<HomingAttack>(
                includeInactive: true);

        return
            resolvedAttack != null &&
            resolvedAttack.HomingAttackUsed;
    }

    private Vector3 ResolveWallNormal(
        Vector3 suppliedNormal,
        Vector3 characterPosition)
    {
        if (IsFiniteVector(
                suppliedNormal) &&
            suppliedNormal.sqrMagnitude >
                0.001f)
        {
            return suppliedNormal.normalized;
        }

        Vector3 fallback =
            characterPosition -
            transform.position;

        if (fallback.sqrMagnitude <=
            0.001f)
        {
            fallback =
                -transform.forward;
        }

        return fallback.normalized;
    }

    #endregion

    #region State Preservation

    private void CacheCharacterState()
    {
        if (stuckRigidbody == null)
            return;

        originalConstraints =
            stuckRigidbody.constraints;

        originalUseGravity =
            stuckRigidbody.useGravity;

        originalIsKinematic =
            stuckRigidbody.isKinematic;

        originalMovementEnabled =
            movement != null &&
            movement.MovementEnabled;

        rigidbodyStateCached = true;
    }

    private void RestoreCharacterState()
    {
        RestoreRigidbodyState();

        if (movement != null &&
            originalMovementEnabled)
        {
            movement.EnableMovement();
        }
    }

    private void RestoreRigidbodyState()
    {
        if (stuckRigidbody == null ||
            !rigidbodyStateCached)
        {
            return;
        }

        stuckRigidbody.constraints =
            originalConstraints;

        stuckRigidbody.useGravity =
            originalUseGravity;

        stuckRigidbody.isKinematic =
            originalIsKinematic;

        stuckRigidbody.WakeUp();
    }

    private void StopRigidbodyMotion()
    {
        if (stuckRigidbody == null)
            return;

        stuckRigidbody.linearVelocity =
            Vector3.zero;

        stuckRigidbody.angularVelocity =
            Vector3.zero;
    }

    #endregion

    #region Runtime Safety

    private bool RunRuntimeSafetyChecks()
    {
        if (!ValidateSceneReferences())
        {
            ResolveSceneReferences();

            if (!ValidateSceneReferences())
            {
                EnterSafetyShutdown(
                    "Scene references could not be restored.");

                return false;
            }
        }

        if (!ValidateTransform(
                transform))
        {
            EnterSafetyShutdown(
                "Triangle Jump has an invalid Transform.");

            return false;
        }

        if (stuckCharacter == null)
            return true;

        if (!ValidateActiveCharacter())
        {
            DetachCharacter(
                notifyListeners: true);

            return false;
        }

        if (!ValidateRigidbodyPhysics())
        {
            DetachCharacter(
                notifyListeners: true);

            return false;
        }

        return true;
    }

    private bool ValidateSceneReferences()
    {
        if (triggerCollider == null)
            return false;

        if (requireSpeedLeader &&
            characterSwitch == null)
        {
            return false;
        }

        if (requireSpeedFormation &&
            actionController == null)
        {
            return false;
        }

        return true;
    }

    private bool ValidateActiveCharacter()
    {
        return
            stuckCharacter != null &&
            movement != null &&
            stuckRigidbody != null &&
            stuckCharacter.activeInHierarchy &&
            movement.isActiveAndEnabled &&
            stuckRigidbody.gameObject.activeInHierarchy &&
            ValidateTransform(
                stuckCharacter.transform);
    }

    private bool ValidateRigidbodyPhysics()
    {
        if (stuckRigidbody == null)
            return false;

        return
            IsFiniteVector(
                stuckRigidbody.position) &&
            IsFiniteQuaternion(
                stuckRigidbody.rotation) &&
            IsFiniteVector(
                stuckRigidbody.linearVelocity) &&
            IsFiniteVector(
                stuckRigidbody.angularVelocity);
    }

    private void EnterSafetyShutdown(
        string reason)
    {
        CleanupRuntimeState();

        initialized = false;

        currentState =
            TriangleJumpState.Disabled;

        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        Debug.LogError(
            $"{nameof(TriangleJump)} entered safety shutdown on '{name}': {reason}",
            this);

        enabled = false;
    }

    #endregion

    #region Validation

    private bool ValidateConfiguration()
    {
        bool valid = true;

        valid &=
            ValidateReference(
                triggerCollider,
                nameof(Collider));

        if (requireSpeedLeader)
        {
            valid &=
                ValidateReference(
                    characterSwitch,
                    nameof(CharacterSwitch));
        }

        if (requireSpeedFormation)
        {
            valid &=
                ValidateReference(
                    actionController,
                    nameof(TeamActionController));
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
            $"{nameof(TriangleJump)} requires {displayName} on '{name}'.",
            this);

        return false;
    }

    #endregion

    #region Animation

    private void PlayAnimation(
        int animationHash)
    {
        if (stuckAnimator == null ||
            animationHash == 0)
        {
            return;
        }

        stuckAnimator.Play(
            animationHash);
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        CancelTriangleJumpRoutine();
        CancelLaunchResetRoutine();

        if (stuckCharacter != null)
        {
            RestoreCharacterState();
        }

        triangleJumpReady = false;
        isJumping = false;

        ClearCharacterReferences();
    }

    private void CancelTriangleJumpRoutine()
    {
        if (triangleJumpRoutine == null)
            return;

        StopCoroutine(
            triangleJumpRoutine);

        triangleJumpRoutine = null;
    }

    private void CancelLaunchResetRoutine()
    {
        if (launchResetRoutine == null)
            return;

        StopCoroutine(
            launchResetRoutine);

        launchResetRoutine = null;
    }

    private void ClearCharacterReferences()
    {
        stuckCharacter = null;
        movement = null;
        stuckRigidbody = null;
        stuckAnimator = null;
        homingAttack = null;

        triangleJumpRoutine = null;

        wallNormal =
            Vector3.forward;

        originalConstraints =
            RigidbodyConstraints.None;

        originalUseGravity = false;
        originalIsKinematic = false;
        originalMovementEnabled = false;

        rigidbodyStateCached = false;
    }

    #endregion

    #region Helpers

    private bool ValidateTransform(
        Transform target)
    {
        if (target == null)
            return false;

        Vector3 scale =
            target.lossyScale;

        return
            IsFiniteVector(
                target.position) &&
            IsFiniteQuaternion(
                target.rotation) &&
            IsFiniteVector(
                scale) &&
            Mathf.Abs(scale.x) >=
                minimumValidScale &&
            Mathf.Abs(scale.y) >=
                minimumValidScale &&
            Mathf.Abs(scale.z) >=
                minimumValidScale;
    }

    private static bool IsFiniteVector(
        Vector3 value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }

    private static bool IsFiniteQuaternion(
        Quaternion value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z) &&
            float.IsFinite(value.w);
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