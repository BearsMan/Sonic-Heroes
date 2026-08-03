using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SonicOverdrive : MonoBehaviour
{
    #region Constants

    private const int MaxTargetResults = 64;
    private const float MinimumDirectionMagnitude = 0.001f;
    private const float MinimumDuration = 0.05f;

    #endregion

    #region Animator Hashes

    private static readonly int SonicOverdriveHash =
        Animator.StringToHash("Sonic Overdrive");

    #endregion

    #region Inspector

    [Header("References")]
    [SerializeField] private TeamActionController actionController;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Transform blastOrigin;

    [Header("Overdrive")]
    [SerializeField, Min(0.1f)] private float blastRadius = 18f;
    [SerializeField, Min(0f)] private float damage = 125f;
    [SerializeField, Min(0f)] private float knockbackForce = 25f;
    [SerializeField, Min(MinimumDuration)] private float activeDuration = 0.75f;
    [SerializeField, Min(1)] private int pulseCount = 3;
    [SerializeField, Min(0f)] private float pulseInterval = 0.12f;
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Team Rush")]
    [SerializeField, Min(0f)] private float forwardRushDistance = 8f;
    [SerializeField, Min(0f)] private float rushDuration = 0.2f;
    [SerializeField] private bool moveTeamForward = true;

    [Header("Effects")]
    [SerializeField] private GameObject activationEffect;
    [SerializeField] private GameObject pulseEffect;
    [SerializeField, Min(0f)] private float effectLifetime = 3f;

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

    private readonly HashSet<GameObject> damagedTargets =
        new();

    private Coroutine overdriveRoutine;

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

        BeginOverdrive();
        return true;
    }

    public void Cancel()
    {
        if (!isActive)
            return;

        FinishOverdrive();
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
        if (!InitializeSonicOverdrive())
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
        blastRadius = Mathf.Max(0.1f, blastRadius);
        damage = Mathf.Max(0f, damage);
        knockbackForce = Mathf.Max(0f, knockbackForce);
        activeDuration = Mathf.Max(MinimumDuration, activeDuration);
        pulseCount = Mathf.Max(1, pulseCount);
        pulseInterval = Mathf.Max(0f, pulseInterval);
        forwardRushDistance = Mathf.Max(0f, forwardRushDistance);
        rushDuration = Mathf.Max(0f, rushDuration);
        effectLifetime = Mathf.Max(0f, effectLifetime);
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

    public bool InitializeSonicOverdrive()
    {
        if (isInitialized)
            return true;

        CacheComponents();
        ResolveReferences();

        if (!ValidateConfiguration())
        {
            Debug.LogError(
                $"SonicOverdrive failed to initialize on '{name}'.",
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

    #region Sonic Overdrive State

    private bool CanActivate()
    {
        return
            isInitialized &&
            !isActive &&
            TeamSetup.Instance != null &&
            TeamSetup.Instance.PlayableTeam ==
                PlayableTeam.TeamSonic &&
            actionController != null;
    }

    private void BeginOverdrive()
    {
        isActive = true;
        damagedTargets.Clear();

        PlayAnimation();
        PlaySound(activationSound);

        SpawnEffect(
            activationEffect,
            GetBlastPosition());

        overdriveRoutine =
            StartCoroutine(
                SonicOverdriveRoutine());

        LogStateChange(
            "Sonic Overdrive started.");
    }

    private IEnumerator SonicOverdriveRoutine()
    {
        if (moveTeamForward &&
            forwardRushDistance > 0f &&
            rushDuration > 0f)
        {
            yield return
                RushTeamForward();
        }

        WaitForSeconds pulseWait =
            pulseInterval > 0f
                ? new WaitForSeconds(pulseInterval)
                : null;

        for (int pulseIndex = 0;
             pulseIndex < pulseCount;
             pulseIndex++)
        {
            ApplyOverdrivePulse();

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

        FinishOverdrive();
    }

    private void FinishOverdrive()
    {
        if (!isActive)
            return;

        isActive = false;

        if (overdriveRoutine != null)
        {
            StopCoroutine(
                overdriveRoutine);

            overdriveRoutine = null;
        }

        damagedTargets.Clear();

        LogStateChange(
            "Sonic Overdrive finished.");
    }

    private void ResetRuntimeState()
    {
        isActive = false;
        overdriveRoutine = null;
        damagedTargets.Clear();
    }

    #endregion

    #region Team Rush

    private IEnumerator RushTeamForward()
    {
        Transform[] teamCharacters =
        {
            actionController.SpeedCharacter,
            actionController.FlyCharacter,
            actionController.PowerCharacter
        };

        Vector3 direction =
            transform.forward;

        if (direction.sqrMagnitude <=
            MinimumDirectionMagnitude)
        {
            direction =
                Vector3.forward;
        }

        direction.Normalize();

        Vector3[] startingPositions =
            new Vector3[teamCharacters.Length];

        for (int index = 0;
             index < teamCharacters.Length;
             index++)
        {
            if (teamCharacters[index] != null)
            {
                startingPositions[index] =
                    teamCharacters[index].position;
            }
        }

        float elapsed = 0f;

        while (elapsed < rushDuration)
        {
            elapsed +=
                Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsed /
                    rushDuration);

            for (int index = 0;
                 index < teamCharacters.Length;
                 index++)
            {
                Transform character =
                    teamCharacters[index];

                if (character == null)
                    continue;

                Vector3 destination =
                    startingPositions[index] +
                    direction *
                    forwardRushDistance;

                character.position =
                    Vector3.Lerp(
                        startingPositions[index],
                        destination,
                        progress);
            }

            yield return null;
        }
    }

    #endregion

    #region Damage

    private void ApplyOverdrivePulse()
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
                !damagedTargets.Add(target))
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
            SonicOverdriveHash);
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
                "SonicOverdrive could not find an Animator.",
                this);
        }

        if (audioSource == null)
        {
            Debug.LogWarning(
                "SonicOverdrive could not find an AudioSource.",
                this);
        }

        if (activationEffect == null)
        {
            Debug.LogWarning(
                "SonicOverdrive has no activation effect.",
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
            $"SonicOverdrive requires {displayName}.",
            this);

        return false;
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        if (isActive)
        {
            FinishOverdrive();
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
