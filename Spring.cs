using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class Spring : MonoBehaviour
{
    #region Types

    public enum SpringType
    {
        Regular,
        Wide
    }

    public enum SpringState
    {
        Ready,
        Launching,
        Cooldown,
        Disabled
    }

    #endregion

    #region Constants

    private const string LeftPadName = "Left Pad";
    private const string CenterPadName = "Center Pad";
    private const string RightPadName = "Right Pad";
    private const int MaximumTeamCapacity = 3;

    private static readonly int ActivateHash =
        Animator.StringToHash("Activate");

    #endregion

    #region Inspector

    [Header("Spring")]
    [SerializeField]
    private SpringType springType =
        SpringType.Regular;

    [SerializeField]
    private SpringState startingState =
        SpringState.Ready;

    [Header("Dependencies")]
    [SerializeField] private Collider springCollider;
    [SerializeField] private Animator springAnimator;
    [SerializeField] private AudioSource audioSource;

    [Header("Launch")]
    [SerializeField, Min(0f)] private float launchSpeed = 28f;

    [SerializeField]
    private Vector3 localLaunchDirection =
        Vector3.up;

    [SerializeField, Min(0f)] private float positionOffset = 0.1f;
    [SerializeField] private bool clearExistingVelocity = true;
    [SerializeField] private bool enableMovementBeforeLaunch = true;

    [Header("Activation")]
    [SerializeField] private bool requirePlayerTag = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField, Min(0f)] private float reactivationDelay = 0.2f;
    [SerializeField, Min(0f)] private float globalCooldown = 0.05f;

    [Header("Wide Spring")]
    [SerializeField] private Transform leftPad;
    [SerializeField] private Transform centerPad;
    [SerializeField] private Transform rightPad;
    [SerializeField, Min(0.1f)] private float teamSearchRadius = 5f;
    [SerializeField] private bool launchEntireTeam = true;
    [SerializeField] private bool requireAllThreePads;
    [SerializeField, Min(0f)] private float padActivationWindow = 0.75f;

    [Header("Presentation")]
    [SerializeField] private AudioClip launchClip;
    [SerializeField] private ParticleSystem launchEffect;
    [SerializeField] private ParticleSystem leftPadEffect;
    [SerializeField] private ParticleSystem centerPadEffect;
    [SerializeField] private ParticleSystem rightPadEffect;

    [Header("Runtime Safety")]
    [SerializeField] private bool enableRuntimeSafety = true;
    [SerializeField, Min(1f)] private float maximumLaunchSpeed = 100f;
    [SerializeField, Min(0.01f)] private float minimumValidScale = 0.01f;
    [SerializeField, Min(0.1f)] private float safetyCheckInterval = 1f;
    [SerializeField] private bool restoreDisabledComponents = true;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private readonly Dictionary<Rigidbody, float> activationTimes =
        new();

    private readonly List<Rigidbody> expiredBodies =
        new();

    private readonly List<UltimatePlayerMovement> teamCandidates =
        new(MaximumTeamCapacity);

    private SpringState currentState;

    private float cooldownTimer;
    private float safetyTimer;

    private float leftPadActivationTime =
        float.NegativeInfinity;

    private float centerPadActivationTime =
        float.NegativeInfinity;

    private float rightPadActivationTime =
        float.NegativeInfinity;

    private bool initialized;
    private bool shuttingDown;
    private bool applicationQuitting;
    private bool safetyShutdown;

    #endregion

    #region Public API

    public event Action<SpringState> StateChanged;
    public event Action<UltimatePlayerMovement> CharacterLaunched;
    public event Action<int> TeamLaunched;

    public SpringType Type =>
        springType;

    public SpringState CurrentState =>
        currentState;

    public bool IsInitialized =>
        initialized;

    public bool IsSafetyShutdown =>
        safetyShutdown;

    public Vector3 LaunchDirection =>
        GetLaunchDirection();

    public bool Launch(
        GameObject character)
    {
        if (character == null)
            return false;

        UltimatePlayerMovement movement =
            ResolveMovement(
                character);

        return Launch(
            movement);
    }

    public bool Launch(
        UltimatePlayerMovement movement)
    {
        if (!CanStartLaunch() ||
            !IsValidMovement(
                movement) ||
            !ResolveCharacterRigidbody(
                movement,
                out Rigidbody body) ||
            !CanActivateBody(
                body))
        {
            return false;
        }

        if (springType ==
                SpringType.Wide &&
            requireAllThreePads &&
            !AreAllPadsActive())
        {
            return false;
        }

        ChangeState(
            SpringState.Launching);

        int launchedCount;

        switch (springType)
        {
            case SpringType.Regular:
                launchedCount =
                    LaunchCharacter(
                        movement,
                        body)
                        ? 1
                        : 0;
                break;

            case SpringType.Wide:
                launchedCount =
                    launchEntireTeam
                        ? LaunchWideTeam(
                            movement)
                        : LaunchCharacter(
                            movement,
                            body)
                            ? 1
                            : 0;
                break;

            default:
                Debug.LogError(
                    $"Unhandled {nameof(SpringType)} value '{springType}'.",
                    this);

                launchedCount =
                    0;
                break;
        }

        if (launchedCount <= 0)
        {
            ChangeState(
                SpringState.Ready);

            return false;
        }

        PlayPresentation();

        if (springType ==
            SpringType.Wide)
        {
            TeamLaunched?.Invoke(
                launchedCount);
        }

        cooldownTimer =
            globalCooldown;

        ChangeState(
            cooldownTimer > 0f
                ? SpringState.Cooldown
                : SpringState.Ready);

        return true;
    }

    public void RegisterPadActivation(
        Transform pad)
    {
        if (springType !=
                SpringType.Wide ||
            pad == null)
        {
            return;
        }

        if (pad == leftPad)
        {
            leftPadActivationTime =
                Time.time;

            leftPadEffect?.Play();
            return;
        }

        if (pad == centerPad)
        {
            centerPadActivationTime =
                Time.time;

            centerPadEffect?.Play();
            return;
        }

        if (pad == rightPad)
        {
            rightPadActivationTime =
                Time.time;

            rightPadEffect?.Play();
        }
    }

    public bool ResetSpring()
    {
        if (shuttingDown ||
            applicationQuitting)
        {
            return false;
        }

        ResetRuntimeState();

        safetyShutdown =
            false;

        RestoreRequiredComponents();

        enabled =
            true;

        ChangeState(
            SpringState.Ready);

        return true;
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
        if (!InitializeSpring())
        {
            enabled =
                false;
        }
    }

    private void OnEnable()
    {
        if (shuttingDown)
            return;

        ResolveDependencies();
        ConfigureComponents();

        if (initialized &&
            !safetyShutdown)
        {
            ChangeState(
                currentState ==
                    SpringState.Disabled
                    ? SpringState.Ready
                    : currentState);
        }
    }

    private void Update()
    {
        if (!initialized ||
            shuttingDown ||
            applicationQuitting ||
            safetyShutdown)
        {
            return;
        }

        UpdateCooldown();
        UpdateActivationHistory();

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

    private void OnTriggerEnter(
        Collider other)
    {
        if (!initialized ||
            shuttingDown ||
            applicationQuitting ||
            safetyShutdown ||
            other == null)
        {
            return;
        }

        UltimatePlayerMovement movement =
            ResolveMovement(
                other);

        if (movement == null)
            return;

        if (springType ==
            SpringType.Wide)
        {
            RegisterClosestPad(
                movement.transform.position);
        }

        Launch(
            movement);
    }

    private void OnDisable()
    {
        activationTimes.Clear();
        expiredBodies.Clear();
        teamCandidates.Clear();

        if (!shuttingDown &&
            !applicationQuitting)
        {
            ChangeState(
                SpringState.Disabled);
        }
    }

    private void OnDestroy()
    {
        shuttingDown =
            true;

        initialized =
            false;

        StateChanged = null;
        CharacterLaunched = null;
        TeamLaunched = null;

        activationTimes.Clear();
        expiredBodies.Clear();
        teamCandidates.Clear();

        springCollider = null;
        springAnimator = null;
        audioSource = null;

        leftPad = null;
        centerPad = null;
        rightPad = null;

        launchEffect = null;
        leftPadEffect = null;
        centerPadEffect = null;
        rightPadEffect = null;
    }

    private void OnApplicationQuit()
    {
        applicationQuitting =
            true;
    }

    private void OnApplicationPause(
        bool paused)
    {
        if (!paused)
            return;

        cooldownTimer =
            0f;
    }

    private void OnValidate()
    {
        launchSpeed =
            Mathf.Max(
                0f,
                launchSpeed);

        positionOffset =
            Mathf.Max(
                0f,
                positionOffset);

        reactivationDelay =
            Mathf.Max(
                0f,
                reactivationDelay);

        globalCooldown =
            Mathf.Max(
                0f,
                globalCooldown);

        teamSearchRadius =
            Mathf.Max(
                0.1f,
                teamSearchRadius);

        padActivationWindow =
            Mathf.Max(
                0f,
                padActivationWindow);

        maximumLaunchSpeed =
            Mathf.Max(
                1f,
                maximumLaunchSpeed);

        minimumValidScale =
            Mathf.Max(
                0.01f,
                minimumValidScale);

        safetyCheckInterval =
            Mathf.Max(
                0.1f,
                safetyCheckInterval);

        if (!IsFiniteVector(
                localLaunchDirection) ||
            localLaunchDirection.sqrMagnitude <=
                0.0001f)
        {
            localLaunchDirection =
                Vector3.up;
        }

        if (!Enum.IsDefined(
                typeof(SpringType),
                springType))
        {
            springType =
                SpringType.Regular;
        }

        if (!Enum.IsDefined(
                typeof(SpringState),
                startingState))
        {
            startingState =
                SpringState.Ready;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ResolveDependencies();
            ConfigureComponents();
        }
#endif
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 direction =
            GetLaunchDirection();

        Gizmos.DrawRay(
            transform.position,
            direction *
            Mathf.Max(
                1f,
                launchSpeed *
                0.25f));

        if (springType ==
            SpringType.Wide)
        {
            Gizmos.DrawWireSphere(
                transform.position,
                teamSearchRadius);

            DrawPadGizmo(
                leftPad);

            DrawPadGizmo(
                centerPad);

            DrawPadGizmo(
                rightPad);
        }
    }

    #endregion

    #region Initialization

    public bool InitializeSpring()
    {
        if (initialized)
            return true;

        ResolveDependencies();
        ConfigureComponents();

        if (!ValidateConfiguration())
        {
            initialized =
                false;

            safetyShutdown =
                false;

            Debug.LogError(
                $"Spring failed to initialize on '{name}'.",
                this);

            return false;
        }

        ResetRuntimeState();

        currentState =
            IsValidSpringState(
                startingState)
                ? startingState
                : SpringState.Ready;

        initialized =
            true;

        return true;
    }

    private static bool IsValidSpringState(
        SpringState state)
    {
        return Enum.IsDefined(
            typeof(SpringState),
            state);
    }

    private void ResolveDependencies()
    {
        ResolveCollider();
        ResolveAnimator();
        ResolveAudioSource();
        ResolveWideSpringPads();
    }

    private void ResolveCollider()
    {
        if (springCollider != null)
            return;

        springCollider =
            GetComponent<Collider>();

        springCollider ??=
            GetComponentInChildren<Collider>(
                includeInactive: true);

        springCollider ??=
            GetComponentInParent<Collider>();
    }

    private void ResolveAnimator()
    {
        if (springAnimator != null)
            return;

        springAnimator =
            GetComponent<Animator>();

        springAnimator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        springAnimator ??=
            GetComponentInParent<Animator>();
    }

    private void ResolveAudioSource()
    {
        if (audioSource != null)
            return;

        audioSource =
            GetComponent<AudioSource>();

        audioSource ??=
            GetComponentInChildren<AudioSource>(
                includeInactive: true);

        audioSource ??=
            GetComponentInParent<AudioSource>();
    }

    private void ResolveWideSpringPads()
    {
        if (springType !=
            SpringType.Wide)
        {
            return;
        }

        leftPad ??=
            FindChildByName(
                LeftPadName);

        centerPad ??=
            FindChildByName(
                CenterPadName);

        rightPad ??=
            FindChildByName(
                RightPadName);
    }

    private void ConfigureComponents()
    {
        if (springCollider != null)
        {
            springCollider.isTrigger =
                true;
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake =
                false;
        }
    }

    private void ResetRuntimeState()
    {
        activationTimes.Clear();
        expiredBodies.Clear();
        teamCandidates.Clear();

        cooldownTimer =
            0f;

        safetyTimer =
            safetyCheckInterval;

        leftPadActivationTime =
            float.NegativeInfinity;

        centerPadActivationTime =
            float.NegativeInfinity;

        rightPadActivationTime =
            float.NegativeInfinity;
    }

    #endregion

    #region State Machine

    private void ChangeState(
        SpringState newState)
    {
        if (!IsValidSpringState(
                newState))
        {
            Debug.LogError(
                $"Spring received an invalid state value '{newState}'.",
                this);

            return;
        }

        if (currentState ==
            newState)
        {
            return;
        }

        SpringState previousState =
            currentState;

        currentState =
            newState;

        StateChanged?.Invoke(
            currentState);

        if (logStateChanges)
        {
            Debug.Log(
                $"Spring state changed from {previousState} to {currentState} on '{name}'.",
                this);
        }
    }

    #endregion

    #region Character Resolution

    private UltimatePlayerMovement ResolveMovement(
        Collider other)
    {
        if (other == null)
            return null;

        if (requirePlayerTag &&
            !HasTagInHierarchy(
                other.transform,
                playerTag))
        {
            return null;
        }

        UltimatePlayerMovement movement =
            other.GetComponent<UltimatePlayerMovement>();

        movement ??=
            other.GetComponentInParent<UltimatePlayerMovement>();

        movement ??=
            other.GetComponentInChildren<UltimatePlayerMovement>(
                includeInactive: true);

        return IsValidMovement(
                movement)
            ? movement
            : null;
    }

    private UltimatePlayerMovement ResolveMovement(
        GameObject source)
    {
        if (source == null)
            return null;

        UltimatePlayerMovement movement =
            source.GetComponent<UltimatePlayerMovement>();

        movement ??=
            source.GetComponentInParent<UltimatePlayerMovement>();

        movement ??=
            source.GetComponentInChildren<UltimatePlayerMovement>(
                includeInactive: true);

        return IsValidMovement(
                movement)
            ? movement
            : null;
    }

    private static bool IsValidMovement(
        UltimatePlayerMovement movement)
    {
        return
            movement != null &&
            movement.isActiveAndEnabled &&
            movement.IsInitialized &&
            !movement.IsSafetyShutdown;
    }

    private static bool ResolveCharacterRigidbody(
    UltimatePlayerMovement movement,
    out Rigidbody body)
    {
        body =
            null;

        if (movement == null)
            return false;

        body =
            movement.GetComponent<Rigidbody>();

        body ??=
            movement.GetComponentInParent<Rigidbody>();

        body ??=
            movement.GetComponentInChildren<Rigidbody>(
                includeInactive: true);

        return
            body != null &&
            body.gameObject.activeInHierarchy;
    }

    #endregion

    #region Launch

    private bool CanStartLaunch()
    {
        return
            initialized &&
            !shuttingDown &&
            !applicationQuitting &&
            !safetyShutdown &&
            enabled &&
            gameObject.activeInHierarchy &&
            cooldownTimer <= 0f &&
            currentState ==
                SpringState.Ready;
    }

    private bool LaunchCharacter(
        UltimatePlayerMovement movement,
        Rigidbody body)
    {
        if (!IsValidMovement(
                movement) ||
            !ValidateRigidbody(
                body))
        {
            return false;
        }

        RecordActivation(
            body);

        Vector3 direction =
            GetLaunchDirection();

        float safeLaunchSpeed =
            Mathf.Min(
                launchSpeed,
                maximumLaunchSpeed);

        Vector3 launchVelocity =
            direction *
            safeLaunchSpeed;

        if (!IsFiniteVector(
                launchVelocity))
        {
            return false;
        }

        if (positionOffset > 0f)
        {
            Vector3 offsetPosition =
                body.position +
                direction *
                positionOffset;

            if (IsFiniteVector(
                    offsetPosition))
            {
                body.position =
                    offsetPosition;
            }
        }

        if (clearExistingVelocity)
        {
            body.linearVelocity =
                Vector3.zero;

            body.angularVelocity =
                Vector3.zero;
        }

        body.isKinematic =
            false;

        body.useGravity =
            true;

        body.WakeUp();

        if (enableMovementBeforeLaunch)
        {
            movement.EnableMovement();
        }

        movement.LaunchFromSpring(
            launchVelocity);

        CharacterLaunched?.Invoke(
            movement);

        if (logStateChanges)
        {
            Debug.Log(
                $"Spring launched '{movement.name}' at speed {safeLaunchSpeed}.",
                this);
        }

        return true;
    }

    private int LaunchWideTeam(
        UltimatePlayerMovement activator)
    {
        CollectTeamCandidates(
            activator);

        int launchedCount =
            0;

        foreach (UltimatePlayerMovement movement
                 in teamCandidates)
        {
            if (!ResolveCharacterRigidbody(
                    movement,
                    out Rigidbody body) ||
                !CanActivateBody(
                    body))
            {
                continue;
            }

            if (LaunchCharacter(
                    movement,
                    body))
            {
                launchedCount++;
            }

            if (launchedCount >=
                MaximumTeamCapacity)
            {
                break;
            }
        }

        teamCandidates.Clear();

        return launchedCount;
    }

    private void CollectTeamCandidates(
        UltimatePlayerMovement activator)
    {
        teamCandidates.Clear();

        if (IsValidMovement(
                activator))
        {
            teamCandidates.Add(
                activator);
        }

        UltimatePlayerMovement[] movements =
            FindObjectsByType<UltimatePlayerMovement>(
                FindObjectsInactive.Exclude);

        float maximumDistanceSquared =
            teamSearchRadius *
            teamSearchRadius;

        foreach (UltimatePlayerMovement movement
                 in movements)
        {
            if (!IsValidMovement(
                    movement) ||
                movement ==
                    activator)
            {
                continue;
            }

            float distanceSquared =
                (movement.transform.position -
                 transform.position)
                .sqrMagnitude;

            if (distanceSquared >
                maximumDistanceSquared)
            {
                continue;
            }

            teamCandidates.Add(
                movement);
        }

        teamCandidates.Sort(
            CompareMovementDistance);
    }

    private int CompareMovementDistance(
        UltimatePlayerMovement left,
        UltimatePlayerMovement right)
    {
        float leftDistance =
            (left.transform.position -
             transform.position)
            .sqrMagnitude;

        float rightDistance =
            (right.transform.position -
             transform.position)
            .sqrMagnitude;

        return leftDistance.CompareTo(
            rightDistance);
    }

    private Vector3 GetLaunchDirection()
    {
        Vector3 direction =
            transform.TransformDirection(
                localLaunchDirection);

        if (!IsFiniteVector(
                direction) ||
            direction.sqrMagnitude <=
                0.0001f)
        {
            direction =
                transform.up;
        }

        return direction.normalized;
    }

    #endregion

    #region Wide Spring

    private void RegisterClosestPad(
        Vector3 characterPosition)
    {
        Transform closestPad =
            null;

        float closestDistanceSquared =
            float.PositiveInfinity;

        EvaluatePadDistance(
            leftPad,
            characterPosition,
            ref closestPad,
            ref closestDistanceSquared);

        EvaluatePadDistance(
            centerPad,
            characterPosition,
            ref closestPad,
            ref closestDistanceSquared);

        EvaluatePadDistance(
            rightPad,
            characterPosition,
            ref closestPad,
            ref closestDistanceSquared);

        RegisterPadActivation(
            closestPad);
    }

    private static void EvaluatePadDistance(
        Transform pad,
        Vector3 position,
        ref Transform closestPad,
        ref float closestDistanceSquared)
    {
        if (pad == null)
            return;

        float distanceSquared =
            (pad.position -
             position)
            .sqrMagnitude;

        if (distanceSquared >=
            closestDistanceSquared)
        {
            return;
        }

        closestDistanceSquared =
            distanceSquared;

        closestPad =
            pad;
    }

    private bool AreAllPadsActive()
    {
        if (leftPad == null ||
            centerPad == null ||
            rightPad == null)
        {
            return false;
        }

        float oldestAllowedTime =
            Time.time -
            padActivationWindow;

        return
            leftPadActivationTime >=
                oldestAllowedTime &&
            centerPadActivationTime >=
                oldestAllowedTime &&
            rightPadActivationTime >=
                oldestAllowedTime;
    }

    #endregion

    #region Activation Protection

    private bool CanActivateBody(
        Rigidbody body)
    {
        if (!ValidateRigidbody(
                body))
        {
            return false;
        }

        if (!activationTimes.TryGetValue(
                body,
                out float previousTime))
        {
            return true;
        }

        return
            Time.time -
            previousTime >=
            reactivationDelay;
    }

    private void RecordActivation(
        Rigidbody body)
    {
        if (body == null)
            return;

        activationTimes[body] =
            Time.time;
    }

    private void UpdateActivationHistory()
    {
        if (activationTimes.Count == 0)
            return;

        float expirationTime =
            Time.time -
            Mathf.Max(
                1f,
                reactivationDelay *
                4f);

        expiredBodies.Clear();

        foreach (KeyValuePair<Rigidbody, float> entry
                 in activationTimes)
        {
            if (entry.Key == null ||
                entry.Value <=
                    expirationTime)
            {
                expiredBodies.Add(
                    entry.Key);
            }
        }

        foreach (Rigidbody body
                 in expiredBodies)
        {
            activationTimes.Remove(
                body);
        }

        expiredBodies.Clear();
    }

    #endregion

    #region Presentation

    private void PlayPresentation()
    {
        launchEffect?.Play();

        if (springAnimator != null &&
            HasAnimatorParameter(
                springAnimator,
                ActivateHash))
        {
            springAnimator.SetTrigger(
                ActivateHash);
        }

        if (audioSource != null &&
            launchClip != null)
        {
            audioSource.PlayOneShot(
                launchClip);
        }
    }

    private static bool HasAnimatorParameter(
        Animator animator,
        int parameterHash)
    {
        if (animator == null)
            return false;

        foreach (AnimatorControllerParameter parameter
                 in animator.parameters)
        {
            if (parameter.nameHash ==
                parameterHash)
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Cooldown

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
                SpringState.Cooldown)
        {
            ChangeState(
                SpringState.Ready);
        }
    }

    #endregion

    #region Runtime Safety

    private bool RunRuntimeSafetyChecks()
    {
        if (!ValidateRuntimeReferences())
        {
            AttemptRuntimeReferenceRecovery();

            if (!ValidateRuntimeReferences())
            {
                EnterSafetyShutdown(
                    "Required runtime references could not be restored.");

                return false;
            }
        }

        if (!ValidateTransformScale())
        {
            EnterSafetyShutdown(
                "The spring has an invalid or near-zero transform scale.");

            return false;
        }

        if (!IsFiniteVector(
                transform.position) ||
            !IsFiniteQuaternion(
                transform.rotation))
        {
            EnterSafetyShutdown(
                "The spring Transform contains invalid values.");

            return false;
        }

        if (!float.IsFinite(
                cooldownTimer))
        {
            cooldownTimer =
                0f;

            ChangeState(
                SpringState.Ready);
        }

        return true;
    }

    private bool ValidateRuntimeReferences()
    {
        if (springCollider == null)
            return false;

        if (!springCollider.gameObject.activeInHierarchy)
            return false;

        if (restoreDisabledComponents &&
            !springCollider.enabled)
        {
            springCollider.enabled =
                true;
        }

        if (restoreDisabledComponents &&
            springAnimator != null &&
            !springAnimator.enabled)
        {
            springAnimator.enabled =
                true;
        }

        if (restoreDisabledComponents &&
            audioSource != null &&
            !audioSource.enabled)
        {
            audioSource.enabled =
                true;
        }

        return true;
    }

    private void AttemptRuntimeReferenceRecovery()
    {
        ResolveDependencies();
        ConfigureComponents();
    }

    private bool ValidateTransformScale()
    {
        Vector3 scale =
            transform.lossyScale;

        return
            IsFiniteVector(
                scale) &&
            Mathf.Abs(
                scale.x) >=
                minimumValidScale &&
            Mathf.Abs(
                scale.y) >=
                minimumValidScale &&
            Mathf.Abs(
                scale.z) >=
                minimumValidScale;
    }

    private bool ValidateRigidbody(
        Rigidbody body)
    {
        if (body == null ||
            !body.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!IsFiniteVector(
                body.position) ||
            !IsFiniteQuaternion(
                body.rotation) ||
            !IsFiniteVector(
                body.linearVelocity) ||
            !IsFiniteVector(
                body.angularVelocity))
        {
            return false;
        }

        return true;
    }

    private void EnterSafetyShutdown(
        string reason)
    {
        safetyShutdown =
            true;

        initialized =
            false;

        activationTimes.Clear();
        expiredBodies.Clear();
        teamCandidates.Clear();

        if (springCollider != null)
        {
            springCollider.enabled =
                false;
        }

        ChangeState(
            SpringState.Disabled);

        Debug.LogError(
            $"Spring entered safety shutdown on '{name}': {reason}",
            this);

        enabled =
            false;
    }

    private void RestoreRequiredComponents()
    {
        ResolveDependencies();
        ConfigureComponents();

        if (springCollider != null)
        {
            springCollider.enabled =
                true;
        }

        if (springAnimator != null)
        {
            springAnimator.enabled =
                true;
        }

        if (audioSource != null)
        {
            audioSource.enabled =
                true;
        }
    }

    #endregion

    #region Validation

    private bool ValidateConfiguration()
    {
        if (springCollider == null)
        {
            Debug.LogError(
                $"Spring requires a Collider on '{name}'.",
                this);

            return false;
        }

        if (springType ==
                SpringType.Wide &&
            requireAllThreePads &&
            (leftPad == null ||
             centerPad == null ||
             rightPad == null))
        {
            Debug.LogError(
                $"Wide Spring '{name}' requires Left, Center, and Right pad references.",
                this);

            return false;
        }

        return true;
    }

    #endregion

    #region Helpers

    private Transform FindChildByName(
        string targetName)
    {
        if (string.IsNullOrWhiteSpace(
                targetName))
        {
            return null;
        }

        Transform[] children =
            GetComponentsInChildren<Transform>(
                includeInactive: true);

        foreach (Transform child
                 in children)
        {
            if (child != null &&
                string.Equals(
                    child.name,
                    targetName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return child;
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

    private static bool IsFiniteVector(
        Vector3 value)
    {
        return
            float.IsFinite(
                value.x) &&
            float.IsFinite(
                value.y) &&
            float.IsFinite(
                value.z);
    }

    private static bool IsFiniteQuaternion(
        Quaternion value)
    {
        return
            float.IsFinite(
                value.x) &&
            float.IsFinite(
                value.y) &&
            float.IsFinite(
                value.z) &&
            float.IsFinite(
                value.w);
    }

    private static void DrawPadGizmo(
        Transform pad)
    {
        if (pad == null)
            return;

        Gizmos.DrawWireSphere(
            pad.position,
            0.2f);
    }

    #endregion
}
