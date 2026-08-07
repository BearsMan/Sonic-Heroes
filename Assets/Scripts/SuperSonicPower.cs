using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SuperSonicPower : MonoBehaviour
{
    #region Constants

    private const int MaxTargetResults = 64;
    private const float MinimumDirectionMagnitude = 0.001f;
    private const float MinimumDuration = 0.05f;

    #endregion

    #region Animator Hashes

    private static readonly int SuperSonicPowerHash =
        Animator.StringToHash("Super Sonic Power");

    #endregion

    #region Inspector

    [Header("References")]
    [SerializeField] private TeamActionController actionController;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Transform blastOrigin;

    [Header("Super Sonic Power")]
    [SerializeField, Min(0.1f)] private float blastRadius = 24f;
    [SerializeField, Min(0f)] private float damage = 250f;
    [SerializeField, Min(0f)] private float knockbackForce = 30f;
    [SerializeField, Min(MinimumDuration)] private float activeDuration = 1.25f;
    [SerializeField, Min(1)] private int pulseCount = 3;
    [SerializeField, Min(0f)] private float pulseInterval = 0.2f;
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Last Story")]
    [SerializeField] private bool requireMetalOverlord = true;
    [SerializeField] private bool damageMetalOverlord = true;
    [SerializeField, Min(1)] private int metalOverlordHits = 1;
    [SerializeField, Min(0f)] private float metalOverlordHitInterval = 0.15f;

    [Header("Team Support")]
    [SerializeField, Min(0f)] private float invincibilityDuration = 6f;
    [SerializeField] private bool protectWholeTeam = true;

    [Header("Effects")]
    [SerializeField] private GameObject activationEffect;
    [SerializeField] private GameObject pulseEffect;
    [SerializeField] private GameObject bossImpactEffect;
    [SerializeField, Min(0f)] private float effectLifetime = 5f;

    [Header("Audio")]
    [SerializeField] private AudioClip activationSound;
    [SerializeField] private AudioClip pulseSound;
    [SerializeField] private AudioClip bossImpactSound;

    [Header("Debug")]
    [SerializeField] private bool drawBlastRadius = true;
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private readonly Collider[] targetResults =
        new Collider[MaxTargetResults];

    private readonly HashSet<GameObject> affectedTargets =
        new();

    private Coroutine superPowerRoutine;
    private MetalOverlord metalOverlord;

    private bool isActive;
    private bool isInitialized;
    private bool isShuttingDown;

    #endregion

    #region Public API

    public bool IsActive =>
        isActive;

    public bool IsInitialized =>
        isInitialized;

    public bool HasMetalOverlordTarget =>
        metalOverlord != null;

    public bool TryActivate()
    {
        if (!CanActivate())
            return false;

        BeginSuperSonicPower();
        return true;
    }

    public void Cancel()
    {
        if (!isActive)
            return;

        FinishSuperSonicPower();
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
        if (!InitializeSuperSonicPower())
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

        metalOverlordHits =
            Mathf.Max(
                1,
                metalOverlordHits);

        metalOverlordHitInterval =
            Mathf.Max(
                0f,
                metalOverlordHitInterval);

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

    public bool InitializeSuperSonicPower()
    {
        if (isInitialized)
            return true;

        CacheComponents();
        ResolveReferences();

        if (!ValidateConfiguration())
        {
            Debug.LogError(
                $"SuperSonicPower failed to initialize on '{name}'.",
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

        metalOverlord ??=
            FindAnyObjectByType<MetalOverlord>();
    }

    #endregion

    #region Super Sonic Power State

    private bool CanActivate()
    {
        return
            isInitialized &&
            !isActive &&
            actionController != null &&
            (!requireMetalOverlord ||
             metalOverlord != null);
    }

    private void BeginSuperSonicPower()
    {
        isActive = true;
        affectedTargets.Clear();

        ResolveReferences();

        PlayAnimation();
        PlaySound(
            activationSound);

        SpawnEffect(
            activationEffect,
            GetBlastPosition());

        if (protectWholeTeam)
        {
            ApplyTeamInvincibility();
        }

        superPowerRoutine =
            StartCoroutine(
                SuperSonicPowerRoutine());

        LogStateChange(
            "Super Sonic Power started.");
    }

    private IEnumerator SuperSonicPowerRoutine()
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
            ApplyPowerPulse();

            if (pulseIndex <
                    pulseCount - 1 &&
                pulseWait != null)
            {
                yield return pulseWait;
            }
        }

        if (damageMetalOverlord &&
            metalOverlord != null)
        {
            yield return
                DamageMetalOverlordRoutine();
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

        FinishSuperSonicPower();
    }

    private void FinishSuperSonicPower()
    {
        if (!isActive)
            return;

        isActive = false;

        if (superPowerRoutine != null)
        {
            StopCoroutine(
                superPowerRoutine);

            superPowerRoutine = null;
        }

        affectedTargets.Clear();

        LogStateChange(
            "Super Sonic Power finished.");
    }

    private void ResetRuntimeState()
    {
        isActive = false;
        superPowerRoutine = null;
        affectedTargets.Clear();
    }

    #endregion

    #region Damage

    private void ApplyPowerPulse()
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

            target.SendMessage(
                "Break",
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

    #region Metal Overlord

    private IEnumerator DamageMetalOverlordRoutine()
    {
        WaitForSeconds hitWait =
            metalOverlordHitInterval > 0f
                ? new WaitForSeconds(
                    metalOverlordHitInterval)
                : null;

        for (int hitIndex = 0;
             hitIndex < metalOverlordHits;
             hitIndex++)
        {
            if (metalOverlord == null)
                yield break;

            metalOverlord.OnTeamBlastHit();

            SpawnEffect(
                bossImpactEffect,
                metalOverlord.transform.position);

            PlaySound(
                bossImpactSound);

            if (hitIndex <
                    metalOverlordHits - 1 &&
                hitWait != null)
            {
                yield return hitWait;
            }
        }
    }

    #endregion

    #region Team Support

    private void ApplyTeamInvincibility()
    {
        ApplyInvincibilityToCharacter(
            actionController.SpeedCharacter);

        ApplyInvincibilityToCharacter(
            actionController.FlyCharacter);

        ApplyInvincibilityToCharacter(
            actionController.PowerCharacter);
    }

    private void ApplyInvincibilityToCharacter(
        Transform character)
    {
        if (character == null ||
            invincibilityDuration <= 0f)
        {
            return;
        }

        character.gameObject.SendMessage(
            "SetTemporaryInvincibility",
            invincibilityDuration,
            SendMessageOptions.DontRequireReceiver);
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
            SuperSonicPowerHash);
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

        if (requireMetalOverlord &&
            metalOverlord == null)
        {
            Debug.LogWarning(
                "SuperSonicPower requires MetalOverlord, but none was found.",
                this);
        }

        if (animator == null)
        {
            Debug.LogWarning(
                "SuperSonicPower could not find an Animator.",
                this);
        }

        if (audioSource == null)
        {
            Debug.LogWarning(
                "SuperSonicPower could not find an AudioSource.",
                this);
        }

        if (activationEffect == null)
        {
            Debug.LogWarning(
                "SuperSonicPower has no activation effect.",
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
            $"SuperSonicPower requires {displayName}.",
            this);

        return false;
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        if (isActive)
        {
            FinishSuperSonicPower();
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
        metalOverlord = null;
        activationEffect = null;
        pulseEffect = null;
        bossImpactEffect = null;

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
