using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class LightSpeedDash : MonoBehaviour
{
    #region Types

    private enum DashState
    {
        Ready,
        Straight,
        RingChain,
        Cooldown
    }

    #endregion

    #region Constants

    private const int RingQueryCapacity = 32;
    private const int CollisionQueryCapacity = 24;
    private const int DamageQueryCapacity = 48;

    #endregion

    #region Inspector

    [Header("Dependencies")]
    [SerializeField] private UltimatePlayerMovement playerMovement;
    [SerializeField] private TeamActionController teamActions;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private AudioSource audioSource;

    [Header("Input")]
    [SerializeField] private bool acceptInput = true;
    [SerializeField] private KeyCode dashKey = KeyCode.B;

    [Header("Dash Selection")]
    [SerializeField] private bool useRingTrails = true;
    [SerializeField] private LayerMask ringMask;
    [SerializeField, Min(0.1f)] private float ringAcquisitionRadius = 8f;

    [Header("Straight Dash")]
    [SerializeField, Min(0.1f)] private float straightSpeed = 70f;
    [SerializeField, Min(0.1f)] private float straightDistance = 18f;
    [SerializeField, Min(0.05f)] private float straightDuration = 0.35f;

    [Header("Ring Dash")]
    [SerializeField, Min(0.1f)] private float ringSpeed = 55f;
    [SerializeField, Min(0.1f)] private float ringLinkRadius = 5f;
    [SerializeField, Min(0f)] private float ringArrivalRadius = 0.15f;
    [SerializeField, Min(0.1f)] private float ringChainDuration = 4f;
    [SerializeField, Range(-1f, 1f)] private float minimumDirectionAlignment = -0.2f;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float rotationSpeed = 1080f;
    [SerializeField, Min(0f)] private float exitSpeed = 14f;
    [SerializeField] private bool flattenStraightDirection = true;

    [Header("Collision")]
    [SerializeField, Min(0.05f)] private float bodyRadius = 0.65f;
    [SerializeField, Min(0f)] private float collisionPadding = 0.05f;
    [SerializeField] private LayerMask obstacleMask = ~0;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float damage = 25f;
    [SerializeField] private LayerMask damageMask = ~0;
    [SerializeField] private bool breakDamageableObjects = true;

    [Header("Cooldown")]
    [SerializeField, Min(0f)] private float cooldown = 0.5f;

    [Header("Presentation")]
    [SerializeField] private GameObject dashEffect;
    [SerializeField] private TrailRenderer dashTrail;
    [SerializeField] private AudioClip beginSound;
    [SerializeField] private AudioClip ringSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip endSound;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private readonly Collider[] ringQuery =
        new Collider[RingQueryCapacity];

    private readonly RaycastHit[] collisionQuery =
        new RaycastHit[CollisionQueryCapacity];

    private readonly Collider[] damageQuery =
        new Collider[DamageQueryCapacity];

    private readonly HashSet<LightSpeedDashRing> usedRings =
        new();

    private readonly HashSet<GameObject> damagedObjects =
        new();

    private Coroutine dashRoutine;
    private LightSpeedDashRing targetRing;

    private DashState state =
        DashState.Ready;

    private Vector3 dashDirection;

    private float cooldownEndTime;

    private bool savedGravity;
    private bool savedKinematic;
    private bool ownsDirectMovementLock;
    private bool initialized;
    private bool shuttingDown;

    #endregion

    #region Public API

    public bool IsDashing =>
        state == DashState.Straight ||
        state == DashState.RingChain;

    public bool IsFollowingRings =>
        state == DashState.RingChain;

    public bool IsInitialized =>
        initialized;

    public bool CanStart =>
        initialized &&
        state == DashState.Ready &&
        Time.time >= cooldownEndTime;

    public void SetInputEnabled(
        bool enabled)
    {
        acceptInput = enabled;
    }

    public bool StartDash()
    {
        if (!CanStart)
            return false;

        ResolveDependencies();

        if (!ValidateRuntimeReferences())
            return false;

        if (!IsSpeedFormationActive())
            return false;

        LightSpeedDashRing firstRing =
            useRingTrails
                ? FindNextRing(
                    playerRigidbody.position,
                    ringAcquisitionRadius,
                    null)
                : null;

        if (!TakeControl())
            return false;

        BeginDash(firstRing);
        return true;
    }

    public void CancelDash()
    {
        EndDash(
            preserveMomentum: false,
            stopCoroutine: true);
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveDependencies();
        HidePresentation();
    }

    private void Start()
    {
        initialized =
            ValidateConfiguration();

        if (!initialized)
        {
            Debug.LogError(
                $"LightSpeedDash could not initialize on '{name}'.",
                this);

            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (shuttingDown)
            return;

        ResolveDependencies();

        if (!IsDashing)
        {
            HidePresentation();
        }
    }

    private void Update()
    {
        UpdateCooldownState();

        if (!acceptInput ||
            !CanStart)
        {
            return;
        }

        if (Input.GetKeyDown(dashKey))
        {
            StartDash();
        }
    }

    private void OnDisable()
    {
        CancelDash();
        HidePresentation();
    }

    private void OnDestroy()
    {
        shuttingDown = true;

        CancelDash();

        playerMovement = null;
        teamActions = null;
        playerRigidbody = null;
        audioSource = null;

        dashEffect = null;
        dashTrail = null;
    }

    private void OnValidate()
    {
        ringAcquisitionRadius =
            Mathf.Max(
                0.1f,
                ringAcquisitionRadius);

        straightSpeed =
            Mathf.Max(
                0.1f,
                straightSpeed);

        straightDistance =
            Mathf.Max(
                0.1f,
                straightDistance);

        straightDuration =
            Mathf.Max(
                0.05f,
                straightDuration);

        ringSpeed =
            Mathf.Max(
                0.1f,
                ringSpeed);

        ringLinkRadius =
            Mathf.Max(
                0.1f,
                ringLinkRadius);

        ringArrivalRadius =
            Mathf.Max(
                0f,
                ringArrivalRadius);

        ringChainDuration =
            Mathf.Max(
                0.1f,
                ringChainDuration);

        minimumDirectionAlignment =
            Mathf.Clamp(
                minimumDirectionAlignment,
                -1f,
                1f);

        rotationSpeed =
            Mathf.Max(
                0f,
                rotationSpeed);

        exitSpeed =
            Mathf.Max(
                0f,
                exitSpeed);

        bodyRadius =
            Mathf.Max(
                0.05f,
                bodyRadius);

        collisionPadding =
            Mathf.Max(
                0f,
                collisionPadding);

        damage =
            Mathf.Max(
                0f,
                damage);

        cooldown =
            Mathf.Max(
                0f,
                cooldown);
    }

    #endregion

    #region Dependency Resolution

    private void ResolveDependencies()
    {
        playerMovement ??=
            GetComponent<UltimatePlayerMovement>();

        playerMovement ??=
            GetComponentInParent<UltimatePlayerMovement>();

        teamActions ??=
            GetComponent<TeamActionController>();

        teamActions ??=
            GetComponentInParent<TeamActionController>();

        playerRigidbody ??=
            GetComponent<Rigidbody>();

        if (playerRigidbody == null &&
            playerMovement != null)
        {
            playerRigidbody =
                playerMovement.GetComponent<Rigidbody>();
        }

        audioSource ??=
            GetComponent<AudioSource>();

        audioSource ??=
            GetComponentInParent<AudioSource>();
    }

    #endregion

    #region Activation

    private bool IsSpeedFormationActive()
    {
        if (teamActions == null)
            return false;

        return
            teamActions.CurrentFormation ==
            TeamActionController.TeamFormation.Speed;
    }

    private bool TakeControl()
    {
        ownsDirectMovementLock = false;

        if (teamActions != null)
        {
            return
                teamActions.TryBeginAction(
                    TeamActionController.TeamAction.LightDash,
                    TeamActionController.TeamFormation.Speed,
                    mustBeGrounded: false,
                    mustBeAirborne: false,
                    surrenderMovementControl: true);
        }

        if (playerMovement == null)
            return false;

        playerMovement.DisableMovement();
        ownsDirectMovementLock = true;

        return true;
    }

    private void ReleaseControl()
    {
        if (teamActions != null &&
            teamActions.CurrentAction ==
            TeamActionController.TeamAction.LightDash)
        {
            teamActions.EndAction(
                restoreMovementControl: true);
        }
        else if (ownsDirectMovementLock &&
                 playerMovement != null)
        {
            playerMovement.EnableMovement();
        }

        ownsDirectMovementLock = false;
    }

    private void BeginDash(
        LightSpeedDashRing firstRing)
    {
        targetRing = firstRing;

        state =
            firstRing != null
                ? DashState.RingChain
                : DashState.Straight;

        usedRings.Clear();
        damagedObjects.Clear();

        dashDirection =
            BuildInitialDirection();

        SaveAndOverridePhysics();

        ShowPresentation();
        PlaySound(beginSound);

        if (state == DashState.RingChain)
        {
            PlaySound(ringSound);
        }

        dashRoutine =
            StartCoroutine(
                state == DashState.RingChain
                    ? RunRingChain()
                    : RunStraightDash());

        Log(
            state == DashState.RingChain
                ? "Started ring-chain dash."
                : "Started straight dash.");
    }

    private void EndDash(
        bool preserveMomentum,
        bool stopCoroutine)
    {
        if (!IsDashing)
            return;

        if (stopCoroutine &&
            dashRoutine != null)
        {
            StopCoroutine(
                dashRoutine);
        }

        dashRoutine = null;
        targetRing = null;

        RestorePhysics(
            preserveMomentum);

        HidePresentation();
        PlaySound(endSound);
        ReleaseControl();

        usedRings.Clear();
        damagedObjects.Clear();

        cooldownEndTime =
            Time.time +
            cooldown;

        state =
            cooldown > 0f
                ? DashState.Cooldown
                : DashState.Ready;

        Log("Light Speed Dash ended.");
    }

    private void UpdateCooldownState()
    {
        if (state != DashState.Cooldown)
            return;

        if (Time.time <
            cooldownEndTime)
        {
            return;
        }

        state =
            DashState.Ready;
    }

    #endregion

    #region Straight Dash

    private IEnumerator RunStraightDash()
    {
        float elapsed = 0f;
        float travelled = 0f;

        WaitForFixedUpdate wait =
            new();

        while (state == DashState.Straight &&
               elapsed < straightDuration &&
               travelled < straightDistance)
        {
            float requestedDistance =
                Mathf.Min(
                    straightSpeed *
                    Time.fixedDeltaTime,
                    straightDistance -
                    travelled);

            Vector3 startPosition =
                playerRigidbody.position;

            float allowedDistance =
                CalculateSafeDistance(
                    startPosition,
                    dashDirection,
                    requestedDistance);

            Vector3 endPosition =
                startPosition +
                dashDirection *
                allowedDistance;

            MovePlayer(
                startPosition,
                endPosition);

            travelled +=
                allowedDistance;

            elapsed +=
                Time.fixedDeltaTime;

            if (allowedDistance <
                requestedDistance)
            {
                EndDash(
                    preserveMomentum: false,
                    stopCoroutine: false);

                yield break;
            }

            yield return wait;
        }

        EndDash(
            preserveMomentum: true,
            stopCoroutine: false);
    }

    #endregion

    #region Ring Chain

    private IEnumerator RunRingChain()
    {
        float elapsed = 0f;

        WaitForFixedUpdate wait =
            new();

        while (state == DashState.RingChain &&
               elapsed < ringChainDuration)
        {
            if (!CanUseRing(targetRing))
            {
                targetRing =
                    FindNextRing(
                        playerRigidbody.position,
                        ringLinkRadius,
                        dashDirection);

                if (targetRing == null)
                {
                    EndDash(
                        preserveMomentum: true,
                        stopCoroutine: false);

                    yield break;
                }
            }

            Vector3 targetPosition =
                targetRing.DashPosition;

            Vector3 offset =
                targetPosition -
                playerRigidbody.position;

            float distance =
                offset.magnitude;

            if (distance <=
                ringArrivalRadius)
            {
                ArriveAtRing(
                    targetPosition);

                yield return wait;
                continue;
            }

            Vector3 direction =
                offset /
                distance;

            dashDirection =
                direction;

            RotatePlayer(
                direction);

            float requestedDistance =
                Mathf.Min(
                    ringSpeed *
                    Time.fixedDeltaTime,
                    distance);

            Vector3 startPosition =
                playerRigidbody.position;

            float allowedDistance =
                CalculateSafeDistance(
                    startPosition,
                    direction,
                    requestedDistance);

            Vector3 endPosition =
                startPosition +
                direction *
                allowedDistance;

            MovePlayer(
                startPosition,
                endPosition);

            if (allowedDistance <
                requestedDistance)
            {
                EndDash(
                    preserveMomentum: false,
                    stopCoroutine: false);

                yield break;
            }

            elapsed +=
                Time.fixedDeltaTime;

            yield return wait;
        }

        EndDash(
            preserveMomentum: true,
            stopCoroutine: false);
    }

    private void ArriveAtRing(
        Vector3 ringPosition)
    {
        playerRigidbody.position =
            ringPosition;

        DamagePath(
            ringPosition,
            ringPosition);

        usedRings.Add(
            targetRing);

        targetRing =
            FindNextRing(
                ringPosition,
                ringLinkRadius,
                dashDirection);
    }

    private LightSpeedDashRing FindNextRing(
        Vector3 origin,
        float radius,
        Vector3? preferredDirection)
    {
        int count =
            Physics.OverlapSphereNonAlloc(
                origin,
                radius,
                ringQuery,
                ringMask,
                QueryTriggerInteraction.Collide);

        LightSpeedDashRing bestRing =
            null;

        float bestScore =
            float.MaxValue;

        for (int index = 0;
             index < count;
             index++)
        {
            Collider candidateCollider =
                ringQuery[index];

            if (candidateCollider == null)
                continue;

            LightSpeedDashRing candidate =
                candidateCollider
                    .GetComponentInParent<LightSpeedDashRing>();

            if (!CanUseRing(candidate) ||
                usedRings.Contains(candidate))
            {
                continue;
            }

            Vector3 difference =
                candidate.DashPosition -
                origin;

            float distance =
                difference.magnitude;

            if (distance <=
                ringArrivalRadius)
            {
                continue;
            }

            float allowedDistance =
                Mathf.Min(
                    radius,
                    candidate.ChainRange);

            if (distance >
                allowedDistance)
            {
                continue;
            }

            float score =
                distance;

            if (preferredDirection.HasValue)
            {
                float alignment =
                    Vector3.Dot(
                        preferredDirection.Value.normalized,
                        difference.normalized);

                if (alignment <
                    minimumDirectionAlignment)
                {
                    continue;
                }

                score -=
                    alignment *
                    allowedDistance *
                    0.5f;
            }

            if (score >=
                bestScore)
            {
                continue;
            }

            bestScore =
                score;

            bestRing =
                candidate;
        }

        return bestRing;
    }

    private static bool CanUseRing(
        LightSpeedDashRing ring)
    {
        return
            ring != null &&
            ring.CanBeDashedThrough;
    }

    #endregion

    #region Physics Movement

    private void SaveAndOverridePhysics()
    {
        savedGravity =
            playerRigidbody.useGravity;

        savedKinematic =
            playerRigidbody.isKinematic;

        playerRigidbody.linearVelocity =
            Vector3.zero;

        playerRigidbody.angularVelocity =
            Vector3.zero;

        playerRigidbody.useGravity =
            false;

        playerRigidbody.isKinematic =
            true;
    }

    private void RestorePhysics(
        bool preserveMomentum)
    {
        if (playerRigidbody == null)
            return;

        playerRigidbody.isKinematic =
            savedKinematic;

        playerRigidbody.useGravity =
            savedGravity;

        if (playerRigidbody.isKinematic)
            return;

        playerRigidbody.linearVelocity =
            preserveMomentum
                ? dashDirection *
                  exitSpeed
                : Vector3.zero;
    }

    private void MovePlayer(
        Vector3 startPosition,
        Vector3 endPosition)
    {
        playerRigidbody.MovePosition(
            endPosition);

        DamagePath(
            startPosition,
            endPosition);
    }

    private void RotatePlayer(
        Vector3 direction)
    {
        if (direction.sqrMagnitude <=
            0.0001f)
        {
            return;
        }

        Quaternion desiredRotation =
            Quaternion.LookRotation(
                direction,
                Vector3.up);

        playerRigidbody.MoveRotation(
            Quaternion.RotateTowards(
                playerRigidbody.rotation,
                desiredRotation,
                rotationSpeed *
                Time.fixedDeltaTime));
    }

    private float CalculateSafeDistance(
        Vector3 origin,
        Vector3 direction,
        float requestedDistance)
    {
        int count =
            Physics.SphereCastNonAlloc(
                origin,
                bodyRadius,
                direction,
                collisionQuery,
                requestedDistance,
                obstacleMask,
                QueryTriggerInteraction.Ignore);

        float safeDistance =
            requestedDistance;

        for (int index = 0;
             index < count;
             index++)
        {
            RaycastHit hit =
                collisionQuery[index];

            if (hit.collider == null ||
                IsTeamObject(
                    hit.collider.gameObject))
            {
                continue;
            }

            safeDistance =
                Mathf.Min(
                    safeDistance,
                    Mathf.Max(
                        0f,
                        hit.distance -
                        collisionPadding));
        }

        return safeDistance;
    }

    private Vector3 BuildInitialDirection()
    {
        Vector3 direction =
            transform.forward;

        if (flattenStraightDirection)
        {
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <=
            0.0001f)
        {
            direction =
                Vector3.forward;
        }

        return direction.normalized;
    }

    #endregion

    #region Damage

    private void DamagePath(
        Vector3 startPosition,
        Vector3 endPosition)
    {
        Vector3 difference =
            endPosition -
            startPosition;

        float length =
            difference.magnitude;

        Vector3 center =
            startPosition +
            difference *
            0.5f;

        Vector3 extents =
            new(
                bodyRadius,
                bodyRadius,
                length *
                0.5f +
                bodyRadius);

        Quaternion rotation =
            length > 0.0001f
                ? Quaternion.LookRotation(
                    difference.normalized,
                    Vector3.up)
                : transform.rotation;

        int count =
            Physics.OverlapBoxNonAlloc(
                center,
                extents,
                damageQuery,
                rotation,
                damageMask,
                QueryTriggerInteraction.Collide);

        for (int index = 0;
             index < count;
             index++)
        {
            ApplyDamage(
                damageQuery[index]);
        }
    }

    private void ApplyDamage(
        Collider hitCollider)
    {
        if (hitCollider == null)
            return;

        GameObject target =
            hitCollider.attachedRigidbody != null
                ? hitCollider.attachedRigidbody.gameObject
                : hitCollider.gameObject;

        if (IsTeamObject(target) ||
            !damagedObjects.Add(target))
        {
            return;
        }

        target.SendMessage(
            "TakeDamage",
            damage,
            SendMessageOptions.DontRequireReceiver);

        if (breakDamageableObjects)
        {
            target.SendMessage(
                "Break",
                SendMessageOptions.DontRequireReceiver);
        }

        PlaySound(hitSound);
    }

    private bool IsTeamObject(
        GameObject target)
    {
        if (target == null)
            return true;

        Transform targetTransform =
            target.transform;

        if (target == gameObject ||
            targetTransform.IsChildOf(transform) ||
            transform.IsChildOf(targetTransform))
        {
            return true;
        }

        if (teamActions == null)
            return false;

        return
            BelongsToCharacter(
                targetTransform,
                teamActions.SpeedCharacter) ||
            BelongsToCharacter(
                targetTransform,
                teamActions.FlyCharacter) ||
            BelongsToCharacter(
                targetTransform,
                teamActions.PowerCharacter);
    }

    private static bool BelongsToCharacter(
        Transform target,
        Transform character)
    {
        return
            target != null &&
            character != null &&
            (target == character ||
             target.IsChildOf(character) ||
             character.IsChildOf(target));
    }

    #endregion

    #region Presentation

    private void ShowPresentation()
    {
        if (dashEffect != null)
        {
            dashEffect.SetActive(true);
        }

        if (dashTrail != null)
        {
            dashTrail.emitting = true;
        }
    }

    private void HidePresentation()
    {
        if (dashEffect != null)
        {
            dashEffect.SetActive(false);
        }

        if (dashTrail != null)
        {
            dashTrail.emitting = false;
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

    #region Validation

    private bool ValidateConfiguration()
    {
        bool valid = true;

        valid &=
            ValidateRequiredReference(
                playerMovement,
                nameof(UltimatePlayerMovement));

        valid &=
            ValidateRequiredReference(
                playerRigidbody,
                nameof(Rigidbody));

        valid &=
            ValidateRequiredReference(
                teamActions,
                nameof(TeamActionController));

        if (ringMask.value == 0)
        {
            Debug.LogWarning(
                "LightSpeedDash ring mask is empty.",
                this);
        }

        return valid;
    }

    private bool ValidateRuntimeReferences()
    {
        return
            playerMovement != null &&
            playerRigidbody != null &&
            teamActions != null;
    }

    private bool ValidateRequiredReference(
        Object reference,
        string displayName)
    {
        if (reference != null)
            return true;

        Debug.LogError(
            $"LightSpeedDash requires {displayName}.",
            this);

        return false;
    }

    #endregion

    #region Debug

    private void Log(
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