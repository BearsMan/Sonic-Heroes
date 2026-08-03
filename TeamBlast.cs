using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class TeamBlast : MonoBehaviour
{
    #region Constants

    private const float MinimumGaugeValue = 0f;
    private const float MinimumDuration = 0.05f;

    #endregion

    #region Animator Hashes

    private static readonly int TeamBlastHash =
        Animator.StringToHash("Team Blast");

    private static readonly int SonicOverdriveHash =
        Animator.StringToHash("Sonic Overdrive");

    private static readonly int ChaosInfernoHash =
        Animator.StringToHash("Chaos Inferno");

    private static readonly int FlowerFestivalHash =
        Animator.StringToHash("Flower Festival");

    private static readonly int ChaotixRecitalHash =
        Animator.StringToHash("Chaotix Recital");

    private static readonly int SuperSonicPowerHash =
        Animator.StringToHash("Super Sonic Power");

    #endregion

    #region Enums

    public enum TeamBlastType
    {
        SonicOverdrive,
        ChaosInferno,
        FlowerFestival,
        ChaotixRecital,
        SuperSonicPower
    }

    #endregion

    #region Inspector

    [Header("References")]
    [SerializeField] private TeamActionController actionController;
    [SerializeField] private CharacterSwitch characterSwitch;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Transform blastOrigin;
    [SerializeField] private ChaosControl chaosControl;

    [Header("Team Blast")]
    [SerializeField]
    private TeamBlastType currentTeamBlast =
        TeamBlastType.SonicOverdrive;

    [SerializeField] private bool deriveBlastFromPlayableTeam = true;
    [SerializeField] private bool allowSuperSonicPower;
    [SerializeField] private KeyCode teamBlastKey = KeyCode.Z;
    [SerializeField] private bool readPlayerInput = true;

    [Header("Gauge")]
    [SerializeField, Min(1f)] private float maxGauge = 100f;
    [SerializeField, Min(0f)] private float startingGauge;
    [SerializeField] private bool consumeGaugeOnUse = true;

    [Header("Shared Blast")]
    [SerializeField, Min(0.1f)] private float blastRadius = 15f;
    [SerializeField, Min(0f)] private float baseDamage = 100f;
    [SerializeField, Min(0f)] private float knockbackForce = 20f;
    [SerializeField, Min(MinimumDuration)] private float actionDuration = 0.75f;
    [SerializeField, Min(0f)] private float invincibilityDuration = 5f;
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Sonic Overdrive")]
    [SerializeField, Min(0f)] private float sonicDamageMultiplier = 1.25f;
    [SerializeField, Min(0f)] private float sonicKnockbackMultiplier = 1.25f;

    [Header("Chaos Inferno")]
    [SerializeField, Min(0f)] private float chaosDamageMultiplier = 1f;
    [SerializeField, Min(0f)] private float chaosControlDuration = 5f;

    [Header("Flower Festival")]
    [SerializeField, Min(0f)] private float flowerDamageMultiplier = 0.75f;
    [SerializeField, Min(0f)] private float flowerHealingAmount = 50f;
    [SerializeField, Min(0f)] private float flowerShieldDuration = 8f;

    [Header("Chaotix Recital")]
    [SerializeField, Min(0f)] private float chaotixDamageMultiplier = 1f;
    [SerializeField, Min(0f)] private float chaotixGaugeReward = 20f;
    [SerializeField, Min(0)] private int chaotixRingReward = 10;

    [Header("Super Sonic Power")]
    [SerializeField, Min(0f)] private float superSonicDamageMultiplier = 2f;
    [SerializeField] private bool damageMetalOverlord = true;

    [Header("Effects")]
    [SerializeField] private GameObject sonicOverdriveEffect;
    [SerializeField] private GameObject chaosInfernoEffect;
    [SerializeField] private GameObject flowerFestivalEffect;
    [SerializeField] private GameObject chaotixRecitalEffect;
    [SerializeField] private GameObject superSonicPowerEffect;
    [SerializeField, Min(0f)] private float effectLifetime = 4f;

    [Header("Audio")]
    [SerializeField] private AudioClip sonicOverdriveSound;
    [SerializeField] private AudioClip chaosInfernoSound;
    [SerializeField] private AudioClip flowerFestivalSound;
    [SerializeField] private AudioClip chaotixRecitalSound;
    [SerializeField] private AudioClip superSonicPowerSound;

    [Header("Debug")]
    [SerializeField] private bool drawBlastRadius = true;
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private readonly HashSet<GameObject> affectedTargets =
        new();

    private Coroutine blastRoutine;
    private Coroutine invincibilityRoutine;

    private bool isPerformingTeamBlast;
    private bool isInvincible;
    private bool isInitialized;
    private bool isShuttingDown;

    #endregion

    #region Public API

    public TeamBlastType CurrentTeamBlast =>
        currentTeamBlast;

    public bool IsPerformingTeamBlast =>
        isPerformingTeamBlast;

    public bool IsInvincible =>
        isInvincible;

    public bool IsInitialized =>
        isInitialized;

    public float CurrentGauge =>
        Mathf.Clamp(
            GameInstance.teamBlastMeter,
            MinimumGaugeValue,
            maxGauge);

    public float MaxGauge =>
        maxGauge;

    public float GaugePercent =>
        maxGauge > 0f
            ? CurrentGauge / maxGauge
            : 0f;

    public bool BlastReady =>
        CurrentGauge >= maxGauge;

    public bool TryActivateTeamBlast()
    {
        if (!CanActivateTeamBlast())
            return false;

        bool accepted =
            actionController.TryBeginAction(
                TeamActionController.TeamAction.TeamBlast,
                actionController.CurrentFormation,
                mustBeGrounded: false,
                mustBeAirborne: false,
                surrenderMovementControl: true);

        if (!accepted)
            return false;

        BeginTeamBlast();
        return true;
    }

    public void CancelTeamBlast()
    {
        if (!isPerformingTeamBlast)
            return;

        FinishTeamBlast();
    }

    public void AddGauge(
        float amount)
    {
        if (!isInitialized ||
            amount <= 0f)
        {
            return;
        }

        GameInstance.teamBlastMeter =
            Mathf.Clamp(
                GameInstance.teamBlastMeter + amount,
                MinimumGaugeValue,
                maxGauge);
    }

    public void ResetGauge()
    {
        GameInstance.teamBlastMeter =
            MinimumGaugeValue;
    }

    public void SetTeamBlastType(
        TeamBlastType teamBlastType)
    {
        if (isPerformingTeamBlast)
            return;

        currentTeamBlast =
            teamBlastType;
    }

    public void SetInputEnabled(
        bool enabled)
    {
        readPlayerInput =
            enabled;
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
        if (!InitializeTeamBlast())
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

        if (isInitialized &&
            deriveBlastFromPlayableTeam)
        {
            ResolveTeamBlastFromPlayableTeam();
        }
    }

    private void Update()
    {
        if (!isInitialized ||
            !readPlayerInput ||
            isPerformingTeamBlast)
        {
            return;
        }

        if (deriveBlastFromPlayableTeam)
        {
            ResolveTeamBlastFromPlayableTeam();
        }

        if (BlastReady &&
            Input.GetKeyDown(teamBlastKey))
        {
            TryActivateTeamBlast();
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
        maxGauge =
            Mathf.Max(
                1f,
                maxGauge);

        startingGauge =
            Mathf.Clamp(
                startingGauge,
                MinimumGaugeValue,
                maxGauge);

        blastRadius =
            Mathf.Max(
                0.1f,
                blastRadius);

        baseDamage =
            Mathf.Max(
                0f,
                baseDamage);

        knockbackForce =
            Mathf.Max(
                0f,
                knockbackForce);

        actionDuration =
            Mathf.Max(
                MinimumDuration,
                actionDuration);

        invincibilityDuration =
            Mathf.Max(
                0f,
                invincibilityDuration);

        chaosControlDuration =
            Mathf.Max(
                0f,
                chaosControlDuration);

        flowerHealingAmount =
            Mathf.Max(
                0f,
                flowerHealingAmount);

        flowerShieldDuration =
            Mathf.Max(
                0f,
                flowerShieldDuration);

        chaotixGaugeReward =
            Mathf.Max(
                0f,
                chaotixGaugeReward);

        chaotixRingReward =
            Mathf.Max(
                0,
                chaotixRingReward);

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

    public bool InitializeTeamBlast()
    {
        if (isInitialized)
            return true;

        CacheComponents();
        ResolveReferences();

        if (!ValidateConfiguration())
        {
            Debug.LogError(
                $"TeamBlast failed to initialize on '{name}'.",
                this);

            isInitialized = false;
            return false;
        }

        GameInstance.maxTeamBlastMeter =
            maxGauge;

        GameInstance.teamBlastMeter =
            Mathf.Clamp(
                GameInstance.teamBlastMeter > 0f
                    ? GameInstance.teamBlastMeter
                    : startingGauge,
                MinimumGaugeValue,
                maxGauge);

        if (deriveBlastFromPlayableTeam)
        {
            ResolveTeamBlastFromPlayableTeam();
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

        audioSource ??=
            GetComponent<AudioSource>();

        audioSource ??=
            GetComponentInParent<AudioSource>();

        chaosControl ??=
            GetComponentInParent<ChaosControl>();
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

    #region Activation

    private bool CanActivateTeamBlast()
    {
        return
            isInitialized &&
            !isPerformingTeamBlast &&
            BlastReady &&
            actionController != null &&
            IsTeamBlastAllowed();
    }

    private bool IsTeamBlastAllowed()
    {
        if (currentTeamBlast !=
            TeamBlastType.SuperSonicPower)
        {
            return true;
        }

        return allowSuperSonicPower;
    }

    private void BeginTeamBlast()
    {
        isPerformingTeamBlast = true;
        affectedTargets.Clear();

        if (consumeGaugeOnUse)
        {
            ResetGauge();
        }

        ActivateInvincibility();

        PlayTeamBlastAnimation();
        PlayTeamBlastSound();
        SpawnTeamBlastEffect();

        ApplyTeamBlastEffect();

        blastRoutine =
            StartCoroutine(
                TeamBlastRoutine());

        LogStateChange(
            $"{GetTeamBlastName()} activated.");
    }

    private IEnumerator TeamBlastRoutine()
    {
        yield return
            new WaitForSeconds(
                actionDuration);

        FinishTeamBlast();
    }

    private void FinishTeamBlast()
    {
        if (!isPerformingTeamBlast)
            return;

        isPerformingTeamBlast = false;

        if (blastRoutine != null)
        {
            StopCoroutine(
                blastRoutine);

            blastRoutine = null;
        }

        chaosControl?.CancelChaosControl();

        if (actionController != null &&
            actionController.CurrentAction ==
                TeamActionController.TeamAction.TeamBlast)
        {
            actionController.EndAction(
                restoreMovementControl: true);
        }

        affectedTargets.Clear();

        LogStateChange(
            $"{GetTeamBlastName()} finished.");
    }

    private void ResetRuntimeState()
    {
        isPerformingTeamBlast = false;
        isInvincible = false;
        blastRoutine = null;
        invincibilityRoutine = null;
        affectedTargets.Clear();
    }

    #endregion

    #region Team Blast Routing

    private void ApplyTeamBlastEffect()
    {
        switch (currentTeamBlast)
        {
            case TeamBlastType.SonicOverdrive:
                ApplySonicOverdrive();
                break;

            case TeamBlastType.ChaosInferno:
                ApplyChaosInferno();
                break;

            case TeamBlastType.FlowerFestival:
                ApplyFlowerFestival();
                break;

            case TeamBlastType.ChaotixRecital:
                ApplyChaotixRecital();
                break;

            case TeamBlastType.SuperSonicPower:
                ApplySuperSonicPower();
                break;
        }
    }

    private void ResolveTeamBlastFromPlayableTeam()
    {
        if (TeamSetup.Instance == null)
            return;

        currentTeamBlast =
            TeamSetup.Instance.PlayableTeam switch
            {
                PlayableTeam.TeamSonic =>
                    TeamBlastType.SonicOverdrive,

                PlayableTeam.TeamDark =>
                    TeamBlastType.ChaosInferno,

                PlayableTeam.TeamRose =>
                    TeamBlastType.FlowerFestival,

                PlayableTeam.TeamChaotix =>
                    TeamBlastType.ChaotixRecital,

                _ =>
                    currentTeamBlast
            };
    }

    #endregion

    #region Sonic Overdrive

    private void ApplySonicOverdrive()
    {
        DamageNearbyEnemies(
            baseDamage *
            sonicDamageMultiplier,
            knockbackForce *
            sonicKnockbackMultiplier,
            destroyImmediately: true);
    }

    #endregion

    #region Chaos Inferno

    private void ApplyChaosInferno()
    {
        DamageNearbyEnemies(
            baseDamage *
            chaosDamageMultiplier,
            knockbackForce,
            destroyImmediately: false);

        if (chaosControl != null)
        {
            chaosControl.TryStartChaosControl();
        }

        BroadcastToNearbyEnemies(
            "ApplyChaosControl",
            chaosControlDuration);
    }

    #endregion

    #region Flower Festival

    private void ApplyFlowerFestival()
    {
        DamageNearbyEnemies(
            baseDamage *
            flowerDamageMultiplier,
            knockbackForce,
            destroyImmediately: false);

        BroadcastToTeam(
            "Heal",
            flowerHealingAmount);

        BroadcastToTeam(
            "ApplyShield",
            flowerShieldDuration);
    }

    #endregion

    #region Chaotix Recital

    private void ApplyChaotixRecital()
    {
        int defeatedCount =
            DamageNearbyEnemies(
                baseDamage *
                chaotixDamageMultiplier,
                knockbackForce,
                destroyImmediately: true);

        AddGauge(
            chaotixGaugeReward *
            defeatedCount);

        GameObject gameInstanceObject =
            GameObject.Find("GameInstance");

        if (gameInstanceObject != null)
        {
            gameInstanceObject.SendMessage(
                "AddRings",
                chaotixRingReward *
                defeatedCount,
                SendMessageOptions.DontRequireReceiver);
        }
    }

    #endregion

    #region Super Sonic Power

    private void ApplySuperSonicPower()
    {
        DamageNearbyEnemies(
            baseDamage *
            superSonicDamageMultiplier,
            knockbackForce,
            destroyImmediately: true);

        if (!damageMetalOverlord)
            return;

        MetalOverlord boss =
            FindAnyObjectByType<MetalOverlord>();

        boss?.OnTeamBlastHit();
    }

    #endregion

    #region Damage

    private int DamageNearbyEnemies(
        float damage,
        float knockback,
        bool destroyImmediately)
    {
        Collider[] enemies =
            Physics.OverlapSphere(
                GetBlastPosition(),
                blastRadius,
                enemyLayers,
                QueryTriggerInteraction.Collide);

        int affectedCount = 0;

        foreach (Collider enemy in enemies)
        {
            if (enemy == null)
                continue;

            GameObject target =
                enemy.attachedRigidbody != null
                    ? enemy.attachedRigidbody.gameObject
                    : enemy.gameObject;

            if (!affectedTargets.Add(target) ||
                IsTeamCharacter(target))
            {
                continue;
            }

            affectedCount++;

            target.SendMessage(
                "TakeDamage",
                damage,
                SendMessageOptions.DontRequireReceiver);

            ApplyKnockback(
                target,
                enemy.attachedRigidbody,
                knockback);

            if (destroyImmediately)
            {
                target.SendMessage(
                    "Break",
                    SendMessageOptions.DontRequireReceiver);
            }
        }

        return affectedCount;
    }

    private void ApplyKnockback(
        GameObject target,
        Rigidbody targetRigidbody,
        float force)
    {
        if (target == null ||
            targetRigidbody == null ||
            force <= 0f)
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
            Mathf.Epsilon)
        {
            direction =
                transform.forward;
        }

        targetRigidbody.AddForce(
            direction.normalized *
            force,
            ForceMode.VelocityChange);
    }

    private void BroadcastToNearbyEnemies(
        string methodName,
        float value)
    {
        Collider[] enemies =
            Physics.OverlapSphere(
                GetBlastPosition(),
                blastRadius,
                enemyLayers,
                QueryTriggerInteraction.Collide);

        foreach (Collider enemy in enemies)
        {
            if (enemy == null)
                continue;

            GameObject target =
                enemy.attachedRigidbody != null
                    ? enemy.attachedRigidbody.gameObject
                    : enemy.gameObject;

            if (IsTeamCharacter(target))
                continue;

            target.SendMessage(
                methodName,
                value,
                SendMessageOptions.DontRequireReceiver);
        }
    }

    private void BroadcastToTeam(
        string methodName,
        float value)
    {
        if (actionController == null)
            return;

        SendToCharacter(
            actionController.SpeedCharacter,
            methodName,
            value);

        SendToCharacter(
            actionController.FlyCharacter,
            methodName,
            value);

        SendToCharacter(
            actionController.PowerCharacter,
            methodName,
            value);
    }

    private static void SendToCharacter(
        Transform character,
        string methodName,
        float value)
    {
        if (character == null)
            return;

        character.gameObject.SendMessage(
            methodName,
            value,
            SendMessageOptions.DontRequireReceiver);
    }

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

    #region Invincibility

    private void ActivateInvincibility()
    {
        if (invincibilityDuration <= 0f)
            return;

        if (invincibilityRoutine != null)
        {
            StopCoroutine(
                invincibilityRoutine);
        }

        invincibilityRoutine =
            StartCoroutine(
                TeamBlastInvincibilityRoutine());
    }

    private IEnumerator TeamBlastInvincibilityRoutine()
    {
        isInvincible = true;

        BroadcastToTeam(
            "SetInvincible",
            1f);

        yield return
            new WaitForSeconds(
                invincibilityDuration);

        isInvincible = false;

        BroadcastToTeam(
            "SetInvincible",
            0f);

        invincibilityRoutine = null;
    }

    #endregion

    #region Effects

    private void SpawnTeamBlastEffect()
    {
        GameObject effectPrefab =
            currentTeamBlast switch
            {
                TeamBlastType.SonicOverdrive =>
                    sonicOverdriveEffect,

                TeamBlastType.ChaosInferno =>
                    chaosInfernoEffect,

                TeamBlastType.FlowerFestival =>
                    flowerFestivalEffect,

                TeamBlastType.ChaotixRecital =>
                    chaotixRecitalEffect,

                TeamBlastType.SuperSonicPower =>
                    superSonicPowerEffect,

                _ =>
                    null
            };

        if (effectPrefab == null)
            return;

        GameObject spawnedEffect =
            Instantiate(
                effectPrefab,
                GetBlastPosition(),
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

    private void PlayTeamBlastAnimation()
    {
        if (animator == null)
            return;

        animator.SetTrigger(
            TeamBlastHash);

        animator.SetTrigger(
            GetTeamBlastAnimationHash());
    }

    private int GetTeamBlastAnimationHash()
    {
        return currentTeamBlast switch
        {
            TeamBlastType.SonicOverdrive =>
                SonicOverdriveHash,

            TeamBlastType.ChaosInferno =>
                ChaosInfernoHash,

            TeamBlastType.FlowerFestival =>
                FlowerFestivalHash,

            TeamBlastType.ChaotixRecital =>
                ChaotixRecitalHash,

            TeamBlastType.SuperSonicPower =>
                SuperSonicPowerHash,

            _ =>
                TeamBlastHash
        };
    }

    #endregion

    #region Audio

    private void PlayTeamBlastSound()
    {
        if (audioSource == null)
            return;

        AudioClip clip =
            currentTeamBlast switch
            {
                TeamBlastType.SonicOverdrive =>
                    sonicOverdriveSound,

                TeamBlastType.ChaosInferno =>
                    chaosInfernoSound,

                TeamBlastType.FlowerFestival =>
                    flowerFestivalSound,

                TeamBlastType.ChaotixRecital =>
                    chaotixRecitalSound,

                TeamBlastType.SuperSonicPower =>
                    superSonicPowerSound,

                _ =>
                    null
            };

        if (clip != null)
        {
            audioSource.PlayOneShot(
                clip);
        }
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

    private string GetTeamBlastName()
    {
        return currentTeamBlast switch
        {
            TeamBlastType.SonicOverdrive =>
                "Sonic Overdrive",

            TeamBlastType.ChaosInferno =>
                "Chaos Inferno",

            TeamBlastType.FlowerFestival =>
                "Flower Festival",

            TeamBlastType.ChaotixRecital =>
                "Chaotix Recital",

            TeamBlastType.SuperSonicPower =>
                "Super Sonic Power",

            _ =>
                "Team Blast"
        };
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
                audioSource,
                nameof(AudioSource));

        if (characterSwitch == null)
        {
            Debug.LogWarning(
                "TeamBlast could not find CharacterSwitch.",
                this);
        }

        if (animator == null)
        {
            Debug.LogWarning(
                "TeamBlast could not find an Animator.",
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
            $"TeamBlast requires {displayName}.",
            this);

        return false;
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        if (blastRoutine != null)
        {
            StopCoroutine(
                blastRoutine);

            blastRoutine = null;
        }

        if (invincibilityRoutine != null)
        {
            StopCoroutine(
                invincibilityRoutine);

            invincibilityRoutine = null;
        }

        if (isInvincible)
        {
            isInvincible = false;

            BroadcastToTeam(
                "SetInvincible",
                0f);
        }

        if (isPerformingTeamBlast)
        {
            isPerformingTeamBlast = false;

            chaosControl?.CancelChaosControl();

            if (actionController != null &&
                actionController.CurrentAction ==
                    TeamActionController.TeamAction.TeamBlast)
            {
                actionController.EndAction(
                    restoreMovementControl: true);
            }
        }

        affectedTargets.Clear();
    }

    private void CleanupDestroyedState()
    {
        CleanupRuntimeState();

        isInitialized = false;

        actionController = null;
        characterSwitch = null;
        animator = null;
        audioSource = null;
        blastOrigin = null;
        chaosControl = null;

        sonicOverdriveEffect = null;
        chaosInfernoEffect = null;
        flowerFestivalEffect = null;
        chaotixRecitalEffect = null;
        superSonicPowerEffect = null;
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
