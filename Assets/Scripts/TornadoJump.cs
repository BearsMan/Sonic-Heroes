using System.Collections;
using UnityEngine;

public class TornadoJump : MonoBehaviour
{
    [Header("Pole References")]
    public GameObject pole;
    public GameObject ExitPole;
    public GameObject JumpDirection;

    [Header("Swing Settings")]
    public float orbitRadius = 1.5f;       // How far from the pole center the character orbits
    public float orbitSpeed = 360f;        // Degrees per second around the pole
    public float climbSpeed = 4f;          // Units per second rising up the pole
    public float entrySnapSpeed = 10f;     // How fast the character snaps to orbit start

    [Header("Launch Settings")]
    public float launchForce = 18f;        // Speed of the fling at the top
    public float launchUpwardBias = 0.3f;  // Adds upward arc to the launch direction

    public bool tornadoJump;
    public bool teamSwing;

    // ── Trigger: player presses B while inside the collider ──────────────────
    public void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player") && Input.GetKeyDown(KeyCode.B))
        {
            StartCoroutine(Swinging(other.gameObject));
        }
    }

    // ── Main coroutine ────────────────────────────────────────────────────────
    public IEnumerator Swinging(GameObject speedCharacter)
    {
        // 1. Lock player input and physics
        UltimatePlayerMovement upm = speedCharacter.GetComponent<UltimatePlayerMovement>();
        Rigidbody rb = speedCharacter.GetComponent<Rigidbody>();

        UltimatePlayerMovement.Controllable = false;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        upm.leftFollower.SetActive(false);
        upm.rightFollower.SetActive(false);

        // 2. Determine the orbit start position
        //    Project the character onto the XZ plane of the pole,
        //    keep them orbitRadius away, start at the angle they approached from.
        Vector3 poleBase = transform.position;          // this object IS the entry point
        Vector3 toChar = speedCharacter.transform.position - poleBase;
        toChar.y = 0f;

        float startAngle = Mathf.Atan2(toChar.z, toChar.x) * Mathf.Rad2Deg;
        if (toChar.magnitude < 0.01f) startAngle = 0f;  // fallback if standing on center

        float currentAngle = startAngle;
        float currentY = poleBase.y;
        float exitY = ExitPole.transform.position.y;

        // 3. Smooth snap into orbit position before swinging begins
        Vector3 OrbitPos(float angle, float y)
        {
            float rad = angle * Mathf.Deg2Rad;
            return new Vector3(
                poleBase.x + Mathf.Cos(rad) * orbitRadius,
                y,
                poleBase.z + Mathf.Sin(rad) * orbitRadius
            );
        }

        float snapTimer = 0f;
        Vector3 snapStart = speedCharacter.transform.position;
        Vector3 snapTarget = OrbitPos(currentAngle, currentY);

        while (snapTimer < 1f)
        {
            snapTimer += Time.deltaTime * entrySnapSpeed;
            speedCharacter.transform.position = Vector3.Lerp(snapStart, snapTarget, snapTimer);
            yield return null;
        }

        // 4. Spiral upward around the pole
        //    Character orbits at orbitSpeed deg/s while rising at climbSpeed u/s.
        //    Matches the visual of "swinging around the pole" seen in Sonic Heroes.
        while (currentY < exitY)
        {
            currentAngle += orbitSpeed * Time.deltaTime;
            currentY += climbSpeed * Time.deltaTime;
            currentY = Mathf.Min(currentY, exitY);

            speedCharacter.transform.position = OrbitPos(currentAngle, currentY);

            // Face the direction of travel (tangent of orbit)
            float tangentAngle = (currentAngle + 90f) * Mathf.Deg2Rad;
            Vector3 facing = new Vector3(Mathf.Cos(tangentAngle), 0f, Mathf.Sin(tangentAngle));
            if (facing != Vector3.zero)
                speedCharacter.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);

            yield return null;
        }

        // 5. Snap to exit position and fling in the preset arrow direction
        speedCharacter.transform.position = ExitPole.transform.position;

        // JumpDirection is a child/marker whose world-space position relative to
        // ExitPole encodes the launch direction (same as the original intent).
        Vector3 rawDir = (ExitPole.transform.position + JumpDirection.transform.position).normalized;
        Vector3 launchDir = (rawDir + Vector3.up * launchUpwardBias).normalized;

        // Restore physics and hand velocity back to the rigidbody for a proper arc
        rb.useGravity = true;
        rb.linearVelocity = launchDir * launchForce;

        // Re-enable control after a short airtime so the player can steer the landing
        yield return new WaitForSeconds(0.15f);
        UltimatePlayerMovement.Controllable = true;

        upm.leftFollower.SetActive(true);
        upm.rightFollower.SetActive(true);
    }

    // ── Stubs kept for compatibility ──────────────────────────────────────────
    public void RotateToSwing(GameObject speedCharacter) { }
    public void Swing()
    {

    }
}