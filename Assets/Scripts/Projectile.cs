using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float speed = 80f;
    [SerializeField] private float damage = 50f;
    [SerializeField] private float lifetime = 8f;

    [Header("Effects")]
    [SerializeField] private GameObject impactEffect;

    private Rigidbody body;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();

        body.useGravity = false;
        body.isKinematic = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void Start()
    {
        Destroy (gameObject, lifetime);
    }

    private void FixedUpdate()
    {
        Vector3 nextPosition = body.position + transform.forward * speed * Time.fixedDeltaTime;

        body.MovePosition(nextPosition);
    }

    private void OnTriggerEnter(Collider other)
    {
        ShipHealth shipHealth =
            other.GetComponentInParent<ShipHealth>();

        if (shipHealth != null)
        {
            shipHealth.TakeDamage(damage);
        }

        PlayerHit playerHit =
            other.GetComponentInParent<PlayerHit>();

        if (playerHit != null)
        {
            playerHit.Hit();
        }

        if (impactEffect != null)
        {
            Instantiate(impactEffect, transform.position, Quaternion.identity);
        }

        Destroy (gameObject);
    }
}