using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MiniBossAI : AIController
{
    #region Types

    public enum MiniBossState
    {
        Dormant,
        Intro,
        Active,
        PhaseTransition,
        Staggered,
        Enraged,
        Defeated,
        Disabled
    }

    [Serializable]
    public sealed class MiniBossPhase
    {
        [SerializeField] private string phaseName = "Phase";
        [SerializeField, Range(0.01f, 1f)] private float healthThreshold = 1f;
        [SerializeField] private bool enableSpecialState;
        [SerializeField] private AudioClip phaseMusic;
        [SerializeField] private ParticleSystem phaseEffect;

        public string PhaseName =>
            phaseName;

        public float HealthThreshold =>
            healthThreshold;

        public bool EnableSpecialState =>
            enableSpecialState;

        public AudioClip PhaseMusic =>
            phaseMusic;

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

        }
    }

    #endregion

    #region Constants

    private const int MaximumPhaseCount =
        10;

    private const string ArenaTriggerName =
        "Arena Trigger";

    #endregion

    #region Inspector

    [Header("Encounter")]
    [SerializeField] private bool activateOnStart;
    [SerializeField] private bool requirePlayerInArena = true;
    [SerializeField] private Collider arenaTrigger;
    [SerializeField] private Transform arenaCenter;
    [SerializeField, Min(0f)] private float maximumArenaRadius = 35f;
    [SerializeField] private bool returnToArenaWhenOutside = true;

    [Header("Introduction")]
    [SerializeField, Min(0f)] private float introDuration = 1.5f;
    [SerializeField] private bool lockMovementDuringIntro = true;
    [SerializeField] private AudioClip introSound;
    [SerializeField] private ParticleSystem introEffect;

    [Header("Phases")]
    [SerializeField]
    private MiniBossPhase[] phases =
        Array.Empty<MiniBossPhase>();

    [SerializeField, Min(0f)] private float phaseTransitionDuration = 1f;
    [SerializeField] private bool invulnerableDuringTransition = true;
    [SerializeField] private AudioClip phaseTransitionSound;
    [SerializeField] private ParticleSystem phaseTransitionEffect;

    [Header("Stagger")]
    [SerializeField] private bool enableStagger = true;
    [SerializeField, Min(1)] private int damageRequiredToStagger = 3;
    [SerializeField, Min(0f)] private float staggerDuration = 2f;
    [SerializeField, Min(0f)] private float staggerResetDelay = 3f;
    [SerializeField] private AudioClip staggerSound;
    [SerializeField] private ParticleSystem staggerEffect;

    [Header("Enrage")]
    [SerializeField] private bool enableEnrage = true;
    [SerializeField, Range(0f, 1f)] private float enrageHealthThreshold = 0.25f;
    [SerializeField] private AudioClip enrageSound;
    [SerializeField] private ParticleSystem enrageEffect;

    [Header("Arena Recovery")]
    [SerializeField, Min(0f)] private float arenaReturnSpeedMultiplier = 1.5f;
    [SerializeField, Min(0f)] private float arenaArrivalDistance = 1.5f;
    [SerializeField, Min(0.1f)] private float arenaCheckInterval = 0.5f;

    [Header("Presentation")]
    [SerializeField] private AudioSource encounterAudioSource;
    [SerializeField] private AudioClip defeatSound;
    [SerializeField] private ParticleSystem defeatEffect;

    [Header("Runtime Safety")]
    [SerializeField] private bool restoreDisabledComponents = true;
    [SerializeField, Min(0.1f)] private float safetyCheckInterval = 0.5f;
    [SerializeField, Min(0.01f)] private float minimumValidScale = 0.01f;

    [Header("Debug")]
    [SerializeField]
    private MiniBossState miniBossState =
        MiniBossState.Dormant;

    [SerializeField] private bool logMiniBossState;

    #endregion

    #region Runtime State

    private float introTimer;
    private float phaseTransitionTimer;
    private float staggerTimer;
    private float staggerResetTimer;
    private float arenaCheckTimer;
    private float safetyTimer;
    private float normalAgentSpeed;

    private int currentPhaseIndex;
    private int accumulatedStaggerDamage;

    private bool playerInsideArena;
    private bool encounterStarted;
    private bool enraged;
    private bool initializedMiniBoss;
    private bool shuttingDownMiniBoss;
    private bool phaseTransitionPending;

    #endregion

    #region Events

    public event Action<MiniBossAI> EncounterStarted;
    public event Action<MiniBossAI, int, MiniBossPhase> PhaseChanged;
    public event Action<MiniBossAI> StaggerStarted;
    public event Action<MiniBossAI> StaggerEnded;
    public event Action<MiniBossAI> Enraged;
    public event Action<MiniBossAI> EncounterCompleted;
    public event Action<MiniBossAI, MiniBossState> MiniBossStateChanged;

    #endregion

    #region Public API

    public MiniBossState CurrentMiniBossState =>
        miniBossState;

    public int CurrentPhaseIndex =>
        currentPhaseIndex;

    public MiniBossPhase CurrentPhase =>
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

    public bool IsPhaseTransitioning =>
        miniBossState ==
            MiniBossState.PhaseTransition;

    public float HealthRatio =>
        MaximumHealth > 0
            ? Mathf.Clamp01(
                (float)CurrentHealth /
                MaximumHealth)
            : 0f;

    public bool StartEncounter()
    {
        if (!initializedMiniBoss ||
            encounterStarted ||
            IsDead)
        {
            return false;
        }

        encounterStarted =
            true;

        introTimer =
            introDuration;

        ChangeMiniBossState(
            introDuration > 0f
                ? MiniBossState.Intro
                : MiniBossState.Active);

        if (lockMovementDuringIntro)
        {
            StopAgent();
        }

        introEffect?.Play();

        PlayEncounterSound(
            introSound);

        EncounterStarted?.Invoke(
            this);

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
                $"{nameof(MiniBossAI)} on '{name}' requires a child Collider named '{ArenaTriggerName}'.",
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
            miniBossState ==
                MiniBossState.Staggered)
        {
            return false;
        }

        BeginStagger();

        return true;
    }

    public void ResetMiniBoss()
    {
        ResetAI();

        encounterStarted =
            false;

        enraged =
            false;

        phaseTransitionPending =
            false;

        playerInsideArena =
            false;

        currentPhaseIndex =
            0;

        accumulatedStaggerDamage =
            0;

        introTimer =
            0f;

        phaseTransitionTimer =
            0f;

        staggerTimer =
            0f;

        staggerResetTimer =
            0f;

        ChangeMiniBossState(
            MiniBossState.Dormant);
    }

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        ResolveMiniBossReferences();
        ConfigureMiniBossComponents();
        InitializeMiniBossRuntime();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (shuttingDownMiniBoss)
            return;

        ResolveMiniBossReferences();
        ConfigureMiniBossComponents();
        SubscribeToBaseEvents();

        if (!initializedMiniBoss)
        {
            InitializeMiniBossRuntime();
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

        UpdateMiniBossTimers();

        if (IsDead)
        {
            HandleDefeat();
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

            RunMiniBossSafetyChecks();
        }
    }

    protected override void OnDisable()
    {
        UnsubscribeFromBaseEvents();

        ChangeMiniBossState(
            MiniBossState.Disabled);

        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        shuttingDownMiniBoss =
            true;

        UnsubscribeFromBaseEvents();

        EncounterStarted = null;
        PhaseChanged = null;
        StaggerStarted = null;
        StaggerEnded = null;
        Enraged = null;
        EncounterCompleted = null;
        MiniBossStateChanged = null;

        arenaTrigger = null;
        arenaCenter = null;
        encounterAudioSource = null;
        phases = null;

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
            ResolveMiniBossReferences();
            ConfigureMiniBossComponents();
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

        ResolveMiniBossReferences();
        ConfigureMiniBossComponents();
        InitializeMiniBossRuntime();

        return initializedMiniBoss;
    }

    private void ResolveMiniBossReferences()
    {
        arenaCenter ??=
            FindChildByName(
                "Arena Center");

        arenaCenter ??=
            transform;

        arenaTrigger ??=
            FindChildColliderByName(
                ArenaTriggerName);

        encounterAudioSource ??=
            GetComponent<AudioSource>();

        encounterAudioSource ??=
            GetComponentInChildren<AudioSource>(
                includeInactive: true);
    }

    private void ConfigureMiniBossComponents()
    {
        if (arenaTrigger != null)
        {
            arenaTrigger.isTrigger =
                true;
        }

        if (encounterAudioSource != null)
        {
            encounterAudioSource.playOnAwake =
                false;
        }
    }

    private void InitializeMiniBossRuntime()
    {
        ValidatePhases();
        SortPhases();

        introTimer =
            0f;

        phaseTransitionTimer =
            0f;

        staggerTimer =
            0f;

        staggerResetTimer =
            0f;

        arenaCheckTimer =
            arenaCheckInterval;

        safetyTimer =
            safetyCheckInterval;

        normalAgentSpeed =
            Agent != null
                ? Agent.speed
                : 0f;

        currentPhaseIndex =
            0;

        accumulatedStaggerDamage =
            0;

        playerInsideArena =
            !requirePlayerInArena;

        encounterStarted =
            false;

        enraged =
            false;

        phaseTransitionPending =
            false;

        initializedMiniBoss =
            ValidateMiniBossConfiguration();

        ChangeMiniBossState(
            MiniBossState.Dormant);

        SubscribeToBaseEvents();

        if (activateOnStart &&
            initializedMiniBoss)
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
            miniBossState !=
                MiniBossState.Staggered &&
            miniBossState !=
                MiniBossState.PhaseTransition)
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

        HandleDefeat();
    }

    #endregion

    #region Encounter State

    private void UpdateEncounterState()
    {
        switch (miniBossState)
        {
            case MiniBossState.Dormant:
                UpdateDormantState();
                break;

            case MiniBossState.Intro:
                UpdateIntroState();
                break;

            case MiniBossState.Active:
                UpdateActiveState();
                break;

            case MiniBossState.PhaseTransition:
                UpdatePhaseTransitionState();
                break;

            case MiniBossState.Staggered:
                UpdateStaggerState();
                break;

            case MiniBossState.Enraged:
                UpdateEnragedState();
                break;

            case MiniBossState.Defeated:
            case MiniBossState.Disabled:
                break;

            default:
                Debug.LogError(
                    $"Unhandled {nameof(MiniBossState)} value '{miniBossState}'.",
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

        ChangeMiniBossState(
            MiniBossState.Active);

        SetState(
            Target != null
                ? AIState.Chase
                : AIState.Idle);
    }

    private void UpdateActiveState()
    {
        if (!encounterStarted)
        {
            ChangeMiniBossState(
                MiniBossState.Dormant);
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
            ChangeMiniBossState(
                MiniBossState.Active);
        }
    }

    #endregion

    #region AI State Overrides

    protected override bool CanReceiveDamage()
    {
        if (!base.CanReceiveDamage())
            return false;

        if (invulnerableDuringTransition &&
            miniBossState ==
                MiniBossState.PhaseTransition)
        {
            return false;
        }

        return
            miniBossState !=
                MiniBossState.Intro &&
            miniBossState !=
                MiniBossState.Defeated &&
            miniBossState !=
                MiniBossState.Disabled;
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

        MiniBossPhase phase =
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
            phaseTransitionPending)
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
            MiniBossPhase phase =
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

        ChangeMiniBossState(
            MiniBossState.PhaseTransition);

        StopAgent();

        phaseTransitionEffect?.Play();

        PlayEncounterSound(
            phaseTransitionSound);

        MiniBossPhase phase =
            CurrentPhase;

        phase?.PhaseEffect?.Play();

        if (phase != null &&
            phase.PhaseMusic != null &&
            encounterAudioSource != null)
        {
            encounterAudioSource.clip =
                phase.PhaseMusic;

            encounterAudioSource.loop =
                true;

            encounterAudioSource.Play();
        }

        PhaseChanged?.Invoke(
            this,
            currentPhaseIndex,
            phase);

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

        ChangeMiniBossState(
            enraged
                ? MiniBossState.Enraged
                : MiniBossState.Active);

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

        ChangeMiniBossState(
            MiniBossState.Staggered);

        StopAgent();

        staggerEffect?.Play();

        PlayEncounterSound(
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

        ChangeMiniBossState(
            enraged
                ? MiniBossState.Enraged
                : MiniBossState.Active);

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

        ChangeMiniBossState(
            MiniBossState.Enraged);

        enrageEffect?.Play();

        PlayEncounterSound(
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

    private void UpdateMiniBossTimers()
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

    #region Defeat

    private void HandleDefeat()
    {
        if (miniBossState ==
            MiniBossState.Defeated)
        {
            return;
        }

        encounterStarted =
            false;

        phaseTransitionPending =
            false;

        StopAgent();
        RestoreNormalAgentSpeed();

        ChangeMiniBossState(
            MiniBossState.Defeated);

        defeatEffect?.Play();

        PlayEncounterSound(
            defeatSound);

        EncounterCompleted?.Invoke(
            this);
    }

    #endregion

    #region Runtime Safety

    protected override bool RunRuntimeSafetyChecks()
    {
        if (!base.RunRuntimeSafetyChecks())
            return false;

        return RunMiniBossSafetyChecks();
    }

    private bool RunMiniBossSafetyChecks()
    {
        if (!ValidateMiniBossReferences())
        {
            ResolveMiniBossReferences();
            ConfigureMiniBossComponents();

            if (!ValidateMiniBossReferences())
            {
                EnterMiniBossSafetyShutdown(
                    "Required mini-boss references could not be restored.");

                return false;
            }
        }

        if (!ValidateTransformState())
        {
            EnterMiniBossSafetyShutdown(
                "Mini-boss Transform contains invalid values.");

            return false;
        }

        if (restoreDisabledComponents &&
            encounterAudioSource != null &&
            !encounterAudioSource.enabled)
        {
            encounterAudioSource.enabled =
                true;
        }

        return true;
    }

    private bool ValidateMiniBossReferences()
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

    private void EnterMiniBossSafetyShutdown(
        string reason)
    {
        initializedMiniBoss =
            false;

        encounterStarted =
            false;

        StopAgent();
        RestoreNormalAgentSpeed();

        ChangeMiniBossState(
            MiniBossState.Disabled);

        Debug.LogError(
            $"{nameof(MiniBossAI)} entered safety shutdown on '{name}': {reason}",
            this);

        enabled =
            false;
    }

    #endregion

    #region Validation

    private bool ValidateMiniBossConfiguration()
    {
        if (arenaCenter == null)
        {
            Debug.LogError(
                $"{nameof(MiniBossAI)} on '{name}' requires an arena center.",
                this);

            return false;
        }

        if (phases == null ||
            phases.Length == 0)
        {
            Debug.LogWarning(
                $"{nameof(MiniBossAI)} on '{name}' has no configured phases.",
                this);
        }

        return true;
    }

    private void ValidatePhases()
    {
        if (phases == null)
        {
            phases =
                Array.Empty<MiniBossPhase>();

            return;
        }

        if (phases.Length >
            MaximumPhaseCount)
        {
            Array.Resize(
                ref phases,
                MaximumPhaseCount);
        }

        foreach (MiniBossPhase phase
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

    private void ChangeMiniBossState(
        MiniBossState newState)
    {
        if (!Enum.IsDefined(
                typeof(MiniBossState),
                newState))
        {
            return;
        }

        if (miniBossState ==
            newState)
        {
            return;
        }

        miniBossState =
            newState;

        MiniBossStateChanged?.Invoke(
            this,
            miniBossState);

        if (logMiniBossState)
        {
            Debug.Log(
                $"{nameof(MiniBossAI)} on '{name}' changed to {miniBossState}.",
                this);
        }
    }

    private bool IsControlLocked()
    {
        return
            miniBossState ==
                MiniBossState.Intro ||
            miniBossState ==
                MiniBossState.PhaseTransition ||
            miniBossState ==
                MiniBossState.Staggered ||
            miniBossState ==
                MiniBossState.Defeated ||
            miniBossState ==
                MiniBossState.Disabled;
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

    private void PlayEncounterSound(
        AudioClip clip)
    {
        if (encounterAudioSource == null ||
            clip == null)
        {
            return;
        }

        encounterAudioSource.PlayOneShot(
            clip);
    }
    #endregion
}
