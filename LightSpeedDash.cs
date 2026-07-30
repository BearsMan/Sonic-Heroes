using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Speed Formation forward burst attack.
/// Dashes the Speed character straight ahead and damages enemies in the path.
/// </summary>
public class LightSpeedDash : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TeamActionController actionController;
    [SerializeField] private UltimatePlayerMovement movement;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private AudioSource audioSource;

    [Header("Input")]
    [SerializeField] private KeyCode dashKey = KeyCode.B;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float dashSpeed = 80f;
    [SerializeField, Min(0.1f)] private float maximumDashDistance = 20f;
    [SerializeField, Min(0.05f)] private float maximumDashTime = 0.4f;
    [SerializeField, Min(0f)] private float exitSpeed = 15f;
    [SerializeField] private bool preserveVerticalDirection;
    [SerializeField] private bool stopOnObstacle = true;

    [Header("Ring Detection")]
    [SerializeField] private float ringSearchRadius = 8f;
    [SerializeField] private float ringSnapDistance = 2f;
    [SerializeField] private LayerMask ringLayer;
    [SerializeField] private bool prioritizeRingTrails = true;

    [Header("Collision")]
    [SerializeField, Min(0.05f)] private float dashRadius = 0.75f;
    [SerializeField] private LayerMask obstacleLayers = ~0;
    [SerializeField] private LayerMask damageLayers = ~0;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float damage = 25f;
    [SerializeField] private bool breakObjects = true;

    [Header("Cooldown")]
    [SerializeField, Min(0f)] private float dashCoolDown = 0.5f;

    [Header("Animation")]
    [SerializeField] private string dashAnimation = "Light Speed Dash";
    [SerializeField] private string exitAnimation = "Run";

    [Header("Effects")]
    [SerializeField] private GameObject dashEffect;
    [SerializeField] private TrailRenderer dashTrail;

    [Header("Audio")]
    [SerializeField] private AudioClip dashStartSound;
    [SerializeField] private AudioClip dashHitSound;
    [SerializeField] private AudioClip dashEndSound;

    [Header("Debug")]
    [SerializeField] private bool drawDashPath = true;
    [SerializeField] private bool logStateChanges;

    private readonly HashSet<GameObject> damagedObjects = new();
    private Coroutine dashCoroutine;
    private Vector3 dashDirection;
    private bool isDashing;
    private bool previousGravityState;
    private bool movementDisabledDirectly;
    private float nextDashTime;

    public bool IsDashing => isDashing;
    public bool CanDash => !isDashing && Time.time >= nextDashTime;

    private void Awake()
    {
        ResolveReferences();
        SetPresentation(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(dashKey))
            TryStartDash();
    }

    private void ResolveReferences()
    {
        if (actionController == null)
            actionController = GetComponent<TeamActionController>();
        if (actionController == null)
            actionController = GetComponentInParent<TeamActionController>();
        if (movement == null)
            movement = GetComponent<UltimatePlayerMovement>();
        if (movement == null)
            movement = GetComponentInParent<UltimatePlayerMovement>();
        if (playerRigidbody == null)
            playerRigidbody = GetComponent<Rigidbody>();
        if (playerRigidbody == null && movement != null)
            playerRigidbody = movement.GetComponent<Rigidbody>();
        if (playerAnimator == null)
            playerAnimator = GetComponentInChildren<Animator>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public bool TryStartDash()
    {
        if (!CanDash)
            return false;
        ResolveReferences();
        if (playerRigidbody == null)
        {
            Debug.LogWarning("LightSpeedDash could not find a Rigidbody.", this);
            return false;
        }
        if (!BeginTeamAction())
            return false;
        BeginDash();
        return true;
    }

    private bool BeginTeamAction()
    {
        movementDisabledDirectly = false;
        if (actionController != null)
        {
            bool accepted = actionController.TryBeginAction(
                TeamActionController.TeamAction.LightDash,
                TeamActionController.TeamFormation.Speed,
                mustBeGrounded: true,
                mustBeAirborne: false,
                surrenderMovementControl: true);
            if (!accepted)
                return false;
        }
        else if (movement != null)
        {
            movement.DisableMovement();
            movementDisabledDirectly = true;
        }
        return true;
    }

    private void BeginDash()
    {
        isDashing = true;
        damagedObjects.Clear();
        dashDirection = GetDashDirection();
        previousGravityState = playerRigidbody.useGravity;
        playerRigidbody.linearVelocity = Vector3.zero;
        playerRigidbody.angularVelocity = Vector3.zero;
        playerRigidbody.useGravity = false;
        PlayAnimation(dashAnimation);
        PlaySound(dashStartSound);
        SetPresentation(true);
        dashCoroutine = StartCoroutine(DashRoutine());
        if (logStateChanges)
            Debug.Log("Light Speed Dash started.", this);
    }

    private Vector3 GetDashDirection()
    {
        Vector3 direction = transform.forward;
        if (!preserveVerticalDirection)
            direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;
        return direction.normalized;
    }

    private IEnumerator DashRoutine()
    {
        float dashTimer = 0f;
        float distanceTravelled = 0f;
        WaitForFixedUpdate fixedUpdate = new();
        while (isDashing &&
               dashTimer < maximumDashTime &&
               distanceTravelled < maximumDashDistance)
        {
            if (playerRigidbody == null)
            {
                FinishDash(false);
                yield break;
            }
            float remainingDistance = maximumDashDistance - distanceTravelled;
            float movementDistance = Mathf.Min(
                dashSpeed * Time.fixedDeltaTime,
                remainingDistance);
            Vector3 startPosition = playerRigidbody.position;
            if (CheckDashCollision(startPosition, movementDistance, out RaycastHit hit))
            {
                float safeDistance = Mathf.Max(0f, hit.distance - 0.05f);
                if (safeDistance > 0f)
                {
                    Vector3 impactPosition = startPosition + dashDirection * safeDistance;
                    playerRigidbody.MovePosition(impactPosition);
                    DamageDashPath(startPosition, impactPosition);
                    distanceTravelled += safeDistance;
                }
                DamageTarget(hit.collider);
                if (stopOnObstacle && IsObstacle(hit.collider))
                {
                    FinishDash(false);
                    yield break;
                }
            }
            else
            {
                Vector3 nextPosition = startPosition + dashDirection * movementDistance;
                playerRigidbody.MovePosition(nextPosition);
                DamageDashPath(startPosition, nextPosition);
                distanceTravelled += movementDistance;
            }
            dashTimer += Time.fixedDeltaTime;
            yield return fixedUpdate;
        }
        FinishDash(true);
    }

    private bool CheckDashCollision(
        Vector3 startPosition,
        float distance,
        out RaycastHit closestHit)
    {
        closestHit = default;
        RaycastHit[] hits = Physics.SphereCastAll(
            startPosition,
            dashRadius,
            dashDirection,
            distance,
            obstacleLayers | damageLayers,
            QueryTriggerInteraction.Ignore);
        float closestDistance = float.MaxValue;
        bool foundHit = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || IsSelfOrTeamCharacter(hitCollider.gameObject))
                continue;
            if (hits[i].distance >= closestDistance)
                continue;
            closestDistance = hits[i].distance;
            closestHit = hits[i];
            foundHit = true;
        }
        return foundHit;
    }

    private void DamageDashPath(Vector3 startPosition, Vector3 endPosition)
    {
        Vector3 difference = endPosition - startPosition;
        float distance = difference.magnitude;
        Vector3 centre = distance > 0f
            ? startPosition + difference * 0.5f
            : startPosition;
        Vector3 halfExtents = new(
            dashRadius,
            dashRadius,
            distance * 0.5f + dashRadius);
        Quaternion rotation = distance > 0.0001f
            ? Quaternion.LookRotation(difference.normalized, Vector3.up)
            : transform.rotation;
        Collider[] hits = Physics.OverlapBox(
            centre,
            halfExtents,
            rotation,
            damageLayers,
            QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
            DamageTarget(hits[i]);
    }

    private void DamageTarget(Collider hitCollider)
    {
        if (hitCollider == null)
            return;
        GameObject target = hitCollider.attachedRigidbody != null
            ? hitCollider.attachedRigidbody.gameObject
            : hitCollider.gameObject;
        if (IsSelfOrTeamCharacter(target))
            return;
        if (!damagedObjects.Add(target))
            return;
        target.SendMessage(
            "TakeDamage",
            damage,
            SendMessageOptions.DontRequireReceiver);
        if (breakObjects)
        {
            target.SendMessage(
                "Break",
                SendMessageOptions.DontRequireReceiver);
        }
        PlaySound(dashHitSound);
    }

    private bool IsObstacle(Collider hitCollider)
    {
        if (hitCollider == null)
            return false;
        int hitLayer = 1 << hitCollider.gameObject.layer;
        return (obstacleLayers.value & hitLayer) != 0;
    }

    private bool IsSelfOrTeamCharacter(GameObject target)
    {
        if (target == null)
            return true;
        if (target == gameObject ||
            target.transform.IsChildOf(transform) ||
            transform.IsChildOf(target.transform))
        {
            return true;
        }
        if (actionController == null)
            return false;
        return target == actionController.SpeedCharacter ||
               target == actionController.FlyCharacter ||
               target == actionController.PowerCharacter;
    }

    public void CancelDash()
    {
        if (!isDashing)
            return;
        FinishDash(false);
    }

    private void FinishDash(bool applyExitMomentum)
    {
        if (!isDashing)
            return;
        isDashing = false;
        nextDashTime = Time.time + dashCoolDown;
        if (dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
            dashCoroutine = null;
        }
        if (playerRigidbody != null)
        {
            playerRigidbody.useGravity = previousGravityState;
            if (applyExitMomentum)
                playerRigidbody.linearVelocity = dashDirection * exitSpeed;
            else
                playerRigidbody.linearVelocity = Vector3.zero;
        }
        PlayAnimation(exitAnimation);
        PlaySound(dashEndSound);
        SetPresentation(false);
        RestoreMovement();
        damagedObjects.Clear();
        if (logStateChanges)
            Debug.Log("Light Speed Dash finished.", this);
    }

    private void RestoreMovement()
    {
        if (actionController != null &&
            actionController.CurrentAction == TeamActionController.TeamAction.LightDash)
        {
            actionController.EndAction(restoreMovementControl: true);
        }
        else if (movementDisabledDirectly && movement != null)
        {
            movement.EnableMovement();
        }
        movementDisabledDirectly = false;
    }

    private void PlayAnimation(string animationName)
    {
        if (playerAnimator == null || string.IsNullOrWhiteSpace(animationName))
            return;
        playerAnimator.Play(animationName);
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource == null || clip == null)
            return;
        audioSource.PlayOneShot(clip);
    }

    private void SetPresentation(bool active)
    {
        if (dashEffect != null)
            dashEffect.SetActive(active);
        if (dashTrail != null)
            dashTrail.emitting = active;
    }

    private void OnDisable()
    {
        if (isDashing)
            FinishDash(false);
        else
            SetPresentation(false);
    }

    private void OnValidate()
    {
        dashSpeed = Mathf.Max(0.1f, dashSpeed);
        maximumDashDistance = Mathf.Max(0.1f, maximumDashDistance);
        maximumDashTime = Mathf.Max(0.05f, maximumDashTime);
        exitSpeed = Mathf.Max(0f, exitSpeed);
        dashRadius = Mathf.Max(0.05f, dashRadius);
        damage = Mathf.Max(0f, damage);
        dashCoolDown = Mathf.Max(0f, dashCoolDown);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDashPath)
            return;
        Vector3 direction = transform.forward;
        if (!preserveVerticalDirection)
            direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;
        direction.Normalize();
        Vector3 startPosition = transform.position;
        Vector3 endPosition = startPosition + direction * maximumDashDistance;
        Gizmos.DrawWireSphere(startPosition, dashRadius);
        Gizmos.DrawLine(startPosition, endPosition);
        Gizmos.DrawWireSphere(endPosition, dashRadius);
    }
}