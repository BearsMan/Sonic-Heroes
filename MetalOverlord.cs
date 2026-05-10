using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class MetalOverlord : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public GameObject crystalPillarPrefab;
    public GameObject missilePrefab;
    public GameObject crystalCagePrefab;
    public GameObject eggFleetShipPrefab;
    public GameObject ringBalloonPrefab;

    [Header("Ring Countdown")]
    public float rings = 50f;
    public float ringDrainRate = 1f;          // Rings lost per second
    public int ringBalloonCount = 10;         // Rings restored per balloon pickup

    [Header("Boss State")]
    public int teamBlastHitsRequired = 5;     // Must be hit by Team Blast 5 times to win
    private int teamBlastHitsReceived = 0;
    private bool isFrozenByChaosControl = false;
    private float chaosControlDuration = 20f;
    private float chaosControlTimer = 0f;

    [Header("Attack Timings")]
    public float crystalAttackInterval = 4f;
    public float missileAttackInterval = 7f;
    public float shipAttackInterval = 12f;
    public float chaosControlInterval = 20f;  // Triggers if player stalls too long

    private float crystalAttackTimer = 0f;
    private float missileAttackTimer = 0f;
    private float shipAttackTimer = 0f;
    private float chaosControlTimer_cooldown = 0f;

    private Animator anim;
    private bool isDead = false;

    void Start()
    {
        player = GameObject.FindWithTag("Player").transform;
        anim = GetComponent<Animator>();

        // Metal Overlord is always airborne - immediately flies into the sky
        anim.Play("Flying");
        anim.SetBool("Flying", true);

        StartCoroutine(SpawnRingBalloons());
    }

    void Update()
    {
        if (isDead) return;

        DrainRings();

        if (isFrozenByChaosControl)
        {
            HandleChaosControlFreeze();
            return; // Player can't act during Chaos Control, but rings still drain
        }

        // Cycle through attacks
        crystalAttackTimer += Time.deltaTime;
        missileAttackTimer += Time.deltaTime;
        shipAttackTimer += Time.deltaTime;
        chaosControlTimer_cooldown += Time.deltaTime;

        // Crystal pillars - most frequent attack, countered by Super Sonic's Homing Attack
        if (crystalAttackTimer >= crystalAttackInterval)
        {
            crystalAttackTimer = 0f;
            StartCoroutine(ShootCrystalPillars());
        }

        // Missiles that form crystal cages - countered by Tails' Thunder Shoot
        if (missileAttackTimer >= missileAttackInterval)
        {
            missileAttackTimer = 0f;
            StartCoroutine(LaunchCrystalCageMissiles());
        }

        // Egg Fleet ship throw - countered by Knuckles' Fire Spike/fireballs
        if (shipAttackTimer >= shipAttackInterval)
        {
            shipAttackTimer = 0f;
            StartCoroutine(ThrowEggFleetShip());
        }

        // Chaos Control - triggers if the player stalls too long
        if (chaosControlTimer_cooldown >= chaosControlInterval)
        {
            chaosControlTimer_cooldown = 0f;
            ActivateChaosControl();
        }
    }

    // ─── Ring Countdown ──────────────────────────────────────────────────────────

    void DrainRings()
    {
        rings -= ringDrainRate * Time.deltaTime;
        rings = Mathf.Max(rings, 0f);

        if (rings <= 0f)
        {
            // Super Sonic reverts - player loses a life
            OnSuperSonicReverted();
        }
    }

    void OnSuperSonicReverted()
    {
        Debug.Log("Super Sonic ran out of rings! Player loses a life.");
        // Trigger death/respawn logic here
    }

    // Balloon pickups scattered around the arena to keep ring count up
    IEnumerator SpawnRingBalloons()
    {
        while (!isDead)
        {
            if (ringBalloonPrefab != null)
            {
                Vector3 spawnPos = transform.position + Random.insideUnitSphere * 15f;
                Instantiate(ringBalloonPrefab, spawnPos, Quaternion.identity);
            }
            yield return new WaitForSeconds(8f);
        }
    }

    public void CollectRingBalloon()
    {
        rings += ringBalloonCount;
        Debug.Log($"Collected ring balloon! Rings: {rings}");
    }

    // ─── Attacks ─────────────────────────────────────────────────────────────────

    // Most common attack - Super Sonic counters with Homing Attack, Rocket Accel, or Tornado Jump
    IEnumerator ShootCrystalPillars()
    {
        anim.SetTrigger("ShootCrystals");
        int crystalCount = Random.Range(3, 7);
        for (int i = 0; i < crystalCount; i++)
        {
            if (crystalPillarPrefab != null)
            {
                Vector3 direction = (player.position - transform.position).normalized;
                Quaternion rot = Quaternion.LookRotation(direction);
                Instantiate(crystalPillarPrefab, transform.position, rot);
            }
            yield return new WaitForSeconds(0.4f);
        }
        Debug.Log("Metal Overlord: Crystal pillars launched! Counter with Homing Attack.");
    }

    // Missiles that encase teammates in crystal cages - Tails' Thunder Shoot charges Team Blast gauge
    IEnumerator LaunchCrystalCageMissiles()
    {
        anim.SetTrigger("LaunchMissiles");
        int missileCount = Random.Range(2, 5);
        for (int i = 0; i < missileCount; i++)
        {
            if (missilePrefab != null)
                Instantiate(missilePrefab, transform.position, Quaternion.identity);
            yield return new WaitForSeconds(0.6f);
        }
        Debug.Log("Metal Overlord: Crystal cage missiles! Tails Thunder Shoot to free teammates & charge Team Blast.");
    }

    // Dives below clouds and hurls an Egg Fleet ship - Knuckles' fireballs destroy it & charge Team Blast faster
    IEnumerator ThrowEggFleetShip()
    {
        anim.SetTrigger("DiveForShip");
        yield return new WaitForSeconds(1.5f); // Diving animation

        if (eggFleetShipPrefab != null)
        {
            Vector3 spawnPos = transform.position + Vector3.down * 10f;
            Instantiate(eggFleetShipPrefab, spawnPos, Quaternion.identity);
        }
        anim.SetTrigger("ThrowShip");
        Debug.Log("Metal Overlord: Egg Fleet ship thrown! Use Knuckles' Fire Spike to destroy it.");
    }

    // Freezes Team Super Sonic for 20 seconds - player can mash to reduce duration
    void ActivateChaosControl()
    {
        isFrozenByChaosControl = true;
        chaosControlTimer = chaosControlDuration;
        anim.SetTrigger("ChaosControl");
        Debug.Log("Metal Overlord: CHAOS CONTROL! Frozen for 20 seconds. Mash buttons to escape faster!");
    }

    void HandleChaosControlFreeze()
    {
        chaosControlTimer -= Time.deltaTime;

        // Player mashing buttons/joystick reduces freeze duration faster
        if (Input.anyKey)
            chaosControlTimer -= Time.deltaTime * 2f;

        if (chaosControlTimer <= 0f)
        {
            isFrozenByChaosControl = false;
            Debug.Log("Chaos Control ended - Team Super Sonic is free!");
        }
    }

    // ─── Damage (Team Blast Only) ─────────────────────────────────────────────────

    // Metal Overlord is completely invincible to all normal attacks.
    // ONLY Team Blast (Super Sonic Power) can damage him.
    // Must be hit 5 times to defeat him.
    public void OnTeamBlastHit()
    {
        if (isDead) return;

        teamBlastHitsReceived++;
        Debug.Log($"Team Blast hit! {teamBlastHitsReceived}/{teamBlastHitsRequired}");
        anim.SetTrigger("TakeHit");

        if (teamBlastHitsReceived >= teamBlastHitsRequired)
        {
            Die();
        }
    }

    // Ignore all non-Team-Blast damage - Metal Overlord is invincible otherwise
    public void OnNormalAttackHit()
    {
        Debug.Log("Normal attacks have no effect on Metal Overlord! Use Team Blast.");
    }

    // ─── Death ───────────────────────────────────────────────────────────────────

    void Die()
    {
        isDead = true;
        anim.SetTrigger("Defeated");
        Debug.Log("Metal Overlord defeated! He reverts back to Metal Sonic.");
        // Trigger ending cutscene / revert-to-Metal-Sonic sequence here
    }
}