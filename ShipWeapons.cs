using UnityEngine;

public class ShipWeapons : MonoBehaviour
{
    [Header("Weapon Settings")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireCooldown = 2f;

    private float lastFireTime = -999f;

    public bool CanFire()
    {
        return Time.time >= lastFireTime + fireCooldown;
    }

    public void Fire(Transform target)
    {
        if (!CanFire())
            return;

        if (projectilePrefab == null || firePoint == null || target == null)
            return;

        Vector3 direction = target.position - firePoint.position;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion firingRotation =
            Quaternion.LookRotation(direction.normalized, Vector3.up);

        Instantiate(projectilePrefab, firePoint.position, firingRotation);

        lastFireTime = Time.time;
    }
}