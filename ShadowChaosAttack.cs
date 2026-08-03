using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShadowChaosAttack : MonoBehaviour
{
    #region Constants

    private const int MaxTargetResults = 32;
    private const float MinimumTargetMagnitude = 0.001f;

    #endregion

    #region Animator Hashes

    private static readonly int ChaosAttackHash =
        Animator.StringToHash("Chaos Attack");

    #endregion

    #region Inspector

    [Header("References")]
    [SerializeField] private TeamActionController actionController;
    [SerializeField] private CharacterSwitch characterSwitch;
    [SerializeField] private UltimatePlayerMovement movement;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Transform projectileOrigin;

    [Header("Input")]
    [SerializeField] private KeyCode chaosAttackKey = KeyCode.B;
    [SerializeField] private bool readPlayerInput = true;

    [Header("Target Detection")]
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField, Min(0.1f)] private float targetDetectionRadius = 22f;
    [SerializeField, Range(0f, 180f)] private float targetDetectionAngle = 70f;
    [SerializeField]
    private Vector3 targetDetectionOffset =
        new(0f, 1f, 0f);

    [Header("Chaos Spear")]
    [SerializeField, Min(0.1f)] private float projectileSpeed = 42f;
    [SerializeField, Min(0.1f)] private float projectileRadius = 0.35f;
    [SerializeField, Min(0.05f)] private float projectileLifetime = 1.25f;
    [SerializeField, Min(0f)] private float damage = 35f;
    [SerializeField, Min(0f)] private float knockbackForce = 10f;
    [SerializeField, Min(0.05f)] private float attackRecovery = 0.35f;
    [SerializeField, Min(0f)] private float cooldown = 0.8f;

    [Header("Effects")]
    [SerializeField] private GameObject chaosSpearEffect;
    [SerializeField] private GameObject impactEffect;
    [SerializeField, Min(0f)] private float impactEffectLifetime = 2f;

    [Header("Audio")]
    [SerializeField] private AudioClip chaosAttackSound;
    [SerializeField] private AudioClip impactSound;

    [Header("Debug")]
    [SerializeField] private bool drawTargetDetection = true;
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private readonly Collider[] targetResults =
        new Collider[MaxTargetResults];

    private readonly HashSet<GameObject> damagedTargets =
        new();

    private Coroutine attackRoutine;
    private Transform currentTarget;
    private GameObject activeProjectile;

    private bool isPerformingChaosAttack;
    private bool isInitialized;
    private bool isShuttingDown;

    private float nextAvailableTime;

    #endregion

    #region Public API

    public bool IsPerformingChaosAttack =>
        isPerformingChaosAttack;

    public bool IsInitialized =>
        isInitialized;

    public bool IsOnCooldown =>
        Time.time < nextAvailableTime;

    public Transform CurrentTarget =>
        currentTarget;

    public bool TryStartChaosAttack()
    {
        if (!CanStartChaosAttack())
            return false;

        bool accepted =
            actionController.TryBeginAction(
                TeamActionController.TeamAction.ShadowChaosAttack,
                TeamActionController.TeamFormation.Speed,
                mustBeGrounded: false,
                mustBeAirborne: false,
                surrenderMovementControl: true);

        if (!accepted)
            return false;

        BeginChaosAttack();
        return true;
    }

    public void CancelChaosAttack()
    {
        if (!isPerformingChaosAttack)
            return;

        FinishChaosAttack();
    }

    public void SetInputEnabled(
        bool enabled)
    {
        readPlayerInput = enabled;
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
        if (!InitializeChaosAttack())
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
    }

    private void Update()
    {
        if (!isInitialized ||
            !readPlayerInput ||
            isPerformingChaosAttack)
        {
            return;
        }

        if (Input.GetKeyDown(
                chaosAttackKey))
        {
            TryStartChaosAttack();
        }
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

    private void OnValidate()
    {
        targetDetectionRadius =
            Mathf.Max(
                0.1f,
                targetDetectionRadius);

        targetDetectionAngle =
            Mathf.Clamp(
                targetDetectionAngle,
                0f,
                180f);

        projectileSpeed =
            Mathf.Max(
                0.1f,
                projectileSpeed);

        projectileRadius =
            Mathf.Max(
                0.1f,
                projectileRadius);

        projectileLifetime =
            Mathf.Max(
                0.05f,
                projectileLifetime);

        damage =
            Mathf.Max(
                0f,
                damage);

        knockbackForce =
            Mathf.Max(
                0f,
                knockbackForce);

        attackRecovery =
            Mathf.Max(
                0.05f,
                attackRecovery);

        cooldown =
            Mathf.Max(
                0f,
                cooldown);

        impactEffectLifetime =
            Mathf.Max(
                0f,
                impactEffectLifetime);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawTargetDetection)
            return;

        Vector3 origin =
            GetTargetDetectionOrigin();

        Gizmos.DrawWireSphere(
            origin,
            targetDetectionRadius);

        if (currentTarget != null)
        {
            Gizmos.DrawLine(
                origin,
                GetTargetPosition(currentTarget));
        }
    }

    #endregion

    #region Initialization

    public bool InitializeChaosAttack()
    {
        if (isInitialized)
            return true;

        CacheComponents();
        ResolveReferences();

        if (!ValidateConfiguration())
        {
            Debug.LogError(
                $"ShadowChaosAttack failed to initialize on '{name}'.",
                this);

            isInitialized = false;
            return false;
        }

        ResetRuntimeState();

        isInitialized = true;
        return true;
    }

    private void CacheComponents()
    {
        actionController ??=
            GetComponentInParent<TeamActionController>();

        characterSwitch ??=
            GetComponentInParent<CharacterSwitch>();

        movement ??=
            GetComponentInParent<UltimatePlayerMovement>();

        playerRigidbody ??=
            GetComponentInParent<Rigidbody>();

        audioSource ??=
            GetComponentInParent<AudioSource>();
    }

    private void ResolveReferences()
    {
        animator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        if (animator == null &&
            movement != null)
        {
            animator =
                movement.GetComponentInChildren<Animator>(
                    includeInactive: true);
        }

        projectileOrigin ??=
            transform;
    }

    #endregion

    #region Chaos Attack State

    private bool CanUseChaosAttack()
    {
        return
            TeamSetup.Instance != null &&
            TeamSetup.Instance.PlayableTeam == PlayableTeam.TeamDark &&
            characterSwitch != null &&
            characterSwitch.CurrentLeaderType == CHARACTERTYPES.Speed &&
            actionController != null &&
            actionController.CurrentFormation ==
                TeamActionController.TeamFormation.Speed;
    }

    private bool CanStartChaosAttack()
    {
        return
            CanUseChaosAttack() &&
            isInitialized &&
            !isPerformingChaosAttack &&
            !IsOnCooldown &&
            playerRigidbody != null;
    }

    private void BeginChaosAttack()
    {
        isPerformingChaosAttack = true;
        damagedTargets.Clear();

        playerRigidbody.linearVelocity =
            Vector3.zero;

        playerRigidbody.angularVelocity =
            Vector3.zero;

        currentTarget =
            FindBestTarget();

        PlayAnimation();
        PlaySound(chaosAttackSound);

        attackRoutine =
            StartCoroutine(
                ChaosAttackRoutine());

        LogStateChange(
            currentTarget != null
                ? $"Shadow Chaos Attack targeted '{currentTarget.name}'."
                : "Shadow Chaos Attack fired forward.");
    }

    private IEnumerator ChaosAttackRoutine()
    {
        yield return
            LaunchChaosSpear();

        if (attackRecovery > 0f)
        {
            yield return
                new WaitForSeconds(
                    attackRecovery);
        }

        FinishChaosAttack();
    }

    private void FinishChaosAttack()
    {
        if (!isPerformingChaosAttack)
            return;

        isPerformingChaosAttack = false;

        if (attackRoutine != null)
        {
            StopCoroutine(
                attackRoutine);

            attackRoutine = null;
        }

        DestroyActiveProjectile();

        currentTarget = null;
        damagedTargets.Clear();

        nextAvailableTime =
            Time.time +
            cooldown;

        if (actionController != null &&
            actionController.CurrentAction ==
                TeamActionController.TeamAction.ShadowChaosAttack)
        {
            actionController.EndAction(
                restoreMovementControl: true);
        }

        LogStateChange(
            "Shadow Chaos Attack finished.");
    }

    private void ResetRuntimeState()
    {
        isPerformingChaosAttack = false;
        attackRoutine = null;
        currentTarget = null;
        activeProjectile = null;
        nextAvailableTime = 0f;
        damagedTargets.Clear();
    }

    #endregion

    #region Target Detection

    private Transform FindBestTarget()
    {
        Vector3 origin =
            GetTargetDetectionOrigin();

        int resultCount =
            Physics.OverlapSphereNonAlloc(
                origin,
                targetDetectionRadius,
                targetResults,
                targetLayers,
                QueryTriggerInteraction.Collide);

        Transform bestTarget = null;
        float bestScore = float.MaxValue;

        for (int index = 0;
             index < resultCount;
             index++)
        {
            Collider candidateCollider =
                targetResults[index];

            targetResults[index] = null;

            if (!TryEvaluateTarget(
                    candidateCollider,
                    origin,
                    out Transform candidate,
                    out float score))
            {
                continue;
            }

            if (score >= bestScore)
                continue;

            bestScore = score;
            bestTarget = candidate;
        }

        return bestTarget;
    }

    private bool TryEvaluateTarget(
        Collider candidateCollider,
        Vector3 origin,
        out Transform candidate,
        out float score)
    {
        candidate = null;
        score = float.MaxValue;

        if (candidateCollider == null)
            return false;

        GameObject target =
            candidateCollider.attachedRigidbody != null
                ? candidateCollider.attachedRigidbody.gameObject
                : candidateCollider.gameObject;

        if (target == gameObject ||
            IsTeamCharacter(target))
        {
            return false;
        }

        Vector3 targetPosition =
            candidateCollider.bounds.center;

        Vector3 toTarget =
            targetPosition -
            origin;

        float distance =
            toTarget.magnitude;

        if (distance <= Mathf.Epsilon)
            return false;

        float angle =
            Vector3.Angle(
                transform.forward,
                toTarget);

        if (angle > targetDetectionAngle)
            return false;

        candidate =
            candidateCollider.attachedRigidbody != null
                ? candidateCollider.attachedRigidbody.transform
                : candidateCollider.transform;

        score =
            distance +
            angle /
            Mathf.Max(
                1f,
                targetDetectionAngle);

        return true;
    }

    private Vector3 GetTargetDetectionOrigin()
    {
        return
            projectileOrigin != null
                ? projectileOrigin.position
                : transform.position +
                  transform.TransformDirection(
                      targetDetectionOffset);
    }

    private static Vector3 GetTargetPosition(
        Transform target)
    {
        if (target == null)
            return Vector3.zero;

        Collider targetCollider =
            target.GetComponentInChildren<Collider>();

        return
            targetCollider != null
                ? targetCollider.bounds.center
                : target.position;
    }

    #endregion

    #region Chaos Spear

    private IEnumerator LaunchChaosSpear()
    {
        Vector3 startPosition =
            projectileOrigin != null
                ? projectileOrigin.position
                : transform.position;

        Vector3 direction =
            GetLaunchDirection(
                startPosition);

        activeProjectile =
            CreateProjectileVisual(
                startPosition,
                direction);

        float elapsed = 0f;

        Vector3 currentPosition =
            startPosition;

        WaitForFixedUpdate fixedUpdate =
            new();

        while (elapsed <
               projectileLifetime)
        {
            elapsed +=
                Time.fixedDeltaTime;

            UpdateProjectileDirection(
                ref direction,
                currentPosition);

            float travelDistance =
                projectileSpeed *
                Time.fixedDeltaTime;

            if (TryHitTarget(
                    currentPosition,
                    direction,
                    travelDistance,
                    out RaycastHit hit))
            {
                HandleImpact(
                    hit);

                yield break;
            }

            MoveProjectile(
                ref currentPosition,
                direction,
                travelDistance);

            UpdateProjectile(
                currentPosition,
                direction);

            yield return fixedUpdate;
        }

        DestroyActiveProjectile();
    }

    private void UpdateProjectileDirection(
        ref Vector3 direction,
        Vector3 projectilePosition)
    {
        if (currentTarget == null)
            return;

        Vector3 targetDirection =
            GetTargetPosition(
                currentTarget) -
            projectilePosition;

        if (targetDirection.sqrMagnitude >
            MinimumTargetMagnitude)
        {
            direction =
                targetDirection.normalized;
        }
    }

    private void MoveProjectile(
        ref Vector3 position,
        Vector3 direction,
        float distance)
    {
        position +=
            direction * distance;
    }

    private void UpdateProjectile(
        Vector3 position,
        Vector3 direction)
    {
        UpdateProjectileVisual(
            position,
            direction);
    }

    private Vector3 GetLaunchDirection(
        Vector3 startPosition)
    {
        if (currentTarget == null)
            return transform.forward;

        Vector3 direction =
            GetTargetPosition(
                currentTarget) -
            startPosition;

        return
            direction.sqrMagnitude >
            MinimumTargetMagnitude
                ? direction.normalized
                : transform.forward;
    }

    private bool TryHitTarget(
        Vector3 origin,
        Vector3 direction,
        float distance,
        out RaycastHit hit)
    {
        return Physics.SphereCast(
            origin,
            projectileRadius,
            direction,
            out hit,
            distance,
            targetLayers,
            QueryTriggerInteraction.Collide);
    }

    private void HandleImpact(
        RaycastHit hit)
    {
        GameObject target =
            hit.rigidbody != null
                ? hit.rigidbody.gameObject
                : hit.collider.gameObject;

        if (!IsTeamCharacter(target) &&
            damagedTargets.Add(target))
        {
            target.SendMessage(
                "TakeDamage",
                damage,
                SendMessageOptions.DontRequireReceiver);

            ApplyKnockback(
                target,
                hit.rigidbody);
        }

        SpawnImpactEffect(
            hit.point,
            hit.normal);

        PlaySound(
            impactSound);

        DestroyActiveProjectile();
    }

    private void ApplyKnockback(
        GameObject target,
        Rigidbody targetRigidbody)
    {
        if (target == null ||
            targetRigidbody == null ||
            knockbackForce <= 0f)
        {
            return;
        }

        Vector3 direction =
            target.transform.position -
            transform.position;

        direction.y =
            Mathf.Max(
                direction.y,
                0.2f);

        if (direction.sqrMagnitude <=
            MinimumTargetMagnitude)
        {
            direction =
                transform.forward;
        }

        targetRigidbody.AddForce(
            direction.normalized *
            knockbackForce,
            ForceMode.VelocityChange);
    }

    #endregion

    #region Team Filtering

    private bool IsTeamCharacter(
        GameObject target)
    {
        if (target == null ||
            actionController == null)
        {
            return false;
        }

        return
            MatchesCharacter(
                target,
                actionController.SpeedCharacter) ||
            MatchesCharacter(
                target,
                actionController.FlyCharacter) ||
            MatchesCharacter(
                target,
                actionController.PowerCharacter) ||
            target.transform.IsChildOf(
                transform);
    }

    private static bool MatchesCharacter(
        GameObject target,
        Transform character)
    {
        return
            character != null &&
            (target == character.gameObject ||
             target.transform.IsChildOf(character));
    }

    #endregion

    #region Effects

    private GameObject CreateProjectileVisual(
        Vector3 position,
        Vector3 direction)
    {
        if (chaosSpearEffect == null)
            return null;

        return Instantiate(
            chaosSpearEffect,
            position,
            Quaternion.LookRotation(
                direction,
                Vector3.up));
    }

    private void UpdateProjectileVisual(
        Vector3 position,
        Vector3 direction)
    {
        if (activeProjectile == null)
            return;

        activeProjectile.transform.SetPositionAndRotation(
            position,
            Quaternion.LookRotation(
                direction,
                Vector3.up));
    }

    private void SpawnImpactEffect(
        Vector3 position,
        Vector3 normal)
    {
        if (impactEffect == null)
            return;

        Quaternion rotation =
            normal.sqrMagnitude >
            MinimumTargetMagnitude
                ? Quaternion.LookRotation(
                    normal,
                    Vector3.up)
                : Quaternion.identity;

        GameObject spawnedEffect =
            Instantiate(
                impactEffect,
                position,
                rotation);

        if (impactEffectLifetime > 0f)
        {
            Destroy(
                spawnedEffect,
                impactEffectLifetime);
        }
    }

    private void DestroyActiveProjectile()
    {
        if (activeProjectile == null)
            return;

        Destroy(
            activeProjectile);

        activeProjectile = null;
    }

    #endregion

    #region Animation

    private void PlayAnimation()
    {
        if (animator == null)
            return;

        animator.SetTrigger(
            ChaosAttackHash);
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

    #endregion

    #region Validation

    private bool ValidateConfiguration()
    {
        bool valid = true;

        valid &=
            ValidateReference(
                actionController,
                nameof(TeamActionController));

        valid &=
            ValidateReference(
                characterSwitch,
                nameof(CharacterSwitch));

        valid &=
            ValidateReference(
                playerRigidbody,
                nameof(Rigidbody));

        if (movement == null)
        {
            Debug.LogWarning(
                "ShadowChaosAttack could not find UltimatePlayerMovement.",
                this);
        }

        if (animator == null)
        {
            Debug.LogWarning(
                "ShadowChaosAttack could not find an Animator.",
                this);
        }

        if (audioSource == null)
        {
            Debug.LogWarning(
                "ShadowChaosAttack could not find an AudioSource.",
                this);
        }

        if (chaosSpearEffect == null)
        {
            Debug.LogWarning(
                "ShadowChaosAttack has no Chaos Spear visual effect.",
                this);
        }

        return valid;
    }

    private bool ValidateReference(
        Object reference,
        string displayName)
    {
        if (reference != null)
            return true;

        Debug.LogError(
            $"ShadowChaosAttack requires {displayName}.",
            this);

        return false;
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        if (isPerformingChaosAttack)
        {
            FinishChaosAttack();
        }

        DestroyActiveProjectile();
    }

    private void CleanupDestroyedState()
    {
        CleanupRuntimeState();

        isInitialized = false;

        actionController = null;
        characterSwitch = null;
        movement = null;
        playerRigidbody = null;
        animator = null;
        audioSource = null;
        projectileOrigin = null;
        currentTarget = null;
        chaosSpearEffect = null;
        impactEffect = null;

        damagedTargets.Clear();
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
