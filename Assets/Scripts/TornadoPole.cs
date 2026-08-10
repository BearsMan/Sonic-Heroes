using System;
using System.Collections;
using UnityEngine;

public class TornadoPole : MonoBehaviour
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
        Ready,
        Capturing,
        Spiraling,
        Launching,
        Cooldown,
        Disabled
    }

    #endregion

    #region Constants

    private const float MinimumDirectionMagnitude =
        0.0001f;

    private static readonly int TeamSwingHash =
        Animator.StringToHash(
            "Team Swing");

    #endregion

    #region Pole

    [Header("Pole")]

    [SerializeField]
    private PoleDirection poleDirection =
        PoleDirection.Up;

    [SerializeField]
    private bool clockwise;

    [SerializeField]
    private Transform orbitCenter;

    [SerializeField]
    private Transform exitPoint;

    [SerializeField]
    private Transform launchDirection;

    #endregion

    #region Activation

    [Header("Activation")]

    [SerializeField]
    private bool activateAutomatically = true;

    [SerializeField]
    private bool requirePlayerTag = true;

    [SerializeField]
    private string playerTag =
        "Player";

    [SerializeField, Min(0f)]
    private float reuseCooldown =
        0.25f;

    #endregion

    #region Entry

    [Header("Entry")]

    [SerializeField, Min(0f)]
    private float entryDuration =
        0.12f;

    [SerializeField]
    private bool alignToPoleOnEntry = true;

    #endregion

    #region Spiral

    [Header("Spiral")]

    [SerializeField, Min(0.1f)]
    private float orbitRadius =
        1.5f;

    [SerializeField, Min(0f)]
    private float orbitSpeed =
        540f;

    [SerializeField, Min(0.1f)]
    private float climbSpeed =
        7f;

    [SerializeField, Min(0.01f)]
    private float exitHeightTolerance =
        0.05f;

    [SerializeField]
    private bool faceTravelDirection = true;

    [SerializeField, Min(0f)]
    private float rotationSharpness =
        14f;

    [SerializeField, Min(0.1f)]
    private float maximumRideDuration =
        10f;

    #endregion

    #region Launch

    [Header("Launch")]

    [SerializeField, Min(0f)]
    private float launchSpeed =
        22f;

    [SerializeField, Min(0f)]
    private float upwardBias =
        0.25f;

    [SerializeField, Min(0f)]
    private float controlReturnDelay =
        0.15f;

    [SerializeField, Min(1f)]
    private float maximumLaunchSpeed =
        100f;

    #endregion

    #region Presentation

    [Header("Animation")]

    [SerializeField]
    private Animator playerAnimator;

    [Header("Audio")]

    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip captureSound;

    [SerializeField]
    private AudioClip spiralSound;

    [SerializeField]
    private AudioClip launchSound;

    [Header("Effects")]

    [SerializeField]
    private ParticleSystem captureEffect;

    [SerializeField]
    private ParticleSystem spiralEffect;

    [SerializeField]
    private ParticleSystem launchEffect;

    #endregion

    #region Runtime References

    private Collider triggerCollider;

    private UltimatePlayerMovement currentMovement;
    private TornadoJump currentTornadoJump;
    private Rigidbody currentRigidbody;
    private Transform currentPlayer;

    private Coroutine rideRoutine;

    #endregion

    #region Runtime State

    private PoleState currentState =
        PoleState.Ready;

    private bool initialized;
    private bool occupied;
    private bool shuttingDown;

    private bool originalGravity;
    private bool originalKinematic;

    private float cooldownTimer;

    #endregion

    #region Events

    public event Action<
        TornadoPole,
        UltimatePlayerMovement>
        Captured;

    public event Action<
        TornadoPole,
        UltimatePlayerMovement>
        SpiralStarted;

    public event Action<
        TornadoPole,
        UltimatePlayerMovement>
        Launched;

    public event Action<
        TornadoPole,
        PoleState,
        PoleState>
        StateChanged;

    #endregion

    #region Properties

    public PoleDirection Direction =>
        poleDirection;

    public PoleState CurrentState =>
        currentState;

    public bool IsInitialized =>
        initialized;

    public bool IsOccupied =>
        occupied;

    public bool CanAcceptPlayer =>
        initialized &&
        !occupied &&
        !shuttingDown &&
        cooldownTimer <= 0f &&
        currentState ==
            PoleState.Ready &&
        isActiveAndEnabled;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (!initialized &&
            !shuttingDown)
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
            shuttingDown)
        {
            return;
        }

        UpdateCooldown();
    }

    private void OnTriggerEnter(
        Collider other)
    {
        if (!activateAutomatically ||
            other == null ||
            !CanAcceptPlayer)
        {
            return;
        }

        UltimatePlayerMovement movement =
            ResolveMovement(
                other);

        if (movement == null)
        {
            return;
        }

        TornadoJump tornadoJump =
            ResolveTornadoJump(
                movement);

        if (tornadoJump == null)
        {
            return;
        }

        TryActivate(
            movement,
            tornadoJump);
    }

    private void OnDisable()
    {
        if (shuttingDown)
        {
            return;
        }

        CancelRide();

        ChangeState(
            PoleState.Disabled);
    }

    private void OnDestroy()
    {
        shuttingDown =
            true;

        CancelRideRoutine();
        RestorePlayerState();

        Captured =
            null;

        SpiralStarted =
            null;

        Launched =
            null;

        StateChanged =
            null;

        initialized =
            false;
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

        maximumRideDuration =
            Mathf.Max(
                0.1f,
                maximumRideDuration);

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

        maximumLaunchSpeed =
            Mathf.Max(
                1f,
                maximumLaunchSpeed);
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
        {
            return true;
        }

        if (shuttingDown)
        {
            return false;
        }

        ResolveReferences();

        if (!ValidateConfiguration())
        {
            initialized =
                false;

            return false;
        }

        triggerCollider.isTrigger =
            true;

        if (audioSource != null)
        {
            audioSource.playOnAwake =
                false;
        }

        occupied =
            false;

        cooldownTimer =
            0f;

        currentState =
            PoleState.Ready;

        initialized =
            true;

        return true;
    }

    private void ResolveReferences()
    {
        triggerCollider ??=
            GetComponent<Collider>();

        orbitCenter ??=
            transform;

        audioSource ??=
            GetComponent<AudioSource>();

        audioSource ??=
            GetComponentInChildren<AudioSource>(
                includeInactive: true);
    }

    private bool ValidateConfiguration()
    {
        return
            triggerCollider != null &&
            orbitCenter != null &&
            exitPoint != null &&
            ValidateTransform(
                orbitCenter) &&
            ValidateTransform(
                exitPoint);
    }

    #endregion

    #region Public API

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
            player.GetComponent<
                UltimatePlayerMovement>();

        movement ??=
            player.GetComponentInParent<
                UltimatePlayerMovement>();

        movement ??=
            player.GetComponentInChildren<
                UltimatePlayerMovement>(
                    includeInactive: true);

        return
            TryActivate(
                movement,
                tornadoJump);
    }

    public bool TryActivate(
        UltimatePlayerMovement movement,
        TornadoJump tornadoJump)
    {
        if (!CanAcceptPlayer ||
            movement == null ||
            tornadoJump == null)
        {
            return false;
        }

        if (!PrepareReferences(
                movement,
                tornadoJump))
        {
            return false;
        }

        if (!currentTornadoJump
                .BeginPoleAction(
                    this))
        {
            ClearPlayerReferences();

            return false;
        }

        occupied =
            true;

        rideRoutine =
            StartCoroutine(
                RideRoutine());

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
        RestorePlayerState();

        occupied =
            false;

        BeginCooldown();

        return true;
    }

    public bool ResetPole()
    {
        if (shuttingDown)
        {
            return false;
        }

        CancelRideRoutine();
        RestorePlayerState();

        cooldownTimer =
            0f;

        occupied =
            false;

        ChangeState(
            PoleState.Ready);

        return true;
    }

    #endregion

    #region Ride Flow

    private IEnumerator RideRoutine()
    {
        ChangeState(
            PoleState.Capturing);

        if (!PreparePlayerForRide())
        {
            FinishFailedRide();

            yield break;
        }

        Captured?.Invoke(
            this,
            currentMovement);

        PlayCapturePresentation();

        yield return
            EnterOrbit();

        if (!HasValidPlayer())
        {
            FinishFailedRide();

            yield break;
        }

        ChangeState(
            PoleState.Spiraling);

        SpiralStarted?.Invoke(
            this,
            currentMovement);

        PlaySpiralPresentation();

        yield return
            SpiralToExit();

        if (!HasValidPlayer())
        {
            FinishFailedRide();

            yield break;
        }

        ChangeState(
            PoleState.Launching);

        LaunchPlayer();

        Launched?.Invoke(
            this,
            currentMovement);

        if (controlReturnDelay > 0f)
        {
            yield return
                new WaitForSeconds(
                    controlReturnDelay);
        }

        RestorePlayerControl();

        currentTornadoJump?
            .FinishPoleAction();

        ClearPlayerReferences();

        occupied =
            false;

        rideRoutine =
            null;

        BeginCooldown();
    }

    private IEnumerator EnterOrbit()
    {
        if (!HasValidPlayer())
        {
            yield break;
        }

        Vector3 startingPosition =
            currentPlayer.position;

        Quaternion startingRotation =
            currentPlayer.rotation;

        float angle =
            GetOrbitAngle();

        Vector3 destination =
            GetOrbitPosition(
                angle,
                currentPlayer.position.y);

        Quaternion destinationRotation =
            alignToPoleOnEntry
                ? GetTangentRotation(
                    angle)
                : startingRotation;

        if (entryDuration <= 0f)
        {
            currentPlayer
                .SetPositionAndRotation(
                    destination,
                    destinationRotation);

            yield break;
        }

        float elapsed =
            0f;

        while (elapsed <
            entryDuration)
        {
            if (!HasValidPlayer())
            {
                yield break;
            }

            elapsed +=
                Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsed /
                    entryDuration);

            progress =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    progress);

            currentPlayer.position =
                Vector3.Lerp(
                    startingPosition,
                    destination,
                    progress);

            currentPlayer.rotation =
                Quaternion.Slerp(
                    startingRotation,
                    destinationRotation,
                    progress);

            yield return null;
        }

        currentPlayer
            .SetPositionAndRotation(
                destination,
                destinationRotation);
    }

    private IEnumerator SpiralToExit()
    {
        if (!HasValidPlayer() ||
            exitPoint == null)
        {
            yield break;
        }

        float angle =
            GetOrbitAngle();

        float height =
            currentPlayer.position.y;

        float targetHeight =
            exitPoint.position.y;

        float direction =
            GetSpiralDirection(
                height,
                targetHeight);

        float elapsed =
            0f;

        while (Mathf.Abs(
                height -
                targetHeight) >
            exitHeightTolerance)
        {
            if (!HasValidPlayer())
            {
                yield break;
            }

            elapsed +=
                Time.deltaTime;

            if (elapsed >=
                maximumRideDuration)
            {
                break;
            }

            angle +=
                orbitSpeed *
                (clockwise
                    ? -1f
                    : 1f) *
                Time.deltaTime;

            height =
                Mathf.MoveTowards(
                    height,
                    targetHeight,
                    climbSpeed *
                    Time.deltaTime);

            currentPlayer.position =
                GetOrbitPosition(
                    angle,
                    height);

            if (faceTravelDirection)
            {
                Quaternion targetRotation =
                    GetTangentRotation(
                        angle);

                float interpolation =
                    rotationSharpness <= 0f
                        ? 1f
                        : 1f -
                          Mathf.Exp(
                            -rotationSharpness *
                            Time.deltaTime);

                currentPlayer.rotation =
                    Quaternion.Slerp(
                        currentPlayer.rotation,
                        targetRotation,
                        interpolation);
            }

            if (direction == 0f)
            {
                break;
            }

            yield return null;
        }

        if (currentPlayer != null &&
            exitPoint != null)
        {
            currentPlayer.position =
                exitPoint.position;
        }
    }

    #endregion

    #region Player Control

    private bool PrepareReferences(
        UltimatePlayerMovement movement,
        TornadoJump tornadoJump)
    {
        if (movement == null ||
            tornadoJump == null ||
            !movement.isActiveAndEnabled ||
            !movement.gameObject
                .activeInHierarchy ||
            !tornadoJump.isActiveAndEnabled ||
            !tornadoJump.CanUsePole)
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
            ResolveRigidbody(
                movement);

        if (currentRigidbody == null)
        {
            ClearPlayerReferences();

            return false;
        }

        playerAnimator =
            movement.GetComponent<
                Animator>();

        playerAnimator ??=
            movement
                .GetComponentInChildren<
                    Animator>(
                        includeInactive: true);

        return
            HasValidPlayer();
    }

    private bool PreparePlayerForRide()
    {
        if (!HasValidPlayer() ||
            currentTornadoJump == null ||
            !currentTornadoJump
                .TransferToPole())
        {
            return false;
        }

        originalGravity =
            currentRigidbody.useGravity;

        originalKinematic =
            currentRigidbody.isKinematic;

        currentMovement
            .DisableMovement();

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

        return true;
    }

    private void LaunchPlayer()
    {
        if (!HasValidPlayer())
        {
            return;
        }

        SetSwingAnimation(
            false);

        currentRigidbody.isKinematic =
            false;

        currentRigidbody.useGravity =
            true;

        Vector3 launchVelocity =
            ResolveLaunchDirection() *
            Mathf.Min(
                launchSpeed,
                maximumLaunchSpeed);

        if (!IsFiniteVector(
                launchVelocity))
        {
            launchVelocity =
                Vector3.up *
                Mathf.Min(
                    launchSpeed,
                    maximumLaunchSpeed);
        }

        currentRigidbody.linearVelocity =
            launchVelocity;

        currentRigidbody.angularVelocity =
            Vector3.zero;

        currentRigidbody.WakeUp();

        PlayLaunchPresentation();
    }

    private void RestorePlayerControl()
    {
        if (currentMovement != null)
        {
            currentMovement
                .EnableMovement();
        }

        if (currentRigidbody != null)
        {
            currentRigidbody.isKinematic =
                false;

            currentRigidbody.useGravity =
                true;
        }
    }

    private void RestorePlayerState()
    {
        SetSwingAnimation(
            false);

        if (currentRigidbody != null)
        {
            currentRigidbody.isKinematic =
                originalKinematic;

            currentRigidbody.useGravity =
                originalGravity;

            if (!IsFiniteVector(
                    currentRigidbody
                        .linearVelocity))
            {
                currentRigidbody.linearVelocity =
                    Vector3.zero;
            }

            if (!IsFiniteVector(
                    currentRigidbody
                        .angularVelocity))
            {
                currentRigidbody.angularVelocity =
                    Vector3.zero;
            }
        }

        if (currentMovement != null)
        {
            currentMovement
                .EnableMovement();
        }

        currentTornadoJump?
            .CancelPoleAction();

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

    #region Player Resolution

    private UltimatePlayerMovement ResolveMovement(
        Collider other)
    {
        if (other == null)
        {
            return null;
        }

        if (requirePlayerTag &&
            !HasTagInHierarchy(
                other.transform,
                playerTag))
        {
            return null;
        }

        UltimatePlayerMovement movement =
            other.GetComponent<
                UltimatePlayerMovement>();

        movement ??=
            other.GetComponentInParent<
                UltimatePlayerMovement>();

        movement ??=
            other.GetComponentInChildren<
                UltimatePlayerMovement>(
                    includeInactive: true);

        if (movement == null ||
            !movement.isActiveAndEnabled ||
            !movement.gameObject
                .activeInHierarchy)
        {
            return null;
        }

        return movement;
    }

    private static TornadoJump
        ResolveTornadoJump(
            UltimatePlayerMovement movement)
    {
        if (movement == null)
        {
            return null;
        }

        TornadoJump tornadoJump =
            movement.GetComponent<
                TornadoJump>();

        tornadoJump ??=
            movement
                .GetComponentInParent<
                    TornadoJump>();

        tornadoJump ??=
            movement
                .GetComponentInChildren<
                    TornadoJump>(
                        includeInactive: true);

        return tornadoJump;
    }

    private static Rigidbody ResolveRigidbody(
        UltimatePlayerMovement movement)
    {
        if (movement == null)
        {
            return null;
        }

        if (movement.body != null)
        {
            return
                movement.body;
        }

        Rigidbody body =
            movement.GetComponent<
                Rigidbody>();

        body ??=
            movement
                .GetComponentInParent<
                    Rigidbody>();

        body ??=
            movement
                .GetComponentInChildren<
                    Rigidbody>(
                        includeInactive: true);

        return body;
    }

    #endregion

    #region Orbit

    private float GetOrbitAngle()
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

        if (!IsFiniteVector(
                offset) ||
            offset.sqrMagnitude <=
                MinimumDirectionMagnitude)
        {
            return 0f;
        }

        return
            Mathf.Atan2(
                offset.z,
                offset.x) *
            Mathf.Rad2Deg;
    }

    private Vector3 GetOrbitPosition(
        float angle,
        float height)
    {
        Transform center =
            orbitCenter != null
                ? orbitCenter
                : transform;

        float radians =
            angle *
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

    private Quaternion GetTangentRotation(
        float angle)
    {
        float radians =
            angle *
            Mathf.Deg2Rad;

        float direction =
            clockwise
                ? -1f
                : 1f;

        Vector3 tangent =
            new Vector3(
                -Mathf.Sin(
                    radians) *
                direction,
                0f,
                Mathf.Cos(
                    radians) *
                direction);

        if (!IsFiniteVector(
                tangent) ||
            tangent.sqrMagnitude <=
                MinimumDirectionMagnitude)
        {
            tangent =
                transform.forward;
        }

        return
            Quaternion.LookRotation(
                tangent.normalized,
                Vector3.up);
    }

    private float GetSpiralDirection(
        float currentHeight,
        float targetHeight)
    {
        return poleDirection switch
        {
            PoleDirection.Up =>
                targetHeight >=
                currentHeight
                    ? 1f
                    : 0f,

            PoleDirection.Down =>
                targetHeight <=
                currentHeight
                    ? -1f
                    : 0f,

            _ =>
                Mathf.Sign(
                    targetHeight -
                    currentHeight)
        };
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
                MinimumDirectionMagnitude)
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
                MinimumDirectionMagnitude)
        {
            direction =
                Vector3.up;
        }

        return
            direction.normalized;
    }

    #endregion

    #region State

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
            currentState);
    }

    private void BeginCooldown()
    {
        cooldownTimer =
            reuseCooldown;

        ChangeState(
            cooldownTimer > 0f
                ? PoleState.Cooldown
                : PoleState.Ready);
    }

    private void UpdateCooldown()
    {
        if (cooldownTimer <= 0f)
        {
            return;
        }

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

    private void FinishFailedRide()
    {
        CancelRideRoutine();
        RestorePlayerState();

        occupied =
            false;

        BeginCooldown();
    }

    private void CancelRideRoutine()
    {
        if (rideRoutine == null)
        {
            return;
        }

        StopCoroutine(
            rideRoutine);

        rideRoutine =
            null;
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
                ParticleSystemStopBehavior
                    .StopEmitting);
        }

        launchEffect?.Play();

        PlaySound(
            launchSound);
    }

    private void SetSwingAnimation(
        bool value)
    {
        if (playerAnimator == null ||
            !playerAnimator
                .isActiveAndEnabled ||
            playerAnimator
                .runtimeAnimatorController ==
                null)
        {
            return;
        }

        if (!HasAnimatorParameter(
                playerAnimator,
                TeamSwingHash,
                AnimatorControllerParameterType
                    .Bool))
        {
            return;
        }

        playerAnimator.SetBool(
            TeamSwingHash,
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

    private static bool HasAnimatorParameter(
        Animator animator,
        int hash,
        AnimatorControllerParameterType type)
    {
        if (animator == null)
        {
            return false;
        }

        foreach (
            AnimatorControllerParameter parameter
            in animator.parameters)
        {
            if (parameter.nameHash ==
                    hash &&
                parameter.type ==
                    type)
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Validation

    private bool HasValidPlayer()
    {
        return
            currentMovement != null &&
            currentTornadoJump != null &&
            currentRigidbody != null &&
            currentPlayer != null &&
            currentMovement
                .gameObject
                .activeInHierarchy &&
            ValidateTransform(
                currentPlayer) &&
            IsFiniteVector(
                currentRigidbody
                    .linearVelocity) &&
            IsFiniteVector(
                currentRigidbody
                    .angularVelocity);
    }

    private static bool ValidateTransform(
        Transform target)
    {
        if (target == null)
        {
            return false;
        }

        Vector3 scale =
            target.lossyScale;

        return
            IsFiniteVector(
                target.position) &&
            IsFiniteQuaternion(
                target.rotation) &&
            IsFiniteVector(
                scale) &&
            Mathf.Abs(scale.x) >
                0.0001f &&
            Mathf.Abs(scale.y) >
                0.0001f &&
            Mathf.Abs(scale.z) >
                0.0001f;
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

    #region Helpers

    private static bool HasTagInHierarchy(
        Transform source,
        string requiredTag)
    {
        if (source == null)
        {
            return false;
        }

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

    #endregion
}