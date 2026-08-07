using UnityEngine;

[DisallowMultipleComponent]
public sealed class Health : MonoBehaviour
{
    #region Animator Hashes

    private static readonly int SleepHash =
        Animator.StringToHash(
            "Sleep");

    private static readonly int DieHash =
        Animator.StringToHash(
            "Die");

    #endregion

    #region Inspector

    [Header("Health")]
    [SerializeField, Min(1)]
    private int startingHealth = 3;

    [Header("References")]
    public HealthBar bar;

    [Header("Rewards")]
    [SerializeField, Min(0)]
    private int powerReward = 10;

    [SerializeField, Min(0f)]
    private float destroyDelay = 4f;

    #endregion

    #region Runtime State

    [SerializeField]
    private int health;

    [SerializeField]
    public bool dead;

    private int maximumHealth;

    private Collider healthCollider;
    private Animator healthAnimator;
    private HUD hud;

    private bool isInitialized;
    private bool isShuttingDown;

    #endregion

    #region Public API

    public int HealthValue
    {
        get => health;

        set
        {
            SetHealth(
                value);
        }
    }

    public int MaximumHealth =>
        maximumHealth;

    public bool IsDead =>
        dead;

    public bool IsInitialized =>
        isInitialized;

    public void TakeDamage(
        int damage)
    {
        if (!CanTakeDamage(
                damage))
        {
            return;
        }

        int nextHealth =
            health -
            damage;

        SetHealth(
            nextHealth);
    }

    public void RestoreHealth(
        int amount)
    {
        if (!isInitialized ||
            dead ||
            amount <= 0)
        {
            return;
        }

        SetHealth(
            health + amount);
    }

    public void RestoreFullHealth()
    {
        if (!isInitialized)
            return;

        SetHealth(
            maximumHealth);
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveDependencies();
    }

    private void Start()
    {
        if (!InitializeHealth())
        {
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (isShuttingDown)
            return;

        ResolveDependencies();

        if (!isInitialized)
            return;

        ApplyHealthState();
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
        startingHealth =
            Mathf.Max(
                1,
                startingHealth);

        powerReward =
            Mathf.Max(
                0,
                powerReward);

        destroyDelay =
            Mathf.Max(
                0f,
                destroyDelay);
    }

    private void OnCollisionEnter(
        Collision collision)
    {
        if (!isInitialized ||
            dead ||
            collision == null ||
            collision.transform == null)
        {
            return;
        }

        if (!collision.transform.CompareTag(
                "Player"))
        {
            return;
        }

        TakeDamage(1);
    }

    #endregion

    #region Initialization

    private bool InitializeHealth()
    {
        if (isInitialized)
            return true;

        ResolveDependencies();

        if (!ValidateConfiguration())
        {
            isInitialized = false;

            Debug.LogError(
                $"Health failed to initialize on '{name}'.",
                this);

            return false;
        }

        ResetRuntimeState();

        isInitialized = true;

        ApplyHealthState();
        RefreshHealthBar();

        return true;
    }

    private void ResolveDependencies()
    {
        ResolveCollider();
        ResolveAnimator();
        ResolveHUD();
    }

    private void ResolveCollider()
    {
        healthCollider ??=
            GetComponent<Collider>();

        healthCollider ??=
            GetComponentInChildren<Collider>(
                includeInactive: true);
    }

    private void ResolveAnimator()
    {
        healthAnimator ??=
            GetComponent<Animator>();

        healthAnimator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        healthAnimator ??=
            GetComponentInParent<Animator>();
    }

    private void ResolveHUD()
    {
        hud ??=
            FindAnyObjectByType<HUD>(
                FindObjectsInactive.Exclude);
    }

    private void ResetRuntimeState()
    {
        maximumHealth =
            ResolveMaximumHealth();

        health =
            Mathf.Clamp(
                startingHealth,
                0,
                maximumHealth);

        dead =
            health <= 0;
    }

    private int ResolveMaximumHealth()
    {
        if (bar != null &&
            bar.bars != null &&
            bar.bars.Length > 0)
        {
            return Mathf.Max(
                1,
                bar.bars.Length - 1);
        }

        return Mathf.Max(
            1,
            startingHealth);
    }

    #endregion

    #region Health State

    private bool CanTakeDamage(
        int damage)
    {
        return
            isInitialized &&
            !dead &&
            damage > 0;
    }

    private void SetHealth(
        int value)
    {
        if (!isInitialized)
            return;

        maximumHealth =
            ResolveMaximumHealth();

        bool wasDead =
            dead;

        health =
            Mathf.Clamp(
                value,
                0,
                maximumHealth);

        dead =
            health <= 0;

        RefreshHealthBar();

        if (dead)
        {
            if (!wasDead)
            {
                HandleDeath();
            }

            return;
        }

        ApplyHealthState();
    }

    private void ApplyHealthState()
    {
        if (dead)
            return;

        if (healthCollider != null &&
            !healthCollider.enabled)
        {
            healthCollider.enabled =
                true;
        }

        if (healthAnimator != null)
        {
            healthAnimator.SetBool(
                SleepHash,
                false);
        }
    }

    private void HandleDeath()
    {
        health = 0;
        dead = true;

        if (healthAnimator != null)
        {
            healthAnimator.SetTrigger(
                DieHash);
        }

        if (healthCollider != null)
        {
            healthCollider.enabled =
                false;
        }

        if (hud != null &&
            powerReward > 0)
        {
            hud.AddPower(
                powerReward);
        }

        Destroy(
            gameObject,
            destroyDelay);
    }

    private void RefreshHealthBar()
    {
        if (bar == null)
            return;

        bar.Hit(
            health);
    }

    #endregion

    #region Validation

    private bool ValidateConfiguration()
    {
        bool valid = true;

        if (healthCollider == null)
        {
            Debug.LogWarning(
                $"Health on '{name}' could not find a Collider.",
                this);
        }

        if (healthAnimator == null)
        {
            Debug.LogWarning(
                $"Health on '{name}' could not find an Animator.",
                this);
        }

        if (bar == null)
        {
            Debug.LogWarning(
                $"Health on '{name}' has no HealthBar.",
                this);
        }

        maximumHealth =
            ResolveMaximumHealth();

        valid &=
            maximumHealth > 0;

        return valid;
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        if (!isInitialized)
            return;
    }

    private void CleanupDestroyedState()
    {
        isInitialized = false;

        healthCollider = null;
        healthAnimator = null;
        hud = null;
        bar = null;
    }

    #endregion
}