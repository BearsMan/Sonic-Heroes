using System.Collections;
using UnityEngine;

public class MetalOverlord : MonoBehaviour
{
    #region References

    [Header("References")]

    [SerializeField]
    private Transform player;

    [SerializeField]
    private Animator animator;

    [SerializeField]
    private AudioSource audioSource;

    #endregion

    #region Attack Prefabs

    [Header("Attack Prefabs")]

    [SerializeField]
    private GameObject crystalPillarPrefab;

    [SerializeField]
    private GameObject missilePrefab;

    [SerializeField]
    private GameObject eggFleetShipPrefab;

    [SerializeField]
    private GameObject ringBalloonPrefab;

    #endregion

    #region Spawn Points

    [Header("Spawn Points")]

    [SerializeField]
    private Transform crystalSpawnPoint;

    [SerializeField]
    private Transform missileSpawnPoint;

    [SerializeField]
    private Transform shipSpawnPoint;

    #endregion

    #region Boss Health

    [Header("Boss Health")]

    [SerializeField, Min(1)]
    private int teamBlastHitsRequired = 5;

    [SerializeField, Min(0f)]
    private float hitRecoveryTime = 1.5f;

    #endregion

    #region Ring Countdown

    [Header("Ring Countdown")]

    [SerializeField, Min(0f)]
    private float startingRings = 50f;

    [SerializeField, Min(0f)]
    private float ringDrainRate = 1f;

    [SerializeField, Min(1)]
    private int ringBalloonAmount = 10;

    [SerializeField, Min(0.1f)]
    private float ringBalloonInterval = 8f;

    [SerializeField, Min(0f)]
    private float ringBalloonSpawnRadius = 15f;

    #endregion

    #region Crystal Attack

    [Header("Crystal Attack")]

    [SerializeField, Min(0.1f)]
    private float crystalAttackInterval = 4f;

    [SerializeField, Min(1)]
    private int minimumCrystalCount = 3;

    [SerializeField, Min(1)]
    private int maximumCrystalCount = 6;

    [SerializeField, Min(0f)]
    private float crystalLaunchInterval = 0.4f;

    #endregion

    #region Missile Attack

    [Header("Missile Attack")]

    [SerializeField, Min(0.1f)]
    private float missileAttackInterval = 7f;

    [SerializeField, Min(1)]
    private int minimumMissileCount = 2;

    [SerializeField, Min(1)]
    private int maximumMissileCount = 4;

    [SerializeField, Min(0f)]
    private float missileLaunchInterval = 0.6f;

    #endregion

    #region Egg Fleet Attack

    [Header("Egg Fleet Attack")]

    [SerializeField, Min(0.1f)]
    private float shipAttackInterval = 12f;

    [SerializeField, Min(0f)]
    private float shipDiveDuration = 1.5f;

    [SerializeField]
    private Vector3 shipSpawnOffset =
        new(0f, -10f, 0f);

    #endregion

    #region Chaos Control

    [Header("Chaos Control")]

    [SerializeField, Min(0.1f)]
    private float chaosControlInterval = 20f;

    [SerializeField, Min(0.1f)]
    private float chaosControlDuration = 20f;

    [SerializeField, Min(0f)]
    private float mashReductionMultiplier = 2f;

    #endregion

    #region Effects

    [Header("Effects")]

    [SerializeField]
    private GameObject teamBlastHitEffect;

    [SerializeField]
    private GameObject defeatEffect;

    [SerializeField]
    private GameObject chaosControlEffect;

    [SerializeField, Min(0f)]
    private float effectLifetime = 5f;

    #endregion

    #region Audio

    [Header("Audio")]

    [SerializeField]
    private AudioClip crystalAttackSound;

    [SerializeField]
    private AudioClip missileAttackSound;

    [SerializeField]
    private AudioClip shipAttackSound;

    [SerializeField]
    private AudioClip chaosControlSound;

    [SerializeField]
    private AudioClip teamBlastHitSound;

    [SerializeField]
    private AudioClip defeatSound;

    #endregion

    #region Runtime State

    private float rings;

    private float crystalTimer;
    private float missileTimer;
    private float shipTimer;
    private float chaosControlCooldownTimer;
    private float chaosControlTimer;
    private float hitRecoveryTimer;

    private int teamBlastHitsReceived;

    private bool isFrozenByChaosControl;
    private bool isRecoveringFromHit;
    private bool isDead;

    private Coroutine crystalRoutine;
    private Coroutine missileRoutine;
    private Coroutine shipRoutine;
    private Coroutine ringBalloonRoutine;

    #endregion

    #region Properties

    public bool IsDead =>
        isDead;

    public bool IsFrozen =>
        isFrozenByChaosControl;

    public int TeamBlastHitsReceived =>
        teamBlastHitsReceived;

    public int TeamBlastHitsRequired =>
        teamBlastHitsRequired;

    public float Rings =>
        rings;

    public float BossProgress =>
        teamBlastHitsRequired > 0
            ? Mathf.Clamp01(
                (float)teamBlastHitsReceived /
                teamBlastHitsRequired)
            : 1f;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();

        rings =
            startingRings;
    }

    private void Start()
    {
        if (animator != null &&
            animator.isActiveAndEnabled)
        {
            animator.SetBool(
                "Flying",
                true);

            animator.Play(
                "Flying");
        }

        if (ringBalloonPrefab != null)
        {
            ringBalloonRoutine =
                StartCoroutine(
                    RingBalloonRoutine());
        }
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        UpdateRingCountdown();

        if (isDead)
        {
            return;
        }

        UpdateHitRecovery();

        if (isFrozenByChaosControl)
        {
            UpdateChaosControl();

            return;
        }

        if (isRecoveringFromHit)
        {
            return;
        }

        UpdateAttackTimers();
    }

    private void OnDisable()
    {
        StopBossCoroutines();

        isFrozenByChaosControl =
            false;

        isRecoveringFromHit =
            false;
    }

    private void OnValidate()
    {
        teamBlastHitsRequired =
            Mathf.Max(
                1,
                teamBlastHitsRequired);

        hitRecoveryTime =
            Mathf.Max(
                0f,
                hitRecoveryTime);

        startingRings =
            Mathf.Max(
                0f,
                startingRings);

        ringDrainRate =
            Mathf.Max(
                0f,
                ringDrainRate);

        ringBalloonAmount =
            Mathf.Max(
                1,
                ringBalloonAmount);

        ringBalloonInterval =
            Mathf.Max(
                0.1f,
                ringBalloonInterval);

        ringBalloonSpawnRadius =
            Mathf.Max(
                0f,
                ringBalloonSpawnRadius);

        crystalAttackInterval =
            Mathf.Max(
                0.1f,
                crystalAttackInterval);

        minimumCrystalCount =
            Mathf.Max(
                1,
                minimumCrystalCount);

        maximumCrystalCount =
            Mathf.Max(
                minimumCrystalCount,
                maximumCrystalCount);

        crystalLaunchInterval =
            Mathf.Max(
                0f,
                crystalLaunchInterval);

        missileAttackInterval =
            Mathf.Max(
                0.1f,
                missileAttackInterval);

        minimumMissileCount =
            Mathf.Max(
                1,
                minimumMissileCount);

        maximumMissileCount =
            Mathf.Max(
                minimumMissileCount,
                maximumMissileCount);

        missileLaunchInterval =
            Mathf.Max(
                0f,
                missileLaunchInterval);

        shipAttackInterval =
            Mathf.Max(
                0.1f,
                shipAttackInterval);

        shipDiveDuration =
            Mathf.Max(
                0f,
                shipDiveDuration);

        chaosControlInterval =
            Mathf.Max(
                0.1f,
                chaosControlInterval);

        chaosControlDuration =
            Mathf.Max(
                0.1f,
                chaosControlDuration);

        mashReductionMultiplier =
            Mathf.Max(
                0f,
                mashReductionMultiplier);

        effectLifetime =
            Mathf.Max(
                0f,
                effectLifetime);
    }

    #endregion

    #region Ring Countdown

    private void UpdateRingCountdown()
    {
        if (ringDrainRate <= 0f)
        {
            return;
        }

        rings -=
            ringDrainRate *
            Time.deltaTime;

        rings =
            Mathf.Max(
                0f,
                rings);

        if (rings <= 0f)
        {
            HandleRingDepletion();
        }
    }

    private void HandleRingDepletion()
    {
        rings =
            0f;

        StopBossCoroutines();

        isFrozenByChaosControl =
            false;

        isRecoveringFromHit =
            false;
    }

    public void ResetRingCountdown()
    {
        if (isDead)
        {
            return;
        }

        rings =
            startingRings;
    }

    public void AddRings(
        int amount)
    {
        if (isDead ||
            amount <= 0)
        {
            return;
        }

        rings +=
            amount;
    }

    public void CollectRingBalloon()
    {
        AddRings(
            ringBalloonAmount);
    }

    #endregion

    #region Ring Balloons

    private IEnumerator RingBalloonRoutine()
    {
        WaitForSeconds wait =
            new(
                ringBalloonInterval);

        while (!isDead)
        {
            yield return
                wait;

            if (isDead ||
                ringBalloonPrefab == null)
            {
                continue;
            }

            Vector3 spawnPosition =
                GetRingBalloonSpawnPosition();

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

        ringBalloonRoutine =
            null;
    }

    private Vector3 GetRingBalloonSpawnPosition()
    {
        Vector2 circle =
            Random.insideUnitCircle *
            ringBalloonSpawnRadius;

        return
            transform.position +
            new Vector3(
                circle.x,
                0f,
                circle.y);
    }

    #endregion

    #region Attack Timers

    private void UpdateAttackTimers()
    {
        crystalTimer +=
            Time.deltaTime;

        missileTimer +=
            Time.deltaTime;

        shipTimer +=
            Time.deltaTime;

        chaosControlCooldownTimer +=
            Time.deltaTime;

        if (crystalTimer >=
            crystalAttackInterval)
        {
            crystalTimer =
                0f;

            if (crystalRoutine == null)
            {
                crystalRoutine =
                    StartCoroutine(
                        CrystalAttackRoutine());
            }
        }

        if (missileTimer >=
            missileAttackInterval)
        {
            missileTimer =
                0f;

            if (missileRoutine == null)
            {
                missileRoutine =
                    StartCoroutine(
                        MissileAttackRoutine());
            }
        }

        if (shipTimer >=
            shipAttackInterval)
        {
            shipTimer =
                0f;

            if (shipRoutine == null)
            {
                shipRoutine =
                    StartCoroutine(
                        ShipAttackRoutine());
            }
        }

        if (chaosControlCooldownTimer >=
            chaosControlInterval)
        {
            chaosControlCooldownTimer =
                0f;

            ActivateChaosControl();
        }
    }

    #endregion

    #region Crystal Attack

    private IEnumerator CrystalAttackRoutine()
    {
        PlayTrigger(
            "ShootCrystals");

        PlaySound(
            crystalAttackSound);

        int count =
            Random.Range(
                minimumCrystalCount,
                maximumCrystalCount + 1);

        for (int index = 0;
            index < count;
            index++)
        {
            if (isDead ||
                isFrozenByChaosControl)
            {
                break;
            }

            SpawnCrystalPillar();

            if (crystalLaunchInterval > 0f &&
                index < count - 1)
            {
                yield return
                    new WaitForSeconds(
                        crystalLaunchInterval);
            }
        }

        crystalRoutine =
            null;
    }

    private void SpawnCrystalPillar()
    {
        if (crystalPillarPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition =
            GetSpawnPosition(
                crystalSpawnPoint);

        Quaternion rotation =
            GetRotationTowardsPlayer(
                spawnPosition);

        Instantiate(
            crystalPillarPrefab,
            spawnPosition,
            rotation);
    }

    #endregion

    #region Missile Attack

    private IEnumerator MissileAttackRoutine()
    {
        PlayTrigger(
            "LaunchMissiles");

        PlaySound(
            missileAttackSound);

        int count =
            Random.Range(
                minimumMissileCount,
                maximumMissileCount + 1);

        for (int index = 0;
            index < count;
            index++)
        {
            if (isDead ||
                isFrozenByChaosControl)
            {
                break;
            }

            SpawnMissile();

            if (missileLaunchInterval > 0f &&
                index < count - 1)
            {
                yield return
                    new WaitForSeconds(
                        missileLaunchInterval);
            }
        }

        missileRoutine =
            null;
    }

    private void SpawnMissile()
    {
        if (missilePrefab == null)
        {
            return;
        }

        Vector3 spawnPosition =
            GetSpawnPosition(
                missileSpawnPoint);

        Quaternion rotation =
            GetRotationTowardsPlayer(
                spawnPosition);

        Instantiate(
            missilePrefab,
            spawnPosition,
            rotation);
    }

    #endregion

    #region Egg Fleet Attack

    private IEnumerator ShipAttackRoutine()
    {
        PlayTrigger(
            "DiveForShip");

        if (shipDiveDuration > 0f)
        {
            yield return
                new WaitForSeconds(
                    shipDiveDuration);
        }

        if (isDead ||
            isFrozenByChaosControl)
        {
            shipRoutine =
                null;

            yield break;
        }

        SpawnEggFleetShip();

        PlayTrigger(
            "ThrowShip");

        PlaySound(
            shipAttackSound);

        shipRoutine =
            null;
    }

    private void SpawnEggFleetShip()
    {
        if (eggFleetShipPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition;

        if (shipSpawnPoint != null &&
            IsFiniteVector(
                shipSpawnPoint.position))
        {
            spawnPosition =
                shipSpawnPoint.position;
        }
        else
        {
            spawnPosition =
                transform.position +
                shipSpawnOffset;
        }

        if (!IsFiniteVector(
                spawnPosition))
        {
            return;
        }

        Instantiate(
            eggFleetShipPrefab,
            spawnPosition,
            GetRotationTowardsPlayer(
                spawnPosition));
    }

    #endregion

    #region Chaos Control

    private void ActivateChaosControl()
    {
        if (isDead ||
            isFrozenByChaosControl)
        {
            return;
        }

        isFrozenByChaosControl =
            true;

        chaosControlTimer =
            chaosControlDuration;

        PlayTrigger(
            "ChaosControl");

        PlaySound(
            chaosControlSound);

        SpawnEffect(
            chaosControlEffect,
            transform.position);
    }

    private void UpdateChaosControl()
    {
        chaosControlTimer -=
            Time.deltaTime;

        if (Input.anyKey)
        {
            chaosControlTimer -=
                Time.deltaTime *
                mashReductionMultiplier;
        }

        if (chaosControlTimer <= 0f)
        {
            EndChaosControl();
        }
    }

    private void EndChaosControl()
    {
        isFrozenByChaosControl =
            false;

        chaosControlTimer =
            0f;
    }

    #endregion

    #region Team Blast Damage

    public bool OnTeamBlastHit()
    {
        if (isDead ||
            isRecoveringFromHit)
        {
            return false;
        }

        teamBlastHitsReceived++;

        teamBlastHitsReceived =
            Mathf.Min(
                teamBlastHitsReceived,
                teamBlastHitsRequired);

        PlayTrigger(
            "TakeHit");

        PlaySound(
            teamBlastHitSound);

        SpawnEffect(
            teamBlastHitEffect,
            transform.position);

        if (teamBlastHitsReceived >=
            teamBlastHitsRequired)
        {
            Defeat();

            return true;
        }

        if (hitRecoveryTime > 0f)
        {
            isRecoveringFromHit =
                true;

            hitRecoveryTimer =
                hitRecoveryTime;
        }

        return true;
    }

    public bool OnNormalAttackHit()
    {
        return false;
    }

    private void UpdateHitRecovery()
    {
        if (!isRecoveringFromHit)
        {
            return;
        }

        hitRecoveryTimer -=
            Time.deltaTime;

        if (hitRecoveryTimer <= 0f)
        {
            hitRecoveryTimer =
                0f;

            isRecoveringFromHit =
                false;
        }
    }

    #endregion

    #region Defeat

    private void Defeat()
    {
        if (isDead)
        {
            return;
        }

        isDead =
            true;

        isFrozenByChaosControl =
            false;

        isRecoveringFromHit =
            false;

        StopBossCoroutines();

        PlayTrigger(
            "Defeated");

        PlaySound(
            defeatSound);

        SpawnEffect(
            defeatEffect,
            transform.position);
    }

    #endregion

    #region Coroutines

    private void StopBossCoroutines()
    {
        if (crystalRoutine != null)
        {
            StopCoroutine(
                crystalRoutine);

            crystalRoutine =
                null;
        }

        if (missileRoutine != null)
        {
            StopCoroutine(
                missileRoutine);

            missileRoutine =
                null;
        }

        if (shipRoutine != null)
        {
            StopCoroutine(
                shipRoutine);

            shipRoutine =
                null;
        }

        if (ringBalloonRoutine != null)
        {
            StopCoroutine(
                ringBalloonRoutine);

            ringBalloonRoutine =
                null;
        }
    }

    #endregion

    #region References

    private void ResolveReferences()
    {
        animator ??=
            GetComponent<Animator>();

        audioSource ??=
            GetComponent<AudioSource>();

        if (player == null)
        {
            GameObject playerObject =
                GameObject.FindWithTag(
                    "Player");

            if (playerObject != null)
            {
                player =
                    playerObject.transform;
            }
        }
    }

    #endregion

    #region Spawn Helpers

    private Vector3 GetSpawnPosition(
        Transform spawnPoint)
    {
        if (spawnPoint != null &&
            IsFiniteVector(
                spawnPoint.position))
        {
            return
                spawnPoint.position;
        }

        return
            transform.position;
    }

    private Quaternion GetRotationTowardsPlayer(
        Vector3 origin)
    {
        if (player == null ||
            !IsFiniteVector(
                player.position) ||
            !IsFiniteVector(
                origin))
        {
            return
                transform.rotation;
        }

        Vector3 direction =
            player.position -
            origin;

        if (!IsFiniteVector(
                direction) ||
            direction.sqrMagnitude <=
                0.0001f)
        {
            return
                transform.rotation;
        }

        return
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);
    }

    #endregion

    #region Animation

    private void PlayTrigger(
        string trigger)
    {
        if (animator == null ||
            !animator.isActiveAndEnabled ||
            animator.runtimeAnimatorController ==
                null ||
            string.IsNullOrWhiteSpace(
                trigger))
        {
            return;
        }

        animator.SetTrigger(
            trigger);
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

    #region Effects

    private void SpawnEffect(
        GameObject effect,
        Vector3 position)
    {
        if (effect == null ||
            !IsFiniteVector(
                position))
        {
            return;
        }

        GameObject instance =
            Instantiate(
                effect,
                position,
                transform.rotation);

        if (effectLifetime > 0f)
        {
            Destroy(
                instance,
                effectLifetime);
        }
    }

    #endregion

    #region Validation

    private static bool IsFiniteVector(
        Vector3 value)
    {
        return
            float.IsFinite(
                value.x) &&
            float.IsFinite(
                value.y) &&
            float.IsFinite(
                value.z);
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (ringBalloonSpawnRadius > 0f)
        {
            Gizmos.DrawWireSphere(
                transform.position,
                ringBalloonSpawnRadius);
        }

        if (crystalSpawnPoint != null)
        {
            Gizmos.DrawWireSphere(
                crystalSpawnPoint.position,
                0.5f);
        }

        if (missileSpawnPoint != null)
        {
            Gizmos.DrawWireSphere(
                missileSpawnPoint.position,
                0.5f);
        }

        if (shipSpawnPoint != null)
        {
            Gizmos.DrawWireSphere(
                shipSpawnPoint.position,
                0.5f);
        }
    }

    #endregion
}