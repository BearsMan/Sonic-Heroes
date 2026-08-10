using UnityEngine;

public class RailGrinding : MonoBehaviour
{
    #region Team Type

    public enum TeamType
    {
        Speed,
        Fly,
        Power
    }

    #endregion

    #region Grind Speed

    [Header("Grind Speed")]

    [SerializeField, Min(0.1f)]
    private float baseGrindSpeed = 18f;

    [SerializeField, Min(0.1f)]
    private float maximumGrindSpeed = 40f;

    [SerializeField, Min(0f)]
    private float minimumGrindSpeed = 2f;

    [SerializeField, Min(0f)]
    private float slopeAcceleration = 12f;

    [SerializeField, Min(0f)]
    private float crouchSpeedMultiplier = 1.25f;

    #endregion

    #region Team Modifiers

    [Header("Team Type Modifiers")]

    [SerializeField, Min(0f)]
    private float speedModifier = 1f;

    [SerializeField, Min(0f)]
    private float flyModifier = 0.85f;

    [SerializeField, Min(0f)]
    private float powerModifier = 0.75f;

    #endregion

    #region Team Formation

    [Header("Team Formation")]

    [SerializeField]
    private Transform member2;

    [SerializeField]
    private Transform member3;

    [SerializeField, Min(0f)]
    private float memberSpacing = 1.2f;

    [SerializeField, Min(0f)]
    private float memberSnapSpeed = 14f;

    #endregion

    #region Rail Detection

    [Header("Rail Detection")]

    [SerializeField]
    private LayerMask railLayers;

    [SerializeField]
    private string railTag = "Rail";

    [SerializeField, Min(0.01f)]
    private float grindSnapRadius = 1.2f;

    #endregion

    #region Rail Switching

    [Header("Rail Switching")]

    [SerializeField, Min(0.1f)]
    private float switchScanRadius = 4f;

    [SerializeField, Range(0f, 180f)]
    private float switchMaximumAngle = 45f;

    [SerializeField, Min(0f)]
    private float switchCooldown = 0.4f;

    [SerializeField, Range(0f, 1f)]
    private float railSwitchJumpMultiplier = 0.35f;

    #endregion

    #region Rail Alignment

    [Header("Rail Alignment")]

    [SerializeField, Min(0f)]
    private float snapSpeed = 20f;

    [SerializeField, Min(0f)]
    private float rotationSpeed = 720f;

    #endregion

    #region Jump

    [Header("Jump Off Rail")]

    [SerializeField, Min(0f)]
    private float railJumpForce = 12f;

    [SerializeField, Min(0f)]
    private float railJumpForwardForce = 6f;

    #endregion

    #region References

    [Header("References")]

    [SerializeField]
    private Rigidbody playerRigidbody;

    [SerializeField]
    private Animator animator;

    [SerializeField]
    private AudioSource audioSource;

    #endregion

    #region Effects

    [Header("Effects")]

    [SerializeField]
    private ParticleSystem leaderSparks;

    [SerializeField]
    private ParticleSystem member2Sparks;

    [SerializeField]
    private ParticleSystem member3Sparks;

    #endregion

    #region Audio

    [Header("Audio")]

    [SerializeField]
    private AudioClip grindLoopSound;

    [SerializeField]
    private AudioClip grindStartSound;

    [SerializeField]
    private AudioClip grindEndSound;

    [SerializeField]
    private AudioClip railSwitchSound;

    #endregion

    #region Runtime State

    private RailSpline currentRail;

    private TeamType currentTeam =
        TeamType.Speed;

    private float railPosition;
    private float grindSpeed;
    private float previousGravityStateTime;
    private float lastSwitchTime =
        float.NegativeInfinity;

    private bool isGrinding;
    private bool isCrouching;
    private bool previousGravity;

    #endregion

    #region Properties

    public bool IsGrinding =>
        isGrinding;

    public bool IsCrouching =>
        isCrouching;

    public float CurrentGrindSpeed =>
        grindSpeed;

    public float RailPosition =>
        railPosition;

    public RailSpline CurrentRail =>
        currentRail;

    public TeamType CurrentTeam =>
        currentTeam;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
        StopEffects();
    }

    private void Update()
    {
        if (!isGrinding)
        {
            return;
        }

        if (!ValidateGrindingState())
        {
            StopGrinding();

            return;
        }

        UpdateInput();
        UpdateGrindSpeed();
        AdvanceAlongRail();
        AlignLeader();
        PositionTeammates();
        UpdateAnimator();
    }

    private void OnDisable()
    {
        if (isGrinding)
        {
            StopGrinding();
        }

        StopEffects();
        StopGrindLoop();
    }

    private void OnValidate()
    {
        baseGrindSpeed =
            Mathf.Max(
                0.1f,
                baseGrindSpeed);

        maximumGrindSpeed =
            Mathf.Max(
                baseGrindSpeed,
                maximumGrindSpeed);

        minimumGrindSpeed =
            Mathf.Clamp(
                minimumGrindSpeed,
                0f,
                maximumGrindSpeed);

        slopeAcceleration =
            Mathf.Max(
                0f,
                slopeAcceleration);

        crouchSpeedMultiplier =
            Mathf.Max(
                0f,
                crouchSpeedMultiplier);

        speedModifier =
            Mathf.Max(
                0f,
                speedModifier);

        flyModifier =
            Mathf.Max(
                0f,
                flyModifier);

        powerModifier =
            Mathf.Max(
                0f,
                powerModifier);

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
                0.01f,
                grindSnapRadius);

        switchScanRadius =
            Mathf.Max(
                0.1f,
                switchScanRadius);

        switchCooldown =
            Mathf.Max(
                0f,
                switchCooldown);

        snapSpeed =
            Mathf.Max(
                0f,
                snapSpeed);

        rotationSpeed =
            Mathf.Max(
                0f,
                rotationSpeed);

        railJumpForce =
            Mathf.Max(
                0f,
                railJumpForce);

        railJumpForwardForce =
            Mathf.Max(
                0f,
                railJumpForwardForce);
    }

    #endregion

    #region Trigger Detection

    private void OnTriggerEnter(
        Collider other)
    {
        if (isGrinding ||
            other == null)
        {
            return;
        }

        if (!IsRailCollider(
                other))
        {
            return;
        }

        RailSpline rail =
            other.GetComponent<RailSpline>();

        rail ??=
            other.GetComponentInParent<RailSpline>();

        if (rail == null)
        {
            return;
        }

        TrySnapToRail(
            rail);
    }

    #endregion

    #region Public API

    public void SetTeamType(
        TeamType team)
    {
        currentTeam =
            team;
    }

    public bool TrySnapToRail(
        RailSpline rail)
    {
        if (rail == null)
        {
            return false;
        }

        float closestPosition =
            rail.GetClosestT(
                transform.position);

        return
            TrySnapToRail(
                rail,
                closestPosition);
    }

    public bool TrySnapToRail(
        RailSpline rail,
        float contactT)
    {
        if (isGrinding ||
            rail == null)
        {
            return false;
        }

        float targetT =
            Mathf.Clamp01(
                contactT);

        Vector3 railPoint =
            rail.GetPoint(
                targetT);

        if (!IsFiniteVector(
                railPoint))
        {
            return false;
        }

        float distance =
            Vector3.Distance(
                transform.position,
                railPoint);

        if (!float.IsFinite(
                distance) ||
            distance >
                grindSnapRadius)
        {
            return false;
        }

        currentRail =
            rail;

        railPosition =
            targetT;

        Vector3 railForward =
            GetRailForward();

        float inheritedSpeed =
            0f;

        if (playerRigidbody != null &&
            IsFiniteVector(
                playerRigidbody.linearVelocity))
        {
            inheritedSpeed =
                Mathf.Abs(
                    Vector3.Dot(
                        playerRigidbody.linearVelocity,
                        railForward));
        }

        grindSpeed =
            Mathf.Max(
                baseGrindSpeed *
                    GetTeamModifier(),
                inheritedSpeed);

        grindSpeed =
            Mathf.Clamp(
                grindSpeed,
                minimumGrindSpeed,
                maximumGrindSpeed);

        StartGrinding();

        return true;
    }

    public void StopRailGrinding()
    {
        StopGrinding();
    }

    #endregion

    #region Start And Stop

    private void StartGrinding()
    {
        if (currentRail == null)
        {
            return;
        }

        isGrinding =
            true;

        isCrouching =
            false;

        if (playerRigidbody != null)
        {
            previousGravity =
                playerRigidbody.useGravity;

            playerRigidbody.useGravity =
                false;

            playerRigidbody.linearVelocity =
                Vector3.zero;

            playerRigidbody.angularVelocity =
                Vector3.zero;
        }

        PlaySound(
            grindStartSound);

        StartGrindLoop();
        StartEffects();

        UpdateAnimator();
    }

    private void StopGrinding(
        bool jumped = false)
    {
        if (!isGrinding)
        {
            return;
        }

        Vector3 exitDirection =
            GetRailForward();

        isGrinding =
            false;

        isCrouching =
            false;

        if (playerRigidbody != null)
        {
            playerRigidbody.useGravity =
                previousGravity;

            playerRigidbody.angularVelocity =
                Vector3.zero;

            if (jumped)
            {
                Vector3 exitVelocity =
                    exitDirection *
                        (
                            grindSpeed +
                            railJumpForwardForce
                        ) +
                    Vector3.up *
                        railJumpForce;

                if (IsFiniteVector(
                        exitVelocity))
                {
                    playerRigidbody.linearVelocity =
                        exitVelocity;
                }
            }
        }

        StopEffects();
        StopGrindLoop();

        PlaySound(
            grindEndSound);

        if (animator != null &&
            animator.isActiveAndEnabled)
        {
            animator.SetBool(
                "IsGrinding",
                false);

            animator.SetBool(
                "IsCrouching",
                false);
        }

        currentRail =
            null;

        railPosition =
            0f;
    }

    #endregion

    #region Input

    private void UpdateInput()
    {
        if (Input.GetButtonDown(
                "Jump"))
        {
            StopGrinding(
                true);

            return;
        }

        isCrouching =
            Input.GetButton(
                "Crouch");

        float horizontal =
            Input.GetAxis(
                "Horizontal");

        if (Mathf.Abs(
                horizontal) >
            0.5f)
        {
            TrySwitchRail(
                horizontal);
        }
    }

    #endregion

    #region Grind Speed

    private void UpdateGrindSpeed()
    {
        if (currentRail == null)
        {
            return;
        }

        Vector3 tangent =
            currentRail.GetTangent(
                railPosition);

        if (!IsFiniteVector(
                tangent) ||
            tangent.sqrMagnitude <=
                0.0001f)
        {
            return;
        }

        float slope =
            Vector3.Dot(
                tangent.normalized,
                Vector3.down);

        grindSpeed +=
            slope *
            slopeAcceleration *
            Time.deltaTime;

        if (isCrouching)
        {
            grindSpeed +=
                baseGrindSpeed *
                (
                    crouchSpeedMultiplier -
                    1f
                ) *
                Time.deltaTime;
        }

        grindSpeed =
            Mathf.Clamp(
                grindSpeed,
                0f,
                maximumGrindSpeed);

        if (grindSpeed <
            minimumGrindSpeed)
        {
            StopGrinding();
        }
    }

    #endregion

    #region Rail Movement

    private void AdvanceAlongRail()
    {
        if (currentRail == null)
        {
            return;
        }

        float railLength =
            currentRail.ApproximateLength();

        if (!float.IsFinite(
                railLength) ||
            railLength <= 0f)
        {
            StopGrinding();

            return;
        }

        railPosition +=
            (
                grindSpeed *
                Time.deltaTime
            ) /
            railLength;

        if (railPosition >= 1f)
        {
            railPosition =
                1f;

            AlignLeader();

            StopGrinding();
        }
    }

    private void AlignLeader()
    {
        if (currentRail == null)
        {
            return;
        }

        Vector3 targetPosition =
            currentRail.GetPoint(
                railPosition);

        Vector3 tangent =
            currentRail.GetTangent(
                railPosition);

        if (!IsFiniteVector(
                targetPosition))
        {
            return;
        }

        Vector3 nextPosition =
            Vector3.Lerp(
                transform.position,
                targetPosition,
                Mathf.Clamp01(
                    snapSpeed *
                    Time.deltaTime));

        if (IsFiniteVector(
                nextPosition))
        {
            if (playerRigidbody != null &&
                !playerRigidbody.isKinematic)
            {
                playerRigidbody.MovePosition(
                    nextPosition);
            }
            else
            {
                transform.position =
                    nextPosition;
            }
        }

        RotateAlongRail(
            tangent);
    }

    private void RotateAlongRail(
        Vector3 direction)
    {
        if (!IsFiniteVector(
                direction) ||
            direction.sqrMagnitude <=
                0.0001f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);

        Quaternion rotation =
            Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed *
                    Time.deltaTime);

        if (playerRigidbody != null &&
            !playerRigidbody.isKinematic)
        {
            playerRigidbody.MoveRotation(
                rotation);
        }
        else
        {
            transform.rotation =
                rotation;
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

        PlaceMember(
            member2,
            memberSpacing);

        PlaceMember(
            member3,
            memberSpacing * 2f);
    }

    private void PlaceMember(
        Transform member,
        float distanceBehind)
    {
        if (member == null ||
            currentRail == null)
        {
            return;
        }

        float railLength =
            currentRail.ApproximateLength();

        if (!float.IsFinite(
                railLength) ||
            railLength <= 0f)
        {
            return;
        }

        float memberT =
            Mathf.Clamp01(
                railPosition -
                distanceBehind /
                    railLength);

        Vector3 targetPosition =
            currentRail.GetPoint(
                memberT);

        Vector3 tangent =
            currentRail.GetTangent(
                memberT);

        if (!IsFiniteVector(
                targetPosition))
        {
            return;
        }

        member.position =
            Vector3.Lerp(
                member.position,
                targetPosition,
                Mathf.Clamp01(
                    memberSnapSpeed *
                    Time.deltaTime));

        if (IsFiniteVector(
                tangent) &&
            tangent.sqrMagnitude >
                0.0001f)
        {
            member.rotation =
                Quaternion.LookRotation(
                    tangent.normalized,
                    Vector3.up);
        }
    }

    #endregion

    #region Rail Switching

    private void TrySwitchRail(
        float horizontalInput)
    {
        if (currentRail == null ||
            Time.time -
                lastSwitchTime <
                switchCooldown)
        {
            return;
        }

        Vector3 position =
            transform.position;

        Vector3 currentForward =
            GetRailForward();

        Vector3 lateralDirection =
            Vector3.Cross(
                Vector3.up,
                currentForward)
            .normalized *
            Mathf.Sign(
                horizontalInput);

        Collider[] nearbyRails =
            Physics.OverlapSphere(
                position,
                switchScanRadius,
                railLayers,
                QueryTriggerInteraction.Collide);

        RailSpline bestRail =
            null;

        float bestT =
            0f;

        float bestScore =
            float.PositiveInfinity;

        foreach (Collider candidateCollider
            in nearbyRails)
        {
            if (candidateCollider == null)
            {
                continue;
            }

            RailSpline candidate =
                candidateCollider
                    .GetComponent<RailSpline>();

            candidate ??=
                candidateCollider
                    .GetComponentInParent<RailSpline>();

            if (candidate == null ||
                candidate ==
                    currentRail)
            {
                continue;
            }

            float candidateT =
                candidate.GetClosestT(
                    position);

            Vector3 candidatePosition =
                candidate.GetPoint(
                    candidateT);

            Vector3 candidateTangent =
                candidate.GetTangent(
                    candidateT);

            if (!IsFiniteVector(
                    candidatePosition) ||
                !IsFiniteVector(
                    candidateTangent))
            {
                continue;
            }

            float distance =
                Vector3.Distance(
                    position,
                    candidatePosition);

            if (!float.IsFinite(
                    distance) ||
                distance >
                    switchScanRadius)
            {
                continue;
            }

            float angle =
                Vector3.Angle(
                    currentForward,
                    candidateTangent);

            if (angle >
                switchMaximumAngle)
            {
                continue;
            }

            Vector3 toCandidate =
                candidatePosition -
                position;

            if (toCandidate.sqrMagnitude >
                0.0001f)
            {
                toCandidate.Normalize();
            }

            float sidePreference =
                Vector3.Dot(
                    toCandidate,
                    lateralDirection);

            if (sidePreference <= 0f)
            {
                continue;
            }

            float score =
                distance -
                sidePreference *
                    switchScanRadius;

            if (score >=
                bestScore)
            {
                continue;
            }

            bestScore =
                score;

            bestRail =
                candidate;

            bestT =
                candidateT;
        }

        if (bestRail == null)
        {
            return;
        }

        currentRail =
            bestRail;

        railPosition =
            bestT;

        lastSwitchTime =
            Time.time;

        PlaySound(
            railSwitchSound);

        if (playerRigidbody != null)
        {
            Vector3 velocity =
                GetRailForward() *
                    grindSpeed +
                Vector3.up *
                    railJumpForce *
                    railSwitchJumpMultiplier;

            if (IsFiniteVector(
                    velocity))
            {
                playerRigidbody.linearVelocity =
                    velocity;
            }
        }
    }

    #endregion

    #region Rail Validation

    private bool ValidateGrindingState()
    {
        if (currentRail == null)
        {
            return false;
        }

        Vector3 currentPoint =
            currentRail.GetPoint(
                railPosition);

        Vector3 tangent =
            currentRail.GetTangent(
                railPosition);

        return
            IsFiniteVector(
                currentPoint) &&
            IsFiniteVector(
                tangent);
    }

    private bool IsRailCollider(
        Collider candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(
                railTag) &&
            candidate.CompareTag(
                railTag))
        {
            return true;
        }

        return
            candidate.GetComponent<RailSpline>() !=
                null ||
            candidate.GetComponentInParent<RailSpline>() !=
                null;
    }

    #endregion

    #region Team Modifier

    private float GetTeamModifier()
    {
        return currentTeam switch
        {
            TeamType.Speed =>
                speedModifier,

            TeamType.Fly =>
                flyModifier,

            TeamType.Power =>
                powerModifier,

            _ =>
                1f
        };
    }

    #endregion

    #region Rail Direction

    private Vector3 GetRailForward()
    {
        if (currentRail == null)
        {
            return
                transform.forward;
        }

        Vector3 tangent =
            currentRail.GetTangent(
                railPosition);

        if (!IsFiniteVector(
                tangent) ||
            tangent.sqrMagnitude <=
                0.0001f)
        {
            return
                transform.forward;
        }

        return
            tangent.normalized;
    }

    #endregion

    #region References

    private void ResolveReferences()
    {
        playerRigidbody ??=
            GetComponent<Rigidbody>();

        animator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        audioSource ??=
            GetComponent<AudioSource>();

        audioSource ??=
            GetComponentInChildren<AudioSource>(
                includeInactive: true);
    }

    #endregion

    #region Animation

    private void UpdateAnimator()
    {
        if (animator == null ||
            !animator.isActiveAndEnabled ||
            animator.runtimeAnimatorController ==
                null)
        {
            return;
        }

        SetAnimatorBool(
            "IsGrinding",
            isGrinding);

        SetAnimatorBool(
            "IsCrouching",
            isCrouching);

        SetAnimatorFloat(
            "GrindSpeed",
            grindSpeed);
    }

    private void SetAnimatorBool(
        string parameterName,
        bool value)
    {
        foreach (
            AnimatorControllerParameter parameter
            in animator.parameters)
        {
            if (parameter.name !=
                    parameterName ||
                parameter.type !=
                    AnimatorControllerParameterType.Bool)
            {
                continue;
            }

            animator.SetBool(
                parameterName,
                value);

            return;
        }
    }

    private void SetAnimatorFloat(
        string parameterName,
        float value)
    {
        foreach (
            AnimatorControllerParameter parameter
            in animator.parameters)
        {
            if (parameter.name !=
                    parameterName ||
                parameter.type !=
                    AnimatorControllerParameterType.Float)
            {
                continue;
            }

            animator.SetFloat(
                parameterName,
                value);

            return;
        }
    }

    #endregion

    #region Audio

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

    private void StartGrindLoop()
    {
        if (audioSource == null ||
            grindLoopSound == null)
        {
            return;
        }

        audioSource.clip =
            grindLoopSound;

        audioSource.loop =
            true;

        audioSource.Play();
    }

    private void StopGrindLoop()
    {
        if (audioSource == null)
        {
            return;
        }

        audioSource.loop =
            false;

        if (audioSource.clip ==
            grindLoopSound)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }
    }

    #endregion

    #region Effects

    private void StartEffects()
    {
        PlayEffect(
            leaderSparks);

        PlayEffect(
            member2Sparks);

        PlayEffect(
            member3Sparks);
    }

    private void StopEffects()
    {
        StopEffect(
            leaderSparks);

        StopEffect(
            member2Sparks);

        StopEffect(
            member3Sparks);
    }

    private static void PlayEffect(
        ParticleSystem effect)
    {
        if (effect != null &&
            !effect.isPlaying)
        {
            effect.Play();
        }
    }

    private static void StopEffect(
        ParticleSystem effect)
    {
        if (effect != null &&
            effect.isPlaying)
        {
            effect.Stop();
        }
    }

    #endregion

    #region Validation

    private static bool IsFiniteVector(
        Vector3 value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }

    #endregion
}