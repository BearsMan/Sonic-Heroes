using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class TeamBlast : MonoBehaviour
{
    public enum TeamType
    {
        Sonic,
        Dark,
        Rose,
        Chaotix,
        SuperSonic
    }

    [Header("Team")]
    [SerializeField] private TeamType currentTeam = TeamType.Sonic;

    [Header("Gauge")]
    [SerializeField] private float maxGauge = 100f;
    [SerializeField] private KeyCode teamBlastKey = KeyCode.Z;

    [Header("Blast")]
    [SerializeField] private float blastRadius = 15f;
    [SerializeField] private float invincibilityTime = 5f;
    [SerializeField] private LayerMask enemyLayers;

    [Header("Effects")]
    [SerializeField] private GameObject blastEffect;
    [SerializeField] private AudioClip blastSound;

    private bool isInvincible = false;
    private bool blastActive = false;
    private AudioSource blastAudioSource;
    private Coroutine invincibilityCoroutine;
    public bool BlastReady => GameInstance.teamBlastMeter >= maxGauge;
    public bool IsInvincible => isInvincible;
    public float GaugePercent => maxGauge > 0f ? GameInstance.teamBlastMeter / maxGauge : 0f;

    private void Awake()
    {
        blastAudioSource = GetComponent<AudioSource>();
        GameInstance.maxTeamBlastMeter = maxGauge;

    }

    private void Update()
    {
        if (blastActive)
        {
            return;
        }

        if (BlastReady && Input.GetKeyDown(teamBlastKey))
        {
            ActivateTeamBlast();
        }

    }
    public void AddGauge(float amount)
    {
        GameInstance.teamBlastMeter = Mathf.Clamp(GameInstance.teamBlastMeter + amount, 0, maxGauge);
    }

    public void ResetGauge()
    {
        GameInstance.teamBlastMeter = 0f;
    }

    private void ActivateTeamBlast()
    {
        blastActive = true;
        Debug.Log(GetTeamName() + " Activated!");
        ResetGauge();

        if (blastAudioSource != null && blastSound != null)
        {
            blastAudioSource.PlayOneShot(blastSound);
        }

        if (blastEffect != null)
        {
            Instantiate(blastEffect, transform.position, Quaternion.identity);
        }

        ActivateInvincibility();

        DestroyNearbyEnemies();

        DamageMetalOverlord();

        ApplyTeamEffect();

        Invoke(nameof(FinishBlast), 0.5f);
    }

    private void FinishBlast()
    {
        blastActive = false;
    }

    private void ActivateInvincibility()
    {
        if (invincibilityCoroutine != null)
        {
            StopCoroutine(invincibilityCoroutine);
        }

        invincibilityCoroutine = StartCoroutine(TeamBlastInvincibility());
    }

    private IEnumerator TeamBlastInvincibility()
    {
        isInvincible = true;

        Debug.Log("Team Blast invincibility started.");

        yield return new WaitForSeconds(invincibilityTime);

        isInvincible = false;
        invincibilityCoroutine = null;

        Debug.Log("Team Blast invincibility ended.");
    }
    private void DestroyNearbyEnemies()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, blastRadius, enemyLayers);
        foreach (Collider enemy in enemies)
        {
            Destroy(enemy.gameObject);
        }
    }

    private void DamageMetalOverlord()
    {
        MetalOverlord boss = FindAnyObjectByType<MetalOverlord>();
        if (boss != null)
        {
            boss.OnTeamBlastHit();
        }
    }

    private void ApplyTeamEffect()
    {
        switch (currentTeam)
        {
            case TeamType.Sonic:
                Debug.Log("Sonic Overdrive");
                break;

            case TeamType.Dark:
                Debug.Log("Chaos Inferno");
                break;

            case TeamType.Rose:
                Debug.Log("Flower Festival");
                break;

            case TeamType.Chaotix:
                Debug.Log("Chaotix Recital");
                break;

            case TeamType.SuperSonic:
                Debug.Log("Super Sonic Power");
                break;
        }
    }

    private string GetTeamName()
    {
        switch (currentTeam)
        {
            case TeamType.Sonic:
                return "Sonic Overdrive";

            case TeamType.Dark:
                return "Chaos Inferno";

            case TeamType.Rose:
                return "Flower Festival";

            case TeamType.Chaotix:
                return "Chaotix Recital";

            case TeamType.SuperSonic:
                return "Super Sonic Power";
        }

        return "Team Blast";
    }
}