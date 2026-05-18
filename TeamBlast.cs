using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TeamBlast.cs - Rewritten to match Sonic Heroes (2003) mechanics.
///
/// How Team Blast works in the original game:
///   - Each team has a dedicated Team Blast attack with a unique name and effect.
///   - A power gauge fills as players collect rings, defeat enemies, or perform tricks.
///   - When the gauge is full, the player can manually trigger the Team Blast.
///   - The blast defeats all nearby enemies, grants a brief period of invincibility,
///     and resets the power gauge to zero afterward.
///   - Each team's blast has a unique secondary effect:
///       Team Sonic  (Sonic Overdrive)   - brief speed boost after activation
///       Team Dark   (Chaos Inferno)     - extended invincibility duration
///       Team Rose   (Flower Festival)  - all characters powered up / rings attracted
///       Team Chaotix (Chaotix Recital) - score multiplier / enemy clear
///       Team Super Sonic (Super Sonic Power) - full invincibility + massive damage
/// </summary>
public class TeamBlast : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

    [Header("Team Identity")]
    public bool isTeamSonic;
    public bool isTeamDark;
    public bool isTeamRose;
    public bool isTeamChaotix;
    public bool isTeamSuperSonic;

    [Header("Power Gauge")]
    [Tooltip("Maximum value of the power gauge before Team Blast can be triggered.")]
    public float maxPowerGauge = 100f;

    [Tooltip("Amount of gauge gained per ring collected.")]
    public float gaugePerRing = 2f;

    [Tooltip("Amount of gauge gained per enemy defeated.")]
    public float gaugePerEnemy = 10f;

    [Header("Blast Settings")]
    [Tooltip("Radius of the Team Blast explosion that clears enemies.")]
    public float blastRadius = 10f;

    [Tooltip("Base duration of invincibility granted after Team Blast (seconds).")]
    public float baseInvincibilityDuration = 5f;

    [Tooltip("Layer mask for enemy objects.")]
    public LayerMask enemyLayer;

    [Header("Audio")]
    public AudioClip teamBlastSFX;

    [Header("VFX / Sprites")]
    public GameObject teamBlastVFXPrefab;
    public Sprite Heroes_TeamBlastSonic;
    public Sprite Heroes_TeamBlastDark;
    public Sprite Heroes_TeamBlastRose;
    public Sprite Heroes_TeamBlastChaotix;
    public Sprite Super_Sonic_Power_Team_Blast;

    // -------------------------------------------------------------------------
    // Runtime State
    // -------------------------------------------------------------------------

    private float currentPowerGauge = 0f;
    private bool isInvincible = false;
    private bool teamBlastActive = false;
    private AudioSource audioSource;
    private SpriteRenderer spriteRenderer;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        AssignTeamSprite();
    }

    void Update()
    {
        HandleTeamBlastInput();
    }

    // -------------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------------

    /// <summary>
    /// Assigns the correct Team Blast artwork based on the active team flag.
    /// </summary>
    private void AssignTeamSprite()
    {
        if (isTeamSuperSonic && Super_Sonic_Power_Team_Blast != null)
        {
            spriteRenderer.sprite = Super_Sonic_Power_Team_Blast;
        }
        else if (isTeamSonic && Heroes_TeamBlastSonic != null)
        {
            spriteRenderer.sprite = Heroes_TeamBlastSonic;
        }
        else if (isTeamDark && Heroes_TeamBlastDark != null)
        {
            spriteRenderer.sprite = Heroes_TeamBlastDark;
        }
        else if (isTeamRose && Heroes_TeamBlastRose != null)
        {
            spriteRenderer.sprite = Heroes_TeamBlastRose;
        }
        else if (isTeamChaotix && Heroes_TeamBlastChaotix != null)
        {
            spriteRenderer.sprite = Heroes_TeamBlastChaotix;
        }
    }

    // -------------------------------------------------------------------------
    // Power Gauge
    // -------------------------------------------------------------------------

    /// <summary>
    /// Call this whenever the player collects a ring.
    /// </summary>
    public void OnRingCollected()
    {
        AddGauge(gaugePerRing);
    }

    /// <summary>
    /// Call this whenever the player defeats an enemy.
    /// </summary>
    public void OnEnemyDefeated()
    {
        AddGauge(gaugePerEnemy);
    }

    private void AddGauge(float amount)
    {
        if (teamBlastActive) return; // Don't fill gauge during blast

        currentPowerGauge = Mathf.Min(currentPowerGauge + amount, maxPowerGauge);
        Debug.Log($"[TeamBlast] Power Gauge: {currentPowerGauge} / {maxPowerGauge}");
    }

    /// <summary>
    /// Returns a 0–1 normalized gauge value, useful for driving a UI fill bar.
    /// </summary>
    public float GetGaugeNormalized()
    {
        return currentPowerGauge / maxPowerGauge;
    }

    public bool IsGaugeFull()
    {
        return currentPowerGauge >= maxPowerGauge;
    }

    // -------------------------------------------------------------------------
    // Input
    // -------------------------------------------------------------------------

    private void HandleTeamBlastInput()
    {
        // In Sonic Heroes the player presses a dedicated button when the gauge is full.
        // Replace "Fire2" with whatever input you've mapped in your project.
        if (Input.GetButtonDown("Fire2") && IsGaugeFull() && !teamBlastActive)
        {
            StartCoroutine(ExecuteTeamBlast());
        }
    }

    // -------------------------------------------------------------------------
    // Team Blast Execution
    // -------------------------------------------------------------------------

    /// <summary>
    /// Core Team Blast coroutine:
    ///   1. Play SFX and spawn VFX.
    ///   2. Clear all enemies in blast radius.
    ///   3. Grant invincibility.
    ///   4. Apply team-specific bonus effect.
    ///   5. Reset gauge.
    /// </summary>
    private IEnumerator ExecuteTeamBlast()
    {
        teamBlastActive = true;
        Debug.Log($"[TeamBlast] {GetTeamBlastName()} activated!");

        // --- Audio ---
        if (audioSource != null && teamBlastSFX != null)
        {
            audioSource.PlayOneShot(teamBlastSFX);
        }

        // --- VFX ---
        if (teamBlastVFXPrefab != null)
        {
            Instantiate(teamBlastVFXPrefab, transform.position, Quaternion.identity);
        }

        // --- Clear nearby enemies ---
        ClearEnemiesInRadius();

        // --- Invincibility ---
        float invincDuration = GetInvincibilityDuration();
        StartCoroutine(GrantInvincibility(invincDuration));

        // --- Team-specific secondary effect ---
        ApplyTeamSpecificEffect();

        // --- Reset gauge ---
        currentPowerGauge = 0f;
        Debug.Log("[TeamBlast] Power gauge reset.");

        // Short cooldown so the blast animation can play out
        yield return new WaitForSeconds(0.5f);
        teamBlastActive = false;
    }

    /// <summary>
    /// Damages / destroys all enemies within blastRadius using an overlap sphere.
    /// </summary>
    private void ClearEnemiesInRadius()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, blastRadius, enemyLayer);
        Debug.Log($"[TeamBlast] Clearing {hits.Length} enemies in radius {blastRadius}.");

        foreach (Collider hit in hits)
        {
            // Assumes enemies have a component that implements ITakeDamage or similar.
            // Adapt to your own enemy health / destroy logic.
            Destroy(hit.gameObject);
        }
    }

    /// <summary>
    /// Returns how long invincibility lasts for the active team.
    /// Team Dark's Chaos Inferno grants extended invincibility in the original game.
    /// </summary>
    private float GetInvincibilityDuration()
    {
        if (isTeamDark || isTeamSuperSonic)
            return baseInvincibilityDuration * 2f; // Extended for Team Dark / Super Sonic
        return baseInvincibilityDuration;
    }

    private IEnumerator GrantInvincibility(float duration)
    {
        isInvincible = true;
        Debug.Log($"[TeamBlast] Invincibility active for {duration}s.");
        yield return new WaitForSeconds(duration);
        isInvincible = false;
        Debug.Log("[TeamBlast] Invincibility ended.");
    }

    public bool IsInvincible() => isInvincible;

    // -------------------------------------------------------------------------
    // Team-Specific Effects
    // -------------------------------------------------------------------------

    private void ApplyTeamSpecificEffect()
    {
        if (isTeamSonic) SonicOverdriveEffect();
        else if (isTeamDark) ChaosInfernoEffect();
        else if (isTeamRose) FlowerFestivalEffect();
        else if (isTeamChaotix) ChaotixRecitalEffect();
        else if (isTeamSuperSonic) SuperSonicPowerEffect();
    }

    /// <summary>
    /// Sonic Overdrive: brief speed boost for Team Sonic.
    /// Hook into your character movement controller here.
    /// </summary>
    private void SonicOverdriveEffect()
    {
        Debug.Log("[TeamBlast] Sonic Overdrive! Brief speed boost applied.");
        // e.g. playerMovement.ApplySpeedBoost(2f, 3f);
    }

    /// <summary>
    /// Chaos Inferno: extended invincibility (already handled in GetInvincibilityDuration).
    /// Can also stun surviving enemies in the original game.
    /// </summary>
    private void ChaosInfernoEffect()
    {
        Debug.Log("[TeamBlast] Chaos Inferno! Extended invincibility and enemy stun.");
        // e.g. StunSurvivingEnemies();
    }

    /// <summary>
    /// Flower Festival: attracts nearby rings and powers up all three team members.
    /// </summary>
    private void FlowerFestivalEffect()
    {
        Debug.Log("[TeamBlast] Flower Festival! Ring attraction and team power-up active.");
        // e.g. StartCoroutine(AttractRings(5f)); teamPowerUp.Activate();
    }

    /// <summary>
    /// Chaotix Recital: applies a score multiplier and clears the stage of enemies.
    /// </summary>
    private void ChaotixRecitalEffect()
    {
        Debug.Log("[TeamBlast] Chaotix Recital! Score multiplier activated.");
        // e.g. ScoreManager.instance.SetMultiplier(2, 10f);
    }

    /// <summary>
    /// Super Sonic Power: maximum damage, full area clear, extended invincibility.
    /// Only available in the final story.
    /// </summary>
    private void SuperSonicPowerEffect()
    {
        Debug.Log("[TeamBlast] Super Sonic Power! Maximum power unleashed.");
        // e.g. ClearEntireStageEnemies(); ApplyGodMode(10f);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private string GetTeamBlastName()
    {
        if (isTeamSonic) return "Sonic Overdrive";
        if (isTeamDark) return "Chaos Inferno";
        if (isTeamRose) return "Flower Festival";
        if (isTeamChaotix) return "Chaotix Recital";
        if (isTeamSuperSonic) return "Super Sonic Power";
        return "Team Blast";
    }

    // -------------------------------------------------------------------------
    // Debug Gizmos
    // -------------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, blastRadius);
    }
}