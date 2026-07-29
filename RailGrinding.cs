using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(UltimatePlayerMovement))]
[RequireComponent (typeof(AudioSource))]
public class RailGrinding : MonoBehaviour
{
    #region Inspector

    [Header("Grind Speed")]
    [SerializeField] private float baseGrindSpeed = 18f;
    [SerializeField] private float maxGrindSpeed = 40f;
    [SerializeField] private float minGrindSpeed = 2f;
    [SerializeField] private float slopeAcceleration = 12f;
    [SerializeField] private float crouchSpeedMultiplier = 1.25f;

    [Header("Team Type Speed Modifiers")]
    [SerializeField] private float speedTeamModifier = 1f;
    [SerializeField] private float flyTeamModifier = 0.85f;
    [SerializeField] private float powerTeamModifier = 0.75f;

    [Header("Team Formation")]
    [SerializeField] private Transform member2;
    [SerializeField] private Transform member3;
    [SerializeField] private float memberSpacing = 1.2f;
    [SerializeField] private float memberSnapSpeed = 14f;

    [Header("Rail Detection")]
    [SerializeField] private LayerMask railLayerMask;
    [SerializeField] private float grindSnapRadius = 1.2f;
    [SerializeField] private float railDetectionRadius = 1.2f;
    [SerializeField] private float railRayDistance = 1.75f;
    [SerializeField]
    private Vector3 railDetectionOffset = new Vector3(0f, -0.25f, 0f);

    [Header("Rail Movement")]
    [SerializeField] private float snapSpeed = 20f;

    [Header("Rail Switching")]
    [SerializeField] private float switchScanRadius = 4f;
    [SerializeField] private float switchMaxAngle = 45f;
    [SerializeField] private float switchCooldown = 0.4f;
    [SerializeField] private float switchInputThreshold = 0.5f;

    [Header("Jump Off Rail")]
    [SerializeField] private KeyCode railJumpKey = KeyCode.Space;
    [SerializeField] private float railJumpForce = 12f;
    [SerializeField] private float railJumpForwardForce = 6f;

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

    [Header("Animation")]
    [SerializeField] private Animator railAnimator;

    #endregion
    
    #region Player Movements
    [SerializeField] private UltimatePlayerMovement playerMovement;
    #endregion

    #region Types

    public enum TeamType
    {
        Speed,
        Fly,
        Power
    }

    #endregion

    #region Runtime State

    [SerializeField] private TeamType currentTeam = TeamType.Speed;
    [SerializeField] private bool isGrinding;
    [SerializeField] private bool isCrouching;
    [SerializeField] private float currentGrindSpeed;
    [SerializeField, Range(0f, 1f)] private float splineT;

    private RailSpline currentRail;
    private Rigidbody playerRigidbody;
    private AudioSource audioSource;

    private float grindDirectionSign = 1f;
    private float lastSwitchTime = -999f;

    #endregion

    #region Properties

    public bool IsGrinding => isGrinding;
    public bool IsCrouching => isCrouching;
    public float CurrentGrindSpeed => currentGrindSpeed;
    public RailSpline CurrentRail => currentRail;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        playerRigidbody = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();

        if (railAnimator == null)
        {
            railAnimator = GetComponentInChildren<Animator>();
        }

        if (playerMovement == null)
        {
            playerMovement = GetComponent<UltimatePlayerMovement>();
        }
    }

    private void Start()
    {
        StopAllSparksFX();
    }

    private void Update()
    {
        if (!isGrinding)
        {
            return;
        }

        HandleGrindingInput();
        UpdateAnimatorState();
    }

    private void FixedUpdate()
    {
        if (!isGrinding)
        {
            CheckForNearbyRail();
            return;
        }

        AdvanceAlongRail();
        AlignLeaderToRail();
        PositionTeammates();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isGrinding)
        {
            return;
        }

        TryAttachFromCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (isGrinding)
        {
            return;
        }

        TryAttachFromCollider(other);
    }

    #endregion

    #region Public API

    public void SetTeamType(TeamType teamType)
    {
        currentTeam = teamType;
    }

    public bool TrySnapToRail(RailSpline rail, float contactT)
    {
        if (isGrinding || rail == null)
        {
            return false;
        }

        Vector3 railPoint = rail.GetPoint(contactT);

        if (Vector3.Distance(transform.position, railPoint) >
            grindSnapRadius)
        {
            return false;
        }

        currentRail = rail;
        splineT = Mathf.Clamp01(contactT);

        DetermineGrindingDirection();
        CalculateStartingSpeed();
        StartGrinding();

        return true;
    }

    public void ForceStopGrinding()
    {
        StopGrinding(false);
    }

    #endregion

    #region Detection

    private void CheckForNearbyRail()
    {
        Vector3 detectionOrigin =
            playerRigidbody.position + railDetectionOffset;

        // Detection method 1: downward raycast.
        if (Physics.Raycast(
            detectionOrigin,
            Vector3.down,
            out RaycastHit hit,
            railRayDistance,
            railLayerMask,
            QueryTriggerInteraction.Collide))
        {
            RailSpline raycastRail =
                hit.collider.GetComponentInParent<RailSpline>();

            if (TryAttachToRail(raycastRail))
            {
                return;
            }
        }

        // Detection method 2: overlap sphere fallback.
        Collider[] nearbyColliders = Physics.OverlapSphere(
            detectionOrigin,
            railDetectionRadius,
            railLayerMask,
            QueryTriggerInteraction.Collide);

        RailSpline closestRail = null;
        float closestT = 0f;
        float closestDistance = float.MaxValue;

        foreach (Collider nearbyCollider in nearbyColliders)
        {
            RailSpline candidate =
                nearbyCollider.GetComponentInParent<RailSpline>();

            if (candidate == null)
            {
                continue;
            }

            float candidateT =
                candidate.GetClosestT(playerRigidbody.position);

            Vector3 candidatePoint =
                candidate.GetPoint(candidateT);

            float distance = Vector3.SqrMagnitude(
                candidatePoint - playerRigidbody.position);

            if (distance >= closestDistance)
            {
                continue;
            }

            closestDistance = distance;
            closestRail = candidate;
            closestT = candidateT;
        }

        if (closestRail != null &&
            closestDistance <= grindSnapRadius * grindSnapRadius)
        {
            TrySnapToRail(closestRail, closestT);
        }
    }

    private void TryAttachFromCollider(Collider other)
    {
        if (!IsLayerInMask(other.gameObject.layer, railLayerMask) &&
            !other.CompareTag("Rail"))
        {
            return;
        }

        RailSpline rail =
            other.GetComponentInParent<RailSpline>();

        TryAttachToRail(rail);
    }

    private bool TryAttachToRail(RailSpline rail)
    {
        if (rail == null)
        {
            return false;
        }

        float closestT =
            rail.GetClosestT(playerRigidbody.position);

        return TrySnapToRail(rail, closestT);
    }

    private static bool IsLayerInMask(
        int layer,
        LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

    #endregion

    #region Start And Stop

    private void StartGrinding()
    {
        if (currentRail == null)
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
    }

    private void StopGrinding(bool jumped)
    {
        if (!isGrinding)
        {
            return;
        }

        Vector3 exitDirection = GetTravelDirection();

        isGrinding = false;
        isCrouching = false;

        playerRigidbody.useGravity = true;
        playerRigidbody.WakeUp();

        if (jumped)
        {
            playerRigidbody.linearVelocity =
                exitDirection *
                (currentGrindSpeed + railJumpForwardForce)
                + Vector3.up * railJumpForce;
        }
        else
        {
            playerRigidbody.linearVelocity =
                exitDirection * currentGrindSpeed;
        }

        playerMovement?.ExitGrindingState();

        currentRail = null;

        StopGrindLoop();
        PlayOneShot(grindEndSFX);
        StopAllSparksFX();

        UpdateAnimatorState();
    }

    #endregion

    #region Rail Movement

    private void DetermineGrindingDirection()
    {
        Vector3 railTangent = currentRail.GetTangent(splineT).normalized;

        float velocityDot = Vector3.Dot(playerRigidbody.linearVelocity, railTangent);

        if (Mathf.Abs(velocityDot) > 0.1f)
        {
            grindDirectionSign = Mathf.Sign(velocityDot);
            return;
        }

        float facingDot = Vector3.Dot(transform.forward, railTangent);

        grindDirectionSign =
            facingDot >= 0f ? 1f : -1f;
    }

    private void CalculateStartingSpeed()
    {
        Vector3 travelDirection = GetTravelDirection();

        float inheritedSpeed = Mathf.Abs(Vector3.Dot(playerRigidbody.linearVelocity, travelDirection));

        float teamBaseSpeed = baseGrindSpeed * GetTeamModifier();

        currentGrindSpeed = Mathf.Clamp(
            Mathf.Max(teamBaseSpeed, inheritedSpeed),
            minGrindSpeed,
            maxGrindSpeed);
    }

    private void AdvanceAlongRail()
    {
        if (currentRail == null)
        {
            StopGrinding(false);
            return;
        }

        Vector3 travelDirection = GetTravelDirection();

        float slopeAmount = Vector3.Dot(
            travelDirection,
            Vector3.down);

        currentGrindSpeed +=
            slopeAmount *
            slopeAcceleration *
            Time.fixedDeltaTime;

        currentGrindSpeed = Mathf.Clamp(
            currentGrindSpeed,
            minGrindSpeed,
            maxGrindSpeed);

        float effectiveSpeed = isCrouching
            ? currentGrindSpeed * crouchSpeedMultiplier
            : currentGrindSpeed;

        effectiveSpeed = Mathf.Min(
            effectiveSpeed,
            maxGrindSpeed);

        float railLength =
            currentRail.ApproximateLength();

        if (railLength <= 0f)
        {
            StopGrinding(false);
            return;
        }

        splineT +=
            grindDirectionSign *
            (effectiveSpeed * Time.fixedDeltaTime) /
            railLength;

        bool reachedEnd =
            splineT >= 1f || splineT <= 0f;

        if (!reachedEnd)
        {
            return;
        }

        splineT = Mathf.Clamp01(splineT);
        StopGrinding(false);
    }

    private void AlignLeaderToRail()
    {
        if (currentRail == null)
        {
            return;
        }

        Vector3 targetPosition =
            currentRail.GetPoint(splineT);

        Vector3 travelDirection =
            GetTravelDirection();

        Vector3 snappedPosition = Vector3.Lerp(
            playerRigidbody.position,
            targetPosition,
            snapSpeed * Time.fixedDeltaTime);

        playerRigidbody.MovePosition(snappedPosition);

        if (travelDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                travelDirection,
                Vector3.up);

        playerRigidbody.MoveRotation(
            Quaternion.Slerp(
                playerRigidbody.rotation,
                targetRotation,
                snapSpeed * Time.fixedDeltaTime));
    }

    private Vector3 GetTravelDirection()
    {
        if (currentRail == null)
        {
            return transform.forward;
        }

        Vector3 tangent =
            currentRail.GetTangent(splineT);

        if (tangent.sqrMagnitude <= 0.001f)
        {
            return transform.forward;
        }

        return tangent.normalized * grindDirectionSign;
    }

    #endregion

    #region Input

    private void HandleGrindingInput()
    {
        if (Input.GetKeyDown(railJumpKey))
        {
            StopGrinding(true);
            return;
        }

        isCrouching = Input.GetKey(crouchKey);

        float horizontalInput =
            Input.GetAxisRaw("Horizontal");

        if (Mathf.Abs(horizontalInput) >=
            switchInputThreshold)
        {
            TrySwitchRail(horizontalInput);
        }
    }

    #endregion

    #region Team Formation

    private void PositionTeammates()
    {
        if (currentRail == null)
        {
            return;
        }

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
        if (member == null || currentRail == null)
        {
            return;
        }

        float railLength =
            currentRail.ApproximateLength();

        if (railLength <= 0f)
        {
            return;
        }

        float offsetT =
            worldOffset / railLength;

        float memberT = Mathf.Clamp01(
            splineT -
            grindDirectionSign * offsetT);

        Vector3 targetPosition =
            currentRail.GetPoint(memberT);

        Vector3 tangent =
            currentRail.GetTangent(memberT) *
            grindDirectionSign;

        member.position = Vector3.Lerp(
            member.position,
            targetPosition,
            memberSnapSpeed * Time.fixedDeltaTime);

        if (tangent.sqrMagnitude > 0.001f)
        {
            member.rotation = Quaternion.Slerp(
                member.rotation,
                Quaternion.LookRotation(
                    tangent,
                    Vector3.up),
                memberSnapSpeed *
                Time.fixedDeltaTime);
        }

        if (sparks != null && !sparks.isPlaying)
        {
            sparks.Play();
        }
    }

    #endregion

    #region Rail Switching

    private void TrySwitchRail(float lateralInput)
    {
        if (currentRail == null)
        {
            return;
        }

        if (Time.time - lastSwitchTime <
            switchCooldown)
        {
            return;
        }

        Vector3 playerPosition =
            playerRigidbody.position;

        Vector3 travelDirection =
            GetTravelDirection();

        Vector3 rightDirection =
            Vector3.Cross(
                Vector3.up,
                travelDirection).normalized;

        Vector3 preferredDirection =
            rightDirection * Mathf.Sign(lateralInput);

        Collider[] nearbyRails =
            Physics.OverlapSphere(
                playerPosition,
                switchScanRadius,
                railLayerMask,
                QueryTriggerInteraction.Collide);

        RailSpline bestRail = null;
        float bestRailT = 0f;
        float bestScore = float.MaxValue;
        float bestDirectionSign = 1f;

        foreach (Collider nearbyCollider in nearbyRails)
        {
            RailSpline candidate =
                nearbyCollider.GetComponentInParent<RailSpline>();

            if (candidate == null ||
                candidate == currentRail)
            {
                continue;
            }

            float candidateT =
                candidate.GetClosestT(playerPosition);

            Vector3 candidatePoint =
                candidate.GetPoint(candidateT);

            Vector3 toCandidate =
                candidatePoint - playerPosition;

            float distance =
                toCandidate.magnitude;

            if (distance > switchScanRadius ||
                distance <= 0.001f)
            {
                continue;
            }

            float sideAmount = Vector3.Dot(
                toCandidate.normalized,
                preferredDirection);

            if (sideAmount <= 0f)
            {
                continue;
            }

            Vector3 candidateTangent =
                candidate.GetTangent(candidateT).normalized;

            float forwardAngle =
                Vector3.Angle(
                    travelDirection,
                    candidateTangent);

            float reverseAngle =
                Vector3.Angle(
                    travelDirection,
                    -candidateTangent);

            float candidateAngle =
                Mathf.Min(
                    forwardAngle,
                    reverseAngle);

            if (candidateAngle > switchMaxAngle)
            {
                continue;
            }

            float candidateSign =
                forwardAngle <= reverseAngle
                    ? 1f
                    : -1f;

            float score =
                distance -
                sideAmount * switchScanRadius;

            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestRail = candidate;
            bestRailT = candidateT;
            bestDirectionSign = candidateSign;
        }

        if (bestRail == null)
        {
            return;
        }

        currentRail = bestRail;
        splineT = bestRailT;
        grindDirectionSign = bestDirectionSign;
        lastSwitchTime = Time.time;

        PlayOneShot(railSwitchSFX);
    }

    #endregion

    #region Team Modifier

    private float GetTeamModifier()
    {
        return currentTeam switch
        {
            TeamType.Speed => speedTeamModifier,
            TeamType.Fly => flyTeamModifier,
            TeamType.Power => powerTeamModifier,
            _ => 1f
        };
    }

    #endregion

    #region Animation

    private void UpdateAnimatorState()
    {
        if (railAnimator == null)
        {
            return;
        }

        railAnimator.SetBool(
            "IsGrinding",
            isGrinding);

        railAnimator.SetBool(
            "IsCrouching",
            isCrouching);

        railAnimator.SetFloat(
            "GrindSpeed",
            currentGrindSpeed);
    }

    #endregion

    #region Audio

    private void PlayOneShot(AudioClip clip)
    {
        if (audioSource == null || clip == null)
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
        {
            return;
        }

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

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Vector3 detectionOrigin =
            transform.position + railDetectionOffset;

        Gizmos.DrawWireSphere(
            detectionOrigin,
            railDetectionRadius);

        Gizmos.DrawLine(
            detectionOrigin,
            detectionOrigin +
            Vector3.down * railRayDistance);

        Gizmos.DrawWireSphere(
            transform.position,
            switchScanRadius);
    }

    #endregion
}