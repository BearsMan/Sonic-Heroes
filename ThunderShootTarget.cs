using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ThunderShootTarget : MonoBehaviour
{
    #region Types

    public enum TargetState
    {
        Uninitialized,
        Active,
        Stunned,
        Invulnerable,
        Dead,
        Disabled
    }

    #endregion

    #region Inspector

    [Header("Health")]
    [SerializeField, Min(1)] private int maximumHealth = 3;
    [SerializeField, Min(0)] private int startingHealth = 3;
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField, Min(0f)] private float destructionDelay = 0.1f;

    [Header("Thunder Shoot")]
    [SerializeField, Min(0)] private int defaultThunderDamage = 1;
    [SerializeField, Min(0f)] private float defaultStunDuration = 2f;
    [SerializeField] private bool allowStunAtZeroDamage = true;
    [SerializeField] private bool refreshStunDuration = true;

    [Header("Damage Protection")]
    [SerializeField, Min(0f)] private float damageImmunityDuration = 0.1f;
    [SerializeField] private bool ignoreDamageWhileDead = true;
    [SerializeField] private bool ignoreDamageWhileDisabled = true;

    [Header("Stun Control")]
    [Tooltip(
        "Behaviours such as enemy movement, attacks, or AI that should be " +
        "disabled while this target is stunned.")]
    [SerializeField] private Behaviour[] behavioursDisabledWhileStunned;

    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] private bool freezeRigidbodyWhileStunned;
    [SerializeField] private bool clearVelocityWhenStunned = true;

    [Header("Animation")]
    [SerializeField] private Animator targetAnimator;
    [SerializeField] private string stunnedBool = "Stunned";
    [SerializeField] private string damagedTrigger = "Damaged";
    [SerializeField] private string deathTrigger = "Dead";

    [Header("Effects")]
    [SerializeField] private ParticleSystem stunEffect;
    [SerializeField] private ParticleSystem damageEffect;
    [SerializeField] private ParticleSystem deathEffect;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip stunSound;
    [SerializeField] private AudioClip damageSound;
    [SerializeField] private AudioClip deathSound;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;
    [SerializeField]
    private TargetState currentState =
        TargetState.Uninitialized;

    #endregion

    #region Runtime State

    private bool[] originalBehaviourStates;

    private RigidbodyConstraints originalConstraints;
    private bool originalUseGravity;
    private bool originalIsKinematic;

    private int currentHealth;

    private float stunTimer;
    private float immunityTimer;

    private int stunnedBoolHash;
    private int damagedTriggerHash;
    private int deathTriggerHash;

    private bool initialized;
    private bool isStunned;
    private bool isDead;
    private bool rigidbodyStateCached;
    private bool shuttingDown;
    private bool applicationQuitting;

    #endregion

    #region Events

    public event Action<ThunderShootTarget, int> Damaged;
    public event Action<ThunderShootTarget> Stunned;
    public event Action<ThunderShootTarget> Recovered;
    public event Action<ThunderShootTarget> Died;

    #endregion

    #region Public API

    public int CurrentHealth =>
        currentHealth;

    public int MaximumHealth =>
        maximumHealth;

    public bool IsInitialized =>
        initialized;

    public bool IsStunned =>
        isStunned;

    public bool IsDead =>
        isDead;

    public bool IsInvulnerable =>
        immunityTimer > 0f;

    public TargetState CurrentState =>
        currentState;

    public float RemainingStunTime =>
        Mathf.Max(
            0f,
            stunTimer);

    public bool HitByThunderShoot(
        int damage,
        float stunDuration)
    {
        if (!CanReceiveThunderShoot())
            return false;

        int safeDamage =
            Mathf.Max(
                0,
                damage);

        float safeStunDuration =
            Mathf.Max(
                0f,
                stunDuration);

        bool damageApplied =
            safeDamage > 0 &&
            ApplyDamage(
                safeDamage);

        if (isDead)
            return damageApplied;

        bool shouldStun =
            safeStunDuration > 0f &&
            (damageApplied ||
             allowStunAtZeroDamage);

        if (shouldStun)
        {
            ApplyStun(
                safeStunDuration);
        }

        return
            damageApplied ||
            shouldStun;
    }

    public bool TakeDamage(
        int damage)
    {
        return ApplyDamage(
            damage);
    }

    public bool Stun()
    {
        return ApplyStun(
            defaultStunDuration);
    }

    public bool Stun(
        float duration)
    {
        return ApplyStun(
            duration);
    }

    public void OnThunderShootHit(
        GameObject source)
    {
        HitByThunderShoot(
            defaultThunderDamage,
            defaultStunDuration);
    }

    public void ThunderShootHit(
        GameObject source)
    {
        HitByThunderShoot(
            defaultThunderDamage,
            defaultStunDuration);
    }

    public bool RestoreHealth(
        int amount)
    {
        if (!initialized ||
            isDead ||
            amount <= 0)
        {
            return false;
        }

        int previousHealth =
            currentHealth;

        currentHealth =
            Mathf.Clamp(
                currentHealth + amount,
                0,
                maximumHealth);

        return
            currentHealth >
            previousHealth;
    }

    public bool SetHealth(
        int value)
    {
        if (!initialized ||
            isDead)
        {
            return false;
        }

        currentHealth =
            Mathf.Clamp(
                value,
                0,
                maximumHealth);

        if (currentHealth <= 0)
        {
            Die();
        }

        return true;
    }

    public void ClearStun()
    {
        EndStun();
    }

    public bool ResetTarget()
    {
        if (shuttingDown ||
            applicationQuitting)
        {
            return false;
        }

        CancelInvoke(
            nameof(DestroyTarget));

        isDead = false;
        isStunned = false;

        currentHealth =
            Mathf.Clamp(
                startingHealth,
                1,
                maximumHealth);

        stunTimer = 0f;
        immunityTimer = 0f;

        RestoreStunnedBehaviours();
        RestoreRigidbodyState();

        if (targetAnimator != null &&
            stunnedBoolHash != 0)
        {
            targetAnimator.SetBool(
                stunnedBoolHash,
                false);
        }

        currentState =
            TargetState.Active;

        enabled = true;

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
            !isDead &&
            !isStunned)
        {
            currentState =
                TargetState.Active;
        }
    }

    private void Update()
    {
        if (!initialized ||
            shuttingDown ||
            applicationQuitting ||
            isDead)
        {
            return;
        }

        UpdateImmunityTimer();
        UpdateStunTimer();
        ValidateRuntimeState();
    }

    private void OnDisable()
    {
        if (shuttingDown ||
            applicationQuitting)
        {
            return;
        }

        if (isStunned)
        {
            EndStun();
        }

        currentState =
            TargetState.Disabled;
    }

    private void OnApplicationQuit()
    {
        applicationQuitting = true;
    }

    private void OnDestroy()
    {
        shuttingDown = true;
        initialized = false;

        CancelInvoke();

        RestoreStunnedBehaviours();
        RestoreRigidbodyState();

        Damaged = null;
        Stunned = null;
        Recovered = null;
        Died = null;

        behavioursDisabledWhileStunned = null;
        originalBehaviourStates = null;

        targetRigidbody = null;
        targetAnimator = null;
        audioSource = null;

        stunEffect = null;
        damageEffect = null;
        deathEffect = null;

        currentState =
            TargetState.Disabled;
    }

    private void OnValidate()
    {
        maximumHealth =
            Mathf.Max(
                1,
                maximumHealth);

        startingHealth =
            Mathf.Clamp(
                startingHealth,
                1,
                maximumHealth);

        defaultThunderDamage =
            Mathf.Max(
                0,
                defaultThunderDamage);

        defaultStunDuration =
            Mathf.Max(
                0f,
                defaultStunDuration);

        damageImmunityDuration =
            Mathf.Max(
                0f,
                damageImmunityDuration);

        destructionDelay =
            Mathf.Max(
                0f,
                destructionDelay);

        CacheAnimatorHashes();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ResolveReferences();
        }
#endif
    }

    #endregion

    #region Initialization

    private bool Initialize()
    {
        if (initialized)
            return true;

        ResolveReferences();
        CacheAnimatorHashes();
        CacheBehaviourStates();
        CacheRigidbodyState();

        maximumHealth =
            Mathf.Max(
                1,
                maximumHealth);

        startingHealth =
            Mathf.Clamp(
                startingHealth,
                1,
                maximumHealth);

        currentHealth =
            startingHealth;

        stunTimer = 0f;
        immunityTimer = 0f;

        isStunned = false;
        isDead = false;

        currentState =
            TargetState.Active;

        initialized = true;

        return true;
    }

    private void ResolveReferences()
    {
        targetRigidbody ??=
            GetComponent<Rigidbody>();

        targetRigidbody ??=
            GetComponentInParent<Rigidbody>();

        targetRigidbody ??=
            GetComponentInChildren<Rigidbody>(
                includeInactive: true);

        targetAnimator ??=
            GetComponent<Animator>();

        targetAnimator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        targetAnimator ??=
            GetComponentInParent<Animator>();

        audioSource ??=
            GetComponent<AudioSource>();

        audioSource ??=
            GetComponentInChildren<AudioSource>(
                includeInactive: true);
    }

    private void CacheAnimatorHashes()
    {
        stunnedBoolHash =
            string.IsNullOrWhiteSpace(
                stunnedBool)
                ? 0
                : Animator.StringToHash(
                    stunnedBool);

        damagedTriggerHash =
            string.IsNullOrWhiteSpace(
                damagedTrigger)
                ? 0
                : Animator.StringToHash(
                    damagedTrigger);

        deathTriggerHash =
            string.IsNullOrWhiteSpace(
                deathTrigger)
                ? 0
                : Animator.StringToHash(
                    deathTrigger);
    }

    private void CacheBehaviourStates()
    {
        if (behavioursDisabledWhileStunned == null)
        {
            behavioursDisabledWhileStunned =
                Array.Empty<Behaviour>();
        }

        originalBehaviourStates =
            new bool[
                behavioursDisabledWhileStunned.Length];

        for (int index = 0;
             index <
             behavioursDisabledWhileStunned.Length;
             index++)
        {
            Behaviour targetBehaviour =
                behavioursDisabledWhileStunned[index];

            originalBehaviourStates[index] =
                targetBehaviour != null &&
                targetBehaviour.enabled;
        }
    }

    private void CacheRigidbodyState()
    {
        if (targetRigidbody == null ||
            rigidbodyStateCached)
        {
            return;
        }

        originalConstraints =
            targetRigidbody.constraints;

        originalUseGravity =
            targetRigidbody.useGravity;

        originalIsKinematic =
            targetRigidbody.isKinematic;

        rigidbodyStateCached = true;
    }

    #endregion

    #region Runtime Safety

    private void ValidateRuntimeState()
    {
        if (currentHealth < 0 ||
            currentHealth > maximumHealth)
        {
            currentHealth =
                Mathf.Clamp(
                    currentHealth,
                    0,
                    maximumHealth);
        }

        if (targetRigidbody == null)
        {
            ResolveReferences();
            CacheRigidbodyState();
        }

        if (targetAnimator == null)
        {
            ResolveReferences();
        }

        if (isStunned &&
            stunTimer <= 0f)
        {
            EndStun();
        }

        if (!isDead &&
            currentHealth <= 0)
        {
            Die();
        }
    }

    private bool CanReceiveThunderShoot()
    {
        if (!initialized ||
            shuttingDown ||
            applicationQuitting)
        {
            return false;
        }

        if (ignoreDamageWhileDead &&
            isDead)
        {
            return false;
        }

        if (ignoreDamageWhileDisabled &&
            !isActiveAndEnabled)
        {
            return false;
        }

        return true;
    }

    private bool CanReceiveDamage()
    {
        return
            CanReceiveThunderShoot() &&
            immunityTimer <= 0f;
    }

    #endregion

    #region Damage

    private bool ApplyDamage(
        int damage)
    {
        if (!CanReceiveDamage() ||
            damage <= 0)
        {
            return false;
        }

        int previousHealth =
            currentHealth;

        currentHealth =
            Mathf.Max(
                0,
                currentHealth - damage);

        int appliedDamage =
            previousHealth -
            currentHealth;

        if (appliedDamage <= 0)
            return false;

        immunityTimer =
            damageImmunityDuration;

        PlayDamagePresentation();

        Damaged?.Invoke(
            this,
            appliedDamage);

        LogStateChange(
            $"'{name}' received {appliedDamage} damage. " +
            $"Health: {currentHealth}/{maximumHealth}.");

        if (currentHealth <= 0)
        {
            Die();
        }
        else if (!isStunned)
        {
            currentState =
                immunityTimer > 0f
                    ? TargetState.Invulnerable
                    : TargetState.Active;
        }

        return true;
    }

    private void UpdateImmunityTimer()
    {
        if (immunityTimer <= 0f)
            return;

        immunityTimer =
            Mathf.Max(
                0f,
                immunityTimer -
                Time.deltaTime);

        if (immunityTimer <= 0f &&
            !isStunned &&
            !isDead)
        {
            currentState =
                TargetState.Active;
        }
    }

    #endregion

    #region Stun

    private bool ApplyStun(
        float duration)
    {
        if (!CanReceiveThunderShoot() ||
            isDead ||
            duration <= 0f)
        {
            return false;
        }

        if (isStunned)
        {
            stunTimer =
                refreshStunDuration
                    ? Mathf.Max(
                        stunTimer,
                        duration)
                    : stunTimer + duration;

            return true;
        }

        isStunned = true;
        stunTimer = duration;

        currentState =
            TargetState.Stunned;

        DisableStunnedBehaviours();
        ApplyStunnedRigidbodyState();
        PlayStunPresentation();

        Stunned?.Invoke(
            this);

        LogStateChange(
            $"'{name}' stunned for {duration:0.00} seconds.");

        return true;
    }

    private void UpdateStunTimer()
    {
        if (!isStunned)
            return;

        stunTimer =
            Mathf.Max(
                0f,
                stunTimer -
                Time.deltaTime);

        if (stunTimer <= 0f)
        {
            EndStun();
        }
    }

    private void EndStun()
    {
        if (!isStunned)
            return;

        isStunned = false;
        stunTimer = 0f;

        RestoreStunnedBehaviours();
        RestoreRigidbodyState();

        if (targetAnimator != null &&
            stunnedBoolHash != 0)
        {
            targetAnimator.SetBool(
                stunnedBoolHash,
                false);
        }

        if (stunEffect != null)
        {
            stunEffect.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting);
        }

        if (!isDead)
        {
            currentState =
                immunityTimer > 0f
                    ? TargetState.Invulnerable
                    : TargetState.Active;
        }

        Recovered?.Invoke(
            this);

        LogStateChange(
            $"'{name}' recovered from stun.");
    }

    private void DisableStunnedBehaviours()
    {
        if (behavioursDisabledWhileStunned == null)
            return;

        for (int index = 0;
             index <
             behavioursDisabledWhileStunned.Length;
             index++)
        {
            Behaviour targetBehaviour =
                behavioursDisabledWhileStunned[index];

            if (targetBehaviour == null ||
                targetBehaviour == this)
            {
                continue;
            }

            targetBehaviour.enabled = false;
        }
    }

    private void RestoreStunnedBehaviours()
    {
        if (behavioursDisabledWhileStunned == null ||
            originalBehaviourStates == null)
        {
            return;
        }

        int restoreCount =
            Mathf.Min(
                behavioursDisabledWhileStunned.Length,
                originalBehaviourStates.Length);

        for (int index = 0;
             index < restoreCount;
             index++)
        {
            Behaviour targetBehaviour =
                behavioursDisabledWhileStunned[index];

            if (targetBehaviour == null ||
                targetBehaviour == this)
            {
                continue;
            }

            targetBehaviour.enabled =
                originalBehaviourStates[index];
        }
    }

    private void ApplyStunnedRigidbodyState()
    {
        if (targetRigidbody == null)
            return;

        CacheRigidbodyState();

        if (clearVelocityWhenStunned)
        {
            targetRigidbody.linearVelocity =
                Vector3.zero;

            targetRigidbody.angularVelocity =
                Vector3.zero;
        }

        if (freezeRigidbodyWhileStunned)
        {
            targetRigidbody.useGravity = false;
            targetRigidbody.isKinematic = true;
        }
    }

    private void RestoreRigidbodyState()
    {
        if (targetRigidbody == null ||
            !rigidbodyStateCached)
        {
            return;
        }

        targetRigidbody.constraints =
            originalConstraints;

        targetRigidbody.useGravity =
            originalUseGravity;

        targetRigidbody.isKinematic =
            originalIsKinematic;

        targetRigidbody.WakeUp();
    }

    #endregion

    #region Death

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        isStunned = false;

        currentHealth = 0;
        stunTimer = 0f;
        immunityTimer = 0f;

        currentState =
            TargetState.Dead;

        RestoreStunnedBehaviours();
        RestoreRigidbodyState();
        PlayDeathPresentation();

        Died?.Invoke(
            this);

        LogStateChange(
            $"'{name}' was defeated.");

        if (!destroyOnDeath)
            return;

        if (destructionDelay <= 0f)
        {
            DestroyTarget();
            return;
        }

        Invoke(
            nameof(DestroyTarget),
            destructionDelay);
    }

    private void DestroyTarget()
    {
        if (shuttingDown ||
            applicationQuitting)
        {
            return;
        }

        Destroy(
            gameObject);
    }

    #endregion

    #region Presentation

    private void PlayDamagePresentation()
    {
        if (targetAnimator != null &&
            damagedTriggerHash != 0)
        {
            targetAnimator.SetTrigger(
                damagedTriggerHash);
        }

        if (damageEffect != null)
        {
            damageEffect.Play();
        }

        PlaySound(
            damageSound);
    }

    private void PlayStunPresentation()
    {
        if (targetAnimator != null &&
            stunnedBoolHash != 0)
        {
            targetAnimator.SetBool(
                stunnedBoolHash,
                true);
        }

        if (stunEffect != null)
        {
            stunEffect.Play();
        }

        PlaySound(
            stunSound);
    }

    private void PlayDeathPresentation()
    {
        if (targetAnimator != null)
        {
            if (stunnedBoolHash != 0)
            {
                targetAnimator.SetBool(
                    stunnedBoolHash,
                    false);
            }

            if (deathTriggerHash != 0)
            {
                targetAnimator.SetTrigger(
                    deathTriggerHash);
            }
        }

        if (stunEffect != null)
        {
            stunEffect.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting);
        }

        if (deathEffect != null)
        {
            deathEffect.Play();
        }

        PlaySound(
            deathSound);
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