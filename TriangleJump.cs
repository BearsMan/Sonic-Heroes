using System.Collections;
using UnityEngine;

public class TriangleJump : MonoBehaviour
{
    [Header("Triangle Jump Settings")]
    public float launchForce = 18f;        // Upward force on jump
    public float launchAngle = 60f;        // Angle of launch relative to wall normal
    public float stickDuration = 0.25f;    // How long the character sticks before input is accepted
    public float gravityRestoreDelay = 0.1f;
    private UltimatePlayerMovement movement;
    [Header("State")]
    public bool triangleJumpReady = false; // True while stuck, waiting for jump input
    public bool isJumping = false;

    private GameObject stuckCharacter = null;
    private Coroutine stickCoroutine = null;

    // The wall's outward normal, used to calculate launch direction
    private Vector3 wallNormal = Vector3.zero;

    // -----------------------------------------------------------------------
    // Called by OnTriggerEnter when a HomingAttack hits this bumper
    // -----------------------------------------------------------------------
    public void Stick(GameObject speedCharacter, Vector3 contactNormal)
    {
        if (stickCoroutine != null)
            StopCoroutine(stickCoroutine);

        stuckCharacter = speedCharacter;
        wallNormal = contactNormal;

        stickCoroutine = StartCoroutine(StickRoutine(speedCharacter));
    }

    // -----------------------------------------------------------------------
    // Stick coroutine: freeze the character briefly, then wait for Jump input
    // -----------------------------------------------------------------------
    private IEnumerator StickRoutine(GameObject speedCharacter)
    {
        // ---- Freeze character ----
        movement = speedCharacter.GetComponent<UltimatePlayerMovement>();

        if (movement == null)
        {
            movement = speedCharacter.GetComponentInParent<UltimatePlayerMovement>();
        }

        if (movement != null)
        {
            movement.DisableMovement();
        }

        Rigidbody rb = speedCharacter.GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Play the Triangle Jump animation (idle on wall)
        Animator anim = speedCharacter.GetComponent<Animator>();
        if (anim != null)
            anim.Play("Triangle Jump");

        // Brief mandatory stick window (matches Heroes' short freeze)
        triangleJumpReady = false;
        yield return new WaitForSeconds(stickDuration);
        triangleJumpReady = true;

        // ---- Wait for jump input ----
        float inputTimeout = 3f; // safety: release if player never presses
        float elapsed = 0f;

        while (elapsed < inputTimeout)
        {
            // In Sonic Heroes the jump button triggers the wall launch
            if (Input.GetButtonDown("Jump"))
            {
                Launch(speedCharacter);
                yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Timed out — detach without launching
        Detach(speedCharacter);
    }

    // -----------------------------------------------------------------------
    // Launch: fire the character upward along the wall normal
    // -----------------------------------------------------------------------
    private void Launch(GameObject speedCharacter)
    {
        triangleJumpReady = false;
        isJumping = true;

        Rigidbody rb = speedCharacter.GetComponent<Rigidbody>();

        // Build launch direction: blend wall normal outward with world up,
        // producing a diagonal arc like the original game
        Vector3 outward = Vector3.ProjectOnPlane(wallNormal, Vector3.up).normalized;
        float rad = launchAngle * Mathf.Deg2Rad;
        Vector3 launchDir = (outward * Mathf.Cos(rad) + Vector3.up * Mathf.Sin(rad)).normalized;

        // Re-enable physics, apply launch impulse
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(launchDir * launchForce, ForceMode.VelocityChange);

        // Play launch animation
        Animator anim = speedCharacter.GetComponent<Animator>();
        if (anim != null)
            anim.Play("Jump");

        // Give control back to the player
        if (movement != null)
        {
            movement.EnableMovement();
        }

        stuckCharacter = null;
        stickCoroutine = null;

        // Reset isJumping after the character is airborne
        StartCoroutine(ResetJumpFlag());
    }

    // -----------------------------------------------------------------------
    // Detach: release without launching (timeout or external cancel)
    // -----------------------------------------------------------------------
    private void Detach(GameObject speedCharacter)
    {
        triangleJumpReady = false;
        isJumping = false;

        Rigidbody rb = speedCharacter.GetComponent<Rigidbody>();
        rb.useGravity = true;

        if (movement != null)
        {
            movement.EnableMovement();
        }

        stuckCharacter = null;
        stickCoroutine = null;
    }

    // -----------------------------------------------------------------------
    // Small helper: wait one fixed frame then clear the jumping flag
    // -----------------------------------------------------------------------
    private IEnumerator ResetJumpFlag()
    {
        yield return new WaitForFixedUpdate();
        isJumping = false;
    }

    // -----------------------------------------------------------------------
    // Collision detection
    // -----------------------------------------------------------------------
    public void OnTriggerEnter(Collider other)
    {
        HomingAttack ha = other.GetComponent<HomingAttack>();
        if (ha == null || !ha.homingAttackUsed)
            return;

        // Already has a character stuck — ignore additional hits
        if (stuckCharacter != null)
            return;

        // Compute the contact normal: direction from bumper center to character
        Vector3 contactNormal = (other.transform.position - transform.position).normalized;

        Stick(other.gameObject, contactNormal);
    }

    // -----------------------------------------------------------------------
    // If the stuck character leaves the trigger early (edge case), clean up
    // -----------------------------------------------------------------------
    public void OnTriggerExit(Collider other)
    {
        if (stuckCharacter == other.gameObject && triangleJumpReady)
        {
            if (stickCoroutine != null)
                StopCoroutine(stickCoroutine);

            Detach(other.gameObject);
        }
    }
}