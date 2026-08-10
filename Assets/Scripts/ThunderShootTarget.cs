using System.Collections;
using UnityEngine;

public class ThunderShootTarget : MonoBehaviour
{
    [SerializeField] private int health = 3;

    private bool isStunned;

    public void HitByThunderShoot(int damage, float stunDuration)
    {
        health -= damage;

        if (health <= 0)
        {
            Destroy(gameObject);
            return;
        }

        StopAllCoroutines();
        StartCoroutine(StunRoutine(stunDuration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        isStunned = true;

        // Disable enemy movement or attacks here.

        yield return new WaitForSeconds(duration);

        isStunned = false;

        // Re-enable enemy movement or attacks here.
    }
}