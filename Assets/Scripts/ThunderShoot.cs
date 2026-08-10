using UnityEngine;

public class ThunderShoot : MonoBehaviour
{
    [Header("Thunder Shoot")]
    [SerializeField] private GameObject teammatePrefab;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private float launchSpeed = 30f;
    [SerializeField] private float targetingRadius = 20f;
    [SerializeField] private float cooldown = 0.35f;

    [Header("Target Detection")]
    [SerializeField] private LayerMask enemyLayer;

    private float nextShootTime;

    private void Update()
    {
        if (Input.GetButtonDown("Fire1") && Time.time >= nextShootTime)
        {
            PerformThunderShoot();
            nextShootTime = Time.time + cooldown;
        }
    }

    private void PerformThunderShoot()
    {
        if (teammatePrefab == null || shootPoint == null)
        {
            Debug.LogWarning("Thunder Shoot is missing a prefab or shoot point.");
            return;
        }

        Transform target = FindClosestEnemy();

        Vector3 launchDirection = shootPoint.forward;

        if (target != null)
        {
            launchDirection =
                (target.position - shootPoint.position).normalized;
        }

        GameObject launchedTeammate = Instantiate(
            teammatePrefab,
            shootPoint.position,
            Quaternion.LookRotation(launchDirection)
        );

        Rigidbody rb = launchedTeammate.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.useGravity = false;
            rb.linearVelocity = launchDirection * launchSpeed;
        }

        HomingThunderProjectile homingProjectile =
            launchedTeammate.GetComponent<HomingThunderProjectile>();

        if (homingProjectile != null)
        {
            homingProjectile.SetTarget(target);
        }
    }

    private Transform FindClosestEnemy()
    {
        Collider[] enemies = Physics.OverlapSphere(
            transform.position,
            targetingRadius,
            enemyLayer
        );

        Transform closestEnemy = null;
        float closestDistanceSqr = Mathf.Infinity;

        foreach (Collider enemy in enemies)
        {
            Vector3 difference =
                enemy.transform.position - transform.position;

            float distanceSqr = difference.sqrMagnitude;

            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closestEnemy = enemy.transform;
            }
        }

        return closestEnemy;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, targetingRadius);
    }
}