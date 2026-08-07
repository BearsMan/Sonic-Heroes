using UnityEngine;

namespace SonicHeroes
{
    [RequireComponent(typeof(Rigidbody))]
    public class Orca : MonoBehaviour
    {
        [Header("Swim Settings")]
        public float swimSpeed = 14.0f;
        public float chaseSpeed = 20.0f;
        public float turnSpeed = 6.0f;
        public float acceleration = 3.0f;

        [Header("Path")]
        public Transform[] waypointPath;
        private int currentWaypoint = 0;

        [Header("Jump Settings")]
        public float jumpHeight = 7.0f;
        public float jumpForwardDistance = 12.0f;
        public float jumpSpeed = 14.0f;

        [Header("Destruction")]
        public LayerMask breakableLayer;
        public float destroyRadius = 3.5f;

        [Header("Animation")]
        public Animator anim;
        private readonly int SwimHash = Animator.StringToHash("Swim");
        private readonly int BreachHash = Animator.StringToHash("Breach");
        private readonly int LandHash = Animator.StringToHash("Land");
        private readonly int SpeedHash = Animator.StringToHash("Speed");

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip swimLoop;
        public AudioClip breachSound;
        public AudioClip impactSound;

        [Header("FX")]
        public GameObject splashFX;
        public GameObject landingSplashFX;

        // Internal state
        private Rigidbody rb;
        private bool isChasing = false;
        private bool isJumping = false;
        private bool reachedPeak = false;
        private float currentSpeed = 0f;

        private Vector3 bottomPos;
        private Vector3 peakPos;
        private Vector3 landingPos;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            if (anim != null)
                anim.Play(SwimHash);

            if (audioSource != null && swimLoop != null)
            {
                audioSource.loop = true;
                audioSource.clip = swimLoop;
                audioSource.Play();
            }
        }

        void Update()
        {
            if (!isChasing) return;

            if (isJumping)
                HandleJump();
            else
                SwimAlongPath();
        }

        public void BeginChase()
        {
            isChasing = true;
            currentWaypoint = 0;
            currentSpeed = 0f;
            isJumping = false;
            reachedPeak = false;
        }

        private void SwimAlongPath()
        {
            if (waypointPath == null || waypointPath.Length == 0)
                return;

            if (currentWaypoint >= waypointPath.Length)
                return;

            currentSpeed = Mathf.MoveTowards(currentSpeed, chaseSpeed, acceleration * Time.deltaTime);

            if (anim != null)
                anim.SetFloat(SpeedHash, currentSpeed / chaseSpeed);

            Transform target = waypointPath[currentWaypoint];
            Vector3 toTarget = target.position - transform.position;
            Vector3 direction = toTarget.normalized;

            if (direction != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }

            // MoveTowards prevents overshooting the waypoint when swimming fast.
            transform.position = Vector3.MoveTowards(
                transform.position, target.position, currentSpeed * Time.deltaTime);

            if (toTarget.sqrMagnitude < 1.0f)
                currentWaypoint++;
        }

        public void TriggerJump()
        {
            if (isJumping) return;

            isJumping = true;
            reachedPeak = false;

            bottomPos = transform.position;
            peakPos = bottomPos + transform.up * jumpHeight + transform.forward * jumpForwardDistance;
            landingPos = bottomPos + transform.forward * (jumpForwardDistance * 2f);

            if (anim != null)
                anim.SetTrigger(BreachHash);

            if (audioSource != null && breachSound != null)
                audioSource.PlayOneShot(breachSound);

            if (splashFX != null)
                Instantiate(splashFX, bottomPos, Quaternion.identity);
        }

        private void HandleJump()
        {
            float step = jumpSpeed * Time.deltaTime;

            if (!reachedPeak)
            {
                transform.position = Vector3.MoveTowards(transform.position, peakPos, step);

                if (Vector3.Distance(transform.position, peakPos) < 0.1f)
                    reachedPeak = true;
            }
            else
            {
                transform.position = Vector3.MoveTowards(transform.position, landingPos, step);

                if (Vector3.Distance(transform.position, landingPos) < 0.1f)
                {
                    isJumping = false;
                    OnLand();
                }
            }
        }

        private void OnLand()
        {
            if (anim != null)
                anim.SetTrigger(LandHash);

            if (audioSource != null && impactSound != null)
                audioSource.PlayOneShot(impactSound);

            if (landingSplashFX != null)
                Instantiate(landingSplashFX, transform.position, Quaternion.identity);

            Collider[] hits = Physics.OverlapSphere(transform.position, destroyRadius, breakableLayer);
            foreach (Collider hit in hits)
            {
                if (hit.TryGetComponent(out Breakable b))
                    b.Break();
                else
                    Destroy(hit.gameObject);
            }
        }

        // Animation Event Hooks
        public void SplashFX_Start()
        {
            if (splashFX != null)
                Instantiate(splashFX, transform.position, Quaternion.identity);
        }

        public void Play_Breach_Sound()
        {
            if (audioSource != null && breachSound != null)
                audioSource.PlayOneShot(breachSound);
        }

        public void SplashFX_Land()
        {
            if (landingSplashFX != null)
                Instantiate(landingSplashFX, transform.position, Quaternion.identity);
        }

        public void Play_Impact_Sound()
        {
            if (audioSource != null && impactSound != null)
                audioSource.PlayOneShot(impactSound);
        }

        public void Destroy_Boardwalk()
        {
            // Only destroy breakables — do NOT call OnLand(), which would
            // double-trigger audio and splash FX already fired by their own
            // animation event hooks (Play_Impact_Sound / SplashFX_Land).
            Collider[] hits = Physics.OverlapSphere(transform.position, destroyRadius, breakableLayer);
            foreach (Collider hit in hits)
            {
                if (hit.TryGetComponent(out Breakable b))
                    b.Break();
                else
                    Destroy(hit.gameObject);
            }
        }

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

    /// <summary>
    /// Stub — replace with your actual Breakable implementation.
    /// Any GameObject on the breakableLayer that has this component
    /// will have Break() called instead of being hard-destroyed.
    /// </summary>
    public class Breakable : MonoBehaviour
    {
        public virtual void Break()
        {
            Destroy(gameObject);
        }
    }
}