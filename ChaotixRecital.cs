using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChaotixRecital : MonoBehaviour
{
    #region Constants

    private const int MaxTargetResults = 64;
    private const float MinimumDirectionMagnitude = 0.001f;
    private const float MinimumDuration = 0.05f;

    #endregion

    #region Animator Hashes

    private static readonly int ChaotixRecitalHash =
        Animator.StringToHash("Chaotix Recital");

    #endregion

    #region Inspector

    [Header("References")]
    [SerializeField] private TeamActionController actionController;
    [SerializeField] private TeamBlast teamBlast;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Transform blastOrigin;

    [Header("Recital")]
    [SerializeField, Min(0.1f)] private float blastRadius = 18f;
    [SerializeField, Min(0f)] private float damage = 100f;
    [SerializeField, Min(0f)] private float knockbackForce = 18f;
    [SerializeField, Min(MinimumDuration)] private float activeDuration = 0.9f;
    [SerializeField, Min(1)] private int pulseCount = 3;
    [SerializeField, Min(0f)] private float pulseInterval = 0.15f;
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Rewards")]
    [SerializeField, Min(0)] private int ringRewardPerEnemy = 10;
    [SerializeField, Min(0f)] private float gaugeRewardPerEnemy = 15f;
    [SerializeField, Min(0)] private int maximumRewardedEnemies = 12;

    [Header("Effects")]
    [SerializeField] private GameObject activationEffect;
    [SerializeField] private GameObject pulseEffect;
    [SerializeField] private GameObject rewardEffect;
    [SerializeField, Min(0f)] private float effectLifetime = 4f;

    [Header("Audio")]
    [SerializeField] private AudioClip activationSound;
    [SerializeField] private AudioClip pulseSound;
    [SerializeField] private AudioClip rewardSound;

    [Header("Debug")]
    [SerializeField] private bool drawBlastRadius = true;
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private readonly Collider[] targetResults =
        new Collider[MaxTargetResults];

    private readonly HashSet<GameObject> affectedTargets =
        new();

    private Coroutine recitalRoutine;

    private bool isActive;
    private bool isInitialized;
    private bool isShuttingDown;

    private int defeatedEnemyCount;

    #endregion

    #region Public API

    public bool IsActive =>
        isActive;

    public bool IsInitialized =>
        isInitialized;

    public int DefeatedEnemyCount =>
        defeatedEnemyCount;

    public bool TryActivate()
    {
        if (!CanActivate())
            return false;

        BeginChaotixRecital();
        return true;
    }

    public void Cancel()
    {
        if (!isActive)
            return;

        FinishChaotixRecital();
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
        if (!InitializeChaotixRecital())
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

        ringRewardPerEnemy =
            Mathf.Max(
                0,
                ringRewardPerEnemy);

        gaugeRewardPerEnemy =
            Mathf.Max(
                0f,
                gaugeRewardPerEnemy);

        maximumRewardedEnemies =
            Mathf.Max(
                0,
                maximumRewardedEnemies);

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

    public bool InitializeChaotixRecital()
    {
        if (isInitialized)
            return true;

        CacheComponents();
        ResolveReferences();

        if (!ValidateConfiguration())
        {
            Debug.LogError(
                $"ChaotixRecital failed to initialize on '{name}'.",
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

        teamBlast ??=
            GetComponentInParent<TeamBlast>();

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

    #region Chaotix Recital State

    private bool CanActivate()
    {
        return
            isInitialized &&
            !isActive &&
            TeamSetup.Instance != null &&
            TeamSetup.Instance.PlayableTeam ==
                PlayableTeam.TeamChaotix &&
            actionController != null;
    }

    private void BeginChaotixRecital()
    {
        isActive = true;
        defeatedEnemyCount = 0;
        affectedTargets.Clear();

        PlayAnimation();
        PlaySound(
            activationSound);

        SpawnEffect(
            activationEffect,
            GetBlastPosition());

        recitalRoutine =
            StartCoroutine(
                ChaotixRecitalRoutine());

        LogStateChange(
            "Chaotix Recital started.");
    }

    private IEnumerator ChaotixRecitalRoutine()
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
            ApplyRecitalPulse();

            if (pulseIndex <
                    pulseCount - 1 &&
                pulseWait != null)
            {
                yield return pulseWait;
            }
        }

        ApplyRewards();

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

        FinishChaotixRecital();
    }

    private void FinishChaotixRecital()
    {
        if (!isActive)
            return;

        isActive = false;

        if (recitalRoutine != null)
        {
            StopCoroutine(
                recitalRoutine);

            recitalRoutine = null;
        }

        affectedTargets.Clear();

        LogStateChange(
            $"Chaotix Recital finished after defeating {defeatedEnemyCount} enemies.");
    }

    private void ResetRuntimeState()
    {
        isActive = false;
        recitalRoutine = null;
        defeatedEnemyCount = 0;
        affectedTargets.Clear();
    }

    #endregion

    #region Damage

    private void ApplyRecitalPulse()
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

            defeatedEnemyCount++;
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

    #region Rewards

    private void ApplyRewards()
    {
        int rewardedEnemies =
            maximumRewardedEnemies > 0
                ? Mathf.Min(
                    defeatedEnemyCount,
                    maximumRewardedEnemies)
                : defeatedEnemyCount;

        if (rewardedEnemies <= 0)
            return;

        int ringReward =
            ringRewardPerEnemy *
            rewardedEnemies;

        float gaugeReward =
            gaugeRewardPerEnemy *
            rewardedEnemies;

        if (ringReward > 0)
        {
            GameObject gameInstanceObject =
                GameObject.Find("GameInstance");

            if (gameInstanceObject != null)
            {
                gameInstanceObject.SendMessage(
                    "AddRings",
                    ringReward,
                    SendMessageOptions.DontRequireReceiver);
            }
        }

        if (gaugeReward > 0f &&
            teamBlast != null)
        {
            teamBlast.AddGauge(
                gaugeReward);
        }

        SpawnEffect(
            rewardEffect,
            GetBlastPosition());

        PlaySound(
            rewardSound);

        LogStateChange(
            $"Chaotix Recital awarded {ringReward} rings and {gaugeReward} Team Blast gauge.");
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
            ChaotixRecitalHash);
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

        if (teamBlast == null)
        {
            Debug.LogWarning(
                "ChaotixRecital could not find TeamBlast. Gauge rewards will be skipped.",
                this);
        }

        if (animator == null)
        {
            Debug.LogWarning(
                "ChaotixRecital could not find an Animator.",
                this);
        }

        if (audioSource == null)
        {
            Debug.LogWarning(
                "ChaotixRecital could not find an AudioSource.",
                this);
        }

        if (activationEffect == null)
        {
            Debug.LogWarning(
                "ChaotixRecital has no activation effect.",
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
            $"ChaotixRecital requires {displayName}.",
            this);

        return false;
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        if (isActive)
        {
            FinishChaotixRecital();
        }
    }

    private void CleanupDestroyedState()
    {
        CleanupRuntimeState();

        isInitialized = false;

        actionController = null;
        teamBlast = null;
        animator = null;
        audioSource = null;
        blastOrigin = null;
        activationEffect = null;
        pulseEffect = null;
        rewardEffect = null;

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
