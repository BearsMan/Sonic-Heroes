using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Light Speed Dash - Sonic Heroes style
///
/// How to use:
///   1. Attach this script to your Sonic character GameObject.
///   2. Assign a Ring prefab/tag — rings in your scene must have the tag "Ring".
///   3. Assign the optional VFX references in the Inspector.
///   4. Call TryActivate() from your input handler when the player presses the LSD button.
///
/// Behaviour matches Sonic Heroes:
///   - Sonic locks on to the nearest ring within lockOnRadius.
///   - He dashes through rings in a chain, collecting each one as he passes.
///   - If the next ring in the chain is within chainRadius of the last collected ring
///     the dash continues; otherwise it ends.
///   - While dashing, gravity and normal movement are suppressed.
///   - A charge hold (holdToCharge) mode is supported: hold the button near a ring
///     trail and release to dash, replicating the Sonic Adventure / Heroes feel.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class LightSpeedDash : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector-exposed settings
    // -------------------------------------------------------------------------

    [Header("Detection")]
    [Tooltip("How close Sonic must be to a ring to begin a dash.")]
    public float lockOnRadius = 4f;

    [Tooltip("Max distance between consecutive rings to continue the chain.")]
    public float chainRadius = 5f;

    [Tooltip("Tag used to identify collectible rings in the scene.")]
    public string ringTag = "Ring";

    [Header("Dash Movement")]
    [Tooltip("Speed Sonic travels between rings (units per second).")]
    public float dashSpeed = 40f;

    [Tooltip("Minimum speed — Sonic never moves slower than this along the chain.")]
    public float minDashSpeed = 20f;

    [Tooltip("How quickly Sonic rotates to face the next ring.")]
    public float rotationSpeed = 720f;

    [Header("Charge Mode")]
    [Tooltip("If true, the player must HOLD the button to charge and RELEASE to dash (Heroes style). "
           + "If false, pressing the button once triggers the dash immediately.")]
    public bool holdToCharge = true;

    [Tooltip("Time the player must hold the button before the dash is ready (seconds).")]
    public float chargeTime = 0.3f;

    [Header("Visual Effects")]
    [Tooltip("Particle system or GameObject activated while dashing.")]
    public GameObject dashVFX;

    [Tooltip("Optional speed-line / motion-blur effect enabled during the dash.")]
    public GameObject speedLineVFX;

    [Tooltip("Trail renderer on Sonic — enabled during the dash.")]
    public TrailRenderer dashTrail;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip chargeSFX;
    public AudioClip dashSFX;
    public AudioClip collectRingSFX;
    public AudioClip dashEndSFX;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private Rigidbody rb;
    private bool isDashing = false;
    private bool isCharging = false;
    private float chargeTimer = 0f;

    // The ordered list of rings that make up the current dash path.
    private List<Transform> ringChain = new List<Transform>();
    private int currentRingIndex = -1;

    // Cached position of the ring Sonic just left — used to find the next ring.
    private Vector3 lastRingPosition;

    // Original gravity scale / kinematic state so we can restore them.
    private bool wasKinematic = false;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (isDashing) return; // Movement handled in FixedUpdate during a dash.

        HandleChargeInput();
    }

    void FixedUpdate()
    {
        if (isDashing)
        {
            AdvanceDash();
        }
    }

    // -------------------------------------------------------------------------
    // Public API — call these from your input / player-controller script
    // -------------------------------------------------------------------------

    /// <summary>
    /// Call every frame while the LSD button is held (holdToCharge = true) or
    /// once when the button is pressed (holdToCharge = false).
    /// </summary>
    public void OnLSDButtonHeld()
    {
        if (isDashing) return;

        if (!holdToCharge)
        {
            TryActivate();
            return;
        }

        if (!isCharging)
            BeginCharge();
    }

    /// <summary>
    /// Call when the LSD button is released (only meaningful if holdToCharge = true).
    /// </summary>
    public void OnLSDButtonReleased()
    {
        if (!holdToCharge || !isCharging) return;

        if (chargeTimer >= chargeTime)
            TryActivate();
        else
            CancelCharge();
    }

    /// <summary>
    /// Immediately attempt to begin a Light Speed Dash from the current position.
    /// </summary>
    public void TryActivate()
    {
        CancelCharge();

        if (isDashing) return;

        // Build the ring chain starting from the nearest ring.
        ringChain.Clear();
        Transform firstRing = FindNearestRing(transform.position, lockOnRadius);

        if (firstRing == null)
            return; // No rings in range — abort.

        BuildChain(firstRing);

        if (ringChain.Count == 0)
            return;

        StartDash();
    }

    // -------------------------------------------------------------------------
    // Charging
    // -------------------------------------------------------------------------

    private void HandleChargeInput()
    {
        // If the developer is using Unity's new Input System or a custom wrapper
        // they should call OnLSDButtonHeld() / OnLSDButtonReleased() directly.
        // This block handles the legacy Input Manager as a convenience.
        // Remove or replace as needed.
        if (Input.GetButton("Fire2")) // Map "Fire2" to your LSD button.
            OnLSDButtonHeld();
        else if (Input.GetButtonUp("Fire2"))
            OnLSDButtonReleased();

        if (isCharging)
        {
            chargeTimer += Time.deltaTime;

            // Play charge SFX once ready.
            if (chargeTimer >= chargeTime && audioSource != null && chargeSFX != null)
            {
                if (!audioSource.isPlaying)
                    audioSource.PlayOneShot(chargeSFX);
            }
        }
    }

    private void BeginCharge()
    {
        isCharging = true;
        chargeTimer = 0f;
    }

    private void CancelCharge()
    {
        isCharging = false;
        chargeTimer = 0f;
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    // -------------------------------------------------------------------------
    // Ring discovery
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the nearest active ring within <paramref name="searchRadius"/> of
    /// <paramref name="origin"/>, or null if none found.
    /// </summary>
    private Transform FindNearestRing(Vector3 origin, float searchRadius)
    {
        GameObject[] rings = GameObject.FindGameObjectsWithTag(ringTag);
        Transform nearest = null;
        float nearestDist = searchRadius * searchRadius; // Compare squared distances.

        foreach (GameObject ring in rings)
        {
            if (!ring.activeInHierarchy) continue;

            float sqDist = (ring.transform.position - origin).sqrMagnitude;
            if (sqDist < nearestDist)
            {
                nearestDist = sqDist;
                nearest = ring.transform;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Builds an ordered chain of rings starting from <paramref name="first"/>
    /// by greedily finding the nearest uncollected ring within chainRadius of
    /// the previous ring. This mirrors how Sonic Heroes constructs ring rails.
    /// </summary>
    private void BuildChain(Transform first)
    {
        HashSet<Transform> visited = new HashSet<Transform>();
        Transform current = first;

        while (current != null)
        {
            ringChain.Add(current);
            visited.Add(current);

            // Find the next unvisited ring closest to `current` within chainRadius.
            GameObject[] rings = GameObject.FindGameObjectsWithTag(ringTag);
            Transform next = null;
            float nearestSqDist = chainRadius * chainRadius;

            foreach (GameObject ring in rings)
            {
                if (!ring.activeInHierarchy) continue;
                if (visited.Contains(ring.transform)) continue;

                float sqDist = (ring.transform.position - current.position).sqrMagnitude;
                if (sqDist < nearestSqDist)
                {
                    nearestSqDist = sqDist;
                    next = ring.transform;
                }
            }

            current = next;
        }
    }

    // -------------------------------------------------------------------------
    // Dash execution
    // -------------------------------------------------------------------------

    private void StartDash()
    {
        isDashing = true;
        currentRingIndex = 0;
        lastRingPosition = transform.position;

        // Suppress normal physics during the dash.
        wasKinematic = rb.isKinematic;
        rb.isKinematic = true;

        // Enable VFX / SFX.
        SetVFXActive(true);
        if (audioSource != null && dashSFX != null)
            audioSource.PlayOneShot(dashSFX);
    }

    /// <summary>
    /// Called every FixedUpdate while isDashing == true.
    /// Moves Sonic toward the current target ring, collecting it upon arrival,
    /// then advances to the next ring in the chain.
    /// </summary>
    private void AdvanceDash()
    {
        if (currentRingIndex >= ringChain.Count)
        {
            EndDash();
            return;
        }

        Transform targetRing = ringChain[currentRingIndex];

        // Ring may have been collected by something else between frames.
        if (targetRing == null || !targetRing.gameObject.activeInHierarchy)
        {
            currentRingIndex++;
            return;
        }

        Vector3 toRing = targetRing.position - transform.position;
        float distanceToRing = toRing.magnitude;
        float step = dashSpeed * Time.fixedDeltaTime;

        // Smoothly rotate toward the next ring.
        if (toRing != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(toRing.normalized);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }

        if (step >= distanceToRing)
        {
            // Snap to the ring and collect it.
            transform.position = targetRing.position;
            CollectRing(targetRing);
            lastRingPosition = transform.position;
            currentRingIndex++;
        }
        else
        {
            // Move toward the ring.
            transform.position += toRing.normalized * step;
        }
    }

    private void CollectRing(Transform ring)
    {
        // Notify the ring that it has been collected.
        // Assumes rings have a Collectible component with a Collect() method.
        // Replace this block with whatever your project uses.
        var collectible = ring.GetComponent<RingCollectible>();
        if (collectible != null)
            collectible.Collect();
        else
            ring.gameObject.SetActive(false); // Fallback: simply hide the ring.

        if (audioSource != null && collectRingSFX != null)
            audioSource.PlayOneShot(collectRingSFX, 0.5f);
    }

    private void EndDash()
    {
        isDashing = false;

        // Restore physics state.
        rb.isKinematic = wasKinematic;

        // Give Sonic forward momentum at the end of the dash so it feels fluid.
        rb.linearVelocity = transform.forward * Mathf.Max(dashSpeed * 0.5f, minDashSpeed);

        // Disable VFX.
        SetVFXActive(false);

        if (audioSource != null && dashEndSFX != null)
            audioSource.PlayOneShot(dashEndSFX);

        ringChain.Clear();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void SetVFXActive(bool active)
    {
        if (dashVFX != null) dashVFX.SetActive(active);
        if (speedLineVFX != null) speedLineVFX.SetActive(active);
        if (dashTrail != null) dashTrail.emitting = active;
    }

    // -------------------------------------------------------------------------
    // Debug visualisation (Editor only)
    // -------------------------------------------------------------------------

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Lock-on radius.
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, lockOnRadius);

        // Chain radius (shown relative to transform for reference).
        Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, chainRadius);

        // Draw the current chain if dashing.
        if (ringChain != null && ringChain.Count > 1)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < ringChain.Count - 1; i++)
            {
                if (ringChain[i] != null && ringChain[i + 1] != null)
                    Gizmos.DrawLine(ringChain[i].position, ringChain[i + 1].position);
            }
        }
    }
#endif
}

// =============================================================================
// Minimal RingCollectible stub — replace with your own ring script.
// =============================================================================

/// <summary>
/// Attach this to every ring prefab in the scene.
/// Replace the body of Collect() with your own ring-collection logic
/// (incrementing a ring counter, playing effects, etc.).
/// </summary>
public class RingCollectible : MonoBehaviour
{
    [Tooltip("Points awarded when this ring is collected.")]
    public int value = 1;

    private bool collected;

    public void Collect()
    {
        if (collected) return;
        collected = true;

        // TODO: award rings to the player, play effects, etc.
        // Example: GameManager.Instance.AddRings(value);

        gameObject.SetActive(false);
    }
}