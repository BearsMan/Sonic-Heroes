using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls a Speed Formation character moving through a predefined
/// Light Speed Attack ring trail.
/// </summary>
public class LightSpeed : MonoBehaviour
{
    [Header("Ring Trail")]
    [SerializeField] private List<GameObject> rings = new();
    [SerializeField] private bool loopTrail;

    [Header("Input")]
    [SerializeField] private KeyCode attackKey = KeyCode.B;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float dashSpeed = 45f;
    [SerializeField, Min(0f)] private float arrivalDistance = 0.1f;
    [SerializeField, Min(0f)] private float rotationSpeed = 720f;
    [SerializeField, Min(0f)] private float exitSpeed = 15f;

    [Header("Animation")]
    [SerializeField] private string attackAnimation = "Light Speed Attack";
    [SerializeField] private string exitAnimation = "Jump Down";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip dashStartSound;
    [SerializeField] private AudioClip dashEndSound;

    [Header("Effects")]
    [SerializeField] private GameObject dashEffect;
    [SerializeField] private TrailRenderer dashTrail;

    [Header("Debug")]
    [SerializeField] private bool drawRingTrail = true;
    [SerializeField] private bool logStateChanges;

    private UltimatePlayerMovement movement;
    private TeamActionController actionController;
    private Rigidbody playerRigidbody;
    private Animator playerAnimator;
    private Collider eligibleCharacter;
    private Coroutine dashCoroutine;
    private bool previousGravityState;
    private bool previousKinematicState;
    private bool movementDisabledDirectly;
    private bool isDashing;
    private int currentRingIndex;

    public bool IsDashing => isDashing;
    public int CurrentRingIndex => currentRingIndex;
    public int RingCount => rings.Count;

    private void Awake()
    {
        SetPresentation(false);
    }

    private void Update()
    {
        if (isDashing || eligibleCharacter == null)
            return;
        if (Input.GetKeyDown(attackKey))
            TryStartDash(eligibleCharacter);
    }

    public bool TryStartDash(Collider characterCollider)
    {
        if (isDashing || characterCollider == null)
            return false;
        if (!ResolveCharacterReferences(characterCollider))
            return false;
        if (!HasValidRing())
            return false;
        if (!BeginTeamAction())
            return false;
        BeginDash();
        return true;
    }

    public bool TryStartDash(GameObject speedCharacter)
    {
        if (speedCharacter == null)
            return false;
        Collider characterCollider = speedCharacter.GetComponent<Collider>();
        if (characterCollider == null)
            characterCollider = speedCharacter.GetComponentInChildren<Collider>();
        return TryStartDash(characterCollider);
    }

    private bool ResolveCharacterReferences(Collider characterCollider)
    {
        movement = characterCollider.GetComponentInParent<UltimatePlayerMovement>();
        if (movement == null)
        {
            Debug.LogWarning("LightSpeed could not find UltimatePlayerMovement on the entering character.", this);
            ClearCharacterReferences();
            return false;
        }
        actionController = movement.GetComponent<TeamActionController>();
        if (actionController == null)
            actionController = movement.GetComponentInParent<TeamActionController>();
        playerRigidbody = characterCollider.attachedRigidbody;
        if (playerRigidbody == null)
            playerRigidbody = movement.GetComponent<Rigidbody>();
        if (playerRigidbody == null)
            playerRigidbody = movement.GetComponentInChildren<Rigidbody>();
        if (playerRigidbody == null)
        {
            Debug.LogWarning("LightSpeed could not find a Rigidbody on the entering character.", this);
            ClearCharacterReferences();
            return false;
        }
        playerAnimator = movement.GetComponentInChildren<Animator>();
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
        else
        {
            movement.DisableMovement();
            movementDisabledDirectly = true;
        }
        return true;
    }

    private void BeginDash()
    {
        isDashing = true;
        currentRingIndex = FindFirstValidRingIndex();
        previousGravityState = playerRigidbody.useGravity;
        previousKinematicState = playerRigidbody.isKinematic;
        playerRigidbody.linearVelocity = Vector3.zero;
        playerRigidbody.angularVelocity = Vector3.zero;
        playerRigidbody.useGravity = false;
        playerRigidbody.isKinematic = true;
        PlayAnimation(attackAnimation);
        PlaySound(dashStartSound);
        SetPresentation(true);
        dashCoroutine = StartCoroutine(DashRoutine());
        if (logStateChanges)
            Debug.Log("Light Speed Attack started.", this);
    }

    private IEnumerator DashRoutine()
    {
        WaitForFixedUpdate fixedUpdate = new();
        while (isDashing)
        {
            if (playerRigidbody == null)
            {
                FinishDash(false);
                yield break;
            }
            if (currentRingIndex < 0 || currentRingIndex >= rings.Count)
            {
                if (loopTrail)
                {
                    currentRingIndex = FindFirstValidRingIndex();
                    if (currentRingIndex < 0)
                    {
                        FinishDash(false);
                        yield break;
                    }
                }
                else
                {
                    FinishDash(true);
                    yield break;
                }
            }
            GameObject targetRing = rings[currentRingIndex];
            if (targetRing == null || !targetRing.activeInHierarchy)
            {
                AdvanceRing();
                yield return null;
                continue;
            }
            Vector3 targetPosition = targetRing.transform.position;
            Vector3 difference = targetPosition - playerRigidbody.position;
            float distance = difference.magnitude;
            if (distance <= arrivalDistance)
            {
                playerRigidbody.position = targetPosition;
                AdvanceRing();
                yield return null;
                continue;
            }
            Vector3 direction = difference / distance;
            RotatePlayer(direction);
            float movementDistance = dashSpeed * Time.fixedDeltaTime;
            Vector3 nextPosition = Vector3.MoveTowards(playerRigidbody.position, targetPosition, movementDistance);
            playerRigidbody.MovePosition(nextPosition);
            yield return fixedUpdate;
        }
    }

    private void RotatePlayer(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return;
        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        Quaternion nextRotation = Quaternion.RotateTowards(
            playerRigidbody.rotation,
            targetRotation,
            rotationSpeed * Time.fixedDeltaTime);
        playerRigidbody.MoveRotation(nextRotation);
    }

    private void AdvanceRing()
    {
        currentRingIndex++;
        if (currentRingIndex < rings.Count)
            return;
        if (loopTrail)
            currentRingIndex = FindFirstValidRingIndex();
    }

    private int FindFirstValidRingIndex()
    {
        for (int i = 0; i < rings.Count; i++)
        {
            if (rings[i] != null && rings[i].activeInHierarchy)
                return i;
        }
        return -1;
    }

    private bool HasValidRing()
    {
        if (rings == null || rings.Count == 0)
        {
            Debug.LogWarning("LightSpeed has no rings assigned.", this);
            return false;
        }
        if (FindFirstValidRingIndex() >= 0)
            return true;
        Debug.LogWarning("LightSpeed has no valid active rings assigned.", this);
        return false;
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
                playerRigidbody.linearVelocity = playerRigidbody.transform.forward * exitSpeed;
        }
        PlayAnimation(exitAnimation);
        PlaySound(dashEndSound);
        SetPresentation(false);
        RestoreMovement();
        if (logStateChanges)
            Debug.Log("Light Speed Attack finished.", this);
        ClearCharacterReferences();
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

    private void ClearCharacterReferences()
    {
        movement = null;
        actionController = null;
        playerRigidbody = null;
        playerAnimator = null;
        currentRingIndex = 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDashing || other == null)
            return;
        UltimatePlayerMovement enteringMovement = other.GetComponentInParent<UltimatePlayerMovement>();
        if (enteringMovement != null)
            eligibleCharacter = other;
    }

    private void OnTriggerStay(Collider other)
    {
        if (isDashing || eligibleCharacter != null || other == null)
            return;
        UltimatePlayerMovement enteringMovement = other.GetComponentInParent<UltimatePlayerMovement>();
        if (enteringMovement != null)
            eligibleCharacter = other;
    }

    private void OnTriggerExit(Collider other)
    {
        if (isDashing || other != eligibleCharacter)
            return;
        eligibleCharacter = null;
    }

    private void OnDisable()
    {
        eligibleCharacter = null;
        if (isDashing)
            FinishDash(false);
        else
            SetPresentation(false);
    }

    private void OnValidate()
    {
        dashSpeed = Mathf.Max(0.1f, dashSpeed);
        arrivalDistance = Mathf.Max(0f, arrivalDistance);
        rotationSpeed = Mathf.Max(0f, rotationSpeed);
        exitSpeed = Mathf.Max(0f, exitSpeed);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawRingTrail || rings == null || rings.Count == 0)
            return;
        for (int i = 0; i < rings.Count; i++)
        {
            GameObject currentRing = rings[i];
            if (currentRing == null)
                continue;
            Gizmos.DrawWireSphere(currentRing.transform.position, 0.2f);
            int nextIndex = i + 1;
            if (nextIndex < rings.Count && rings[nextIndex] != null)
                Gizmos.DrawLine(currentRing.transform.position, rings[nextIndex].transform.position);
        }
        if (loopTrail && rings.Count > 1 && rings[0] != null && rings[^1] != null)
            Gizmos.DrawLine(rings[^1].transform.position, rings[0].transform.position);
    }
}