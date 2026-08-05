using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(AudioSource))]
public sealed class Case : MonoBehaviour
{
    #region Types

    public enum SpawnMode
    {
        RandomItem,
        AllItems
    }

    public enum CaseState
    {
        Uninitialized,
        Closed,
        Opening,
        Opened,
        Disabled
    }

    #endregion

    #region Constants

    private const string DefaultSpawnPointName =
        "Spawn Point";

    private const string DefaultOpenTriggerName =
        "Open";

    private const int MaximumTrackedItems =
        64;

    #endregion

    #region Inspector

    [Header("Contents")]
    [SerializeField] private GameObject[] spawnableItems;
    [SerializeField]
    private SpawnMode spawnMode =
        SpawnMode.RandomItem;
    [SerializeField] private Transform spawnPoint;

    [Header("Item Placement")]
    [SerializeField, Min(0f)] private float itemSpacing = 0.35f;
    [SerializeField, Min(0f)] private float spawnClearanceRadius = 0.2f;
    [SerializeField, Min(1)] private int maximumSpawnAttempts = 4;
    [SerializeField] private LayerMask spawnBlockingLayers = ~0;

    [Header("Item Launch")]
    [SerializeField] private bool launchSpawnedItems = true;
    [SerializeField, Min(0f)] private float itemLaunchSpeed = 5f;
    [SerializeField]
    private Vector3 localLaunchDirection =
        Vector3.up;
    [SerializeField] private bool clearSpawnedVelocity = true;

    [Header("Activation")]
    [SerializeField] private bool requirePlayerTag = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool requirePlayerMovement = true;
    [SerializeField] private bool openOnlyOnce = true;

    [Header("Switch Activation")]
    [SerializeField] private bool activateSwitchWhenOpened;
    [SerializeField] private Switch targetSwitch;
    [SerializeField] private bool resolveSwitchAutomatically = true;
    [SerializeField] private bool includeInactiveSwitches;
    [SerializeField, Min(0f)] private float switchSearchRadius = 20f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip switchActivatedSound;

    [Header("Animation")]
    [SerializeField] private Animator caseAnimator;
    [SerializeField]
    private string openTrigger =
        DefaultOpenTriggerName;

    [Header("Effects")]
    [SerializeField] private ParticleSystem openEffect;

    [Header("Lifetime")]
    [SerializeField] private bool destroyAfterOpening = true;
    [SerializeField, Min(0f)] private float destroyDelay = 1.5f;
    [SerializeField] private bool disableVisualsBeforeDestroy;

    [Header("Runtime Safety")]
    [SerializeField] private bool enableRuntimeSafety = true;
    [SerializeField] private bool restoreDisabledComponents = true;
    [SerializeField] private bool validateBeforeOpening = true;
    [SerializeField] private bool preventDuplicateSpawns = true;
    [SerializeField, Min(0.1f)] private float referenceCheckInterval = 1f;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;
    [SerializeField]
    private CaseState currentState =
        CaseState.Uninitialized;

    #endregion

    #region Runtime State

    private readonly List<GameObject> validSpawnableItems =
        new();

    private readonly List<GameObject> spawnedItems =
        new();

    private Collider caseCollider;
    private Renderer[] caseRenderers;

    private float referenceCheckTimer;

    private int openTriggerHash;

    private bool initialized;
    private bool hasOpened;
    private bool isOpening;
    private bool shuttingDown;
    private bool applicationQuitting;

    #endregion

    #region Events

    public event Action<Case> Opened;
    public event Action<Case, GameObject> ItemSpawned;
    public event Action<Case, Switch> SwitchActivated;

    #endregion

    #region Public API

    public bool IsInitialized =>
        initialized;

    public bool HasOpened =>
        hasOpened;

    public bool IsOpening =>
        isOpening;

    public CaseState CurrentState =>
        currentState;

    public Transform SpawnPoint =>
        spawnPoint;

    public Switch TargetSwitch =>
        targetSwitch;

    public IReadOnlyList<GameObject> SpawnedItems =>
        spawnedItems;

    public bool OpenCase()
    {
        if (!CanOpen())
            return false;

        if (validateBeforeOpening &&
            !ValidateRuntimeConfiguration())
        {
            Debug.LogError(
                $"{nameof(Case)} on '{name}' failed runtime validation and could not open.",
                this);

            return false;
        }

        isOpening = true;
        currentState = CaseState.Opening;

        DisableCaseCollider();
        PlayOpenPresentation();

        int spawnedCount =
            SpawnContents();

        if (activateSwitchWhenOpened)
        {
            ActivateConfiguredSwitch();
        }

        hasOpened = true;
        isOpening = false;
        currentState = CaseState.Opened;

        Opened?.Invoke(
            this);

        LogStateChange(
            $"Opened '{name}' and spawned {spawnedCount} item(s).");

        if (destroyAfterOpening)
        {
            ScheduleDestruction();
        }

        return true;
    }

    public bool SetSpawnPoint(
        Transform newSpawnPoint)
    {
        if (!IsValidTransform(
                newSpawnPoint))
        {
            return false;
        }

        spawnPoint =
            newSpawnPoint;

        return true;
    }

    public bool SetTargetSwitch(
        Switch newTargetSwitch)
    {
        if (!IsValidSwitch(
                newTargetSwitch,
                allowInactive: true))
        {
            return false;
        }

        targetSwitch =
            newTargetSwitch;

        return true;
    }

    public void ClearTargetSwitch()
    {
        targetSwitch = null;
    }

    public bool ResolveNearestSwitch()
    {
        targetSwitch =
            FindNearestSwitch();

        return targetSwitch != null;
    }

    public bool ResetCase()
    {
        if (shuttingDown ||
            applicationQuitting)
        {
            return false;
        }

        CancelInvoke(
            nameof(DestroyCase));

        hasOpened = false;
        isOpening = false;
        currentState = CaseState.Closed;

        RestoreCaseCollider();
        SetRenderersEnabled(
            true);

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
            !hasOpened)
        {
            currentState =
                CaseState.Closed;
        }
    }

    private void Update()
    {
        if (!initialized ||
            !enableRuntimeSafety ||
            shuttingDown ||
            applicationQuitting)
        {
            return;
        }

        referenceCheckTimer -=
            Time.deltaTime;

        if (referenceCheckTimer > 0f)
            return;

        referenceCheckTimer =
            referenceCheckInterval;

        RunRuntimeSafetyChecks();
    }

    private void OnDisable()
    {
        if (!shuttingDown)
        {
            currentState =
                CaseState.Disabled;
        }
    }

    private void OnTriggerEnter(
        Collider other)
    {
        if (!initialized ||
            hasOpened ||
            isOpening ||
            shuttingDown ||
            other == null)
        {
            return;
        }

        if (!IsValidActivator(
                other))
        {
            return;
        }

        OpenCase();
    }

    private void OnApplicationQuit()
    {
        applicationQuitting = true;
    }

    private void OnDestroy()
    {
        shuttingDown = true;
        initialized = false;
        isOpening = false;

        CancelInvoke();

        Opened = null;
        ItemSpawned = null;
        SwitchActivated = null;

        validSpawnableItems.Clear();
        spawnedItems.Clear();

        caseCollider = null;
        caseRenderers = null;
        spawnPoint = null;
        targetSwitch = null;
        audioSource = null;
        caseAnimator = null;
        openEffect = null;

        currentState =
            CaseState.Disabled;
    }

    private void OnValidate()
    {
        itemSpacing =
            Mathf.Max(
                0f,
                itemSpacing);

        spawnClearanceRadius =
            Mathf.Max(
                0f,
                spawnClearanceRadius);

        maximumSpawnAttempts =
            Mathf.Max(
                1,
                maximumSpawnAttempts);

        itemLaunchSpeed =
            Mathf.Max(
                0f,
                itemLaunchSpeed);

        switchSearchRadius =
            Mathf.Max(
                0f,
                switchSearchRadius);

        destroyDelay =
            Mathf.Max(
                0f,
                destroyDelay);

        referenceCheckInterval =
            Mathf.Max(
                0.1f,
                referenceCheckInterval);

        if (!IsFiniteVector(
                localLaunchDirection) ||
            localLaunchDirection.sqrMagnitude <=
                0.0001f)
        {
            localLaunchDirection =
                Vector3.up;
        }

        CacheOpenTriggerHash();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ResolveReferences();
            ConfigureComponents();
            CacheSpawnableItems();
        }
#endif
    }

    private void OnDrawGizmosSelected()
    {
        Transform resolvedPoint =
            spawnPoint != null
                ? spawnPoint
                : transform;

        Vector3 launchDirection =
            GetLaunchDirection();

        Gizmos.DrawWireSphere(
            resolvedPoint.position,
            spawnClearanceRadius);

        Gizmos.DrawRay(
            resolvedPoint.position,
            launchDirection *
            Mathf.Max(
                1f,
                itemLaunchSpeed));

        if (activateSwitchWhenOpened &&
            switchSearchRadius > 0f)
        {
            Gizmos.DrawWireSphere(
                transform.position,
                switchSearchRadius);
        }
    }

    #endregion

    #region Initialization

    private bool Initialize()
    {
        if (initialized)
            return true;

        ResolveReferences();
        ConfigureComponents();
        CacheOpenTriggerHash();
        CacheSpawnableItems();

        referenceCheckTimer =
            referenceCheckInterval;

        if (!ValidateConfiguration())
        {
            initialized = false;
            currentState =
                CaseState.Uninitialized;

            enabled = false;

            return false;
        }

        initialized = true;
        currentState =
            hasOpened
                ? CaseState.Opened
                : CaseState.Closed;

        return true;
    }

    private void ResolveReferences()
    {
        ResolveCollider();
        ResolveAudioSource();
        ResolveAnimator();
        ResolveSpawnPoint();
        ResolveRenderers();

        if (activateSwitchWhenOpened &&
            resolveSwitchAutomatically &&
            !IsValidSwitch(
                targetSwitch,
                allowInactive: true))
        {
            targetSwitch =
                FindNearestSwitch();
        }
    }

    private void ResolveCollider()
    {
        caseCollider ??=
            GetComponent<Collider>();

        caseCollider ??=
            GetComponentInChildren<Collider>(
                includeInactive: true);
    }

    private void ResolveAudioSource()
    {
        audioSource ??=
            GetComponent<AudioSource>();

        audioSource ??=
            GetComponentInChildren<AudioSource>(
                includeInactive: true);
    }

    private void ResolveAnimator()
    {
        caseAnimator ??=
            GetComponent<Animator>();

        caseAnimator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);
    }

    private void ResolveSpawnPoint()
    {
        if (IsValidTransform(
                spawnPoint))
        {
            return;
        }

        spawnPoint =
            FindDescendantByName(
                DefaultSpawnPointName);

        spawnPoint ??=
            transform;
    }

    private void ResolveRenderers()
    {
        if (caseRenderers != null &&
            caseRenderers.Length > 0)
        {
            return;
        }

        caseRenderers =
            GetComponentsInChildren<Renderer>(
                includeInactive: true);
    }

    private void ConfigureComponents()
    {
        if (caseCollider != null)
        {
            caseCollider.isTrigger = true;

            if (!hasOpened)
            {
                caseCollider.enabled = true;
            }
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
        }
    }

    private void CacheOpenTriggerHash()
    {
        openTriggerHash =
            string.IsNullOrWhiteSpace(
                openTrigger)
                ? 0
                : Animator.StringToHash(
                    openTrigger);
    }

    private void CacheSpawnableItems()
    {
        validSpawnableItems.Clear();

        if (spawnableItems == null)
            return;

        foreach (GameObject itemPrefab
                 in spawnableItems)
        {
            if (itemPrefab == null ||
                validSpawnableItems.Contains(
                    itemPrefab))
            {
                continue;
            }

            validSpawnableItems.Add(
                itemPrefab);
        }
    }

    #endregion

    #region Runtime Safety

    private bool RunRuntimeSafetyChecks()
    {
        if (shuttingDown ||
            applicationQuitting)
        {
            return false;
        }

        bool referencesValid =
            ValidateCoreReferences();

        if (!referencesValid)
        {
            ResolveReferences();
            ConfigureComponents();

            referencesValid =
                ValidateCoreReferences();
        }

        if (!referencesValid)
        {
            EnterSafetyShutdown(
                "Required references could not be restored.");

            return false;
        }

        if (!IsFiniteVector(
                transform.position) ||
            !IsFiniteQuaternion(
                transform.rotation) ||
            !IsValidScale(
                transform.lossyScale))
        {
            EnterSafetyShutdown(
                "The case Transform contains invalid values.");

            return false;
        }

        if (restoreDisabledComponents)
        {
            RestoreRequiredComponents();
        }

        RemoveDestroyedSpawnedItems();

        if (targetSwitch != null &&
            !IsValidSwitch(
                targetSwitch,
                allowInactive: true))
        {
            targetSwitch = null;
        }

        if (activateSwitchWhenOpened &&
            resolveSwitchAutomatically &&
            targetSwitch == null &&
            !hasOpened)
        {
            targetSwitch =
                FindNearestSwitch();
        }

        return true;
    }

    private bool ValidateCoreReferences()
    {
        return
            caseCollider != null &&
            audioSource != null &&
            IsValidTransform(
                spawnPoint);
    }

    private bool ValidateRuntimeConfiguration()
    {
        RunRuntimeSafetyChecks();
        CacheSpawnableItems();

        if (!ValidateCoreReferences())
            return false;

        if (openOnlyOnce &&
            hasOpened)
        {
            return false;
        }

        return
            IsFiniteVector(
                spawnPoint.position) &&
            IsFiniteQuaternion(
                spawnPoint.rotation);
    }

    private void RestoreRequiredComponents()
    {
        if (caseCollider != null &&
            !caseCollider.enabled &&
            !hasOpened &&
            !isOpening)
        {
            caseCollider.enabled = true;
        }

        if (audioSource != null &&
            !audioSource.enabled)
        {
            audioSource.enabled = true;
        }

        if (caseAnimator != null &&
            !caseAnimator.enabled)
        {
            caseAnimator.enabled = true;
        }
    }

    private void RemoveDestroyedSpawnedItems()
    {
        for (int index =
                 spawnedItems.Count - 1;
             index >= 0;
             index--)
        {
            if (spawnedItems[index] == null)
            {
                spawnedItems.RemoveAt(
                    index);
            }
        }
    }

    private void EnterSafetyShutdown(
        string reason)
    {
        initialized = false;
        isOpening = false;
        currentState =
            CaseState.Disabled;

        if (caseCollider != null)
        {
            caseCollider.enabled = false;
        }

        Debug.LogError(
            $"{nameof(Case)} entered safety shutdown on '{name}': {reason}",
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
                caseCollider,
                nameof(Collider));

        valid &=
            ValidateReference(
                audioSource,
                nameof(AudioSource));

        valid &=
            ValidateReference(
                spawnPoint,
                "Spawn Point");

        if (validSpawnableItems.Count == 0)
        {
            Debug.LogWarning(
                $"{nameof(Case)} on '{name}' has no valid item prefabs.",
                this);
        }

        if (activateSwitchWhenOpened &&
            targetSwitch == null &&
            !resolveSwitchAutomatically)
        {
            Debug.LogWarning(
                $"{nameof(Case)} on '{name}' is configured to activate a switch, but no switch is assigned.",
                this);
        }

        return valid;
    }

    private bool ValidateReference(
        UnityEngine.Object reference,
        string displayName)
    {
        if (reference != null)
            return true;

        Debug.LogError(
            $"{nameof(Case)} requires {displayName} on '{name}'.",
            this);

        return false;
    }

    #endregion

    #region Activation

    private bool CanOpen()
    {
        if (!initialized ||
            shuttingDown ||
            applicationQuitting ||
            isOpening ||
            !isActiveAndEnabled)
        {
            return false;
        }

        if (openOnlyOnce &&
            hasOpened)
        {
            return false;
        }

        return true;
    }

    private bool IsValidActivator(
        Collider other)
    {
        if (other == null)
            return false;

        if (requirePlayerTag &&
            !HasTagInHierarchy(
                other.transform,
                playerTag))
        {
            return false;
        }

        if (!requirePlayerMovement)
            return true;

        UltimatePlayerMovement movement =
            other.GetComponent<UltimatePlayerMovement>();

        movement ??=
            other.GetComponentInParent<UltimatePlayerMovement>();

        movement ??=
            other.GetComponentInChildren<UltimatePlayerMovement>(
                includeInactive: true);

        return
            movement != null &&
            movement.isActiveAndEnabled &&
            movement.IsInitialized &&
            !movement.IsSafetyShutdown;
    }

    private void DisableCaseCollider()
    {
        if (caseCollider != null)
        {
            caseCollider.enabled = false;
        }
    }

    private void RestoreCaseCollider()
    {
        if (caseCollider != null)
        {
            caseCollider.enabled = true;
            caseCollider.isTrigger = true;
        }
    }

    #endregion

    #region Item Spawning

    private int SpawnContents()
    {
        CacheSpawnableItems();
        RemoveDestroyedSpawnedItems();

        if (validSpawnableItems.Count == 0)
            return 0;

        return spawnMode switch
        {
            SpawnMode.RandomItem =>
                SpawnRandomItem(),

            SpawnMode.AllItems =>
                SpawnAllItems(),

            _ => 0
        };
    }

    private int SpawnRandomItem()
    {
        int randomIndex =
            UnityEngine.Random.Range(
                0,
                validSpawnableItems.Count);

        return SpawnItem(
                validSpawnableItems[randomIndex],
                0,
                1) != null
            ? 1
            : 0;
    }

    private int SpawnAllItems()
    {
        int spawnedCount = 0;
        int itemCount =
            validSpawnableItems.Count;

        for (int index = 0;
             index < itemCount;
             index++)
        {
            if (SpawnItem(
                    validSpawnableItems[index],
                    index,
                    itemCount) != null)
            {
                spawnedCount++;
            }
        }

        return spawnedCount;
    }

    private GameObject SpawnItem(
        GameObject itemPrefab,
        int itemIndex,
        int itemCount)
    {
        if (itemPrefab == null ||
            !IsValidTransform(
                spawnPoint) ||
            shuttingDown ||
            applicationQuitting)
        {
            return null;
        }

        if (preventDuplicateSpawns &&
            HasSpawnedPrefab(
                itemPrefab))
        {
            return null;
        }

        if (!TryFindSafeSpawnPosition(
                itemIndex,
                itemCount,
                out Vector3 safePosition))
        {
            Debug.LogWarning(
                $"{nameof(Case)} on '{name}' could not find a clear position for '{itemPrefab.name}'.",
                this);

            safePosition =
                spawnPoint.position;
        }

        Quaternion spawnRotation =
            IsFiniteQuaternion(
                spawnPoint.rotation)
                ? spawnPoint.rotation
                : Quaternion.identity;

        GameObject spawnedItem =
            Instantiate(
                itemPrefab,
                safePosition,
                spawnRotation);

        if (spawnedItem == null)
            return null;

        spawnedItems.Add(
            spawnedItem);

        PrepareSpawnedItem(
            spawnedItem);

        ItemSpawned?.Invoke(
            this,
            spawnedItem);

        LogStateChange(
            $"Spawned '{spawnedItem.name}'.");

        return spawnedItem;
    }

    private bool TryFindSafeSpawnPosition(
        int itemIndex,
        int itemCount,
        out Vector3 safePosition)
    {
        safePosition =
            CalculateSpawnPosition(
                itemIndex,
                itemCount);

        if (spawnClearanceRadius <= 0f)
            return true;

        for (int attempt = 0;
             attempt < maximumSpawnAttempts;
             attempt++)
        {
            Vector3 attemptPosition =
                safePosition +
                spawnPoint.up *
                itemSpacing *
                attempt;

            if (!Physics.CheckSphere(
                    attemptPosition,
                    spawnClearanceRadius,
                    spawnBlockingLayers,
                    QueryTriggerInteraction.Ignore))
            {
                safePosition =
                    attemptPosition;

                return true;
            }
        }

        return false;
    }

    private Vector3 CalculateSpawnPosition(
        int itemIndex,
        int itemCount)
    {
        Vector3 position =
            spawnPoint.position;

        if (itemCount <= 1)
            return position;

        float centeredIndex =
            itemIndex -
            (itemCount - 1) * 0.5f;

        position +=
            spawnPoint.right *
            centeredIndex *
            itemSpacing;

        return position;
    }

    private void PrepareSpawnedItem(
        GameObject spawnedItem)
    {
        if (!launchSpawnedItems ||
            spawnedItem == null)
        {
            return;
        }

        Rigidbody itemRigidbody =
            spawnedItem.GetComponent<Rigidbody>();

        itemRigidbody ??=
            spawnedItem.GetComponentInChildren<Rigidbody>(
                includeInactive: true);

        if (itemRigidbody == null)
            return;

        Vector3 launchDirection =
            GetLaunchDirection();

        if (clearSpawnedVelocity)
        {
            itemRigidbody.linearVelocity =
                Vector3.zero;

            itemRigidbody.angularVelocity =
                Vector3.zero;
        }

        itemRigidbody.isKinematic = false;
        itemRigidbody.useGravity = true;

        itemRigidbody.AddForce(
            launchDirection *
            itemLaunchSpeed,
            ForceMode.VelocityChange);

        itemRigidbody.WakeUp();
    }

    private bool HasSpawnedPrefab(
        GameObject itemPrefab)
    {
        if (itemPrefab == null)
            return false;

        string expectedName =
            $"{itemPrefab.name}(Clone)";

        foreach (GameObject spawnedItem
                 in spawnedItems)
        {
            if (spawnedItem != null &&
                string.Equals(
                    spawnedItem.name,
                    expectedName,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 GetLaunchDirection()
    {
        Transform reference =
            IsValidTransform(
                spawnPoint)
                ? spawnPoint
                : transform;

        Vector3 direction =
            reference.TransformDirection(
                localLaunchDirection);

        if (!IsFiniteVector(
                direction) ||
            direction.sqrMagnitude <=
                0.0001f)
        {
            direction =
                reference.up;
        }

        return direction.normalized;
    }

    #endregion

    #region Switch System

    private bool ActivateConfiguredSwitch()
    {
        Switch resolvedSwitch =
            ResolveSwitch();

        if (resolvedSwitch == null)
        {
            Debug.LogWarning(
                $"{nameof(Case)} on '{name}' could not resolve a switch.",
                this);

            return false;
        }

        resolvedSwitch.ActivateSwitch();

        PlaySound(
            switchActivatedSound);

        SwitchActivated?.Invoke(
            this,
            resolvedSwitch);

        LogStateChange(
            $"Activated switch '{resolvedSwitch.name}'.");

        return true;
    }

    private Switch ResolveSwitch()
    {
        if (IsValidSwitch(
                targetSwitch,
                includeInactiveSwitches))
        {
            return targetSwitch;
        }

        targetSwitch = null;

        if (!resolveSwitchAutomatically)
            return null;

        targetSwitch =
            FindNearestSwitch();

        return targetSwitch;
    }

    private Switch FindNearestSwitch()
    {
        Switch[] switches =
            FindObjectsByType<Switch>(
                includeInactiveSwitches
                    ? FindObjectsInactive.Include
                    : FindObjectsInactive.Exclude);

        Switch closestSwitch =
            null;

        float maximumSqrDistance =
            switchSearchRadius *
            switchSearchRadius;

        float closestSqrDistance =
            maximumSqrDistance;

        foreach (Switch candidate
                 in switches)
        {
            if (!IsValidSwitch(
                    candidate,
                    includeInactiveSwitches))
            {
                continue;
            }

            float sqrDistance =
                (candidate.transform.position -
                 transform.position)
                .sqrMagnitude;

            if (sqrDistance >
                closestSqrDistance)
            {
                continue;
            }

            closestSqrDistance =
                sqrDistance;

            closestSwitch =
                candidate;
        }

        return closestSwitch;
    }

    private static bool IsValidSwitch(
        Switch candidate,
        bool allowInactive)
    {
        if (candidate == null ||
            !candidate.gameObject.scene.IsValid())
        {
            return false;
        }

        return
            allowInactive ||
            candidate.gameObject.activeInHierarchy;
    }

    #endregion

    #region Presentation

    private void PlayOpenPresentation()
    {
        if (caseAnimator != null &&
            openTriggerHash != 0)
        {
            caseAnimator.SetTrigger(
                openTriggerHash);
        }

        if (openEffect != null)
        {
            openEffect.Play();
        }

        PlaySound(
            openSound);
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

    #endregion

    #region Lifetime

    private void ScheduleDestruction()
    {
        if (disableVisualsBeforeDestroy)
        {
            SetRenderersEnabled(
                false);
        }

        if (destroyDelay <= 0f)
        {
            DestroyCase();
            return;
        }

        Invoke(
            nameof(DestroyCase),
            destroyDelay);
    }

    private void DestroyCase()
    {
        if (shuttingDown ||
            applicationQuitting)
        {
            return;
        }

        Destroy(
            gameObject);
    }

    private void SetRenderersEnabled(
        bool value)
    {
        if (caseRenderers == null)
            return;

        foreach (Renderer caseRenderer
                 in caseRenderers)
        {
            if (caseRenderer != null)
            {
                caseRenderer.enabled =
                    value;
            }
        }
    }

    #endregion

    #region Helpers

    private Transform FindDescendantByName(
        string targetName)
    {
        if (string.IsNullOrWhiteSpace(
                targetName))
        {
            return null;
        }

        Transform[] descendants =
            GetComponentsInChildren<Transform>(
                includeInactive: true);

        foreach (Transform descendant
                 in descendants)
        {
            if (descendant == null)
                continue;

            if (string.Equals(
                    descendant.name,
                    targetName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return descendant;
            }
        }

        return null;
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

    private static bool IsValidTransform(
        Transform value)
    {
        return
            value != null &&
            value.gameObject.scene.IsValid() &&
            IsFiniteVector(
                value.position) &&
            IsFiniteQuaternion(
                value.rotation);
    }

    private static bool IsValidScale(
        Vector3 scale)
    {
        return
            IsFiniteVector(scale) &&
            Mathf.Abs(scale.x) >
                0.0001f &&
            Mathf.Abs(scale.y) >
                0.0001f &&
            Mathf.Abs(scale.z) >
                0.0001f;
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