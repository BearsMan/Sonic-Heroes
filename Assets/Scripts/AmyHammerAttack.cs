using UnityEngine;

public class AmyHammerAttack : MonoBehaviour
{
    public float attackRange = 2f;
    public int attackDamage = 10;
    public LayerMask enemyLayer;
    private Animator animator;
    private void Start()
    {
        animator = GetComponentInChildren<Animator>();
    }
    public void Attack()
    {
        // Play attack animation
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
        // Detect enemies in range
        Collider[] hitEnemies = Physics.OverlapSphere(transform.position, attackRange, enemyLayer);
        // Damage them
        foreach (Collider enemy in hitEnemies)
        {
            enemy.GetComponent<Health>()?.TakeDamage(attackDamage);
        }
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}