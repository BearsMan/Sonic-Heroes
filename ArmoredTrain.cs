using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ArmoredTrain : TrackVehicle
{
    #region Types

    public enum TrainState
    {
        Operational,
        Damaged,
        CoreDestroyed,
        Destroying,
        Destroyed
    }

    #endregion

    #region Constants

    private const string EngineCoreName = "Engine Core";

    #endregion

    #region Inspector

    [Header("Engine Core")]
    [SerializeField] private Transform engineCore;
    [SerializeField] private Collider engineCoreCollider;
    [SerializeField, Min(1)] private int maximumCoreHealth = 10;
    [SerializeField, Min(0f)] private float damageCooldown = 0.15f;

    [Header("Train Cars")]
    [SerializeField]
    private GameObject[] trainCars =
        Array.Empty<GameObject>();

    [SerializeField] private bool destroyCarsWhenCoreBreaks = true;
    [SerializeField, Min(0f)] private float carDestructionInterval = 0.25f;

    [Header("Train Behavior")]
    [SerializeField] private bool stopWhenCoreDestroyed = true;
    [SerializeField] private bool completeTrackWhenDestroyed;
    [SerializeField, Min(0f)] private float collisionKnockbackSpeed = 12f;
    [SerializeField, Min(0f)] private float collisionHurtDuration = 0.75f;

    [Header("Presentation")]
    [SerializeField] private ParticleSystem coreDamageEffect;
    [SerializeField] private ParticleSystem coreDestroyEffect;
    [SerializeField] private AudioClip coreDamageClip;
    [SerializeField] private AudioClip coreDestroyClip;

    [Header("Debug")]
    [SerializeField] private bool logTrainStateChanges;

    #endregion

    #region Runtime State

    private TrainState currentTrainState =
        TrainState.Operational;

    private int currentCoreHealth;
    private int nextCarIndex;

    private float damageCooldownTimer;

    #endregion

    #region Public API

    public event Action<TrainState> TrainStateChanged;
    public event Action<int> CoreDamaged;
    public event Action CoreDestroyed;

    public TrainState CurrentTrainState =>
        currentTrainState;

    public int CurrentCoreHealth =>
        currentCoreHealth;

    public int MaximumCoreHealth =>
        maximumCoreHealth;

    public bool IsCoreDestroyed =>
        currentTrainState ==
            TrainState.CoreDestroyed ||
        currentTrainState ==
            TrainState.Destroying ||
        currentTrainState ==
            TrainState.Destroyed;

    public bool TakeCoreDamage(
        int amount)
    {
        if (amount <= 0 ||
            damageCooldownTimer > 0f ||
            IsCoreDestroyed)
        {
            return false;
        }

        int appliedDamage =
            Mathf.Min(
                currentCoreHealth,
                amount);

        currentCoreHealth =
            Mathf.Max(
                0,
                currentCoreHealth -
                appliedDamage);

        damageCooldownTimer =
            damageCooldown;

        CoreDamaged?.Invoke(
            appliedDamage);

        coreDamageEffect?.Play();

        PlayOneShot(
            coreDamageClip);

        if (currentCoreHealth <= 0)
        {
            HandleCoreDestroyed();
        }
        else
        {
            ChangeTrainState(
                TrainState.Damaged);
        }

        return true;
    }

    public bool ResetTrain()
    {
        if (!ResetVehicle())
            return false;

        CancelInvoke(
            nameof(DestroyNextCar));

        currentCoreHealth =
            maximumCoreHealth;

        nextCarIndex =
            0;

        damageCooldownTimer =
            0f;

        if (engineCoreCollider != null)
        {
            engineCoreCollider.enabled =
                true;
        }

        ChangeTrainState(
            TrainState.Operational);

        return true;
    }

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        ResolveTrainDependencies();

        base.Awake();
    }

    protected override void Start()
    {
        ResolveTrainDependencies();

        currentCoreHealth =
            maximumCoreHealth;

        nextCarIndex =
            0;

        base.Start();
    }

    protected override void Update()
    {
        base.Update();

        if (damageCooldownTimer > 0f)
        {
            damageCooldownTimer =
                Mathf.Max(
                    0f,
                    damageCooldownTimer -
                    Time.deltaTime);
        }
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        maximumCoreHealth =
            Mathf.Max(
                1,
                maximumCoreHealth);

        damageCooldown =
            Mathf.Max(
                0f,
                damageCooldown);

        carDestructionInterval =
            Mathf.Max(
                0f,
                carDestructionInterval);

        collisionKnockbackSpeed =
            Mathf.Max(
                0f,
                collisionKnockbackSpeed);

        collisionHurtDuration =
            Mathf.Max(
                0f,
                collisionHurtDuration);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ResolveTrainDependencies();
        }
#endif
    }

    protected override void OnDestroy()
    {
        CancelInvoke(
            nameof(DestroyNextCar));

        TrainStateChanged = null;
        CoreDamaged = null;
        CoreDestroyed = null;

        engineCore = null;
        engineCoreCollider = null;
        trainCars = null;

        coreDamageEffect = null;
        coreDestroyEffect = null;

        coreDamageClip = null;
        coreDestroyClip = null;

        base.OnDestroy();
    }

    #endregion

    #region Overrides

    protected override void OnVehicleCollision(
        Collision collision)
    {
        if (collision == null ||
            collision.collider == null)
        {
            return;
        }

        UltimatePlayerMovement movement =
            collision.collider
                .GetComponent<UltimatePlayerMovement>();

        movement ??=
            collision.collider
                .GetComponentInParent<UltimatePlayerMovement>();

        movement ??=
            collision.collider
                .GetComponentInChildren<UltimatePlayerMovement>(
                    includeInactive: true);

        if (movement == null)
            return;

        Vector3 direction =
            movement.transform.position -
            transform.position;

        direction.y =
            Mathf.Max(
                0.25f,
                direction.y);

        if (direction.sqrMagnitude <=
            0.0001f)
        {
            direction =
                transform.right;
        }

        Vector3 knockbackVelocity =
            direction.normalized *
            collisionKnockbackSpeed;

        movement.EnterHurtState(
            knockbackVelocity,
            collisionHurtDuration);
    }

    protected override void OnTrackCompleted()
    {
        if (currentTrainState ==
            TrainState.Damaged)
        {
            ChangeTrainState(
                TrainState.Operational);
        }
    }

    #endregion

    #region Initialization

    private void ResolveTrainDependencies()
    {
        if (engineCore == null)
        {
            Transform[] children =
                GetComponentsInChildren<Transform>(
                    includeInactive: true);

            foreach (Transform child
                     in children)
            {
                if (child != null &&
                    string.Equals(
                        child.name,
                        EngineCoreName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    engineCore =
                        child;

                    break;
                }
            }
        }

        if (engineCoreCollider == null &&
            engineCore != null)
        {
            engineCoreCollider =
                engineCore.GetComponent<Collider>();

            engineCoreCollider ??=
                engineCore.GetComponentInChildren<Collider>(
                    includeInactive: true);
        }

        trainCars ??=
            Array.Empty<GameObject>();
    }

    #endregion

    #region Train State

    private void ChangeTrainState(
        TrainState newState)
    {
        if (!Enum.IsDefined(
                typeof(TrainState),
                newState))
        {
            return;
        }

        if (currentTrainState ==
            newState)
        {
            return;
        }

        TrainState previousState =
            currentTrainState;

        currentTrainState =
            newState;

        TrainStateChanged?.Invoke(
            currentTrainState);

        if (logTrainStateChanges)
        {
            Debug.Log(
                $"ArmoredTrain state changed from {previousState} to {currentTrainState} on '{name}'.",
                this);
        }
    }

    private void HandleCoreDestroyed()
    {
        ChangeTrainState(
            TrainState.CoreDestroyed);

        if (engineCoreCollider != null)
        {
            engineCoreCollider.enabled =
                false;
        }

        coreDestroyEffect?.Play();

        PlayOneShot(
            coreDestroyClip);

        CoreDestroyed?.Invoke();

        if (stopWhenCoreDestroyed)
        {
            StopDriving();
        }

        if (destroyCarsWhenCoreBreaks)
        {
            BeginCarDestruction();
        }
        else
        {
            FinishTrainDestruction();
        }
    }

    #endregion

    #region Car Destruction

    private void BeginCarDestruction()
    {
        ChangeTrainState(
            TrainState.Destroying);

        nextCarIndex =
            0;

        if (trainCars.Length == 0)
        {
            FinishTrainDestruction();
            return;
        }

        InvokeRepeating(
            nameof(DestroyNextCar),
            0f,
            Mathf.Max(
                0.01f,
                carDestructionInterval));
    }

    private void DestroyNextCar()
    {
        while (nextCarIndex <
                   trainCars.Length &&
               trainCars[nextCarIndex] == null)
        {
            nextCarIndex++;
        }

        if (nextCarIndex >=
            trainCars.Length)
        {
            CancelInvoke(
                nameof(DestroyNextCar));

            FinishTrainDestruction();
            return;
        }

        GameObject car =
            trainCars[nextCarIndex];

        nextCarIndex++;

        if (car != null)
        {
            Destroy(
                car);
        }
    }

    private void FinishTrainDestruction()
    {
        ChangeTrainState(
            TrainState.Destroyed);

        if (completeTrackWhenDestroyed)
        {
            CompleteTrack();
        }
    }

    #endregion
}
