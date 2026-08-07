using System.Collections;
using UnityEngine;

/// <summary>
/// EggmanShips — Sonic Heroes, Stages 13 (Egg Fleet) & 14 (Final Fortress).
///
/// Covers three ship archetypes that appear in those stages:
///   • SmallShip      — fast patrol craft, flies a looping waypoint path
///   • SmallShipLow   — same hull but flies low over the deck, acts as a moving platform
///   • MantaRayShip   — large flagship-style carrier, slow, fires homing missiles
///
/// Ships are scene-disabled by default and activated only when the active
/// stage index matches Egg Fleet (13) or Final Fortress (14).
/// </summary>
public class EggmanShips : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // Ship Type
    // ─────────────────────────────────────────────
    public enum ShipType { SmallShip, SmallShipLow, MantaRayShip }

    [Header("Identity")]
    public ShipType shipType = ShipType.SmallShip;

    // ─────────────────────────────────────────────
    // Stage Gating  (stages 13 & 14 only)
    // ─────────────────────────────────────────────
    [Header("Stage Gating")]
    [Tooltip("Stage indices this ship is allowed to appear in.")]
    public int[] allowedStageIndices = new int[] { 13, 14 };

    // ─────────────────────────────────────────────
    // Movement
    // ─────────────────────────────────────────────
    [Header("Movement")]
    public float cruiseSpeed = 8f;          // base patrol speed
    public float hoverHeight = 12f;         // target altitude above the deck
    public float hoverDamping = 4f;         // spring strength for altitude correction
    public float bankAngle = 20f;           // roll into turns (cosmetic tilt)
    public float bankSpeed = 3f;

    [Header("Waypoints")]
    [Tooltip("World-space waypoints the ship patrols. Loops endlessly.")]
    public Transform[] waypoints;
    public float waypointReachRadius = 3f;

    // ─────────────────────────────────────────────
    // Combat (MantaRayShip only)
    // ─────────────────────────────────────────────
    [Header("Combat — MantaRay only")]
    public GameObject missilePrefab;
    public Transform[] missileFirePoints;
    public float missileFireRate = 3f;      // seconds between salvos
    public float missileDetectRange = 30f;
    public LayerMask playerLayer;

    // ─────────────────────────────────────────────
    // Platform (SmallShipLow only)
    // ─────────────────────────────────────────────
    [Header("Platform — SmallShipLow only")]
    [Tooltip("Collider on top of the ship that the player can stand on.")]
    public Collider deckCollider;

    // ─────────────────────────────────────────────
    // Cutscene
    // ─────────────────────────────────────────────
    [Header("Cutscene")]
    [Tooltip("Assign a path for the ship to follow during cutscenes.")]
    public Transform[] cutsceneWaypoints;
    public float cutsceneSpeed = 5f;
    private bool inCutscene = false;

    // ─────────────────────────────────────────────
    // Internal
    // ─────────────────────────────────────────────
    private Rigidbody body;
    private int currentWaypoint = 0;
    private float fireCooldown = 0f;
    private bool isActive = false;          // true only when correct stage loaded
    private float currentBankAngle = 0f;
    private Transform playerTarget;

    // ─────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────
    void Awake()
    {
        body = GetComponent<Rigidbody>();

        // Ships float — disable built-in gravity; we apply hover correction manually
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.freezeRotation = true;

        // Start hidden; StageManager (or your equivalent) calls ActivateForStage()
        gameObject.SetActive(false);
    }

    void Start()
    {
        // Scale speed per ship type to match in-game feel
        switch (shipType)
        {
            case ShipType.SmallShip:
                cruiseSpeed *= 1.4f;    // fast patrol craft
                hoverHeight = 14f;
                break;

            case ShipType.SmallShipLow:
                cruiseSpeed *= 1.0f;    // medium, acts as moving platform
                hoverHeight = 3f;       // skims just above the deck
                if (deckCollider != null)
                    deckCollider.enabled = true;
                break;

            case ShipType.MantaRayShip:
                cruiseSpeed *= 0.5f;    // large, slow flagship
                hoverHeight = 20f;
                break;
        }
    }

    void Update()
    {
        if (!isActive) return;

        if (inCutscene)
        {
            FollowCutscenePath();
            return;
        }

        fireCooldown -= Time.deltaTime;

        // MantaRay: scan for player and fire missiles
        if (shipType == ShipType.MantaRayShip)
        {
            ScanForPlayer();
            if (playerTarget != null && fireCooldown <= 0f)
            {
                StartCoroutine(FireMissileSalvo());
                fireCooldown = missileFireRate;
            }
        }
    }

    void FixedUpdate()
    {
        if (!isActive || inCutscene) return;

        PatrolToWaypoint();
        MaintainHoverHeight();
        ApplyBanking();
    }

    // ─────────────────────────────────────────────
    // Stage Activation
    // ─────────────────────────────────────────────

    /// <summary>
    /// Call this from your StageManager when a stage loads.
    /// Ships silently stay inactive for any other stage index.
    /// </summary>
    public void ActivateForStage(int stageIndex)
    {
        bool allowed = System.Array.IndexOf(allowedStageIndices, stageIndex) >= 0;
        gameObject.SetActive(allowed);
        isActive = allowed;

        if (allowed)
            Debug.Log($"[EggmanShips] {shipType} activated for Stage {stageIndex}.");
    }

    // ─────────────────────────────────────────────
    // Patrol Movement
    // ─────────────────────────────────────────────
    private void PatrolToWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Transform target = waypoints[currentWaypoint];
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f; // horizontal steering only; vertical handled by hover

        float distance = toTarget.magnitude;

        if (distance < waypointReachRadius)
        {
            // Advance to next waypoint, looping
            currentWaypoint = (currentWaypoint + 1) % waypoints.Length;
            return;
        }

        // Rotate smoothly toward the next waypoint
        Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized);
        Quaternion smoothRot = Quaternion.Slerp(
            Quaternion.Euler(0f, transform.eulerAngles.y, 0f),
            targetRot,
            Time.fixedDeltaTime * 3f
        );
        body.MoveRotation(Quaternion.Euler(0f, smoothRot.eulerAngles.y, 0f));

        // Move forward at cruise speed
        Vector3 velocity = transform.forward * cruiseSpeed;
        velocity.y = body.linearVelocity.y; // preserve hover correction
        body.linearVelocity = velocity;
    }

    // ─────────────────────────────────────────────
    // Hover Height Correction
    // ─────────────────────────────────────────────
    private void MaintainHoverHeight()
    {
        RaycastHit hit;
        float currentHeight = 0f;
        bool overSurface = Physics.Raycast(transform.position, Vector3.down, out hit, hoverHeight + 10f);

        if (overSurface)
            currentHeight = hit.distance;
        else
            currentHeight = hoverHeight; // assume already at correct height over open sky

        float heightError = hoverHeight - currentHeight;
        float correctionForce = heightError * hoverDamping;

        Vector3 vel = body.linearVelocity;
        vel.y = Mathf.Lerp(vel.y, correctionForce, Time.fixedDeltaTime * hoverDamping);
        body.linearVelocity = vel;
    }

    // ─────────────────────────────────────────────
    // Banking (cosmetic roll into turns)
    // ─────────────────────────────────────────────
    private void ApplyBanking()
    {
        float turnRate = body.angularVelocity.y;
        float targetBank = -turnRate * bankAngle;
        currentBankAngle = Mathf.Lerp(currentBankAngle, targetBank, Time.fixedDeltaTime * bankSpeed);

        Vector3 euler = transform.eulerAngles;
        euler.z = currentBankAngle;
        transform.eulerAngles = euler;
    }

    // ─────────────────────────────────────────────
    // Combat — MantaRay missile system
    // ─────────────────────────────────────────────
    private void ScanForPlayer()
    {
        // Simple sphere overlap — replace with your game's player reference system as needed
        Collider[] hits = Physics.OverlapSphere(transform.position, missileDetectRange, playerLayer);
        playerTarget = hits.Length > 0 ? hits[0].transform : null;
    }

    private IEnumerator FireMissileSalvo()
    {
        if (missilePrefab == null || missileFirePoints == null) yield break;

        foreach (Transform firePoint in missileFirePoints)
        {
            if (firePoint == null) continue;

            GameObject missile = Instantiate(missilePrefab, firePoint.position, firePoint.rotation);

            // Pass the target to the missile if it has a homing component
            HomingMissile homing = missile.GetComponent<HomingMissile>();
            if (homing != null && playerTarget != null)
                homing.SetTarget(playerTarget);

            yield return new WaitForSeconds(0.25f); // slight stagger between fire points
        }
    }

    // ─────────────────────────────────────────────
    // Cutscene Playback
    // ─────────────────────────────────────────────

    /// <summary>
    /// Call from your cutscene director to start cutscene movement.
    /// </summary>
    public void StartCutscene()
    {
        inCutscene = true;
        currentWaypoint = 0;
        body.linearVelocity = Vector3.zero;
    }

    public void EndCutscene()
    {
        inCutscene = false;
        currentWaypoint = 0;
    }

    private void FollowCutscenePath()
    {
        if (cutsceneWaypoints == null || cutsceneWaypoints.Length == 0) return;

        Transform target = cutsceneWaypoints[currentWaypoint];
        Vector3 toTarget = target.position - transform.position;
        float distance = toTarget.magnitude;

        if (distance < 0.5f)
        {
            currentWaypoint++;
            if (currentWaypoint >= cutsceneWaypoints.Length)
            {
                EndCutscene();
                return;
            }
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            target.position,
            cutsceneSpeed * Time.deltaTime
        );

        if (toTarget != Vector3.zero)
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(toTarget.normalized),
                Time.deltaTime * 5f
            );
    }

    // ─────────────────────────────────────────────
    // Collision — player lands on SmallShipLow deck
    // ─────────────────────────────────────────────
    private void OnCollisionStay(Collision collision)
    {
        if (shipType != ShipType.SmallShipLow) return;

        // Carry the player with the ship while they stand on it
        UltimatePlayerMovement player = collision.gameObject.GetComponentInParent<UltimatePlayerMovement>();
        if (player == null) return;

        // Push the player along with the ship's velocity so they don't slide off
        Rigidbody playerBody = collision.rigidbody;
        if (playerBody != null)
        {
            Vector3 shipHorizontalVel = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
            playerBody.linearVelocity = new Vector3(
                shipHorizontalVel.x,
                playerBody.linearVelocity.y,
                shipHorizontalVel.z
            );
        }
    }

    // ─────────────────────────────────────────────
    // Debug Gizmos
    // ─────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        // Patrol path
        if (waypoints != null && waypoints.Length > 1)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                int next = (i + 1) % waypoints.Length;
                if (waypoints[next] != null)
                    Gizmos.DrawLine(waypoints[i].position, waypoints[next].position);
                Gizmos.DrawWireSphere(waypoints[i].position, waypointReachRadius);
            }
        }

        // Missile detect range (MantaRay)
        if (shipType == ShipType.MantaRayShip)
        {
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, missileDetectRange);
        }

        // Hover height ray
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * hoverHeight);
    }
}

// Minimal HomingMissile component to satisfy references from EggmanShips.
// If your project already has a richer HomingMissile implementation, remove this stub and keep yours.
public class HomingMissile : MonoBehaviour
{
    public Transform Target { get; private set; }
    public float speed = 20f;
    public float rotateSpeed = 5f;
    public float lifetime = 10f;

    public void SetTarget(Transform target)
    {
        Target = target;
    }

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (Target == null)
        {
            transform.position += transform.forward * speed * Time.deltaTime;
            return;
        }

        Vector3 dir = (Target.position - transform.position).normalized;
        if (dir == Vector3.zero) return;

        Quaternion look = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, rotateSpeed * Time.deltaTime);
        transform.position += transform.forward * speed * Time.deltaTime;
    }
}