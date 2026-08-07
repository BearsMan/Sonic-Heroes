using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(UltimatePlayerMovement))]
[RequireComponent(typeof(AudioSource))]
public sealed class RailGrinding : MonoBehaviour
{
    #region Types

    public enum TeamType
    {
        Speed,
        Fly,
        Power
    }

    #endregion

    #region Animator Hashes
    private static readonly int IsGrindingHash =
        Animator.StringToHash("IsGrinding");

    private static readonly int IsCrouchingHash =
        Animator.StringToHash("IsCrouching");

    private static readonly int GrindSpeedHash =
        Animator.StringToHash("GrindSpeed");

    private const int RailSwitchBufferSize = 16;
    private const float MinimumVectorMagnitude = 0.000001f;
    #endregion

    #region Inspector

    [Header("References")]
    [SerializeField] private UltimatePlayerMovement playerMovement;
    [SerializeField] private Animator railAnimator;
    [SerializeField] private Transform member2;
    [SerializeField] private Transform member3;

    [Header("Grind Speed")]
    [SerializeField, Min(0f)] private float baseGrindSpeed = 18f;
    [SerializeField, Min(0f)] private float maxGrindSpeed = 40f;
    [SerializeField, Min(0f)] private float minGrindSpeed = 2f;
    [SerializeField, Min(0f)] private float slopeAcceleration = 12f;
    [SerializeField, Min(0f)] private float crouchSpeedMultiplier = 1.25f;

    [Header("Team Speed Modifiers")]
    [SerializeField, Min(0f)] private float speedTeamModifier = 1f;
    [SerializeField, Min(0f)] private float flyTeamModifier = 0.85f;
    [SerializeField, Min(0f)] private float powerTeamModifier = 0.75f;

    [Header("Team Formation")]
    [SerializeField, Min(0f)] private float memberSpacing = 1.2f;
    [SerializeField, Min(0f)] private float memberSnapSpeed = 14f;

    [Header("Rail Detection")]
    [SerializeField] private LayerMask railLayerMask;
    [SerializeField, Min(0f)] private float grindSnapRadius = 1.2f;
    [SerializeField, Min(0f)] private float railDetectionRadius = 1.2f;
    [SerializeField, Min(0f)] private float railRayDistance = 1.75f;
    [SerializeField]
    private Vector3 railDetectionOffset =
        new(0f, -0.25f, 0f);

    [Header("Rail Movement")]
    [SerializeField, Min(0f)] private float snapSpeed = 20f;

    [Header("Rail Switching")]
    [SerializeField, Min(0f)] private float switchScanRadius = 4f;
    [SerializeField, Range(0f, 180f)] private float switchMaxAngle = 45f;
    [SerializeField, Min(0f)] private float switchCooldown = 0.4f;
    [SerializeField, Range(0f, 1f)] private float switchInputThreshold = 0.5f;

    [Header("Jump Off Rail")]
    [SerializeField] private KeyCode railJumpKey = KeyCode.Space;
    [SerializeField, Min(0f)] private float railJumpForce = 12f;
    [SerializeField, Min(0f)] private float railJumpForwardForce = 6f;

    [Header("Crouching")]
    [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;

    [Header("Audio")]
    [SerializeField] private AudioClip grindLoopSFX;
    [SerializeField] private AudioClip grindStartSFX;
    [SerializeField] private AudioClip grindEndSFX;
    [SerializeField] private AudioClip railSwitchSFX;

    [Header("Particle Effects")]
    [SerializeField] private ParticleSystem leaderSparksFX;
    [SerializeField] private ParticleSystem member2SparksFX;
    [SerializeField] private ParticleSystem member3SparksFX;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    [SerializeField] private TeamType currentTeam = TeamType.Speed;
    [SerializeField] private bool isGrinding;
    [SerializeField] private bool isCrouching;
    [SerializeField] private float currentGrindSpeed;
    private readonly Collider[] railSwitchBuffer =
    new Collider[RailSwitchBufferSize];
    [SerializeField, Range(0f, 1f)] private float splineT;

    private RailSpline currentRail;
    private float currentRailLength;
    private float grindSnapRadiusSqr;
    private float switchScanRadiusSqr;
    private float currentTeamModifier = 1f;
    private Rigidbody playerRigidbody;
    private AudioSource audioSource;

    private float grindDirectionSign = 1f;
    private float lastSwitchTime = float.NegativeInfinity;

    private bool isInitialized;
    private bool isShuttingDown;

    #endregion

    #region Public API

    public TeamType CurrentTeam => currentTeam;
    public bool IsCrouching => isCrouching;
    public float CurrentGrindSpeed => currentGrindSpeed;
    public float SplineT => splineT;
    public RailSpline CurrentRail => currentRail;
    public bool IsInitialized => isInitialized;

    public bool IsGrinding
    {
        get
        {
            return isGrinding;
        }
    }

    public void SetTeamType(
    TeamType teamType)
    {
        currentTeam = teamType;

        currentTeamModifier =
            GetTeamModifier();
    }

    public bool TrySnapToRail(
    RailSpline rail,
    float contactT)
    {
        if (!isInitialized ||
            isGrinding ||
            rail == null ||
            playerRigidbody == null)
        {
            return false;
        }

        if (!float.IsFinite(contactT))
        {
            return false;
        }

        Vector3 playerPosition =
            playerRigidbody.position;

        if (!IsFiniteVector(
                playerPosition))
        {
            return false;
        }

        float clampedT =
            Mathf.Clamp01(contactT);

        Vector3 railPoint =
            rail.GetPoint(
                clampedT);

        if (!IsFiniteVector(
                railPoint))
        {
            return false;
        }

        float snapDistanceSqr =
            (railPoint -
             playerPosition)
            .sqrMagnitude;

        if (!float.IsFinite(
        snapDistanceSqr) ||
    snapDistanceSqr >
    grindSnapRadiusSqr)
        {
            return false;
        }

        float railLength =
            rail.ApproximateLength();

        if (!float.IsFinite(
                railLength) ||
            railLength <= Mathf.Epsilon)
        {
            return false;
        }

        currentRail = rail;
        splineT = clampedT;
        currentRailLength = railLength;

        DetermineGrindingDirection();
        CalculateStartingSpeed();

        if (!IsValidGrindingRuntimeState())
        {
            currentRail = null;
            currentRailLength = 0f;
            currentGrindSpeed = 0f;
            splineT = 0f;

            return false;
        }

        StartGrinding();

        return isGrinding;
    }

    public void ForceStopGrinding()
    {
        StopGrinding(
            jumped: false);
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheComponents();
        ResolveReferences();
    }

    private void Start()
    {
        if (!InitializeRailGrinding())
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

        RestoreRuntimeState();
    }

    private void Update()
    {
        if (!isInitialized ||
            !isGrinding)
        {
            return;
        }

        HandleGrindingInput();
        UpdateAnimatorState();
    }

    private void FixedUpdate()
    {
        if (!isInitialized)
            return;

        if (!isGrinding)
        {
            CheckForNearbyRail();
            return;
        }

        AdvanceAlongRail();

        if (!isGrinding)
            return;

        AlignLeaderToRail();
        PositionTeammates();
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

    private void OnTriggerEnter(
        Collider other)
    {
        TryAttachFromCollider(other);
    }

    private void OnTriggerStay(
        Collider other)
    {
        TryAttachFromCollider(other);
    }

    private void OnValidate()
    {
        baseGrindSpeed =
            Mathf.Max(
                0f,
                baseGrindSpeed);

        minGrindSpeed =
            Mathf.Max(
                0f,
                minGrindSpeed);

        maxGrindSpeed =
            Mathf.Max(
                minGrindSpeed,
                maxGrindSpeed);

        baseGrindSpeed =
            Mathf.Clamp(
                baseGrindSpeed,
                minGrindSpeed,
                maxGrindSpeed);

        slopeAcceleration =
            Mathf.Max(
                0f,
                slopeAcceleration);

        crouchSpeedMultiplier =
            Mathf.Max(
                0f,
                crouchSpeedMultiplier);

        speedTeamModifier =
            Mathf.Max(
                0f,
                speedTeamModifier);

        flyTeamModifier =
            Mathf.Max(
                0f,
                flyTeamModifier);

        powerTeamModifier =
            Mathf.Max(
                0f,
                powerTeamModifier);

        memberSpacing =
            Mathf.Max(
                0f,
                memberSpacing);

        memberSnapSpeed =
            Mathf.Max(
                0f,
                memberSnapSpeed);

        grindSnapRadius =
            Mathf.Max(
                0f,
                grindSnapRadius);

        railDetectionRadius =
            Mathf.Max(
                0f,
                railDetectionRadius);

        railRayDistance =
            Mathf.Max(
                0f,
                railRayDistance);

        snapSpeed =
            Mathf.Max(
                0f,
                snapSpeed);

        switchScanRadius =
            Mathf.Max(
                0f,
                switchScanRadius);

        switchMaxAngle =
            Mathf.Clamp(
                switchMaxAngle,
                0f,
                180f);

        switchCooldown =
            Mathf.Max(
                0f,
                switchCooldown);

        switchInputThreshold =
            Mathf.Clamp01(
                switchInputThreshold);

        railJumpForce =
            Mathf.Max(
                0f,
                railJumpForce);

        railJumpForwardForce =
            Mathf.Max(
                0f,
                railJumpForwardForce);

        splineT =
            Mathf.Clamp01(
                splineT);

        RefreshCachedValues();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 detectionOrigin =
            transform.position +
            railDetectionOffset;

        Gizmos.DrawWireSphere(
            detectionOrigin,
            railDetectionRadius);

        Gizmos.DrawLine(
            detectionOrigin,
            detectionOrigin +
            Vector3.down *
            railRayDistance);

        Gizmos.DrawWireSphere(
            transform.position,
            switchScanRadius);
    }

    #endregion

    #region Initialization

    public bool InitializeRailGrinding()
    {
        if (isInitialized)
            return true;

        CacheComponents();
        ResolveReferences();

        if (!ValidateConfiguration())
        {
            Debug.LogError(
                $"RailGrinding failed to initialize on '{name}'.",
                this);

            isInitialized = false;
            return false;
        }

        ResetRuntimeState();
        RefreshCachedValues();

        currentTeamModifier =
            GetTeamModifier();

        StopAllSparksFX();
        UpdateAnimatorState();

        isInitialized = true;

        return true;
    }

    private void CacheComponents()
    {
        playerRigidbody ??=
            GetComponent<Rigidbody>();

        audioSource ??=
            GetComponent<AudioSource>();

        playerMovement ??=
            GetComponent<UltimatePlayerMovement>();
    }

    private void ResolveReferences()
    {
        railAnimator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);
    }

    private void RefreshCachedValues()
    {
        grindSnapRadiusSqr =
            grindSnapRadius *
            grindSnapRadius;

        switchScanRadiusSqr =
            switchScanRadius *
            switchScanRadius;
    }

    private void RestoreRuntimeState()
    {
        if (!isGrinding)
        {
            StopGrindLoop();
            StopAllSparksFX();
        }

        UpdateAnimatorState();
    }

    #endregion

    #region Rail Detection

    private void CheckForNearbyRail()
    {
        if (playerRigidbody == null ||
            railLayerMask.value == 0)
        {
            return;
        }

        Vector3 detectionOrigin =
            playerRigidbody.position +
            railDetectionOffset;

        if (TryAttachFromRaycast(
                detectionOrigin))
        {
            return;
        }

        TryAttachFromOverlap(
            detectionOrigin);
    }

    private bool TryAttachFromRaycast(
        Vector3 detectionOrigin)
    {
        if (!Physics.Raycast(
                detectionOrigin,
                Vector3.down,
                out RaycastHit hit,
                railRayDistance,
                railLayerMask,
                QueryTriggerInteraction.Collide))
        {
            return false;
        }

        RailSpline rail =
            hit.collider.GetComponentInParent<RailSpline>();

        return TryAttachToRail(rail);
    }

    private void TryAttachFromOverlap(
        Vector3 detectionOrigin)
    {
        Collider[] nearbyColliders =
            Physics.OverlapSphere(
                detectionOrigin,
                railDetectionRadius,
                railLayerMask,
                QueryTriggerInteraction.Collide);

        RailSpline closestRail = null;
        float closestT = 0f;
        float closestDistanceSqr = float.MaxValue;

        foreach (Collider nearbyCollider in nearbyColliders)
        {
            if (nearbyCollider == null)
                continue;

            RailSpline candidate =
                nearbyCollider.GetComponentInParent<RailSpline>();

            if (candidate == null)
                continue;

            float candidateT =
                candidate.GetClosestT(
                    playerRigidbody.position);

            Vector3 candidatePoint =
                candidate.GetPoint(
                    candidateT);

            float distanceSqr =
                (candidatePoint -
                 playerRigidbody.position)
                .sqrMagnitude;

            if (distanceSqr >= closestDistanceSqr)
                continue;

            closestDistanceSqr = distanceSqr;
            closestRail = candidate;
            closestT = candidateT;
        }

        if (closestRail == null ||
    closestDistanceSqr >
    grindSnapRadiusSqr)
        {
            return;
        }

        TrySnapToRail(
            closestRail,
            closestT);
    }

    private void TryAttachFromCollider(
        Collider other)
    {
        if (!isInitialized ||
            isGrinding ||
            other == null)
        {
            return;
        }

        bool validLayer =
            IsLayerInMask(
                other.gameObject.layer,
                railLayerMask);

        if (!validLayer &&
            !other.CompareTag("Rail"))
        {
            return;
        }

        RailSpline rail =
            other.GetComponentInParent<RailSpline>();

        TryAttachToRail(rail);
    }

    private bool TryAttachToRail(
        RailSpline rail)
    {
        if (rail == null ||
            playerRigidbody == null)
        {
            return false;
        }

        float closestT =
            rail.GetClosestT(
                playerRigidbody.position);

        return TrySnapToRail(
            rail,
            closestT);
    }

    private static bool IsLayerInMask(
        int layer,
        LayerMask layerMask)
    {
        return
            (layerMask.value &
             (1 << layer)) != 0;
    }

    #endregion

    #region Grinding State

    private void StartGrinding()
    {
        if (currentRail == null ||
            playerRigidbody == null)
        {
            return;
        }

        isGrinding = true;
        isCrouching = false;

        playerMovement?.EnterGrindingState();

        playerRigidbody.useGravity = false;
        playerRigidbody.linearVelocity = Vector3.zero;
        playerRigidbody.angularVelocity = Vector3.zero;

        PlayOneShot(grindStartSFX);
        StartGrindLoop();
        PlayAllSparksFX();
        UpdateAnimatorState();

        LogStateChange(
            $"Started grinding on '{currentRail.name}'.");
    }

    private void StopGrinding(
        bool jumped)
    {
        if (!isGrinding)
            return;

        Vector3 exitDirection =
            GetTravelDirection();

        isGrinding = false;
        isCrouching = false;

        if (playerRigidbody != null)
        {
            playerRigidbody.useGravity = true;
            playerRigidbody.WakeUp();

            float forwardSpeed =
                jumped
                    ? currentGrindSpeed +
                      railJumpForwardForce
                    : currentGrindSpeed;

            Vector3 exitVelocity =
                exitDirection *
                forwardSpeed;

            if (jumped)
            {
                exitVelocity +=
                    Vector3.up *
                    railJumpForce;
            }

            playerRigidbody.linearVelocity =
                exitVelocity;
        }

        playerMovement?.ExitGrindingState();

        currentRail = null;
        currentRailLength = 0f;

        StopGrindLoop();
        PlayOneShot(grindEndSFX);
        StopAllSparksFX();
        UpdateAnimatorState();

        LogStateChange(
            jumped
                ? "Jumped off rail."
                : "Stopped grinding.");
    }

    private void ResetRuntimeState()
    {
        isGrinding = false;
        isCrouching = false;
        currentRail = null;
        currentRailLength = 0f;
        currentGrindSpeed = 0f;
        splineT = 0f;
        grindDirectionSign = 1f;
        lastSwitchTime = float.NegativeInfinity;
    }

    #endregion

    #region Rail Movement

    private void DetermineGrindingDirection()
    {
        grindDirectionSign = 1f;

        if (currentRail == null ||
            playerRigidbody == null)
        {
            return;
        }

        if (!float.IsFinite(splineT))
        {
            splineT = 0f;
            return;
        }

        Vector3 railTangent =
            currentRail.GetTangent(
                splineT);

        if (!IsFiniteVector(
                railTangent) ||
            railTangent.sqrMagnitude <=
                MinimumVectorMagnitude)
        {
            return;
        }

        railTangent.Normalize();

        Vector3 velocity =
            playerRigidbody.linearVelocity;

        if (IsFiniteVector(
                velocity))
        {
            float velocityDot =
                Vector3.Dot(
                    velocity,
                    railTangent);

            if (float.IsFinite(
                    velocityDot) &&
                Mathf.Abs(velocityDot) >
                    0.1f)
            {
                grindDirectionSign =
                    velocityDot >= 0f
                        ? 1f
                        : -1f;

                return;
            }
        }

        Vector3 forward =
            transform.forward;

        if (!IsFiniteVector(
                forward) ||
            forward.sqrMagnitude <=
                MinimumVectorMagnitude)
        {
            return;
        }

        float facingDot =
            Vector3.Dot(
                forward.normalized,
                railTangent);

        if (!float.IsFinite(
                facingDot))
        {
            return;
        }

        grindDirectionSign =
            facingDot >= 0f
                ? 1f
                : -1f;
    }

    private void CalculateStartingSpeed()
    {
        float teamModifier =
            float.IsFinite(currentTeamModifier)
                ? Mathf.Max(
                    0f,
                    currentTeamModifier)
                : 1f;

        float safeBaseSpeed =
            float.IsFinite(baseGrindSpeed)
                ? Mathf.Max(
                    0f,
                    baseGrindSpeed)
                : 0f;

        float safeMinimumSpeed =
            float.IsFinite(minGrindSpeed)
                ? Mathf.Max(
                    0f,
                    minGrindSpeed)
                : 0f;

        float safeMaximumSpeed =
            float.IsFinite(maxGrindSpeed)
                ? Mathf.Max(
                    safeMinimumSpeed,
                    maxGrindSpeed)
                : safeMinimumSpeed;

        float teamBaseSpeed =
            safeBaseSpeed *
            teamModifier;

        if (!float.IsFinite(
                teamBaseSpeed))
        {
            teamBaseSpeed =
                safeMinimumSpeed;
        }

        float inheritedSpeed = 0f;

        if (playerRigidbody != null)
        {
            Vector3 travelDirection =
                GetTravelDirection();

            Vector3 velocity =
                playerRigidbody.linearVelocity;

            if (IsFiniteVector(
                    travelDirection) &&
                travelDirection.sqrMagnitude >
                    MinimumVectorMagnitude &&
                IsFiniteVector(
                    velocity))
            {
                travelDirection.Normalize();

                float projectedSpeed =
                    Vector3.Dot(
                        velocity,
                        travelDirection);

                if (float.IsFinite(
                        projectedSpeed))
                {
                    inheritedSpeed =
                        Mathf.Abs(
                            projectedSpeed);
                }
            }
        }

        float startingSpeed =
            Mathf.Max(
                teamBaseSpeed,
                inheritedSpeed);

        if (!float.IsFinite(
                startingSpeed))
        {
            startingSpeed =
                safeMinimumSpeed;
        }

        currentGrindSpeed =
            Mathf.Clamp(
                startingSpeed,
                safeMinimumSpeed,
                safeMaximumSpeed);
    }

    private void AdvanceAlongRail()
    {
        if (currentRail == null ||
            !IsValidGrindingRuntimeState())
        {
            StopGrinding(
                jumped: false);

            return;
        }

        Vector3 travelDirection =
            GetTravelDirection();

        if (!IsFiniteVector(
                travelDirection) ||
            travelDirection.sqrMagnitude <=
                MinimumVectorMagnitude)
        {
            StopGrinding(
                jumped: false);

            return;
        }

        travelDirection.Normalize();

        float safeSlopeAcceleration =
            float.IsFinite(slopeAcceleration)
                ? Mathf.Max(
                    0f,
                    slopeAcceleration)
                : 0f;

        float slopeAmount =
            Vector3.Dot(
                travelDirection,
                Vector3.down);

        if (!float.IsFinite(
                slopeAmount))
        {
            slopeAmount = 0f;
        }

        float speedDelta =
            slopeAmount *
            safeSlopeAcceleration *
            Time.fixedDeltaTime;

        if (!float.IsFinite(
                speedDelta))
        {
            speedDelta = 0f;
        }

        currentGrindSpeed +=
            speedDelta;

        if (!float.IsFinite(
                currentGrindSpeed))
        {
            StopGrinding(
                jumped: false);

            return;
        }

        float safeMinimumSpeed =
            float.IsFinite(minGrindSpeed)
                ? Mathf.Max(
                    0f,
                    minGrindSpeed)
                : 0f;

        float safeMaximumSpeed =
            float.IsFinite(maxGrindSpeed)
                ? Mathf.Max(
                    safeMinimumSpeed,
                    maxGrindSpeed)
                : safeMinimumSpeed;

        currentGrindSpeed =
            Mathf.Clamp(
                currentGrindSpeed,
                safeMinimumSpeed,
                safeMaximumSpeed);

        float crouchMultiplier =
            float.IsFinite(crouchSpeedMultiplier)
                ? Mathf.Max(
                    0f,
                    crouchSpeedMultiplier)
                : 1f;

        float effectiveSpeed =
            isCrouching
                ? currentGrindSpeed *
                  crouchMultiplier
                : currentGrindSpeed;

        if (!float.IsFinite(
                effectiveSpeed))
        {
            StopGrinding(
                jumped: false);

            return;
        }

        effectiveSpeed =
            Mathf.Clamp(
                effectiveSpeed,
                safeMinimumSpeed,
                safeMaximumSpeed);

        if (!float.IsFinite(
                currentRailLength) ||
            currentRailLength <=
                Mathf.Epsilon)
        {
            StopGrinding(
                jumped: false);

            return;
        }

        float directionSign =
            float.IsFinite(grindDirectionSign) &&
            Mathf.Abs(grindDirectionSign) >
                MinimumVectorMagnitude
                ? Mathf.Sign(
                    grindDirectionSign)
                : 1f;

        float parameterDelta =
            directionSign *
            effectiveSpeed *
            Time.fixedDeltaTime /
            currentRailLength;

        if (!float.IsFinite(
                parameterDelta))
        {
            StopGrinding(
                jumped: false);

            return;
        }

        splineT +=
            parameterDelta;

        if (!float.IsFinite(
                splineT))
        {
            StopGrinding(
                jumped: false);

            return;
        }

        if (splineT > 0f &&
            splineT < 1f)
        {
            return;
        }

        splineT =
            Mathf.Clamp01(
                splineT);

        StopGrinding(
            jumped: false);
    }

    private void AlignLeaderToRail()
    {
        if (currentRail == null ||
            playerRigidbody == null ||
            !float.IsFinite(splineT))
        {
            return;
        }

        Vector3 currentPosition =
            playerRigidbody.position;

        if (!IsFiniteVector(
                currentPosition))
        {
            StopGrinding(
                jumped: false);

            return;
        }

        Vector3 targetPosition =
            currentRail.GetPoint(
                splineT);

        if (!IsFiniteVector(
                targetPosition))
        {
            StopGrinding(
                jumped: false);

            return;
        }

        float safeSnapSpeed =
            float.IsFinite(snapSpeed)
                ? Mathf.Max(
                    0f,
                    snapSpeed)
                : 0f;

        float positionLerpFactor =
            safeSnapSpeed *
            Time.fixedDeltaTime;

        if (!float.IsFinite(
                positionLerpFactor))
        {
            positionLerpFactor = 0f;
        }

        Vector3 snappedPosition =
            Vector3.Lerp(
                currentPosition,
                targetPosition,
                Mathf.Clamp01(
                    positionLerpFactor));

        if (!IsFiniteVector(
                snappedPosition))
        {
            StopGrinding(
                jumped: false);

            return;
        }

        playerRigidbody.MovePosition(
            snappedPosition);

        Vector3 travelDirection =
            GetTravelDirection();

        if (!IsFiniteVector(
                travelDirection) ||
            travelDirection.sqrMagnitude <=
                MinimumVectorMagnitude)
        {
            return;
        }

        travelDirection.Normalize();

        Quaternion currentRotation =
            playerRigidbody.rotation;

        if (!IsFiniteQuaternion(
                currentRotation))
        {
            currentRotation =
                Quaternion.identity;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                travelDirection,
                Vector3.up);

        if (!IsFiniteQuaternion(
                targetRotation))
        {
            return;
        }

        float rotationLerpFactor =
            safeSnapSpeed *
            Time.fixedDeltaTime;

        if (!float.IsFinite(
                rotationLerpFactor))
        {
            rotationLerpFactor = 0f;
        }

        Quaternion smoothedRotation =
            Quaternion.Slerp(
                currentRotation,
                targetRotation,
                Mathf.Clamp01(
                    rotationLerpFactor));

        if (!IsFiniteQuaternion(
                smoothedRotation))
        {
            return;
        }

        playerRigidbody.MoveRotation(
            smoothedRotation);
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

    private Vector3 GetTravelDirection()
    {
        Vector3 fallbackDirection =
            transform.forward;

        if (!IsFiniteVector(
                fallbackDirection) ||
            fallbackDirection.sqrMagnitude <=
                MinimumVectorMagnitude)
        {
            fallbackDirection =
                Vector3.forward;
        }
        else
        {
            fallbackDirection.Normalize();
        }

        if (currentRail == null ||
            !float.IsFinite(splineT))
        {
            return fallbackDirection;
        }

        Vector3 tangent =
            currentRail.GetTangent(
                splineT);

        if (!IsFiniteVector(
                tangent) ||
            tangent.sqrMagnitude <=
                MinimumVectorMagnitude)
        {
            return fallbackDirection;
        }

        tangent.Normalize();

        float directionSign =
            float.IsFinite(grindDirectionSign) &&
            Mathf.Abs(grindDirectionSign) >
                MinimumVectorMagnitude
                ? Mathf.Sign(
                    grindDirectionSign)
                : 1f;

        Vector3 travelDirection =
            tangent *
            directionSign;

        if (!IsFiniteVector(
                travelDirection) ||
            travelDirection.sqrMagnitude <=
                MinimumVectorMagnitude)
        {
            return fallbackDirection;
        }

        return travelDirection;
    }

    #endregion

    #region Input

    private void HandleGrindingInput()
    {
        if (Input.GetKeyDown(railJumpKey))
        {
            StopGrinding(
                jumped: true);

            return;
        }

        isCrouching =
            Input.GetKey(crouchKey);

        float horizontalInput =
            Input.GetAxisRaw("Horizontal");

        if (Mathf.Abs(horizontalInput) <
            switchInputThreshold)
        {
            return;
        }

        TrySwitchRail(
            horizontalInput);
    }

    #endregion

    #region Team Formation

    private void PositionTeammates()
    {
        if (currentRail == null)
            return;

        PlaceMemberAtOffset(
            member2,
            memberSpacing,
            member2SparksFX);

        PlaceMemberAtOffset(
            member3,
            memberSpacing * 2f,
            member3SparksFX);
    }

    private void PlaceMemberAtOffset(
    Transform member,
    float worldOffset,
    ParticleSystem sparks)
    {
        if (member == null ||
            currentRail == null ||
            !float.IsFinite(currentRailLength) ||
            currentRailLength <= Mathf.Epsilon)
        {
            return;
        }

        if (!float.IsFinite(worldOffset))
        {
            return;
        }

        float offsetT =
            worldOffset /
            currentRailLength;

        if (!float.IsFinite(
                offsetT))
        {
            return;
        }

        float directionSign =
            float.IsFinite(grindDirectionSign) &&
            Mathf.Abs(grindDirectionSign) >
                MinimumVectorMagnitude
                ? Mathf.Sign(
                    grindDirectionSign)
                : 1f;

        float memberT =
            splineT -
            directionSign *
            offsetT;

        if (!float.IsFinite(
                memberT))
        {
            return;
        }

        memberT =
            Mathf.Clamp01(
                memberT);

        Vector3 targetPosition =
            currentRail.GetPoint(
                memberT);

        if (!IsFiniteVector(
                targetPosition))
        {
            return;
        }

        Vector3 currentPosition =
            member.position;

        if (!IsFiniteVector(
                currentPosition))
        {
            currentPosition =
                targetPosition;
        }

        float safeSnapSpeed =
            float.IsFinite(memberSnapSpeed)
                ? Mathf.Max(
                    0f,
                    memberSnapSpeed)
                : 0f;

        float lerpFactor =
            safeSnapSpeed *
            Time.fixedDeltaTime;

        if (!float.IsFinite(
                lerpFactor))
        {
            lerpFactor = 0f;
        }

        Vector3 nextPosition =
            Vector3.Lerp(
                currentPosition,
                targetPosition,
                Mathf.Clamp01(
                    lerpFactor));

        if (IsFiniteVector(
                nextPosition))
        {
            member.position =
                nextPosition;
        }

        Vector3 tangent =
            currentRail.GetTangent(
                memberT);

        if (!IsFiniteVector(
                tangent) ||
            tangent.sqrMagnitude <=
                MinimumVectorMagnitude)
        {
            UpdateMemberSparks(
                sparks);

            return;
        }

        tangent.Normalize();

        Vector3 travelDirection =
            tangent *
            directionSign;

        if (!IsFiniteVector(
                travelDirection) ||
            travelDirection.sqrMagnitude <=
                MinimumVectorMagnitude)
        {
            UpdateMemberSparks(
                sparks);

            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                travelDirection,
                Vector3.up);

        if (IsFiniteQuaternion(
                targetRotation))
        {
            Quaternion currentRotation =
                member.rotation;

            if (!IsFiniteQuaternion(
                    currentRotation))
            {
                currentRotation =
                    targetRotation;
            }

            Quaternion nextRotation =
                Quaternion.Slerp(
                    currentRotation,
                    targetRotation,
                    Mathf.Clamp01(
                        lerpFactor));

            if (IsFiniteQuaternion(
                    nextRotation))
            {
                member.rotation =
                    nextRotation;
            }
        }

        UpdateMemberSparks(
            sparks);
    }

    private static void UpdateMemberSparks(
    ParticleSystem sparks)
    {
        if (sparks == null ||
            sparks.isPlaying)
        {
            return;
        }

        sparks.Play();
    }

    #endregion

    #region Rail Switching

    private void TrySwitchRail(
        float lateralInput)
    {
        if (!CanAttemptRailSwitch())
            return;

        Vector3 playerPosition =
            playerRigidbody.position;

        Vector3 travelDirection =
            GetTravelDirection();

        Vector3 preferredDirection =
            GetPreferredSwitchDirection(
                travelDirection,
                lateralInput);

        if (!TryFindBestRailSwitch(
                playerPosition,
                travelDirection,
                preferredDirection,
                out RailSwitchCandidate candidate))
        {
            return;
        }

        ApplyRailSwitch(candidate);
    }

    private bool CanAttemptRailSwitch()
    {
        return
            currentRail != null &&
            playerRigidbody != null &&
            Time.time -
            lastSwitchTime >=
            switchCooldown;
    }

    private static Vector3 GetPreferredSwitchDirection(
        Vector3 travelDirection,
        float lateralInput)
    {
        Vector3 rightDirection =
            Vector3.Cross(
                Vector3.up,
                travelDirection).normalized;

        return
            rightDirection *
            Mathf.Sign(lateralInput);
    }

    private bool TryFindBestRailSwitch(
    Vector3 playerPosition,
    Vector3 travelDirection,
    Vector3 preferredDirection,
    out RailSwitchCandidate bestCandidate)
    {
        bestCandidate = default;

        bool foundCandidate = false;
        float bestScore = float.MaxValue;

        int railCount =
            Physics.OverlapSphereNonAlloc(
                playerPosition,
                switchScanRadius,
                railSwitchBuffer,
                railLayerMask,
                QueryTriggerInteraction.Collide);

        for (int index = 0;
             index < railCount;
             index++)
        {
            Collider nearbyCollider =
                railSwitchBuffer[index];

            if (!TryEvaluateRailCandidate(
                    nearbyCollider,
                    playerPosition,
                    travelDirection,
                    preferredDirection,
                    out RailSwitchCandidate candidate))
            {
                continue;
            }

            if (candidate.Score >= bestScore)
                continue;

            bestScore = candidate.Score;
            bestCandidate = candidate;
            foundCandidate = true;
        }

        return foundCandidate;
    }

    private bool TryEvaluateRailCandidate(
    Collider nearbyCollider,
    Vector3 playerPosition,
    Vector3 travelDirection,
    Vector3 preferredDirection,
    out RailSwitchCandidate candidate)
    {
        candidate = default;

        if (nearbyCollider == null ||
            !IsFiniteVector(playerPosition) ||
            !IsFiniteVector(travelDirection) ||
            !IsFiniteVector(preferredDirection))
        {
            return false;
        }

        if (travelDirection.sqrMagnitude <=
                MinimumVectorMagnitude ||
            preferredDirection.sqrMagnitude <=
                MinimumVectorMagnitude)
        {
            return false;
        }

        RailSpline rail =
            nearbyCollider.GetComponentInParent<RailSpline>();

        if (rail == null ||
            rail == currentRail)
        {
            return false;
        }

        float railT =
            rail.GetClosestT(
                playerPosition);

        if (!float.IsFinite(
                railT))
        {
            return false;
        }

        railT =
            Mathf.Clamp01(
                railT);

        Vector3 railPoint =
            rail.GetPoint(
                railT);

        if (!IsFiniteVector(
                railPoint))
        {
            return false;
        }

        Vector3 toRail =
            railPoint -
            playerPosition;

        if (!IsFiniteVector(
                toRail))
        {
            return false;
        }

        float distanceSqr =
            toRail.sqrMagnitude;

        if (!float.IsFinite(
                distanceSqr) ||
            distanceSqr <=
                MinimumVectorMagnitude)
        {
            return false;
        }

        if (!float.IsFinite(
                switchScanRadiusSqr) ||
            switchScanRadiusSqr <= 0f ||
            distanceSqr >
                switchScanRadiusSqr)
        {
            return false;
        }

        float distance =
            Mathf.Sqrt(
                distanceSqr);

        if (!float.IsFinite(
                distance) ||
            distance <=
                Mathf.Epsilon)
        {
            return false;
        }

        Vector3 toRailDirection =
            toRail /
            distance;

        if (!IsFiniteVector(
                toRailDirection) ||
            toRailDirection.sqrMagnitude <=
                MinimumVectorMagnitude)
        {
            return false;
        }

        Vector3 safePreferredDirection =
            preferredDirection.normalized;

        if (!IsFiniteVector(
                safePreferredDirection) ||
            safePreferredDirection.sqrMagnitude <=
                MinimumVectorMagnitude)
        {
            return false;
        }

        float sideAmount =
            Vector3.Dot(
                toRailDirection,
                safePreferredDirection);

        if (!float.IsFinite(
                sideAmount) ||
            sideAmount <= 0f)
        {
            return false;
        }

        Vector3 railTangent =
            rail.GetTangent(
                railT);

        if (!IsFiniteVector(
                railTangent) ||
            railTangent.sqrMagnitude <=
                MinimumVectorMagnitude)
        {
            return false;
        }

        railTangent.Normalize();

        Vector3 safeTravelDirection =
            travelDirection.normalized;

        if (!IsFiniteVector(
                safeTravelDirection) ||
            safeTravelDirection.sqrMagnitude <=
                MinimumVectorMagnitude)
        {
            return false;
        }

        float forwardAngle =
            Vector3.Angle(
                safeTravelDirection,
                railTangent);

        float reverseAngle =
            Vector3.Angle(
                safeTravelDirection,
                -railTangent);

        if (!float.IsFinite(
                forwardAngle) ||
            !float.IsFinite(
                reverseAngle))
        {
            return false;
        }

        float bestAngle =
            Mathf.Min(
                forwardAngle,
                reverseAngle);

        float safeMaximumAngle =
            float.IsFinite(
                switchMaxAngle)
                ? Mathf.Clamp(
                    switchMaxAngle,
                    0f,
                    180f)
                : 0f;

        if (!float.IsFinite(
                bestAngle) ||
            bestAngle >
                safeMaximumAngle)
        {
            return false;
        }

        float directionSign =
            forwardAngle <= reverseAngle
                ? 1f
                : -1f;

        float safeSwitchRadius =
            float.IsFinite(
                switchScanRadius)
                ? Mathf.Max(
                    0f,
                    switchScanRadius)
                : 0f;

        float score =
            distance -
            sideAmount *
            safeSwitchRadius;

        if (!float.IsFinite(
                score))
        {
            return false;
        }

        candidate =
            new RailSwitchCandidate(
                rail,
                railT,
                directionSign,
                score);

        return true;
    }

    private void ApplyRailSwitch(
    RailSwitchCandidate candidate)
    {
        if (candidate.Rail == null)
            return;

        if (!float.IsFinite(
                candidate.SplineT) ||
            !float.IsFinite(
                candidate.DirectionSign) ||
            !float.IsFinite(
                candidate.Score))
        {
            return;
        }

        float directionSign =
            Mathf.Sign(
                candidate.DirectionSign);

        if (Mathf.Approximately(
                directionSign,
                0f))
        {
            directionSign = 1f;
        }

        float railLength =
            candidate.Rail.ApproximateLength();

        if (!float.IsFinite(
                railLength) ||
            railLength <=
                Mathf.Epsilon)
        {
            return;
        }

        currentRail =
            candidate.Rail;

        splineT =
            Mathf.Clamp01(
                candidate.SplineT);

        grindDirectionSign =
            directionSign;

        currentRailLength =
            railLength;

        AlignLeaderToRail();

        if (!isGrinding ||
            currentRail == null)
        {
            return;
        }

        lastSwitchTime =
            Time.time;

        PlayOneShot(
            railSwitchSFX);

        LogStateChange(
            $"Switched to rail '{currentRail.name}'.");
    }

    #endregion

    #region Team Modifier

    private float GetTeamModifier()
    {
        return currentTeam switch
        {
            TeamType.Speed =>
                speedTeamModifier,

            TeamType.Fly =>
                flyTeamModifier,

            TeamType.Power =>
                powerTeamModifier,

            _ =>
                1f
        };
    }

    #endregion

    #region Animation

    private void UpdateAnimatorState()
    {
        if (railAnimator == null)
            return;

        railAnimator.SetBool(
            IsGrindingHash,
            isGrinding);

        railAnimator.SetBool(
            IsCrouchingHash,
            isCrouching);

        railAnimator.SetFloat(
            GrindSpeedHash,
            currentGrindSpeed);
    }

    #endregion

    #region Audio

    private void PlayOneShot(
        AudioClip clip)
    {
        if (audioSource == null ||
            clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip);
    }

    private void StartGrindLoop()
    {
        if (audioSource == null ||
            grindLoopSFX == null)
        {
            return;
        }

        audioSource.clip = grindLoopSFX;
        audioSource.loop = true;
        audioSource.Play();
    }

    private void StopGrindLoop()
    {
        if (audioSource == null)
            return;

        audioSource.loop = false;
        audioSource.Stop();
        audioSource.clip = null;
    }

    #endregion

    #region Particle Effects

    private void PlayAllSparksFX()
    {
        if (leaderSparksFX)
        {
            leaderSparksFX.Play();
        }

        if (member2SparksFX)
        {
            member2SparksFX.Play();
        }

        if (member3SparksFX)
        {
            member3SparksFX.Play();
        }
    }

    private void StopAllSparksFX()
    {
        if (leaderSparksFX)
        {
            leaderSparksFX.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (member2SparksFX)
        {
            member2SparksFX.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (member3SparksFX)
        {
            member3SparksFX.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    #endregion

    #region Validation

    private bool ValidateConfiguration()
    {
        bool valid = true;

        valid &=
            ValidateReference(
                playerRigidbody,
                nameof(Rigidbody));

        valid &=
            ValidateReference(
                playerMovement,
                nameof(UltimatePlayerMovement));

        valid &=
            ValidateReference(
                audioSource,
                nameof(AudioSource));

        if (railLayerMask.value == 0)
        {
            Debug.LogWarning(
                "RailGrinding rail layer mask is empty.",
                this);
        }

        if (railAnimator == null)
        {
            Debug.LogWarning(
                "RailGrinding could not find an Animator.",
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
            $"RailGrinding requires {displayName}.",
            this);

        return false;
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        if (isGrinding)
        {
            StopGrinding(
                jumped: false);
        }

        StopGrindLoop();
        StopAllSparksFX();
        isCrouching = false;
        UpdateAnimatorState();
    }

    private void CleanupDestroyedState()
    {
        CleanupRuntimeState();

        isInitialized = false;

        currentRail = null;
        currentRailLength = 0f;
        playerRigidbody = null;
        playerMovement = null;
        audioSource = null;
        railAnimator = null;

        member2 = null;
        member3 = null;

        leaderSparksFX = null;
        member2SparksFX = null;
        member3SparksFX = null;
    }

    #endregion

    private static bool IsFiniteVector(
    Vector3 value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }

    private bool IsValidGrindingRuntimeState()
    {
        return
            currentRail != null &&
            playerRigidbody != null &&
            float.IsFinite(splineT) &&
            float.IsFinite(currentRailLength) &&
            currentRailLength > Mathf.Epsilon &&
            float.IsFinite(currentGrindSpeed) &&
            currentGrindSpeed >= 0f;
    }

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

    #region Internal Types

    private readonly struct RailSwitchCandidate
    {
        public RailSwitchCandidate(
            RailSpline rail,
            float splineT,
            float directionSign,
            float score)
        {
            Rail = rail;
            SplineT = splineT;
            DirectionSign = directionSign;
            Score = score;
        }

        public RailSpline Rail { get; }
        public float SplineT { get; }
        public float DirectionSign { get; }
        public float Score { get; }
    }

    #endregion
}
