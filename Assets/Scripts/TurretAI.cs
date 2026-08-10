using System;
using UnityEngine;

public class TurretAI : AIController
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

    #region Aiming

    [Header("Aiming")]

    [SerializeField]
    private Transform rotatingBase;

    [SerializeField]
    private Transform aimingBarrel;

    [SerializeField]
    private Transform projectileSpawnPoint;

    [SerializeField, Min(0f)]
    private float horizontalTurnSpeed = 180f;

    [SerializeField, Min(0f)]
    private float verticalTurnSpeed = 120f;

    [SerializeField, Range(0f, 180f)]
    private float maximumHorizontalAngle = 180f;

    [SerializeField, Range(0f, 89f)]
    private float maximumUpAngle = 65f;

    [SerializeField, Range(0f, 89f)]
    private float maximumDownAngle = 10f;

    [SerializeField, Min(0.1f)]
    private float aimTolerance = 3f;

    [SerializeField]
    private bool returnToRestRotation = true;

    [SerializeField, Min(0f)]
    private float returnRotationSpeed = 120f;

    #endregion

    #region Target Leading

    [Header("Target Leading")]

    [SerializeField]
    private bool leadTarget = true;

    [SerializeField, Min(0f)]
    private float projectileSpeed = 30f;

    [SerializeField, Min(0f)]
    private float maximumLeadTime = 2f;

    #endregion

    #region Line Of Sight

    [Header("Line Of Sight")]

    [SerializeField]
    private bool requireLineOfSight = true;

    [SerializeField]
    private LayerMask lineOfSightLayers = ~0;

    #endregion

    #region Firing

    [Header("Firing")]

    [SerializeField]
    private FireMode fireMode =
        FireMode.Single;

    [SerializeField]
    private GameObject projectilePrefab;

    [SerializeField, Min(0f)]
    private float fireCooldown = 1f;

    [SerializeField, Min(0f)]
    private float firingWindup = 0.25f;

    [SerializeField, Min(1)]
    private int burstCount = 3;

    [SerializeField, Min(0.01f)]
    private float burstInterval = 0.12f;

    [SerializeField]
    private bool inheritTurretVelocity;

    #endregion

    #region Projectile

    [Header("Projectile")]

    [SerializeField, Min(0f)]
    private float projectileLifetime = 10f;

    [SerializeField, Min(0f)]
    private float projectileSpawnOffset = 0.05f;

    #endregion

    #region Presentation

    [Header("Presentation")]

    [SerializeField]
    private ParticleSystem muzzleFlash;

    [SerializeField]
    private AudioSource turretAudioSource;

    [SerializeField]
    private AudioClip aimSound;

    [SerializeField]
    private AudioClip fireSound;

    [SerializeField]
    private AudioClip cooldownSound;

    #endregion

    #region Debug

    [Header("Debug")]

    [SerializeField]
    private TurretState turretState =
        TurretState.Searching;

    [SerializeField]
    private bool drawAimDebug = true;

    #endregion

    #region Runtime State

    private Quaternion baseRestRotation;
    private Quaternion barrelRestRotation;

    private Vector3 previousTargetPosition;
    private Vector3 estimatedTargetVelocity;

    private float fireCooldownTimer;
    private float firingWindupTimer;
    private float burstTimer;

    private int remainingBurstShots;

    private bool targetPositionInitialized;
    private bool aimSoundPlayed;
    private bool turretInitialized;

    #endregion

    #region Events

    public event Action<TurretAI>
        AimStarted;

    public event Action<TurretAI, Vector3>
        Aimed;

    public event Action<TurretAI, GameObject>
        ProjectileFired;

    public event Action<TurretAI>
        BurstCompleted;

    public event Action<TurretAI, TurretState>
        TurretStateChanged;

    #endregion

    #region Properties

    public TurretState CurrentTurretState =>
        turretState;

    public bool IsTurretInitialized =>
        turretInitialized;

    public bool IsAimed =>
        HasAimLock();

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        ResolveTurretReferences();
        CacheRestRotations();
        ConfigureTurret();
        InitializeTurret();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        ResolveTurretReferences();
        ConfigureTurret();

        if (!turretInitialized)
        {
            InitializeTurret();
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
            !turretInitialized ||
            IsDead ||
            CurrentState ==
                AIState.Disabled)
        {
            return;
        }

        UpdateTimers();
        UpdateTargetVelocity();
        UpdateTurretState();
    }

    protected override void OnDisable()
    {
        CancelFiring();

        ChangeTurretState(
            TurretState.Disabled);

        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        AimStarted =
            null;

        Aimed =
            null;

        ProjectileFired =
            null;

        BurstCompleted =
            null;

        TurretStateChanged =
            null;

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

        maximumUpAngle =
            Mathf.Clamp(
                maximumUpAngle,
                0f,
                89f);

        maximumDownAngle =
            Mathf.Clamp(
                maximumDownAngle,
                0f,
                89f);

        aimTolerance =
            Mathf.Max(
                0.1f,
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
    }

    #endregion

    #region Initialization

    protected override bool Initialize()
    {
        bool initialized =
            base.Initialize();

        if (!initialized)
        {
            return false;
        }

        ResolveTurretReferences();
        CacheRestRotations();
        ConfigureTurret();
        InitializeTurret();

        return
            turretInitialized;
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
            baseRestRotation =
                rotatingBase.localRotation;
        }

        if (aimingBarrel != null)
        {
            barrelRestRotation =
                aimingBarrel.localRotation;
        }
    }

    private void ConfigureTurret()
    {
        if (Agent != null)
        {
            Agent.updatePosition =
                false;

            Agent.updateRotation =
                false;

            if (Agent.enabled &&
                Agent.isOnNavMesh)
            {
                Agent.isStopped =
                    true;

                Agent.ResetPath();
            }
        }

        if (turretAudioSource != null)
        {
            turretAudioSource.playOnAwake =
                false;
        }
    }

    private void InitializeTurret()
    {
        fireCooldownTimer =
            0f;

        firingWindupTimer =
            0f;

        burstTimer =
            0f;

        remainingBurstShots =
            0;

        previousTargetPosition =
            Vector3.zero;

        estimatedTargetVelocity =
            Vector3.zero;

        targetPositionInitialized =
            false;

        aimSoundPlayed =
            false;

        turretInitialized =
            rotatingBase != null &&
            aimingBarrel != null &&
            projectileSpawnPoint != null;

        ChangeTurretState(
            turretInitialized
                ? TurretState.Searching
                : TurretState.Disabled);
    }

    #endregion

    #region AI Overrides

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

            return;
        }

        UpdateReturnToRest();
    }

    protected override void UpdatePatrolState()
    {
        UpdateIdleState();
    }

    protected override void UpdateChaseState()
    {
        if (!IsTargetUsable())
        {
            HandleTargetLost();

            return;
        }

        if (requireLineOfSight &&
            !HasTurretLineOfSight())
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
            BeginFiring();
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
        {
            return;
        }

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

    #region Turret State

    private void UpdateTurretState()
    {
        if (turretState ==
            TurretState.Firing)
        {
            UpdateFiring();

            return;
        }

        if (turretState ==
                TurretState.CoolingDown &&
            fireCooldownTimer <= 0f)
        {
            ChangeTurretState(
                IsTargetUsable()
                    ? TurretState.Tracking
                    : TurretState.Searching);
        }
    }

    private void ChangeTurretState(
        TurretState newState)
    {
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
    }

    #endregion

    #region Aiming

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

        if (!IsFiniteVector(
                targetPosition))
        {
            return;
        }

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
        Vector3 direction =
            targetPosition -
            rotatingBase.position;

        direction.y =
            0f;

        if (!IsFiniteVector(
                direction) ||
            direction.sqrMagnitude <=
                0.0001f)
        {
            return;
        }

        Quaternion desiredWorldRotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);

        Quaternion relativeRotation =
            Quaternion.Inverse(
                transform.rotation) *
            desiredWorldRotation;

        float yaw =
            NormalizeAngle(
                relativeRotation.eulerAngles.y);

        yaw =
            Mathf.Clamp(
                yaw,
                -maximumHorizontalAngle,
                maximumHorizontalAngle);

        Quaternion targetRotation =
            baseRestRotation *
            Quaternion.Euler(
                0f,
                yaw,
                0f);

        rotatingBase.localRotation =
            Quaternion.RotateTowards(
                rotatingBase.localRotation,
                targetRotation,
                horizontalTurnSpeed *
                    Time.deltaTime);
    }

    private void RotateBarrelToward(
        Vector3 targetPosition)
    {
        Vector3 direction =
            targetPosition -
            aimingBarrel.position;

        if (!IsFiniteVector(
                direction) ||
            direction.sqrMagnitude <=
                0.0001f)
        {
            return;
        }

        Vector3 localDirection =
            rotatingBase
                .InverseTransformDirection(
                    direction.normalized);

        float horizontalMagnitude =
            new Vector2(
                localDirection.x,
                localDirection.z)
            .magnitude;

        float pitch =
            -Mathf.Atan2(
                localDirection.y,
                horizontalMagnitude) *
            Mathf.Rad2Deg;

        pitch =
            Mathf.Clamp(
                pitch,
                -maximumUpAngle,
                maximumDownAngle);

        Quaternion targetRotation =
            barrelRestRotation *
            Quaternion.Euler(
                pitch,
                0f,
                0f);

        aimingBarrel.localRotation =
            Quaternion.RotateTowards(
                aimingBarrel.localRotation,
                targetRotation,
                verticalTurnSpeed *
                    Time.deltaTime);
    }

    #endregion

    #region Rest Rotation

    private void UpdateReturnToRest()
    {
        aimSoundPlayed =
            false;

        if (!returnToRestRotation)
        {
            return;
        }

        ChangeTurretState(
            TurretState.Returning);

        if (rotatingBase != null)
        {
            rotatingBase.localRotation =
                Quaternion.RotateTowards(
                    rotatingBase.localRotation,
                    baseRestRotation,
                    returnRotationSpeed *
                        Time.deltaTime);
        }

        if (aimingBarrel != null)
        {
            aimingBarrel.localRotation =
                Quaternion.RotateTowards(
                    aimingBarrel.localRotation,
                    barrelRestRotation,
                    returnRotationSpeed *
                        Time.deltaTime);
        }
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
                baseRestRotation) <=
                aimTolerance &&
            Quaternion.Angle(
                aimingBarrel.localRotation,
                barrelRestRotation) <=
                aimTolerance;
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

        Vector3 position =
            Target.position;

        if (!targetPositionInitialized)
        {
            previousTargetPosition =
                position;

            targetPositionInitialized =
                true;

            return;
        }

        float deltaTime =
            Mathf.Max(
                Time.deltaTime,
                0.0001f);

        Vector3 velocity =
            (
                position -
                previousTargetPosition
            ) /
            deltaTime;

        if (IsFiniteVector(
                velocity))
        {
            estimatedTargetVelocity =
                velocity;
        }

        previousTargetPosition =
            position;
    }

    private Vector3 GetPredictedTargetPosition()
    {
        if (!IsTargetUsable())
        {
            return
                transform.position;
        }

        Vector3 targetPosition =
            Target.position;

        if (!leadTarget ||
            projectileSpeed <= 0f ||
            projectileSpawnPoint == null)
        {
            return
                targetPosition;
        }

        float distance =
            Vector3.Distance(
                projectileSpawnPoint.position,
                targetPosition);

        if (!float.IsFinite(
                distance))
        {
            return
                targetPosition;
        }

        float leadTime =
            Mathf.Clamp(
                distance /
                    projectileSpeed,
                0f,
                maximumLeadTime);

        Vector3 prediction =
            targetPosition +
            estimatedTargetVelocity *
                leadTime;

        return
            IsFiniteVector(
                prediction)
                ? prediction
                : targetPosition;
    }

    #endregion

    #region Firing

    public bool FireImmediately()
    {
        if (!CanFire())
        {
            return false;
        }

        BeginFiring(
            true);

        return true;
    }

    private bool CanFire()
    {
        return
            turretInitialized &&
            projectilePrefab != null &&
            projectileSpawnPoint != null &&
            IsTargetUsable() &&
            fireCooldownTimer <= 0f &&
            turretState !=
                TurretState.Firing &&
            HasAimLock() &&
            (
                !requireLineOfSight ||
                HasTurretLineOfSight()
            );
    }

    private void BeginFiring(
        bool bypassWindup = false)
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

    private void UpdateFiring()
    {
        if (!IsTargetUsable())
        {
            CancelFiring();

            return;
        }

        AimAtTarget();

        if (firingWindupTimer > 0f)
        {
            firingWindupTimer =
                Mathf.Max(
                    0f,
                    firingWindupTimer -
                        Time.deltaTime);

            return;
        }

        burstTimer =
            Mathf.Max(
                0f,
                burstTimer -
                    Time.deltaTime);

        if (burstTimer > 0f)
        {
            return;
        }

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

        Vector3 direction =
            GetPredictedTargetPosition() -
            spawnPosition;

        if (!IsFiniteVector(
                direction) ||
            direction.sqrMagnitude <=
                0.0001f)
        {
            direction =
                projectileSpawnPoint.forward;
        }

        if (!IsFiniteVector(
                direction) ||
            direction.sqrMagnitude <=
                0.0001f)
        {
            return;
        }

        direction.Normalize();

        Quaternion rotation =
            Quaternion.LookRotation(
                direction,
                Vector3.up);

        GameObject projectile =
            Instantiate(
                projectilePrefab,
                spawnPosition,
                rotation);

        Rigidbody body =
            projectile.GetComponent<Rigidbody>();

        body ??=
            projectile.GetComponentInChildren<Rigidbody>(
                includeInactive: true);

        if (body != null)
        {
            Vector3 velocity =
                direction *
                    projectileSpeed;

            if (inheritTurretVelocity)
            {
                Rigidbody turretBody =
                    GetComponent<Rigidbody>();

                if (turretBody != null &&
                    IsFiniteVector(
                        turretBody.linearVelocity))
                {
                    velocity +=
                        turretBody.linearVelocity;
                }
            }

            if (IsFiniteVector(
                    velocity))
            {
                body.linearVelocity =
                    velocity;
            }
        }

        if (projectileLifetime > 0f)
        {
            Destroy(
                projectile,
                projectileLifetime);
        }

        if (muzzleFlash != null)
        {
            muzzleFlash.Play();
        }

        PlaySound(
            fireSound);

        ProjectileFired?.Invoke(
            this,
            projectile);
    }

    private void CancelFiring()
    {
        firingWindupTimer =
            0f;

        burstTimer =
            0f;

        remainingBurstShots =
            0;

        if (turretState !=
            TurretState.Disabled)
        {
            ChangeTurretState(
                TurretState.Searching);
        }
    }

    #endregion

    #region Aim Lock

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

        if (!IsFiniteVector(
                direction))
        {
            return false;
        }

        if (direction.sqrMagnitude <=
            0.0001f)
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

    #endregion

    #region Line Of Sight

    private bool HasTurretLineOfSight()
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
            distance <= 0.001f)
        {
            return false;
        }

        direction /=
            distance;

        if (!Physics.Raycast(
                origin,
                direction,
                out RaycastHit hit,
                distance,
                lineOfSightLayers,
                QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        if (hit.collider == null)
        {
            return true;
        }

        if (IsOwnCollider(
                hit.collider))
        {
            return true;
        }

        Transform hitTransform =
            hit.transform;

        return
            hitTransform ==
                Target ||
            hitTransform.IsChildOf(
                Target) ||
            Target.IsChildOf(
                hitTransform);
    }

    #endregion

    #region Target

    private bool IsTargetUsable()
    {
        return
            Target != null &&
            Target.gameObject.activeInHierarchy &&
            IsFiniteVector(
                Target.position);
    }

    private void HandleTargetLost()
    {
        aimSoundPlayed =
            false;

        ClearTarget();
        CancelFiring();

        SetState(
            AIState.Returning);
    }

    #endregion

    #region Reset

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

        if (rotatingBase != null)
        {
            rotatingBase.localRotation =
                baseRestRotation;
        }

        if (aimingBarrel != null)
        {
            aimingBarrel.localRotation =
                barrelRestRotation;
        }

        ChangeTurretState(
            TurretState.Searching);
    }

    #endregion

    #region Timers

    private void UpdateTimers()
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

    #region References

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
            if (child == null)
            {
                continue;
            }

            if (string.Equals(
                    child.name,
                    targetName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    #endregion

    #region Audio

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

    #endregion

    #region Helpers

    private bool IsOwnCollider(
        Collider candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        Transform candidateTransform =
            candidate.transform;

        return
            candidateTransform ==
                transform ||
            candidateTransform.IsChildOf(
                transform);
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

    #region Gizmos

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        if (!drawAimDebug)
        {
            return;
        }

        Transform spawn =
            projectileSpawnPoint != null
                ? projectileSpawnPoint
                : transform;

        Gizmos.DrawRay(
            spawn.position,
            spawn.forward *
                3f);

        if (Target != null &&
            IsFiniteVector(
                Target.position))
        {
            Gizmos.DrawLine(
                spawn.position,
                Target.position);
        }
    }

    #endregion
}