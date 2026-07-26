using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thunder Shoot designed specifically for this hierarchy:
///
/// Test Player
/// ├── LeftPos
/// ├── RightPos
/// ├── Right Team Member  (Power teammate)
/// └── Left Team Member   (Fly leader / Tails)
///     └── Shoot Point
///
/// Attach this script to Left Team Member.
///
/// The script:
/// - Requires Fly Formation.
/// - Moves the Power teammate to Shoot Point.
/// - Launches the Power teammate toward a target.
/// - Damages or activates the target.
/// - Returns the Power teammate to RightPos.
/// - Never moves Test Player or the Fly leader.
/// </summary>
public class ThunderShoot : MonoBehaviour
{
    private enum ThunderShootState
    {
        Ready,
        Preparing,
        Flying,
        Returning,
        Cooldown
    }

    [Header("Core References")]

    [Tooltip("UltimatePlayerMovement on Test Player.")]
    [SerializeField]
    private UltimatePlayerMovement movement;

    [Tooltip("The TeamActionController controlling this team.")]
    [SerializeField]
    private TeamActionController actionController;

    [Tooltip("Left Team Member, such as Tails.")]
    [SerializeField]
    private Transform flyCharacter;

    [Tooltip("Right Team Member, such as Knuckles.")]
    [SerializeField]
    private Transform powerTeammate;

    [Tooltip("RightPos underneath Test Player.")]
    [SerializeField]
    private Transform powerReturnPoint;

    [Tooltip("Shoot Point underneath the Fly character.")]
    [SerializeField]
    private Transform shootPoint;

    [Tooltip("The main gameplay camera.")]
    [SerializeField]
    private Camera playerCamera;

    [Header("Follower Control")]

    [Tooltip(
        "Follower scripts on the Power teammate that should be disabled " +
        "while it is being launched. Leave empty if unnecessary.")]
    [SerializeField]
    private Behaviour[] powerFollowerBehaviours;

    [Header("Input")]

    [SerializeField]
    private KeyCode actionKey = KeyCode.B;

    [Tooltip("Allow Thunder Shoot while grounded.")]
    [SerializeField]
    private bool allowGroundedUse = true;

    [Tooltip("Allow Thunder Shoot while airborne.")]
    [SerializeField]
    private bool allowAirborneUse = true;

    [Header("Preparation")]

    [Tooltip("Time used to move the Power teammate to Shoot Point.")]
    [SerializeField, Min(0f)]
    private float preparationDuration = 0.12f;

    [Tooltip("Additional position offset from Shoot Point.")]
    [SerializeField]
    private Vector3 shootPointOffset = Vector3.zero;

    [Header("Targeting")]

    [Tooltip("Layers containing enemies and Thunder Shoot targets.")]
    [SerializeField]
    private LayerMask targetLayers;

    [Tooltip("Maximum target search range.")]
    [SerializeField, Min(0f)]
    private float targetingRadius = 20f;

    [Tooltip("Maximum angle from the camera or Fly character's forward direction.")]
    [SerializeField, Range(0f, 180f)]
    private float targetingAngle = 75f;

    [Tooltip("Vertical offset added to the target position.")]
    [SerializeField]
    private float targetHeightOffset = 0.5f;

    [Tooltip("Require an unobstructed path to a target.")]
    [SerializeField]
    private bool requireLineOfSight;

    [Tooltip("Layers that can block line of sight.")]
    [SerializeField]
    private LayerMask obstructionLayers;

    [Header("Projectile Movement")]

    [Tooltip("Initial launch speed.")]
    [SerializeField, Min(0f)]
    private float launchSpeed = 28f;

    [Tooltip("Maximum projectile speed.")]
    [SerializeField, Min(0f)]
    private float maximumSpeed = 38f;

    [Tooltip("Acceleration toward maximum speed.")]
    [SerializeField, Min(0f)]
    private float acceleration = 55f;

    [Tooltip("How strongly the teammate turns toward its target.")]
    [SerializeField, Min(0f)]
    private float homingSharpness = 12f;

    [Tooltip("Maximum projectile flight time.")]
    [SerializeField, Min(0.1f)]
    private float maximumFlightDuration = 1.25f;

    [Tooltip("Slight upward aim when no target is available.")]
    [SerializeField]
    private float noTargetUpwardBias = 0.08f;

    [Tooltip("Radius used when checking projectile impacts.")]
    [SerializeField, Min(0.01f)]
    private float impactRadius = 0.6f;

    [Tooltip("Layers that stop the projectile.")]
    [SerializeField]
    private LayerMask solidLayers;

    [Header("Impact")]

    [Tooltip("Damage sent through TakeDamage.")]
    [SerializeField, Min(0)]
    private int damage = 1;

    [Tooltip("Radius of the electric impact burst.")]
    [SerializeField, Min(0f)]
    private float electricBurstRadius = 1.25f;

    [Tooltip("Damage nearby targets when the projectile strikes.")]
    [SerializeField]
    private bool useElectricBurst = true;

    [Header("Return")]

    [Tooltip("How fast the Power teammate returns to RightPos.")]
    [SerializeField, Min(0.01f)]
    private float returnSpeed = 25f;

    [Tooltip("How smoothly the teammate turns toward RightPos.")]
    [SerializeField, Min(0f)]
    private float returnSharpness = 14f;

    [Tooltip("Maximum time allowed for returning.")]
    [SerializeField, Min(0.1f)]
    private float maximumReturnDuration = 1f;

    [Tooltip("Distance at which the return is considered complete.")]
    [SerializeField, Min(0.01f)]
    private float returnCompletionDistance = 0.15f;

    [Tooltip("Delay before Thunder Shoot may be used again.")]
    [SerializeField, Min(0f)]
    private float shotCooldown = 0.3f;

    [Header("Animation")]

    [SerializeField]
    private Animator flyAnimator;

    [SerializeField]
    private Animator powerAnimator;

    [Tooltip("Trigger played by the Fly leader when shooting.")]
    [SerializeField]
    private string shootTrigger = "";

    [Tooltip("Bool active while Thunder Shoot is running.")]
    [SerializeField]
    private string shootingBool = "";

    [Tooltip("Trigger played by the launched Power teammate.")]
    [SerializeField]
    private string projectileTrigger = "";

    [Header("Audio")]

    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip preparationSound;

    [SerializeField]
    private AudioClip launchSound;

    [SerializeField]
    private AudioClip impactSound;

    [Header("Effects")]

    [SerializeField]
    private ParticleSystem preparationEffect;

    [SerializeField]
    private ParticleSystem launchEffect;

    [SerializeField]
    private GameObject impactEffectPrefab;

    [Header("Runtime Debug")]

    [SerializeField]
    private ThunderShootState state = ThunderShootState.Ready;

    [SerializeField]
    private Transform currentTarget;

    [SerializeField]
    private float currentProjectileSpeed;

    private Coroutine thunderShootRoutine;

    private Transform originalPowerParent;
    private Vector3 originalPowerLocalPosition;
    private Quaternion originalPowerLocalRotation;
    private Vector3 originalPowerLocalScale;

    private Rigidbody powerRigidbody;
    private bool originalPowerKinematic;
    private bool originalPowerGravity;
    private RigidbodyConstraints originalPowerConstraints;

    private Collider[] powerColliders;
    private bool[] originalColliderStates;

    private bool[] originalFollowerBehaviourStates;

    private bool powerStateCached;
    private bool actionStarted;

    private readonly HashSet<GameObject> damagedTargets =
        new HashSet<GameObject>();

    public bool IsActive =>
        state != ThunderShootState.Ready &&
        state != ThunderShootState.Cooldown;

    public Transform CurrentTarget => currentTarget;

    private void Awake()
    {
        FindMissingReferences();
        CachePowerTeammateState();
    }

    private void Update()
    {
        if (Input.GetKeyDown(actionKey))
            TryStartThunderShoot();
    }

    private void FindMissingReferences()
    {
        if (flyCharacter == null)
            flyCharacter = transform;

        if (movement == null)
        {
            movement =
                GetComponentInParent<UltimatePlayerMovement>();
        }

        if (actionController == null)
        {
            actionController =
                GetComponent<TeamActionController>();
        }

        if (actionController == null)
        {
            actionController =
                GetComponentInParent<TeamActionController>();
        }

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (flyAnimator == null &&
            flyCharacter != null)
        {
            flyAnimator =
                flyCharacter.GetComponentInChildren<Animator>();
        }

        if (powerAnimator == null &&
            powerTeammate != null)
        {
            powerAnimator =
                powerTeammate.GetComponentInChildren<Animator>();
        }

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    /// <summary>
    /// Attempts to start Thunder Shoot.
    /// </summary>
    public bool TryStartThunderShoot()
    {
        if (state != ThunderShootState.Ready)
            return false;

        if (!ReferencesAreValid())
            return false;

        bool grounded =
            movement != null &&
            movement.isGrounded;

        if (grounded && !allowGroundedUse)
            return false;

        if (!grounded && !allowAirborneUse)
            return false;

        if (actionController != null)
        {
            bool accepted =
                actionController.TryBeginAction(
                    TeamActionController.TeamAction.ThunderShoot,
                    TeamActionController.TeamFormation.Fly,
                    mustBeGrounded: false,
                    mustBeAirborne: false,
                    surrenderMovementControl: false);

            if (!accepted)
                return false;

            actionStarted = true;
        }

        currentTarget = FindBestTarget();
        damagedTargets.Clear();

        thunderShootRoutine =
            StartCoroutine(ThunderShootRoutine());

        return true;
    }

    private bool ReferencesAreValid()
    {
        if (flyCharacter == null)
        {
            Debug.LogWarning(
                "Thunder Shoot: Fly Character is not assigned.",
                this);

            return false;
        }

        if (powerTeammate == null)
        {
            Debug.LogWarning(
                "Thunder Shoot: Power Teammate is not assigned.",
                this);

            return false;
        }

        if (powerReturnPoint == null)
        {
            Debug.LogWarning(
                "Thunder Shoot: Power Return Point is not assigned.",
                this);

            return false;
        }

        if (shootPoint == null)
        {
            Debug.LogWarning(
                "Thunder Shoot: Shoot Point is not assigned.",
                this);

            return false;
        }

        return true;
    }

    private IEnumerator ThunderShootRoutine()
    {
        state = ThunderShootState.Preparing;

        PreparePowerTeammate();
        StartPreparationPresentation();

        yield return MovePowerToShootPoint();

        if (powerTeammate == null)
        {
            CancelThunderShoot();
            yield break;
        }

        StopPreparationPresentation();
        StartLaunchPresentation();

        state = ThunderShootState.Flying;

        yield return FlyPowerProjectile();

        if (powerTeammate == null)
        {
            CancelThunderShoot();
            yield break;
        }

        state = ThunderShootState.Returning;

        yield return ReturnPowerTeammate();

        RestorePowerTeammate();
        StopAllPresentation();
        FinishTeamAction();

        state = ThunderShootState.Cooldown;

        if (shotCooldown > 0f)
            yield return new WaitForSeconds(shotCooldown);

        currentTarget = null;
        currentProjectileSpeed = 0f;
        thunderShootRoutine = null;
        state = ThunderShootState.Ready;
    }

    private IEnumerator MovePowerToShootPoint()
    {
        Vector3 startPosition =
            powerTeammate.position;

        Quaternion startRotation =
            powerTeammate.rotation;

        float elapsed = 0f;

        if (preparationDuration <= 0f)
        {
            powerTeammate.position =
                GetShootPosition();

            powerTeammate.rotation =
                GetLaunchRotation();

            yield break;
        }

        while (elapsed < preparationDuration)
        {
            if (powerTeammate == null)
                yield break;

            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / preparationDuration);

            t = SmoothStep(t);

            powerTeammate.position =
                Vector3.Lerp(
                    startPosition,
                    GetShootPosition(),
                    t);

            powerTeammate.rotation =
                Quaternion.Slerp(
                    startRotation,
                    GetLaunchRotation(),
                    t);

            yield return null;
        }

        powerTeammate.position =
            GetShootPosition();

        powerTeammate.rotation =
            GetLaunchRotation();
    }

    private IEnumerator FlyPowerProjectile()
    {
        Vector3 direction =
            GetInitialLaunchDirection();

        currentProjectileSpeed = launchSpeed;

        float elapsed = 0f;

        while (elapsed < maximumFlightDuration &&
               state == ThunderShootState.Flying)
        {
            if (powerTeammate == null)
                yield break;

            elapsed += Time.fixedDeltaTime;

            if (!IsTargetValid(currentTarget))
                currentTarget = FindBestTarget();

            Vector3 desiredDirection =
                GetDesiredFlightDirection(direction);

            float turnAmount =
                1f - Mathf.Exp(
                    -homingSharpness *
                    Time.fixedDeltaTime);

            direction =
                Vector3.Slerp(
                    direction,
                    desiredDirection,
                    turnAmount).normalized;

            currentProjectileSpeed =
                Mathf.MoveTowards(
                    currentProjectileSpeed,
                    maximumSpeed,
                    acceleration *
                    Time.fixedDeltaTime);

            Vector3 startPosition =
                powerTeammate.position;

            float movementDistance =
                currentProjectileSpeed *
                Time.fixedDeltaTime;

            if (CheckSolidImpact(
                startPosition,
                direction,
                movementDistance,
                out RaycastHit solidHit))
            {
                powerTeammate.position =
                    solidHit.point;

                ProcessImpact(
                    solidHit.collider,
                    solidHit.point);

                yield break;
            }

            powerTeammate.position +=
                direction * movementDistance;

            if (direction.sqrMagnitude > 0.001f)
            {
                powerTeammate.rotation =
                    Quaternion.LookRotation(
                        direction,
                        Vector3.up);
            }

            Collider targetHit =
                FindTargetAtPosition(
                    powerTeammate.position);

            if (targetHit != null)
            {
                ProcessImpact(
                    targetHit,
                    powerTeammate.position);

                yield break;
            }

            yield return new WaitForFixedUpdate();
        }
    }

    private Vector3 GetDesiredFlightDirection(
        Vector3 currentDirection)
    {
        if (!IsTargetValid(currentTarget))
            return currentDirection;

        Vector3 targetPosition =
            currentTarget.position +
            Vector3.up * targetHeightOffset;

        Vector3 direction =
            targetPosition -
            powerTeammate.position;

        if (direction.sqrMagnitude < 0.001f)
            return currentDirection;

        return direction.normalized;
    }

    private bool CheckSolidImpact(
        Vector3 origin,
        Vector3 direction,
        float distance,
        out RaycastHit hit)
    {
        if (solidLayers.value == 0)
        {
            hit = default;
            return false;
        }

        return Physics.SphereCast(
            origin,
            impactRadius * 0.75f,
            direction,
            out hit,
            distance,
            solidLayers,
            QueryTriggerInteraction.Ignore);
    }

    private Collider FindTargetAtPosition(
        Vector3 position)
    {
        if (targetLayers.value == 0)
            return null;

        Collider[] hits =
            Physics.OverlapSphere(
                position,
                impactRadius,
                targetLayers,
                QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            if (IsValidTargetCollider(hit))
                return hit;
        }

        return null;
    }

    private bool IsValidTargetCollider(
        Collider targetCollider)
    {
        if (targetCollider == null)
            return false;

        if (powerTeammate != null &&
            targetCollider.transform.IsChildOf(
                powerTeammate))
        {
            return false;
        }

        if (flyCharacter != null &&
            targetCollider.transform.IsChildOf(
                flyCharacter))
        {
            return false;
        }

        if (movement != null &&
            targetCollider.transform.IsChildOf(
                movement.transform))
        {
            return false;
        }

        return true;
    }

    private void ProcessImpact(
        Collider directHit,
        Vector3 impactPosition)
    {
        if (directHit != null)
        {
            GameObject directTarget =
                GetTargetObject(directHit);

            DamageTarget(directTarget);
        }

        if (useElectricBurst &&
            electricBurstRadius > 0f &&
            targetLayers.value != 0)
        {
            Collider[] nearbyTargets =
                Physics.OverlapSphere(
                    impactPosition,
                    electricBurstRadius,
                    targetLayers,
                    QueryTriggerInteraction.Collide);

            foreach (Collider targetCollider
                     in nearbyTargets)
            {
                if (!IsValidTargetCollider(
                    targetCollider))
                {
                    continue;
                }

                DamageTarget(
                    GetTargetObject(
                        targetCollider));
            }
        }

        SpawnImpactEffect(impactPosition);
        PlayImpactSound();

        state = ThunderShootState.Returning;
    }

    private GameObject GetTargetObject(
        Collider targetCollider)
    {
        if (targetCollider == null)
            return null;

        if (targetCollider.attachedRigidbody != null)
        {
            return targetCollider
                .attachedRigidbody
                .gameObject;
        }

        return targetCollider
            .transform
            .root
            .gameObject;
    }

    private void DamageTarget(GameObject target)
    {
        if (target == null)
            return;

        if (!damagedTargets.Add(target))
            return;

        target.SendMessage(
            "OnThunderShootHit",
            gameObject,
            SendMessageOptions.DontRequireReceiver);

        target.SendMessage(
            "ThunderShootHit",
            gameObject,
            SendMessageOptions.DontRequireReceiver);

        target.SendMessage(
            "Stun",
            SendMessageOptions.DontRequireReceiver);

        target.SendMessage(
            "Activate",
            SendMessageOptions.DontRequireReceiver);

        if (damage > 0)
        {
            target.SendMessage(
                "TakeDamage",
                damage,
                SendMessageOptions.DontRequireReceiver);
        }
    }

    private IEnumerator ReturnPowerTeammate()
    {
        float elapsed = 0f;
        Vector3 returnVelocity = Vector3.zero;

        while (elapsed < maximumReturnDuration)
        {
            if (powerTeammate == null ||
                powerReturnPoint == null)
            {
                yield break;
            }

            elapsed += Time.fixedDeltaTime;

            Vector3 destination =
                powerReturnPoint.position;

            Vector3 toDestination =
                destination -
                powerTeammate.position;

            if (toDestination.sqrMagnitude <=
                returnCompletionDistance *
                returnCompletionDistance)
            {
                powerTeammate.position =
                    destination;

                yield break;
            }

            Vector3 desiredVelocity =
                toDestination.normalized *
                returnSpeed;

            float returnAmount =
                1f - Mathf.Exp(
                    -returnSharpness *
                    Time.fixedDeltaTime);

            returnVelocity =
                Vector3.Lerp(
                    returnVelocity,
                    desiredVelocity,
                    returnAmount);

            Vector3 movementStep =
                returnVelocity *
                Time.fixedDeltaTime;

            if (movementStep.sqrMagnitude >
                toDestination.sqrMagnitude)
            {
                movementStep = toDestination;
            }

            powerTeammate.position +=
                movementStep;

            if (returnVelocity.sqrMagnitude > 0.001f)
            {
                powerTeammate.rotation =
                    Quaternion.LookRotation(
                        returnVelocity.normalized,
                        Vector3.up);
            }

            yield return new WaitForFixedUpdate();
        }

        if (powerTeammate != null &&
            powerReturnPoint != null)
        {
            powerTeammate.position =
                powerReturnPoint.position;
        }
    }

    private Transform FindBestTarget()
    {
        if (targetLayers.value == 0)
            return null;

        Vector3 origin =
            GetShootPosition();

        Vector3 aimDirection =
            GetAimDirection();

        Collider[] candidates =
            Physics.OverlapSphere(
                origin,
                targetingRadius,
                targetLayers,
                QueryTriggerInteraction.Collide);

        Transform bestTarget = null;
        float bestScore = Mathf.Infinity;

        HashSet<GameObject> processedTargets =
            new HashSet<GameObject>();

        foreach (Collider candidate in candidates)
        {
            if (!IsValidTargetCollider(candidate))
                continue;

            GameObject targetObject =
                GetTargetObject(candidate);

            if (targetObject == null ||
                !processedTargets.Add(targetObject))
            {
                continue;
            }

            Vector3 targetPosition =
                candidate.bounds.center;

            Vector3 toTarget =
                targetPosition - origin;

            float distance =
                toTarget.magnitude;

            if (distance <= 0.001f)
                continue;

            Vector3 direction =
                toTarget / distance;

            float angle =
                Vector3.Angle(
                    aimDirection,
                    direction);

            if (angle > targetingAngle)
                continue;

            if (requireLineOfSight &&
                !HasLineOfSight(
                    origin,
                    candidate,
                    targetPosition))
            {
                continue;
            }

            float angleScore =
                angle /
                Mathf.Max(targetingAngle, 0.01f);

            float distanceScore =
                distance /
                Mathf.Max(targetingRadius, 0.01f);

            float totalScore =
                angleScore * 0.7f +
                distanceScore * 0.3f;

            if (totalScore < bestScore)
            {
                bestScore = totalScore;

                bestTarget =
                    candidate.attachedRigidbody != null
                        ? candidate.attachedRigidbody.transform
                        : candidate.transform;
            }
        }

        return bestTarget;
    }

    private bool HasLineOfSight(
        Vector3 origin,
        Collider targetCollider,
        Vector3 targetPosition)
    {
        Vector3 direction =
            targetPosition - origin;

        float distance =
            direction.magnitude;

        if (distance <= 0.001f)
            return true;

        direction /= distance;

        if (!Physics.Raycast(
            origin,
            direction,
            out RaycastHit hit,
            distance,
            obstructionLayers,
            QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        return hit.collider == targetCollider ||
               hit.transform.IsChildOf(
                   targetCollider.transform) ||
               targetCollider.transform.IsChildOf(
                   hit.transform);
    }

    private bool IsTargetValid(Transform target)
    {
        return target != null &&
               target.gameObject.activeInHierarchy;
    }

    private Vector3 GetInitialLaunchDirection()
    {
        if (IsTargetValid(currentTarget))
        {
            Vector3 targetPosition =
                currentTarget.position +
                Vector3.up * targetHeightOffset;

            Vector3 direction =
                targetPosition -
                GetShootPosition();

            if (direction.sqrMagnitude > 0.001f)
                return direction.normalized;
        }

        Vector3 directionWithoutTarget =
            GetAimDirection() +
            Vector3.up * noTargetUpwardBias;

        if (directionWithoutTarget.sqrMagnitude <
            0.001f)
        {
            directionWithoutTarget =
                flyCharacter.forward;
        }

        return directionWithoutTarget.normalized;
    }

    private Vector3 GetAimDirection()
    {
        if (playerCamera != null)
        {
            Vector3 cameraForward =
                playerCamera.transform.forward;

            if (cameraForward.sqrMagnitude > 0.001f)
                return cameraForward.normalized;
        }

        if (flyCharacter != null)
            return flyCharacter.forward.normalized;

        return transform.forward.normalized;
    }

    private Vector3 GetShootPosition()
    {
        if (shootPoint == null)
            return transform.position;

        return shootPoint.TransformPoint(
            shootPointOffset);
    }

    private Quaternion GetLaunchRotation()
    {
        Vector3 direction =
            GetInitialLaunchDirection();

        if (direction.sqrMagnitude < 0.001f)
            return powerTeammate.rotation;

        return Quaternion.LookRotation(
            direction,
            Vector3.up);
    }

    private void CachePowerTeammateState()
    {
        if (powerTeammate == null ||
            powerStateCached)
        {
            return;
        }

        originalPowerParent =
            powerTeammate.parent;

        originalPowerLocalPosition =
            powerTeammate.localPosition;

        originalPowerLocalRotation =
            powerTeammate.localRotation;

        originalPowerLocalScale =
            powerTeammate.localScale;

        powerRigidbody =
            powerTeammate.GetComponent<Rigidbody>();

        if (powerRigidbody == null)
        {
            powerRigidbody =
                powerTeammate
                    .GetComponentInChildren<Rigidbody>();
        }

        if (powerRigidbody != null)
        {
            originalPowerKinematic =
                powerRigidbody.isKinematic;

            originalPowerGravity =
                powerRigidbody.useGravity;

            originalPowerConstraints =
                powerRigidbody.constraints;
        }

        powerColliders =
            powerTeammate.GetComponentsInChildren<Collider>(
                true);

        originalColliderStates =
            new bool[powerColliders.Length];

        for (int i = 0;
             i < powerColliders.Length;
             i++)
        {
            originalColliderStates[i] =
                powerColliders[i] != null &&
                powerColliders[i].enabled;
        }

        originalFollowerBehaviourStates =
            new bool[powerFollowerBehaviours.Length];

        for (int i = 0;
             i < powerFollowerBehaviours.Length;
             i++)
        {
            originalFollowerBehaviourStates[i] =
                powerFollowerBehaviours[i] != null &&
                powerFollowerBehaviours[i].enabled;
        }

        powerStateCached = true;
    }

    private void PreparePowerTeammate()
    {
        CachePowerTeammateState();

        if (powerTeammate == null)
            return;

        for (int i = 0;
             i < powerFollowerBehaviours.Length;
             i++)
        {
            if (powerFollowerBehaviours[i] != null)
                powerFollowerBehaviours[i].enabled = false;
        }

        powerTeammate.SetParent(
            null,
            true);

        if (powerRigidbody != null)
        {
            powerRigidbody.linearVelocity =
                Vector3.zero;

            powerRigidbody.angularVelocity =
                Vector3.zero;

            powerRigidbody.useGravity = false;
            powerRigidbody.isKinematic = true;
        }

        foreach (Collider powerCollider
                 in powerColliders)
        {
            if (powerCollider != null)
                powerCollider.enabled = false;
        }

        if (powerAnimator != null &&
            !string.IsNullOrWhiteSpace(
                projectileTrigger))
        {
            powerAnimator.SetTrigger(
                projectileTrigger);
        }
    }

    private void RestorePowerTeammate()
    {
        if (!powerStateCached ||
            powerTeammate == null)
        {
            return;
        }

        powerTeammate.SetParent(
            originalPowerParent,
            false);

        powerTeammate.localPosition =
            originalPowerLocalPosition;

        powerTeammate.localRotation =
            originalPowerLocalRotation;

        powerTeammate.localScale =
            originalPowerLocalScale;

        if (powerRigidbody != null)
        {
            powerRigidbody.linearVelocity =
                Vector3.zero;

            powerRigidbody.angularVelocity =
                Vector3.zero;

            powerRigidbody.isKinematic =
                originalPowerKinematic;

            powerRigidbody.useGravity =
                originalPowerGravity;

            powerRigidbody.constraints =
                originalPowerConstraints;
        }

        int colliderCount =
            Mathf.Min(
                powerColliders.Length,
                originalColliderStates.Length);

        for (int i = 0;
             i < colliderCount;
             i++)
        {
            if (powerColliders[i] != null)
            {
                powerColliders[i].enabled =
                    originalColliderStates[i];
            }
        }

        int behaviourCount =
            Mathf.Min(
                powerFollowerBehaviours.Length,
                originalFollowerBehaviourStates.Length);

        for (int i = 0;
             i < behaviourCount;
             i++)
        {
            if (powerFollowerBehaviours[i] != null)
            {
                powerFollowerBehaviours[i].enabled =
                    originalFollowerBehaviourStates[i];
            }
        }
    }

    /// <summary>
    /// Immediately stops Thunder Shoot and restores the Power teammate.
    /// </summary>
    public void CancelThunderShoot()
    {
        if (thunderShootRoutine != null)
        {
            StopCoroutine(thunderShootRoutine);
            thunderShootRoutine = null;
        }

        RestorePowerTeammate();
        StopAllPresentation();
        FinishTeamAction();

        currentTarget = null;
        currentProjectileSpeed = 0f;
        damagedTargets.Clear();
        state = ThunderShootState.Ready;
    }

    private void FinishTeamAction()
    {
        if (!actionStarted)
            return;

        if (actionController != null &&
            actionController.CurrentAction ==
            TeamActionController.TeamAction.ThunderShoot)
        {
            actionController.EndAction();
        }

        actionStarted = false;
    }

    private void StartPreparationPresentation()
    {
        if (flyAnimator != null)
        {
            if (!string.IsNullOrWhiteSpace(
                shootTrigger))
            {
                flyAnimator.SetTrigger(
                    shootTrigger);
            }

            if (!string.IsNullOrWhiteSpace(
                shootingBool))
            {
                flyAnimator.SetBool(
                    shootingBool,
                    true);
            }
        }

        if (preparationEffect != null)
            preparationEffect.Play();

        if (audioSource != null &&
            preparationSound != null)
        {
            audioSource.PlayOneShot(
                preparationSound);
        }
    }

    private void StopPreparationPresentation()
    {
        if (preparationEffect != null)
        {
            preparationEffect.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void StartLaunchPresentation()
    {
        if (launchEffect != null)
            launchEffect.Play();

        if (audioSource != null &&
            launchSound != null)
        {
            audioSource.PlayOneShot(
                launchSound);
        }
    }

    private void StopAllPresentation()
    {
        StopPreparationPresentation();

        if (flyAnimator != null &&
            !string.IsNullOrWhiteSpace(
                shootingBool))
        {
            flyAnimator.SetBool(
                shootingBool,
                false);
        }

        if (launchEffect != null)
        {
            launchEffect.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void SpawnImpactEffect(
        Vector3 position)
    {
        if (impactEffectPrefab == null)
            return;

        GameObject effect =
            Instantiate(
                impactEffectPrefab,
                position,
                Quaternion.identity);

        Destroy(effect, 5f);
    }

    private void PlayImpactSound()
    {
        if (audioSource != null &&
            impactSound != null)
        {
            audioSource.PlayOneShot(
                impactSound);
        }
    }

    private void OnDisable()
    {
        if (state != ThunderShootState.Ready)
            CancelThunderShoot();
    }

    private void OnDestroy()
    {
        RestorePowerTeammate();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin =
            shootPoint != null
                ? shootPoint.position
                : transform.position;

        Gizmos.DrawWireSphere(
            origin,
            targetingRadius);

        Gizmos.DrawWireSphere(
            origin,
            impactRadius);

        if (powerReturnPoint != null)
        {
            Gizmos.DrawWireSphere(
                powerReturnPoint.position,
                returnCompletionDistance);
        }

        Vector3 aimDirection =
            playerCamera != null
                ? playerCamera.transform.forward
                : transform.forward;

        Gizmos.DrawLine(
            origin,
            origin +
            aimDirection.normalized *
            targetingRadius);
    }

    private static float SmoothStep(float value)
    {
        return value *
               value *
               (3f - 2f * value);
    }
}