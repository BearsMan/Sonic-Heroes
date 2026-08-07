using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ThunderShoot : MonoBehaviour
{
    #region Types

    public enum ThunderShootState
    {
        Uninitialized,
        Ready,
        Preparing,
        Flying,
        Returning,
        Cooldown,
        Disabled
    }

    #endregion

    #region Constants

    private const string DefaultShootPointName =
        "Shoot Point";

    private const string DefaultPowerReturnPointName =
        "RightPos";

    #endregion

    #region Inspector

    [Header("Core References")]
    [SerializeField] private UltimatePlayerMovement movement;
    [SerializeField] private TeamActionController actionController;
    [SerializeField] private Transform flyCharacter;
    [SerializeField] private Transform powerTeammate;
    [SerializeField] private Transform powerReturnPoint;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private Camera playerCamera;

    [Header("Follower Control")]
    [SerializeField]
    private Behaviour[] powerFollowerBehaviours =
        Array.Empty<Behaviour>();

    [Header("Input")]
    [SerializeField] private KeyCode actionKey = KeyCode.B;
    [SerializeField] private bool allowGroundedUse = true;
    [SerializeField] private bool allowAirborneUse = true;

    [Header("Preparation")]
    [SerializeField, Min(0f)] private float preparationDuration = 0.12f;
    [SerializeField] private Vector3 shootPointOffset = Vector3.zero;

    [Header("Targeting")]
    [SerializeField] private LayerMask targetLayers;
    [SerializeField, Min(0f)] private float targetingRadius = 20f;
    [SerializeField, Range(0f, 180f)] private float targetingAngle = 75f;
    [SerializeField] private float targetHeightOffset = 0.5f;
    [SerializeField] private bool requireLineOfSight;
    [SerializeField] private LayerMask obstructionLayers;
    [SerializeField, Min(1)] private int targetBufferSize = 64;

    [Header("Projectile Movement")]
    [SerializeField, Min(0f)] private float launchSpeed = 28f;
    [SerializeField, Min(0f)] private float maximumSpeed = 38f;
    [SerializeField, Min(0f)] private float acceleration = 55f;
    [SerializeField, Min(0f)] private float homingSharpness = 12f;
    [SerializeField, Min(0.1f)] private float maximumFlightDuration = 1.25f;
    [SerializeField] private float noTargetUpwardBias = 0.08f;
    [SerializeField, Min(0.01f)] private float impactRadius = 0.6f;
    [SerializeField] private LayerMask solidLayers;

    [Header("Impact")]
    [SerializeField, Min(0)] private int damage = 1;
    [SerializeField, Min(0f)] private float stunDuration = 2f;
    [SerializeField, Min(0f)] private float electricBurstRadius = 1.25f;
    [SerializeField] private bool useElectricBurst = true;

    [Header("Return")]
    [SerializeField, Min(0.01f)] private float returnSpeed = 25f;
    [SerializeField, Min(0f)] private float returnSharpness = 14f;
    [SerializeField, Min(0.1f)] private float maximumReturnDuration = 1f;
    [SerializeField, Min(0.01f)] private float returnCompletionDistance = 0.15f;
    [SerializeField, Min(0f)] private float shotCooldown = 0.3f;

    [Header("Animation")]
    [SerializeField] private Animator flyAnimator;
    [SerializeField] private Animator powerAnimator;
    [SerializeField] private string shootTrigger = "";
    [SerializeField] private string shootingBool = "";
    [SerializeField] private string projectileTrigger = "";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip preparationSound;
    [SerializeField] private AudioClip launchSound;
    [SerializeField] private AudioClip impactSound;

    [Header("Effects")]
    [SerializeField] private ParticleSystem preparationEffect;
    [SerializeField] private ParticleSystem launchEffect;
    [SerializeField] private GameObject impactEffectPrefab;
    [SerializeField, Min(0f)] private float impactEffectLifetime = 5f;

    [Header("Runtime Safety")]
    [SerializeField] private bool enableRuntimeSafety = true;
    [SerializeField, Min(0.1f)] private float safetyCheckInterval = 0.5f;
    [SerializeField, Min(1f)] private float maximumSafeProjectileSpeed = 250f;
    [SerializeField, Min(0.01f)] private float minimumValidScale = 0.01f;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;
    [SerializeField]
    private ThunderShootState currentState =
        ThunderShootState.Uninitialized;
    [SerializeField] private Transform currentTarget;
    [SerializeField] private float currentProjectileSpeed;

    #endregion

    #region Runtime State

    private readonly HashSet<GameObject> damagedTargets =
        new();

    private Collider[] targetBuffer;
    private Collider[] burstBuffer;

    private Coroutine thunderShootRoutine;

    private Transform originalPowerParent;
    private Vector3 originalPowerLocalPosition;
    private Quaternion originalPowerLocalRotation;
    private Vector3 originalPowerLocalScale;

    private Rigidbody powerRigidbody;
    private UltimatePlayerMovement powerMovement;

    private bool originalPowerKinematic;
    private bool originalPowerGravity;
    private RigidbodyConstraints originalPowerConstraints;

    private Collider[] powerColliders =
        Array.Empty<Collider>();

    private bool[] originalColliderStates =
        Array.Empty<bool>();

    private bool[] originalFollowerBehaviourStates =
        Array.Empty<bool>();

    private float safetyTimer;

    private int shootTriggerHash;
    private int shootingBoolHash;
    private int projectileTriggerHash;

    private bool powerStateCached;
    private bool actionStarted;
    private bool initialized;
    private bool shuttingDown;
    private bool applicationQuitting;

    #endregion

    #region Events

    public event Action<ThunderShoot> Started;
    public event Action<ThunderShoot, Transform> TargetAcquired;
    public event Action<ThunderShoot, GameObject> TargetHit;
    public event Action<ThunderShoot> Completed;
    public event Action<ThunderShoot> Cancelled;

    #endregion

    #region Public API

    public bool IsInitialized =>
        initialized;

    public bool IsActive =>
        currentState != ThunderShootState.Ready &&
        currentState != ThunderShootState.Cooldown &&
        currentState != ThunderShootState.Disabled &&
        currentState != ThunderShootState.Uninitialized;

    public ThunderShootState CurrentState =>
        currentState;

    public Transform CurrentTarget =>
        currentTarget;

    public bool TryStartThunderShoot()
    {
        if (!CanStartThunderShoot())
            return false;

        if (!ValidateReferences())
        {
            ResolveReferences();

            if (!ValidateReferences())
                return false;
        }

        bool isGrounded =
            movement != null &&
            movement.IsGrounded;

        if (isGrounded &&
            !allowGroundedUse)
        {
            return false;
        }

        if (!isGrounded &&
            !allowAirborneUse)
        {
            return false;
        }

        if (!BeginTeamAction())
            return false;

        currentTarget =
            FindBestTarget();

        if (currentTarget != null)
        {
            TargetAcquired?.Invoke(
                this,
                currentTarget);
        }

        damagedTargets.Clear();

        thunderShootRoutine =
            StartCoroutine(
                ThunderShootRoutine());

        Started?.Invoke(
            this);

        return true;
    }

    public void CancelThunderShoot()
    {
        if (thunderShootRoutine != null)
        {
            StopCoroutine(
                thunderShootRoutine);

            thunderShootRoutine = null;
        }

        RestorePowerTeammate();
        StopAllPresentation();
        FinishTeamAction();

        currentTarget = null;
        currentProjectileSpeed = 0f;
        damagedTargets.Clear();

        currentState =
            initialized
                ? ThunderShootState.Ready
                : ThunderShootState.Disabled;

        Cancelled?.Invoke(
            this);

        LogStateChange(
            "Thunder Shoot cancelled.");
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
            currentState == ThunderShootState.Disabled)
        {
            currentState =
                ThunderShootState.Ready;
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

        if (Input.GetKeyDown(
                actionKey))
        {
            TryStartThunderShoot();
        }

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
        if (IsActive)
        {
            CancelThunderShoot();
        }

        currentState =
            ThunderShootState.Disabled;
    }

    private void OnApplicationQuit()
    {
        applicationQuitting = true;
    }

    private void OnDestroy()
    {
        shuttingDown = true;
        initialized = false;

        if (thunderShootRoutine != null)
        {
            StopCoroutine(
                thunderShootRoutine);

            thunderShootRoutine = null;
        }

        RestorePowerTeammate();
        StopAllPresentation();
        FinishTeamAction();

        Started = null;
        TargetAcquired = null;
        TargetHit = null;
        Completed = null;
        Cancelled = null;

        movement = null;
        actionController = null;
        flyCharacter = null;
        powerTeammate = null;
        powerReturnPoint = null;
        shootPoint = null;
        playerCamera = null;
        flyAnimator = null;
        powerAnimator = null;
        audioSource = null;
        preparationEffect = null;
        launchEffect = null;
        impactEffectPrefab = null;

        targetBuffer = null;
        burstBuffer = null;

        currentState =
            ThunderShootState.Disabled;
    }

    private void OnValidate()
    {
        preparationDuration =
            Mathf.Max(
                0f,
                preparationDuration);

        targetingRadius =
            Mathf.Max(
                0f,
                targetingRadius);

        targetingAngle =
            Mathf.Clamp(
                targetingAngle,
                0f,
                180f);

        targetBufferSize =
            Mathf.Max(
                1,
                targetBufferSize);

        launchSpeed =
            Mathf.Max(
                0f,
                launchSpeed);

        maximumSpeed =
            Mathf.Max(
                launchSpeed,
                maximumSpeed);

        acceleration =
            Mathf.Max(
                0f,
                acceleration);

        homingSharpness =
            Mathf.Max(
                0f,
                homingSharpness);

        maximumFlightDuration =
            Mathf.Max(
                0.1f,
                maximumFlightDuration);

        impactRadius =
            Mathf.Max(
                0.01f,
                impactRadius);

        damage =
            Mathf.Max(
                0,
                damage);

        stunDuration =
            Mathf.Max(
                0f,
                stunDuration);

        electricBurstRadius =
            Mathf.Max(
                0f,
                electricBurstRadius);

        returnSpeed =
            Mathf.Max(
                0.01f,
                returnSpeed);

        returnSharpness =
            Mathf.Max(
                0f,
                returnSharpness);

        maximumReturnDuration =
            Mathf.Max(
                0.1f,
                maximumReturnDuration);

        returnCompletionDistance =
            Mathf.Max(
                0.01f,
                returnCompletionDistance);

        shotCooldown =
            Mathf.Max(
                0f,
                shotCooldown);

        impactEffectLifetime =
            Mathf.Max(
                0f,
                impactEffectLifetime);

        safetyCheckInterval =
            Mathf.Max(
                0.1f,
                safetyCheckInterval);

        maximumSafeProjectileSpeed =
            Mathf.Max(
                1f,
                maximumSafeProjectileSpeed);

        minimumValidScale =
            Mathf.Max(
                0.01f,
                minimumValidScale);

        CacheAnimatorHashes();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ResolveReferences();
            EnsureBuffers();
        }
#endif
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin =
            shootPoint != null
                ? shootPoint.position
                : transform.position;

        Gizmos.DrawWireSphere(
            origin,
            targetingRadius);

        Gizmos.DrawWireSphere(
            origin,
            impactRadius);

        if (powerReturnPoint != null)
        {
            Gizmos.DrawWireSphere(
                powerReturnPoint.position,
                returnCompletionDistance);
        }

        Vector3 aimDirection =
            playerCamera != null
                ? playerCamera.transform.forward
                : transform.forward;

        Gizmos.DrawLine(
            origin,
            origin +
            aimDirection.normalized *
            targetingRadius);
    }

    #endregion

    #region Initialization

    private bool Initialize()
    {
        if (initialized)
            return true;

        ResolveReferences();
        CacheAnimatorHashes();
        EnsureBuffers();
        CachePowerTeammateState();

        safetyTimer =
            safetyCheckInterval;

        if (!ValidateReferences())
        {
            currentState =
                ThunderShootState.Uninitialized;

            initialized = false;
            enabled = false;

            return false;
        }

        initialized = true;
        currentState =
            ThunderShootState.Ready;

        return true;
    }

    private void ResolveReferences()
    {
        flyCharacter ??=
            transform;

        movement ??=
            GetComponentInParent<UltimatePlayerMovement>();

        actionController ??=
            GetComponent<TeamActionController>();

        actionController ??=
            GetComponentInParent<TeamActionController>();

        playerCamera ??=
            Camera.main;

        shootPoint ??=
            FindDescendantByName(
                flyCharacter,
                DefaultShootPointName);

        Transform teamRoot =
            movement != null
                ? movement.transform
                : transform.root;

        powerReturnPoint ??=
            FindDescendantByName(
                teamRoot,
                DefaultPowerReturnPointName);

        if (powerTeammate == null &&
            powerReturnPoint != null)
        {
            powerTeammate =
                ResolvePowerTeammateFromTeamRoot(
                    teamRoot);
        }

        flyAnimator ??=
            flyCharacter != null
                ? flyCharacter.GetComponentInChildren<Animator>(
                    includeInactive: true)
                : null;

        powerAnimator ??=
            powerTeammate != null
                ? powerTeammate.GetComponentInChildren<Animator>(
                    includeInactive: true)
                : null;

        audioSource ??=
            GetComponent<AudioSource>();

        if (powerTeammate != null)
        {
            powerRigidbody ??=
                powerTeammate.GetComponent<Rigidbody>();

            powerRigidbody ??=
                powerTeammate.GetComponentInChildren<Rigidbody>(
                    includeInactive: true);

            powerMovement ??=
                powerTeammate.GetComponent<UltimatePlayerMovement>();

            powerMovement ??=
                powerTeammate.GetComponentInChildren<UltimatePlayerMovement>(
                    includeInactive: true);
        }

        if (powerFollowerBehaviours == null)
        {
            powerFollowerBehaviours =
                Array.Empty<Behaviour>();
        }
    }

    private void CacheAnimatorHashes()
    {
        shootTriggerHash =
            GetAnimatorHash(
                shootTrigger);

        shootingBoolHash =
            GetAnimatorHash(
                shootingBool);

        projectileTriggerHash =
            GetAnimatorHash(
                projectileTrigger);
    }

    private void EnsureBuffers()
    {
        int size =
            Mathf.Max(
                1,
                targetBufferSize);

        if (targetBuffer == null ||
            targetBuffer.Length != size)
        {
            targetBuffer =
                new Collider[size];
        }

        if (burstBuffer == null ||
            burstBuffer.Length != size)
        {
            burstBuffer =
                new Collider[size];
        }
    }

    #endregion

    #region Team Action

    private bool BeginTeamAction()
    {
        if (actionController == null)
        {
            movement?.DisableMovement();
            return true;
        }

        bool accepted =
            actionController.TryBeginAction(
                TeamActionController.TeamAction.ThunderShoot,
                TeamActionController.TeamFormation.Fly,
                mustBeGrounded: false,
                mustBeAirborne: false,
                surrenderMovementControl: false);

        if (!accepted)
            return false;

        actionStarted = true;
        movement?.DisableMovement();

        return true;
    }

    private void FinishTeamAction()
    {
        if (actionStarted &&
            actionController != null &&
            actionController.CurrentAction ==
                TeamActionController.TeamAction.ThunderShoot)
        {
            actionController.EndAction();
        }
        else
        {
            movement?.EnableMovement();
        }

        actionStarted = false;
    }

    #endregion

    #region Thunder Shoot Flow

    private IEnumerator ThunderShootRoutine()
    {
        currentState =
            ThunderShootState.Preparing;

        PreparePowerTeammate();
        StartPreparationPresentation();

        yield return MovePowerToShootPoint();

        if (!ValidateActivePowerTeammate())
        {
            CancelThunderShoot();
            yield break;
        }

        StopPreparationPresentation();
        StartLaunchPresentation();

        currentState =
            ThunderShootState.Flying;

        yield return FlyPowerProjectile();

        if (!ValidateActivePowerTeammate())
        {
            CancelThunderShoot();
            yield break;
        }

        currentState =
            ThunderShootState.Returning;

        yield return ReturnPowerTeammate();

        RestorePowerTeammate();
        StopAllPresentation();
        FinishTeamAction();

        currentState =
            ThunderShootState.Cooldown;

        if (shotCooldown > 0f)
        {
            yield return new WaitForSeconds(
                shotCooldown);
        }

        currentTarget = null;
        currentProjectileSpeed = 0f;
        thunderShootRoutine = null;

        currentState =
            ThunderShootState.Ready;

        Completed?.Invoke(
            this);

        LogStateChange(
            "Thunder Shoot completed.");
    }

    private IEnumerator MovePowerToShootPoint()
    {
        if (!ValidateActivePowerTeammate())
            yield break;

        Vector3 startPosition =
            powerTeammate.position;

        Quaternion startRotation =
            powerTeammate.rotation;

        float elapsed = 0f;

        if (preparationDuration <= 0f)
        {
            powerTeammate.SetPositionAndRotation(
                GetShootPosition(),
                GetLaunchRotation());

            yield break;
        }

        while (elapsed <
               preparationDuration)
        {
            if (!ValidateActivePowerTeammate())
                yield break;

            elapsed +=
                Time.deltaTime;

            float t =
                SmoothStep(
                    Mathf.Clamp01(
                        elapsed /
                        preparationDuration));

            powerTeammate.position =
                Vector3.Lerp(
                    startPosition,
                    GetShootPosition(),
                    t);

            powerTeammate.rotation =
                Quaternion.Slerp(
                    startRotation,
                    GetLaunchRotation(),
                    t);

            yield return null;
        }

        powerTeammate.SetPositionAndRotation(
            GetShootPosition(),
            GetLaunchRotation());
    }

    private IEnumerator FlyPowerProjectile()
    {
        Vector3 direction =
            GetInitialLaunchDirection();

        if (!IsFiniteVector(direction) ||
            direction.sqrMagnitude <= 0.001f)
        {
            yield break;
        }

        currentProjectileSpeed =
            Mathf.Min(
                launchSpeed,
                maximumSafeProjectileSpeed);

        float elapsed = 0f;

        while (elapsed <
               maximumFlightDuration &&
               currentState ==
               ThunderShootState.Flying)
        {
            if (!ValidateActivePowerTeammate())
                yield break;

            elapsed +=
                Time.fixedDeltaTime;

            if (!IsTargetValid(
                    currentTarget))
            {
                currentTarget =
                    FindBestTarget();
            }

            Vector3 desiredDirection =
                GetDesiredFlightDirection(
                    direction);

            float turnAmount =
                1f -
                Mathf.Exp(
                    -homingSharpness *
                    Time.fixedDeltaTime);

            direction =
                Vector3.Slerp(
                    direction,
                    desiredDirection,
                    turnAmount);

            if (direction.sqrMagnitude <= 0.001f ||
                !IsFiniteVector(
                    direction))
            {
                yield break;
            }

            direction.Normalize();

            currentProjectileSpeed =
                Mathf.MoveTowards(
                    currentProjectileSpeed,
                    Mathf.Min(
                        maximumSpeed,
                        maximumSafeProjectileSpeed),
                    acceleration *
                    Time.fixedDeltaTime);

            Vector3 startPosition =
                powerTeammate.position;

            float movementDistance =
                currentProjectileSpeed *
                Time.fixedDeltaTime;

            if (CheckSolidImpact(
                    startPosition,
                    direction,
                    movementDistance,
                    out RaycastHit solidHit))
            {
                powerTeammate.position =
                    solidHit.point;

                ProcessImpact(
                    solidHit.collider,
                    solidHit.point);

                yield break;
            }

            powerTeammate.position +=
                direction *
                movementDistance;

            powerTeammate.rotation =
                Quaternion.LookRotation(
                    direction,
                    Vector3.up);

            Collider targetHit =
                FindTargetAtPosition(
                    powerTeammate.position);

            if (targetHit != null)
            {
                ProcessImpact(
                    targetHit,
                    powerTeammate.position);

                yield break;
            }

            yield return new WaitForFixedUpdate();
        }
    }

    private IEnumerator ReturnPowerTeammate()
    {
        float elapsed = 0f;
        Vector3 returnVelocity =
            Vector3.zero;

        while (elapsed <
               maximumReturnDuration)
        {
            if (!ValidateActivePowerTeammate() ||
                powerReturnPoint == null)
            {
                yield break;
            }

            elapsed +=
                Time.fixedDeltaTime;

            Vector3 destination =
                powerReturnPoint.position;

            Vector3 toDestination =
                destination -
                powerTeammate.position;

            if (toDestination.sqrMagnitude <=
                returnCompletionDistance *
                returnCompletionDistance)
            {
                powerTeammate.position =
                    destination;

                yield break;
            }

            Vector3 desiredVelocity =
                toDestination.normalized *
                returnSpeed;

            float returnAmount =
                1f -
                Mathf.Exp(
                    -returnSharpness *
                    Time.fixedDeltaTime);

            returnVelocity =
                Vector3.Lerp(
                    returnVelocity,
                    desiredVelocity,
                    returnAmount);

            Vector3 movementStep =
                returnVelocity *
                Time.fixedDeltaTime;

            if (movementStep.sqrMagnitude >
                toDestination.sqrMagnitude)
            {
                movementStep =
                    toDestination;
            }

            powerTeammate.position +=
                movementStep;

            if (returnVelocity.sqrMagnitude >
                0.001f)
            {
                powerTeammate.rotation =
                    Quaternion.LookRotation(
                        returnVelocity.normalized,
                        Vector3.up);
            }

            yield return new WaitForFixedUpdate();
        }

        if (powerTeammate != null &&
            powerReturnPoint != null)
        {
            powerTeammate.position =
                powerReturnPoint.position;
        }
    }

    #endregion

    #region Targeting

    private Transform FindBestTarget()
    {
        if (targetLayers.value == 0 ||
            targetingRadius <= 0f)
        {
            return null;
        }

        EnsureBuffers();

        Vector3 origin =
            GetShootPosition();

        Vector3 aimDirection =
            GetAimDirection();

        int hitCount =
            Physics.OverlapSphereNonAlloc(
                origin,
                targetingRadius,
                targetBuffer,
                targetLayers,
                QueryTriggerInteraction.Collide);

        Transform bestTarget = null;
        float bestScore =
            float.PositiveInfinity;

        for (int index = 0;
             index < hitCount;
             index++)
        {
            Collider candidate =
                targetBuffer[index];

            if (!IsValidTargetCollider(
                    candidate))
            {
                continue;
            }

            Vector3 targetPosition =
                candidate.bounds.center;

            Vector3 toTarget =
                targetPosition -
                origin;

            float distance =
                toTarget.magnitude;

            if (distance <= 0.001f)
                continue;

            Vector3 direction =
                toTarget /
                distance;

            float angle =
                Vector3.Angle(
                    aimDirection,
                    direction);

            if (angle >
                targetingAngle)
            {
                continue;
            }

            if (requireLineOfSight &&
                !HasLineOfSight(
                    origin,
                    candidate,
                    targetPosition))
            {
                continue;
            }

            float angleScore =
                angle /
                Mathf.Max(
                    targetingAngle,
                    0.01f);

            float distanceScore =
                distance /
                Mathf.Max(
                    targetingRadius,
                    0.01f);

            float score =
                angleScore *
                0.7f +
                distanceScore *
                0.3f;

            if (score >=
                bestScore)
            {
                continue;
            }

            bestScore =
                score;

            bestTarget =
                candidate.attachedRigidbody != null
                    ? candidate.attachedRigidbody.transform
                    : candidate.transform;
        }

        return bestTarget;
    }

    private Vector3 GetDesiredFlightDirection(
        Vector3 currentDirection)
    {
        if (!IsTargetValid(
                currentTarget))
        {
            return currentDirection;
        }

        Vector3 targetPosition =
            currentTarget.position +
            Vector3.up *
            targetHeightOffset;

        Vector3 direction =
            targetPosition -
            powerTeammate.position;

        if (direction.sqrMagnitude <
            0.001f)
        {
            return currentDirection;
        }

        return direction.normalized;
    }

    private bool HasLineOfSight(
        Vector3 origin,
        Collider targetCollider,
        Vector3 targetPosition)
    {
        Vector3 direction =
            targetPosition -
            origin;

        float distance =
            direction.magnitude;

        if (distance <= 0.001f)
            return true;

        direction /=
            distance;

        if (!Physics.Raycast(
                origin,
                direction,
                out RaycastHit hit,
                distance,
                obstructionLayers,
                QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        return
            hit.collider ==
                targetCollider ||
            hit.transform.IsChildOf(
                targetCollider.transform) ||
            targetCollider.transform.IsChildOf(
                hit.transform);
    }

    private bool IsTargetValid(
        Transform target)
    {
        return
            target != null &&
            target.gameObject.activeInHierarchy &&
            ValidateTransform(
                target);
    }

    #endregion

    #region Impact

    private bool CheckSolidImpact(
        Vector3 origin,
        Vector3 direction,
        float distance,
        out RaycastHit hit)
    {
        if (solidLayers.value == 0)
        {
            hit =
                default;

            return false;
        }

        return Physics.SphereCast(
            origin,
            impactRadius *
            0.75f,
            direction,
            out hit,
            distance,
            solidLayers,
            QueryTriggerInteraction.Ignore);
    }

    private Collider FindTargetAtPosition(
        Vector3 position)
    {
        if (targetLayers.value == 0)
            return null;

        EnsureBuffers();

        int hitCount =
            Physics.OverlapSphereNonAlloc(
                position,
                impactRadius,
                targetBuffer,
                targetLayers,
                QueryTriggerInteraction.Collide);

        for (int index = 0;
             index < hitCount;
             index++)
        {
            Collider hit =
                targetBuffer[index];

            if (IsValidTargetCollider(
                    hit))
            {
                return hit;
            }
        }

        return null;
    }

    private void ProcessImpact(
        Collider directHit,
        Vector3 impactPosition)
    {
        if (directHit != null)
        {
            DamageTarget(
                GetTargetObject(
                    directHit));
        }

        if (useElectricBurst &&
            electricBurstRadius > 0f &&
            targetLayers.value != 0)
        {
            EnsureBuffers();

            int hitCount =
                Physics.OverlapSphereNonAlloc(
                    impactPosition,
                    electricBurstRadius,
                    burstBuffer,
                    targetLayers,
                    QueryTriggerInteraction.Collide);

            for (int index = 0;
                 index < hitCount;
                 index++)
            {
                Collider targetCollider =
                    burstBuffer[index];

                if (!IsValidTargetCollider(
                        targetCollider))
                {
                    continue;
                }

                DamageTarget(
                    GetTargetObject(
                        targetCollider));
            }
        }

        SpawnImpactEffect(
            impactPosition);

        PlayImpactSound();

        currentState =
            ThunderShootState.Returning;
    }

    private void DamageTarget(
        GameObject target)
    {
        if (target == null ||
            !damagedTargets.Add(
                target))
        {
            return;
        }

        ThunderShootTarget thunderTarget =
            target.GetComponent<ThunderShootTarget>();

        thunderTarget ??=
            target.GetComponentInParent<ThunderShootTarget>();

        thunderTarget ??=
            target.GetComponentInChildren<ThunderShootTarget>(
                includeInactive: true);

        if (thunderTarget != null)
        {
            thunderTarget.HitByThunderShoot(
                damage,
                stunDuration);
        }
        else
        {
            target.SendMessage(
                "OnThunderShootHit",
                gameObject,
                SendMessageOptions.DontRequireReceiver);

            target.SendMessage(
                "Activate",
                SendMessageOptions.DontRequireReceiver);

            if (damage > 0)
            {
                target.SendMessage(
                    "TakeDamage",
                    damage,
                    SendMessageOptions.DontRequireReceiver);
            }
        }

        TargetHit?.Invoke(
            this,
            target);
    }

    private GameObject GetTargetObject(
        Collider targetCollider)
    {
        if (targetCollider == null)
            return null;

        if (targetCollider.attachedRigidbody != null)
        {
            return targetCollider
                .attachedRigidbody
                .gameObject;
        }

        return targetCollider
            .transform
            .root
            .gameObject;
    }

    private bool IsValidTargetCollider(
        Collider targetCollider)
    {
        if (targetCollider == null ||
            !targetCollider.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (powerTeammate != null &&
            targetCollider.transform.IsChildOf(
                powerTeammate))
        {
            return false;
        }

        if (flyCharacter != null &&
            targetCollider.transform.IsChildOf(
                flyCharacter))
        {
            return false;
        }

        if (movement != null &&
            targetCollider.transform.IsChildOf(
                movement.transform))
        {
            return false;
        }

        return true;
    }

    #endregion

    #region Power Teammate State

    private void CachePowerTeammateState()
    {
        if (powerTeammate == null ||
            powerStateCached)
        {
            return;
        }

        originalPowerParent =
            powerTeammate.parent;

        originalPowerLocalPosition =
            powerTeammate.localPosition;

        originalPowerLocalRotation =
            powerTeammate.localRotation;

        originalPowerLocalScale =
            powerTeammate.localScale;

        powerRigidbody ??=
            powerTeammate.GetComponent<Rigidbody>();

        powerRigidbody ??=
            powerTeammate.GetComponentInChildren<Rigidbody>(
                includeInactive: true);

        powerMovement ??=
            powerTeammate.GetComponent<UltimatePlayerMovement>();

        powerMovement ??=
            powerTeammate.GetComponentInChildren<UltimatePlayerMovement>(
                includeInactive: true);

        if (powerRigidbody != null)
        {
            originalPowerKinematic =
                powerRigidbody.isKinematic;

            originalPowerGravity =
                powerRigidbody.useGravity;

            originalPowerConstraints =
                powerRigidbody.constraints;
        }

        powerColliders =
            powerTeammate.GetComponentsInChildren<Collider>(
                includeInactive: true);

        originalColliderStates =
            new bool[
                powerColliders.Length];

        for (int index = 0;
             index < powerColliders.Length;
             index++)
        {
            originalColliderStates[index] =
                powerColliders[index] != null &&
                powerColliders[index].enabled;
        }

        if (powerFollowerBehaviours == null)
        {
            powerFollowerBehaviours =
                Array.Empty<Behaviour>();
        }

        originalFollowerBehaviourStates =
            new bool[
                powerFollowerBehaviours.Length];

        for (int index = 0;
             index < powerFollowerBehaviours.Length;
             index++)
        {
            originalFollowerBehaviourStates[index] =
                powerFollowerBehaviours[index] != null &&
                powerFollowerBehaviours[index].enabled;
        }

        powerStateCached = true;
    }

    private void PreparePowerTeammate()
    {
        CachePowerTeammateState();

        if (!ValidateActivePowerTeammate())
            return;

        powerMovement?.DisableMovement();

        for (int index = 0;
             index < powerFollowerBehaviours.Length;
             index++)
        {
            if (powerFollowerBehaviours[index] != null)
            {
                powerFollowerBehaviours[index].enabled =
                    false;
            }
        }

        powerTeammate.SetParent(
            null,
            true);

        if (powerRigidbody != null)
        {
            powerRigidbody.linearVelocity =
                Vector3.zero;

            powerRigidbody.angularVelocity =
                Vector3.zero;

            powerRigidbody.useGravity =
                false;

            powerRigidbody.isKinematic =
                true;
        }

        for (int index = 0;
             index < powerColliders.Length;
             index++)
        {
            if (powerColliders[index] != null)
            {
                powerColliders[index].enabled =
                    false;
            }
        }

        if (powerAnimator != null &&
            projectileTriggerHash != 0)
        {
            powerAnimator.SetTrigger(
                projectileTriggerHash);
        }
    }

    private void RestorePowerTeammate()
    {
        if (!powerStateCached ||
            powerTeammate == null)
        {
            return;
        }

        powerTeammate.SetParent(
            originalPowerParent,
            false);

        powerTeammate.localPosition =
            originalPowerLocalPosition;

        powerTeammate.localRotation =
            originalPowerLocalRotation;

        powerTeammate.localScale =
            originalPowerLocalScale;

        if (powerRigidbody != null)
        {
            powerRigidbody.linearVelocity =
                Vector3.zero;

            powerRigidbody.angularVelocity =
                Vector3.zero;

            powerRigidbody.constraints =
                originalPowerConstraints;

            powerRigidbody.isKinematic =
                originalPowerKinematic;

            powerRigidbody.useGravity =
                originalPowerGravity;

            powerRigidbody.WakeUp();
        }

        int colliderCount =
            Mathf.Min(
                powerColliders.Length,
                originalColliderStates.Length);

        for (int index = 0;
             index < colliderCount;
             index++)
        {
            if (powerColliders[index] != null)
            {
                powerColliders[index].enabled =
                    originalColliderStates[index];
            }
        }

        int behaviourCount =
            Mathf.Min(
                powerFollowerBehaviours.Length,
                originalFollowerBehaviourStates.Length);

        for (int index = 0;
             index < behaviourCount;
             index++)
        {
            if (powerFollowerBehaviours[index] != null)
            {
                powerFollowerBehaviours[index].enabled =
                    originalFollowerBehaviourStates[index];
            }
        }

        powerMovement?.EnableMovement();
    }

    #endregion

    #region Presentation

    private void StartPreparationPresentation()
    {
        if (flyAnimator != null)
        {
            if (shootTriggerHash != 0)
            {
                flyAnimator.SetTrigger(
                    shootTriggerHash);
            }

            if (shootingBoolHash != 0)
            {
                flyAnimator.SetBool(
                    shootingBoolHash,
                    true);
            }
        }

        preparationEffect?.Play();
        PlaySound(
            preparationSound);
    }

    private void StopPreparationPresentation()
    {
        if (preparationEffect != null)
        {
            preparationEffect.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void StartLaunchPresentation()
    {
        launchEffect?.Play();
        PlaySound(
            launchSound);
    }

    private void StopAllPresentation()
    {
        StopPreparationPresentation();

        if (flyAnimator != null &&
            shootingBoolHash != 0)
        {
            flyAnimator.SetBool(
                shootingBoolHash,
                false);
        }

        if (launchEffect != null)
        {
            launchEffect.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void SpawnImpactEffect(
        Vector3 position)
    {
        if (impactEffectPrefab == null)
            return;

        GameObject effect =
            Instantiate(
                impactEffectPrefab,
                position,
                Quaternion.identity);

        if (effect != null &&
            impactEffectLifetime > 0f)
        {
            Destroy(
                effect,
                impactEffectLifetime);
        }
    }

    private void PlayImpactSound()
    {
        PlaySound(
            impactSound);
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
        if (!ValidateReferences())
        {
            ResolveReferences();

            if (!ValidateReferences())
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
                "Thunder Shoot has an invalid Transform.");

            return false;
        }

        if (IsActive &&
            !ValidateActivePowerTeammate())
        {
            CancelThunderShoot();
            return false;
        }

        if (!float.IsFinite(
                currentProjectileSpeed) ||
            currentProjectileSpeed >
                maximumSafeProjectileSpeed)
        {
            currentProjectileSpeed =
                Mathf.Clamp(
                    currentProjectileSpeed,
                    0f,
                    maximumSafeProjectileSpeed);
        }

        return true;
    }

    private void EnterSafetyShutdown(
        string reason)
    {
        CancelThunderShoot();

        initialized = false;
        currentState =
            ThunderShootState.Disabled;

        Debug.LogError(
            $"{nameof(ThunderShoot)} entered safety shutdown on '{name}': {reason}",
            this);

        enabled = false;
    }

    #endregion

    #region Validation

    private bool CanStartThunderShoot()
    {
        return
            initialized &&
            currentState ==
                ThunderShootState.Ready &&
            !shuttingDown &&
            !applicationQuitting &&
            isActiveAndEnabled;
    }

    private bool ValidateReferences()
    {
        return
            movement != null &&
            actionController != null &&
            flyCharacter != null &&
            powerTeammate != null &&
            powerReturnPoint != null &&
            shootPoint != null;
    }

    private bool ValidateActivePowerTeammate()
    {
        return
            powerTeammate != null &&
            powerTeammate.gameObject.activeInHierarchy &&
            ValidateTransform(
                powerTeammate) &&
            (powerRigidbody == null ||
             ValidateRigidbody(
                 powerRigidbody));
    }

    private bool ValidateRigidbody(
        Rigidbody body)
    {
        return
            body != null &&
            IsFiniteVector(
                body.position) &&
            IsFiniteQuaternion(
                body.rotation) &&
            IsFiniteVector(
                body.linearVelocity) &&
            IsFiniteVector(
                body.angularVelocity);
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

    #region Helpers

    private Vector3 GetInitialLaunchDirection()
    {
        if (IsTargetValid(
                currentTarget))
        {
            Vector3 targetPosition =
                currentTarget.position +
                Vector3.up *
                targetHeightOffset;

            Vector3 direction =
                targetPosition -
                GetShootPosition();

            if (direction.sqrMagnitude >
                0.001f)
            {
                return direction.normalized;
            }
        }

        Vector3 fallback =
            GetAimDirection() +
            Vector3.up *
            noTargetUpwardBias;

        if (fallback.sqrMagnitude <
            0.001f)
        {
            fallback =
                flyCharacter != null
                    ? flyCharacter.forward
                    : transform.forward;
        }

        return fallback.normalized;
    }

    private Vector3 GetAimDirection()
    {
        if (playerCamera != null)
        {
            Vector3 cameraForward =
                playerCamera.transform.forward;

            if (cameraForward.sqrMagnitude >
                0.001f)
            {
                return cameraForward.normalized;
            }
        }

        if (flyCharacter != null)
        {
            return flyCharacter.forward.normalized;
        }

        return transform.forward.normalized;
    }

    private Vector3 GetShootPosition()
    {
        if (shootPoint == null)
            return transform.position;

        return shootPoint.TransformPoint(
            shootPointOffset);
    }

    private Quaternion GetLaunchRotation()
    {
        Vector3 direction =
            GetInitialLaunchDirection();

        if (direction.sqrMagnitude <
            0.001f)
        {
            return
                powerTeammate != null
                    ? powerTeammate.rotation
                    : transform.rotation;
        }

        return Quaternion.LookRotation(
            direction,
            Vector3.up);
    }

    private Transform ResolvePowerTeammateFromTeamRoot(
        Transform teamRoot)
    {
        if (teamRoot == null)
            return null;

        UltimatePlayerMovement[] candidates =
            teamRoot.GetComponentsInChildren<UltimatePlayerMovement>(
                includeInactive: true);

        foreach (UltimatePlayerMovement candidate
                 in candidates)
        {
            if (candidate == null ||
                candidate == movement)
            {
                continue;
            }

            CharacterDefinition definition =
                candidate.CharacterDefinition;

            if (definition != null &&
                definition.characterType ==
                    CharacterDefinition.CharacterType.Power)
            {
                return candidate.transform;
            }
        }

        return null;
    }

    private static Transform FindDescendantByName(
        Transform root,
        string targetName)
    {
        if (root == null ||
            string.IsNullOrWhiteSpace(
                targetName))
        {
            return null;
        }

        Transform[] descendants =
            root.GetComponentsInChildren<Transform>(
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
