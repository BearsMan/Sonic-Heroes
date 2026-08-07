using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class Case : MonoBehaviour
{
    #region Types

    public enum CaseType
    {
        Item,
        GoalRing,
        Chao,
        Key,
        MissionObject,
        Progression,
        Custom
    }

    public enum CaseRequirementType
    {
        None,
        PlayerTouch,
        AnySwitchActivated,
        AllSwitchesActivated,
        EnemyCountDefeated,
        ObjectCountCollected,
        TeamFormation,
        CharacterType,
        Timed,
        ExternalSignal,
        Custom
    }

    public enum CaseState
    {
        Uninitialized,
        Ready,
        Locked,
        Waiting,
        Opening,
        Open,
        Empty,
        Cooldown,
        Disabled
    }

    public enum SpawnMode
    {
        RandomItem,
        AllItems,
        WeightedRandom
    }

    public enum RewardType
    {
        None,
        Ring,
        ItemBox,
        ExtraLife,
        Shield,
        Invincibility,
        SpeedShoes,
        Key,
        Emerald,
        Character,
        GoalRing,
        Custom
    }

    [Serializable]
    public sealed class CaseReward
    {
        [SerializeField]
        private RewardType rewardType =
            RewardType.Custom;

        [SerializeField] private GameObject prefab;

        [SerializeField, Min(1)]
        private int quantity = 1;

        [SerializeField, Min(0f)]
        private float weight = 1f;

        [SerializeField] private bool launchAfterSpawn = true;

        public RewardType Type =>
            rewardType;

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

    [Serializable]
    public sealed class CaseRequirement
    {
        [SerializeField]
        private CaseRequirementType requirementType =
            CaseRequirementType.None;

        [SerializeField]
        private Switch[] switches =
            Array.Empty<Switch>();

        [SerializeField, Min(0)]
        private int requiredCount;

        [SerializeField] private bool invertResult;

        private int runtimeCount;
        private bool externalSignalReceived;

        public CaseRequirementType Type =>
            requirementType;

        public bool IsSatisfied(
            Case owner,
            UltimatePlayerMovement activator)
        {
            bool result =
                requirementType switch
                {
                    CaseRequirementType.None =>
                        true,

                    CaseRequirementType.PlayerTouch =>
                        activator != null,

                    CaseRequirementType.AnySwitchActivated =>
                        IsAnySwitchActivated(),

                    CaseRequirementType.AllSwitchesActivated =>
                        AreAllSwitchesActivated(),

                    CaseRequirementType.EnemyCountDefeated =>
                        runtimeCount >=
                        requiredCount,

                    CaseRequirementType.ObjectCountCollected =>
                        runtimeCount >=
                        requiredCount,

                    CaseRequirementType.TeamFormation =>
                        owner.ValidateTeamFormationRequirement(
                            activator),

                    CaseRequirementType.CharacterType =>
                        owner.ValidateCharacterTypeRequirement(
                            activator),

                    CaseRequirementType.Timed =>
                        owner.HasTimedRequirementCompleted,

                    CaseRequirementType.ExternalSignal =>
                        externalSignalReceived,

                    CaseRequirementType.Custom =>
                        owner.EvaluateCustomRequirement(
                            this,
                            activator),

                    _ =>
                        throw new ArgumentOutOfRangeException(
                            nameof(requirementType),
                            requirementType,
                            null)
                };

            return invertResult
                ? !result
                : result;
        }

        public void AddProgress(
            int amount)
        {
            runtimeCount =
                Mathf.Max(
                    0,
                    runtimeCount +
                    amount);
        }

        public void SetProgress(
            int amount)
        {
            runtimeCount =
                Mathf.Max(
                    0,
                    amount);
        }

        public void SetExternalSignal(
            bool value)
        {
            externalSignalReceived =
                value;
        }

        public void ResetRuntime()
        {
            runtimeCount =
                0;

            externalSignalReceived =
                false;
        }

        public void ResolveSwitchesAutomatically(
            Case owner,
            float searchRadius,
            bool includeInactive)
        {
            if (owner == null)
                return;

            if (switches != null &&
                switches.Length > 0)
            {
                return;
            }

            Switch[] foundSwitches =
                FindObjectsByType<Switch>(
                    includeInactive
                        ? FindObjectsInactive.Include
                        : FindObjectsInactive.Exclude);

            List<Switch> nearbySwitches =
                new();

            float maximumSqrDistance =
                searchRadius *
                searchRadius;

            foreach (Switch candidate
                     in foundSwitches)
            {
                if (candidate == null ||
                    !candidate.gameObject.scene.IsValid())
                {
                    continue;
                }

                float sqrDistance =
                    (candidate.transform.position -
                     owner.transform.position)
                    .sqrMagnitude;

                if (sqrDistance <=
                    maximumSqrDistance)
                {
                    nearbySwitches.Add(
                        candidate);
                }
            }

            switches =
                nearbySwitches.ToArray();
        }

        private bool IsAnySwitchActivated()
        {
            if (switches == null ||
                switches.Length == 0)
            {
                return false;
            }

            foreach (Switch targetSwitch
                     in switches)
            {
                if (targetSwitch != null &&
                    targetSwitch.IsActivated)
                {
                    return true;
                }
            }

            return false;
        }

        private bool AreAllSwitchesActivated()
        {
            if (switches == null ||
                switches.Length == 0)
            {
                return false;
            }

            foreach (Switch targetSwitch
                     in switches)
            {
                if (targetSwitch == null ||
                    !targetSwitch.IsActivated)
                {
                    return false;
                }
            }

            return true;
        }
    }

    #endregion

    #region Constants

    private const string DefaultSpawnPointName =
        "Spawn Point";

    private const string DefaultOpenTrigger =
        "Open";

    private const string DefaultLockedTrigger =
        "Locked";

    #endregion

    #region Inspector

    [Header("Case")]
    [SerializeField]
    private CaseType caseType =
        CaseType.Item;

    [SerializeField]
    private CaseState currentState =
        CaseState.Uninitialized;

    [Header("Rewards")]
    [SerializeField]
    private SpawnMode spawnMode =
        SpawnMode.RandomItem;

    [SerializeField]
    private CaseReward[] rewards =
        Array.Empty<CaseReward>();

    [SerializeField] private Transform spawnPoint;

    [SerializeField, Min(0f)]
    private float itemSpacing = 0.35f;

    [SerializeField, Min(0f)]
    private float spawnClearanceRadius = 0.2f;

    [SerializeField, Min(1)]
    private int maximumSpawnAttempts = 4;

    [SerializeField] private LayerMask spawnBlockingLayers = ~0;

    [Header("Item Launch")]
    [SerializeField] private bool launchSpawnedItems = true;

    [SerializeField, Min(0f)]
    private float itemLaunchSpeed = 5f;

    [SerializeField]
    private Vector3 localLaunchDirection =
        Vector3.up;

    [SerializeField] private bool clearSpawnedVelocity = true;

    [Header("Requirements")]
    [SerializeField]
    private CaseRequirement[] requirements =
        Array.Empty<CaseRequirement>();

    [SerializeField] private bool requireAllRequirements = true;
    [SerializeField] private bool openAutomaticallyWhenRequirementsMet;
    [SerializeField] private bool resolveSwitchRequirementsAutomatically = true;
    [SerializeField] private bool includeInactiveSwitches;
    [SerializeField, Min(0f)] private float switchSearchRadius = 20f;

    [Header("Timed Requirement")]
    [SerializeField, Min(0f)] private float timedRequirementDuration;
    [SerializeField] private bool startTimedRequirementOnEnable;

    [Header("Activator")]
    [SerializeField] private bool requirePlayerTag = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool requirePlayerMovement = true;
    [SerializeField] private bool openOnPlayerTouch = true;

    [Header("Behavior")]
    [SerializeField] private bool openOnlyOnce = true;
    [SerializeField] private bool destroyAfterOpening = true;
    [SerializeField, Min(0f)] private float destroyDelay = 1.5f;
    [SerializeField] private bool disableVisualsBeforeDestroy;
    [SerializeField, Min(0f)] private float reopenCooldown = 1f;

    [Header("Animation")]
    [SerializeField] private Animator caseAnimator;
    [SerializeField]
    private string openTrigger =
        DefaultOpenTrigger;

    [SerializeField]
    private string lockedTrigger =
        DefaultLockedTrigger;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip lockedSound;

    [Header("Effects")]
    [SerializeField] private ParticleSystem openEffect;
    [SerializeField] private ParticleSystem lockedEffect;

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

    private readonly List<GameObject> spawnedItems =
        new();

    private Collider caseCollider;
    private Renderer[] caseRenderers =
        Array.Empty<Renderer>();

    private float safetyTimer;
    private float timedRequirementTimer;
    private float cooldownTimer;

    private int openTriggerHash;
    private int lockedTriggerHash;

    private bool initialized;
    private bool hasOpened;
    private bool opening;
    private bool timedRequirementRunning;
    private bool timedRequirementCompleted;
    private bool shuttingDown;
    private bool applicationQuitting;

    #endregion

    #region Events

    public event Action<Case> Opened;
    public event Action<Case> LockedAttempted;
    public event Action<Case, GameObject> RewardSpawned;
    public event Action<Case, CaseState, CaseState> StateChanged;
    public event Action<Case> ResetCompleted;

    #endregion

    #region Public API

    public CaseType Type =>
        caseType;

    public CaseState CurrentState =>
        currentState;

    public bool IsInitialized =>
        initialized;

    public bool HasOpened =>
        hasOpened;

    public bool IsOpening =>
        opening;

    public bool HasTimedRequirementCompleted =>
        timedRequirementCompleted;

    public IReadOnlyList<GameObject> SpawnedItems =>
        spawnedItems;

    public bool OpenCase()
    {
        return OpenCase(
            null);
    }

    public bool OpenCase(
        UltimatePlayerMovement activator)
    {
        if (!CanAttemptOpen())
            return false;

        if (!AreRequirementsSatisfied(
                activator))
        {
            HandleLockedAttempt();

            return false;
        }

        return PerformOpen();
    }

    public bool ResetCase()
    {
        if (shuttingDown ||
            applicationQuitting)
        {
            return false;
        }

        CancelInvoke(
            nameof(DestroyCase));

        hasOpened =
            false;

        opening =
            false;

        cooldownTimer =
            0f;

        timedRequirementTimer =
            timedRequirementDuration;

        timedRequirementCompleted =
            timedRequirementDuration <= 0f;

        timedRequirementRunning =
            startTimedRequirementOnEnable &&
            !timedRequirementCompleted;

        foreach (CaseRequirement requirement
                 in requirements)
        {
            requirement?.ResetRuntime();
        }

        RestoreCollider();
        SetRenderersEnabled(
            true);

        ChangeState(
            AreRequirementsSatisfied(
                null)
                ? CaseState.Ready
                : CaseState.Locked);

        ResetCompleted?.Invoke(
            this);

        return true;
    }

    public void AddRequirementProgress(
        CaseRequirementType type,
        int amount = 1)
    {
        foreach (CaseRequirement requirement
                 in requirements)
        {
            if (requirement != null &&
                requirement.Type ==
                type)
            {
                requirement.AddProgress(
                    amount);
            }
        }
    }

    public void SetRequirementProgress(
        CaseRequirementType type,
        int amount)
    {
        foreach (CaseRequirement requirement
                 in requirements)
        {
            if (requirement != null &&
                requirement.Type ==
                type)
            {
                requirement.SetProgress(
                    amount);
            }
        }
    }

    public void SetExternalSignal(
        bool value)
    {
        foreach (CaseRequirement requirement
                 in requirements)
        {
            if (requirement != null &&
                requirement.Type ==
                CaseRequirementType.ExternalSignal)
            {
                requirement.SetExternalSignal(
                    value);
            }
        }
    }

    public void StartTimedRequirement()
    {
        timedRequirementTimer =
            timedRequirementDuration;

        timedRequirementCompleted =
            timedRequirementDuration <= 0f;

        timedRequirementRunning =
            !timedRequirementCompleted;
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

        if (startTimedRequirementOnEnable &&
            !timedRequirementCompleted)
        {
            StartTimedRequirement();
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

        UpdateTimedRequirement();
        UpdateCooldown();
        UpdateAutomaticOpening();

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

    private void OnDisable()
    {
        if (!shuttingDown &&
            !applicationQuitting)
        {
            ChangeState(
                CaseState.Disabled);
        }
    }

    private void OnTriggerEnter(
        Collider other)
    {
        if (!initialized ||
            shuttingDown ||
            applicationQuitting ||
            other == null ||
            !openOnPlayerTouch)
        {
            return;
        }

        UltimatePlayerMovement activator =
            ResolveActivator(
                other);

        if (requirePlayerMovement &&
            activator == null)
        {
            return;
        }

        OpenCase(
            activator);
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

        Opened = null;
        LockedAttempted = null;
        RewardSpawned = null;
        StateChanged = null;
        ResetCompleted = null;

        spawnedItems.Clear();

        caseCollider = null;
        caseRenderers = null;
        spawnPoint = null;
        caseAnimator = null;
        audioSource = null;
        openEffect = null;
        lockedEffect = null;
        requirements = null;
        rewards = null;

        currentState =
            CaseState.Disabled;
    }

    private void OnValidate()
    {
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

        itemLaunchSpeed =
            Mathf.Max(
                0f,
                itemLaunchSpeed);

        switchSearchRadius =
            Mathf.Max(
                0f,
                switchSearchRadius);

        timedRequirementDuration =
            Mathf.Max(
                0f,
                timedRequirementDuration);

        destroyDelay =
            Mathf.Max(
                0f,
                destroyDelay);

        reopenCooldown =
            Mathf.Max(
                0f,
                reopenCooldown);

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

        requirements ??=
            Array.Empty<CaseRequirement>();

        rewards ??=
            Array.Empty<CaseReward>();

        CacheAnimatorHashes();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ResolveReferences();
            ConfigureComponents();
            ResolveRequirementSwitches();
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
                itemLaunchSpeed));

        if (resolveSwitchRequirementsAutomatically &&
            switchSearchRadius > 0f)
        {
            Gizmos.DrawWireSphere(
                transform.position,
                switchSearchRadius);
        }
    }

    #endregion

    #region Initialization

    private bool Initialize()
    {
        if (initialized)
            return true;

        ResolveReferences();
        ConfigureComponents();
        CacheAnimatorHashes();
        ResolveRequirementSwitches();

        safetyTimer =
            safetyCheckInterval;

        timedRequirementTimer =
            timedRequirementDuration;

        timedRequirementCompleted =
            timedRequirementDuration <= 0f;

        timedRequirementRunning =
            startTimedRequirementOnEnable &&
            !timedRequirementCompleted;

        if (!ValidateConfiguration())
        {
            initialized =
                false;

            currentState =
                CaseState.Uninitialized;

            enabled =
                false;

            return false;
        }

        initialized =
            true;

        ChangeState(
            AreRequirementsSatisfied(
                null)
                ? CaseState.Ready
                : CaseState.Locked);

        return true;
    }

    private void ResolveReferences()
    {
        caseCollider ??=
            GetComponent<Collider>();

        caseAnimator ??=
            GetComponent<Animator>();

        caseAnimator ??=
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

        if (caseRenderers == null ||
            caseRenderers.Length == 0)
        {
            caseRenderers =
                GetComponentsInChildren<Renderer>(
                    includeInactive: true);
        }
    }

    private void ConfigureComponents()
    {
        if (caseCollider != null)
        {
            caseCollider.isTrigger =
                true;
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake =
                false;
        }
    }

    private void CacheAnimatorHashes()
    {
        openTriggerHash =
            GetAnimatorHash(
                openTrigger);

        lockedTriggerHash =
            GetAnimatorHash(
                lockedTrigger);
    }

    private void ResolveRequirementSwitches()
    {
        if (!resolveSwitchRequirementsAutomatically)
            return;

        foreach (CaseRequirement requirement
                 in requirements)
        {
            requirement?.ResolveSwitchesAutomatically(
                this,
                switchSearchRadius,
                includeInactiveSwitches);
        }
    }

    #endregion

    #region State Machine

    private void ChangeState(
        CaseState newState)
    {
        if (currentState ==
            newState)
        {
            return;
        }

        CaseState previousState =
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

    private bool PerformOpen()
    {
        opening =
            true;

        ChangeState(
            CaseState.Opening);

        DisableCollider();
        PlayOpenPresentation();

        int spawnedCount =
            SpawnRewards();

        hasOpened =
            true;

        opening =
            false;

        ChangeState(
            spawnedCount > 0
                ? CaseState.Open
                : CaseState.Empty);

        Opened?.Invoke(
            this);

        if (destroyAfterOpening)
        {
            ScheduleDestruction();
        }
        else if (!openOnlyOnce)
        {
            cooldownTimer =
                reopenCooldown;

            ChangeState(
                cooldownTimer > 0f
                    ? CaseState.Cooldown
                    : CaseState.Ready);
        }

        return true;
    }

    #endregion

    #region Requirements

    private bool AreRequirementsSatisfied(
        UltimatePlayerMovement activator)
    {
        if (requirements == null ||
            requirements.Length == 0)
        {
            return true;
        }

        bool anyValidRequirement =
            false;

        if (requireAllRequirements)
        {
            foreach (CaseRequirement requirement
                     in requirements)
            {
                if (requirement == null)
                    continue;

                anyValidRequirement =
                    true;

                if (!requirement.IsSatisfied(
                        this,
                        activator))
                {
                    return false;
                }
            }

            return
                !anyValidRequirement ||
                true;
        }

        foreach (CaseRequirement requirement
                 in requirements)
        {
            if (requirement == null)
                continue;

            anyValidRequirement =
                true;

            if (requirement.IsSatisfied(
                    this,
                    activator))
            {
                return true;
            }
        }

        return
            !anyValidRequirement;
    }

    private void UpdateAutomaticOpening()
    {
        if (!openAutomaticallyWhenRequirementsMet ||
            hasOpened ||
            opening ||
            cooldownTimer > 0f)
        {
            return;
        }

        if (AreRequirementsSatisfied(
                null))
        {
            OpenCase();
        }
        else if (currentState !=
                 CaseState.Locked)
        {
            ChangeState(
                CaseState.Locked);
        }
    }

    private void UpdateTimedRequirement()
    {
        if (!timedRequirementRunning)
            return;

        timedRequirementTimer =
            Mathf.Max(
                0f,
                timedRequirementTimer -
                Time.deltaTime);

        if (timedRequirementTimer > 0f)
            return;

        timedRequirementRunning =
            false;

        timedRequirementCompleted =
            true;
    }

    private void HandleLockedAttempt()
    {
        ChangeState(
            CaseState.Locked);

        SetAnimatorTrigger(
            lockedTriggerHash);

        lockedEffect?.Play();

        PlaySound(
            lockedSound);

        LockedAttempted?.Invoke(
            this);
    }

    internal bool ValidateTeamFormationRequirement(
        UltimatePlayerMovement activator)
    {
        return activator != null;
    }

    internal bool ValidateCharacterTypeRequirement(
        UltimatePlayerMovement activator)
    {
        return
            activator != null &&
            activator.CharacterDefinition != null;
    }

    internal bool EvaluateCustomRequirement(
        CaseRequirement requirement,
        UltimatePlayerMovement activator)
    {
        return false;
    }

    #endregion

    #region Reward Spawning

    private int SpawnRewards()
    {
        RemoveDestroyedSpawnedItems();

        List<CaseReward> validRewards =
            GetValidRewards();

        if (validRewards.Count == 0)
            return 0;

        return spawnMode switch
        {
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

    private List<CaseReward> GetValidRewards()
    {
        List<CaseReward> validRewards =
            new();

        foreach (CaseReward reward
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
        IReadOnlyList<CaseReward> validRewards)
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
        IReadOnlyList<CaseReward> validRewards)
    {
        float totalWeight =
            0f;

        foreach (CaseReward reward
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

        foreach (CaseReward reward
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
        IReadOnlyList<CaseReward> validRewards)
    {
        int count =
            0;

        int rewardIndex =
            0;

        foreach (CaseReward reward
                 in validRewards)
        {
            count +=
                SpawnReward(
                    reward,
                    rewardIndex,
                    validRewards.Count);

            rewardIndex++;
        }

        return count;
    }

    private int SpawnReward(
        CaseReward reward,
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
        CaseReward reward,
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

        spawnedItems.Add(
            spawned);

        if (launchSpawnedItems &&
            reward.LaunchAfterSpawn)
        {
            LaunchSpawnedReward(
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

    private void LaunchSpawnedReward(
        GameObject spawned)
    {
        Rigidbody body =
            spawned.GetComponent<Rigidbody>();

        body ??=
            spawned.GetComponentInChildren<Rigidbody>(
                includeInactive: true);

        if (body == null)
            return;

        if (clearSpawnedVelocity)
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
            itemLaunchSpeed,
            ForceMode.VelocityChange);

        body.WakeUp();
    }

    private bool HasSpawnedPrefab(
        GameObject prefab)
    {
        string expectedName =
            $"{prefab.name}(Clone)";

        foreach (GameObject spawned
                 in spawnedItems)
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

    #region Activator

    private UltimatePlayerMovement ResolveActivator(
        Collider other)
    {
        if (other == null)
            return null;

        if (requirePlayerTag &&
            !HasTagInHierarchy(
                other.transform,
                playerTag))
        {
            return null;
        }

        UltimatePlayerMovement movement =
            other.GetComponent<UltimatePlayerMovement>();

        movement ??=
            other.GetComponentInParent<UltimatePlayerMovement>();

        movement ??=
            other.GetComponentInChildren<UltimatePlayerMovement>(
                includeInactive: true);

        if (movement == null)
            return null;

        return
            movement.isActiveAndEnabled &&
            movement.IsInitialized &&
            !movement.IsSafetyShutdown
                ? movement
                : null;
    }

    private bool CanAttemptOpen()
    {
        if (!initialized ||
            shuttingDown ||
            applicationQuitting ||
            opening ||
            cooldownTimer > 0f ||
            currentState ==
                CaseState.Disabled)
        {
            return false;
        }

        return
            !openOnlyOnce ||
            !hasOpened;
    }

    #endregion

    #region Presentation

    private void PlayOpenPresentation()
    {
        SetAnimatorTrigger(
            openTriggerHash);

        openEffect?.Play();

        PlaySound(
            openSound);
    }

    private void SetAnimatorTrigger(
        int hash)
    {
        if (caseAnimator == null ||
            hash == 0)
        {
            return;
        }

        caseAnimator.SetTrigger(
            hash);
    }

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

    #region Timers And Lifetime

    private void UpdateCooldown()
    {
        if (cooldownTimer <= 0f)
            return;

        cooldownTimer =
            Mathf.Max(
                0f,
                cooldownTimer -
                Time.deltaTime);

        if (cooldownTimer <= 0f &&
            !openOnlyOnce)
        {
            RestoreCollider();

            ChangeState(
                AreRequirementsSatisfied(
                    null)
                    ? CaseState.Ready
                    : CaseState.Locked);
        }
    }

    private void ScheduleDestruction()
    {
        if (disableVisualsBeforeDestroy)
        {
            SetRenderersEnabled(
                false);
        }

        if (destroyDelay <= 0f)
        {
            DestroyCase();
            return;
        }

        Invoke(
            nameof(DestroyCase),
            destroyDelay);
    }

    private void DestroyCase()
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
                "Case Transform contains invalid values.");

            return false;
        }

        if (restoreDisabledComponents)
        {
            RestoreRequiredComponents();
        }

        RemoveDestroyedSpawnedItems();

        return true;
    }

    private void RestoreRequiredComponents()
    {
        if (caseCollider != null &&
            !caseCollider.enabled &&
            !hasOpened &&
            !opening)
        {
            caseCollider.enabled =
                true;
        }

        if (caseAnimator != null &&
            !caseAnimator.enabled)
        {
            caseAnimator.enabled =
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

        opening =
            false;

        ChangeState(
            CaseState.Disabled);

        DisableCollider();

        Debug.LogError(
            $"{nameof(Case)} entered safety shutdown on '{name}': {reason}",
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
                $"{nameof(Case)} on '{name}' requires a Collider and Spawn Point.",
                this);
        }

        return valid;
    }

    private bool ValidateCoreReferences()
    {
        return
            caseCollider != null &&
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
        if (caseCollider != null)
        {
            caseCollider.enabled =
                false;
        }
    }

    private void RestoreCollider()
    {
        if (caseCollider == null)
            return;

        caseCollider.enabled =
            true;

        caseCollider.isTrigger =
            true;
    }

    private void SetRenderersEnabled(
        bool value)
    {
        foreach (Renderer targetRenderer
                 in caseRenderers)
        {
            if (targetRenderer != null)
            {
                targetRenderer.enabled =
                    value;
            }
        }
    }

    private void RemoveDestroyedSpawnedItems()
    {
        for (int index =
                 spawnedItems.Count - 1;
             index >= 0;
             index--)
        {
            if (spawnedItems[index] == null)
            {
                spawnedItems.RemoveAt(
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
