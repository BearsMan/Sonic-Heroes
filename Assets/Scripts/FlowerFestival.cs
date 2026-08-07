using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FlowerFestival : MonoBehaviour
{
    #region Constants

    private const int MaxTargetResults = 64;
    private const float MinimumDirectionMagnitude = 0.001f;
    private const float MinimumDuration = 0.05f;

    #endregion

    #region Animator Hashes

    private static readonly int FlowerFestivalHash =
        Animator.StringToHash("Flower Festival");

    #endregion

    #region Inspector

    [Header("References")]
    [SerializeField] private TeamActionController actionController;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Transform blastOrigin;

    [Header("Festival")]
    [SerializeField, Min(0.1f)] private float blastRadius = 16f;
    [SerializeField, Min(0f)] private float damage = 80f;
    [SerializeField, Min(0f)] private float knockbackForce = 12f;
    [SerializeField, Min(MinimumDuration)] private float activeDuration = 1f;
    [SerializeField, Min(1)] private int pulseCount = 2;
    [SerializeField, Min(0f)] private float pulseInterval = 0.25f;
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Team Support")]
    [SerializeField, Min(0f)] private float healingAmount = 50f;
    [SerializeField, Min(0f)] private float shieldDuration = 8f;
    [SerializeField, Min(0f)] private float invincibilityDuration = 3f;
    [SerializeField] private bool reviveDefeatedTeammates;

    [Header("Effects")]
    [SerializeField] private GameObject activationEffect;
    [SerializeField] private GameObject pulseEffect;
    [SerializeField] private GameObject teamSupportEffect;
    [SerializeField, Min(0f)] private float effectLifetime = 4f;

    [Header("Audio")]
    [SerializeField] private AudioClip activationSound;
    [SerializeField] private AudioClip pulseSound;
    [SerializeField] private AudioClip supportSound;

    [Header("Debug")]
    [SerializeField] private bool drawBlastRadius = true;
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private readonly Collider[] targetResults =
        new Collider[MaxTargetResults];

    private readonly HashSet<GameObject> affectedTargets =
        new();

    private Coroutine festivalRoutine;

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

        BeginFlowerFestival();
        return true;
    }

    public void Cancel()
    {
        if (!isActive)
            return;

        FinishFlowerFestival();
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
        if (!InitializeFlowerFestival())
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

        healingAmount =
            Mathf.Max(
                0f,
                healingAmount);

        shieldDuration =
            Mathf.Max(
                0f,
                shieldDuration);

        invincibilityDuration =
            Mathf.Max(
                0f,
                invincibilityDuration);

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

    public bool InitializeFlowerFestival()
    {
        if (isInitialized)
            return true;

        CacheComponents();
        ResolveReferences();

        if (!ValidateConfiguration())
        {
            Debug.LogError(
                $"FlowerFestival failed to initialize on '{name}'.",
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

    #region Flower Festival State

    private bool CanActivate()
    {
        return
            isInitialized &&
            !isActive &&
            TeamSetup.Instance != null &&
            TeamSetup.Instance.PlayableTeam ==
                PlayableTeam.TeamRose &&
            actionController != null;
    }

    private void BeginFlowerFestival()
    {
        isActive = true;
        affectedTargets.Clear();

        PlayAnimation();
        PlaySound(
            activationSound);

        SpawnEffect(
            activationEffect,
            GetBlastPosition());

        ApplyTeamSupport();

        festivalRoutine =
            StartCoroutine(
                FlowerFestivalRoutine());

        LogStateChange(
            "Flower Festival started.");
    }

    private IEnumerator FlowerFestivalRoutine()
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
            ApplyFestivalPulse();

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

        FinishFlowerFestival();
    }

    private void FinishFlowerFestival()
    {
        if (!isActive)
            return;

        isActive = false;

        if (festivalRoutine != null)
        {
            StopCoroutine(
                festivalRoutine);

            festivalRoutine = null;
        }

        affectedTargets.Clear();

        LogStateChange(
            "Flower Festival finished.");
    }

    private void ResetRuntimeState()
    {
        isActive = false;
        festivalRoutine = null;
        affectedTargets.Clear();
    }

    #endregion

    #region Team Support

    private void ApplyTeamSupport()
    {
        PlaySound(
            supportSound);

        ApplySupportToCharacter(
            actionController.SpeedCharacter);

        ApplySupportToCharacter(
            actionController.FlyCharacter);

        ApplySupportToCharacter(
            actionController.PowerCharacter);
    }

    private void ApplySupportToCharacter(
        Transform character)
    {
        if (character == null)
            return;

        GameObject target =
            character.gameObject;

        if (healingAmount > 0f)
        {
            target.SendMessage(
                "Heal",
                healingAmount,
                SendMessageOptions.DontRequireReceiver);
        }

        if (shieldDuration > 0f)
        {
            target.SendMessage(
                "ApplyShield",
                shieldDuration,
                SendMessageOptions.DontRequireReceiver);
        }

        if (invincibilityDuration > 0f)
        {
            target.SendMessage(
                "SetTemporaryInvincibility",
                invincibilityDuration,
                SendMessageOptions.DontRequireReceiver);
        }

        if (reviveDefeatedTeammates)
        {
            target.SendMessage(
                "Revive",
                SendMessageOptions.DontRequireReceiver);
        }

        SpawnEffect(
            teamSupportEffect,
            character.position);
    }

    #endregion

    #region Damage

    private void ApplyFestivalPulse()
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
            FlowerFestivalHash);
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
                "FlowerFestival could not find an Animator.",
                this);
        }

        if (audioSource == null)
        {
            Debug.LogWarning(
                "FlowerFestival could not find an AudioSource.",
                this);
        }

        if (activationEffect == null)
        {
            Debug.LogWarning(
                "FlowerFestival has no activation effect.",
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
            $"FlowerFestival requires {displayName}.",
            this);

        return false;
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        if (isActive)
        {
            FinishFlowerFestival();
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
        teamSupportEffect = null;

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
