using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class Cannon : MonoBehaviour
{
    #region Types

    public enum CannonType
    {
        Speed,
        Vertical,
        Sideways,
        Custom
    }

    public enum CannonState
    {
        Uninitialized,
        Ready,
        Charging,
        Launching,
        Cooldown,
        Destroyed,
        Disabled
    }

    #endregion

    #region Constants

    private const string DefaultChargeTrigger =
        "Charge";

    private const string DefaultLaunchTrigger =
        "Launch";

    private const string DefaultDestroyTrigger =
        "Destroy";

    #endregion

    #region Inspector

    [Header("Cannon")]
    [SerializeField]
    private CannonType cannonType =
        CannonType.Speed;

    [SerializeField, Min(0f)] private float launchSpeed = 50f;
    [SerializeField, Min(0f)] private float chargeDuration = 0.15f;
    [SerializeField, Min(0f)] private float controlLockDuration = 0.5f;
    [SerializeField, Min(0f)] private float reuseCooldown = 0.5f;
    [SerializeField] private bool allowMultipleUses = true;

    [Header("Launch Direction")]
    [SerializeField]
    private Vector3 localCustomDirection =
        Vector3.forward;

    [SerializeField, Range(-180f, 180f)]
    private float customPitch = 45f;

    [SerializeField] private bool powerCharactersUseCustomAim = true;
    [SerializeField] private bool clearVelocityBeforeLaunch = true;
    [SerializeField] private bool moveCharacterToLaunchPoint = true;
    [SerializeField] private Transform launchPoint;

    [Header("Activation")]
    [SerializeField] private bool requirePlayerTag = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool requireCharacterDefinition;
    [SerializeField] private Collider triggerCollider;

    [Header("Destruction")]
    [SerializeField] private bool canBeDestroyed = true;
    [SerializeField] private bool destroyGameObject;
    [SerializeField, Min(0f)] private float destroyDelay = 0.5f;
    [SerializeField, Min(0f)] private float destructionShakeDuration = 0.35f;
    [SerializeField, Min(0f)] private float destructionShakeAmount = 0.1f;

    [Header("Animation")]
    [SerializeField] private Animator cannonAnimator;
    [SerializeField]
    private string chargeTrigger =
        DefaultChargeTrigger;
    [SerializeField]
    private string launchTrigger =
        DefaultLaunchTrigger;
    [SerializeField]
    private string destroyTrigger =
        DefaultDestroyTrigger;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip chargeClip;
    [SerializeField] private AudioClip launchClip;
    [SerializeField] private AudioClip destructionClip;

    [Header("Effects")]
    [SerializeField] private ParticleSystem chargeEffect;
    [SerializeField] private ParticleSystem launchEffect;
    [SerializeField] private ParticleSystem destructionEffect;

    [Header("Visuals")]
    [SerializeField] private GameObject cannonVisual;
    [SerializeField] private Renderer[] renderers;

    [Header("Runtime Safety")]
    [SerializeField] private bool enableRuntimeSafety = true;
    [SerializeField, Min(0.1f)] private float safetyCheckInterval = 1f;
    [SerializeField, Min(1f)] private float maximumLaunchSpeed = 250f;
    [SerializeField, Min(0.01f)] private float minimumValidScale = 0.01f;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;
    [SerializeField]
    private CannonState currentState =
        CannonState.Uninitialized;

    #endregion

    #region Runtime State

    private UltimatePlayerMovement currentMovement;
    private Rigidbody currentRigidbody;
    private CharacterDefinition currentCharacterDefinition;

    private Coroutine launchRoutine;
    private Coroutine destructionRoutine;

    private Quaternion startingRotation;
    private Vector3 startingPosition;

    private float safetyTimer;
    private float cooldownTimer;

    private int chargeTriggerHash;
    private int launchTriggerHash;
    private int destroyTriggerHash;

    private bool initialized;
    private bool isDestroyed;
    private bool shuttingDown;
    private bool applicationQuitting;

    #endregion

    #region Events

    public event Action<Cannon, UltimatePlayerMovement> Charged;
    public event Action<Cannon, UltimatePlayerMovement> Launched;
    public event Action<Cannon> Destroyed;
    public event Action<Cannon> ResetCompleted;

    #endregion

    #region Public API

    public CannonType Type =>
        cannonType;

    public CannonState CurrentState =>
        currentState;

    public bool IsInitialized =>
        initialized;

    public bool IsDestroyed =>
        isDestroyed;

    public bool IsAvailable =>
        initialized &&
        !isDestroyed &&
        currentState == CannonState.Ready;

    public Vector3 LaunchDirection =>
        GetLaunchDirection(
            currentCharacterDefinition);

    public bool TryUse(
        GameObject character)
    {
        if (!CanBeginUse() ||
            character == null)
        {
            return false;
        }

        if (!ResolveCharacter(
                character))
        {
            return false;
        }

        if (CanCurrentCharacterDestroyCannon())
        {
            DestroyCannon();
            return true;
        }

        launchRoutine =
            StartCoroutine(
                LaunchRoutine());

        return true;
    }

    public bool SetCannonType(
        CannonType value)
    {
        if (!Enum.IsDefined(
                typeof(CannonType),
                value))
        {
            return false;
        }

        cannonType =
            value;

        return true;
    }

    public void SetLaunchSpeed(
        float value)
    {
        launchSpeed =
            Mathf.Clamp(
                value,
                0f,
                maximumLaunchSpeed);
    }

    public bool AimPowerCannon(
        float yaw,
        float pitch)
    {
        if (isDestroyed ||
            currentCharacterDefinition == null ||
            currentCharacterDefinition.characterType !=
                CharacterDefinition.CharacterType.Power)
        {
            return false;
        }

        Quaternion rotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f);

        if (!IsFiniteQuaternion(
                rotation))
        {
            return false;
        }

        transform.rotation =
            rotation;

        customPitch =
            Mathf.Clamp(
                pitch,
                -180f,
                180f);

        return true;
    }

    public bool ForceDestroy()
    {
        if (!initialized ||
            isDestroyed)
        {
            return false;
        }

        DestroyCannon();

        return true;
    }

    public bool ResetCannon()
    {
        if (shuttingDown ||
            applicationQuitting)
        {
            return false;
        }

        CancelLaunchRoutine();
        CancelDestructionRoutine();

        isDestroyed = false;
        cooldownTimer = 0f;

        transform.SetPositionAndRotation(
            startingPosition,
            startingRotation);

        RestoreRequiredComponents();
        SetVisualsEnabled(
            true);

        currentState =
            CannonState.Ready;

        ResetCharacterReferences();

        ResetCompleted?.Invoke(
            this);

        LogStateChange(
            $"Cannon '{name}' reset.");

        return true;
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (shuttingDown ||
            applicationQuitting)
        {
            return;
        }

        if (!initialized)
        {
            Initialize();
        }

        if (initialized &&
            !isDestroyed &&
            currentState == CannonState.Disabled)
        {
            currentState =
                CannonState.Ready;
        }
    }

    private void Update()
    {
        if (!initialized ||
            shuttingDown ||
            applicationQuitting)
        {
            return;
        }

        UpdateCooldown();

        if (!enableRuntimeSafety)
            return;

        safetyTimer -=
            Time.deltaTime;

        if (safetyTimer > 0f)
            return;

        safetyTimer =
            safetyCheckInterval;

        RunRuntimeSafetyChecks();
    }

    private void OnDisable()
    {
        CancelLaunchRoutine();

        if (!shuttingDown &&
            !isDestroyed)
        {
            currentState =
                CannonState.Disabled;
        }

        RestoreCurrentCharacter();
    }

    private void OnApplicationQuit()
    {
        applicationQuitting = true;
    }

    private void OnDestroy()
    {
        shuttingDown = true;
        initialized = false;

        CancelLaunchRoutine();
        CancelDestructionRoutine();
        RestoreCurrentCharacter();

        Charged = null;
        Launched = null;
        Destroyed = null;
        ResetCompleted = null;

        currentMovement = null;
        currentRigidbody = null;
        currentCharacterDefinition = null;

        triggerCollider = null;
        launchPoint = null;
        cannonAnimator = null;
        audioSource = null;
        chargeEffect = null;
        launchEffect = null;
        destructionEffect = null;
        cannonVisual = null;
        renderers = null;

        currentState =
            CannonState.Disabled;
    }

    private void OnValidate()
    {
        launchSpeed =
            Mathf.Clamp(
                launchSpeed,
                0f,
                maximumLaunchSpeed);

        chargeDuration =
            Mathf.Max(
                0f,
                chargeDuration);

        controlLockDuration =
            Mathf.Max(
                0f,
                controlLockDuration);

        reuseCooldown =
            Mathf.Max(
                0f,
                reuseCooldown);

        destroyDelay =
            Mathf.Max(
                0f,
                destroyDelay);

        destructionShakeDuration =
            Mathf.Max(
                0f,
                destructionShakeDuration);

        destructionShakeAmount =
            Mathf.Max(
                0f,
                destructionShakeAmount);

        safetyCheckInterval =
            Mathf.Max(
                0.1f,
                safetyCheckInterval);

        maximumLaunchSpeed =
            Mathf.Max(
                1f,
                maximumLaunchSpeed);

        minimumValidScale =
            Mathf.Max(
                0.01f,
                minimumValidScale);

        if (!IsFiniteVector(
                localCustomDirection) ||
            localCustomDirection.sqrMagnitude <=
                0.0001f)
        {
            localCustomDirection =
                Vector3.forward;
        }

        CacheAnimatorHashes();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ResolveReferences();
            ConfigureComponents();
        }
#endif
    }

    private void OnTriggerEnter(
        Collider other)
    {
        if (!initialized ||
            shuttingDown ||
            applicationQuitting ||
            other == null)
        {
            return;
        }

        GameObject character =
            ResolveCharacterObject(
                other);

        if (character == null)
            return;

        TryUse(
            character);
    }

    private void OnDrawGizmosSelected()
    {
        Transform origin =
            launchPoint != null
                ? launchPoint
                : transform;

        Vector3 direction =
            GetLaunchDirection(
                currentCharacterDefinition);

        Gizmos.DrawWireSphere(
            origin.position,
            0.2f);

        Gizmos.DrawRay(
            origin.position,
            direction *
            Mathf.Max(
                1f,
                launchSpeed * 0.15f));
    }

    #endregion

    #region Initialization

    private bool Initialize()
    {
        if (initialized)
            return true;

        ResolveReferences();
        ConfigureComponents();
        CacheAnimatorHashes();

        startingPosition =
            transform.position;

        startingRotation =
            transform.rotation;

        safetyTimer =
            safetyCheckInterval;

        cooldownTimer = 0f;

        if (!ValidateConfiguration())
        {
            initialized = false;
            currentState =
                CannonState.Uninitialized;

            enabled = false;

            return false;
        }

        initialized = true;
        isDestroyed = false;

        currentState =
            CannonState.Ready;

        return true;
    }

    private void ResolveReferences()
    {
        triggerCollider ??=
            GetComponent<Collider>();

        triggerCollider ??=
            GetComponentInChildren<Collider>(
                includeInactive: true);

        launchPoint ??=
            FindDescendantByName(
                "Launch Point");

        launchPoint ??=
            transform;

        cannonAnimator ??=
            GetComponent<Animator>();

        cannonAnimator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        audioSource ??=
            GetComponent<AudioSource>();

        audioSource ??=
            GetComponentInChildren<AudioSource>(
                includeInactive: true);

        cannonVisual ??=
            gameObject;

        if (renderers == null ||
            renderers.Length == 0)
        {
            renderers =
                GetComponentsInChildren<Renderer>(
                    includeInactive: true);
        }
    }

    private void ConfigureComponents()
    {
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;

            if (!isDestroyed)
            {
                triggerCollider.enabled = true;
            }
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
        }
    }

    private void CacheAnimatorHashes()
    {
        chargeTriggerHash =
            GetAnimatorHash(
                chargeTrigger);

        launchTriggerHash =
            GetAnimatorHash(
                launchTrigger);

        destroyTriggerHash =
            GetAnimatorHash(
                destroyTrigger);
    }

    #endregion

    #region Character Resolution

    private GameObject ResolveCharacterObject(
        Collider other)
    {
        if (other == null)
            return null;

        if (requirePlayerTag &&
            !HasTagInHierarchy(
                other.transform,
                playerTag))
        {
            return null;
        }

        UltimatePlayerMovement movement =
            other.GetComponent<UltimatePlayerMovement>();

        movement ??=
            other.GetComponentInParent<UltimatePlayerMovement>();

        movement ??=
            other.GetComponentInChildren<UltimatePlayerMovement>(
                includeInactive: true);

        if (movement != null)
        {
            return movement.gameObject;
        }

        return other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : null;
    }

    private bool ResolveCharacter(
        GameObject character)
    {
        ResetCharacterReferences();

        currentMovement =
            character.GetComponent<UltimatePlayerMovement>();

        currentMovement ??=
            character.GetComponentInParent<UltimatePlayerMovement>();

        currentMovement ??=
            character.GetComponentInChildren<UltimatePlayerMovement>(
                includeInactive: true);

        currentRigidbody =
            character.GetComponent<Rigidbody>();

        currentRigidbody ??=
            character.GetComponentInParent<Rigidbody>();

        currentRigidbody ??=
            character.GetComponentInChildren<Rigidbody>(
                includeInactive: true);

        currentCharacterDefinition =
            currentMovement != null
                ? currentMovement.CharacterDefinition
                : null;

        if (currentMovement == null ||
            currentRigidbody == null)
        {
            ResetCharacterReferences();
            return false;
        }

        if (requireCharacterDefinition &&
            currentCharacterDefinition == null)
        {
            ResetCharacterReferences();
            return false;
        }

        return
            currentMovement.isActiveAndEnabled &&
            currentRigidbody.gameObject.activeInHierarchy;
    }

    private void ResetCharacterReferences()
    {
        currentMovement = null;
        currentRigidbody = null;
        currentCharacterDefinition = null;
    }

    #endregion

    #region Launch System

    private bool CanBeginUse()
    {
        return
            initialized &&
            !isDestroyed &&
            !shuttingDown &&
            !applicationQuitting &&
            cooldownTimer <= 0f &&
            currentState == CannonState.Ready &&
            isActiveAndEnabled;
    }

    private IEnumerator LaunchRoutine()
    {
        currentState =
            CannonState.Charging;

        if (currentMovement != null)
        {
            currentMovement.DisableMovement();
        }

        StopCharacterMotion();
        MoveCharacterToLaunchPoint();
        PlayChargePresentation();

        Charged?.Invoke(
            this,
            currentMovement);

        if (chargeDuration > 0f)
        {
            yield return new WaitForSeconds(
                chargeDuration);
        }

        if (!ValidateCurrentCharacter())
        {
            RestoreCurrentCharacter();
            currentState =
                CannonState.Ready;

            launchRoutine = null;
            yield break;
        }

        currentState =
            CannonState.Launching;

        PlayLaunchPresentation();
        ApplyLaunch();

        Launched?.Invoke(
            this,
            currentMovement);

        if (controlLockDuration > 0f)
        {
            yield return new WaitForSeconds(
                controlLockDuration);
        }

        RestoreCurrentCharacter();

        cooldownTimer =
            reuseCooldown;

        currentState =
            allowMultipleUses
                ? CannonState.Cooldown
                : CannonState.Disabled;

        launchRoutine = null;
    }

    private void ApplyLaunch()
    {
        if (!ValidateCurrentCharacter())
            return;

        Vector3 direction =
            GetLaunchDirection(
                currentCharacterDefinition);

        if (!IsFiniteVector(
                direction) ||
            direction.sqrMagnitude <=
                0.0001f)
        {
            direction =
                transform.forward;
        }

        Vector3 launchVelocity =
            direction.normalized *
            Mathf.Clamp(
                launchSpeed,
                0f,
                maximumLaunchSpeed);

        if (clearVelocityBeforeLaunch)
        {
            StopCharacterMotion();
        }

        currentRigidbody.isKinematic = false;
        currentRigidbody.useGravity = true;

        currentRigidbody.linearVelocity =
            launchVelocity;

        currentRigidbody.WakeUp();

        LogStateChange(
            $"Launched '{currentMovement.name}' at {launchVelocity.magnitude:0.00}.");
    }

    private void MoveCharacterToLaunchPoint()
    {
        if (!moveCharacterToLaunchPoint ||
            currentRigidbody == null ||
            launchPoint == null)
        {
            return;
        }

        if (!IsFiniteVector(
                launchPoint.position) ||
            !IsFiniteQuaternion(
                launchPoint.rotation))
        {
            return;
        }

        currentRigidbody.position =
            launchPoint.position;

        currentRigidbody.rotation =
            launchPoint.rotation;

        Physics.SyncTransforms();
    }

    private Vector3 GetLaunchDirection(
        CharacterDefinition definition)
    {
        bool usePowerAim =
            powerCharactersUseCustomAim &&
            definition != null &&
            definition.characterType ==
                CharacterDefinition.CharacterType.Power;

        if (usePowerAim)
        {
            return GetCustomDirection();
        }

        return cannonType switch
        {
            CannonType.Speed =>
                transform.forward,

            CannonType.Vertical =>
                transform.up,

            CannonType.Sideways =>
                transform.right,

            CannonType.Custom =>
                GetCustomDirection(),

            _ =>
                transform.forward
        };
    }

    private Vector3 GetCustomDirection()
    {
        Vector3 baseDirection =
            transform.TransformDirection(
                localCustomDirection);

        if (baseDirection.sqrMagnitude <=
            0.0001f)
        {
            baseDirection =
                transform.forward;
        }

        Quaternion pitchRotation =
            Quaternion.AngleAxis(
                customPitch,
                transform.right);

        Vector3 direction =
            pitchRotation *
            baseDirection.normalized;

        return direction.normalized;
    }

    private void StopCharacterMotion()
    {
        if (currentRigidbody == null)
            return;

        currentRigidbody.linearVelocity =
            Vector3.zero;

        currentRigidbody.angularVelocity =
            Vector3.zero;
    }

    private void RestoreCurrentCharacter()
    {
        if (currentMovement != null)
        {
            currentMovement.EnableMovement();
        }

        ResetCharacterReferences();
    }

    private void UpdateCooldown()
    {
        if (cooldownTimer <= 0f)
            return;

        cooldownTimer =
            Mathf.Max(
                0f,
                cooldownTimer -
                Time.deltaTime);

        if (cooldownTimer <= 0f &&
            allowMultipleUses &&
            !isDestroyed)
        {
            currentState =
                CannonState.Ready;
        }
    }

    #endregion

    #region Destruction

    private bool CanCurrentCharacterDestroyCannon()
    {
        if (!canBeDestroyed ||
            currentCharacterDefinition == null)
        {
            return false;
        }

        return
            currentCharacterDefinition.characterType ==
                CharacterDefinition.CharacterType.Power;
    }

    private void DestroyCannon()
    {
        if (isDestroyed)
            return;

        CancelLaunchRoutine();
        RestoreCurrentCharacter();

        isDestroyed = true;

        currentState =
            CannonState.Destroyed;

        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        PlayDestructionPresentation();

        Destroyed?.Invoke(
            this);

        destructionRoutine =
            StartCoroutine(
                DestructionRoutine());
    }

    private IEnumerator DestructionRoutine()
    {
        Vector3 originalPosition =
            transform.position;

        float elapsed = 0f;

        while (elapsed <
               destructionShakeDuration)
        {
            if (shuttingDown ||
                applicationQuitting)
            {
                yield break;
            }

            transform.position =
                originalPosition +
                UnityEngine.Random.insideUnitSphere *
                destructionShakeAmount;

            elapsed +=
                Time.deltaTime;

            yield return null;
        }

        transform.position =
            originalPosition;

        SetVisualsEnabled(
            false);

        if (destroyGameObject)
        {
            if (destroyDelay > 0f)
            {
                yield return new WaitForSeconds(
                    destroyDelay);
            }

            Destroy(
                gameObject);
        }

        destructionRoutine = null;
    }

    #endregion

    #region Runtime Safety

    private bool RunRuntimeSafetyChecks()
    {
        if (!ValidateCoreReferences())
        {
            ResolveReferences();
            ConfigureComponents();

            if (!ValidateCoreReferences())
            {
                EnterSafetyShutdown(
                    "Required references could not be restored.");

                return false;
            }
        }

        if (!ValidateTransform(
                transform))
        {
            EnterSafetyShutdown(
                "The cannon Transform contains invalid values.");

            return false;
        }

        if (currentRigidbody != null &&
            !ValidateRigidbody(
                currentRigidbody))
        {
            RestoreCurrentCharacter();
            CancelLaunchRoutine();

            currentState =
                isDestroyed
                    ? CannonState.Destroyed
                    : CannonState.Ready;

            return false;
        }

        if (currentMovement == null &&
            currentState != CannonState.Ready &&
            currentState != CannonState.Cooldown &&
            currentState != CannonState.Destroyed &&
            currentState != CannonState.Disabled)
        {
            CancelLaunchRoutine();

            currentState =
                CannonState.Ready;
        }

        return true;
    }

    private bool ValidateCurrentCharacter()
    {
        return
            currentMovement != null &&
            currentRigidbody != null &&
            currentMovement.isActiveAndEnabled &&
            currentRigidbody.gameObject.activeInHierarchy &&
            ValidateRigidbody(
                currentRigidbody);
    }

    private bool ValidateRigidbody(
        Rigidbody body)
    {
        return
            body != null &&
            IsFiniteVector(
                body.position) &&
            IsFiniteQuaternion(
                body.rotation) &&
            IsFiniteVector(
                body.linearVelocity) &&
            IsFiniteVector(
                body.angularVelocity);
    }

    private void EnterSafetyShutdown(
        string reason)
    {
        CancelLaunchRoutine();
        RestoreCurrentCharacter();

        initialized = false;

        currentState =
            CannonState.Disabled;

        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        Debug.LogError(
            $"{nameof(Cannon)} entered safety shutdown on '{name}': {reason}",
            this);

        enabled = false;
    }

    #endregion

    #region Validation

    private bool ValidateConfiguration()
    {
        bool valid = true;

        valid &=
            ValidateReference(
                triggerCollider,
                nameof(Collider));

        valid &=
            ValidateReference(
                launchPoint,
                "Launch Point");

        if (audioSource == null &&
            (chargeClip != null ||
             launchClip != null ||
             destructionClip != null))
        {
            Debug.LogWarning(
                $"{nameof(Cannon)} on '{name}' has audio clips but no AudioSource.",
                this);
        }

        return valid;
    }

    private bool ValidateCoreReferences()
    {
        return
            triggerCollider != null &&
            launchPoint != null;
    }

    private bool ValidateReference(
        UnityEngine.Object reference,
        string displayName)
    {
        if (reference != null)
            return true;

        Debug.LogError(
            $"{nameof(Cannon)} requires {displayName} on '{name}'.",
            this);

        return false;
    }

    #endregion

    #region Presentation

    private void PlayChargePresentation()
    {
        SetAnimatorTrigger(
            chargeTriggerHash);

        if (chargeEffect != null)
        {
            chargeEffect.Play();
        }

        PlaySound(
            chargeClip);
    }

    private void PlayLaunchPresentation()
    {
        SetAnimatorTrigger(
            launchTriggerHash);

        if (chargeEffect != null)
        {
            chargeEffect.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting);
        }

        if (launchEffect != null)
        {
            launchEffect.Play();
        }

        PlaySound(
            launchClip);
    }

    private void PlayDestructionPresentation()
    {
        SetAnimatorTrigger(
            destroyTriggerHash);

        if (chargeEffect != null)
        {
            chargeEffect.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (launchEffect != null)
        {
            launchEffect.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (destructionEffect != null)
        {
            destructionEffect.Play();
        }

        PlaySound(
            destructionClip);
    }

    private void SetAnimatorTrigger(
        int triggerHash)
    {
        if (cannonAnimator == null ||
            triggerHash == 0)
        {
            return;
        }

        cannonAnimator.SetTrigger(
            triggerHash);
    }

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

    private void SetVisualsEnabled(
        bool value)
    {
        if (cannonVisual != null &&
            cannonVisual != gameObject)
        {
            cannonVisual.SetActive(
                value);
        }

        if (renderers == null)
            return;

        foreach (Renderer cannonRenderer
                 in renderers)
        {
            if (cannonRenderer != null)
            {
                cannonRenderer.enabled =
                    value;
            }
        }
    }

    #endregion

    #region Cleanup

    private void CancelLaunchRoutine()
    {
        if (launchRoutine == null)
            return;

        StopCoroutine(
            launchRoutine);

        launchRoutine = null;
    }

    private void CancelDestructionRoutine()
    {
        if (destructionRoutine == null)
            return;

        StopCoroutine(
            destructionRoutine);

        destructionRoutine = null;
    }

    private void RestoreRequiredComponents()
    {
        if (triggerCollider != null)
        {
            triggerCollider.enabled = true;
            triggerCollider.isTrigger = true;
        }

        if (audioSource != null)
        {
            audioSource.enabled = true;
        }

        if (cannonAnimator != null)
        {
            cannonAnimator.enabled = true;
        }
    }

    #endregion

    #region Helpers

    private Transform FindDescendantByName(
        string targetName)
    {
        Transform[] descendants =
            GetComponentsInChildren<Transform>(
                includeInactive: true);

        foreach (Transform descendant
                 in descendants)
        {
            if (descendant != null &&
                string.Equals(
                    descendant.name,
                    targetName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return descendant;
            }
        }

        return null;
    }

    private bool ValidateTransform(
        Transform target)
    {
        if (target == null)
            return false;

        Vector3 scale =
            target.lossyScale;

        return
            IsFiniteVector(
                target.position) &&
            IsFiniteQuaternion(
                target.rotation) &&
            IsFiniteVector(
                scale) &&
            Mathf.Abs(scale.x) >=
                minimumValidScale &&
            Mathf.Abs(scale.y) >=
                minimumValidScale &&
            Mathf.Abs(scale.z) >=
                minimumValidScale;
    }

    private static bool HasTagInHierarchy(
        Transform source,
        string requiredTag)
    {
        if (source == null)
            return false;

        if (string.IsNullOrWhiteSpace(
                requiredTag))
        {
            return true;
        }

        Transform current =
            source;

        while (current != null)
        {
            if (current.CompareTag(
                    requiredTag))
            {
                return true;
            }

            current =
                current.parent;
        }

        return false;
    }

    private static int GetAnimatorHash(
        string parameterName)
    {
        return
            string.IsNullOrWhiteSpace(
                parameterName)
                ? 0
                : Animator.StringToHash(
                    parameterName);
    }

    private static bool IsFiniteVector(
        Vector3 value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }

    private static bool IsFiniteQuaternion(
        Quaternion value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z) &&
            float.IsFinite(value.w);
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