using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class MetalOverlord : MonoBehaviour
{
    #region Enums

    public enum BossState
    {
        Intro,
        Flying,
        Attacking,
        ChaosControl,
        Defeated
    }

    #endregion

    #region References

    [Header("References")]

    [SerializeField] private Animator anim;
    [SerializeField] private Transform player;

    [SerializeField] private GameObject crystalPillarPrefab;
    [SerializeField] private GameObject missilePrefab;
    [SerializeField] private GameObject crystalCagePrefab;
    [SerializeField] private GameObject eggFleetShipPrefab;
    [SerializeField] private GameObject ringBalloonPrefab;

    #endregion

    #region Boss Settings

    [Header("Boss")]

    [SerializeField] private BossState currentState = BossState.Intro;

    [SerializeField] private int teamBlastHitsRequired = 5;

    private int teamBlastHits;

    private bool defeated;

    #endregion

    #region Ring System

    [Header("Ring Countdown")]

    [SerializeField] private float startingRings = 50f;
    [SerializeField] private float ringDrainRate = 1f;
    [SerializeField] private int ringsPerBalloon = 10;

    private float currentRings;

    #endregion

    #region Attack Settings

    [Header("Attack Timers")]

    [SerializeField] private float crystalAttackDelay = 4f;
    [SerializeField] private float missileAttackDelay = 7f;
    [SerializeField] private float shipAttackDelay = 12f;
    [SerializeField] private float chaosControlDelay = 20f;

    private float crystalTimer;
    private float missileTimer;
    private float shipTimer;
    private float chaosTimer;

    #endregion

    #region Unity

    private void Awake()
    {
        if (anim == null)
        {
            anim = GetComponent<Animator>();
        }


        currentRings = startingRings;
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject obj = GameObject.FindGameObjectWithTag("Player");

            if (obj != null)
            {
                player = obj.transform;
            }
        }

        currentState = BossState.Flying;

        StartCoroutine(SpawnRingBalloons());
    }

    private void Update()
    {
        if (defeated)
        {
            return;
        }

        UpdateRingCountdown();

        switch (currentState)
        {
            case BossState.Flying:
                UpdateAttackTimers();
                break;

            case BossState.Attacking:
                UpdateAttackTimers();
                break;

            case BossState.ChaosControl:
                break;

            case BossState.Defeated:
                break;
        }
    }

    #endregion

    #region Ring Countdown

    private void UpdateRingCountdown()
    {
        currentRings -= ringDrainRate * Time.deltaTime;

        currentRings = Mathf.Max(currentRings, 0);

        if (currentRings <= 0)
        {
            OnSuperSonicReverted();
        }
    }

    public void AddRings(int amount)
    {
        currentRings += amount;
    }

    private void OnSuperSonicReverted()
    {
        Debug.Log("Super Sonic reverted.");
    }

    #endregion

    #region Attack Logic

    private void UpdateAttackTimers()
    {
        crystalTimer += Time.deltaTime;
        missileTimer += Time.deltaTime;
        shipTimer += Time.deltaTime;
        chaosTimer += Time.deltaTime;

        if (crystalTimer >= crystalAttackDelay)
        {
            crystalTimer = 0;
            LaunchCrystalPillars();
        }

        if (missileTimer >= missileAttackDelay)
        {
            missileTimer = 0;
            LaunchCrystalCageMissles();
        }

        if (shipTimer >= shipAttackDelay)
        {
            shipTimer = 0;
            ThrowEggFleetShips();
        }

        if (chaosTimer >= chaosControlDelay)
        {
            chaosTimer = 0;
            ActivateChaosControl();
        }
    }

    private void LaunchCrystalPillars()
    {
        if (player == null || crystalPillarPrefab == null)
            return;

        anim.SetTrigger("ShootCrystals");

        StartCoroutine(CrystalAttackRoutine());
    }

    private IEnumerator CrystalAttackRoutine()
    {
        int count = Random.Range(3, 7);

        for (int i = 0; i < count; i++)
        {
            Vector3 direction = (player.position - transform.position).normalized;

            Instantiate(crystalPillarPrefab, transform.position, Quaternion.LookRotation(direction));

            yield return new WaitForSeconds(0.4f);
        }
    }

    private void LaunchCrystalCageMissles()
    {
        if (missilePrefab == null)
            return;

        anim.SetTrigger("LaunchMissiles");

        StartCoroutine(MissileRoutine());
    }

    private IEnumerator MissileRoutine()
    {
        int count = Random.Range(2, 5);

        for (int i = 0; i < count; i++)
        {
            Instantiate(
                missilePrefab,
                transform.position,
                Quaternion.identity);

            yield return new WaitForSeconds(0.6f);
        }
    }

    private void ThrowEggFleetShips()
    {
        if (eggFleetShipPrefab == null)
        {
            return;
        }

        StartCoroutine(ShipRoutine());
    }

    private IEnumerator ShipRoutine()
    {
        anim.SetTrigger("DiveForShip");

        yield return new WaitForSeconds(1.5f);

        Vector3 spawn = transform.position + Vector3.down * 10f;

        Instantiate(eggFleetShipPrefab, spawn, Quaternion.identity);

        anim.SetTrigger("ThrowShip");
    }

    private void ActivateChaosControl()
    {
        currentState = BossState.ChaosControl;
        anim.SetTrigger("Chaos Control!");
        StartCoroutine (ChaosControlRoutine());
    }

    private IEnumerator ChaosControlRoutine()
    {
        float timer = 20f;

        while (timer > 0f)
        {
            timer -= Time.deltaTime;

            if (Input.anyKey)
            {
                timer -= Time.deltaTime * 2f;
            }

            yield return null;
        }

        currentState = BossState.Flying;
    }

    #endregion

    #region Team Blast

    public void OnTeamBlastHit()
    {
        if (defeated)
        {
            return;
        }

        teamBlastHits++;

        anim.SetTrigger("TakeHit");

        if (teamBlastHits >= teamBlastHitsRequired)
        {
            DefeatBoss();
        }
    }

    public void OnNormalAttackHit()
    {
        Debug.Log("Metal Overlord is immune.");
    }

    #endregion

    #region Defeat

    private void DefeatBoss()
    {
        defeated = true;

        currentState = BossState.Defeated;

        anim.SetTrigger("Defeated");

        Debug.Log("Metal Overlord Defeated");
    }

    #endregion

    #region Ring Balloons

    private IEnumerator SpawnRingBalloons()
    {
        while (!defeated)
        {
            yield return new WaitForSeconds(8f);

            if (ringBalloonPrefab == null)
            {
                continue;
            }

            Vector3 pos = transform.position + Random.insideUnitSphere * 15f;

            Instantiate(ringBalloonPrefab, pos, Quaternion.identity);
        }
    }

    #endregion
}