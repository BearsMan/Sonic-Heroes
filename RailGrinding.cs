using UnityEngine;
using System.Collections.Generic;

// =============================================================================
//  RailGrinding.cs  —  Sonic Heroes accurate rail grinding
//
//  Mechanics implemented:
//    • Leader snaps to rail; teammates trail behind in formation (Speed/Fly/Power)
//    • Slope-based speed (downhill accelerates, uphill decelerates)
//    • Crouch for speed boost and low hitbox
//    • Jump off rail with preserved momentum
//    • Rail switching — scan for adjacent parallel rails and hop to them
//    • Team-type speed modifiers (Speed > Fly > Power)
//    • Sparks FX + grind audio loop per member
// =============================================================================

public class RailGrinding : MonoBehaviour
{
    // =========================================================================
    // Inspector fields
    // =========================================================================

    // --- Grind speed ----------------------------------------------------------
    [Header("Grind Speed")]
    [Tooltip("Base speed in units/second (Heroes default ~18).")]
    public float baseGrindSpeed = 18f;

    [Tooltip("Absolute speed cap while grinding downhill.")]
    public float maxGrindSpeed = 40f;

    [Tooltip("Speed floor — drop below this and the team falls off.")]
    public float minGrindSpeed = 2f;

    [Tooltip("Slope gravity strength (units/s^2 per normalised slope dot).")]
    public float slopeAcceleration = 12f;

    [Tooltip("Speed multiplier while crouching on the rail.")]
    public float crouchSpeedMultiplier = 1.25f;

    // --- Team type modifiers --------------------------------------------------
    [Header("Team Type Speed Modifiers")]
    public float speedTeamModifier = 1.00f;   // Sonic / Shadow / Espio
    public float flyTeamModifier = 0.85f;   // Tails / Rouge / Charmy
    public float powerTeamModifier = 0.75f;   // Knuckles / Omega / Vector

    // --- Team formation -------------------------------------------------------
    [Header("Team Formation")]
    [Tooltip("The two teammates that follow the leader on the rail.")]
    public Transform member2;           // Middle member
    public Transform member3;           // Rear member

    [Tooltip("Gap (in world units) between each team member while grinding.")]
    public float memberSpacing = 1.2f;

    [Tooltip("How fast teammates lerp to their rail positions each frame.")]
    public float memberSnapSpeed = 14f;

    // --- Rail switching -------------------------------------------------------
    [Header("Rail Switching")]
    [Tooltip("Radius to scan for adjacent rails when the player inputs a switch.")]
    public float switchScanRadius = 4f;

    [Tooltip("Max angle (degrees) between current rail tangent and candidate — filters out opposing/perpendicular rails.")]
    public float switchMaxAngle = 45f;

    [Tooltip("Cooldown in seconds between switch attempts (prevents flicker).")]
    public float switchCooldown = 0.4f;

    [Tooltip("Layer mask that contains rail colliders.")]
    public LayerMask railLayerMask;

    // --- Snap & alignment -----------------------------------------------------
    [Header("Snap & Alignment")]
    public float snapSpeed = 20f;
    public float grindSnapRadius = 1.2f;

    // --- Jump -----------------------------------------------------------------
    [Header("Jump Off Rail")]
    public float railJumpForce = 12f;
    public float railJumpForwardForce = 6f;

    // --- Audio & FX -----------------------------------------------------------
    [Header("Audio & FX")]
    public AudioClip grindLoopSFX;
    public AudioClip grindStartSFX;
    public AudioClip grindEndSFX;
    public AudioClip railSwitchSFX;

    [Tooltip("Spark FX played under the leader's feet.")]
    public ParticleSystem leaderSparksFX;

    [Tooltip("Spark FX played under member 2.")]
    public ParticleSystem member2SparksFX;

    [Tooltip("Spark FX played under member 3.")]
    public ParticleSystem member3SparksFX;

    // =========================================================================
    // Private runtime state
    // =========================================================================

    public enum TeamType { Speed, Fly, Power }
    private TeamType currentTeam = TeamType.Speed;

    private bool isGrinding = false;
    private float currentGrindSpeed = 0f;
    private float splineT = 0f;    // Leader's normalised position [0,1]
    private bool isCrouching = false;
    private float lastSwitchTime = -999f;

    private RailSpline railSpline;
    private Rigidbody rb;
    private AudioSource audioSource;
    private Animator animator;

    // =========================================================================
    // Unity lifecycle
    // =========================================================================

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        animator = GetComponent<Animator>();
    }

    void Start()
    {
        StopAllSparksFX();
    }

    void Update()
    {
        if (!isGrinding) return;

        HandleGrindingInput();
        AdvanceAlongRail();
        AlignLeaderToRail();
        PositionTeammates();
        UpdateAnimatorState();
    }

    // =========================================================================
    // Public API
    // =========================================================================

    public bool IsGrinding => isGrinding;

    public void SetTeamType(TeamType team) => currentTeam = team;

    /// <summary>
    /// Call from a trigger/collision handler when the leader touches a rail.
    /// </summary>
    public void TrySnapToRail(RailSpline spline, float contactT)
    {
        if (isGrinding || spline == null) return;

        railSpline = spline;
        splineT = Mathf.Clamp01(contactT);

        float inheritedSpeed = rb != null
            ? Vector3.Dot(rb.linearVelocity, GetRailForward())
            : 0f;

        currentGrindSpeed = Mathf.Max(
            baseGrindSpeed * GetTeamModifier(),
            Mathf.Abs(inheritedSpeed));

        StartGrinding();
    }

    // =========================================================================
    // Core grinding — start / stop
    // =========================================================================

    private void StartGrinding()
    {
        isGrinding = true;

        if (rb != null)
        {
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
        }

        PlayOneShot(grindStartSFX);
        StartGrindLoop();
        PlayAllSparksFX();
    }

    private void StopGrinding(bool jumped = false)
    {
        isGrinding = false;
        isCrouching = false;

        if (rb != null)
        {
            rb.useGravity = true;
            if (jumped)
            {
                rb.linearVelocity =
                    GetRailForward() * (currentGrindSpeed + railJumpForwardForce)
                    + Vector3.up * railJumpForce;
            }
        }

        StopGrindLoop();
        PlayOneShot(grindEndSFX);
        StopAllSparksFX();

        animator?.SetBool("IsGrinding", false);
        animator?.SetBool("IsCrouching", false);
    }

    // =========================================================================
    // Core grinding — advance along spline each frame
    // =========================================================================

    private void AdvanceAlongRail()
    {
        if (railSpline == null) { StopGrinding(); return; }

        // Slope-based acceleration — downhill tangent has a positive dot with Vector3.down
        Vector3 tangent = railSpline.GetTangent(splineT);
        float slopeDot = Vector3.Dot(tangent.normalized, Vector3.down);
        currentGrindSpeed += slopeDot * slopeAcceleration * Time.deltaTime;

        float effectiveSpeed = isCrouching
            ? currentGrindSpeed * crouchSpeedMultiplier
            : currentGrindSpeed;

        effectiveSpeed = Mathf.Clamp(effectiveSpeed, minGrindSpeed, maxGrindSpeed);
        currentGrindSpeed = effectiveSpeed;

        float len = railSpline.ApproximateLength();
        if (len > 0f)
            splineT += (effectiveSpeed * Time.deltaTime) / len;

        if (splineT >= 1f) { splineT = 1f; StopGrinding(); return; }

        if (currentGrindSpeed <= minGrindSpeed) StopGrinding();
    }

    // =========================================================================
    // Core grinding — leader alignment
    // =========================================================================

    private void AlignLeaderToRail()
    {
        if (railSpline == null) return;

        Vector3 targetPos = railSpline.GetPoint(splineT);
        Vector3 tangent = railSpline.GetTangent(splineT);

        transform.position = Vector3.Lerp(transform.position, targetPos, snapSpeed * Time.deltaTime);
        if (tangent != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
    }

    // =========================================================================
    // Team formation — place teammates behind the leader on the same rail
    // =========================================================================

    /// <summary>
    /// In Sonic Heroes each team member trails the leader at a fixed spacing
    /// along the rail spline.  Member 2 is one gap back; Member 3 is two gaps
    /// back.  Their T values are computed by walking the spline backward by
    /// memberSpacing world units from the leader's current T.
    /// </summary>
    private void PositionTeammates()
    {
        if (railSpline == null) return;
        PlaceMemberAtOffset(member2, 1f * memberSpacing, member2SparksFX);
        PlaceMemberAtOffset(member3, 2f * memberSpacing, member3SparksFX);
    }

    private void PlaceMemberAtOffset(Transform member, float worldOffset, ParticleSystem sparks)
    {
        if (member == null) return;

        float len = railSpline.ApproximateLength();
        if (len <= 0f) return;

        float memberT = Mathf.Clamp01(splineT - worldOffset / len);
        Vector3 tgtPos = railSpline.GetPoint(memberT);
        Vector3 tangent = railSpline.GetTangent(memberT);

        member.position = Vector3.Lerp(member.position, tgtPos, memberSnapSpeed * Time.deltaTime);
        if (tangent != Vector3.zero)
            member.rotation = Quaternion.LookRotation(tangent, Vector3.up);

        if (sparks != null && !sparks.isPlaying)
            sparks.Play();
    }

    // =========================================================================
    // Rail switching
    // =========================================================================

    /// <summary>
    /// Scans for a nearby parallel RailSpline and switches the whole team to it.
    ///
    /// Heroes behaviour reproduced:
    ///   - Press Left / Right (horizontal input perpendicular to rail direction).
    ///   - Candidate must be within switchScanRadius.
    ///   - Candidate tangent must be within switchMaxAngle of current tangent
    ///     (rejects opposing and cross-angled rails).
    ///   - Prefer the rail most in the direction of the player's input.
    ///   - A small upward arc is applied so the team visually hops the gap.
    ///   - switchCooldown prevents flickering back and forth.
    /// </summary>
    private void TrySwitchRail(float lateralInput)
    {
        if (Time.time - lastSwitchTime < switchCooldown) return;
        if (railSpline == null) return;

        Vector3 leaderPos = transform.position;
        Vector3 currentTangent = GetRailForward();

        // Preferred lateral direction relative to current rail
        Vector3 preferredDir = Vector3.Cross(currentTangent, Vector3.up).normalized
                               * Mathf.Sign(lateralInput);

        Collider[] hits = Physics.OverlapSphere(leaderPos, switchScanRadius, railLayerMask);

        RailSpline bestCandidate = null;
        float bestScore = float.MaxValue;

        foreach (Collider col in hits)
        {
            RailSpline candidate = col.GetComponent<RailSpline>();
            if (candidate == null || candidate == railSpline) continue;

            float candT = candidate.GetClosestT(leaderPos);
            Vector3 candPos = candidate.GetPoint(candT);

            float dist = Vector3.Distance(leaderPos, candPos);
            if (dist > switchScanRadius) continue;

            float angle = Vector3.Angle(currentTangent, candidate.GetTangent(candT));
            if (angle > switchMaxAngle) continue;

            // Score: prefer closer + more in the input direction
            Vector3 toCandidate = (candPos - leaderPos).normalized;
            float sideBonus = Vector3.Dot(toCandidate, preferredDir) * switchScanRadius;
            float score = dist - sideBonus;

            if (score < bestScore) { bestScore = score; bestCandidate = candidate; }
        }

        if (bestCandidate == null) return;

        // --- Commit the switch ---
        lastSwitchTime = Time.time;
        railSpline = bestCandidate;
        splineT = bestCandidate.GetClosestT(leaderPos);

        PlayOneShot(railSwitchSFX);

        // Small hop arc matching Heroes' visual (about 35% of a full jump)
        if (rb != null)
        {
            rb.linearVelocity = GetRailForward() * currentGrindSpeed
                              + Vector3.up * (railJumpForce * 0.35f);
        }
    }

    // =========================================================================
    // Input handling
    // =========================================================================

    private void HandleGrindingInput()
    {
        // Jump off rail
        if (Input.GetButtonDown("Jump"))
        {
            StopGrinding(jumped: true);
            return;
        }

        // Crouch — hold for speed bonus + low hitbox
        isCrouching = Input.GetButton("Crouch");

        // Rail switch — horizontal input while grinding
        float h = Input.GetAxis("Horizontal");
        if (Mathf.Abs(h) > 0.5f)
            TrySwitchRail(h);
    }

    // =========================================================================
    // Collision — snap leader to rail on contact
    // =========================================================================

    private void OnTriggerEnter(Collider other)
    {
        if (isGrinding) return;
        if (!other.CompareTag("Rail")) return;

        RailSpline spline = other.GetComponent<RailSpline>();
        if (spline == null) return;

        float closestT = spline.GetClosestT(transform.position);
        if (Vector3.Distance(transform.position, spline.GetPoint(closestT)) <= grindSnapRadius)
            TrySnapToRail(spline, closestT);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private float GetTeamModifier() => currentTeam switch
    {
        TeamType.Speed => speedTeamModifier,
        TeamType.Fly => flyTeamModifier,
        TeamType.Power => powerTeamModifier,
        _ => 1f
    };

    private Vector3 GetRailForward() =>
        railSpline != null ? railSpline.GetTangent(splineT).normalized : transform.forward;

    private void UpdateAnimatorState()
    {
        if (animator == null) return;
        animator.SetBool("IsGrinding", isGrinding);
        animator.SetBool("IsCrouching", isCrouching);
        animator.SetFloat("GrindSpeed", currentGrindSpeed);
    }

    // =========================================================================
    // Audio helpers
    // =========================================================================

    private void PlayOneShot(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    private void StartGrindLoop()
    {
        if (audioSource == null || grindLoopSFX == null) return;
        audioSource.clip = grindLoopSFX;
        audioSource.loop = true;
        audioSource.Play();
    }

    private void StopGrindLoop()
    {
        if (audioSource == null) return;
        audioSource.loop = false;
        audioSource.Stop();
    }

    // =========================================================================
    // Particle FX helpers
    // =========================================================================

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
}

// =============================================================================
//  RailSpline.cs  —  Catmull-Rom spline for a single grinding rail.
//  Attach to each rail GameObject and populate controlPoints in the Inspector.
// =============================================================================

public class RailSpline : MonoBehaviour
{
    [Tooltip("World-space control points (minimum 2).")]
    public Vector3[] controlPoints;

    private float cachedLength = -1f;
    private const int SAMPLE_COUNT = 100;

    void Awake() => cachedLength = ComputeLength();

    // --- Public API ----------------------------------------------------------

    /// <summary>World-space position at normalised t in [0,1].</summary>
    public Vector3 GetPoint(float t)
    {
        if (controlPoints == null || controlPoints.Length < 2) return transform.position;
        return EvaluateCatmullRom(Mathf.Clamp01(t));
    }

    /// <summary>Tangent direction (normalised) at t in [0,1].</summary>
    public Vector3 GetTangent(float t)
    {
        const float d = 0.001f;
        return (GetPoint(Mathf.Clamp01(t + d)) - GetPoint(Mathf.Clamp01(t - d))).normalized;
    }

    /// <summary>Approximate arc-length in world units.</summary>
    public float ApproximateLength() => cachedLength > 0f ? cachedLength : ComputeLength();

    /// <summary>Brute-force closest normalised T to a world position.</summary>
    public float GetClosestT(Vector3 worldPos)
    {
        float bestT = 0f, bestSq = float.MaxValue;
        for (int i = 0; i <= SAMPLE_COUNT; i++)
        {
            float t = i / (float)SAMPLE_COUNT;
            float sq = (GetPoint(t) - worldPos).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; bestT = t; }
        }
        return bestT;
    }

    // --- Catmull-Rom evaluation ----------------------------------------------

    private Vector3 EvaluateCatmullRom(float t)
    {
        int n = controlPoints.Length;
        float sc = t * (n - 1);
        int i = Mathf.Clamp(Mathf.FloorToInt(sc), 0, n - 2);
        float u = sc - i;

        Vector3 p0 = controlPoints[Mathf.Max(i - 1, 0)];
        Vector3 p1 = controlPoints[i];
        Vector3 p2 = controlPoints[Mathf.Min(i + 1, n - 1)];
        Vector3 p3 = controlPoints[Mathf.Min(i + 2, n - 1)];

        return 0.5f * (
              2f * p1
            + (-p0 + p2) * u
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u
            + (-p0 + 3f * p1 - 3f * p2 + p3) * u * u * u
        );
    }

    private float ComputeLength()
    {
        float len = 0f; Vector3 prev = GetPoint(0f);
        for (int i = 1; i <= SAMPLE_COUNT; i++)
        {
            Vector3 curr = GetPoint(i / (float)SAMPLE_COUNT);
            len += Vector3.Distance(prev, curr);
            prev = curr;
        }
        return len;
    }
}