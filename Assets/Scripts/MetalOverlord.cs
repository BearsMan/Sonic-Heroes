using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class MetalOverlord : MonoBehaviour
{
    #region Types

    public enum BossState
    {
        Intro,
        Flying,
        Attacking,
        ChaosControl,
        Defeated
    }

    #endregion

    #region Animator Hashes

    private static readonly int ShootCrystalsHash =
        Animator.StringToHash(
            "ShootCrystals");

    private static readonly int LaunchMissilesHash =
        Animator.StringToHash(
            "LaunchMissiles");

    private static readonly int DiveForShipHash =
        Animator.StringToHash(
            "DiveForShip");

    private static readonly int ThrowShipHash =
        Animator.StringToHash(
            "ThrowShip");

    private static readonly int ChaosControlHash =
        Animator.StringToHash(
            "Chaos Control!");

    private static readonly int TakeHitHash =
        Animator.StringToHash(
            "TakeHit");

    private static readonly int DefeatedHash =
        Animator.StringToHash(
            "Defeated");

    #endregion

    #region Inspector

    [Header("References")]
    [SerializeField]
    private Animator bossAnimator;

    [SerializeField]
    private Transform player;

    [SerializeField]
    private GameObject crystalPillarPrefab;

    [SerializeField]
    private GameObject missilePrefab;

    [SerializeField]
    private GameObject eggFleetShipPrefab;

    [SerializeField]
    private GameObject ringBalloonPrefab;

    [Header("Boss")]
    [SerializeField]
    private BossState startingState =
        BossState.Flying;

    [SerializeField, Min(1)]
    private int teamBlastHitsRequired = 5;

    [Header("Ring Countdown")]
    [SerializeField, Min(0f)]
    private float startingRings = 50f;

    [SerializeField, Min(0f)]
    private float maximumRings = 50f;

    [SerializeField, Min(0f)]
    private float ringDrainRate = 1f;

    [Header("Attack Timers")]
    [SerializeField, Min(0f)]
    private float crystalAttackDelay = 4f;

    [SerializeField, Min(0f)]
    private float missileAttackDelay = 7f;

    [SerializeField, Min(0f)]
    private float shipAttackDelay = 12f;

    [SerializeField, Min(0f)]
    private float chaosControlDelay = 20f;

    [Header("Attack Settings")]
    [SerializeField, Min(1)]
    private int minimumCrystalCount = 3;

    [SerializeField, Min(1)]
    private int maximumCrystalCount = 6;

    [SerializeField, Min(0f)]
    private float crystalSpawnInterval = 0.4f;

    [SerializeField, Min(1)]
    private int minimumMissileCount = 2;

    [SerializeField, Min(1)]
    private int maximumMissileCount = 4;

    [SerializeField, Min(0f)]
    private float missileSpawnInterval = 0.6f;

    [SerializeField, Min(0f)]
    private float shipGrabDelay = 1.5f;

    [SerializeField, Min(0f)]
    private float shipSpawnOffset = 10f;

    [Header("Chaos Control")]
    [SerializeField, Min(0f)]
    private float chaosControlDuration = 20f;

    [SerializeField, Min(0f)]
    private float chaosControlInputMultiplier = 2f;

    [Header("Ring Balloons")]
    [SerializeField, Min(0.01f)]
    private float ringBalloonSpawnInterval = 8f;

    [SerializeField, Min(0f)]
    private float ringBalloonSpawnRadius = 15f;

    #endregion

    #region Runtime State

    [SerializeField]
    private BossState currentState =
        BossState.Intro;

    [SerializeField]
    private float currentRings;

    private int teamBlastHits;

    private float crystalTimer;
    private float missileTimer;
    private float shipTimer;
    private float chaosTimer;

    private bool defeated;
    private bool isInitialized;
    private bool isShuttingDown;

    private Coroutine ringBalloonRoutine;
    private Coroutine chaosControlRoutine;

    private WaitForSeconds crystalSpawnWait;
    private WaitForSeconds missileSpawnWait;
    private WaitForSeconds shipGrabWait;
    private WaitForSeconds ringBalloonSpawnWait;

    #endregion

    #region Public API

    public BossState CurrentState =>
        currentState;

    public float CurrentRings =>
        currentRings;

    public bool IsDefeated =>
        defeated;

    public bool IsInitialized =>
        isInitialized;

    public void AddRings(
        int amount)
    {
        if (!isInitialized ||
            defeated ||
            amount <= 0)
        {
            return;
        }

        currentRings =
            Mathf.Clamp(
                currentRings + amount,
                0f,
                maximumRings);
    }

    private void RefreshWaitInstructions()
    {
        crystalSpawnWait =
            new WaitForSeconds(
                crystalSpawnInterval);

        missileSpawnWait =
            new WaitForSeconds(
                missileSpawnInterval);

        shipGrabWait =
            new WaitForSeconds(
                shipGrabDelay);

        ringBalloonSpawnWait =
            new WaitForSeconds(
                ringBalloonSpawnInterval);
    }

    public void OnTeamBlastHit()
    {
        if (!isInitialized ||
            defeated)
        {
            return;
        }

        teamBlastHits++;

        TriggerAnimator(
            TakeHitHash);

        if (teamBlastHits <
            teamBlastHitsRequired)
        {
            return;
        }

        DefeatBoss();
    }

    public void OnNormalAttackHit()
    {
        if (!isInitialized ||
            defeated)
        {
            return;
        }

        Debug.Log(
            "Metal Overlord is immune.",
            this);
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveDependencies();
    }

    private void Start()
    {
        if (!InitializeBoss())
        {
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (isShuttingDown)
            return;

        ResolveDependencies();

        if (!isInitialized)
            return;

        RestoreRuntimeState();
    }

    private void Update()
    {
        if (!CanUpdateBoss())
            return;

        UpdateRingCountdown();

        if (currentState ==
                BossState.Flying ||
            currentState ==
                BossState.Attacking)
        {
            UpdateAttackTimers();
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
        teamBlastHitsRequired =
            Mathf.Max(
                1,
                teamBlastHitsRequired);

        startingRings =
            Mathf.Max(
                0f,
                startingRings);

        maximumRings =
            Mathf.Max(
                startingRings,
                maximumRings);

        ringDrainRate =
            Mathf.Max(
                0f,
                ringDrainRate);

        crystalAttackDelay =
            Mathf.Max(
                0f,
                crystalAttackDelay);

        missileAttackDelay =
            Mathf.Max(
                0f,
                missileAttackDelay);

        shipAttackDelay =
            Mathf.Max(
                0f,
                shipAttackDelay);

        chaosControlDelay =
            Mathf.Max(
                0f,
                chaosControlDelay);

        minimumCrystalCount =
            Mathf.Max(
                1,
                minimumCrystalCount);

        maximumCrystalCount =
            Mathf.Max(
                minimumCrystalCount,
                maximumCrystalCount);

        crystalSpawnInterval =
            Mathf.Max(
                0f,
                crystalSpawnInterval);

        minimumMissileCount =
            Mathf.Max(
                1,
                minimumMissileCount);

        maximumMissileCount =
            Mathf.Max(
                minimumMissileCount,
                maximumMissileCount);

        missileSpawnInterval =
            Mathf.Max(
                0f,
                missileSpawnInterval);

        shipGrabDelay =
            Mathf.Max(
                0f,
                shipGrabDelay);

        shipSpawnOffset =
            Mathf.Max(
                0f,
                shipSpawnOffset);

        chaosControlDuration =
            Mathf.Max(
                0f,
                chaosControlDuration);

        chaosControlInputMultiplier =
            Mathf.Max(
                0f,
                chaosControlInputMultiplier);

        ringBalloonSpawnInterval =
            Mathf.Max(
                0.01f,
                ringBalloonSpawnInterval);

        ringBalloonSpawnRadius =
            Mathf.Max(
                0f,
                ringBalloonSpawnRadius);

        if (!System.Enum.IsDefined(
                typeof(BossState),
                startingState))
        {
            startingState =
                BossState.Flying;
        }
    }

    #endregion

    #region Initialization

    private bool InitializeBoss()
    {
        if (isInitialized)
            return true;

        ResolveDependencies();

        if (!ValidateConfiguration())
        {
            isInitialized = false;

            Debug.LogError(
                $"MetalOverlord failed to initialize on '{name}'.",
                this);

            return false;
        }

        ResetRuntimeState();
        RefreshWaitInstructions();

        currentState =
            IsValidBossState(
                startingState)
                ? startingState
                : BossState.Flying;

        isInitialized = true;

        StartRingBalloonRoutine();

        return true;
    }

    private static bool IsValidBossState(
        BossState state)
    {
        return
            System.Enum.IsDefined(
                typeof(BossState),
                state);
    }

    private void ResolveDependencies()
    {
        ResolveAnimator();
        ResolvePlayer();
    }

    private void ResolveAnimator()
    {
        bossAnimator ??=
            GetComponent<Animator>();

        bossAnimator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);
    }

    private void ResolvePlayer()
    {
        if (player != null)
            return;

        UltimatePlayerMovement playerMovement =
            FindAnyObjectByType<UltimatePlayerMovement>(
                FindObjectsInactive.Include);

        if (playerMovement == null)
            return;

        player =
            playerMovement.transform;
    }

    private void ResetRuntimeState()
    {
        teamBlastHits = 0;

        currentRings =
            Mathf.Clamp(
                startingRings,
                0f,
                maximumRings);

        crystalTimer = 0f;
        missileTimer = 0f;
        shipTimer = 0f;
        chaosTimer = 0f;

        defeated = false;
    }

    private void RestoreRuntimeState()
    {
        if (defeated)
            return;

        if (ringBalloonRoutine == null)
        {
            StartRingBalloonRoutine();
        }
    }

    #endregion

    #region Runtime Validation

    private bool CanUpdateBoss()
    {
        return
            isInitialized &&
            !defeated &&
            bossAnimator != null &&
            IsValidBossState(
                currentState);
    }

    #endregion

    #region Ring Countdown

    private void UpdateRingCountdown()
    {
        if (!float.IsFinite(
                currentRings))
        {
            currentRings =
                Mathf.Clamp(
                    startingRings,
                    0f,
                    maximumRings);
        }

        float safeDrainRate =
            float.IsFinite(
                ringDrainRate)
                ? Mathf.Max(
                    0f,
                    ringDrainRate)
                : 0f;

        float drainAmount =
            safeDrainRate *
            Time.deltaTime;

        if (!float.IsFinite(
                drainAmount))
        {
            drainAmount = 0f;
        }

        currentRings =
            Mathf.Max(
                0f,
                currentRings -
                drainAmount);

        if (currentRings >
            Mathf.Epsilon)
        {
            return;
        }

        currentRings = 0f;

        OnSuperSonicReverted();
    }

    private void OnSuperSonicReverted()
    {
        Debug.Log(
            "Super Sonic reverted.",
            this);
    }

    #endregion

    #region Attack Timers

    private void UpdateAttackTimers()
    {
        crystalTimer =
            AdvanceTimer(
                crystalTimer);

        missileTimer =
            AdvanceTimer(
                missileTimer);

        shipTimer =
            AdvanceTimer(
                shipTimer);

        chaosTimer =
            AdvanceTimer(
                chaosTimer);

        if (HasTimerElapsed(
                crystalTimer,
                crystalAttackDelay))
        {
            crystalTimer = 0f;
            LaunchCrystalPillars();
        }

        if (HasTimerElapsed(
                missileTimer,
                missileAttackDelay))
        {
            missileTimer = 0f;
            LaunchMissiles();
        }

        if (HasTimerElapsed(
                shipTimer,
                shipAttackDelay))
        {
            shipTimer = 0f;
            ThrowEggFleetShip();
        }

        if (HasTimerElapsed(
                chaosTimer,
                chaosControlDelay))
        {
            chaosTimer = 0f;
            ActivateChaosControl();
        }
    }

    private static float AdvanceTimer(
        float timer)
    {
        if (!float.IsFinite(timer))
        {
            timer = 0f;
        }

        timer +=
            Time.deltaTime;

        return
            float.IsFinite(timer)
                ? timer
                : 0f;
    }

    private static bool HasTimerElapsed(
        float timer,
        float delay)
    {
        if (!float.IsFinite(timer) ||
            !float.IsFinite(delay))
        {
            return false;
        }

        return
            timer >=
            Mathf.Max(
                0f,
                delay);
    }

    #endregion

    #region Crystal Attack

    private void LaunchCrystalPillars()
    {
        if (player == null ||
            crystalPillarPrefab == null)
        {
            return;
        }

        TriggerAnimator(
            ShootCrystalsHash);

        StartCoroutine(
            CrystalAttackRoutine());
    }

    private IEnumerator CrystalAttackRoutine()
    {
        int count =
            Random.Range(
                minimumCrystalCount,
                maximumCrystalCount + 1);

        for (int index = 0;
             index < count;
             index++)
        {
            if (defeated ||
                player == null)
            {
                yield break;
            }

            Vector3 direction =
                player.position -
                transform.position;

            if (!IsFiniteVector(
                    direction) ||
                direction.sqrMagnitude <=
                    0.000001f)
            {
                yield return null;
                continue;
            }

            direction.Normalize();

            Quaternion rotation =
                Quaternion.LookRotation(
                    direction);

            if (!IsFiniteQuaternion(
                    rotation))
            {
                yield return null;
                continue;
            }

            Instantiate(
                crystalPillarPrefab,
                transform.position,
                rotation);

            yield return
                crystalSpawnWait;
        }
    }

    #endregion

    #region Missile Attack

    private void LaunchMissiles()
    {
        if (missilePrefab == null)
            return;

        TriggerAnimator(
            LaunchMissilesHash);

        StartCoroutine(
            MissileRoutine());
    }

    private IEnumerator MissileRoutine()
    {
        int count =
            Random.Range(
                minimumMissileCount,
                maximumMissileCount + 1);

        for (int index = 0;
             index < count;
             index++)
        {
            if (defeated)
                yield break;

            Instantiate(
                missilePrefab,
                transform.position,
                Quaternion.identity);

            yield return
                missileSpawnWait;
        }
    }

    #endregion

    #region Ship Attack

    private void ThrowEggFleetShip()
    {
        if (eggFleetShipPrefab == null)
            return;

        StartCoroutine(
            ShipRoutine());
    }

    private IEnumerator ShipRoutine()
    {
        TriggerAnimator(
            DiveForShipHash);

        yield return
            shipGrabWait;

        if (defeated)
            yield break;

        Vector3 spawnPosition =
            transform.position +
            Vector3.down *
            shipSpawnOffset;

        if (!IsFiniteVector(
                spawnPosition))
        {
            yield break;
        }

        Instantiate(
            eggFleetShipPrefab,
            spawnPosition,
            Quaternion.identity);

        TriggerAnimator(
            ThrowShipHash);
    }

    #endregion

    #region Chaos Control

    private void ActivateChaosControl()
    {
        if (defeated ||
            currentState ==
                BossState.ChaosControl)
        {
            return;
        }

        currentState =
            BossState.ChaosControl;

        TriggerAnimator(
            ChaosControlHash);

        if (chaosControlRoutine != null)
        {
            StopCoroutine(
                chaosControlRoutine);
        }

        chaosControlRoutine =
            StartCoroutine(
                ChaosControlRoutine());
    }

    private IEnumerator ChaosControlRoutine()
    {
        float timer =
            chaosControlDuration;

        while (timer > 0f &&
               !defeated)
        {
            float delta =
                Time.deltaTime;

            if (Input.anyKey)
            {
                delta +=
                    Time.deltaTime *
                    chaosControlInputMultiplier;
            }

            if (!float.IsFinite(
                    delta))
            {
                delta = 0f;
            }

            timer -=
                delta;

            yield return null;
        }

        chaosControlRoutine = null;

        if (defeated)
            yield break;

        currentState =
            BossState.Flying;
    }

    #endregion

    #region Ring Balloons

    private void StartRingBalloonRoutine()
    {
        if (ringBalloonRoutine != null)
            return;

        ringBalloonRoutine =
            StartCoroutine(
                SpawnRingBalloons());
    }

    private IEnumerator SpawnRingBalloons()
    {
        while (!defeated)
        {
            yield return
                ringBalloonSpawnWait;

            if (defeated)
                yield break;

            if (ringBalloonPrefab == null)
                continue;

            Vector3 randomOffset =
                Random.insideUnitSphere *
                ringBalloonSpawnRadius;

            Vector3 spawnPosition =
                transform.position +
                randomOffset;

            if (!IsFiniteVector(
                    spawnPosition))
            {
                continue;
            }

            Instantiate(
                ringBalloonPrefab,
                spawnPosition,
                Quaternion.identity);
        }

        ringBalloonRoutine = null;
    }

    #endregion

    #region Defeat

    private void DefeatBoss()
    {
        if (defeated)
            return;

        defeated = true;

        currentState =
            BossState.Defeated;

        StopRuntimeCoroutines();

        TriggerAnimator(
            DefeatedHash);

        Debug.Log(
            "Metal Overlord Defeated.",
            this);
    }

    #endregion

    #region Validation

    private bool ValidateConfiguration()
    {
        bool valid = true;

        valid &=
            ValidateReference(
                bossAnimator,
                nameof(Animator));

        if (player == null)
        {
            Debug.LogWarning(
                "MetalOverlord could not resolve the player.",
                this);
        }

        if (crystalPillarPrefab == null)
        {
            Debug.LogWarning(
                "MetalOverlord has no crystal pillar prefab.",
                this);
        }

        if (missilePrefab == null)
        {
            Debug.LogWarning(
                "MetalOverlord has no missile prefab.",
                this);
        }

        if (eggFleetShipPrefab == null)
        {
            Debug.LogWarning(
                "MetalOverlord has no Egg Fleet ship prefab.",
                this);
        }

        if (ringBalloonPrefab == null)
        {
            Debug.LogWarning(
                "MetalOverlord has no ring balloon prefab.",
                this);
        }

        return valid;
    }

    private bool ValidateReference(
        UnityEngine.Object reference,
        string displayName)
    {
        if (reference != null)
            return true;

        Debug.LogError(
            $"MetalOverlord requires {displayName}.",
            this);

        return false;
    }

    #endregion

    #region Helpers

    private void TriggerAnimator(
        int parameterHash)
    {
        if (bossAnimator == null)
            return;

        bossAnimator.SetTrigger(
            parameterHash);
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

    #region Cleanup

    private void CleanupRuntimeState()
    {
        StopRuntimeCoroutines();
    }

    private void StopRuntimeCoroutines()
    {
        if (ringBalloonRoutine != null)
        {
            StopCoroutine(
                ringBalloonRoutine);

            ringBalloonRoutine = null;
        }

        if (chaosControlRoutine != null)
        {
            StopCoroutine(
                chaosControlRoutine);

            chaosControlRoutine = null;
        }
    }

    private void CleanupDestroyedState()
    {
        CleanupRuntimeState();

        isInitialized = false;

        bossAnimator = null;
        player = null;

        crystalPillarPrefab = null;
        missilePrefab = null;
        eggFleetShipPrefab = null;
        ringBalloonPrefab = null;
    }

    #endregion
}