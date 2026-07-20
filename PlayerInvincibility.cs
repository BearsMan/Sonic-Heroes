using System.Collections;
using UnityEngine;

public class PlayerInvincibility : MonoBehaviour
{
    public bool IsInvincible { get; private set; }

    [SerializeField] private float duration = 20f;

    public void Activate()
    {
        StopAllCoroutines();
        StartCoroutine(InvincibilityRoutine());
    }

    private IEnumerator InvincibilityRoutine()
    {
        IsInvincible = true;

        Debug.Log("Invincibility Activated");

        yield return new WaitForSeconds(duration);

        IsInvincible = false;

        Debug.Log("Invincibility Ended");
    }
}