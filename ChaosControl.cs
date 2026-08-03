using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChaosControl : MonoBehaviour
{
    #region Constants

    private const float MinimumDirectionMagnitude = 0.001f;

    #endregion

    #region Animator Hashes

    private static readonly int ChaosControlHash =
        Animator.StringToHash("Chaos Control");

    #endregion

    #region Inspector

    [Header("References")]
    [SerializeField] private TeamActionController actionController;
    [SerializeField] private CharacterSwitch characterSwitch;
    [SerializeField] private UltimatePlayerMovement movement;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Transform teleportOrigin;

    [Header("Input")]
    [SerializeField] private KeyCode chaosControlKey = KeyCode.C;
    [SerializeField] private bool readPlayerInput = true;

    [Header("Teleport")]
    [SerializeField, Min(0.1f)] private float teleportDistance = 12f;
    [SerializeField, Min(0f)] private float obstacleClearance = 0.75f;
    [SerializeField, Min(0.05f)] private float teleportDelay = 0.12f;
    [SerializeField, Min(0.05f)] private float recoveryDuration = 0.2f;
    [SerializeField, Min(0f)] private float cooldown = 1.5f;
    [SerializeField] private LayerMask obstacleLayers = ~0;

    [Header("Ground Placement")]
    [SerializeField] private bool snapToGround = true;
    [SerializeField, Min(0.1f)] private float groundCheckHeight = 4f;
    [SerializeField, Min(0.1f)] private float groundCheckDistance = 8f;
    [SerializeField, Min(0f)] private float groundOffset = 0.1f;
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Effects")]
    [SerializeField] private GameObject departureEffect;
    [SerializeField] private GameObject arrivalEffect;
    [SerializeField, Min(0f)] private float effectLifetime = 2f;

    [Header("Audio")]
    [SerializeField] private AudioClip chaosControlSound;
    [SerializeField] private AudioClip arrivalSound;

    [Header("Debug")]
    [SerializeField] private bool drawTeleportPreview = true;
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private Coroutine chaosControlRoutine;
    private Vector3 teleportDestination;

    private bool isPerformingChaosControl;
    private bool isInitialized;
    private bool isShuttingDown;
    private bool previousGravityState;

    private float nextAvailableTime;

    #endregion

    #region Public API

    public bool IsPerformingChaosControl =>
        isPerformingChaosControl;

    public bool IsInitialized =>
        isInitialized;

    public bool IsOnCooldown =>
        Time.time < nextAvailableTime;

    public Vector3 TeleportDestination =>
        teleportDestination;

    public bool TryStartChaosControl()
    {
        if (!CanStartChaosControl())
            return false;

        bool accepted =
            actionController.TryBeginAction(
                TeamActionController.TeamAction.ChaosControl,
                TeamActionController.TeamFormation.Speed,
                mustBeGrounded: false,
                mustBeAirborne: false,
                surrenderMovementControl: true);

        if (!accepted)
            return false;

        BeginChaosControl();
        return true;
    }

    public void CancelChaosControl()
    {
        if (!isPerformingChaosControl)
            return;

        FinishChaosControl();
    }

    public void SetInputEnabled(
        bool enabled)
    {
        readPlayerInput = enabled;
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheComponents();
        ResolveReferences();
    }

    private void Start()
    {
        if (!InitializeChaosControl())
        {
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (isShuttingDown)
            return;

        CacheComponents();
        ResolveReferences();
    }

    private void Update()
    {
        if (!isInitialized ||
            !readPlayerInput ||
            isPerformingChaosControl)
        {
            return;
        }

        if (Input.GetKeyDown(
                chaosControlKey))
        {
            TryStartChaosControl();
        }
    }

    private void OnDisable()
    {
        CleanupRuntimeState();
    }

    private void OnDestroy()
    {
        isShuttingDown = true;
        CleanupDestroyedState();
    }

    private void OnValidate()
    {
        teleportDistance = Mathf.Max(0.1f, teleportDistance);
        obstacleClearance = Mathf.Max(0f, obstacleClearance);
        teleportDelay = Mathf.Max(0.05f, teleportDelay);
        recoveryDuration = Mathf.Max(0.05f, recoveryDuration);
        cooldown = Mathf.Max(0f, cooldown);
        groundCheckHeight = Mathf.Max(0.1f, groundCheckHeight);
        groundCheckDistance = Mathf.Max(0.1f, groundCheckDistance);
        groundOffset = Mathf.Max(0f, groundOffset);
        effectLifetime = Mathf.Max(0f, effectLifetime);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawTeleportPreview)
            return;

        Vector3 origin =
            teleportOrigin != null
                ? teleportOrigin.position
                : transform.position;

        Vector3 destination =
            Application.isPlaying
                ? teleportDestination
                : CalculateTeleportDestination(origin);

        Gizmos.DrawLine(origin, destination);
        Gizmos.DrawWireSphere(destination, obstacleClearance);
    }

    #endregion

    #region Initialization

    public bool InitializeChaosControl()
    {
        if (isInitialized)
            return true;

        CacheComponents();
        ResolveReferences();

        if (!ValidateConfiguration())
        {
            Debug.LogError(
                $"ChaosControl failed to initialize on '{name}'.",
                this);

            isInitialized = false;
            return false;
        }

        ResetRuntimeState();

        isInitialized = true;
        return true;
    }

    private void CacheComponents()
    {
        actionController ??=
            GetComponentInParent<TeamActionController>();

        characterSwitch ??=
            GetComponentInParent<CharacterSwitch>();

        movement ??=
            GetComponentInParent<UltimatePlayerMovement>();

        playerRigidbody ??=
            GetComponentInParent<Rigidbody>();

        audioSource ??=
            GetComponentInParent<AudioSource>();
    }

    private void ResolveReferences()
    {
        animator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        if (animator == null && movement != null)
        {
            animator =
                movement.GetComponentInChildren<Animator>(
                    includeInactive: true);
        }

        teleportOrigin ??=
            transform;
    }

    #endregion

    #region Chaos Control State

    private bool CanUseChaosControl()
    {
        return
            TeamSetup.Instance != null &&
            TeamSetup.Instance.PlayableTeam == PlayableTeam.TeamDark &&
            characterSwitch != null &&
            characterSwitch.CurrentLeaderType == CHARACTERTYPES.Speed &&
            actionController != null &&
            actionController.CurrentFormation ==
                TeamActionController.TeamFormation.Speed;
    }

    private bool CanStartChaosControl()
    {
        return
            CanUseChaosControl() &&
            isInitialized &&
            !isPerformingChaosControl &&
            !IsOnCooldown &&
            playerRigidbody != null;
    }

    private void BeginChaosControl()
    {
        isPerformingChaosControl = true;
        previousGravityState = playerRigidbody.useGravity;

        playerRigidbody.linearVelocity = Vector3.zero;
        playerRigidbody.angularVelocity = Vector3.zero;
        playerRigidbody.useGravity = false;

        teleportDestination =
            FindSafeTeleportDestination();

        PlayAnimation();
        PlaySound(chaosControlSound);

        SpawnEffect(
            departureEffect,
            playerRigidbody.position,
            transform.rotation);

        chaosControlRoutine =
            StartCoroutine(
                ChaosControlRoutine());

        LogStateChange(
            $"Chaos Control started. Destination: {teleportDestination}.");
    }

    private IEnumerator ChaosControlRoutine()
    {
        if (teleportDelay > 0f)
        {
            yield return
                new WaitForSeconds(
                    teleportDelay);
        }

        TeleportPlayer();

        SpawnEffect(
            arrivalEffect,
            teleportDestination,
            transform.rotation);

        PlaySound(arrivalSound);

        if (recoveryDuration > 0f)
        {
            yield return
                new WaitForSeconds(
                    recoveryDuration);
        }

        FinishChaosControl();
    }

    private void FinishChaosControl()
    {
        if (!isPerformingChaosControl)
            return;

        isPerformingChaosControl = false;

        if (chaosControlRoutine != null)
        {
            StopCoroutine(chaosControlRoutine);
            chaosControlRoutine = null;
        }

        if (playerRigidbody != null)
        {
            playerRigidbody.useGravity = previousGravityState;
        }

        nextAvailableTime =
            Time.time + cooldown;

        if (actionController != null &&
            actionController.CurrentAction ==
                TeamActionController.TeamAction.ChaosControl)
        {
            actionController.EndAction(
                restoreMovementControl: true);
        }

        LogStateChange(
            "Chaos Control finished.");
    }

    private void ResetRuntimeState()
    {
        isPerformingChaosControl = false;
        chaosControlRoutine = null;
        teleportDestination = transform.position;
        previousGravityState = true;
        nextAvailableTime = 0f;
    }

    #endregion

    #region Teleport

    private Vector3 FindSafeTeleportDestination()
    {
        Vector3 origin =
            teleportOrigin != null
                ? teleportOrigin.position
                : transform.position;

        Vector3 destination =
            CalculateTeleportDestination(origin);

        if (snapToGround)
        {
            destination =
                SnapDestinationToGround(destination);
        }

        return destination;
    }

    private Vector3 CalculateTeleportDestination(
        Vector3 origin)
    {
        Vector3 direction =
            transform.forward;

        if (direction.sqrMagnitude <=
            MinimumDirectionMagnitude)
        {
            direction = Vector3.forward;
        }

        direction.Normalize();

        if (Physics.SphereCast(
                origin,
                obstacleClearance,
                direction,
                out RaycastHit hit,
                teleportDistance,
                obstacleLayers,
                QueryTriggerInteraction.Ignore))
        {
            float safeDistance =
                Mathf.Max(
                    0f,
                    hit.distance - obstacleClearance);

            return
                origin +
                direction * safeDistance;
        }

        return
            origin +
            direction * teleportDistance;
    }

    private Vector3 SnapDestinationToGround(
        Vector3 destination)
    {
        Vector3 rayOrigin =
            destination +
            Vector3.up * groundCheckHeight;

        if (!Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                groundCheckDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore))
        {
            return destination;
        }

        return
            hit.point +
            Vector3.up * groundOffset;
    }

    private void TeleportPlayer()
    {
        if (playerRigidbody == null)
            return;

        playerRigidbody.position =
            teleportDestination;

        playerRigidbody.linearVelocity =
            Vector3.zero;

        playerRigidbody.angularVelocity =
            Vector3.zero;

        Physics.SyncTransforms();
    }

    #endregion

    #region Effects

    private void SpawnEffect(
        GameObject effectPrefab,
        Vector3 position,
        Quaternion rotation)
    {
        if (effectPrefab == null)
            return;

        GameObject spawnedEffect =
            Instantiate(
                effectPrefab,
                position,
                rotation);

        if (effectLifetime > 0f)
        {
            Destroy(
                spawnedEffect,
                effectLifetime);
        }
    }

    #endregion

    #region Animation

    private void PlayAnimation()
    {
        if (animator == null)
            return;

        animator.SetTrigger(
            ChaosControlHash);
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

        audioSource.PlayOneShot(clip);
    }

    #endregion

    #region Validation

    private bool ValidateConfiguration()
    {
        bool valid = true;

        valid &=
            ValidateReference(
                actionController,
                nameof(TeamActionController));

        valid &=
            ValidateReference(
                characterSwitch,
                nameof(CharacterSwitch));

        valid &=
            ValidateReference(
                playerRigidbody,
                nameof(Rigidbody));

        if (movement == null)
        {
            Debug.LogWarning(
                "ChaosControl could not find UltimatePlayerMovement.",
                this);
        }

        if (animator == null)
        {
            Debug.LogWarning(
                "ChaosControl could not find an Animator.",
                this);
        }

        if (audioSource == null)
        {
            Debug.LogWarning(
                "ChaosControl could not find an AudioSource.",
                this);
        }

        if (departureEffect == null)
        {
            Debug.LogWarning(
                "ChaosControl has no departure effect.",
                this);
        }

        if (arrivalEffect == null)
        {
            Debug.LogWarning(
                "ChaosControl has no arrival effect.",
                this);
        }

        return valid;
    }

    private bool ValidateReference(
        Object reference,
        string displayName)
    {
        if (reference != null)
            return true;

        Debug.LogError(
            $"ChaosControl requires {displayName}.",
            this);

        return false;
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        if (isPerformingChaosControl)
        {
            FinishChaosControl();
        }
    }

    private void CleanupDestroyedState()
    {
        CleanupRuntimeState();

        isInitialized = false;

        actionController = null;
        characterSwitch = null;
        movement = null;
        playerRigidbody = null;
        animator = null;
        audioSource = null;
        teleportOrigin = null;
        departureEffect = null;
        arrivalEffect = null;
    }

    #endregion

    #region Debug

    private void LogStateChange(
        string message)
    {
        if (!logStateChanges)
            return;

        Debug.Log(
            message,
            this);
    }

    #endregion
}
