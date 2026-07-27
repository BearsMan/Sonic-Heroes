using System.Collections;
using UnityEngine;

/// <summary>
/// A pole activated by the player's Tornado Jump.
/// Put this script on the pole's trigger object.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TornadoPole : MonoBehaviour
{
    [Header("Path References")]
    [SerializeField] private Transform orbitCenter;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private Transform launchDirection;

    [Header("Entry")]
    [SerializeField, Min(0.01f)] private float entryDuration = 0.12f;

    [Header("Spiral")]
    [SerializeField, Min(0.1f)] private float orbitRadius = 1.5f;
    [SerializeField] private float orbitSpeed = 540f;
    [SerializeField, Min(0.1f)] private float climbSpeed = 7f;
    [SerializeField] private bool clockwise;

    [Header("Launch")]
    [SerializeField, Min(0.1f)] private float launchSpeed = 22f;
    [SerializeField, Min(0f)] private float upwardBias = 0.25f;
    [SerializeField, Min(0f)] private float controlReturnDelay = 0.15f;

    [Header("Animation")]
    [SerializeField] private string swingBool = "Team Swing";

    private bool occupied;
    private Coroutine rideRoutine;

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();

        trigger.isTrigger = true;
        orbitCenter = transform;
    }

    public bool TryActivate(
        GameObject player,
        TornadoJump tornadoJump)
    {
        if (occupied || player == null || tornadoJump == null)
            return false;

        Rigidbody body = player.GetComponent<Rigidbody>();

        UltimatePlayerMovement movement =
            player.GetComponent<UltimatePlayerMovement>();

        if (movement == null)
        {
            movement =
                player.GetComponentInParent<UltimatePlayerMovement>();
        }

        if (body == null || movement == null)
            return false;

        occupied = true;

        rideRoutine = StartCoroutine(
            RidePole(
                player,
                body,
                movement,
                tornadoJump));

        return true;
    }

    private IEnumerator RidePole(
        GameObject player,
        Rigidbody body,
        UltimatePlayerMovement movement,
        TornadoJump tornadoJump)
    {
        tornadoJump.TransferToPole();

        Transform center =
            orbitCenter != null ? orbitCenter : transform;

        Transform exit =
            exitPoint != null ? exitPoint : center;

        bool movementWasEnabled = movement.enabled;
        bool gravityWasEnabled = body.useGravity;
        bool wasKinematic = body.isKinematic;

        bool leftFollowerWasActive =
            movement.leftFollower != null &&
            movement.leftFollower.activeSelf;

        bool rightFollowerWasActive =
            movement.rightFollower != null &&
            movement.rightFollower.activeSelf;

        Animator animator =
            player.GetComponentInChildren<Animator>();

        movement.DisableMovement();
        movement.enabled = false;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.useGravity = false;
        body.isKinematic = true;

        if (movement.leftFollower != null)
            movement.leftFollower.SetActive(false);

        if (movement.rightFollower != null)
            movement.rightFollower.SetActive(false);

        if (animator != null &&
            !string.IsNullOrWhiteSpace(swingBool))
        {
            animator.SetBool(swingBool, true);
        }

        Vector3 flatOffset =
            player.transform.position - center.position;

        flatOffset.y = 0f;

        float currentAngle =
            flatOffset.sqrMagnitude > 0.001f
                ? Mathf.Atan2(
                    flatOffset.z,
                    flatOffset.x) * Mathf.Rad2Deg
                : 0f;

        float startY = player.transform.position.y;
        float targetY = exit.position.y;

        Vector3 entryStart =
            player.transform.position;

        Vector3 entryTarget =
            OrbitPosition(
                center.position,
                currentAngle,
                startY);

        float entryElapsed = 0f;

        while (entryElapsed < entryDuration)
        {
            entryElapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    entryElapsed / entryDuration);

            t = t * t * (3f - 2f * t);

            player.transform.position =
                Vector3.Lerp(
                    entryStart,
                    entryTarget,
                    t);

            yield return null;
        }

        float currentY = startY;
        float direction = clockwise ? -1f : 1f;

        while (!Mathf.Approximately(currentY, targetY))
        {
            currentAngle +=
                orbitSpeed *
                direction *
                Time.deltaTime;

            currentY = Mathf.MoveTowards(
                currentY,
                targetY,
                climbSpeed * Time.deltaTime);

            Vector3 position =
                OrbitPosition(
                    center.position,
                    currentAngle,
                    currentY);

            player.transform.position = position;

            Vector3 tangent =
                OrbitTangent(
                    currentAngle,
                    direction);

            if (tangent.sqrMagnitude > 0.001f)
            {
                player.transform.rotation =
                    Quaternion.LookRotation(
                        tangent,
                        Vector3.up);
            }

            yield return null;
        }

        player.transform.position = exit.position;

        if (animator != null &&
            !string.IsNullOrWhiteSpace(swingBool))
        {
            animator.SetBool(swingBool, false);
        }

        Vector3 launchVector =
            ResolveLaunchDirection(exit);

        body.isKinematic = false;
        body.useGravity = gravityWasEnabled;
        body.linearVelocity =
            launchVector * launchSpeed;

        movement.enabled = movementWasEnabled;

        if (controlReturnDelay > 0f)
        {
            yield return new WaitForSeconds(controlReturnDelay);
        }

        movement.EnableMovement();

        if (movement.leftFollower != null)
        {
            movement.leftFollower.SetActive(
                leftFollowerWasActive);
        }

        if (movement.rightFollower != null)
        {
            movement.rightFollower.SetActive(
                rightFollowerWasActive);
        }

        if (wasKinematic)
            body.isKinematic = true;

        tornadoJump.FinishPoleAction();

        occupied = false;
        rideRoutine = null;
    }

    private Vector3 OrbitPosition(
        Vector3 center,
        float angleDegrees,
        float height)
    {
        float radians =
            angleDegrees * Mathf.Deg2Rad;

        return new Vector3(
            center.x +
            Mathf.Cos(radians) * orbitRadius,

            height,

            center.z +
            Mathf.Sin(radians) * orbitRadius);
    }

    private static Vector3 OrbitTangent(
        float angleDegrees,
        float direction)
    {
        float radians =
            angleDegrees * Mathf.Deg2Rad;

        return new Vector3(
            -Mathf.Sin(radians) * direction,
            0f,
            Mathf.Cos(radians) * direction).normalized;
    }

    private Vector3 ResolveLaunchDirection(
        Transform exit)
    {
        Vector3 direction;

        if (launchDirection != null)
        {
            direction =
                launchDirection.position -
                exit.position;

            if (direction.sqrMagnitude < 0.001f)
                direction = launchDirection.forward;
        }
        else
        {
            direction = exit.forward;
        }

        direction += Vector3.up * upwardBias;

        if (direction.sqrMagnitude < 0.001f)
            direction = Vector3.up;

        return direction.normalized;
    }

    private void OnDisable()
    {
        if (rideRoutine != null)
        {
            StopCoroutine(rideRoutine);
            rideRoutine = null;
        }

        occupied = false;
    }

    private void OnDrawGizmosSelected()
    {
        Transform center =
            orbitCenter != null
                ? orbitCenter
                : transform;

        Gizmos.DrawWireSphere(
            center.position,
            orbitRadius);

        if (exitPoint != null &&
            launchDirection != null)
        {
            Gizmos.DrawLine(
                exitPoint.position,
                launchDirection.position);
        }
    }
}