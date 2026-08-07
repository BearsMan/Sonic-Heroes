using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class Crate : MonoBehaviour
{
    #region Types

    public enum CrateType
    {
        Standard,
        Reinforced,
        Explosive,
        Item,
        Mission,
        Custom
    }

    public enum BreakMode
    {
        PlayerCollision,
        PlayerAttack,
        ExternalSignal,
        AnyValidHit
    }

    public enum SpawnMode
    {
        None,
        RandomItem,
        AllItems,
        WeightedRandom
    }

    public enum CrateState
    {
        Uninitialized,
        Intact,
        Breaking,
        Broken,
        Cooldown,
        Disabled
    }

    [Serializable]
    public sealed class CrateReward
    {
        [SerializeField] private GameObject prefab;

        [SerializeField, Min(1)]
        private int quantity = 1;

        [SerializeField, Min(0f)]
        private float weight = 1f;

        [SerializeField]
        private bool launchAfterSpawn = true;

        public GameObject Prefab =>
            prefab;

        public int Quantity =>
            Mathf.Max(
                1,
                quantity);

        public float Weight =>
            Mathf.Max(
                0f,
                weight);

        public bool LaunchAfterSpawn =>
            launchAfterSpawn;

        public bool IsValid =>
            prefab != null;
    }

    #endregion

    #region Constants

    private const string DefaultSpawnPointName =
        "Spawn Point";

    private const string DefaultBreakTrigger =
        "Break";

    #endregion

    #region Inspector

    [Header("Crate")]
    [SerializeField]
    private CrateType crateType =
        CrateType.Standard;

    [SerializeField]
    private BreakMode breakMode =
        BreakMode.PlayerCollision;

    [SerializeField]
    private CrateState currentState =
        CrateState.Uninitialized;

    [Header("Durability")]
    [SerializeField, Min(1)] private int maximumHealth = 1;
    [SerializeField, Min(1)] private int startingHealth = 1;
    [SerializeField, Min(0f)] private float damageCooldown = 0.1f;

    [Header("Rewards")]
    [SerializeField]
    private SpawnMode spawnMode =
        SpawnMode.RandomItem;

    [SerializeField]
    private CrateReward[] rewards =
        Array.Empty<CrateReward>();

    [SerializeField] private Transform spawnPoint;

    [SerializeField, Min(0f)]
    private float itemSpacing = 0.35f;

    [SerializeField, Min(0f)]
    private float spawnClearanceRadius = 0.2f;

    [SerializeField, Min(1)]
    private int maximumSpawnAttempts = 4;

    [SerializeField] private LayerMask spawnBlockingLayers = ~0;

    [Header("Reward Launch")]
    [SerializeField] private bool launchRewards = true;

    [SerializeField, Min(0f)]
    private float rewardLaunchSpeed = 5f;

    [SerializeField]
    private Vector3 localLaunchDirection =
        Vector3.up;

    [SerializeField] private bool clearRewardVelocity = true;

    [Header("Activation")]
    [SerializeField] private bool requirePlayerTag = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool requirePlayerMovement = true;
    [SerializeField, Min(0f)] private float minimumCollisionSpeed;

    [Header("Presentation")]
    [SerializeField] private Animator crateAnimator;
    [SerializeField]
    private string breakTrigger =
        DefaultBreakTrigger;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip breakSound;
    [SerializeField] private ParticleSystem breakEffect;

    [Header("Lifetime")]
    [SerializeField] private bool disableVisualsWhenBroken = true;
    [SerializeField] private bool destroyAfterBreaking = true;
    [SerializeField, Min(0f)] private float destroyDelay = 0.25f;
    [SerializeField, Min(0f)] private float resetCooldown = 1f;

    [Header("Runtime Safety")]
    [SerializeField] private bool enableRuntimeSafety = true;
    [SerializeField] private bool restoreDisabledComponents = true;
    [SerializeField, Min(0.1f)] private float safetyCheckInterval = 1f;
    [SerializeField, Min(0.01f)] private float minimumValidScale = 0.01f;
    [SerializeField] private bool preventDuplicatePrefabSpawns = true;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private readonly List<GameObject> spawnedRewards =
        new();

    private Collider crateCollider;
    private Renderer[] crateRenderers =
        Array.Empty<Renderer>();

    private int currentHealth;
    private int breakTriggerHash;

    private float damageCooldownTimer;
    private float resetCooldownTimer;
    private float safetyTimer;

    private bool initialized;
    private bool breaking;
    private bool broken;
    private bool shuttingDown;
    private bool applicationQuitting;

    #endregion

    #region Events

    public event Action<Crate, int> Damaged;
    public event Action<Crate> Broken;
    public event Action<Crate, GameObject> RewardSpawned;
    public event Action<Crate, CrateState, CrateState> StateChanged;
    public event Action<Crate> ResetCompleted;

    #endregion

    #region Public API

    public CrateType Type =>
        crateType;

    public CrateState CurrentState =>
        currentState;

    public int CurrentHealth =>
        currentHealth;

    public int MaximumHealth =>
        maximumHealth;

    public bool IsInitialized =>
        initialized;

    public bool IsBroken =>
        broken;

    public IReadOnlyList<GameObject> SpawnedRewards =>
        spawnedRewards;

    public bool TakeDamage(
        int damage)
    {
        if (!CanReceiveDamage() ||
            damage <= 0)
        {
            return false;
        }

        int appliedDamage =
            Mathf.Min(
                currentHealth,
                damage);

        currentHealth =
            Mathf.Max(
                0,
                currentHealth -
                appliedDamage);

        damageCooldownTimer =
            damageCooldown;

        Damaged?.Invoke(
            this,
            appliedDamage);

        if (currentHealth <= 0)
        {
            return BreakCrate();
        }

        return true;
    }

    public bool BreakCrate()
    {
        if (!CanBreak())
            return false;

        breaking =
            true;

        ChangeState(
            CrateState.Breaking);

        DisableCollider();
        PlayBreakPresentation();

        SpawnRewards();

        broken =
            true;

        breaking =
            false;

        ChangeState(
            CrateState.Broken);

        Broken?.Invoke(
            this);

        if (disableVisualsWhenBroken)
        {
            SetRenderersEnabled(
                false);
        }

        if (destroyAfterBreaking)
        {
            ScheduleDestruction();
        }
        else
        {
            resetCooldownTimer =
                resetCooldown;

            if (resetCooldownTimer > 0f)
            {
                ChangeState(
                    CrateState.Cooldown);
            }
        }

        return true;
    }

    public bool ResetCrate()
    {
        if (shuttingDown ||
            applicationQuitting)
        {
            return false;
        }

        CancelInvoke(
            nameof(DestroyCrateObject));

        currentHealth =
            Mathf.Clamp(
                startingHealth,
                1,
                maximumHealth);

        damageCooldownTimer =
            0f;

        resetCooldownTimer =
            0f;

        breaking =
            false;

        broken =
            false;

        RestoreCollider();
        SetRenderersEnabled(
            true);

        ChangeState(
            CrateState.Intact);

        ResetCompleted?.Invoke(
            this);

        return true;
    }

    public bool ReceiveExternalBreakSignal()
    {
        if (breakMode !=
            BreakMode.ExternalSignal)
        {
            return false;
        }

        return BreakCrate();
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
            currentState ==
            CrateState.Disabled)
        {
            ChangeState(
                broken
                    ? CrateState.Broken
                    : CrateState.Intact);
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

        UpdateTimers();

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

    private void OnCollisionEnter(
        Collision collision)
    {
        if (!initialized ||
            collision == null ||
            breaking ||
            broken)
        {
            return;
        }

        if (breakMode !=
                BreakMode.PlayerCollision &&
            breakMode !=
                BreakMode.AnyValidHit)
        {
            return;
        }

        if (!IsValidActivator(
                collision.collider))
        {
            return;
        }

        if (collision.relativeVelocity.magnitude <
            minimumCollisionSpeed)
        {
            return;
        }

        BreakCrate();
    }

    private void OnTriggerEnter(
        Collider other)
    {
        if (!initialized ||
            other == null ||
            breaking ||
            broken)
        {
            return;
        }

        if (breakMode !=
                BreakMode.PlayerAttack &&
            breakMode !=
                BreakMode.AnyValidHit)
        {
            return;
        }

        if (!IsValidActivator(
                other))
        {
            return;
        }

        BreakCrate();
    }

    private void OnDisable()
    {
        if (!shuttingDown &&
            !applicationQuitting)
        {
            ChangeState(
                CrateState.Disabled);
        }
    }

    private void OnApplicationQuit()
    {
        applicationQuitting =
            true;
    }

    private void OnDestroy()
    {
        shuttingDown =
            true;

        initialized =
            false;

        CancelInvoke();

        Damaged = null;
        Broken = null;
        RewardSpawned = null;
        StateChanged = null;
        ResetCompleted = null;

        spawnedRewards.Clear();

        crateCollider = null;
        crateRenderers = null;
        spawnPoint = null;
        crateAnimator = null;
        audioSource = null;
        breakEffect = null;
        rewards = null;

        currentState =
            CrateState.Disabled;
    }

    private void OnValidate()
    {
        maximumHealth =
            Mathf.Max(
                1,
                maximumHealth);

        startingHealth =
            Mathf.Clamp(
                startingHealth,
                1,
                maximumHealth);

        damageCooldown =
            Mathf.Max(
                0f,
                damageCooldown);

        itemSpacing =
            Mathf.Max(
                0f,
                itemSpacing);

        spawnClearanceRadius =
            Mathf.Max(
                0f,
                spawnClearanceRadius);

        maximumSpawnAttempts =
            Mathf.Max(
                1,
                maximumSpawnAttempts);

        rewardLaunchSpeed =
            Mathf.Max(
                0f,
                rewardLaunchSpeed);

        minimumCollisionSpeed =
            Mathf.Max(
                0f,
                minimumCollisionSpeed);

        destroyDelay =
            Mathf.Max(
                0f,
                destroyDelay);

        resetCooldown =
            Mathf.Max(
                0f,
                resetCooldown);

        safetyCheckInterval =
            Mathf.Max(
                0.1f,
                safetyCheckInterval);

        minimumValidScale =
            Mathf.Max(
                0.01f,
                minimumValidScale);

        if (!IsFiniteVector(
                localLaunchDirection) ||
            localLaunchDirection.sqrMagnitude <=
                0.0001f)
        {
            localLaunchDirection =
                Vector3.up;
        }

        rewards ??=
            Array.Empty<CrateReward>();

        CacheAnimatorHash();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ResolveReferences();
            ConfigureComponents();
        }
#endif
    }

    private void OnDrawGizmosSelected()
    {
        Transform point =
            spawnPoint != null
                ? spawnPoint
                : transform;

        Gizmos.DrawWireSphere(
            point.position,
            spawnClearanceRadius);

        Gizmos.DrawRay(
            point.position,
            GetLaunchDirection() *
            Mathf.Max(
                1f,
                rewardLaunchSpeed));
    }

    #endregion

    #region Initialization

    private bool Initialize()
    {
        if (initialized)
            return true;

        ResolveReferences();
        ConfigureComponents();
        CacheAnimatorHash();

        currentHealth =
            Mathf.Clamp(
                startingHealth,
                1,
                maximumHealth);

        damageCooldownTimer =
            0f;

        resetCooldownTimer =
            0f;

        safetyTimer =
            safetyCheckInterval;

        if (!ValidateConfiguration())
        {
            initialized =
                false;

            currentState =
                CrateState.Uninitialized;

            enabled =
                false;

            return false;
        }

        initialized =
            true;

        ChangeState(
            CrateState.Intact);

        return true;
    }

    private void ResolveReferences()
    {
        crateCollider ??=
            GetComponent<Collider>();

        crateAnimator ??=
            GetComponent<Animator>();

        crateAnimator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        audioSource ??=
            GetComponent<AudioSource>();

        audioSource ??=
            GetComponentInChildren<AudioSource>(
                includeInactive: true);

        spawnPoint ??=
            FindDescendantByName(
                DefaultSpawnPointName);

        spawnPoint ??=
            transform;

        if (crateRenderers == null ||
            crateRenderers.Length == 0)
        {
            crateRenderers =
                GetComponentsInChildren<Renderer>(
                    includeInactive: true);
        }
    }

    private void ConfigureComponents()
    {
        if (audioSource != null)
        {
            audioSource.playOnAwake =
                false;
        }
    }

    private void CacheAnimatorHash()
    {
        breakTriggerHash =
            GetAnimatorHash(
                breakTrigger);
    }

    #endregion

    #region State Machine

    private void ChangeState(
        CrateState newState)
    {
        if (currentState ==
            newState)
        {
            return;
        }

        CrateState previousState =
            currentState;

        currentState =
            newState;

        StateChanged?.Invoke(
            this,
            previousState,
            newState);

        LogStateChange(
            $"State changed from {previousState} to {newState}.");
    }

    #endregion

    #region Reward Spawning

    private int SpawnRewards()
    {
        RemoveDestroyedRewards();

        List<CrateReward> validRewards =
            GetValidRewards();

        if (validRewards.Count == 0 ||
            spawnMode ==
            SpawnMode.None)
        {
            return 0;
        }

        return spawnMode switch
        {
            SpawnMode.None =>
                0,

            SpawnMode.RandomItem =>
                SpawnRandomReward(
                    validRewards),

            SpawnMode.AllItems =>
                SpawnAllRewards(
                    validRewards),

            SpawnMode.WeightedRandom =>
                SpawnWeightedReward(
                    validRewards),

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(spawnMode),
                    spawnMode,
                    null)
        };
    }

    private List<CrateReward> GetValidRewards()
    {
        List<CrateReward> validRewards =
            new();

        foreach (CrateReward reward
                 in rewards)
        {
            if (reward != null &&
                reward.IsValid)
            {
                validRewards.Add(
                    reward);
            }
        }

        return validRewards;
    }

    private int SpawnRandomReward(
        IReadOnlyList<CrateReward> validRewards)
    {
        int index =
            UnityEngine.Random.Range(
                0,
                validRewards.Count);

        return SpawnReward(
            validRewards[index],
            0,
            1);
    }

    private int SpawnWeightedReward(
        IReadOnlyList<CrateReward> validRewards)
    {
        float totalWeight =
            0f;

        foreach (CrateReward reward
                 in validRewards)
        {
            totalWeight +=
                reward.Weight;
        }

        if (totalWeight <= 0f)
        {
            return SpawnRandomReward(
                validRewards);
        }

        float selection =
            UnityEngine.Random.Range(
                0f,
                totalWeight);

        foreach (CrateReward reward
                 in validRewards)
        {
            selection -=
                reward.Weight;

            if (selection <= 0f)
            {
                return SpawnReward(
                    reward,
                    0,
                    1);
            }
        }

        return SpawnReward(
            validRewards[
                validRewards.Count - 1],
            0,
            1);
    }

    private int SpawnAllRewards(
        IReadOnlyList<CrateReward> validRewards)
    {
        int spawnedCount =
            0;

        for (int index = 0;
             index < validRewards.Count;
             index++)
        {
            spawnedCount +=
                SpawnReward(
                    validRewards[index],
                    index,
                    validRewards.Count);
        }

        return spawnedCount;
    }

    private int SpawnReward(
        CrateReward reward,
        int rewardIndex,
        int rewardCount)
    {
        int spawnedCount =
            0;

        for (int quantityIndex = 0;
             quantityIndex < reward.Quantity;
             quantityIndex++)
        {
            int combinedIndex =
                rewardIndex +
                quantityIndex;

            int combinedCount =
                Mathf.Max(
                    rewardCount,
                    reward.Quantity);

            GameObject spawned =
                SpawnRewardObject(
                    reward,
                    combinedIndex,
                    combinedCount);

            if (spawned != null)
            {
                spawnedCount++;
            }
        }

        return spawnedCount;
    }

    private GameObject SpawnRewardObject(
        CrateReward reward,
        int index,
        int count)
    {
        if (reward == null ||
            reward.Prefab == null ||
            spawnPoint == null)
        {
            return null;
        }

        if (preventDuplicatePrefabSpawns &&
            HasSpawnedPrefab(
                reward.Prefab))
        {
            return null;
        }

        TryFindSafeSpawnPosition(
            index,
            count,
            out Vector3 position);

        Quaternion rotation =
            IsFiniteQuaternion(
                spawnPoint.rotation)
                ? spawnPoint.rotation
                : Quaternion.identity;

        GameObject spawned =
            Instantiate(
                reward.Prefab,
                position,
                rotation);

        if (spawned == null)
            return null;

        spawnedRewards.Add(
            spawned);

        if (launchRewards &&
            reward.LaunchAfterSpawn)
        {
            LaunchReward(
                spawned);
        }

        RewardSpawned?.Invoke(
            this,
            spawned);

        return spawned;
    }

    private bool TryFindSafeSpawnPosition(
        int index,
        int count,
        out Vector3 position)
    {
        position =
            CalculateSpawnPosition(
                index,
                count);

        if (spawnClearanceRadius <= 0f)
            return true;

        for (int attempt = 0;
             attempt < maximumSpawnAttempts;
             attempt++)
        {
            Vector3 candidate =
                position +
                spawnPoint.up *
                itemSpacing *
                attempt;

            if (!Physics.CheckSphere(
                    candidate,
                    spawnClearanceRadius,
                    spawnBlockingLayers,
                    QueryTriggerInteraction.Ignore))
            {
                position =
                    candidate;

                return true;
            }
        }

        return false;
    }

    private Vector3 CalculateSpawnPosition(
        int index,
        int count)
    {
        Vector3 position =
            spawnPoint.position;

        if (count <= 1)
            return position;

        float centeredIndex =
            index -
            (count - 1) *
            0.5f;

        return position +
               spawnPoint.right *
               centeredIndex *
               itemSpacing;
    }

    private void LaunchReward(
        GameObject spawned)
    {
        Rigidbody body =
            spawned.GetComponent<Rigidbody>();

        body ??=
            spawned.GetComponentInChildren<Rigidbody>(
                includeInactive: true);

        if (body == null)
            return;

        if (clearRewardVelocity)
        {
            body.linearVelocity =
                Vector3.zero;

            body.angularVelocity =
                Vector3.zero;
        }

        body.isKinematic =
            false;

        body.useGravity =
            true;

        body.AddForce(
            GetLaunchDirection() *
            rewardLaunchSpeed,
            ForceMode.VelocityChange);

        body.WakeUp();
    }

    private bool HasSpawnedPrefab(
        GameObject prefab)
    {
        string expectedName =
            $"{prefab.name}(Clone)";

        foreach (GameObject spawned
                 in spawnedRewards)
        {
            if (spawned != null &&
                string.Equals(
                    spawned.name,
                    expectedName,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 GetLaunchDirection()
    {
        Transform reference =
            spawnPoint != null
                ? spawnPoint
                : transform;

        Vector3 direction =
            reference.TransformDirection(
                localLaunchDirection);

        if (!IsFiniteVector(
                direction) ||
            direction.sqrMagnitude <=
                0.0001f)
        {
            direction =
                reference.up;
        }

        return direction.normalized;
    }

    #endregion

    #region Activation

    private bool IsValidActivator(
        Collider other)
    {
        if (other == null)
            return false;

        if (requirePlayerTag &&
            !HasTagInHierarchy(
                other.transform,
                playerTag))
        {
            return false;
        }

        if (!requirePlayerMovement)
            return true;

        UltimatePlayerMovement movement =
            other.GetComponent<UltimatePlayerMovement>();

        movement ??=
            other.GetComponentInParent<UltimatePlayerMovement>();

        movement ??=
            other.GetComponentInChildren<UltimatePlayerMovement>(
                includeInactive: true);

        return
            movement != null &&
            movement.isActiveAndEnabled &&
            movement.IsInitialized &&
            !movement.IsSafetyShutdown;
    }

    private bool CanReceiveDamage()
    {
        return
            initialized &&
            !breaking &&
            !broken &&
            !shuttingDown &&
            !applicationQuitting &&
            damageCooldownTimer <= 0f;
    }

    private bool CanBreak()
    {
        return
            initialized &&
            !breaking &&
            !broken &&
            !shuttingDown &&
            !applicationQuitting &&
            currentState !=
                CrateState.Disabled;
    }

    #endregion

    #region Presentation

    private void PlayBreakPresentation()
    {
        if (crateAnimator != null &&
            breakTriggerHash != 0)
        {
            crateAnimator.SetTrigger(
                breakTriggerHash);
        }

        breakEffect?.Play();

        if (audioSource != null &&
            breakSound != null)
        {
            audioSource.PlayOneShot(
                breakSound);
        }
    }

    #endregion

    #region Timers And Lifetime

    private void UpdateTimers()
    {
        if (damageCooldownTimer > 0f)
        {
            damageCooldownTimer =
                Mathf.Max(
                    0f,
                    damageCooldownTimer -
                    Time.deltaTime);
        }

        if (resetCooldownTimer > 0f)
        {
            resetCooldownTimer =
                Mathf.Max(
                    0f,
                    resetCooldownTimer -
                    Time.deltaTime);

            if (resetCooldownTimer <= 0f &&
                !destroyAfterBreaking)
            {
                ResetCrate();
            }
        }
    }

    private void ScheduleDestruction()
    {
        if (destroyDelay <= 0f)
        {
            DestroyCrateObject();
            return;
        }

        Invoke(
            nameof(DestroyCrateObject),
            destroyDelay);
    }

    private void DestroyCrateObject()
    {
        if (shuttingDown ||
            applicationQuitting)
        {
            return;
        }

        Destroy(
            gameObject);
    }

    #endregion

    #region Runtime Safety

    private bool RunRuntimeSafetyChecks()
    {
        if (!ValidateCoreReferences())
        {
            ResolveReferences();
            ConfigureComponents();

            if (!ValidateCoreReferences())
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
                "Crate Transform contains invalid values.");

            return false;
        }

        if (restoreDisabledComponents)
        {
            RestoreRequiredComponents();
        }

        RemoveDestroyedRewards();

        currentHealth =
            Mathf.Clamp(
                currentHealth,
                0,
                maximumHealth);

        return true;
    }

    private void RestoreRequiredComponents()
    {
        if (crateCollider != null &&
            !crateCollider.enabled &&
            !broken &&
            !breaking)
        {
            crateCollider.enabled =
                true;
        }

        if (crateAnimator != null &&
            !crateAnimator.enabled)
        {
            crateAnimator.enabled =
                true;
        }

        if (audioSource != null &&
            !audioSource.enabled)
        {
            audioSource.enabled =
                true;
        }
    }

    private void EnterSafetyShutdown(
        string reason)
    {
        initialized =
            false;

        breaking =
            false;

        ChangeState(
            CrateState.Disabled);

        DisableCollider();

        Debug.LogError(
            $"{nameof(Crate)} entered safety shutdown on '{name}': {reason}",
            this);

        enabled =
            false;
    }

    #endregion

    #region Validation

    private bool ValidateConfiguration()
    {
        bool valid =
            ValidateCoreReferences();

        if (!valid)
        {
            Debug.LogError(
                $"{nameof(Crate)} on '{name}' requires a Collider and Spawn Point.",
                this);
        }

        return valid;
    }

    private bool ValidateCoreReferences()
    {
        return
            crateCollider != null &&
            spawnPoint != null;
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

    #region Collider And Visuals

    private void DisableCollider()
    {
        if (crateCollider != null)
        {
            crateCollider.enabled =
                false;
        }
    }

    private void RestoreCollider()
    {
        if (crateCollider != null)
        {
            crateCollider.enabled =
                true;
        }
    }

    private void SetRenderersEnabled(
        bool value)
    {
        foreach (Renderer targetRenderer
                 in crateRenderers)
        {
            if (targetRenderer != null)
            {
                targetRenderer.enabled =
                    value;
            }
        }
    }

    private void RemoveDestroyedRewards()
    {
        for (int index =
                 spawnedRewards.Count - 1;
             index >= 0;
             index--)
        {
            if (spawnedRewards[index] == null)
            {
                spawnedRewards.RemoveAt(
                    index);
            }
        }
    }

    #endregion

    #region Helpers

    private Transform FindDescendantByName(
        string targetName)
    {
        if (string.IsNullOrWhiteSpace(
                targetName))
        {
            return null;
        }

        Transform[] descendants =
            GetComponentsInChildren<Transform>(
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

    private static bool HasTagInHierarchy(
        Transform source,
        string requiredTag)
    {
        if (source == null)
            return false;

        if (string.IsNullOrWhiteSpace(
                requiredTag))
        {
            return true;
        }

        Transform current =
            source;

        while (current != null)
        {
            if (current.CompareTag(
                    requiredTag))
            {
                return true;
            }

            current =
                current.parent;
        }

        return false;
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
