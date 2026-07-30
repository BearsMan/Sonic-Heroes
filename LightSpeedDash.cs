using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Speed Formation dash attack.
/// Locks onto nearby ring trails when available or performs a straight dash.
/// Damages enemies along both dash paths.
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

    [Header("Straight Dash")]
    [SerializeField, Min(0.1f)] private float dashSpeed = 80f;
    [SerializeField, Min(0.1f)] private float maximumDashDistance = 20f;
    [SerializeField, Min(0.05f)] private float maximumDashTime = 0.4f;
    [SerializeField] private bool preserveVerticalDirection;
    [SerializeField] private bool stopOnObstacle = true;

    [Header("Ring Trail")]
    [SerializeField] private LayerMask ringLayers;
    [SerializeField, Min(0.1f)] private float ringSearchRadius = 8f;
    [SerializeField, Min(0.1f)] private float ringChainRadius = 5f;
    [SerializeField, Min(0.1f)] private float ringDashSpeed = 60f;
    [SerializeField, Min(0f)] private float ringArrivalDistance = 0.1f;
    [SerializeField, Min(0f)] private float ringRotationSpeed = 1080f;
    [SerializeField, Min(0.1f)] private float maximumRingDashTime = 4f;
    [SerializeField, Range(-1f, 1f)] private float minimumRingDirectionDot = -0.25f;
    [SerializeField] private bool prioritizeRingTrails = true;

    [Header("Exit")]
    [SerializeField, Min(0f)] private float exitSpeed = 15f;

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
    [SerializeField] private AudioClip ringLockSound;
    [SerializeField] private AudioClip dashEndSound;

    [Header("Debug")]
    [SerializeField] private bool drawDashPath = true;
    [SerializeField] private bool drawRingSearch = true;
    [SerializeField] private bool logStateChanges;
    private LightSpeedDashRing currentRing;
    private readonly HashSet<GameObject> damagedObjects = new();
    private readonly HashSet<LightSpeedDashRing> visitedRings = new();
    private Coroutine dashCoroutine;
    
    private Vector3 dashDirection;
    private bool isDashing;
    private bool isFollowingRings;
    private bool previousGravityState;
    private bool previousKinematicState;
    private bool movementDisabledDirectly;
    private float nextDashTime;

    public bool IsDashing => isDashing;
    public bool IsFollowingRings => isFollowingRings;
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
        LightSpeedDashRing nearbyRing = prioritizeRingTrails
    ? FindClosestRing(playerRigidbody.position, ringSearchRadius, null)
    : null;
        if (!BeginTeamAction())
            return false;
        BeginDash(nearbyRing);
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
                mustBeGrounded: false,
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

    private void BeginDash(LightSpeedDashRing startingRing)
    {
        isDashing = true;
        isFollowingRings = startingRing != null;
        currentRing = startingRing;
        damagedObjects.Clear();
        visitedRings.Clear();
        dashDirection = GetDashDirection();
        previousGravityState = playerRigidbody.useGravity;
        previousKinematicState = playerRigidbody.isKinematic;
        playerRigidbody.linearVelocity = Vector3.zero;
        playerRigidbody.angularVelocity = Vector3.zero;
        playerRigidbody.useGravity = false;
        playerRigidbody.isKinematic = true;
        PlayAnimation(dashAnimation);
        PlaySound(dashStartSound);
        if (isFollowingRings)
            PlaySound(ringLockSound);
        SetPresentation(true);
        dashCoroutine = StartCoroutine(
            isFollowingRings
                ? RingDashRoutine()
                : StraightDashRoutine());
        if (logStateChanges)
        {
            string dashType = isFollowingRings
                ? "ring trail"
                : "straight";
            Debug.Log($"Light Speed Dash started: {dashType}.", this);
        }
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

    private IEnumerator StraightDashRoutine()
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
            if (CheckObstacle(
                startPosition,
                dashDirection,
                movementDistance,
                out RaycastHit obstacleHit))
            {
                float safeDistance = Mathf.Max(0f, obstacleHit.distance - 0.05f);
                Vector3 impactPosition =
                    startPosition + dashDirection * safeDistance;
                MoveAlongPath(startPosition, impactPosition);
                distanceTravelled += safeDistance;
                if (stopOnObstacle)
                {
                    FinishDash(false);
                    yield break;
                }
            }
            else
            {
                Vector3 nextPosition =
                    startPosition + dashDirection * movementDistance;
                MoveAlongPath(startPosition, nextPosition);
                distanceTravelled += movementDistance;
            }
            dashTimer += Time.fixedDeltaTime;
            yield return fixedUpdate;
        }
        FinishDash(true);
    }

    private IEnumerator RingDashRoutine()
    {
        float dashTimer = 0f;
        WaitForFixedUpdate fixedUpdate = new();
        while (isDashing && dashTimer < maximumRingDashTime)
        {
            if (playerRigidbody == null)
            {
                FinishDash(false);
                yield break;
            }
            if (!IsValidRing(currentRing))
            {
                currentRing = FindClosestRing(
                    playerRigidbody.position,
                    ringChainRadius,
                    dashDirection);
                if (currentRing == null)
                {
                    FinishDash(true);
                    yield break;
                }
            }
            Vector3 targetPosition = currentRing.DashPosition;
            Vector3 difference =
                targetPosition - playerRigidbody.position;
            float distance = difference.magnitude;
            if (distance <= ringArrivalDistance)
            {
                Vector3 arrivalDirection = difference.sqrMagnitude > 0.0001f
                    ? difference.normalized
                    : dashDirection;
                playerRigidbody.position = targetPosition;
                DamageDashPath(targetPosition, targetPosition);
                visitedRings.Add(currentRing);
                dashDirection = arrivalDirection;
                LightSpeedDashRing previousRing = currentRing;
                currentRing = FindClosestRing(
                    targetPosition,
                    ringChainRadius,
                    dashDirection);
                if (currentRing == null || currentRing == previousRing)
                {
                    FinishDash(true);
                    yield break;
                }
                yield return fixedUpdate;
                continue;
            }
            Vector3 direction = difference / distance;
            dashDirection = direction;
            RotatePlayer(direction);
            float movementDistance = Mathf.Min(
                ringDashSpeed * Time.fixedDeltaTime,
                distance);
            Vector3 startPosition = playerRigidbody.position;
            if (CheckObstacle(
                startPosition,
                direction,
                movementDistance,
                out RaycastHit obstacleHit))
            {
                float safeDistance = Mathf.Max(0f, obstacleHit.distance - 0.05f);
                Vector3 impactPosition =
                    startPosition + direction * safeDistance;
                MoveAlongPath(startPosition, impactPosition);
                FinishDash(false);
                yield break;
            }
            Vector3 nextPosition = Vector3.MoveTowards(
                startPosition,
                targetPosition,
                movementDistance);
            MoveAlongPath(startPosition, nextPosition);
            dashTimer += Time.fixedDeltaTime;
            yield return fixedUpdate;
        }
        FinishDash(true);
    }

    private void MoveAlongPath(
        Vector3 startPosition,
        Vector3 endPosition)
    {
        playerRigidbody.MovePosition(endPosition);
        DamageDashPath(startPosition, endPosition);
    }

    private LightSpeedDashRing FindClosestRing(
    Vector3 searchPosition,
    float searchRadius,
    Vector3? preferredDirection)
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(
            searchPosition,
            searchRadius,
            ringLayers,
            QueryTriggerInteraction.Collide);

        LightSpeedDashRing closestRing = null;
        float closestScore = float.MaxValue;

        for (int i = 0; i < nearbyColliders.Length; i++)
        {
            LightSpeedDashRing ring =
                nearbyColliders[i].GetComponentInParent<LightSpeedDashRing>();

            if (!IsValidRing(ring) || visitedRings.Contains(ring))
                continue;

            Vector3 difference = ring.DashPosition - searchPosition;
            float distance = difference.magnitude;

            if (distance <= ringArrivalDistance)
                continue;

            float allowedChainRange = Mathf.Min(
                searchRadius,
                ring.ChainRange);

            if (distance > allowedChainRange)
                continue;

            float score = distance;

            if (preferredDirection.HasValue &&
                preferredDirection.Value.sqrMagnitude > 0.0001f &&
                difference.sqrMagnitude > 0.0001f)
            {
                float directionDot = Vector3.Dot(
                    preferredDirection.Value.normalized,
                    difference.normalized);

                if (directionDot < minimumRingDirectionDot)
                    continue;

                score -= directionDot * allowedChainRange * 0.5f;
            }

            if (score >= closestScore)
                continue;

            closestScore = score;
            closestRing = ring;
        }

        return closestRing;
    }

    private bool IsValidRing(LightSpeedDashRing ring)
    {
        return ring != null && ring.CanBeDashedThrough;
    }

    private bool CheckObstacle(
        Vector3 startPosition,
        Vector3 direction,
        float distance,
        out RaycastHit closestHit)
    {
        closestHit = default;
        RaycastHit[] hits = Physics.SphereCastAll(
            startPosition,
            dashRadius,
            direction,
            distance,
            obstacleLayers,
            QueryTriggerInteraction.Ignore);
        float closestDistance = float.MaxValue;
        bool foundHit = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null ||
                IsSelfOrTeamCharacter(hitCollider.gameObject))
            {
                continue;
            }
            if (hits[i].distance >= closestDistance)
                continue;
            closestDistance = hits[i].distance;
            closestHit = hits[i];
            foundHit = true;
        }
        return foundHit;
    }

    private void DamageDashPath(
        Vector3 startPosition,
        Vector3 endPosition)
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
            : playerRigidbody.rotation;
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

    private void RotatePlayer(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return;
        Quaternion targetRotation =
            Quaternion.LookRotation(direction, Vector3.up);
        Quaternion nextRotation = Quaternion.RotateTowards(
            playerRigidbody.rotation,
            targetRotation,
            ringRotationSpeed * Time.fixedDeltaTime);
        playerRigidbody.MoveRotation(nextRotation);
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
        isFollowingRings = false;
        nextDashTime = Time.time + dashCoolDown;
        if (dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
            dashCoroutine = null;
        }
        if (playerRigidbody != null)
        {
            playerRigidbody.isKinematic = previousKinematicState;
            playerRigidbody.useGravity = previousGravityState;
            if (applyExitMomentum && !playerRigidbody.isKinematic)
            {
                playerRigidbody.linearVelocity =
                    dashDirection * exitSpeed;
            }
            else if (!playerRigidbody.isKinematic)
            {
                playerRigidbody.linearVelocity = Vector3.zero;
            }
        }
        currentRing = null;
        PlayAnimation(exitAnimation);
        PlaySound(dashEndSound);
        SetPresentation(false);
        RestoreMovement();
        damagedObjects.Clear();
        visitedRings.Clear();
        if (logStateChanges)
            Debug.Log("Light Speed Dash finished.", this);
    }

    private void RestoreMovement()
    {
        if (actionController != null &&
            actionController.CurrentAction ==
            TeamActionController.TeamAction.LightDash)
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
        if (playerAnimator == null ||
            string.IsNullOrWhiteSpace(animationName))
        {
            return;
        }
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
        ringSearchRadius = Mathf.Max(0.1f, ringSearchRadius);
        ringChainRadius = Mathf.Max(0.1f, ringChainRadius);
        ringDashSpeed = Mathf.Max(0.1f, ringDashSpeed);
        ringArrivalDistance = Mathf.Max(0f, ringArrivalDistance);
        ringRotationSpeed = Mathf.Max(0f, ringRotationSpeed);
        maximumRingDashTime = Mathf.Max(0.1f, maximumRingDashTime);
        exitSpeed = Mathf.Max(0f, exitSpeed);
        dashRadius = Mathf.Max(0.05f, dashRadius);
        damage = Mathf.Max(0f, damage);
        dashCoolDown = Mathf.Max(0f, dashCoolDown);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 direction = transform.forward;
        if (!preserveVerticalDirection)
            direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;
        direction.Normalize();
        if (drawDashPath)
        {
            Vector3 startPosition = transform.position;
            Vector3 endPosition =
                startPosition + direction * maximumDashDistance;
            Gizmos.DrawWireSphere(startPosition, dashRadius);
            Gizmos.DrawLine(startPosition, endPosition);
            Gizmos.DrawWireSphere(endPosition, dashRadius);
        }
        if (drawRingSearch)
        {
            Gizmos.DrawWireSphere(
                transform.position,
                ringSearchRadius);
        }
    }
}