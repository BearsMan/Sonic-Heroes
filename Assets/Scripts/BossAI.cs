using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossAI : AIController
{
    #region Types

    public enum BossEncounterState
    {
        Dormant,
        Intro,
        Active,
        PhaseTransition,
        Intermission,
        Staggered,
        Enraged,
        FinalPhase,
        Defeated,
        Completed,
        Disabled
    }

    [Serializable]
    public sealed class BossPhase
    {
        [SerializeField] private string phaseName = "Phase";
        [SerializeField, Range(0.01f, 1f)] private float healthThreshold = 1f;
        [SerializeField, Min(0f)] private float intermissionDuration;
        [SerializeField] private bool enableSpecialState;
        [SerializeField] private bool checkpointPhase;
        [SerializeField] private AudioClip phaseMusic;
        [SerializeField] private AudioClip phaseVoice;
        [SerializeField] private ParticleSystem phaseEffect;

        public string PhaseName =>
            phaseName;

        public float HealthThreshold =>
            healthThreshold;

        public float IntermissionDuration =>
            intermissionDuration;

        public bool EnableSpecialState =>
            enableSpecialState;

        public bool CheckpointPhase =>
            checkpointPhase;

        public AudioClip PhaseMusic =>
            phaseMusic;

        public AudioClip PhaseVoice =>
            phaseVoice;

        public ParticleSystem PhaseEffect =>
            phaseEffect;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(
                    phaseName))
            {
                phaseName =
                    "Phase";
            }

            healthThreshold =
                Mathf.Clamp(
                    healthThreshold,
                    0.01f,
                    1f);

            intermissionDuration =
                Mathf.Max(
                    0f,
                    intermissionDuration);
        }
    }

    #endregion

    #region Constants

    private const int MaximumPhaseCount =
        12;

    private const string ArenaTriggerName =
        "Arena Trigger";

    #endregion

    #region Inspector

    [Header("Encounter")]
    [SerializeField] private bool activateOnStart;
    [SerializeField] private bool requirePlayerInArena = true;
    [SerializeField] private Collider arenaTrigger;
    [SerializeField] private Transform arenaCenter;
    [SerializeField, Min(0f)] private float maximumArenaRadius = 50f;
    [SerializeField] private bool returnToArenaWhenOutside = true;

    [Header("Introduction")]
    [SerializeField, Min(0f)] private float introDuration = 2f;
    [SerializeField] private bool lockMovementDuringIntro = true;
    [SerializeField] private AudioClip introMusic;
    [SerializeField] private AudioClip introVoice;
    [SerializeField] private ParticleSystem introEffect;

    [Header("Phases")]
    [SerializeField]
    private BossPhase[] phases =
        Array.Empty<BossPhase>();

    [SerializeField, Min(0f)] private float phaseTransitionDuration = 1.5f;
    [SerializeField] private bool invulnerableDuringPhaseTransition = true;
    [SerializeField] private AudioClip phaseTransitionSound;
    [SerializeField] private ParticleSystem phaseTransitionEffect;

    [Header("Intermissions")]
    [SerializeField] private bool lockMovementDuringIntermission = true;
    [SerializeField] private bool restoreTargetAfterIntermission = true;
    [SerializeField] private AudioClip intermissionSound;
    [SerializeField] private ParticleSystem intermissionEffect;

    [Header("Stagger")]
    [SerializeField] private bool enableStagger = true;
    [SerializeField, Min(1)] private int damageRequiredToStagger = 5;
    [SerializeField, Min(0f)] private float staggerDuration = 2.5f;
    [SerializeField, Min(0f)] private float staggerResetDelay = 4f;
    [SerializeField] private AudioClip staggerSound;
    [SerializeField] private ParticleSystem staggerEffect;

    [Header("Enrage")]
    [SerializeField] private bool enableEnrage = true;
    [SerializeField, Range(0f, 1f)] private float enrageHealthThreshold = 0.2f;
    [SerializeField] private AudioClip enrageSound;
    [SerializeField] private ParticleSystem enrageEffect;

    [Header("Checkpoints")]
    [SerializeField] private bool enablePhaseCheckpoints = true;
    [SerializeField] private bool restoreCheckpointOnReset = true;
    [SerializeField] private bool restoreHealthToCheckpointThreshold = true;

    [Header("Arena Recovery")]
    [SerializeField, Min(0f)] private float arenaReturnSpeedMultiplier = 1.75f;
    [SerializeField, Min(0f)] private float arenaArrivalDistance = 2f;
    [SerializeField, Min(0.1f)] private float arenaCheckInterval = 0.5f;

    [Header("Victory")]
    [SerializeField, Min(0f)] private float victoryDelay = 2f;
    [SerializeField] private bool disableAfterVictory = true;
    [SerializeField] private AudioClip defeatSound;
    [SerializeField] private AudioClip victoryMusic;
    [SerializeField] private ParticleSystem defeatEffect;
    [SerializeField] private ParticleSystem victoryEffect;

    [Header("Presentation")]
    [SerializeField] private AudioSource bossAudioSource;

    [Header("Runtime Safety")]
    [SerializeField] private bool restoreDisabledComponents = true;
    [SerializeField, Min(0.1f)] private float safetyCheckInterval = 0.5f;
    [SerializeField, Min(0.01f)] private float minimumValidScale = 0.01f;

    [Header("Debug")]
    [SerializeField]
    private BossEncounterState bossEncounterState =
        BossEncounterState.Dormant;

    [SerializeField] private bool logBossState;

    #endregion

    #region Runtime State

    private float introTimer;
    private float phaseTransitionTimer;
    private float intermissionTimer;
    private float staggerTimer;
    private float staggerResetTimer;
    private float arenaCheckTimer;
    private float victoryTimer;
    private float safetyTimer;
    private float normalAgentSpeed;

    private int currentPhaseIndex;
    private int checkpointPhaseIndex;
    private int accumulatedStaggerDamage;

    private bool playerInsideArena;
    private bool encounterStarted;
    private bool enraged;
    private bool finalPhaseEntered;
    private bool phaseTransitionPending;
    private bool victoryPending;
    private bool initializedBoss;
    private bool shuttingDownBoss;

    private Transform preservedTarget;

    #endregion

    #region Events

    public event Action<BossAI> EncounterStarted;
    public event Action<BossAI, int, BossPhase> PhaseChanged;
    public event Action<BossAI, int> CheckpointReached;
    public event Action<BossAI> StaggerStarted;
    public event Action<BossAI> StaggerEnded;
    public event Action<BossAI> Enraged;
    public event Action<BossAI> FinalPhaseStarted;
    public event Action<BossAI> BossDefeated;
    public event Action<BossAI> EncounterCompleted;
    public event Action<BossAI, BossEncounterState> BossStateChanged;

    #endregion

    #region Public API

    public BossEncounterState CurrentBossState =>
        bossEncounterState;

    public int CurrentPhaseIndex =>
        currentPhaseIndex;

    public int CheckpointPhaseIndex =>
        checkpointPhaseIndex;

    public BossPhase CurrentPhase =>
        phases != null &&
        currentPhaseIndex >= 0 &&
        currentPhaseIndex <
            phases.Length
            ? phases[currentPhaseIndex]
            : null;

    public bool EncounterActive =>
        encounterStarted &&
        !IsDead;

    public bool IsEnraged =>
        enraged;

    public bool IsFinalPhase =>
        finalPhaseEntered;

    public bool IsTransitioning =>
        bossEncounterState ==
            BossEncounterState.PhaseTransition ||
        bossEncounterState ==
            BossEncounterState.Intermission;

    public float HealthRatio =>
        MaximumHealth > 0
            ? Mathf.Clamp01(
                (float)CurrentHealth /
                MaximumHealth)
            : 0f;

    public bool StartEncounter()
    {
        if (!initializedBoss ||
            encounterStarted ||
            IsDead)
        {
            return false;
        }

        encounterStarted =
            true;

        introTimer =
            introDuration;

        preservedTarget =
            Target;

        ChangeBossState(
            introDuration > 0f
                ? BossEncounterState.Intro
                : BossEncounterState.Active);

        if (lockMovementDuringIntro)
        {
            StopAgent();
        }

        introEffect?.Play();

        PlayBossSound(
            introVoice);

        PlayBossMusic(
            introMusic);

        EncounterStarted?.Invoke(
            this);

        if (introDuration <= 0f)
        {
            CompleteIntro();
        }

        return true;
    }

    public void NotifyPlayerEnteredArena()
    {
        playerInsideArena =
            true;

        if (!encounterStarted)
        {
            StartEncounter();
        }
    }

    public void NotifyPlayerExitedArena()
    {
        playerInsideArena =
            false;
    }

    public bool ForcePhase(
        int phaseIndex)
    {
        if (requirePlayerInArena &&
            arenaTrigger == null)
        {
            Debug.LogError(
                $"{nameof(BossAI)} on '{name}' requires a child Collider named '{ArenaTriggerName}'.",
                this);

            return false;
        }

        if (phases == null ||
            phases.Length == 0)
        {
            return false;
        }

        int clampedIndex =
            Mathf.Clamp(
                phaseIndex,
                0,
                phases.Length - 1);

        if (clampedIndex ==
            currentPhaseIndex)
        {
            return false;
        }

        BeginPhaseTransition(
            clampedIndex);

        return true;
    }

    public bool ForceStagger()
    {
        if (!enableStagger ||
            IsDead ||
            bossEncounterState ==
                BossEncounterState.Staggered)
        {
            return false;
        }

        BeginStagger();

        return true;
    }

    public bool RestoreBossCheckpoint()
    {
        if (!enablePhaseCheckpoints ||
            checkpointPhaseIndex < 0 ||
            phases == null ||
            checkpointPhaseIndex >=
                phases.Length)
        {
            return false;
        }

        if (!ResetAI())
            return false;

        currentPhaseIndex =
            checkpointPhaseIndex;

        if (restoreHealthToCheckpointThreshold)
        {
            BossPhase checkpointPhase =
                phases[checkpointPhaseIndex];

            if (checkpointPhase != null)
            {
                int targetHealth =
                    Mathf.Clamp(
                        Mathf.CeilToInt(
                            MaximumHealth *
                            checkpointPhase.HealthThreshold),
                        1,
                        MaximumHealth);

                int healthDifference =
                    CurrentHealth -
                    targetHealth;

                if (healthDifference > 0)
                {
                    TakeDamage(
                        healthDifference);
                }
                else if (healthDifference < 0)
                {
                    Heal(
                        -healthDifference);
                }
            }
        }

        encounterStarted =
            true;

        enraged =
            HealthRatio <=
            enrageHealthThreshold;

        finalPhaseEntered =
            currentPhaseIndex ==
            phases.Length - 1;

        phaseTransitionPending =
            false;

        victoryPending =
            false;

        accumulatedStaggerDamage =
            0;

        ChangeBossState(
            finalPhaseEntered
                ? BossEncounterState.FinalPhase
                : enraged
                    ? BossEncounterState.Enraged
                    : BossEncounterState.Active);

        SetState(
            Target != null
                ? AIState.Chase
                : AIState.Idle);

        return true;
    }

    public void ResetBoss()
    {
        ResetAI();

        encounterStarted =
            false;

        enraged =
            false;

        finalPhaseEntered =
            false;

        phaseTransitionPending =
            false;

        victoryPending =
            false;

        playerInsideArena =
            false;

        currentPhaseIndex =
            0;

        if (!restoreCheckpointOnReset)
        {
            checkpointPhaseIndex =
                0;
        }

        accumulatedStaggerDamage =
            0;

        introTimer =
            0f;

        phaseTransitionTimer =
            0f;

        intermissionTimer =
            0f;

        staggerTimer =
            0f;

        staggerResetTimer =
            0f;

        victoryTimer =
            0f;

        ChangeBossState(
            BossEncounterState.Dormant);
    }

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        ResolveBossReferences();
        ConfigureBossComponents();
        InitializeBossRuntime();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (shuttingDownBoss)
            return;

        ResolveBossReferences();
        ConfigureBossComponents();
        SubscribeToBaseEvents();

        if (!initializedBoss)
        {
            InitializeBossRuntime();
        }
    }

    protected override void Update()
    {
        base.Update();

        if (!IsInitialized ||
            CurrentState ==
                AIState.Disabled)
        {
            return;
        }

        UpdateBossTimers();

        if (IsDead)
        {
            HandleBossDefeat();
            UpdateVictorySequence();
            return;
        }

        UpdateEncounterState();
        CheckPhaseProgression();
        CheckEnrage();
        CheckArenaBounds();

        safetyTimer -=
            Time.deltaTime;

        if (safetyTimer <= 0f)
        {
            safetyTimer =
                safetyCheckInterval;

            RunBossSafetyChecks();
        }
    }

    protected override void OnDisable()
    {
        UnsubscribeFromBaseEvents();

        ChangeBossState(
            BossEncounterState.Disabled);

        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        shuttingDownBoss =
            true;

        UnsubscribeFromBaseEvents();

        EncounterStarted = null;
        PhaseChanged = null;
        CheckpointReached = null;
        StaggerStarted = null;
        StaggerEnded = null;
        Enraged = null;
        FinalPhaseStarted = null;
        BossDefeated = null;
        EncounterCompleted = null;
        BossStateChanged = null;

        arenaTrigger = null;
        arenaCenter = null;
        bossAudioSource = null;
        phases = null;
        preservedTarget = null;

        base.OnDestroy();
    }

    private void OnTriggerEnter(
        Collider other)
    {
        if (!requirePlayerInArena ||
            other == null)
        {
            return;
        }

        UltimatePlayerMovement player =
            other.GetComponent<UltimatePlayerMovement>();

        player ??=
            other.GetComponentInParent<UltimatePlayerMovement>();

        if (player != null)
        {
            NotifyPlayerEnteredArena();
        }
    }

    private void OnTriggerExit(
        Collider other)
    {
        if (!requirePlayerInArena ||
            other == null)
        {
            return;
        }

        UltimatePlayerMovement player =
            other.GetComponent<UltimatePlayerMovement>();

        player ??=
            other.GetComponentInParent<UltimatePlayerMovement>();

        if (player != null)
        {
            NotifyPlayerExitedArena();
        }
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        maximumArenaRadius =
            Mathf.Max(
                0f,
                maximumArenaRadius);

        introDuration =
            Mathf.Max(
                0f,
                introDuration);

        phaseTransitionDuration =
            Mathf.Max(
                0f,
                phaseTransitionDuration);

        damageRequiredToStagger =
            Mathf.Max(
                1,
                damageRequiredToStagger);

        staggerDuration =
            Mathf.Max(
                0f,
                staggerDuration);

        staggerResetDelay =
            Mathf.Max(
                0f,
                staggerResetDelay);

        enrageHealthThreshold =
            Mathf.Clamp01(
                enrageHealthThreshold);

        arenaReturnSpeedMultiplier =
            Mathf.Max(
                0f,
                arenaReturnSpeedMultiplier);

        arenaArrivalDistance =
            Mathf.Max(
                0f,
                arenaArrivalDistance);

        arenaCheckInterval =
            Mathf.Max(
                0.1f,
                arenaCheckInterval);

        victoryDelay =
            Mathf.Max(
                0f,
                victoryDelay);

        safetyCheckInterval =
            Mathf.Max(
                0.1f,
                safetyCheckInterval);

        minimumValidScale =
            Mathf.Max(
                0.01f,
                minimumValidScale);

        ValidatePhases();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ResolveBossReferences();
            ConfigureBossComponents();
        }
#endif
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Transform center =
            arenaCenter != null
                ? arenaCenter
                : transform;

        if (maximumArenaRadius >
            0f)
        {
            Gizmos.DrawWireSphere(
                center.position,
                maximumArenaRadius);
        }
    }

    #endregion

    #region Initialization

    protected override bool Initialize()
    {
        bool initialized =
            base.Initialize();

        if (!initialized)
            return false;

        ResolveBossReferences();
        ConfigureBossComponents();
        InitializeBossRuntime();

        return initializedBoss;
    }

    private void ResolveBossReferences()
    {
        arenaCenter ??=
            FindChildByName(
                "Arena Center");

        arenaCenter ??=
            transform;

        arenaTrigger ??=
            FindChildColliderByName(
                ArenaTriggerName);

        bossAudioSource ??=
            GetComponent<AudioSource>();

        bossAudioSource ??=
            GetComponentInChildren<AudioSource>(
                includeInactive: true);
    }

    private void ConfigureBossComponents()
    {
        if (arenaTrigger != null)
        {
            arenaTrigger.isTrigger =
                true;
        }

        if (bossAudioSource != null)
        {
            bossAudioSource.playOnAwake =
                false;
        }
    }

    private void InitializeBossRuntime()
    {
        ValidatePhases();
        SortPhases();

        introTimer =
            0f;

        phaseTransitionTimer =
            0f;

        intermissionTimer =
            0f;

        staggerTimer =
            0f;

        staggerResetTimer =
            0f;

        arenaCheckTimer =
            arenaCheckInterval;

        victoryTimer =
            0f;

        safetyTimer =
            safetyCheckInterval;

        normalAgentSpeed =
            Agent != null
                ? Agent.speed
                : 0f;

        currentPhaseIndex =
            0;

        checkpointPhaseIndex =
            0;

        accumulatedStaggerDamage =
            0;

        playerInsideArena =
            !requirePlayerInArena;

        encounterStarted =
            false;

        enraged =
            false;

        finalPhaseEntered =
            false;

        phaseTransitionPending =
            false;

        victoryPending =
            false;

        initializedBoss =
            ValidateBossConfiguration();

        ChangeBossState(
            BossEncounterState.Dormant);

        SubscribeToBaseEvents();

        if (activateOnStart &&
            initializedBoss)
        {
            StartEncounter();
        }
    }

    #endregion

    #region Base Event Integration

    private void SubscribeToBaseEvents()
    {
        UnsubscribeFromBaseEvents();

        Damaged +=
            HandleBaseDamaged;

        Died +=
            HandleBaseDied;
    }

    private void UnsubscribeFromBaseEvents()
    {
        Damaged -=
            HandleBaseDamaged;

        Died -=
            HandleBaseDied;
    }

    private void HandleBaseDamaged(
        AIController controller,
        int damage)
    {
        if (controller != this ||
            damage <= 0 ||
            IsDead)
        {
            return;
        }

        if (enableStagger &&
            bossEncounterState !=
                BossEncounterState.Staggered &&
            bossEncounterState !=
                BossEncounterState.PhaseTransition &&
            bossEncounterState !=
                BossEncounterState.Intermission)
        {
            accumulatedStaggerDamage +=
                damage;

            staggerResetTimer =
                staggerResetDelay;

            if (accumulatedStaggerDamage >=
                damageRequiredToStagger)
            {
                BeginStagger();
            }
        }

        CheckPhaseProgression();
        CheckEnrage();
    }

    private void HandleBaseDied(
        AIController controller)
    {
        if (controller != this)
            return;

        HandleBossDefeat();
    }

    #endregion

    #region Encounter State

    private void UpdateEncounterState()
    {
        switch (bossEncounterState)
        {
            case BossEncounterState.Dormant:
                UpdateDormantState();
                break;

            case BossEncounterState.Intro:
                UpdateIntroState();
                break;

            case BossEncounterState.Active:
                UpdateActiveState();
                break;

            case BossEncounterState.PhaseTransition:
                UpdatePhaseTransitionState();
                break;

            case BossEncounterState.Intermission:
                UpdateIntermissionState();
                break;

            case BossEncounterState.Staggered:
                UpdateStaggerState();
                break;

            case BossEncounterState.Enraged:
                UpdateEnragedState();
                break;

            case BossEncounterState.FinalPhase:
                UpdateFinalPhaseState();
                break;

            case BossEncounterState.Defeated:
            case BossEncounterState.Completed:
            case BossEncounterState.Disabled:
                break;

            default:
                Debug.LogError(
                    $"Unhandled {nameof(BossEncounterState)} value '{bossEncounterState}'.",
                    this);
                break;
        }
    }

    private void UpdateDormantState()
    {
        StopAgent();

        if (activateOnStart ||
            playerInsideArena)
        {
            StartEncounter();
        }
    }

    private void UpdateIntroState()
    {
        if (lockMovementDuringIntro)
        {
            StopAgent();
        }

        introTimer =
            Mathf.Max(
                0f,
                introTimer -
                Time.deltaTime);

        if (introTimer > 0f)
            return;

        CompleteIntro();
    }

    private void CompleteIntro()
    {
        ChangeBossState(
            BossEncounterState.Active);

        RestorePreservedTarget();

        SetState(
            Target != null
                ? AIState.Chase
                : AIState.Idle);
    }

    private void UpdateActiveState()
    {
        if (!encounterStarted)
        {
            ChangeBossState(
                BossEncounterState.Dormant);
        }
    }

    private void UpdatePhaseTransitionState()
    {
        StopAgent();

        phaseTransitionTimer =
            Mathf.Max(
                0f,
                phaseTransitionTimer -
                Time.deltaTime);

        if (phaseTransitionTimer > 0f)
            return;

        CompletePhaseTransition();
    }

    private void UpdateIntermissionState()
    {
        if (lockMovementDuringIntermission)
        {
            StopAgent();
        }

        intermissionTimer =
            Mathf.Max(
                0f,
                intermissionTimer -
                Time.deltaTime);

        if (intermissionTimer > 0f)
            return;

        CompleteIntermission();
    }

    private void UpdateStaggerState()
    {
        StopAgent();

        staggerTimer =
            Mathf.Max(
                0f,
                staggerTimer -
                Time.deltaTime);

        if (staggerTimer > 0f)
            return;

        EndStagger();
    }

    private void UpdateEnragedState()
    {
        if (!enraged)
        {
            ChangeBossState(
                BossEncounterState.Active);
        }
    }

    private void UpdateFinalPhaseState()
    {
        if (!finalPhaseEntered)
        {
            ChangeBossState(
                enraged
                    ? BossEncounterState.Enraged
                    : BossEncounterState.Active);
        }
    }

    #endregion

    #region AI State Overrides

    protected override bool CanReceiveDamage()
    {
        if (!base.CanReceiveDamage())
            return false;

        if (invulnerableDuringPhaseTransition &&
            (bossEncounterState ==
                 BossEncounterState.PhaseTransition ||
             bossEncounterState ==
                 BossEncounterState.Intermission))
        {
            return false;
        }

        return
            bossEncounterState !=
                BossEncounterState.Intro &&
            bossEncounterState !=
                BossEncounterState.Defeated &&
            bossEncounterState !=
                BossEncounterState.Completed &&
            bossEncounterState !=
                BossEncounterState.Disabled;
    }

    protected override void UpdateIdleState()
    {
        if (!encounterStarted ||
            IsControlLocked())
        {
            StopAgent();
            return;
        }

        base.UpdateIdleState();
    }

    protected override void UpdatePatrolState()
    {
        if (!encounterStarted ||
            IsControlLocked())
        {
            StopAgent();
            return;
        }

        base.UpdatePatrolState();
    }

    protected override void UpdateChaseState()
    {
        if (!encounterStarted ||
            IsControlLocked())
        {
            StopAgent();
            return;
        }

        if (returnToArenaWhenOutside &&
            IsOutsideArena())
        {
            ReturnToArena();
            return;
        }

        base.UpdateChaseState();
    }

    protected override void UpdateAttackState()
    {
        if (!encounterStarted ||
            IsControlLocked())
        {
            StopAgent();
            return;
        }

        base.UpdateAttackState();
    }

    protected override void UpdateSpecialState()
    {
        if (IsControlLocked())
        {
            StopAgent();
            return;
        }

        BossPhase phase =
            CurrentPhase;

        if (phase == null ||
            !phase.EnableSpecialState)
        {
            SetState(
                Target != null
                    ? AIState.Chase
                    : AIState.Idle);

            return;
        }

        base.UpdateSpecialState();
    }

    protected override void UpdateReturningState()
    {
        if (returnToArenaWhenOutside &&
            IsOutsideArena())
        {
            ReturnToArena();
            return;
        }

        base.UpdateReturningState();
    }

    #endregion

    #region Phase Management

    private void CheckPhaseProgression()
    {
        if (!encounterStarted ||
            IsDead ||
            phases == null ||
            phases.Length == 0 ||
            phaseTransitionPending ||
            victoryPending)
        {
            return;
        }

        float healthRatio =
            HealthRatio;

        int targetPhaseIndex =
            currentPhaseIndex;

        for (int index = 0;
             index < phases.Length;
             index++)
        {
            BossPhase phase =
                phases[index];

            if (phase == null)
                continue;

            if (healthRatio <=
                phase.HealthThreshold)
            {
                targetPhaseIndex =
                    Mathf.Max(
                        targetPhaseIndex,
                        index);
            }
        }

        if (targetPhaseIndex >
            currentPhaseIndex)
        {
            BeginPhaseTransition(
                targetPhaseIndex);
        }
    }

    private void BeginPhaseTransition(
        int newPhaseIndex)
    {
        if (phases == null ||
            phases.Length == 0)
        {
            return;
        }

        int clampedIndex =
            Mathf.Clamp(
                newPhaseIndex,
                0,
                phases.Length - 1);

        phaseTransitionPending =
            true;

        currentPhaseIndex =
            clampedIndex;

        phaseTransitionTimer =
            phaseTransitionDuration;

        preservedTarget =
            Target;

        ChangeBossState(
            BossEncounterState.PhaseTransition);

        StopAgent();

        phaseTransitionEffect?.Play();

        PlayBossSound(
            phaseTransitionSound);

        BossPhase phase =
            CurrentPhase;

        phase?.PhaseEffect?.Play();

        PlayBossSound(
            phase?.PhaseVoice);

        if (phase?.PhaseMusic != null)
        {
            PlayBossMusic(
                phase.PhaseMusic);
        }

        if (enablePhaseCheckpoints &&
            phase != null &&
            phase.CheckpointPhase)
        {
            checkpointPhaseIndex =
                currentPhaseIndex;

            CheckpointReached?.Invoke(
                this,
                checkpointPhaseIndex);
        }

        PhaseChanged?.Invoke(
            this,
            currentPhaseIndex,
            phase);

        if (currentPhaseIndex ==
            phases.Length - 1 &&
            !finalPhaseEntered)
        {
            finalPhaseEntered =
                true;

            FinalPhaseStarted?.Invoke(
                this);
        }

        if (phaseTransitionDuration <=
            0f)
        {
            CompletePhaseTransition();
        }
    }

    private void CompletePhaseTransition()
    {
        phaseTransitionPending =
            false;

        BossPhase phase =
            CurrentPhase;

        if (phase != null &&
            phase.IntermissionDuration >
                0f)
        {
            BeginIntermission(
                phase.IntermissionDuration);

            return;
        }

        ResumeAfterPhaseChange();
    }

    private void BeginIntermission(
        float duration)
    {
        intermissionTimer =
            Mathf.Max(
                0f,
                duration);

        ChangeBossState(
            BossEncounterState.Intermission);

        intermissionEffect?.Play();

        PlayBossSound(
            intermissionSound);

        if (intermissionTimer <= 0f)
        {
            CompleteIntermission();
        }
    }

    private void CompleteIntermission()
    {
        if (restoreTargetAfterIntermission)
        {
            RestorePreservedTarget();
        }

        ResumeAfterPhaseChange();
    }

    private void ResumeAfterPhaseChange()
    {
        ChangeBossState(
            finalPhaseEntered
                ? BossEncounterState.FinalPhase
                : enraged
                    ? BossEncounterState.Enraged
                    : BossEncounterState.Active);

        RestorePreservedTarget();

        SetState(
            Target != null
                ? AIState.Chase
                : AIState.Idle);
    }

    #endregion

    #region Stagger

    private void BeginStagger()
    {
        accumulatedStaggerDamage =
            0;

        staggerResetTimer =
            0f;

        staggerTimer =
            staggerDuration;

        preservedTarget =
            Target;

        ChangeBossState(
            BossEncounterState.Staggered);

        StopAgent();

        staggerEffect?.Play();

        PlayBossSound(
            staggerSound);

        StaggerStarted?.Invoke(
            this);

        if (staggerDuration <=
            0f)
        {
            EndStagger();
        }
    }

    private void EndStagger()
    {
        staggerTimer =
            0f;

        ChangeBossState(
            finalPhaseEntered
                ? BossEncounterState.FinalPhase
                : enraged
                    ? BossEncounterState.Enraged
                    : BossEncounterState.Active);

        RestorePreservedTarget();

        SetState(
            Target != null
                ? AIState.Chase
                : AIState.Idle);

        StaggerEnded?.Invoke(
            this);
    }

    #endregion

    #region Enrage

    private void CheckEnrage()
    {
        if (!enableEnrage ||
            enraged ||
            IsDead ||
            HealthRatio >
                enrageHealthThreshold)
        {
            return;
        }

        enraged =
            true;

        ChangeBossState(
            finalPhaseEntered
                ? BossEncounterState.FinalPhase
                : BossEncounterState.Enraged);

        enrageEffect?.Play();

        PlayBossSound(
            enrageSound);

        Enraged?.Invoke(
            this);
    }

    #endregion

    #region Arena

    private void CheckArenaBounds()
    {
        if (!encounterStarted ||
            !returnToArenaWhenOutside)
        {
            return;
        }

        arenaCheckTimer =
            Mathf.Max(
                0f,
                arenaCheckTimer -
                Time.deltaTime);

        if (arenaCheckTimer > 0f)
            return;

        arenaCheckTimer =
            arenaCheckInterval;

        if (IsOutsideArena())
        {
            ReturnToArena();
        }
    }

    private bool IsOutsideArena()
    {
        if (maximumArenaRadius <=
            0f)
        {
            return false;
        }

        Vector3 center =
            arenaCenter != null
                ? arenaCenter.position
                : HomePosition;

        float sqrDistance =
            (transform.position -
             center)
            .sqrMagnitude;

        return
            float.IsFinite(
                sqrDistance) &&
            sqrDistance >
                maximumArenaRadius *
                maximumArenaRadius;
    }

    private void ReturnToArena()
    {
        Vector3 center =
            arenaCenter != null
                ? arenaCenter.position
                : HomePosition;

        if (Agent != null &&
            Agent.enabled)
        {
            Agent.speed =
                normalAgentSpeed *
                arenaReturnSpeedMultiplier;
        }

        MoveAgentTo(
            center);

        if (!HasReachedArenaCenter(
                center))
        {
            return;
        }

        StopAgent();
        RestoreNormalAgentSpeed();

        SetState(
            Target != null
                ? AIState.Chase
                : AIState.Idle);
    }

    private void RestoreNormalAgentSpeed()
    {
        if (Agent != null &&
            Agent.enabled)
        {
            Agent.speed =
                normalAgentSpeed;
        }
    }

    private bool HasReachedArenaCenter(
        Vector3 center)
    {
        float distance =
            Vector3.Distance(
                transform.position,
                center);

        return
            float.IsFinite(
                distance) &&
            distance <=
                arenaArrivalDistance;
    }

    #endregion

    #region Timers

    private void UpdateBossTimers()
    {
        if (staggerResetTimer > 0f)
        {
            staggerResetTimer =
                Mathf.Max(
                    0f,
                    staggerResetTimer -
                    Time.deltaTime);

            if (staggerResetTimer <= 0f)
            {
                accumulatedStaggerDamage =
                    0;
            }
        }
    }

    #endregion

    #region Defeat And Victory

    private void HandleBossDefeat()
    {
        if (bossEncounterState ==
                BossEncounterState.Defeated ||
            bossEncounterState ==
                BossEncounterState.Completed)
        {
            return;
        }

        encounterStarted =
            false;

        phaseTransitionPending =
            false;

        StopAgent();
        RestoreNormalAgentSpeed();

        ChangeBossState(
            BossEncounterState.Defeated);

        defeatEffect?.Play();

        PlayBossSound(
            defeatSound);

        PlayBossMusic(
            victoryMusic);

        BossDefeated?.Invoke(
            this);

        victoryPending =
            true;

        victoryTimer =
            victoryDelay;

        if (victoryDelay <=
            0f)
        {
            CompleteVictory();
        }
    }

    private void UpdateVictorySequence()
    {
        if (!victoryPending)
            return;

        victoryTimer =
            Mathf.Max(
                0f,
                victoryTimer -
                Time.deltaTime);

        if (victoryTimer > 0f)
            return;

        CompleteVictory();
    }

    private void CompleteVictory()
    {
        if (!victoryPending &&
            bossEncounterState ==
                BossEncounterState.Completed)
        {
            return;
        }

        victoryPending =
            false;

        victoryEffect?.Play();

        ChangeBossState(
            BossEncounterState.Completed);

        EncounterCompleted?.Invoke(
            this);

        if (disableAfterVictory)
        {
            enabled =
                false;
        }
    }

    #endregion

    #region Runtime Safety

    protected override bool RunRuntimeSafetyChecks()
    {
        if (!base.RunRuntimeSafetyChecks())
            return false;

        return RunBossSafetyChecks();
    }

    private bool RunBossSafetyChecks()
    {
        if (!ValidateBossReferences())
        {
            ResolveBossReferences();
            ConfigureBossComponents();

            if (!ValidateBossReferences())
            {
                EnterBossSafetyShutdown(
                    "Required boss references could not be restored.");

                return false;
            }
        }

        if (!ValidateTransformState())
        {
            EnterBossSafetyShutdown(
                "Boss Transform contains invalid values.");

            return false;
        }

        if (restoreDisabledComponents &&
            bossAudioSource != null &&
            !bossAudioSource.enabled)
        {
            bossAudioSource.enabled =
                true;
        }

        return true;
    }

    private bool ValidateBossReferences()
    {
        return
            arenaCenter != null;
    }

    private bool ValidateTransformState()
    {
        Vector3 scale =
            transform.lossyScale;

        return
            IsFiniteVector(
                transform.position) &&
            IsFiniteQuaternion(
                transform.rotation) &&
            IsFiniteVector(
                scale) &&
            Mathf.Abs(
                scale.x) >=
                minimumValidScale &&
            Mathf.Abs(
                scale.y) >=
                minimumValidScale &&
            Mathf.Abs(
                scale.z) >=
                minimumValidScale;
    }

    private void EnterBossSafetyShutdown(
        string reason)
    {
        initializedBoss =
            false;

        encounterStarted =
            false;

        victoryPending =
            false;

        StopAgent();
        RestoreNormalAgentSpeed();

        ChangeBossState(
            BossEncounterState.Disabled);

        Debug.LogError(
            $"{nameof(BossAI)} entered safety shutdown on '{name}': {reason}",
            this);

        enabled =
            false;
    }

    #endregion

    #region Validation

    private bool ValidateBossConfiguration()
    {
        if (arenaCenter == null)
        {
            Debug.LogError(
                $"{nameof(BossAI)} on '{name}' requires an arena center.",
                this);

            return false;
        }

        if (phases == null ||
            phases.Length == 0)
        {
            Debug.LogWarning(
                $"{nameof(BossAI)} on '{name}' has no configured phases.",
                this);
        }

        return true;
    }

    private void ValidatePhases()
    {
        if (phases == null)
        {
            phases =
                Array.Empty<BossPhase>();

            return;
        }

        if (phases.Length >
            MaximumPhaseCount)
        {
            Array.Resize(
                ref phases,
                MaximumPhaseCount);
        }

        foreach (BossPhase phase
                 in phases)
        {
            phase?.Validate();
        }
    }

    private void SortPhases()
    {
        if (phases == null ||
            phases.Length <= 1)
        {
            return;
        }

        Array.Sort(
            phases,
            (left, right) =>
            {
                if (left == null &&
                    right == null)
                {
                    return 0;
                }

                if (left == null)
                    return 1;

                if (right == null)
                    return -1;

                return right.HealthThreshold.CompareTo(
                    left.HealthThreshold);
            });
    }

    #endregion

    #region State

    private void ChangeBossState(
        BossEncounterState newState)
    {
        if (!Enum.IsDefined(
                typeof(BossEncounterState),
                newState))
        {
            return;
        }

        if (bossEncounterState ==
            newState)
        {
            return;
        }

        bossEncounterState =
            newState;

        BossStateChanged?.Invoke(
            this,
            bossEncounterState);

        if (logBossState)
        {
            Debug.Log(
                $"{nameof(BossAI)} on '{name}' changed to {bossEncounterState}.",
                this);
        }
    }

    private bool IsControlLocked()
    {
        return
            bossEncounterState ==
                BossEncounterState.Intro ||
            bossEncounterState ==
                BossEncounterState.PhaseTransition ||
            bossEncounterState ==
                BossEncounterState.Intermission ||
            bossEncounterState ==
                BossEncounterState.Staggered ||
            bossEncounterState ==
                BossEncounterState.Defeated ||
            bossEncounterState ==
                BossEncounterState.Completed ||
            bossEncounterState ==
                BossEncounterState.Disabled;
    }

    #endregion

    #region Helpers

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
            if (child != null &&
                string.Equals(
                    child.name,
                    targetName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private Collider FindChildColliderByName(
        string targetName)
    {
        Transform child =
            FindChildByName(
                targetName);

        if (child == null)
            return null;

        Collider resolvedCollider =
            child.GetComponent<Collider>();

        resolvedCollider ??=
            child.GetComponentInChildren<Collider>(
                includeInactive: true);

        return resolvedCollider;
    }

    private void RestorePreservedTarget()
    {
        if (Target != null ||
            preservedTarget == null ||
            !preservedTarget.gameObject.activeInHierarchy)
        {
            return;
        }

        SetTarget(
            preservedTarget);
    }

    private void PlayBossSound(
        AudioClip clip)
    {
        if (bossAudioSource == null ||
            clip == null)
        {
            return;
        }

        bossAudioSource.PlayOneShot(
            clip);
    }

    private void PlayBossMusic(
        AudioClip clip)
    {
        if (bossAudioSource == null ||
            clip == null)
        {
            return;
        }

        bossAudioSource.clip =
            clip;

        bossAudioSource.loop =
            true;

        bossAudioSource.Play();
    }
    #endregion
}
