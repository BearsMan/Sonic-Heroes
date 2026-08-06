using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class TornadoPole : MonoBehaviour
{
    #region Types

    public enum PoleDirection
    {
        Up,
        Down,
        Bidirectional
    }

    public enum PoleState
    {
        Uninitialized,
        Ready,
        Capturing,
        Spiraling,
        Launching,
        Cooldown,
        Disabled
    }

    #endregion

    #region Constants

    private const string DefaultOrbitCenterName =
        "Orbit Center";

    private const string DefaultExitPointName =
        "Exit Point";

    private const string DefaultLaunchDirectionName =
        "Launch Direction";

    private const string DefaultSwingParameter =
        "Team Swing";

    private const float MinimumDirectionSqrMagnitude =
        0.0001f;

    #endregion

    #region Inspector

    [Header("Pole")]
    [SerializeField]
    private PoleDirection poleDirection =
        PoleDirection.Up;

    [SerializeField]
    private PoleState currentState =
        PoleState.Uninitialized;

    [SerializeField] private bool clockwise;

    [Header("Path References")]
    [SerializeField] private Transform orbitCenter;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private Transform launchDirection;

    [Header("Activation")]
    [SerializeField] private bool requirePlayerTag = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool requireTornadoJump = true;
    [SerializeField] private bool activateAutomatically = true;
    [SerializeField, Min(0f)] private float reuseCooldown = 0.25f;

    [Header("Entry")]
    [SerializeField, Min(0f)] private float entryDuration = 0.12f;
    [SerializeField] private bool alignToPoleOnEntry = true;

    [Header("Spiral")]
    [SerializeField, Min(0.1f)] private float orbitRadius = 1.5f;
    [SerializeField, Min(0f)] private float orbitSpeed = 540f;
    [SerializeField, Min(0.1f)] private float climbSpeed = 7f;
    [SerializeField, Min(0.01f)] private float exitHeightTolerance = 0.05f;
    [SerializeField] private bool faceTravelDirection = true;
    [SerializeField, Min(0f)] private float rotationSharpness = 14f;

    [Header("Launch")]
    [SerializeField, Min(0f)] private float launchSpeed = 22f;
    [SerializeField, Min(0f)] private float upwardBias = 0.25f;
    [SerializeField, Min(0f)] private float controlReturnDelay = 0.15f;
    [SerializeField] private bool restoreOriginalGravity = true;
    [SerializeField] private bool restoreOriginalKinematicState;

    [Header("Animation")]
    [SerializeField] private Animator playerAnimator;
    [SerializeField]
    private string swingBool =
        DefaultSwingParameter;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip captureSound;
    [SerializeField] private AudioClip spiralSound;
    [SerializeField] private AudioClip launchSound;

    [Header("Effects")]
    [SerializeField] private ParticleSystem captureEffect;
    [SerializeField] private ParticleSystem spiralEffect;
    [SerializeField] private ParticleSystem launchEffect;

    [Header("Runtime Safety")]
    [SerializeField] private bool enableRuntimeSafety = true;
    [SerializeField] private bool restoreDisabledComponents = true;
    [SerializeField, Min(0.1f)] private float safetyCheckInterval = 0.5f;
    [SerializeField, Min(0.01f)] private float minimumValidScale = 0.01f;
    [SerializeField, Min(1f)] private float maximumSafeLaunchSpeed = 100f;
    [SerializeField, Min(0.1f)] private float maximumRideDuration = 10f;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private Collider triggerCollider;
    private Coroutine rideRoutine;

    private UltimatePlayerMovement currentMovement;
    private TornadoJump currentTornadoJump;
    private Rigidbody currentRigidbody;
    private Transform currentPlayer;

    private bool originalMovementEnabled;
    private bool originalGravityEnabled;
    private bool originalKinematicState;

    private float cooldownTimer;
    private float safetyTimer;

    private int swingBoolHash;

    private bool initialized;
    private bool occupied;
    private bool shuttingDown;
    private bool applicationQuitting;

    #endregion

    #region Events

    public event Action<TornadoPole, UltimatePlayerMovement> Captured;
    public event Action<TornadoPole, UltimatePlayerMovement> SpiralStarted;
    public event Action<TornadoPole, UltimatePlayerMovement> Launched;
    public event Action<TornadoPole> ResetCompleted;
    public event Action<TornadoPole, PoleState, PoleState> StateChanged;

    #endregion

    #region Public API

    public PoleDirection Direction =>
        poleDirection;

    public PoleState CurrentState =>
        currentState;

    public bool IsInitialized =>
        initialized;

    public bool IsOccupied =>
        occupied;

    public bool TryActivate(
        GameObject player,
        TornadoJump tornadoJump)
    {
        if (player == null ||
            tornadoJump == null)
        {
            return false;
        }

        UltimatePlayerMovement movement =
            player.GetComponent<UltimatePlayerMovement>();

        movement ??=
            player.GetComponentInParent<UltimatePlayerMovement>();

        return TryActivate(
            movement,
            tornadoJump);
    }

    public bool TryActivate(
        UltimatePlayerMovement movement,
        TornadoJump tornadoJump)
    {
        if (!CanActivate() ||
            movement == null ||
            tornadoJump == null)
        {
            return false;
        }

        if (!ResolvePlayerReferences(
                movement,
                tornadoJump))
        {
            return false;
        }

        rideRoutine =
            StartCoroutine(
                RidePoleRoutine());

        return true;
    }

    public bool CancelRide()
    {
        if (!occupied &&
            rideRoutine == null)
        {
            return false;
        }

        CancelRideRoutine();
        RestorePlayerState(
            restorePosition: false);

        FinishCycle();

        return true;
    }

    public bool ResetPole()
    {
        if (shuttingDown ||
            applicationQuitting)
        {
            return false;
        }

        CancelRideRoutine();
        RestorePlayerState(
            restorePosition: false);

        cooldownTimer =
            0f;

        occupied =
            false;

        RestoreRequiredComponents();

        ChangeState(
            PoleState.Ready);

        ResetCompleted?.Invoke(
            this);

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
            PoleState.Disabled)
        {
            ChangeState(
                PoleState.Ready);
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
        if (!activateAutomatically ||
            !initialized ||
            other == null ||
            occupied ||
            shuttingDown ||
            applicationQuitting)
        {
            return;
        }

        UltimatePlayerMovement movement =
            ResolveMovement(
                other);

        if (movement == null)
            return;

        TornadoJump tornadoJump =
            movement.GetComponent<TornadoJump>();

        tornadoJump ??=
            movement.GetComponentInChildren<TornadoJump>(
                includeInactive: true);

        if (requireTornadoJump &&
            tornadoJump == null)
        {
            return;
        }

        if (tornadoJump != null)
        {
            TryActivate(
                movement,
                tornadoJump);
        }
    }

    private void OnDisable()
    {
        CancelRideRoutine();
        RestorePlayerState(
            restorePosition: false);

        if (!shuttingDown &&
            !applicationQuitting)
        {
            ChangeState(
                PoleState.Disabled);
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

        CancelRideRoutine();
        RestorePlayerState(
            restorePosition: false);

        Captured = null;
        SpiralStarted = null;
        Launched = null;
        ResetCompleted = null;
        StateChanged = null;

        triggerCollider = null;
        orbitCenter = null;
        exitPoint = null;
        launchDirection = null;
        playerAnimator = null;
        audioSource = null;
        captureEffect = null;
        spiralEffect = null;
        launchEffect = null;

        ClearPlayerReferences();

        currentState =
            PoleState.Disabled;
    }

    private void OnValidate()
    {
        reuseCooldown =
            Mathf.Max(
                0f,
                reuseCooldown);

        entryDuration =
            Mathf.Max(
                0f,
                entryDuration);

        orbitRadius =
            Mathf.Max(
                0.1f,
                orbitRadius);

        orbitSpeed =
            Mathf.Max(
                0f,
                orbitSpeed);

        climbSpeed =
            Mathf.Max(
                0.1f,
                climbSpeed);

        exitHeightTolerance =
            Mathf.Max(
                0.01f,
                exitHeightTolerance);

        rotationSharpness =
            Mathf.Max(
                0f,
                rotationSharpness);

        launchSpeed =
            Mathf.Max(
                0f,
                launchSpeed);

        upwardBias =
            Mathf.Max(
                0f,
                upwardBias);

        controlReturnDelay =
            Mathf.Max(
                0f,
                controlReturnDelay);

        safetyCheckInterval =
            Mathf.Max(
                0.1f,
                safetyCheckInterval);

        minimumValidScale =
            Mathf.Max(
                0.01f,
                minimumValidScale);

        maximumSafeLaunchSpeed =
            Mathf.Max(
                1f,
                maximumSafeLaunchSpeed);

        maximumRideDuration =
            Mathf.Max(
                0.1f,
                maximumRideDuration);

        CacheAnimatorHash();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ResolveReferences();
            ConfigureComponents();
        }
#endif
    }

    private void OnDrawGizmosSelected()
    {
        Transform center =
            orbitCenter != null
                ? orbitCenter
                : transform;

        Gizmos.DrawWireSphere(
            center.position,
            orbitRadius);

        if (exitPoint != null)
        {
            Gizmos.DrawLine(
                center.position,
                exitPoint.position);

            Gizmos.DrawWireSphere(
                exitPoint.position,
                exitHeightTolerance);
        }

        if (exitPoint != null)
        {
            Gizmos.DrawRay(
                exitPoint.position,
                ResolveLaunchDirection() *
                Mathf.Max(
                    1f,
                    launchSpeed));
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
        CacheAnimatorHash();

        cooldownTimer =
            0f;

        safetyTimer =
            safetyCheckInterval;

        if (!ValidateConfiguration())
        {
            initialized =
                false;

            currentState =
                PoleState.Uninitialized;

            enabled =
                false;

            return false;
        }

        initialized =
            true;

        ChangeState(
            PoleState.Ready);

        return true;
    }

    private void ResolveReferences()
    {
        triggerCollider ??=
            GetComponent<Collider>();

        orbitCenter ??=
            FindDescendantByName(
                DefaultOrbitCenterName);

        orbitCenter ??=
            transform;

        exitPoint ??=
            FindDescendantByName(
                DefaultExitPointName);

        launchDirection ??=
            FindDescendantByName(
                DefaultLaunchDirectionName);

        audioSource ??=
            GetComponent<AudioSource>();

        audioSource ??=
            GetComponentInChildren<AudioSource>(
                includeInactive: true);
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

    private void CacheAnimatorHash()
    {
        swingBoolHash =
            GetAnimatorHash(
                swingBool);
    }

    #endregion

    #region State Machine

    private void ChangeState(
        PoleState newState)
    {
        if (currentState ==
            newState)
        {
            return;
        }

        PoleState previousState =
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

    #endregion

    #region Ride Flow

    private IEnumerator RidePoleRoutine()
    {
        occupied =
            true;

        ChangeState(
            PoleState.Capturing);

        PreparePlayerForRide();

        Captured?.Invoke(
            this,
            currentMovement);

        PlayCapturePresentation();

        yield return EnterOrbit();

        if (!ValidatePlayerReferences())
        {
            CancelRide();
            yield break;
        }

        ChangeState(
            PoleState.Spiraling);

        PlaySpiralPresentation();

        SpiralStarted?.Invoke(
            this,
            currentMovement);

        yield return SpiralToExit();

        if (!ValidatePlayerReferences())
        {
            CancelRide();
            yield break;
        }

        ChangeState(
            PoleState.Launching);

        LaunchPlayer();

        if (controlReturnDelay > 0f)
        {
            yield return new WaitForSeconds(
                controlReturnDelay);
        }

        RestorePlayerControl();

        currentTornadoJump?.FinishPoleAction();

        Launched?.Invoke(
            this,
            currentMovement);

        FinishCycle();
    }

    private IEnumerator EnterOrbit()
    {
        if (!ValidatePlayerReferences())
            yield break;

        Vector3 entryStart =
            currentPlayer.position;

        float startAngle =
            GetCurrentOrbitAngle();

        Vector3 entryTarget =
            GetOrbitPosition(
                startAngle,
                currentPlayer.position.y);

        Quaternion startRotation =
            currentPlayer.rotation;

        Quaternion targetRotation =
            alignToPoleOnEntry
                ? GetOrbitFacingRotation(
                    startAngle)
                : startRotation;

        if (entryDuration <= 0f)
        {
            currentPlayer.SetPositionAndRotation(
                entryTarget,
                targetRotation);

            yield break;
        }

        float elapsed =
            0f;

        while (elapsed <
               entryDuration)
        {
            if (!ValidatePlayerReferences())
                yield break;

            elapsed +=
                Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsed /
                    entryDuration);

            progress =
                SmoothStep(
                    progress);

            currentPlayer.position =
                Vector3.Lerp(
                    entryStart,
                    entryTarget,
                    progress);

            currentPlayer.rotation =
                Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    progress);

            yield return null;
        }

        currentPlayer.SetPositionAndRotation(
            entryTarget,
            targetRotation);
    }

    private IEnumerator SpiralToExit()
    {
        if (!ValidatePlayerReferences() ||
            exitPoint == null)
        {
            yield break;
        }

        float currentAngle =
            GetCurrentOrbitAngle();

        float currentHeight =
            currentPlayer.position.y;

        float targetHeight =
            exitPoint.position.y;

        float rotationDirection =
            clockwise
                ? -1f
                : 1f;

        float elapsedRideTime =
            0f;

        while (Mathf.Abs(
                   currentHeight -
                   targetHeight) >
               exitHeightTolerance)
        {
            if (!ValidatePlayerReferences())
                yield break;

            elapsedRideTime +=
                Time.deltaTime;

            if (elapsedRideTime >
                maximumRideDuration)
            {
                Debug.LogWarning(
                    $"{nameof(TornadoPole)} on '{name}' reached its maximum ride duration.",
                    this);

                break;
            }

            currentAngle +=
                orbitSpeed *
                rotationDirection *
                Time.deltaTime;

            currentHeight =
                Mathf.MoveTowards(
                    currentHeight,
                    targetHeight,
                    climbSpeed *
                    Time.deltaTime);

            currentPlayer.position =
                GetOrbitPosition(
                    currentAngle,
                    currentHeight);

            if (faceTravelDirection)
            {
                Quaternion targetRotation =
                    GetTangentRotation(
                        currentAngle,
                        rotationDirection);

                float rotationAmount =
                    1f -
                    Mathf.Exp(
                        -rotationSharpness *
                        Time.deltaTime);

                currentPlayer.rotation =
                    Quaternion.Slerp(
                        currentPlayer.rotation,
                        targetRotation,
                        rotationAmount);
            }

            yield return null;
        }

        currentPlayer.position =
            exitPoint.position;
    }

    #endregion

    #region Player Control

    private bool ResolvePlayerReferences(
        UltimatePlayerMovement movement,
        TornadoJump tornadoJump)
    {
        if (movement == null ||
            tornadoJump == null ||
            !movement.isActiveAndEnabled ||
            !movement.IsInitialized ||
            movement.IsSafetyShutdown)
        {
            return false;
        }

        currentMovement =
            movement;

        currentTornadoJump =
            tornadoJump;

        currentPlayer =
            movement.transform;

        currentRigidbody =
            movement.GetComponent<Rigidbody>();

        currentRigidbody ??=
            movement.GetComponentInChildren<Rigidbody>(
                includeInactive: true);

        playerAnimator =
            movement.GetComponent<Animator>();

        playerAnimator ??=
            movement.GetComponentInChildren<Animator>(
                includeInactive: true);

        return ValidatePlayerReferences();
    }

    private void PreparePlayerForRide()
    {
        if (!ValidatePlayerReferences())
            return;

        originalMovementEnabled =
            currentMovement.enabled;

        originalGravityEnabled =
            currentRigidbody.useGravity;

        originalKinematicState =
            currentRigidbody.isKinematic;

        currentTornadoJump.TransferToPole();

        currentMovement.DisableMovement();
        currentMovement.enabled =
            false;

        currentRigidbody.linearVelocity =
            Vector3.zero;

        currentRigidbody.angularVelocity =
            Vector3.zero;

        currentRigidbody.useGravity =
            false;

        currentRigidbody.isKinematic =
            true;

        SetSwingAnimation(
            true);
    }

    private void LaunchPlayer()
    {
        if (!ValidatePlayerReferences())
            return;

        SetSwingAnimation(
            false);

        currentRigidbody.isKinematic =
            false;

        currentRigidbody.useGravity =
            restoreOriginalGravity
                ? originalGravityEnabled
                : true;

        currentRigidbody.linearVelocity =
            ResolveLaunchDirection() *
            Mathf.Min(
                launchSpeed,
                maximumSafeLaunchSpeed);

        currentRigidbody.angularVelocity =
            Vector3.zero;

        currentRigidbody.WakeUp();

        PlayLaunchPresentation();
    }

    private void RestorePlayerControl()
    {
        if (currentMovement == null)
            return;

        currentMovement.enabled =
            originalMovementEnabled;

        currentMovement.EnableMovement();

        if (currentRigidbody != null &&
            restoreOriginalKinematicState)
        {
            currentRigidbody.isKinematic =
                originalKinematicState;
        }
    }

    private void RestorePlayerState(
        bool restorePosition)
    {
        if (currentPlayer != null &&
            restorePosition &&
            exitPoint != null)
        {
            currentPlayer.position =
                exitPoint.position;
        }

        SetSwingAnimation(
            false);

        if (currentRigidbody != null)
        {
            currentRigidbody.isKinematic =
                restoreOriginalKinematicState
                    ? originalKinematicState
                    : false;

            currentRigidbody.useGravity =
                restoreOriginalGravity
                    ? originalGravityEnabled
                    : true;

            if (!IsFiniteVector(
                    currentRigidbody.linearVelocity))
            {
                currentRigidbody.linearVelocity =
                    Vector3.zero;
            }

            if (!IsFiniteVector(
                    currentRigidbody.angularVelocity))
            {
                currentRigidbody.angularVelocity =
                    Vector3.zero;
            }
        }

        if (currentMovement != null)
        {
            currentMovement.enabled =
                originalMovementEnabled;

            currentMovement.EnableMovement();
        }

        currentTornadoJump?.FinishPoleAction();

        ClearPlayerReferences();
    }

    private void ClearPlayerReferences()
    {
        currentMovement =
            null;

        currentTornadoJump =
            null;

        currentRigidbody =
            null;

        currentPlayer =
            null;

        playerAnimator =
            null;
    }

    #endregion

    #region Orbit Math

    private float GetCurrentOrbitAngle()
    {
        if (currentPlayer == null ||
            orbitCenter == null)
        {
            return 0f;
        }

        Vector3 offset =
            currentPlayer.position -
            orbitCenter.position;

        offset.y =
            0f;

        if (offset.sqrMagnitude <=
            MinimumDirectionSqrMagnitude)
        {
            return 0f;
        }

        return Mathf.Atan2(
                   offset.z,
                   offset.x) *
               Mathf.Rad2Deg;
    }

    private Vector3 GetOrbitPosition(
        float angleDegrees,
        float height)
    {
        Transform center =
            orbitCenter != null
                ? orbitCenter
                : transform;

        float radians =
            angleDegrees *
            Mathf.Deg2Rad;

        return new Vector3(
            center.position.x +
            Mathf.Cos(
                radians) *
            orbitRadius,

            height,

            center.position.z +
            Mathf.Sin(
                radians) *
            orbitRadius);
    }

    private Quaternion GetOrbitFacingRotation(
        float angleDegrees)
    {
        float direction =
            clockwise
                ? -1f
                : 1f;

        return GetTangentRotation(
            angleDegrees,
            direction);
    }

    private Quaternion GetTangentRotation(
        float angleDegrees,
        float direction)
    {
        float radians =
            angleDegrees *
            Mathf.Deg2Rad;

        Vector3 tangent =
            new Vector3(
                -Mathf.Sin(
                    radians) *
                direction,
                0f,
                Mathf.Cos(
                    radians) *
                direction);

        if (tangent.sqrMagnitude <=
            MinimumDirectionSqrMagnitude)
        {
            tangent =
                transform.forward;
        }

        return Quaternion.LookRotation(
            tangent.normalized,
            Vector3.up);
    }

    private Vector3 ResolveLaunchDirection()
    {
        Transform exit =
            exitPoint != null
                ? exitPoint
                : transform;

        Vector3 direction;

        if (launchDirection != null)
        {
            direction =
                launchDirection.position -
                exit.position;

            if (direction.sqrMagnitude <=
                MinimumDirectionSqrMagnitude)
            {
                direction =
                    launchDirection.forward;
            }
        }
        else
        {
            direction =
                exit.forward;
        }

        direction +=
            Vector3.up *
            upwardBias;

        if (!IsFiniteVector(
                direction) ||
            direction.sqrMagnitude <=
                MinimumDirectionSqrMagnitude)
        {
            direction =
                Vector3.up;
        }

        return direction.normalized;
    }

    #endregion

    #region Activation

    private bool CanActivate()
    {
        return
            initialized &&
            !occupied &&
            rideRoutine == null &&
            !shuttingDown &&
            !applicationQuitting &&
            cooldownTimer <= 0f &&
            currentState ==
                PoleState.Ready &&
            isActiveAndEnabled;
    }

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

        return
            movement != null &&
            movement.isActiveAndEnabled &&
            movement.IsInitialized &&
            !movement.IsSafetyShutdown
                ? movement
                : null;
    }

    #endregion

    #region Presentation

    private void PlayCapturePresentation()
    {
        captureEffect?.Play();

        PlaySound(
            captureSound);
    }

    private void PlaySpiralPresentation()
    {
        spiralEffect?.Play();

        PlaySound(
            spiralSound);
    }

    private void PlayLaunchPresentation()
    {
        if (spiralEffect != null)
        {
            spiralEffect.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting);
        }

        launchEffect?.Play();

        PlaySound(
            launchSound);
    }

    private void SetSwingAnimation(
        bool value)
    {
        if (playerAnimator == null ||
            swingBoolHash == 0)
        {
            return;
        }

        playerAnimator.SetBool(
            swingBoolHash,
            value);
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

    #region Timers

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
            PoleState.Cooldown)
        {
            ChangeState(
                PoleState.Ready);
        }
    }

    private void FinishCycle()
    {
        occupied =
            false;

        cooldownTimer =
            reuseCooldown;

        rideRoutine =
            null;

        ClearPlayerReferences();

        ChangeState(
            cooldownTimer > 0f
                ? PoleState.Cooldown
                : PoleState.Ready);
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
                "Tornado Pole Transform contains invalid values.");

            return false;
        }

        if (restoreDisabledComponents)
        {
            RestoreRequiredComponents();
        }

        if (occupied &&
            !ValidatePlayerReferences())
        {
            CancelRide();
            return false;
        }

        return true;
    }

    private void RestoreRequiredComponents()
    {
        if (triggerCollider != null &&
            !triggerCollider.enabled)
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
    }

    private void EnterSafetyShutdown(
        string reason)
    {
        CancelRideRoutine();
        RestorePlayerState(
            restorePosition: false);

        initialized =
            false;

        occupied =
            false;

        ChangeState(
            PoleState.Disabled);

        if (triggerCollider != null)
        {
            triggerCollider.enabled =
                false;
        }

        Debug.LogError(
            $"{nameof(TornadoPole)} entered safety shutdown on '{name}': {reason}",
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
                $"{nameof(TornadoPole)} on '{name}' requires a Collider, Orbit Center, and Exit Point.",
                this);
        }

        return valid;
    }

    private bool ValidateCoreReferences()
    {
        return
            triggerCollider != null &&
            orbitCenter != null &&
            exitPoint != null;
    }

    private bool ValidatePlayerReferences()
    {
        return
            currentMovement != null &&
            currentTornadoJump != null &&
            currentRigidbody != null &&
            currentPlayer != null &&
            currentMovement.isActiveAndEnabled &&
            ValidateTransform(
                currentPlayer) &&
            IsFiniteVector(
                currentRigidbody.linearVelocity) &&
            IsFiniteVector(
                currentRigidbody.angularVelocity);
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

    private void CancelRideRoutine()
    {
        if (rideRoutine == null)
            return;

        StopCoroutine(
            rideRoutine);

        rideRoutine =
            null;
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
            if (descendant != null &&
                string.Equals(
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
