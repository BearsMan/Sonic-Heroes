using System.Collections;
using UnityEngine;

/// <summary>
/// Sonic Heroes-style Triangle Dive.
///
/// Behaviour:
/// - Requires Power Formation.
/// - Starts while airborne by pressing the jump button again.
/// - Places the other two teammates into a triangle formation.
/// - Slows falling while preserving forward movement.
/// - Allows limited horizontal steering.
/// - Rises when inside an updraft.
/// - Ends when the player lands, releases the button, or cancels.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class TriangleDive : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private UltimatePlayerMovement movement;
    [SerializeField] private TeamActionController actionController;
    [SerializeField] private Rigidbody body;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform cameraTransform;

    [Header("Team References")]
    [Tooltip("The currently controlled Power character.")]
    [SerializeField] private Transform powerCharacter;

    [Tooltip("The existing Speed teammate. Do not use a prefab.")]
    [SerializeField] private Transform speedCharacter;

    [Tooltip("The existing Fly teammate. Do not use a prefab.")]
    [SerializeField] private Transform flyCharacter;

    [Header("Triangle Formation")]
    [Tooltip("Speed teammate's local position relative to the Power character.")]
    [SerializeField]
    private Vector3 speedFormationOffset =
        new Vector3(-1.35f, 0.15f, -0.15f);

    [Tooltip("Fly teammate's local position relative to the Power character.")]
    [SerializeField]
    private Vector3 flyFormationOffset =
        new Vector3(1.35f, 0.15f, -0.15f);

    [SerializeField, Min(0.01f)]
    private float formationEnterTime = 0.15f;

    [SerializeField, Min(0.01f)]
    private float formationExitTime = 0.12f;

    [SerializeField, Min(0f)]
    private float teammatePositionSharpness = 20f;

    [SerializeField, Min(0f)]
    private float teammateRotationSharpness = 15f;

    [Header("Input")]
    [SerializeField] private KeyCode diveKey = KeyCode.Space;

    [Tooltip("End Triangle Dive when the jump button is released.")]
    [SerializeField] private bool requireButtonHeld = true;

    [Tooltip(
        "Prevents Triangle Dive from starting on the same input that began the jump.")]
    [SerializeField, Min(0f)]
    private float minimumAirTime = 0.08f;

    [Header("Glide Movement")]
    [Tooltip("Minimum forward speed while Triangle Diving.")]
    [SerializeField, Min(0f)]
    private float forwardSpeed = 10f;

    [Tooltip("Maximum forward speed preserved from the player's current velocity.")]
    [SerializeField, Min(0f)]
    private float maximumForwardSpeed = 24f;

    [Tooltip("Normal downward speed during the glide.")]
    [SerializeField, Min(0f)]
    private float fallSpeed = 2.5f;

    [Tooltip("How quickly vertical velocity approaches the target fall speed.")]
    [SerializeField, Min(0f)]
    private float verticalAcceleration = 16f;

    [Tooltip("Horizontal steering strength.")]
    [SerializeField, Min(0f)]
    private float steeringAcceleration = 16f;

    [Tooltip("Maximum sideways speed created by steering.")]
    [SerializeField, Min(0f)]
    private float maximumSideSpeed = 7f;

    [Tooltip("How quickly the team turns toward the movement direction.")]
    [SerializeField, Min(0f)]
    private float turningSharpness = 10f;

    [Header("Updraft")]
    [Tooltip("Layers containing Triangle Dive updraft triggers.")]
    [SerializeField] private LayerMask updraftLayers;

    [Tooltip("Upward speed while riding an updraft.")]
    [SerializeField, Min(0f)]
    private float updraftRiseSpeed = 12f;

    [Tooltip("How quickly upward speed is gained inside an updraft.")]
    [SerializeField, Min(0f)]
    private float updraftAcceleration = 22f;

    [Tooltip(
        "Seconds to retain the updraft after leaving its trigger. " +
        "This prevents flickering near trigger boundaries.")]
    [SerializeField, Min(0f)]
    private float updraftGraceTime = 0.08f;

    [Header("Limits")]
    [Tooltip("Set to zero for no duration limit.")]
    [SerializeField, Min(0f)]
    private float maximumDiveDuration;

    [Header("Animation")]
    [SerializeField] private string triangleDiveBool = "Triangle Dive";

    [SerializeField]
    private string triangleDiveTrigger =
        "Start Triangle Dive";

    [Header("Effects")]
    [SerializeField] private ParticleSystem diveEffect;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip startSound;
    [SerializeField] private AudioClip updraftSound;

    [Header("Runtime Debug")]
    [SerializeField] private bool diving;
    [SerializeField] private bool insideUpdraft;

    private Coroutine formationRoutine;

    private Transform originalSpeedParent;
    private Transform originalFlyParent;

    private Vector3 originalSpeedLocalPosition;
    private Vector3 originalFlyLocalPosition;

    private Quaternion originalSpeedLocalRotation;
    private Quaternion originalFlyLocalRotation;

    private float airborneTime;
    private float diveTime;
    private float lastUpdraftTime = float.NegativeInfinity;

    private bool cachedOriginalTransforms;
    private bool playedUpdraftSound;

    public bool IsDiving => diving;

    private bool UpdraftActive =>
        insideUpdraft ||
        Time.time - lastUpdraftTime <= updraftGraceTime;

    private void Awake()
    {
        if (body == null)
            body = GetComponent<Rigidbody>();

        if (movement == null)
            movement = GetComponent<UltimatePlayerMovement>();

        if (movement == null)
            movement = GetComponentInParent<UltimatePlayerMovement>();

        if (actionController == null)
            actionController = GetComponent<TeamActionController>();

        if (actionController == null)
            actionController = GetComponentInParent<TeamActionController>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (powerCharacter == null)
            powerCharacter = transform;

        CacheTeammateTransforms();
    }

    private void Update()
    {
        if (movement == null || body == null)
            return;

        UpdateAirborneTimer();

        if (!diving)
        {
            if (CanReadStartInput())
                TryStartTriangleDive();

            return;
        }

        diveTime += Time.deltaTime;

        if (ShouldEndDive())
            StopTriangleDive();
    }

    private void FixedUpdate()
    {
        if (!diving || body == null)
            return;

        ApplyDiveMovement();
        UpdateTeamFormation();
    }

    private void UpdateAirborneTimer()
    {
        if (movement.isGrounded)
            airborneTime = 0f;
        else
            airborneTime += Time.deltaTime;
    }

    private bool CanReadStartInput()
    {
        if (movement.isGrounded)
            return false;

        if (airborneTime < minimumAirTime)
            return false;

        return Input.GetKeyDown(diveKey);
    }

    public bool TryStartTriangleDive()
    {
        if (diving)
            return false;

        if (movement == null || body == null)
            return false;

        if (movement.isGrounded)
            return false;

        if (airborneTime < minimumAirTime)
            return false;

        if (actionController != null)
        {
            bool accepted = actionController.TryBeginAction(
                TeamActionController.TeamAction.TriangleDive,
                TeamActionController.TeamFormation.Power,
                mustBeGrounded: false,
                mustBeAirborne: true,
                surrenderMovementControl: true);

            if (accepted == false)
                return false;
        }
        else
        {
            UltimatePlayerMovement.Controllable = false;
        }

        CacheTeammateTransforms();

        diving = true;
        diveTime = 0f;
        insideUpdraft = false;
        playedUpdraftSound = false;

        StartPresentation();
        BeginFormation();

        return true;
    }

    private bool ShouldEndDive()
    {
        if (movement.isGrounded)
            return true;

        if (requireButtonHeld && !Input.GetKey(diveKey))
            return true;

        if (maximumDiveDuration > 0f &&
            diveTime >= maximumDiveDuration)
        {
            return true;
        }

        return false;
    }

    private void ApplyDiveMovement()
    {
        Vector3 currentVelocity = body.linearVelocity;

        Vector3 horizontalVelocity =
            Vector3.ProjectOnPlane(currentVelocity, Vector3.up);

        Vector3 facingDirection =
            Vector3.ProjectOnPlane(transform.forward, Vector3.up);

        if (facingDirection.sqrMagnitude < 0.001f)
            facingDirection = Vector3.forward;

        facingDirection.Normalize();

        float retainedForwardSpeed =
            Vector3.Dot(horizontalVelocity, facingDirection);

        retainedForwardSpeed = Mathf.Clamp(
            retainedForwardSpeed,
            forwardSpeed,
            maximumForwardSpeed);

        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");

        Vector3 cameraForward = facingDirection;
        Vector3 cameraRight = transform.right;

        if (cameraTransform != null)
        {
            cameraForward = Vector3.ProjectOnPlane(
                cameraTransform.forward,
                Vector3.up).normalized;

            cameraRight = Vector3.ProjectOnPlane(
                cameraTransform.right,
                Vector3.up).normalized;
        }

        Vector3 steeringDirection =
            cameraRight * horizontalInput +
            cameraForward * Mathf.Max(0f, verticalInput);

        if (steeringDirection.sqrMagnitude > 1f)
            steeringDirection.Normalize();

        Vector3 desiredHorizontalVelocity =
            facingDirection * retainedForwardSpeed;

        desiredHorizontalVelocity +=
            steeringDirection * maximumSideSpeed;

        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            desiredHorizontalVelocity,
            steeringAcceleration * Time.fixedDeltaTime);

        float targetVerticalSpeed =
            UpdraftActive ? updraftRiseSpeed : -fallSpeed;

        float verticalSpeed = Mathf.MoveTowards(
            currentVelocity.y,
            targetVerticalSpeed,
            (UpdraftActive
                ? updraftAcceleration
                : verticalAcceleration) * Time.fixedDeltaTime);

        body.linearVelocity =
            horizontalVelocity +
            Vector3.up * verticalSpeed;

        RotateTowardVelocity(horizontalVelocity);
        UpdateUpdraftPresentation();
    }

    private void RotateTowardVelocity(Vector3 horizontalVelocity)
    {
        if (horizontalVelocity.sqrMagnitude < 0.01f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(
            horizontalVelocity.normalized,
            Vector3.up);

        float rotationT =
            1f - Mathf.Exp(-turningSharpness * Time.fixedDeltaTime);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationT);
    }

    private void BeginFormation()
    {
        if (formationRoutine != null)
            StopCoroutine(formationRoutine);

        formationRoutine = StartCoroutine(
            BlendIntoFormation());
    }

    private IEnumerator BlendIntoFormation()
    {
        float elapsed = 0f;

        Vector3 speedStartPosition =
            speedCharacter != null
                ? speedCharacter.position
                : Vector3.zero;

        Vector3 flyStartPosition =
            flyCharacter != null
                ? flyCharacter.position
                : Vector3.zero;

        Quaternion speedStartRotation =
            speedCharacter != null
                ? speedCharacter.rotation
                : Quaternion.identity;

        Quaternion flyStartRotation =
            flyCharacter != null
                ? flyCharacter.rotation
                : Quaternion.identity;

        while (elapsed < formationEnterTime && diving)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / formationEnterTime);

            t = SmoothStep(t);

            if (speedCharacter != null)
            {
                speedCharacter.position = Vector3.Lerp(
                    speedStartPosition,
                    GetFormationPosition(speedFormationOffset),
                    t);

                speedCharacter.rotation = Quaternion.Slerp(
                    speedStartRotation,
                    powerCharacter.rotation,
                    t);
            }

            if (flyCharacter != null)
            {
                flyCharacter.position = Vector3.Lerp(
                    flyStartPosition,
                    GetFormationPosition(flyFormationOffset),
                    t);

                flyCharacter.rotation = Quaternion.Slerp(
                    flyStartRotation,
                    powerCharacter.rotation,
                    t);
            }

            yield return null;
        }

        formationRoutine = null;
    }

    private void UpdateTeamFormation()
    {
        if (powerCharacter == null)
            return;

        float positionT =
            1f - Mathf.Exp(
                -teammatePositionSharpness *
                Time.fixedDeltaTime);

        float rotationT =
            1f - Mathf.Exp(
                -teammateRotationSharpness *
                Time.fixedDeltaTime);

        if (speedCharacter != null)
        {
            speedCharacter.position = Vector3.Lerp(
                speedCharacter.position,
                GetFormationPosition(speedFormationOffset),
                positionT);

            speedCharacter.rotation = Quaternion.Slerp(
                speedCharacter.rotation,
                powerCharacter.rotation,
                rotationT);
        }

        if (flyCharacter != null)
        {
            flyCharacter.position = Vector3.Lerp(
                flyCharacter.position,
                GetFormationPosition(flyFormationOffset),
                positionT);

            flyCharacter.rotation = Quaternion.Slerp(
                flyCharacter.rotation,
                powerCharacter.rotation,
                rotationT);
        }
    }

    private Vector3 GetFormationPosition(Vector3 localOffset)
    {
        Transform center =
            powerCharacter != null
                ? powerCharacter
                : transform;

        return center.TransformPoint(localOffset);
    }

    public void StopTriangleDive()
    {
        if (!diving)
            return;

        diving = false;
        insideUpdraft = false;
        playedUpdraftSound = false;

        StopPresentation();

        if (formationRoutine != null)
        {
            StopCoroutine(formationRoutine);
            formationRoutine = null;
        }

        StartCoroutine(RestoreFormation());

        if (actionController != null &&
            actionController.CurrentAction ==
            TeamActionController.TeamAction.TriangleDive)
        {
            actionController.EndAction();
        }
        else
        {
            UltimatePlayerMovement.Controllable = true;
        }
    }

    public void CancelTriangleDive()
    {
        StopTriangleDive();
    }

    private IEnumerator RestoreFormation()
    {
        if (!cachedOriginalTransforms)
            yield break;

        float elapsed = 0f;

        Vector3 speedStartPosition =
            speedCharacter != null
                ? speedCharacter.localPosition
                : Vector3.zero;

        Vector3 flyStartPosition =
            flyCharacter != null
                ? flyCharacter.localPosition
                : Vector3.zero;

        Quaternion speedStartRotation =
            speedCharacter != null
                ? speedCharacter.localRotation
                : Quaternion.identity;

        Quaternion flyStartRotation =
            flyCharacter != null
                ? flyCharacter.localRotation
                : Quaternion.identity;

        if (speedCharacter != null &&
            speedCharacter.parent != originalSpeedParent)
        {
            speedCharacter.SetParent(
                originalSpeedParent,
                true);
        }

        if (flyCharacter != null &&
            flyCharacter.parent != originalFlyParent)
        {
            flyCharacter.SetParent(
                originalFlyParent,
                true);
        }

        while (elapsed < formationExitTime)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / formationExitTime);

            t = SmoothStep(t);

            if (speedCharacter != null)
            {
                speedCharacter.localPosition = Vector3.Lerp(
                    speedStartPosition,
                    originalSpeedLocalPosition,
                    t);

                speedCharacter.localRotation = Quaternion.Slerp(
                    speedStartRotation,
                    originalSpeedLocalRotation,
                    t);
            }

            if (flyCharacter != null)
            {
                flyCharacter.localPosition = Vector3.Lerp(
                    flyStartPosition,
                    originalFlyLocalPosition,
                    t);

                flyCharacter.localRotation = Quaternion.Slerp(
                    flyStartRotation,
                    originalFlyLocalRotation,
                    t);
            }

            yield return null;
        }

        if (speedCharacter != null)
        {
            speedCharacter.localPosition =
                originalSpeedLocalPosition;

            speedCharacter.localRotation =
                originalSpeedLocalRotation;
        }

        if (flyCharacter != null)
        {
            flyCharacter.localPosition =
                originalFlyLocalPosition;

            flyCharacter.localRotation =
                originalFlyLocalRotation;
        }
    }

    private void CacheTeammateTransforms()
    {
        if (cachedOriginalTransforms)
            return;

        if (speedCharacter != null)
        {
            originalSpeedParent = speedCharacter.parent;
            originalSpeedLocalPosition =
                speedCharacter.localPosition;
            originalSpeedLocalRotation =
                speedCharacter.localRotation;
        }

        if (flyCharacter != null)
        {
            originalFlyParent = flyCharacter.parent;
            originalFlyLocalPosition =
                flyCharacter.localPosition;
            originalFlyLocalRotation =
                flyCharacter.localRotation;
        }

        cachedOriginalTransforms = true;
    }

    private void StartPresentation()
    {
        if (animator != null)
        {
            if (!string.IsNullOrWhiteSpace(
                triangleDiveTrigger))
            {
                animator.SetTrigger(triangleDiveTrigger);
            }

            if (!string.IsNullOrWhiteSpace(
                triangleDiveBool))
            {
                animator.SetBool(triangleDiveBool, true);
            }
        }

        if (diveEffect != null)
            diveEffect.Play();

        if (audioSource != null && startSound != null)
            audioSource.PlayOneShot(startSound);
    }

    private void StopPresentation()
    {
        if (animator != null &&
            !string.IsNullOrWhiteSpace(triangleDiveBool))
        {
            animator.SetBool(triangleDiveBool, false);
        }

        if (diveEffect != null)
        {
            diveEffect.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void UpdateUpdraftPresentation()
    {
        if (!UpdraftActive)
        {
            playedUpdraftSound = false;
            return;
        }

        if (playedUpdraftSound)
            return;

        if (audioSource != null && updraftSound != null)
            audioSource.PlayOneShot(updraftSound);

        playedUpdraftSound = true;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!diving || other == null)
            return;

        int layerMask = 1 << other.gameObject.layer;

        if ((updraftLayers.value & layerMask) == 0)
            return;

        insideUpdraft = true;
        lastUpdraftTime = Time.time;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null)
            return;

        int layerMask = 1 << other.gameObject.layer;

        if ((updraftLayers.value & layerMask) == 0)
            return;

        insideUpdraft = false;
        lastUpdraftTime = Time.time;
    }

    private void OnDisable()
    {
        if (diving)
            StopTriangleDive();
    }

    private void OnDestroy()
    {
        UltimatePlayerMovement.Controllable = true;
    }

    private static float SmoothStep(float value)
    {
        return value * value * (3f - 2f * value);
    }
}