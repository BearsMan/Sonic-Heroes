using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class Spring : MonoBehaviour
{
    #region Constants

    private const string DefaultAudioObjectName =
        "Audio Object";

    #endregion

    #region Inspector

    [Header("Launch")]
    [SerializeField, Min(0f)] private float launchSpeed = 28f;
    [SerializeField]
    private Vector3 localLaunchDirection =
        Vector3.up;
    [SerializeField, Min(0f)] private float positionOffset = 0.1f;
    [SerializeField] private bool clearExistingVelocity = true;

    [Header("Activation")]
    [SerializeField] private bool requirePlayerTag = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField, Min(0f)] private float reactivationDelay = 0.2f;

    [Header("Audio")]
    [SerializeField] private AudioClip launchClip;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private GameObject audioObjectPrefab;
    [SerializeField] private bool resolveAudioObjectAutomatically = true;

    [Header("Effects")]
    [SerializeField] private ParticleSystem launchEffect;
    [SerializeField] private Animator springAnimator;
    [SerializeField] private string activationTrigger = "Activate";

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private readonly Dictionary<Rigidbody, float> activationTimes =
        new();

    private readonly List<Rigidbody> expiredBodies =
        new();

    private Collider springCollider;

    private int activationTriggerHash;

    private bool initialized;
    private bool shuttingDown;

    #endregion

    #region Public API

    public bool IsInitialized =>
        initialized;

    public Vector3 LaunchDirection =>
        GetLaunchDirection();

    public bool Launch(
        GameObject character)
    {
        if (!initialized ||
            shuttingDown ||
            character == null)
        {
            return false;
        }

        if (!ResolveCharacterComponents(
                character,
                out UltimatePlayerMovement movement,
                out Rigidbody characterRigidbody))
        {
            return false;
        }

        if (!CanActivate(
                characterRigidbody))
        {
            return false;
        }

        RecordActivation(
            characterRigidbody);

        ApplyLaunch(
            movement,
            characterRigidbody);

        PlayPresentation();

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
        if (shuttingDown)
            return;

        if (!initialized)
        {
            Initialize();
        }
    }

    private void OnDisable()
    {
        activationTimes.Clear();
        expiredBodies.Clear();
    }

    private void OnDestroy()
    {
        shuttingDown = true;
        initialized = false;

        activationTimes.Clear();
        expiredBodies.Clear();

        springCollider = null;
        audioSource = null;
        audioObjectPrefab = null;
        launchEffect = null;
        springAnimator = null;
    }

    private void OnValidate()
    {
        launchSpeed =
            Mathf.Max(
                0f,
                launchSpeed);

        positionOffset =
            Mathf.Max(
                0f,
                positionOffset);

        reactivationDelay =
            Mathf.Max(
                0f,
                reactivationDelay);

        if (localLaunchDirection.sqrMagnitude <=
            0.0001f)
        {
            localLaunchDirection =
                Vector3.up;
        }

        CacheActivationHash();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ResolveReferences();
            ConfigureCollider();
        }
#endif
    }

    private void OnTriggerEnter(
        Collider other)
    {
        if (!initialized ||
            shuttingDown ||
            other == null)
        {
            return;
        }

        GameObject character =
            ResolveCharacterObject(
                other);

        if (character == null)
            return;

        Launch(
            character);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 direction =
            GetLaunchDirection();

        float rayLength =
            Mathf.Max(
                1f,
                launchSpeed * 0.25f);

        Gizmos.DrawRay(
            transform.position,
            direction * rayLength);

        Gizmos.DrawWireSphere(
            transform.position +
            direction * positionOffset,
            0.15f);
    }

    #endregion

    #region Initialization

    private bool Initialize()
    {
        ResolveReferences();
        ConfigureCollider();
        CacheActivationHash();

        if (springCollider == null)
        {
            Debug.LogError(
                $"{nameof(Spring)} requires a Collider on '{name}'.",
                this);

            initialized = false;
            enabled = false;

            return false;
        }

        initialized = true;

        return true;
    }

    private void ResolveReferences()
    {
        springCollider ??=
            GetComponent<Collider>();

        audioSource ??=
            GetComponent<AudioSource>();

        springAnimator ??=
            GetComponent<Animator>();

        springAnimator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        if (resolveAudioObjectAutomatically &&
            audioObjectPrefab == null)
        {
            audioObjectPrefab =
                Resources.Load<GameObject>(
                    DefaultAudioObjectName);
        }
    }

    private void ConfigureCollider()
    {
        if (springCollider == null)
            return;

        springCollider.isTrigger = true;
    }

    private void CacheActivationHash()
    {
        activationTriggerHash =
            string.IsNullOrWhiteSpace(
                activationTrigger)
                ? 0
                : Animator.StringToHash(
                    activationTrigger);
    }

    #endregion

    #region Character Resolution

    private GameObject ResolveCharacterObject(
        Collider other)
    {
        if (other == null)
            return null;

        if (requirePlayerTag &&
            !HasPlayerTag(
                other))
        {
            return null;
        }

        if (other.attachedRigidbody != null)
        {
            return other
                .attachedRigidbody
                .gameObject;
        }

        UltimatePlayerMovement movement =
            other.GetComponent<UltimatePlayerMovement>();

        movement ??=
            other.GetComponentInParent<UltimatePlayerMovement>();

        movement ??=
            other.GetComponentInChildren<UltimatePlayerMovement>(
                includeInactive: true);

        return movement != null
            ? movement.gameObject
            : null;
    }

    private bool ResolveCharacterComponents(
        GameObject character,
        out UltimatePlayerMovement movement,
        out Rigidbody characterRigidbody)
    {
        movement =
            character.GetComponent<UltimatePlayerMovement>();

        movement ??=
            character.GetComponentInParent<UltimatePlayerMovement>();

        movement ??=
            character.GetComponentInChildren<UltimatePlayerMovement>(
                includeInactive: true);

        characterRigidbody =
            character.GetComponent<Rigidbody>();

        characterRigidbody ??=
            character.GetComponentInParent<Rigidbody>();

        characterRigidbody ??=
            character.GetComponentInChildren<Rigidbody>(
                includeInactive: true);

        if (movement == null)
        {
            Debug.LogWarning(
                $"{nameof(Spring)} could not find {nameof(UltimatePlayerMovement)} on '{character.name}'.",
                this);

            return false;
        }

        if (characterRigidbody == null)
        {
            Debug.LogWarning(
                $"{nameof(Spring)} could not find a Rigidbody on '{character.name}'.",
                this);

            return false;
        }

        if (!characterRigidbody.gameObject.activeInHierarchy)
            return false;

        return true;
    }

    private bool HasPlayerTag(
        Collider other)
    {
        if (string.IsNullOrWhiteSpace(
                playerTag))
        {
            return true;
        }

        Transform current =
            other.transform;

        while (current != null)
        {
            if (current.CompareTag(
                    playerTag))
            {
                return true;
            }

            current =
                current.parent;
        }

        return false;
    }

    #endregion

    #region Launch

    private void ApplyLaunch(
        UltimatePlayerMovement movement,
        Rigidbody characterRigidbody)
    {
        if (movement == null ||
            characterRigidbody == null)
        {
            return;
        }

        Vector3 direction =
            GetLaunchDirection();

        Vector3 launchVelocity =
            direction *
            launchSpeed;

        if (positionOffset > 0f)
        {
            characterRigidbody.position +=
                direction *
                positionOffset;
        }

        if (clearExistingVelocity)
        {
            characterRigidbody.linearVelocity =
                Vector3.zero;

            characterRigidbody.angularVelocity =
                Vector3.zero;
        }

        characterRigidbody.useGravity = true;
        characterRigidbody.isKinematic = false;
        characterRigidbody.WakeUp();

        movement.EnableMovement();

        movement.LaunchFromSpring(
            launchVelocity);

        LogStateChange(
            $"Launched '{movement.name}' at speed {launchSpeed}.");
    }

    private Vector3 GetLaunchDirection()
    {
        Vector3 direction =
            transform.TransformDirection(
                localLaunchDirection);

        if (direction.sqrMagnitude <=
            0.0001f)
        {
            direction =
                transform.up;
        }

        return direction.normalized;
    }

    #endregion

    #region Activation Protection

    private bool CanActivate(
        Rigidbody body)
    {
        if (body == null)
            return false;

        if (!activationTimes.TryGetValue(
                body,
                out float previousTime))
        {
            return true;
        }

        return
            Time.time - previousTime >=
            reactivationDelay;
    }

    private void RecordActivation(
        Rigidbody body)
    {
        if (body == null)
            return;

        activationTimes[body] =
            Time.time;

        RemoveExpiredActivations();
    }

    private void RemoveExpiredActivations()
    {
        if (activationTimes.Count == 0)
            return;

        float expirationTime =
            Time.time -
            Mathf.Max(
                1f,
                reactivationDelay * 4f);

        expiredBodies.Clear();

        foreach (
            KeyValuePair<Rigidbody, float> entry
            in activationTimes)
        {
            if (entry.Key == null ||
                entry.Value <= expirationTime)
            {
                expiredBodies.Add(
                    entry.Key);
            }
        }

        foreach (Rigidbody body in expiredBodies)
        {
            activationTimes.Remove(
                body);
        }

        expiredBodies.Clear();
    }

    #endregion

    #region Presentation

    private void PlayPresentation()
    {
        if (launchEffect != null)
        {
            launchEffect.Play();
        }

        if (springAnimator != null &&
            activationTriggerHash != 0)
        {
            springAnimator.SetTrigger(
                activationTriggerHash);
        }

        PlayLaunchAudio();
    }

    private void PlayLaunchAudio()
    {
        if (launchClip == null)
            return;

        if (audioSource != null)
        {
            audioSource.PlayOneShot(
                launchClip);

            return;
        }

        if (audioObjectPrefab == null)
        {
            Debug.LogWarning(
                $"{nameof(Spring)} could not play audio because no AudioSource or audio prefab was found.",
                this);

            return;
        }

        GameObject audioInstance =
            Instantiate(
                audioObjectPrefab,
                transform.position,
                Quaternion.identity);

        if (audioInstance == null)
            return;

        if (audioInstance.TryGetComponent(
                out AudioObject audioObject))
        {
            audioObject.Setup(
                launchClip,
                transform);

            return;
        }

        AudioSource instanceSource =
            audioInstance.GetComponent<AudioSource>();

        if (instanceSource != null)
        {
            instanceSource.PlayOneShot(
                launchClip);
        }
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