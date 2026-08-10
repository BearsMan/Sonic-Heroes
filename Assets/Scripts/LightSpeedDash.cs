using System.Collections.Generic;
using UnityEngine;

public class LightSpeedDash : MonoBehaviour
{
    #region Detection

    [Header("Detection")]

    [SerializeField, Min(0.1f)]
    private float lockOnRadius = 4f;

    [SerializeField, Min(0.1f)]
    private float chainRadius = 5f;

    [SerializeField, Min(0.01f)]
    private float arrivalDistance = 0.15f;

    [SerializeField]
    private LayerMask ringLayers = ~0;

    [SerializeField]
    private string ringTag = "Ring";

    #endregion

    #region Movement

    [Header("Movement")]

    [SerializeField, Min(0.1f)]
    private float dashSpeed = 40f;

    [SerializeField, Min(0f)]
    private float minimumExitSpeed = 20f;

    [SerializeField, Min(0f)]
    private float exitSpeedMultiplier = 0.5f;

    [SerializeField, Min(0f)]
    private float rotationSpeed = 720f;

    [SerializeField, Min(1)]
    private int maximumChainLength = 128;

    #endregion

    #region Charge

    [Header("Charge")]

    [SerializeField]
    private bool holdToCharge = true;

    [SerializeField, Min(0f)]
    private float chargeTime = 0.3f;

    #endregion

    #region References

    [Header("References")]

    [SerializeField]
    private Rigidbody playerRigidbody;

    [SerializeField]
    private AudioSource audioSource;

    #endregion

    #region Effects

    [Header("Effects")]

    [SerializeField]
    private GameObject chargeEffect;

    [SerializeField]
    private GameObject dashEffect;

    [SerializeField]
    private GameObject speedLineEffect;

    [SerializeField]
    private TrailRenderer dashTrail;

    #endregion

    #region Audio

    [Header("Audio")]

    [SerializeField]
    private AudioClip chargeSound;

    [SerializeField]
    private AudioClip dashSound;

    [SerializeField]
    private AudioClip ringSound;

    [SerializeField]
    private AudioClip finishSound;

    #endregion

    #region Debug

    [Header("Debug")]

    [SerializeField]
    private bool drawDetectionRadius = true;

    [SerializeField]
    private bool drawChain = true;

    #endregion

    #region Runtime State

    private readonly List<Transform> ringChain =
        new();

    private readonly HashSet<Transform> visitedRings =
        new();

    private bool isCharging;
    private bool isDashing;

    private float chargeTimer;

    private int currentRingIndex;

    private bool originalKinematic;
    private bool originalGravity;

    #endregion

    #region Properties

    public bool IsCharging =>
        isCharging;

    public bool IsDashing =>
        isDashing;

    public bool CanDash =>
        !isDashing &&
        FindNearestRing(
            transform.position,
            lockOnRadius) != null;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();

        SetChargeEffects(
            false);

        SetDashEffects(
            false);
    }

    private void FixedUpdate()
    {
        if (!isDashing)
        {
            return;
        }

        UpdateDash();
    }

    private void OnDisable()
    {
        CancelCharge();
        CancelDash();

        SetChargeEffects(
            false);

        SetDashEffects(
            false);
    }

    private void OnValidate()
    {
        lockOnRadius =
            Mathf.Max(
                0.1f,
                lockOnRadius);

        chainRadius =
            Mathf.Max(
                0.1f,
                chainRadius);

        arrivalDistance =
            Mathf.Max(
                0.01f,
                arrivalDistance);

        dashSpeed =
            Mathf.Max(
                0.1f,
                dashSpeed);

        minimumExitSpeed =
            Mathf.Max(
                0f,
                minimumExitSpeed);

        exitSpeedMultiplier =
            Mathf.Max(
                0f,
                exitSpeedMultiplier);

        rotationSpeed =
            Mathf.Max(
                0f,
                rotationSpeed);

        maximumChainLength =
            Mathf.Max(
                1,
                maximumChainLength);

        chargeTime =
            Mathf.Max(
                0f,
                chargeTime);
    }

    #endregion

    #region Public API

    public void OnDashButtonPressed()
    {
        if (isDashing)
        {
            return;
        }

        if (!holdToCharge)
        {
            TryActivate();

            return;
        }

        BeginCharge();
    }

    public void OnDashButtonHeld()
    {
        if (!holdToCharge ||
            !isCharging ||
            isDashing)
        {
            return;
        }

        chargeTimer +=
            Time.deltaTime;
    }

    public void OnDashButtonReleased()
    {
        if (!holdToCharge ||
            !isCharging)
        {
            return;
        }

        bool charged =
            chargeTimer >=
            chargeTime;

        CancelCharge();

        if (charged)
        {
            TryActivate();
        }
    }

    public bool TryActivate()
    {
        if (!CanStartDash())
        {
            return false;
        }

        CancelCharge();

        if (!BuildRingChain())
        {
            return false;
        }

        StartDash();

        return true;
    }

    public void CancelDash()
    {
        if (!isDashing)
        {
            return;
        }

        FinishDash(
            false);
    }

    #endregion

    #region Charge

    private void BeginCharge()
    {
        if (isCharging ||
            isDashing)
        {
            return;
        }

        if (FindNearestRing(
                transform.position,
                lockOnRadius) == null)
        {
            return;
        }

        isCharging =
            true;

        chargeTimer =
            0f;

        SetChargeEffects(
            true);

        PlaySound(
            chargeSound);
    }

    private void CancelCharge()
    {
        isCharging =
            false;

        chargeTimer =
            0f;

        SetChargeEffects(
            false);
    }

    #endregion

    #region Activation

    private bool CanStartDash()
    {
        if (isDashing)
        {
            return false;
        }

        ResolveReferences();

        if (playerRigidbody == null ||
            !playerRigidbody.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!IsFiniteVector(
                playerRigidbody.position) ||
            !IsFiniteVector(
                playerRigidbody.linearVelocity) ||
            !IsFiniteVector(
                playerRigidbody.angularVelocity))
        {
            return false;
        }

        return
            FindNearestRing(
                playerRigidbody.position,
                lockOnRadius) != null;
    }

    #endregion

    #region Ring Chain

    private bool BuildRingChain()
    {
        ringChain.Clear();
        visitedRings.Clear();

        Transform firstRing =
            FindNearestRing(
                playerRigidbody.position,
                lockOnRadius);

        if (firstRing == null)
        {
            return false;
        }

        Transform currentRing =
            firstRing;

        while (currentRing != null &&
            ringChain.Count <
                maximumChainLength)
        {
            ringChain.Add(
                currentRing);

            visitedRings.Add(
                currentRing);

            currentRing =
                FindNearestRing(
                    currentRing.position,
                    chainRadius,
                    visitedRings);
        }

        currentRingIndex =
            0;

        return
            ringChain.Count > 0;
    }

    private Transform FindNearestRing(
        Vector3 origin,
        float radius,
        HashSet<Transform> ignored = null)
    {
        if (!IsFiniteVector(
                origin) ||
            radius <= 0f)
        {
            return null;
        }

        Collider[] candidates =
            Physics.OverlapSphere(
                origin,
                radius,
                ringLayers,
                QueryTriggerInteraction.Collide);

        Transform nearest =
            null;

        float nearestDistance =
            radius * radius;

        foreach (Collider candidate
            in candidates)
        {
            if (candidate == null)
            {
                continue;
            }

            Transform ring =
                FindRingTransform(
                    candidate.transform);

            if (!IsValidRing(
                    ring) ||
                ignored != null &&
                ignored.Contains(
                    ring))
            {
                continue;
            }

            float distance =
                (ring.position - origin)
                .sqrMagnitude;

            if (!float.IsFinite(
                    distance) ||
                distance >=
                    nearestDistance)
            {
                continue;
            }

            nearestDistance =
                distance;

            nearest =
                ring;
        }

        return nearest;
    }

    private Transform FindRingTransform(
        Transform candidate)
    {
        if (candidate == null)
        {
            return null;
        }

        Transform current =
            candidate;

        while (current != null)
        {
            if (HasRingTag(
                    current))
            {
                return current;
            }

            current =
                current.parent;
        }

        return null;
    }

    private bool HasRingTag(
        Transform candidate)
    {
        if (candidate == null ||
            string.IsNullOrWhiteSpace(
                ringTag))
        {
            return false;
        }

        return
            candidate.CompareTag(
                ringTag);
    }

    private bool IsValidRing(
        Transform ring)
    {
        return
            ring != null &&
            ring.gameObject.activeInHierarchy &&
            HasRingTag(
                ring) &&
            IsFiniteVector(
                ring.position);
    }

    #endregion

    #region Dash

    private void StartDash()
    {
        if (playerRigidbody == null ||
            ringChain.Count == 0)
        {
            return;
        }

        isDashing =
            true;

        currentRingIndex =
            0;

        originalKinematic =
            playerRigidbody.isKinematic;

        originalGravity =
            playerRigidbody.useGravity;

        playerRigidbody.linearVelocity =
            Vector3.zero;

        playerRigidbody.angularVelocity =
            Vector3.zero;

        playerRigidbody.useGravity =
            false;

        playerRigidbody.isKinematic =
            true;

        SetDashEffects(
            true);

        PlaySound(
            dashSound);
    }

    private void UpdateDash()
    {
        if (playerRigidbody == null)
        {
            FinishDash(
                false);

            return;
        }

        Transform targetRing =
            GetCurrentRing();

        if (targetRing == null)
        {
            FinishDash(
                true);

            return;
        }

        Vector3 currentPosition =
            playerRigidbody.position;

        Vector3 targetPosition =
            targetRing.position;

        if (!IsFiniteVector(
                currentPosition) ||
            !IsFiniteVector(
                targetPosition))
        {
            AdvanceRing();

            return;
        }

        Vector3 difference =
            targetPosition -
            currentPosition;

        float distance =
            difference.magnitude;

        if (!float.IsFinite(
                distance))
        {
            FinishDash(
                false);

            return;
        }

        if (distance <=
            arrivalDistance)
        {
            playerRigidbody.position =
                targetPosition;

            CollectRing(
                targetRing);

            AdvanceRing();

            return;
        }

        Vector3 direction =
            difference /
            distance;

        RotateTowards(
            direction);

        float step =
            dashSpeed *
            Time.fixedDeltaTime;

        Vector3 nextPosition =
            Vector3.MoveTowards(
                currentPosition,
                targetPosition,
                step);

        if (!IsFiniteVector(
                nextPosition))
        {
            FinishDash(
                false);

            return;
        }

        playerRigidbody.MovePosition(
            nextPosition);
    }

    private Transform GetCurrentRing()
    {
        while (currentRingIndex <
            ringChain.Count)
        {
            Transform ring =
                ringChain[
                    currentRingIndex];

            if (IsValidRing(
                    ring))
            {
                return ring;
            }

            currentRingIndex++;
        }

        return null;
    }

    private void AdvanceRing()
    {
        currentRingIndex++;

        if (currentRingIndex >=
            ringChain.Count)
        {
            FinishDash(
                true);
        }
    }

    #endregion

    #region Ring Collection

    private void CollectRing(
        Transform ring)
    {
        if (ring == null)
        {
            return;
        }

        PlaySound(
            ringSound);

        ring.gameObject.SetActive(
            false);
    }

    #endregion

    #region Rotation

    private void RotateTowards(
        Vector3 direction)
    {
        if (playerRigidbody == null ||
            !IsFiniteVector(
                direction) ||
            direction.sqrMagnitude <=
                0.0001f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);

        Quaternion nextRotation =
            Quaternion.RotateTowards(
                playerRigidbody.rotation,
                targetRotation,
                rotationSpeed *
                    Time.fixedDeltaTime);

        playerRigidbody.MoveRotation(
            nextRotation);
    }

    #endregion

    #region Finish

    private void FinishDash(
        bool preserveMomentum)
    {
        if (!isDashing)
        {
            return;
        }

        isDashing =
            false;

        SetDashEffects(
            false);

        if (playerRigidbody != null)
        {
            playerRigidbody.isKinematic =
                originalKinematic;

            playerRigidbody.useGravity =
                originalGravity;

            playerRigidbody.angularVelocity =
                Vector3.zero;

            if (!playerRigidbody.isKinematic)
            {
                if (preserveMomentum)
                {
                    float exitSpeed =
                        Mathf.Max(
                            dashSpeed *
                                exitSpeedMultiplier,
                            minimumExitSpeed);

                    Vector3 velocity =
                        transform.forward *
                        exitSpeed;

                    if (IsFiniteVector(
                            velocity))
                    {
                        playerRigidbody.linearVelocity =
                            velocity;
                    }
                }
                else
                {
                    playerRigidbody.linearVelocity =
                        Vector3.zero;
                }
            }
        }

        PlaySound(
            finishSound);

        ringChain.Clear();
        visitedRings.Clear();

        currentRingIndex =
            0;
    }

    #endregion

    #region References

    private void ResolveReferences()
    {
        playerRigidbody ??=
            GetComponent<Rigidbody>();

        audioSource ??=
            GetComponent<AudioSource>();

        audioSource ??=
            GetComponentInParent<AudioSource>();
    }

    #endregion

    #region Effects

    private void SetChargeEffects(
        bool active)
    {
        if (chargeEffect != null)
        {
            chargeEffect.SetActive(
                active);
        }
    }

    private void SetDashEffects(
        bool active)
    {
        if (dashEffect != null)
        {
            dashEffect.SetActive(
                active);
        }

        if (speedLineEffect != null)
        {
            speedLineEffect.SetActive(
                active);
        }

        if (dashTrail != null)
        {
            dashTrail.emitting =
                active;
        }
    }

    #endregion

    #region Audio

    private void PlaySound(
        AudioClip clip)
    {
        if (audioSource == null ||
            clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(
            clip);
    }

    #endregion

    #region Validation

    private static bool IsFiniteVector(
        Vector3 value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (drawDetectionRadius)
        {
            Gizmos.DrawWireSphere(
                transform.position,
                lockOnRadius);
        }

        if (!drawChain ||
            ringChain == null ||
            ringChain.Count == 0)
        {
            return;
        }

        for (int index = 0;
            index < ringChain.Count;
            index++)
        {
            Transform ring =
                ringChain[index];

            if (ring == null)
            {
                continue;
            }

            Gizmos.DrawWireSphere(
                ring.position,
                arrivalDistance);

            if (index >=
                ringChain.Count - 1)
            {
                continue;
            }

            Transform nextRing =
                ringChain[
                    index + 1];

            if (nextRing != null)
            {
                Gizmos.DrawLine(
                    ring.position,
                    nextRing.position);
            }
        }
    }

    #endregion
}