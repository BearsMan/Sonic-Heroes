using System.Collections;
using System.Collections.Generic;
using Assets.Scripts; // replace with the real namespace where Breakable lives
using UnityEngine;

// Orca chase sequence - Seaside Hill, Sonic Heroes (2003)
// The orca bursts from the water, chases the player forward, and jumps at set trigger points.
// It is NOT player-controlled. It follows a forward path and destroys breakable obstacles.
public class Orca : MonoBehaviour
{
    [Header("Chase Settings")]
    public float chaseSpeed = 18.0f;          // Forward chase speed (fast, threatening)
    public float acceleration = 2.5f;         // How quickly it reaches chase speed
    public Transform[] waypointPath;          // Path the orca follows through the level
    public bool isChasing = false;            // Set true by trigger to begin the sequence

    [Header("Jump Settings")]
    public float jumpHeight = 6.0f;           // Height of breach jumps at set points
    public float jumpSpeed = 12.0f;           // Speed of jump arc
    public float jumpForwardDistance = 10.0f; // How far forward it travels during a jump

    [Header("Destruction")]
    public LayerMask breakableLayer;          // Layer for boardwalk/wall obstacles it smashes
    public float destroyRadius = 3.0f;        // Radius to break nearby breakables on impact

    [Header("Audio / Feedback")]
    public AudioClip breachSound;
    public AudioClip impactSound;
    public AudioSource audioSource;

    // Internal state
    private int currentWaypoint = 0;
    private bool isJumping = false;
    private bool reachTop = false;
    private float currentSpeed = 0.0f;
    private Vector3 topPosition, bottomPosition, jumpTarget;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // Orca moves kinematically along its path — physics are not used for locomotion.
        // Buoyancy/floating is removed: it emerges dramatically and chases, not bobs.
        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }

    void Update()
    {
        if (!isChasing) return;

        if (isJumping)
        {
            HandleJump();
        }
        else
        {
            ChaseAlongPath();
        }
    }

    // Called by a trigger volume when the player enters the chase zone
    public void BeginChase()
    {
        isChasing = true;
        currentWaypoint = 0;
        currentSpeed = 0.0f;
    }

    // Moves the orca forward along waypoints at increasing speed
    private void ChaseAlongPath()
    {
        if (waypointPath == null || waypointPath.Length == 0) return;

        // Accelerate up to chase speed
        currentSpeed = Mathf.MoveTowards(currentSpeed, chaseSpeed, acceleration * Time.deltaTime);

        Transform target = waypointPath[currentWaypoint];
        Vector3 direction = (target.position - transform.position).normalized;

        // Face the direction of travel
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction),
                Time.deltaTime * 8.0f
            );
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            target.position,
            currentSpeed * Time.deltaTime
        );

        // Advance to next waypoint
        if (Vector3.Distance(transform.position, target.position) < 0.2f)
        {
            currentWaypoint++;
            if (currentWaypoint >= waypointPath.Length)
            {
                isChasing = false; // Sequence complete
            }
        }
    }

    // Triggers a breach jump (called by waypoint or external trigger)
    public void TriggerJump()
    {
        if (isJumping) return;

        bottomPosition = transform.position;
        topPosition = new Vector3(
            transform.position.x,
            transform.position.y + jumpHeight,
            transform.position.z + jumpForwardDistance
        );
        jumpTarget = topPosition;
        reachTop = false;
        isJumping = true;

        if (audioSource != null && breachSound != null)
            audioSource.PlayOneShot(breachSound);
    }

    // Arcs the orca up and forward, then back down — one-way (no return to origin)
    private void HandleJump()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            jumpTarget,
            jumpSpeed * Time.deltaTime
        );

        if (!reachTop && Vector3.Distance(transform.position, topPosition) < 0.1f)
        {
            // Reached peak — now plunge down to the landing point
            jumpTarget = new Vector3(
                topPosition.x + jumpForwardDistance,
                bottomPosition.y,
                topPosition.z
            );
            reachTop = true;
        }

        if (reachTop && Vector3.Distance(transform.position, jumpTarget) < 0.1f)
        {
            isJumping = false;
            OnLand();
        }
    }

    // Smashes breakables on landing, matching the boardwalk destruction in Seaside Hill
    private void OnLand()
    {
        if (audioSource != null && impactSound != null)
            audioSource.PlayOneShot(impactSound);

        Collider[] hits = Physics.OverlapSphere(transform.position, destroyRadius, breakableLayer);
        foreach (Collider hit in hits)
        {
            if (hit.TryGetComponent<Breakable>(out Breakable breakable))
            {
                breakable.Break();
            }
            else
            {
                Destroy(hit.gameObject); // fallback
            }
        }
    }

    // Visualise destroy radius and path in the editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, destroyRadius);

        if (waypointPath != null)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < waypointPath.Length - 1; i++)
            {
                if (waypointPath[i] != null && waypointPath[i + 1] != null)
                    Gizmos.DrawLine(waypointPath[i].position, waypointPath[i + 1].position);
            }
        }
    }
}

namespace Assets.Scripts
{
    public class Breakable : MonoBehaviour
    {
        // Called by Orca when it smashes this object
        public void Break()
        {
            // Add destruction/FX logic here. Minimal safe fallback:
            Destroy (gameObject);
        }
    }
}