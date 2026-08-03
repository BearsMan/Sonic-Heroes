using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChaosInferno : MonoBehaviour
{
    #region Constants

    private const int MaxTargetResults = 64;
    private const float MinimumDirectionMagnitude = 0.001f;
    private const float MinimumDuration = 0.05f;

    #endregion

    #region Animator Hashes

    private static readonly int ChaosInfernoHash =
        Animator.StringToHash("Chaos Inferno");

    #endregion

    #region Inspector

    [Header("References")]
    [SerializeField] private TeamActionController actionController;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Transform blastOrigin;

    [Header("Chaos Inferno")]
    [SerializeField, Min(0.1f)] private float blastRadius = 20f;
    [SerializeField, Min(0f)] private float damage = 110f;
    [SerializeField, Min(0f)] private float knockbackForce = 16f;
    [SerializeField, Min(MinimumDuration)] private float activeDuration = 1f;
    [SerializeField, Min(1)] private int pulseCount = 2;
    [SerializeField, Min(0f)] private float pulseInterval = 0.2f;
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Chaos Control")]
    [SerializeField, Min(0f)] private float controlDuration = 5f;
    [SerializeField, Range(0f, 1f)] private float enemyTimeScale = 0f;
    [SerializeField] private bool destroyControlledEnemies;

    [Header("Effects")]
    [SerializeField] private GameObject activationEffect;
    [SerializeField] private GameObject pulseEffect;
    [SerializeField] private GameObject controlEffect;
    [SerializeField, Min(0f)] private float effectLifetime = 4f;

    [Header("Audio")]
    [SerializeField] private AudioClip activationSound;
    [SerializeField] private AudioClip pulseSound;

    [Header("Debug")]
    [SerializeField] private bool drawBlastRadius = true;
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private readonly Collider[] targetResults =
        new Collider[MaxTargetResults];

    private readonly HashSet<GameObject> affectedTargets =
        new();

    private Coroutine infernoRoutine;

    private bool isActive;
    private bool isInitialized;
    private bool isShuttingDown;

    #endregion

    #region Public API

    public bool IsActive =>
        isActive;

    public bool IsInitialized =>
        isInitialized;

    public bool TryActivate()
    {
        if (!CanActivate())
            return false;

        BeginChaosInferno();
        return true;
    }

    public void Cancel()
    {
        if (!isActive)
            return;

        FinishChaosInferno();
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
        if (!InitializeChaosInferno())
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
        blastRadius =
            Mathf.Max(
                0.1f,
                blastRadius);

        damage =
            Mathf.Max(
                0f,
                damage);

        knockbackForce =
            Mathf.Max(
                0f,
                knockbackForce);

        activeDuration =
            Mathf.Max(
                MinimumDuration,
                activeDuration);

        pulseCount =
            Mathf.Max(
                1,
                pulseCount);

        pulseInterval =
            Mathf.Max(
                0f,
                pulseInterval);

        controlDuration =
            Mathf.Max(
                0f,
                controlDuration);

        enemyTimeScale =
            Mathf.Clamp01(
                enemyTimeScale);

        effectLifetime =
            Mathf.Max(
                0f,
                effectLifetime);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawBlastRadius)
            return;

        Gizmos.DrawWireSphere(
            GetBlastPosition(),
            blastRadius);
    }

    #endregion

    #region Initialization

    public bool InitializeChaosInferno()
    {
        if (isInitialized)
            return true;

        CacheComponents();
        ResolveReferences();

        if (!ValidateConfiguration())
        {
            Debug.LogError(
                $"ChaosInferno failed to initialize on '{name}'.",
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

        audioSource ??=
            GetComponentInParent<AudioSource>();
    }

    private void ResolveReferences()
    {
        animator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        blastOrigin ??=
            transform;
    }

    #endregion

    #region Chaos Inferno State

    private bool CanActivate()
    {
        return
            isInitialized &&
            !isActive &&
            TeamSetup.Instance != null &&
            TeamSetup.Instance.PlayableTeam ==
                PlayableTeam.TeamDark &&
            actionController != null;
    }

    private void BeginChaosInferno()
    {
        isActive = true;
        affectedTargets.Clear();

        PlayAnimation();
        PlaySound(
            activationSound);

        SpawnEffect(
            activationEffect,
            GetBlastPosition());

        infernoRoutine =
            StartCoroutine(
                ChaosInfernoRoutine());

        LogStateChange(
            "Chaos Inferno started.");
    }

    private IEnumerator ChaosInfernoRoutine()
    {
        WaitForSeconds pulseWait =
            pulseInterval > 0f
                ? new WaitForSeconds(
                    pulseInterval)
                : null;

        for (int pulseIndex = 0;
             pulseIndex < pulseCount;
             pulseIndex++)
        {
            ApplyInfernoPulse();

            if (pulseIndex <
                    pulseCount - 1 &&
                pulseWait != null)
            {
                yield return pulseWait;
            }
        }

        float remainingDuration =
            Mathf.Max(
                0f,
                activeDuration -
                pulseInterval *
                Mathf.Max(
                    0,
                    pulseCount - 1));

        if (remainingDuration > 0f)
        {
            yield return
                new WaitForSeconds(
                    remainingDuration);
        }

        FinishChaosInferno();
    }

    private void FinishChaosInferno()
    {
        if (!isActive)
            return;

        isActive = false;

        if (infernoRoutine != null)
        {
            StopCoroutine(
                infernoRoutine);

            infernoRoutine = null;
        }

        affectedTargets.Clear();

        LogStateChange(
            "Chaos Inferno finished.");
    }

    private void ResetRuntimeState()
    {
        isActive = false;
        infernoRoutine = null;
        affectedTargets.Clear();
    }

    #endregion

    #region Chaos Control

    private void ApplyChaosControl(
        GameObject target)
    {
        if (target == null ||
            controlDuration <= 0f)
        {
            return;
        }

        target.SendMessage(
            "ApplyChaosControl",
            controlDuration,
            SendMessageOptions.DontRequireReceiver);

        target.SendMessage(
            "SetLocalTimeScale",
            enemyTimeScale,
            SendMessageOptions.DontRequireReceiver);

        SpawnEffect(
            controlEffect,
            target.transform.position);
    }

    #endregion

    #region Damage

    private void ApplyInfernoPulse()
    {
        SpawnEffect(
            pulseEffect,
            GetBlastPosition());

        PlaySound(
            pulseSound);

        int resultCount =
            Physics.OverlapSphereNonAlloc(
                GetBlastPosition(),
                blastRadius,
                targetResults,
                enemyLayers,
                QueryTriggerInteraction.Collide);

        for (int index = 0;
             index < resultCount;
             index++)
        {
            Collider enemy =
                targetResults[index];

            targetResults[index] = null;

            if (enemy == null)
                continue;

            GameObject target =
                enemy.attachedRigidbody != null
                    ? enemy.attachedRigidbody.gameObject
                    : enemy.gameObject;

            if (IsTeamCharacter(target) ||
                !affectedTargets.Add(target))
            {
                continue;
            }

            target.SendMessage(
                "TakeDamage",
                damage,
                SendMessageOptions.DontRequireReceiver);

            ApplyKnockback(
                target,
                enemy.attachedRigidbody);

            ApplyChaosControl(
                target);

            if (destroyControlledEnemies)
            {
                target.SendMessage(
                    "Break",
                    SendMessageOptions.DontRequireReceiver);
            }
        }
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
            GetBlastPosition();

        direction.y =
            Mathf.Max(
                direction.y,
                0.2f);

        if (direction.sqrMagnitude <=
            MinimumDirectionMagnitude)
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
                actionController.PowerCharacter);
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

    private void SpawnEffect(
        GameObject effectPrefab,
        Vector3 position)
    {
        if (effectPrefab == null)
            return;

        GameObject spawnedEffect =
            Instantiate(
                effectPrefab,
                position,
                transform.rotation);

        if (effectLifetime > 0f)
        {
            Destroy(
                spawnedEffect,
                effectLifetime);
        }
    }

    #endregion

    #region Animation

    private void PlayAnimation()
    {
        if (animator == null)
            return;

        animator.SetTrigger(
            ChaosInfernoHash);
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

    #region Utility

    private Vector3 GetBlastPosition()
    {
        return
            blastOrigin != null
                ? blastOrigin.position
                : transform.position;
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

        if (animator == null)
        {
            Debug.LogWarning(
                "ChaosInferno could not find an Animator.",
                this);
        }

        if (audioSource == null)
        {
            Debug.LogWarning(
                "ChaosInferno could not find an AudioSource.",
                this);
        }

        if (activationEffect == null)
        {
            Debug.LogWarning(
                "ChaosInferno has no activation effect.",
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
            $"ChaosInferno requires {displayName}.",
            this);

        return false;
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        if (isActive)
        {
            FinishChaosInferno();
        }
    }

    private void CleanupDestroyedState()
    {
        CleanupRuntimeState();

        isInitialized = false;

        actionController = null;
        animator = null;
        audioSource = null;
        blastOrigin = null;
        activationEffect = null;
        pulseEffect = null;
        controlEffect = null;

        affectedTargets.Clear();
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
