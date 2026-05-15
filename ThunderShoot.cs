using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ThunderShoot - Replicates Tails' Thunder Shoot ability from Sonic Heroes.
/// 
/// HOW IT WORKS (matching Sonic Heroes behaviour):
///   1. Hold the attack button (X) to enter lock-on mode.
///   2. While held, nearby enemies within lockOnRange enter a lock-on queue (up to maxLockOns).
///   3. Each locked target gets a visual lock-on indicator / spark VFX.
///   4. Release the button → fire one homing Thunder missile per locked target simultaneously.
///   5. Each missile tracks its target with a gentle arc, dealing thunderDamage on impact.
///   6. After firing, a cooldown prevents re-use.
///   7. If no targets were locked, a straight "stray bolt" fires in the facing direction.
///
/// SETUP IN INSPECTOR:
///   - flyCharacter        : The Tails GameObject (for transform/direction reference)
///   - thunderMissilePrefab: A prefab with a Rigidbody + Collider (the homing bolt)
///   - lockOnIndicatorPrefab: Optional UI/world-space lock reticle prefab
///   - lockOnRange         : Sphere radius for auto-targeting (default 15)
///   - maxLockOns          : Max simultaneous targets (default 6, matching Heroes)
///   - chargeTime          : Seconds held before missiles fully charge (default 1.2)
///   - thunderDamage       : Damage per missile (default 3)
///   - cooldownTime        : Seconds before Thunder Shoot can fire again (default 2)
///   - missileSpeed        : Homing missile movement speed (default 18)
///   - homingStrength      : How tightly missiles track targets (default 5)
///   - enemyLayerMask      : LayerMask for enemy detection
/// </summary>
public class ThunderShoot : MonoBehaviour
{
    // ── Inspector-exposed fields ─────────────────────────────────────────────

    [Header("References")]
    public GameObject flyCharacter;
    public GameObject thunderMissilePrefab;      // homing bolt prefab
    public GameObject lockOnIndicatorPrefab;     // optional reticle prefab (can be null)
    public GameObject chargeVFXPrefab;           // spark / charge effect around Tails
    public AudioClip lockOnSound;
    public AudioClip chargeSound;
    public AudioClip fireSound;
    public AudioClip impactSound;

    [Header("Targeting")]
    public float lockOnRange = 15f;
    public int maxLockOns = 6;                   // Heroes locks up to ~6 targets
    public LayerMask enemyLayerMask = ~0;        // set to your Enemy layer

    [Header("Charging")]
    public float chargeTime = 1.2f;              // seconds to hold for full charge
    public bool isShooting = false;              // public so other scripts can read state

    [Header("Damage & Stats")]
    public float thunderDamage = 3f;
    public float missileSpeed = 18f;
    public float homingStrength = 5f;            // angular tracking per second

    [Header("Timing")]
    public float cooldownTime = 2f;
    public float shootingDirection = 0f;         // angle offset if needed

    // ── Private state ────────────────────────────────────────────────────────

    private float shooting = 0f;                 // current charge accumulator
    private float shootingSpeed = 0f;            // unused legacy; kept for compatibility
    private float thunderStrike = 0f;            // time since last fire
    private bool onCooldown = false;

    private bool isCharging = false;
    private float chargeAccumulator = 0f;

    private List<Transform> lockedTargets = new List<Transform>();
    private List<GameObject> lockIndicators = new List<GameObject>();

    private AudioSource audioSource;
    private GameObject activeChargeVFX;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    void Start()
    {
        // Rigidbody reference kept (matches original; may be used by parent class)
        Rigidbody rigidbody = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {
        // Cooldown timer
        if (onCooldown)
        {
            thunderStrike += Time.deltaTime;
            if (thunderStrike >= cooldownTime)
            {
                thunderStrike = 0f;
                onCooldown = false;
            }
        }

        CallThunderShoot();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Main Thunder Shoot controller — call every frame (called automatically from Update).
    /// Can also be called externally by an input manager.
    /// </summary>
    public void CallThunderShoot()
    {
        // Must have the Player tag on the fly character to activate
        if (flyCharacter == null || !flyCharacter.CompareTag("Player")) return;
        if (onCooldown) return;

        // ── Hold: enter charging + lock-on phase ────────────────────────────
        if (Input.GetKey(KeyCode.X))
        {
            if (!isCharging)
                BeginCharge();

            chargeAccumulator += Time.deltaTime;
            shooting = Mathf.Clamp01(chargeAccumulator / chargeTime);

            // Continuously scan for new targets while holding
            ScanForTargets();
            isShooting = true;
        }

        // ── Release: fire all locked missiles ───────────────────────────────
        if (Input.GetKeyUp(KeyCode.X) && isCharging)
        {
            StartCoroutine(ThunderShootAttack(flyCharacter));
        }
    }

    // ── Core coroutine ───────────────────────────────────────────────────────

    /// <summary>
    /// Fires Thunder missiles at every locked target simultaneously, then resets state.
    /// Matches Heroes: all missiles launch at once with a slight stagger for visual clarity.
    /// </summary>
    public IEnumerator ThunderShootAttack(GameObject flyCharacter)
    {
        // Activate follower indicators on UltimatePlayerMovement if present
        UltimatePlayerMovement upm = GetComponent<UltimatePlayerMovement>();
        if (upm != null)
        {
            upm.leftFollower.SetActive(true);
            upm.rightFollower.SetActive(true);
        }

        PlaySound(fireSound);

        // Spawn a missile for each locked target
        if (lockedTargets.Count > 0)
        {
            for (int i = 0; i < lockedTargets.Count; i++)
            {
                Transform target = lockedTargets[i];
                if (target == null) continue;

                SpawnMissile(target);

                // Tiny stagger between missiles for visual readability
                if (i < lockedTargets.Count - 1)
                    yield return new WaitForSeconds(0.05f);
            }
        }
        else
        {
            // No lock-ons — fire a straight "stray bolt" forward
            SpawnStrayBolt();
        }

        ClearLockOns();
        EndCharge();

        onCooldown = true;
        isShooting = false;

        yield return null;
    }

    // ── Charging helpers ─────────────────────────────────────────────────────

    private void BeginCharge()
    {
        isCharging = true;
        chargeAccumulator = 0f;
        PlaySound(chargeSound);

        if (chargeVFXPrefab != null)
        {
            Transform spawnPoint = flyCharacter != null ? flyCharacter.transform : transform;
            activeChargeVFX = Instantiate(chargeVFXPrefab, spawnPoint.position, Quaternion.identity, spawnPoint);
        }
    }

    private void EndCharge()
    {
        isCharging = false;
        chargeAccumulator = 0f;
        shooting = 0f;

        if (activeChargeVFX != null)
        {
            Destroy(activeChargeVFX);
            activeChargeVFX = null;
        }

        // Deactivate followers
        UltimatePlayerMovement upm = GetComponent<UltimatePlayerMovement>();
        if (upm != null)
        {
            upm.leftFollower.SetActive(false);
            upm.rightFollower.SetActive(false);
        }
    }

    // ── Target scanning ──────────────────────────────────────────────────────

    /// <summary>
    /// Sphere-cast around Tails, add new enemies to the lock list until maxLockOns reached.
    /// </summary>
    private void ScanForTargets()
    {
        if (lockedTargets.Count >= maxLockOns) return;

        Transform origin = flyCharacter != null ? flyCharacter.transform : transform;
        Collider[] hits = Physics.OverlapSphere(origin.position, lockOnRange, enemyLayerMask);

        foreach (Collider col in hits)
        {
            if (lockedTargets.Count >= maxLockOns) break;

            Transform t = col.transform;
            if (lockedTargets.Contains(t)) continue;

            // Simple line-of-sight check
            Vector3 dir = (t.position - origin.position).normalized;
            if (!Physics.Raycast(origin.position, dir, out RaycastHit los, lockOnRange) ||
                los.transform != t)
                continue;

            AddLockOn(t);
        }
    }

    private void AddLockOn(Transform target)
    {
        lockedTargets.Add(target);
        PlaySound(lockOnSound);

        // Spawn lock-on reticle attached to the target
        if (lockOnIndicatorPrefab != null)
        {
            GameObject indicator = Instantiate(lockOnIndicatorPrefab,
                                               target.position + Vector3.up * 0.5f,
                                               Quaternion.identity,
                                               target);
            lockIndicators.Add(indicator);
        }
        else
        {
            lockIndicators.Add(null); // keep list lengths in sync
        }
    }

    private void ClearLockOns()
    {
        foreach (GameObject ind in lockIndicators)
            if (ind != null) Destroy(ind);

        lockIndicators.Clear();
        lockedTargets.Clear();
    }

    // ── Missile spawning ─────────────────────────────────────────────────────

    private void SpawnMissile(Transform target)
    {
        if (thunderMissilePrefab == null) return;

        Transform origin = flyCharacter != null ? flyCharacter.transform : transform;
        Vector3 spawnPos = origin.position + origin.forward * 1.2f + Vector3.up * 0.3f;

        GameObject missile = Instantiate(thunderMissilePrefab, spawnPos, origin.rotation);

        ThunderMissile tm = missile.GetComponent<ThunderMissile>();
        if (tm == null)
            tm = missile.AddComponent<ThunderMissile>();

        tm.Initialize(target, missileSpeed, homingStrength, thunderDamage, impactSound);
    }

    private void SpawnStrayBolt()
    {
        if (thunderMissilePrefab == null) return;

        Transform origin = flyCharacter != null ? flyCharacter.transform : transform;
        Vector3 spawnPos = origin.position + origin.forward * 1.2f + Vector3.up * 0.3f;

        // Apply optional direction offset
        Quaternion rot = origin.rotation * Quaternion.Euler(0f, shootingDirection, 0f);
        GameObject bolt = Instantiate(thunderMissilePrefab, spawnPos, rot);

        ThunderMissile tm = bolt.GetComponent<ThunderMissile>();
        if (tm == null)
            tm = bolt.AddComponent<ThunderMissile>();

        // No target — flies straight
        tm.Initialize(null, missileSpeed, 0f, thunderDamage, impactSound);
    }

    // ── Utility ──────────────────────────────────────────────────────────────

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }

    /// <summary>Draws the lock-on sphere in the editor for easy tuning.</summary>
    void OnDrawGizmosSelected()
    {
        Transform origin = flyCharacter != null ? flyCharacter.transform : transform;
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.25f);
        Gizmos.DrawSphere(origin.position, lockOnRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin.position, lockOnRange);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// ThunderMissile — attached at runtime to each spawned bolt
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Self-contained homing missile component.
/// Instantiated by ThunderShoot; tracks its assigned target until impact or timeout.
/// </summary>
public class ThunderMissile : MonoBehaviour
{
    private Transform target;
    private float speed;
    private float homingStrength;
    private float damage;
    private AudioClip impactSound;

    private float lifetime = 6f;            // auto-destroy after 6 s if no hit
    private float elapsed = 0f;
    private bool hasHit = false;

    /// <summary>Called by ThunderShoot immediately after Instantiate.</summary>
    public void Initialize(Transform target, float speed, float homingStrength,
                           float damage, AudioClip impactSound)
    {
        this.target = target;
        this.speed = speed;
        this.homingStrength = homingStrength;
        this.damage = damage;
        this.impactSound = impactSound;
    }

    void Update()
    {
        if (hasHit) return;

        elapsed += Time.deltaTime;
        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        // ── Homing steering ─────────────────────────────────────────────────
        if (target != null)
        {
            Vector3 dirToTarget = (target.position - transform.position).normalized;
            Quaternion targetRot = Quaternion.LookRotation(dirToTarget);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                                                  homingStrength * Time.deltaTime);
        }

        transform.position += transform.forward * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;

        // Don't hit the player
        if (other.CompareTag("Player")) return;

        hasHit = true;

        // Apply damage if the hit object has a health component
        // Adapt the interface name below to match your game's health system
        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
            damageable.TakeDamage(damage);

        // Play impact sound at world position
        if (impactSound != null)
            AudioSource.PlayClipAtPoint(impactSound, transform.position);

        Destroy(gameObject);
    }
}

// ── Minimal damage interface — implement on your enemy health scripts ────────
public interface IDamageable
{
    void TakeDamage(float amount);
}

// ── Stub so the file compiles without UltimatePlayerMovement in the project ─
// Remove this block once UltimatePlayerMovement is present in your project.
#if !ULTIMATE_PLAYER_MOVEMENT_DEFINED
public class UltimatePlayerMovement : MonoBehaviour
{
    public GameObject leftFollower;
    public GameObject rightFollower;
}
#endif