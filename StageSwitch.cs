using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class Switch : MonoBehaviour
{
    #region Types

    public enum SwitchType
    {
        Button,
        FloorPlate,
        Lever,
        Target,
        Timed,
        Toggle,
        TeamAction,
        External
    }

    public enum ActivationMode
    {
        Once,
        Reusable,
        Toggle,
        Hold,
        Timed
    }

    public enum SwitchState
    {
        Uninitialized,
        Ready,
        Activating,
        Active,
        Deactivating,
        Cooldown,
        Disabled
    }

    public enum TargetAction
    {
        ActivateGameObject,
        DeactivateGameObject,
        ToggleGameObject,
        EnableBehaviour,
        DisableBehaviour,
        ToggleBehaviour,
        EnableCollider,
        DisableCollider,
        ToggleCollider,
        PlayAnimatorTrigger,
        SetAnimatorBoolTrue,
        SetAnimatorBoolFalse,
        SendMessage
    }

    [Serializable]
    public sealed class SwitchTargetBinding
    {
        [SerializeField] private GameObject targetObject;
        [SerializeField]
        private TargetAction action =
            TargetAction.SendMessage;

        [SerializeField] private Behaviour targetBehaviour;
        [SerializeField] private Collider targetCollider;
        [SerializeField] private Animator targetAnimator;

        [SerializeField] private string animatorParameter;
        [SerializeField]
        private string messageName =
            "Activate";

        [SerializeField] private bool includeChildren = true;

        public GameObject TargetObject =>
            targetObject;

        public bool Execute(
            bool switchActive,
            UnityEngine.Object context)
        {
            if (!ResolveReferences())
                return false;

            switch (action)
            {
                case TargetAction.ActivateGameObject:
                    targetObject.SetActive(
                        true);
                    return true;

                case TargetAction.DeactivateGameObject:
                    targetObject.SetActive(
                        false);
                    return true;

                case TargetAction.ToggleGameObject:
                    targetObject.SetActive(
                        !targetObject.activeSelf);
                    return true;

                case TargetAction.EnableBehaviour:
                    targetBehaviour.enabled =
                        true;
                    return true;

                case TargetAction.DisableBehaviour:
                    targetBehaviour.enabled =
                        false;
                    return true;

                case TargetAction.ToggleBehaviour:
                    targetBehaviour.enabled =
                        !targetBehaviour.enabled;
                    return true;

                case TargetAction.EnableCollider:
                    targetCollider.enabled =
                        true;
                    return true;

                case TargetAction.DisableCollider:
                    targetCollider.enabled =
                        false;
                    return true;

                case TargetAction.ToggleCollider:
                    targetCollider.enabled =
                        !targetCollider.enabled;
                    return true;

                case TargetAction.PlayAnimatorTrigger:
                    if (string.IsNullOrWhiteSpace(
                            animatorParameter))
                    {
                        return false;
                    }

                    targetAnimator.SetTrigger(
                        Animator.StringToHash(
                            animatorParameter));
                    return true;

                case TargetAction.SetAnimatorBoolTrue:
                    if (string.IsNullOrWhiteSpace(
                            animatorParameter))
                    {
                        return false;
                    }

                    targetAnimator.SetBool(
                        Animator.StringToHash(
                            animatorParameter),
                        true);
                    return true;

                case TargetAction.SetAnimatorBoolFalse:
                    if (string.IsNullOrWhiteSpace(
                            animatorParameter))
                    {
                        return false;
                    }

                    targetAnimator.SetBool(
                        Animator.StringToHash(
                            animatorParameter),
                        false);
                    return true;

                case TargetAction.SendMessage:
                    if (string.IsNullOrWhiteSpace(
                            messageName))
                    {
                        return false;
                    }

                    targetObject.SendMessage(
                        messageName,
                        switchActive,
                        SendMessageOptions.DontRequireReceiver);
                    return true;

                default:
                    Debug.LogError(
                        $"Unhandled {nameof(TargetAction)} value '{action}'.",
                        context);
                    return false;
            }
        }

        private bool ResolveReferences()
        {
            if (targetObject == null)
            {
                if (targetBehaviour != null)
                {
                    targetObject =
                        targetBehaviour.gameObject;
                }
                else if (targetCollider != null)
                {
                    targetObject =
                        targetCollider.gameObject;
                }
                else if (targetAnimator != null)
                {
                    targetObject =
                        targetAnimator.gameObject;
                }
            }

            if (targetObject == null)
                return false;

            if (RequiresBehaviour() &&
                targetBehaviour == null)
            {
                targetBehaviour =
                    includeChildren
                        ? targetObject.GetComponentInChildren<Behaviour>(
                            includeInactive: true)
                        : targetObject.GetComponent<Behaviour>();
            }

            if (RequiresCollider() &&
                targetCollider == null)
            {
                targetCollider =
                    includeChildren
                        ? targetObject.GetComponentInChildren<Collider>(
                            includeInactive: true)
                        : targetObject.GetComponent<Collider>();
            }

            if (RequiresAnimator() &&
                targetAnimator == null)
            {
                targetAnimator =
                    includeChildren
                        ? targetObject.GetComponentInChildren<Animator>(
                            includeInactive: true)
                        : targetObject.GetComponent<Animator>();
            }

            return action switch
            {
                TargetAction.EnableBehaviour or
                TargetAction.DisableBehaviour or
                TargetAction.ToggleBehaviour =>
                    targetBehaviour != null,

                TargetAction.EnableCollider or
                TargetAction.DisableCollider or
                TargetAction.ToggleCollider =>
                    targetCollider != null,

                TargetAction.PlayAnimatorTrigger or
                TargetAction.SetAnimatorBoolTrue or
                TargetAction.SetAnimatorBoolFalse =>
                    targetAnimator != null,

                _ =>
                    targetObject != null
            };
        }

        private bool RequiresBehaviour()
        {
            return action ==
                   TargetAction.EnableBehaviour ||
                   action ==
                   TargetAction.DisableBehaviour ||
                   action ==
                   TargetAction.ToggleBehaviour;
        }

        private bool RequiresCollider()
        {
            return action ==
                   TargetAction.EnableCollider ||
                   action ==
                   TargetAction.DisableCollider ||
                   action ==
                   TargetAction.ToggleCollider;
        }

        private bool RequiresAnimator()
        {
            return action ==
                   TargetAction.PlayAnimatorTrigger ||
                   action ==
                   TargetAction.SetAnimatorBoolTrue ||
                   action ==
                   TargetAction.SetAnimatorBoolFalse;
        }
    }

    #endregion

    #region Constants

    private const string DefaultHandleName =
        "Switch Handle";

    private const string DefaultActivateTrigger =
        "Activate";

    private const string DefaultDeactivateTrigger =
        "Deactivate";

    #endregion

    #region Inspector

    [Header("Switch")]
    [SerializeField]
    private SwitchType switchType =
        SwitchType.Button;

    [SerializeField]
    private ActivationMode activationMode =
        ActivationMode.Once;

    [SerializeField]
    private SwitchState currentState =
        SwitchState.Uninitialized;

    [Header("Activation")]
    [SerializeField] private bool activateOnPlayerEnter = true;
    [SerializeField] private bool requireInteractionInput;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private bool requirePlayerTag = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool requirePlayerMovement = true;
    [SerializeField, Min(0f)] private float reactivationCooldown = 0.25f;
    [SerializeField, Min(0f)] private float activeDuration = 3f;

    [Header("Visual Movement")]
    [SerializeField] private Transform switchHandle;

    [SerializeField]
    private Vector3 activatedLocalPositionOffset =
        Vector3.zero;

    [SerializeField]
    private Vector3 activatedLocalRotation =
        new Vector3(
            70f,
            0f,
            0f);

    [SerializeField, Min(0f)]
    private float handleMoveDuration = 0.35f;

    [Header("Targets")]
    [SerializeField]
    private SwitchTargetBinding[] targets =
        Array.Empty<SwitchTargetBinding>();

    [SerializeField] private bool continueWhenTargetFails = true;

    [Header("Animation")]
    [SerializeField] private Animator switchAnimator;
    [SerializeField]
    private string activateTrigger =
        DefaultActivateTrigger;

    [SerializeField]
    private string deactivateTrigger =
        DefaultDeactivateTrigger;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip activationSound;
    [SerializeField] private AudioClip deactivationSound;

    [Header("Effects")]
    [SerializeField] private ParticleSystem activationEffect;
    [SerializeField] private ParticleSystem deactivationEffect;

    [Header("Runtime Safety")]
    [SerializeField] private bool enableRuntimeSafety = true;
    [SerializeField] private bool restoreDisabledComponents = true;
    [SerializeField, Min(0.1f)] private float safetyCheckInterval = 1f;
    [SerializeField, Min(0.01f)] private float minimumValidScale = 0.01f;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private Collider triggerCollider;

    private Vector3 startingHandleLocalPosition;
    private Quaternion startingHandleLocalRotation;

    private Coroutine stateRoutine;

    private float cooldownTimer;
    private float activeTimer;
    private float safetyTimer;

    private int activateTriggerHash;
    private int deactivateTriggerHash;

    private int playerOverlapCount;

    private bool initialized;
    private bool isActive;
    private bool shuttingDown;
    private bool applicationQuitting;

    #endregion

    #region Events

    public event Action<Switch> Activated;
    public event Action<Switch> Deactivated;
    public event Action<Switch> ResetCompleted;
    public event Action<Switch, SwitchState, SwitchState> StateChanged;

    #endregion

    #region Public API

    public SwitchType Type =>
        switchType;

    public ActivationMode Mode =>
        activationMode;

    public SwitchState CurrentState =>
        currentState;

    public bool IsInitialized =>
        initialized;

    public bool IsActivated =>
        isActive;

    public bool PlayerInRange =>
        playerOverlapCount > 0;

    public bool ActivateSwitch()
    {
        if (!CanActivate())
            return false;

        switch (activationMode)
        {
            case ActivationMode.Once:
            case ActivationMode.Reusable:
            case ActivationMode.Hold:
            case ActivationMode.Timed:
                return BeginActivation();

            case ActivationMode.Toggle:
                return isActive
                    ? BeginDeactivation()
                    : BeginActivation();

            default:
                Debug.LogError(
                    $"Unhandled {nameof(ActivationMode)} value '{activationMode}'.",
                    this);
                return false;
        }
    }

    public bool ActivateSwitch(
        bool value)
    {
        return value
            ? BeginActivation()
            : BeginDeactivation();
    }

    public bool DeactivateSwitch()
    {
        return BeginDeactivation();
    }

    public bool ToggleSwitch()
    {
        return isActive
            ? BeginDeactivation()
            : BeginActivation();
    }

    public bool ResetSwitch()
    {
        if (shuttingDown ||
            applicationQuitting)
        {
            return false;
        }

        CancelStateRoutine();

        isActive =
            false;

        activeTimer =
            0f;

        cooldownTimer =
            0f;

        RestoreHandleImmediately();
        SetAnimatorBoolSafe();
        RestoreTriggerCollider();

        ChangeState(
            SwitchState.Ready);

        ResetCompleted?.Invoke(
            this);

        LogStateChange(
            "Switch reset.");

        return true;
    }

    public bool SetTargets(
        SwitchTargetBinding[] newTargets)
    {
        targets =
            newTargets ??
            Array.Empty<SwitchTargetBinding>();

        return true;
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
            currentState ==
            SwitchState.Disabled)
        {
            ChangeState(
                isActive
                    ? SwitchState.Active
                    : SwitchState.Ready);
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

        UpdateCooldown();
        UpdateTimedActivation();
        UpdateInteractionInput();

        if (!enableRuntimeSafety)
            return;

        safetyTimer -=
            Time.deltaTime;

        if (safetyTimer > 0f)
            return;

        safetyTimer =
            safetyCheckInterval;

        RunRuntimeSafetyChecks();
    }

    private void OnDisable()
    {
        CancelStateRoutine();

        if (!shuttingDown &&
            !applicationQuitting)
        {
            ChangeState(
                SwitchState.Disabled);
        }
    }

    private void OnApplicationQuit()
    {
        applicationQuitting =
            true;
    }

    private void OnDestroy()
    {
        shuttingDown =
            true;

        initialized =
            false;

        CancelStateRoutine();

        Activated = null;
        Deactivated = null;
        ResetCompleted = null;
        StateChanged = null;

        triggerCollider = null;
        switchHandle = null;
        switchAnimator = null;
        audioSource = null;
        activationEffect = null;
        deactivationEffect = null;
        targets = null;

        currentState =
            SwitchState.Disabled;
    }

    private void OnValidate()
    {
        reactivationCooldown =
            Mathf.Max(
                0f,
                reactivationCooldown);

        activeDuration =
            Mathf.Max(
                0f,
                activeDuration);

        handleMoveDuration =
            Mathf.Max(
                0f,
                handleMoveDuration);

        safetyCheckInterval =
            Mathf.Max(
                0.1f,
                safetyCheckInterval);

        minimumValidScale =
            Mathf.Max(
                0.01f,
                minimumValidScale);

        targets ??=
            Array.Empty<SwitchTargetBinding>();

        CacheAnimatorHashes();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ResolveReferences();
            ConfigureComponents();
        }
#endif
    }

    private void OnTriggerEnter(
        Collider other)
    {
        if (!initialized ||
            shuttingDown ||
            applicationQuitting ||
            other == null ||
            !IsValidActivator(
                other))
        {
            return;
        }

        playerOverlapCount++;

        if (!activateOnPlayerEnter ||
            requireInteractionInput)
        {
            return;
        }

        ActivateSwitch();
    }

    private void OnTriggerExit(
        Collider other)
    {
        if (!initialized ||
            other == null ||
            !IsValidActivator(
                other))
        {
            return;
        }

        playerOverlapCount =
            Mathf.Max(
                0,
                playerOverlapCount - 1);

        if (activationMode ==
            ActivationMode.Hold &&
            playerOverlapCount == 0)
        {
            DeactivateSwitch();
        }
    }

    #endregion

    #region Initialization

    private bool Initialize()
    {
        if (initialized)
            return true;

        ResolveReferences();
        ConfigureComponents();
        CacheAnimatorHashes();
        CacheStartingTransform();

        safetyTimer =
            safetyCheckInterval;

        cooldownTimer =
            0f;

        activeTimer =
            0f;

        playerOverlapCount =
            0;

        if (!ValidateConfiguration())
        {
            initialized =
                false;

            currentState =
                SwitchState.Uninitialized;

            enabled =
                false;

            return false;
        }

        initialized =
            true;

        ChangeState(
            SwitchState.Ready);

        return true;
    }

    private void ResolveReferences()
    {
        triggerCollider ??=
            GetComponent<Collider>();

        switchHandle ??=
            FindDescendantByName(
                DefaultHandleName);

        switchAnimator ??=
            GetComponent<Animator>();

        switchAnimator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        audioSource ??=
            GetComponent<AudioSource>();

        audioSource ??=
            GetComponentInChildren<AudioSource>(
                includeInactive: true);

        targets ??=
            Array.Empty<SwitchTargetBinding>();
    }

    private void ConfigureComponents()
    {
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger =
                true;
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake =
                false;
        }
    }

    private void CacheStartingTransform()
    {
        if (switchHandle == null)
            return;

        startingHandleLocalPosition =
            switchHandle.localPosition;

        startingHandleLocalRotation =
            switchHandle.localRotation;
    }

    private void CacheAnimatorHashes()
    {
        activateTriggerHash =
            GetAnimatorHash(
                activateTrigger);

        deactivateTriggerHash =
            GetAnimatorHash(
                deactivateTrigger);
    }

    #endregion

    #region State Machine

    private void ChangeState(
        SwitchState newState)
    {
        if (currentState ==
            newState)
        {
            return;
        }

        SwitchState previousState =
            currentState;

        currentState =
            newState;

        StateChanged?.Invoke(
            this,
            previousState,
            newState);

        LogStateChange(
            $"State changed from {previousState} to {newState}.");
    }

    private bool BeginActivation()
    {
        if (!CanActivate())
            return false;

        CancelStateRoutine();

        stateRoutine =
            StartCoroutine(
                ActivationRoutine());

        return true;
    }

    private bool BeginDeactivation()
    {
        if (!CanDeactivate())
            return false;

        CancelStateRoutine();

        stateRoutine =
            StartCoroutine(
                DeactivationRoutine());

        return true;
    }

    private IEnumerator ActivationRoutine()
    {
        ChangeState(
            SwitchState.Activating);

        PlayActivationPresentation();

        yield return MoveHandle(
            activating: true);

        isActive =
            true;

        ExecuteTargets(
            switchActive: true);

        activeTimer =
            activeDuration;

        ChangeState(
            SwitchState.Active);

        Activated?.Invoke(
            this);

        stateRoutine =
            null;

        if (activationMode ==
            ActivationMode.Once)
        {
            DisableTriggerCollider();
        }
    }

    private IEnumerator DeactivationRoutine()
    {
        ChangeState(
            SwitchState.Deactivating);

        PlayDeactivationPresentation();

        yield return MoveHandle(
            activating: false);

        isActive =
            false;

        ExecuteTargets(
            switchActive: false);

        cooldownTimer =
            reactivationCooldown;

        ChangeState(
            cooldownTimer > 0f
                ? SwitchState.Cooldown
                : SwitchState.Ready);

        Deactivated?.Invoke(
            this);

        stateRoutine =
            null;
    }

    #endregion

    #region Handle Movement

    private IEnumerator MoveHandle(
        bool activating)
    {
        if (switchHandle == null)
            yield break;

        Vector3 startPosition =
            switchHandle.localPosition;

        Quaternion startRotation =
            switchHandle.localRotation;

        Vector3 targetPosition =
            activating
                ? startingHandleLocalPosition +
                  activatedLocalPositionOffset
                : startingHandleLocalPosition;

        Quaternion targetRotation =
            activating
                ? startingHandleLocalRotation *
                  Quaternion.Euler(
                      activatedLocalRotation)
                : startingHandleLocalRotation;

        if (handleMoveDuration <= 0f)
        {
            switchHandle.localPosition =
                targetPosition;

            switchHandle.localRotation =
                targetRotation;

            yield break;
        }

        float elapsed =
            0f;

        while (elapsed <
               handleMoveDuration)
        {
            if (switchHandle == null ||
                shuttingDown ||
                applicationQuitting)
            {
                yield break;
            }

            elapsed +=
                Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsed /
                    handleMoveDuration);

            progress =
                SmoothStep(
                    progress);

            switchHandle.localPosition =
                Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    progress);

            switchHandle.localRotation =
                Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    progress);

            yield return null;
        }

        switchHandle.localPosition =
            targetPosition;

        switchHandle.localRotation =
            targetRotation;
    }

    private void RestoreHandleImmediately()
    {
        if (switchHandle == null)
            return;

        switchHandle.localPosition =
            startingHandleLocalPosition;

        switchHandle.localRotation =
            startingHandleLocalRotation;
    }

    #endregion

    #region Target Execution

    private bool ExecuteTargets(
        bool switchActive)
    {
        if (targets == null ||
            targets.Length == 0)
        {
            return true;
        }

        bool allSucceeded =
            true;

        foreach (SwitchTargetBinding binding
                 in targets)
        {
            if (binding == null)
            {
                allSucceeded =
                    false;

                if (!continueWhenTargetFails)
                    return false;

                continue;
            }

            bool succeeded =
                binding.Execute(
                    switchActive,
                    this);

            if (succeeded)
                continue;

            allSucceeded =
                false;

            if (!continueWhenTargetFails)
                return false;
        }

        return allSucceeded;
    }

    #endregion

    #region Activation Rules

    private bool CanActivate()
    {
        if (!initialized ||
            shuttingDown ||
            applicationQuitting ||
            !isActiveAndEnabled ||
            cooldownTimer > 0f ||
            currentState ==
                SwitchState.Activating ||
            currentState ==
                SwitchState.Deactivating ||
            currentState ==
                SwitchState.Disabled)
        {
            return false;
        }

        if (activationMode ==
            ActivationMode.Once &&
            isActive)
        {
            return false;
        }

        if (activationMode !=
                ActivationMode.Toggle &&
            isActive)
        {
            return false;
        }

        return true;
    }

    private bool CanDeactivate()
    {
        if (!initialized ||
            shuttingDown ||
            applicationQuitting ||
            !isActive ||
            currentState ==
                SwitchState.Deactivating ||
            currentState ==
                SwitchState.Disabled)
        {
            return false;
        }

        return activationMode !=
               ActivationMode.Once;
    }

    private bool IsValidActivator(
        Collider other)
    {
        if (other == null)
            return false;

        if (requirePlayerTag &&
            !HasTagInHierarchy(
                other.transform,
                playerTag))
        {
            return false;
        }

        if (!requirePlayerMovement)
            return true;

        UltimatePlayerMovement movement =
            other.GetComponent<UltimatePlayerMovement>();

        movement ??=
            other.GetComponentInParent<UltimatePlayerMovement>();

        movement ??=
            other.GetComponentInChildren<UltimatePlayerMovement>(
                includeInactive: true);

        return
            movement != null &&
            movement.isActiveAndEnabled &&
            movement.IsInitialized &&
            !movement.IsSafetyShutdown;
    }

    #endregion

    #region Timers And Input

    private void UpdateCooldown()
    {
        if (cooldownTimer <= 0f)
            return;

        cooldownTimer =
            Mathf.Max(
                0f,
                cooldownTimer -
                Time.deltaTime);

        if (cooldownTimer <= 0f &&
            currentState ==
            SwitchState.Cooldown)
        {
            ChangeState(
                SwitchState.Ready);
        }
    }

    private void UpdateTimedActivation()
    {
        if (!isActive ||
            activationMode !=
            ActivationMode.Timed)
        {
            return;
        }

        activeTimer =
            Mathf.Max(
                0f,
                activeTimer -
                Time.deltaTime);

        if (activeTimer <= 0f)
        {
            DeactivateSwitch();
        }
    }

    private void UpdateInteractionInput()
    {
        if (!requireInteractionInput ||
            !PlayerInRange ||
            !Input.GetKeyDown(
                interactionKey))
        {
            return;
        }

        ActivateSwitch();
    }

    #endregion

    #region Presentation

    private void PlayActivationPresentation()
    {
        SetAnimatorTrigger(
            activateTriggerHash);

        activationEffect?.Play();

        PlaySound(
            activationSound);
    }

    private void PlayDeactivationPresentation()
    {
        SetAnimatorTrigger(
            deactivateTriggerHash);

        deactivationEffect?.Play();

        PlaySound(
            deactivationSound);
    }

    private void SetAnimatorTrigger(
        int triggerHash)
    {
        if (switchAnimator == null ||
            triggerHash == 0)
        {
            return;
        }

        switchAnimator.SetTrigger(
            triggerHash);
    }

    private void SetAnimatorBoolSafe()
    {
        if (switchAnimator == null)
            return;

        if (activateTriggerHash != 0)
        {
            switchAnimator.ResetTrigger(
                activateTriggerHash);
        }

        if (deactivateTriggerHash != 0)
        {
            switchAnimator.ResetTrigger(
                deactivateTriggerHash);
        }
    }

    private void PlaySound(
        AudioClip clip)
    {
        if (audioSource == null ||
            clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(
            clip);
    }

    #endregion

    #region Runtime Safety

    private bool RunRuntimeSafetyChecks()
    {
        if (!ValidateCoreReferences())
        {
            ResolveReferences();
            ConfigureComponents();

            if (!ValidateCoreReferences())
            {
                EnterSafetyShutdown(
                    "Required references could not be restored.");

                return false;
            }
        }

        if (!ValidateTransform(
                transform))
        {
            EnterSafetyShutdown(
                "Switch Transform contains invalid values.");

            return false;
        }

        if (restoreDisabledComponents)
        {
            RestoreRequiredComponents();
        }

        playerOverlapCount =
            Mathf.Max(
                0,
                playerOverlapCount);

        return true;
    }

    private void RestoreRequiredComponents()
    {
        if (triggerCollider != null &&
            !triggerCollider.enabled &&
            activationMode !=
                ActivationMode.Once)
        {
            triggerCollider.enabled =
                true;
        }

        if (audioSource != null &&
            !audioSource.enabled)
        {
            audioSource.enabled =
                true;
        }

        if (switchAnimator != null &&
            !switchAnimator.enabled)
        {
            switchAnimator.enabled =
                true;
        }
    }

    private void EnterSafetyShutdown(
        string reason)
    {
        CancelStateRoutine();

        initialized =
            false;

        ChangeState(
            SwitchState.Disabled);

        if (triggerCollider != null)
        {
            triggerCollider.enabled =
                false;
        }

        Debug.LogError(
            $"{nameof(Switch)} entered safety shutdown on '{name}': {reason}",
            this);

        enabled =
            false;
    }

    #endregion

    #region Validation

    private bool ValidateConfiguration()
    {
        bool valid =
            ValidateCoreReferences();

        if (!valid)
        {
            Debug.LogError(
                $"{nameof(Switch)} on '{name}' requires a Collider.",
                this);
        }

        return valid;
    }

    private bool ValidateCoreReferences()
    {
        return triggerCollider !=
               null;
    }

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

    #endregion

    #region Cleanup

    private void CancelStateRoutine()
    {
        if (stateRoutine == null)
            return;

        StopCoroutine(
            stateRoutine);

        stateRoutine =
            null;
    }

    private void DisableTriggerCollider()
    {
        if (triggerCollider != null)
        {
            triggerCollider.enabled =
                false;
        }
    }

    private void RestoreTriggerCollider()
    {
        if (triggerCollider == null)
            return;

        triggerCollider.enabled =
            true;

        triggerCollider.isTrigger =
            true;
    }

    #endregion

    #region Helpers

    private Transform FindDescendantByName(
        string targetName)
    {
        if (string.IsNullOrWhiteSpace(
                targetName))
        {
            return null;
        }

        Transform[] descendants =
            GetComponentsInChildren<Transform>(
                includeInactive: true);

        foreach (Transform descendant
                 in descendants)
        {
            if (descendant == null)
                continue;

            if (string.Equals(
                    descendant.name,
                    targetName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return descendant;
            }
        }

        return null;
    }

    private static bool HasTagInHierarchy(
        Transform source,
        string requiredTag)
    {
        if (source == null)
            return false;

        if (string.IsNullOrWhiteSpace(
                requiredTag))
        {
            return true;
        }

        Transform current =
            source;

        while (current != null)
        {
            if (current.CompareTag(
                    requiredTag))
            {
                return true;
            }

            current =
                current.parent;
        }

        return false;
    }

    private static int GetAnimatorHash(
        string parameterName)
    {
        return
            string.IsNullOrWhiteSpace(
                parameterName)
                ? 0
                : Animator.StringToHash(
                    parameterName);
    }

    private static float SmoothStep(
        float value)
    {
        return
            value *
            value *
            (3f -
             2f *
             value);
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
