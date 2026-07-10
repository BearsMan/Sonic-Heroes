using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HomingThunderProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 30f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private int damage = 1;
    [SerializeField] private float stunDuration = 2f;

    private Transform target;
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        Destroy(gameObject, lifetime);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void FixedUpdate()
    {
        if (target != null)
        {
            Vector3 desiredDirection = (target.position - transform.position).normalized;

            Vector3 homingDirection = Vector3.RotateTowards(rb.linearVelocity.normalized, desiredDirection, rotationSpeed * Time.fixedDeltaTime, 0f);

            rb.linearVelocity = homingDirection * speed;

            if (homingDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(homingDirection);
            }
        }
        else
        {
            rb.linearVelocity =
                transform.forward * speed;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy"))
        {
            return;
        }

        ThunderShootTarget enemy = other.GetComponentInParent<ThunderShootTarget>();

        if (enemy != null)
        {
            enemy.HitByThunderShoot(damage, stunDuration);
        }

        Destroy(gameObject);
    }
}