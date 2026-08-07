using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TurretAI : AIController
{
    #region Types

    public enum TurretState
    {
        Searching,
        Tracking,
        Aiming,
        Firing,
        CoolingDown,
        Returning,
        Disabled
    }

    public enum FireMode
    {
        Single,
        Burst
    }

    #endregion

    #region Constants

    private const float MinimumDirectionSqrMagnitude =
        0.0001f;

    private const float MinimumAngleLimit =
        0.1f;

    private const int LineOfSightHitCapacity =
        16;

    #endregion

    #region Inspector

    [Header("Turret References")]
    [SerializeField] private Transform rotatingBase;
    [SerializeField] private Transform aimingBarrel;
    [SerializeField] private Transform projectileSpawnPoint;

    [Header("Aiming")]
    [SerializeField, Min(0f)] private float horizontalTurnSpeed = 180f;
    [SerializeField, Min(0f)] private float verticalTurnSpeed = 120f;
    [SerializeField, Range(0f, 180f)] private float maximumHorizontalAngle = 180f;
    [SerializeField, Range(0f, 89f)] private float minimumVerticalAngle = 10f;
    [SerializeField, Range(0f, 89f)] private float maximumVerticalAngle = 65f;
    [SerializeField, Min(MinimumAngleLimit)] private float aimTolerance = 3f;
    [SerializeField] private bool returnToRestRotation = true;
    [SerializeField, Min(0f)] private float returnRotationSpeed = 120f;

    [Header("Target Leading")]
    [SerializeField] private bool leadTarget = true;
    [SerializeField, Min(0f)] private float projectileSpeed = 30f;
    [SerializeField, Min(0f)] private float maximumLeadTime = 2f;

    [Header("Line Of Sight")]
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private LayerMask lineOfSightBlockingLayers = ~0;

    [Header("Firing")]
    [SerializeField] private FireMode fireMode = FireMode.Single;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField, Min(0f)] private float fireCooldown = 1f;
    [SerializeField, Min(0f)] private float firingWindup = 0.25f;
    [SerializeField, Min(1)] private int burstCount = 3;
    [SerializeField, Min(0.01f)] private float burstInterval = 0.12f;
    [SerializeField] private bool inheritTurretVelocity;

    [Header("Projectile")]
    [SerializeField, Min(0f)] private float projectileLifetime = 10f;
    [SerializeField, Min(0f)] private float projectileSpawnOffset = 0.05f;

    [Header("Presentation")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private AudioSource turretAudioSource;
    [SerializeField] private AudioClip aimSound;
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private AudioClip cooldownSound;

    [Header("Runtime Safety")]
    [SerializeField] private bool restoreDisabledComponents = true;
    [SerializeField, Min(0.1f)] private float safetyCheckInterval = 0.5f;
    [SerializeField, Min(0.01f)] private float minimumValidScale = 0.01f;

    [Header("Debug")]
    [SerializeField]
    private TurretState turretState =
        TurretState.Searching;

    [SerializeField] private bool logTurretState;

    #endregion

    #region Runtime State

    private readonly RaycastHit[] lineOfSightHits =
        new RaycastHit[LineOfSightHitCapacity];

    private Quaternion baseRestLocalRotation;
    private Quaternion barrelRestLocalRotation;

    private Vector3 lastTargetPosition;
    private Vector3 estimatedTargetVelocity;

    private float fireCooldownTimer;
    private float firingWindupTimer;
    private float burstTimer;
    private float safetyTimer;

    private int remainingBurstShots;

    private bool targetPositionInitialized;
    private bool aimSoundPlayed;
    private bool initializedTurret;
    private bool shuttingDownTurret;

    #endregion

    #region Events

    public event Action<TurretAI> AimStarted;
    public event Action<TurretAI, Vector3> Aimed;
    public event Action<TurretAI, GameObject> ProjectileFired;
    public event Action<TurretAI> BurstCompleted;
    public event Action<TurretAI, TurretState> TurretStateChanged;

    #endregion

    #region Public API

    public TurretState CurrentTurretState =>
        turretState;

    public bool IsTurretInitialized =>
        initializedTurret;

    public bool IsAimed =>
        HasAimLock();

    public bool FireImmediately()
    {
        if (!CanFire())
            return false;

        BeginFiringSequence(
            bypassWindup: true);

        return true;
    }

    public void ResetTurret()
    {
        fireCooldownTimer =
            0f;

        firingWindupTimer =
            0f;

        burstTimer =
            0f;

        remainingBurstShots =
            0;

        estimatedTargetVelocity =
            Vector3.zero;

        targetPositionInitialized =
            false;

        aimSoundPlayed =
            false;

        RestoreRestRotations(
            instant: true);

        ChangeTurretState(
            TurretState.Searching);
    }

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        ResolveTurretReferences();
        CacheRestRotations();
        ConfigureTurretComponents();
        InitializeTurretRuntime();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (shuttingDownTurret)
            return;

        ResolveTurretReferences();
        ConfigureTurretComponents();

        if (!initializedTurret)
        {
            InitializeTurretRuntime();
        }

        if (turretState ==
            TurretState.Disabled)
        {
            ChangeTurretState(
                TurretState.Searching);
        }
    }

    protected override void Update()
    {
        base.Update();

        if (!IsInitialized ||
            IsDead ||
            CurrentState ==
                AIState.Disabled)
        {
            return;
        }

        UpdateTurretTimers();
        UpdateTargetVelocity();
        UpdateTurretBehaviour();

        safetyTimer -=
            Time.deltaTime;

        if (safetyTimer <= 0f)
        {
            safetyTimer =
                safetyCheckInterval;

            RunTurretSafetyChecks();
        }
    }

    protected override void OnDisable()
    {
        ChangeTurretState(
            TurretState.Disabled);

        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        shuttingDownTurret =
            true;

        AimStarted = null;
        Aimed = null;
        ProjectileFired = null;
        BurstCompleted = null;
        TurretStateChanged = null;

        rotatingBase = null;
        aimingBarrel = null;
        projectileSpawnPoint = null;
        projectilePrefab = null;
        turretAudioSource = null;

        base.OnDestroy();
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        horizontalTurnSpeed =
            Mathf.Max(
                0f,
                horizontalTurnSpeed);

        verticalTurnSpeed =
            Mathf.Max(
                0f,
                verticalTurnSpeed);

        maximumHorizontalAngle =
            Mathf.Clamp(
                maximumHorizontalAngle,
                0f,
                180f);

        minimumVerticalAngle =
            Mathf.Clamp(
                minimumVerticalAngle,
                0f,
                89f);

        maximumVerticalAngle =
            Mathf.Clamp(
                maximumVerticalAngle,
                minimumVerticalAngle,
                89f);

        aimTolerance =
            Mathf.Max(
                MinimumAngleLimit,
                aimTolerance);

        returnRotationSpeed =
            Mathf.Max(
                0f,
                returnRotationSpeed);

        projectileSpeed =
            Mathf.Max(
                0f,
                projectileSpeed);

        maximumLeadTime =
            Mathf.Max(
                0f,
                maximumLeadTime);

        fireCooldown =
            Mathf.Max(
                0f,
                fireCooldown);

        firingWindup =
            Mathf.Max(
                0f,
                firingWindup);

        burstCount =
            Mathf.Max(
                1,
                burstCount);

        burstInterval =
            Mathf.Max(
                0.01f,
                burstInterval);

        projectileLifetime =
            Mathf.Max(
                0f,
                projectileLifetime);

        projectileSpawnOffset =
            Mathf.Max(
                0f,
                projectileSpawnOffset);

        safetyCheckInterval =
            Mathf.Max(
                0.1f,
                safetyCheckInterval);

        minimumValidScale =
            Mathf.Max(
                0.01f,
                minimumValidScale);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ResolveTurretReferences();
            ConfigureTurretComponents();
        }
#endif
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Transform spawn =
            projectileSpawnPoint != null
                ? projectileSpawnPoint
                : transform;

        Gizmos.DrawRay(
            spawn.position,
            spawn.forward *
            3f);

        if (Target != null)
        {
            Gizmos.DrawLine(
                spawn.position,
                GetPredictedTargetPosition());
        }
    }

    #endregion

    #region Initialization

    protected override bool Initialize()
    {
        bool initialized =
            base.Initialize();

        if (!initialized)
            return false;

        ResolveTurretReferences();
        CacheRestRotations();
        ConfigureTurretComponents();
        InitializeTurretRuntime();

        return initializedTurret;
    }

    private void ResolveTurretReferences()
    {
        rotatingBase ??=
            FindChildByName(
                "Rotating Base");

        aimingBarrel ??=
            FindChildByName(
                "Aiming Barrel");

        projectileSpawnPoint ??=
            FindChildByName(
                "Projectile Spawn Point");

        rotatingBase ??=
            transform;

        aimingBarrel ??=
            rotatingBase;

        projectileSpawnPoint ??=
            aimingBarrel;

        turretAudioSource ??=
            GetComponent<AudioSource>();

        turretAudioSource ??=
            GetComponentInChildren<AudioSource>(
                includeInactive: true);
    }

    private void CacheRestRotations()
    {
        if (rotatingBase != null)
        {
            baseRestLocalRotation =
                rotatingBase.localRotation;
        }

        if (aimingBarrel != null)
        {
            barrelRestLocalRotation =
                aimingBarrel.localRotation;
        }
    }

    private void ConfigureTurretComponents()
    {
        if (Agent != null)
        {
            Agent.isStopped =
                true;

            Agent.updatePosition =
                false;

            Agent.updateRotation =
                false;
        }

        if (turretAudioSource != null)
        {
            turretAudioSource.playOnAwake =
                false;
        }
    }

    private void InitializeTurretRuntime()
    {
        fireCooldownTimer =
            0f;

        firingWindupTimer =
            0f;

        burstTimer =
            0f;

        safetyTimer =
            safetyCheckInterval;

        remainingBurstShots =
            0;

        lastTargetPosition =
            Vector3.zero;

        estimatedTargetVelocity =
            Vector3.zero;

        targetPositionInitialized =
            false;

        aimSoundPlayed =
            false;

        ChangeTurretState(
            TurretState.Searching);

        initializedTurret =
            ValidateTurretConfiguration();
    }

    #endregion

    #region AI State Overrides

    protected override void UpdateGrounding()
    {
    }

    protected override void UpdateIdleState()
    {
        StopAgent();

        if (TryAcquireTarget())
        {
            SetState(
                AIState.Chase);
        }
        else
        {
            UpdateReturnToRest();
        }
    }

    protected override void UpdatePatrolState()
    {
        UpdateIdleState();
    }

    protected override void UpdateChaseState()
    {
        if (!IsTargetUsable())
        {
            HandleTargetUnavailable();
            return;
        }

        if (requireLineOfSight &&
            !HasLineOfSight())
        {
            ChangeTurretState(
                TurretState.Searching);

            UpdateReturnToRest();
            return;
        }

        AimAtTarget();

        if (!aimSoundPlayed)
        {
            PlaySound(
                aimSound);

            aimSoundPlayed =
                true;
        }

        if (!HasAimLock())
        {
            ChangeTurretState(
                TurretState.Aiming);

            return;
        }

        ChangeTurretState(
            TurretState.Tracking);

        Aimed?.Invoke(
            this,
            GetPredictedTargetPosition());

        if (CanFire())
        {
            BeginFiringSequence(
                bypassWindup: false);
        }
    }

    protected override void UpdateAttackState()
    {
        UpdateChaseState();
    }

    protected override void UpdateReturningState()
    {
        UpdateReturnToRest();

        if (!HasReturnedToRest())
            return;

        SetState(
            AIState.Idle);
    }

    protected override void UpdateInAirState()
    {
        SetState(
            AIState.Idle);
    }

    protected override bool MoveAgentTo(
        Vector3 destination)
    {
        StopAgent();

        return false;
    }

    protected override void StopAgent()
    {
        if (Agent == null ||
            !Agent.enabled ||
            !Agent.isOnNavMesh)
        {
            return;
        }

        Agent.isStopped =
            true;

        Agent.ResetPath();
    }

    protected override void FaceTarget()
    {
        AimAtTarget();
    }

    #endregion

    #region Turret Behaviour

    private void UpdateTurretBehaviour()
    {
        if (!initializedTurret ||
            CurrentState ==
                AIState.Stunned)
        {
            return;
        }

        if (turretState ==
            TurretState.Firing)
        {
            UpdateFiringSequence();
        }
        else if (turretState ==
                 TurretState.CoolingDown)
        {
            if (fireCooldownTimer <= 0f)
            {
                ChangeTurretState(
                    IsTargetUsable()
                        ? TurretState.Tracking
                        : TurretState.Searching);
            }
        }
    }

    private void AimAtTarget()
    {
        if (!IsTargetUsable() ||
            rotatingBase == null ||
            aimingBarrel == null)
        {
            return;
        }

        Vector3 targetPosition =
            GetPredictedTargetPosition();

        AimStarted?.Invoke(
            this);

        RotateBaseToward(
            targetPosition);

        RotateBarrelToward(
            targetPosition);
    }

    private void RotateBaseToward(
        Vector3 targetPosition)
    {
        Vector3 worldDirection =
            targetPosition -
            rotatingBase.position;

        worldDirection.y =
            0f;

        if (worldDirection.sqrMagnitude <=
            MinimumDirectionSqrMagnitude)
        {
            return;
        }

        Quaternion desiredWorldRotation =
            Quaternion.LookRotation(
                worldDirection.normalized,
                Vector3.up);

        Quaternion relativeRotation =
            Quaternion.Inverse(
                transform.rotation) *
            desiredWorldRotation;

        float targetYaw =
            NormalizeAngle(
                relativeRotation.eulerAngles.y);

        targetYaw =
            Mathf.Clamp(
                targetYaw,
                -maximumHorizontalAngle,
                maximumHorizontalAngle);

        Quaternion clampedLocalRotation =
            baseRestLocalRotation *
            Quaternion.Euler(
                0f,
                targetYaw,
                0f);

        rotatingBase.localRotation =
            Quaternion.RotateTowards(
                rotatingBase.localRotation,
                clampedLocalRotation,
                horizontalTurnSpeed *
                Time.deltaTime);
    }

    private void RotateBarrelToward(
        Vector3 targetPosition)
    {
        Vector3 direction =
            targetPosition -
            aimingBarrel.position;

        if (direction.sqrMagnitude <=
            MinimumDirectionSqrMagnitude)
        {
            return;
        }

        Vector3 localDirection =
            rotatingBase.InverseTransformDirection(
                direction.normalized);

        float targetPitch =
            -Mathf.Atan2(
                localDirection.y,
                new Vector2(
                    localDirection.x,
                    localDirection.z)
                .magnitude) *
            Mathf.Rad2Deg;

        targetPitch =
            Mathf.Clamp(
                targetPitch,
                -maximumVerticalAngle,
                minimumVerticalAngle);

        Quaternion desiredLocalRotation =
            barrelRestLocalRotation *
            Quaternion.Euler(
                targetPitch,
                0f,
                0f);

        aimingBarrel.localRotation =
            Quaternion.RotateTowards(
                aimingBarrel.localRotation,
                desiredLocalRotation,
                verticalTurnSpeed *
                Time.deltaTime);
    }

    private void UpdateReturnToRest()
    {
        aimSoundPlayed =
            false;

        if (!returnToRestRotation)
            return;

        ChangeTurretState(
            TurretState.Returning);

        if (rotatingBase != null)
        {
            rotatingBase.localRotation =
                Quaternion.RotateTowards(
                    rotatingBase.localRotation,
                    baseRestLocalRotation,
                    returnRotationSpeed *
                    Time.deltaTime);
        }

        if (aimingBarrel != null)
        {
            aimingBarrel.localRotation =
                Quaternion.RotateTowards(
                    aimingBarrel.localRotation,
                    barrelRestLocalRotation,
                    returnRotationSpeed *
                    Time.deltaTime);
        }
    }

    private void RestoreRestRotations(
        bool instant)
    {
        if (rotatingBase != null)
        {
            rotatingBase.localRotation =
                instant
                    ? baseRestLocalRotation
                    : Quaternion.RotateTowards(
                        rotatingBase.localRotation,
                        baseRestLocalRotation,
                        returnRotationSpeed *
                        Time.deltaTime);
        }

        if (aimingBarrel != null)
        {
            aimingBarrel.localRotation =
                instant
                    ? barrelRestLocalRotation
                    : Quaternion.RotateTowards(
                        aimingBarrel.localRotation,
                        barrelRestLocalRotation,
                        returnRotationSpeed *
                        Time.deltaTime);
        }
    }

    #endregion

    #region Target Prediction

    private void UpdateTargetVelocity()
    {
        if (!IsTargetUsable())
        {
            targetPositionInitialized =
                false;

            estimatedTargetVelocity =
                Vector3.zero;

            return;
        }

        Vector3 currentPosition =
            Target.position;

        if (!targetPositionInitialized)
        {
            lastTargetPosition =
                currentPosition;

            targetPositionInitialized =
                true;

            return;
        }

        float deltaTime =
            Mathf.Max(
                Time.deltaTime,
                0.0001f);

        estimatedTargetVelocity =
            (currentPosition -
             lastTargetPosition) /
            deltaTime;

        lastTargetPosition =
            currentPosition;
    }

    private Vector3 GetPredictedTargetPosition()
    {
        if (!IsTargetUsable())
            return transform.position;

        Vector3 targetPosition =
            Target.position;

        if (!leadTarget ||
            projectileSpeed <= 0f)
        {
            return targetPosition;
        }

        float distance =
            Vector3.Distance(
                projectileSpawnPoint.position,
                targetPosition);

        if (!float.IsFinite(
                distance))
        {
            return targetPosition;
        }

        float leadTime =
            Mathf.Clamp(
                distance /
                projectileSpeed,
                0f,
                maximumLeadTime);

        Vector3 predictedPosition =
            targetPosition +
            estimatedTargetVelocity *
            leadTime;

        return
            IsFiniteVector(
                predictedPosition)
                ? predictedPosition
                : targetPosition;
    }

    #endregion

    #region Firing

    private bool CanFire()
    {
        return
            initializedTurret &&
            projectilePrefab != null &&
            projectileSpawnPoint != null &&
            IsTargetUsable() &&
            fireCooldownTimer <= 0f &&
            turretState !=
                TurretState.Firing &&
            HasAimLock() &&
            (!requireLineOfSight ||
             HasLineOfSight());
    }

    private void BeginFiringSequence(
        bool bypassWindup)
    {
        ChangeTurretState(
            TurretState.Firing);

        firingWindupTimer =
            bypassWindup
                ? 0f
                : firingWindup;

        remainingBurstShots =
            fireMode ==
                FireMode.Burst
                ? burstCount
                : 1;

        burstTimer =
            0f;
    }

    private void UpdateFiringSequence()
    {
        if (!IsTargetUsable())
        {
            CancelFiringSequence();
            return;
        }

        if (firingWindupTimer > 0f)
        {
            firingWindupTimer =
                Mathf.Max(
                    0f,
                    firingWindupTimer -
                    Time.deltaTime);

            AimAtTarget();

            return;
        }

        burstTimer =
            Mathf.Max(
                0f,
                burstTimer -
                Time.deltaTime);

        if (burstTimer > 0f)
            return;

        FireProjectile();

        remainingBurstShots--;

        if (remainingBurstShots > 0)
        {
            burstTimer =
                burstInterval;

            return;
        }

        fireCooldownTimer =
            fireCooldown;

        ChangeTurretState(
            TurretState.CoolingDown);

        BurstCompleted?.Invoke(
            this);

        PlaySound(
            cooldownSound);
    }

    private void FireProjectile()
    {
        if (projectilePrefab == null ||
            projectileSpawnPoint == null)
        {
            return;
        }

        Vector3 spawnPosition =
            projectileSpawnPoint.position +
            projectileSpawnPoint.forward *
            projectileSpawnOffset;

        Vector3 fireDirection =
            GetPredictedTargetPosition() -
            spawnPosition;

        if (!IsFiniteVector(
                fireDirection) ||
            fireDirection.sqrMagnitude <=
                MinimumDirectionSqrMagnitude)
        {
            fireDirection =
                projectileSpawnPoint.forward;
        }

        fireDirection.Normalize();

        Quaternion spawnRotation =
            Quaternion.LookRotation(
                fireDirection,
                Vector3.up);

        GameObject projectile =
            Instantiate(
                projectilePrefab,
                spawnPosition,
                spawnRotation);

        Rigidbody projectileRigidbody =
            projectile.GetComponent<Rigidbody>();

        projectileRigidbody ??=
            projectile.GetComponentInChildren<Rigidbody>(
                includeInactive: true);

        if (projectileRigidbody != null)
        {
            Vector3 velocity =
                projectile.transform.forward *
                projectileSpeed;

            if (inheritTurretVelocity)
            {
                Rigidbody ownerRigidbody =
                    GetComponent<Rigidbody>();

                if (ownerRigidbody != null)
                {
                    velocity +=
                        ownerRigidbody.linearVelocity;
                }
            }

            projectileRigidbody.linearVelocity =
                velocity;
        }

        if (projectileLifetime > 0f)
        {
            Destroy(
                projectile,
                projectileLifetime);
        }

        muzzleFlash?.Play();

        PlaySound(
            fireSound);

        ProjectileFired?.Invoke(
            this,
            projectile);
    }

    private void CancelFiringSequence()
    {
        firingWindupTimer =
            0f;

        burstTimer =
            0f;

        remainingBurstShots =
            0;

        ChangeTurretState(
            TurretState.Searching);
    }

    #endregion

    #region Line Of Sight

    private bool HasLineOfSight()
    {
        if (!IsTargetUsable() ||
            projectileSpawnPoint == null)
        {
            return false;
        }

        Vector3 origin =
            projectileSpawnPoint.position;

        Vector3 destination =
            GetPredictedTargetPosition();

        Vector3 direction =
            destination -
            origin;

        float distance =
            direction.magnitude;

        if (!float.IsFinite(
                distance) ||
            distance <=
                0f)
        {
            return false;
        }

        direction /=
            distance;

        int hitCount =
            Physics.RaycastNonAlloc(
                origin,
                direction,
                lineOfSightHits,
                distance,
                lineOfSightBlockingLayers,
                QueryTriggerInteraction.Ignore);

        float closestDistance =
            float.PositiveInfinity;

        Transform closestTransform =
            null;

        for (int index = 0;
             index < hitCount;
             index++)
        {
            RaycastHit hit =
                lineOfSightHits[index];

            if (hit.collider == null ||
                IsOwnCollider(
                    hit.collider) ||
                hit.distance >=
                    closestDistance)
            {
                continue;
            }

            closestDistance =
                hit.distance;

            closestTransform =
                hit.transform;
        }

        if (closestTransform == null)
            return true;

        return
            closestTransform ==
                Target ||
            closestTransform.IsChildOf(
                Target);
    }

    #endregion

    #region Runtime Safety

    protected override bool RunRuntimeSafetyChecks()
    {
        if (!base.RunRuntimeSafetyChecks())
            return false;

        return RunTurretSafetyChecks();
    }

    private bool RunTurretSafetyChecks()
    {
        if (!ValidateTurretReferences())
        {
            ResolveTurretReferences();
            ConfigureTurretComponents();

            if (!ValidateTurretReferences())
            {
                EnterTurretSafetyShutdown(
                    "Required turret references could not be restored.");

                return false;
            }
        }

        if (!ValidateTransform(
                transform) ||
            !ValidateTransform(
                rotatingBase) ||
            !ValidateTransform(
                aimingBarrel))
        {
            EnterTurretSafetyShutdown(
                "A turret Transform contains invalid values.");

            return false;
        }

        if (restoreDisabledComponents &&
            turretAudioSource != null &&
            !turretAudioSource.enabled)
        {
            turretAudioSource.enabled =
                true;
        }

        return true;
    }

    private bool ValidateTurretReferences()
    {
        return
            rotatingBase != null &&
            aimingBarrel != null &&
            projectileSpawnPoint != null;
    }

    private bool ValidateTransform(
        Transform targetTransform)
    {
        if (targetTransform == null)
            return false;

        Vector3 scale =
            targetTransform.lossyScale;

        return
            IsFiniteVector(
                targetTransform.position) &&
            IsFiniteQuaternion(
                targetTransform.rotation) &&
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

    private void EnterTurretSafetyShutdown(
        string reason)
    {
        initializedTurret =
            false;

        CancelFiringSequence();

        ChangeTurretState(
            TurretState.Disabled);

        Debug.LogError(
            $"{nameof(TurretAI)} entered safety shutdown on '{name}': {reason}",
            this);

        enabled =
            false;
    }

    #endregion

    #region Validation

    private bool ValidateTurretConfiguration()
    {
        if (!ValidateTurretReferences())
        {
            Debug.LogError(
                $"{nameof(TurretAI)} on '{name}' is missing required aiming references.",
                this);

            return false;
        }

        if (projectilePrefab == null)
        {
            Debug.LogWarning(
                $"{nameof(TurretAI)} on '{name}' has no projectile prefab assigned.",
                this);
        }

        return true;
    }

    #endregion

    #region State

    private void ChangeTurretState(
        TurretState newState)
    {
        if (!Enum.IsDefined(
                typeof(TurretState),
                newState))
        {
            return;
        }

        if (turretState ==
            newState)
        {
            return;
        }

        turretState =
            newState;

        TurretStateChanged?.Invoke(
            this,
            turretState);

        if (logTurretState)
        {
            Debug.Log(
                $"{nameof(TurretAI)} on '{name}' changed to {turretState}.",
                this);
        }
    }

    #endregion

    #region Timers

    private void UpdateTurretTimers()
    {
        if (fireCooldownTimer > 0f)
        {
            fireCooldownTimer =
                Mathf.Max(
                    0f,
                    fireCooldownTimer -
                    Time.deltaTime);
        }
    }

    #endregion

    #region Helpers

    private bool IsTargetUsable()
    {
        return
            Target != null &&
            Target.gameObject.activeInHierarchy;
    }

    private bool HasAimLock()
    {
        if (!IsTargetUsable() ||
            projectileSpawnPoint == null)
        {
            return false;
        }

        Vector3 direction =
            GetPredictedTargetPosition() -
            projectileSpawnPoint.position;

        if (direction.sqrMagnitude <=
            MinimumDirectionSqrMagnitude)
        {
            return true;
        }

        float angle =
            Vector3.Angle(
                projectileSpawnPoint.forward,
                direction.normalized);

        return
            float.IsFinite(
                angle) &&
            angle <=
                aimTolerance;
    }

    private bool HasReturnedToRest()
    {
        if (rotatingBase == null ||
            aimingBarrel == null)
        {
            return false;
        }

        return
            Quaternion.Angle(
                rotatingBase.localRotation,
                baseRestLocalRotation) <=
                aimTolerance &&
            Quaternion.Angle(
                aimingBarrel.localRotation,
                barrelRestLocalRotation) <=
                aimTolerance;
    }

    private void HandleTargetUnavailable()
    {
        aimSoundPlayed =
            false;

        ClearTarget();
        CancelFiringSequence();

        SetState(
            AIState.Returning);
    }

    protected override void PlaySound(
        AudioClip clip)
    {
        if (turretAudioSource == null ||
            clip == null)
        {
            return;
        }

        turretAudioSource.PlayOneShot(
            clip);
    }

    private bool IsOwnCollider(
        Collider candidate)
    {
        if (candidate == null)
            return false;

        Transform candidateTransform =
            candidate.transform;

        return
            candidateTransform ==
                transform ||
            candidateTransform.IsChildOf(
                transform);
    }

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

    private static float NormalizeAngle(
        float angle)
    {
        while (angle > 180f)
        {
            angle -=
                360f;
        }

        while (angle < -180f)
        {
            angle +=
                360f;
        }

        return angle;
    }
    #endregion
}
