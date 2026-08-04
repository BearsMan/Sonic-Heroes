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
    public bool IsGrinding => isGrinding;
    public bool IsCrouching => isCrouching;
    public float CurrentGrindSpeed => currentGrindSpeed;
    public float SplineT => splineT;
    public RailSpline CurrentRail => currentRail;
    public bool IsInitialized => isInitialized;

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
            rail == null)
        {
            return false;
        }

        float clampedT =
            Mathf.Clamp01(contactT);

        Vector3 railPoint =
            rail.GetPoint(clampedT);

        if (Vector3.Distance(
                playerRigidbody.position,
                railPoint) >
            grindSnapRadius)
        {
            return false;
        }

        currentRail = rail;
        splineT = clampedT;

        currentRailLength =
            currentRail.ApproximateLength();

        if (currentRailLength <= Mathf.Epsilon)
        {
            currentRail = null;
            currentRailLength = 0f;
            return false;
        }

        DetermineGrindingDirection();
        CalculateStartingSpeed();
        StartGrinding();

        return true;
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
        baseGrindSpeed = Mathf.Max(0f, baseGrindSpeed);
        maxGrindSpeed = Mathf.Max(0f, maxGrindSpeed);
        minGrindSpeed = Mathf.Max(0f, minGrindSpeed);

        if (maxGrindSpeed < minGrindSpeed)
            maxGrindSpeed = minGrindSpeed;

        baseGrindSpeed =
            Mathf.Clamp(
                baseGrindSpeed,
                minGrindSpeed,
                maxGrindSpeed);

        slopeAcceleration = Mathf.Max(0f, slopeAcceleration);
        crouchSpeedMultiplier = Mathf.Max(0f, crouchSpeedMultiplier);

        speedTeamModifier = Mathf.Max(0f, speedTeamModifier);
        flyTeamModifier = Mathf.Max(0f, flyTeamModifier);
        powerTeamModifier = Mathf.Max(0f, powerTeamModifier);

        memberSpacing = Mathf.Max(0f, memberSpacing);
        memberSnapSpeed = Mathf.Max(0f, memberSnapSpeed);

        grindSnapRadius = Mathf.Max(0f, grindSnapRadius);
        railDetectionRadius = Mathf.Max(0f, railDetectionRadius);
        railRayDistance = Mathf.Max(0f, railRayDistance);
        snapSpeed = Mathf.Max(0f, snapSpeed);

        switchScanRadius = Mathf.Max(0f, switchScanRadius);
        switchMaxAngle = Mathf.Clamp(switchMaxAngle, 0f, 180f);
        switchCooldown = Mathf.Max(0f, switchCooldown);
        switchInputThreshold = Mathf.Clamp01(switchInputThreshold);

        railJumpForce = Mathf.Max(0f, railJumpForce);
        railJumpForwardForce = Mathf.Max(0f, railJumpForwardForce);

        splineT = Mathf.Clamp01(splineT);
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
        currentTeamModifier = GetTeamModifier();
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
            grindSnapRadius *
            grindSnapRadius)
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
        if (currentRail == null ||
            playerRigidbody == null)
        {
            grindDirectionSign = 1f;
            return;
        }

        Vector3 railTangent =
            currentRail
                .GetTangent(splineT)
                .normalized;

        float velocityDot =
            Vector3.Dot(
                playerRigidbody.linearVelocity,
                railTangent);

        if (Mathf.Abs(velocityDot) > 0.1f)
        {
            grindDirectionSign =
                Mathf.Sign(velocityDot);

            return;
        }

        float facingDot =
            Vector3.Dot(
                transform.forward,
                railTangent);

        grindDirectionSign =
            facingDot >= 0f
                ? 1f
                : -1f;
    }

    private void CalculateStartingSpeed()
    {
        if (playerRigidbody == null)
        {
            currentGrindSpeed =
                Mathf.Clamp(
                    baseGrindSpeed *
                    currentTeamModifier,
                    minGrindSpeed,
                    maxGrindSpeed);

            return;
        }

        Vector3 travelDirection =
            GetTravelDirection();

        float inheritedSpeed =
            Mathf.Abs(
                Vector3.Dot(
                    playerRigidbody.linearVelocity,
                    travelDirection));

        float teamBaseSpeed =
            baseGrindSpeed *
            currentTeamModifier;

        currentGrindSpeed =
            Mathf.Clamp(
                Mathf.Max(
                    teamBaseSpeed,
                    inheritedSpeed),
                minGrindSpeed,
                maxGrindSpeed);
    }

    private void AdvanceAlongRail()
    {
        if (currentRail == null)
        {
            StopGrinding(
                jumped: false);

            return;
        }

        Vector3 travelDirection =
            GetTravelDirection();

        float slopeAmount =
            Vector3.Dot(
                travelDirection,
                Vector3.down);

        currentGrindSpeed +=
            slopeAmount *
            slopeAcceleration *
            Time.fixedDeltaTime;

        currentGrindSpeed =
            Mathf.Clamp(
                currentGrindSpeed,
                minGrindSpeed,
                maxGrindSpeed);

        float effectiveSpeed =
            isCrouching
                ? currentGrindSpeed *
                  crouchSpeedMultiplier
                : currentGrindSpeed;

        effectiveSpeed =
            Mathf.Min(
                effectiveSpeed,
                maxGrindSpeed);

        if (currentRailLength <= Mathf.Epsilon)
        {
            StopGrinding(
                jumped: false);

            return;
        }

        splineT +=
    grindDirectionSign *
    effectiveSpeed *
    Time.fixedDeltaTime /
    currentRailLength;

        if (splineT > 0f &&
            splineT < 1f)
        {
            return;
        }

        splineT =
            Mathf.Clamp01(splineT);

        StopGrinding(
            jumped: false);
    }

    private void AlignLeaderToRail()
    {
        if (currentRail == null ||
            playerRigidbody == null)
        {
            return;
        }

        Vector3 targetPosition =
            currentRail.GetPoint(splineT);

        Vector3 snappedPosition =
            Vector3.Lerp(
                playerRigidbody.position,
                targetPosition,
                snapSpeed *
                Time.fixedDeltaTime);

        playerRigidbody.MovePosition(
            snappedPosition);

        Vector3 travelDirection =
            GetTravelDirection();

        if (travelDirection.sqrMagnitude <=
            0.001f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                travelDirection,
                Vector3.up);

        Quaternion smoothedRotation =
            Quaternion.Slerp(
                playerRigidbody.rotation,
                targetRotation,
                snapSpeed *
                Time.fixedDeltaTime);

        playerRigidbody.MoveRotation(
            smoothedRotation);
    }

    private Vector3 GetTravelDirection()
    {
        if (currentRail == null)
            return transform.forward;

        Vector3 tangent =
            currentRail.GetTangent(
                splineT);

        if (tangent.sqrMagnitude <= 0.001f)
            return transform.forward;

        return
            tangent.normalized *
            grindDirectionSign;
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
            currentRail == null)
        {
            return;
        }

        if (currentRailLength <= Mathf.Epsilon)
            return;

        float offsetT =
            worldOffset /
            currentRailLength;

        float memberT =
            Mathf.Clamp01(
                splineT -
                grindDirectionSign *
                offsetT);

        Vector3 targetPosition =
            currentRail.GetPoint(memberT);

        Vector3 tangent =
            currentRail.GetTangent(memberT) *
            grindDirectionSign;

        member.position =
            Vector3.Lerp(
                member.position,
                targetPosition,
                memberSnapSpeed *
                Time.fixedDeltaTime);

        if (tangent.sqrMagnitude > 0.001f)
        {
            member.rotation =
                Quaternion.Slerp(
                    member.rotation,
                    Quaternion.LookRotation(
                        tangent,
                        Vector3.up),
                    memberSnapSpeed *
                    Time.fixedDeltaTime);
        }

        if (sparks != null &&
            !sparks.isPlaying)
        {
            sparks.Play();
        }
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

        if (nearbyCollider == null)
            return false;

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

        Vector3 railPoint =
            rail.GetPoint(railT);

        Vector3 toRail =
            railPoint -
            playerPosition;

        float distance =
            toRail.magnitude;

        if (distance <= 0.001f ||
            distance > switchScanRadius)
        {
            return false;
        }

        float sideAmount =
            Vector3.Dot(
                toRail.normalized,
                preferredDirection);

        if (sideAmount <= 0f)
            return false;

        Vector3 railTangent =
            rail.GetTangent(railT).normalized;

        float forwardAngle =
            Vector3.Angle(
                travelDirection,
                railTangent);

        float reverseAngle =
            Vector3.Angle(
                travelDirection,
                -railTangent);

        float bestAngle =
            Mathf.Min(
                forwardAngle,
                reverseAngle);

        if (bestAngle > switchMaxAngle)
            return false;

        float directionSign =
            forwardAngle <= reverseAngle
                ? 1f
                : -1f;

        float score =
            distance -
            sideAmount *
            switchScanRadius;

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
        currentRail = candidate.Rail;
        splineT = candidate.SplineT;
        grindDirectionSign =
            candidate.DirectionSign;
        lastSwitchTime = Time.time;

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
        leaderSparksFX?.Play();
        member2SparksFX?.Play();
        member3SparksFX?.Play();
    }

    private void StopAllSparksFX()
    {
        leaderSparksFX?.Stop();
        member2SparksFX?.Stop();
        member3SparksFX?.Stop();
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
