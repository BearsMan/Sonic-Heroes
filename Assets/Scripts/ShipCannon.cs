using UnityEngine;

public class ShipCannon : MonoBehaviour
{
    [Header("Cannon")]
    [SerializeField]
    private Transform muzzle;

    [SerializeField]
    private Rigidbody projectilePrefab;

    [Header("Firing")]
    [SerializeField, Min(0.1f)]
    private float fireInterval = 2f;

    [SerializeField, Min(0f)]
    private float projectileSpeed = 35f;

    [SerializeField, Min(0f)]
    private float firingRange = 80f;

    [Header("Effects")]
    [SerializeField]
    private ParticleSystem muzzleFlash;

    [SerializeField]
    private AudioSource fireAudio;

    private float nextFireTime;

    public float FiringRange => firingRange;

    public bool ReadyToFire => Time.time >= nextFireTime;

    private void Awake()
    {
        if (muzzle == null)
        {
            muzzle = transform;
        }
    }

    public bool FireAt(Transform target)
    {
        if (!CanFireAt(target))
        {
            return false;
        }

        Vector3 direction = target.position - muzzle.position;

        if (!IsFinite(direction) || direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return false;
        }

        direction.Normalize();

        Rigidbody projectile = Instantiate(
            projectilePrefab,
            muzzle.position,
            Quaternion.LookRotation(direction));

        if (projectile == null)
        {
            return false;
        }

        projectile.linearVelocity = direction * projectileSpeed;
        nextFireTime = Time.time + fireInterval;

        PlayEffects();

        return true;
    }

    public bool CanFireAt(Transform target)
    {
        if (target == null || muzzle == null || projectilePrefab == null)
        {
            return false;
        }

        if (!ReadyToFire)
        {
            return false;
        }

        Vector3 difference = target.position - muzzle.position;

        if (!IsFinite(difference))
        {
            return false;
        }

        return difference.sqrMagnitude <= firingRange * firingRange;
    }

    private void PlayEffects()
    {
        if (muzzleFlash != null)
        {
            muzzleFlash.Play();
        }

        if (fireAudio != null)
        {
            fireAudio.Play();
        }
    }

    private static bool IsFinite(Vector3 value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }
}
