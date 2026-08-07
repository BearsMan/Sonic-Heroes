using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AmyHammerAttack : MonoBehaviour
{
    #region Constants

    private const int MaxTargetResults = 32;
    private const float MinimumDuration = 0.05f;
    private const float MinimumDirectionMagnitude = 0.001f;

    #endregion

    #region Animator Hashes

    private static readonly int HammerAttackHash =
        Animator.StringToHash("Hammer Attack");

    #endregion

    #region Inspector

    [Header("References")]
    [SerializeField] private TeamActionController actionController;
    [SerializeField] private CharacterSwitch characterSwitch;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Transform attackOrigin;

    [Header("Input")]
    [SerializeField] private bool readPlayerInput = true;
    [SerializeField] private KeyCode attackKey = KeyCode.C;

    [Header("Hammer Attack")]
    [SerializeField, Min(0.1f)] private float attackRadius = 2.5f;
    [SerializeField, Min(0f)] private float damage = 40f;
    [SerializeField, Min(0f)] private float knockbackForce = 14f;
    [SerializeField, Min(MinimumDuration)] private float startupDuration = 0.15f;
    [SerializeField, Min(MinimumDuration)] private float activeDuration = 0.2f;
    [SerializeField, Min(MinimumDuration)] private float recoveryDuration = 0.35f;
    [SerializeField] private LayerMask enemyLayers = ~0;
    [SerializeField] private bool destroyBreakableTargets;

    [Header("Movement")]
    [SerializeField] private bool surrenderMovementControl = true;
    [SerializeField] private bool allowAirborneAttack;
    [SerializeField, Min(0f)] private float forwardLungeDistance = 1.25f;
    [SerializeField, Min(MinimumDuration)] private float forwardLungeDuration = 0.12f;

    [Header("Effects")]
    [SerializeField] private GameObject startupEffect;
    [SerializeField] private GameObject impactEffect;
    [SerializeField, Min(0f)] private float effectLifetime = 3f;

    [Header("Audio")]
    [SerializeField] private AudioClip startupSound;
    [SerializeField] private AudioClip impactSound;

    [Header("Debug")]
    [SerializeField] private bool drawAttackRadius = true;
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private readonly Collider[] targetResults =
        new Collider[MaxTargetResults];

    private readonly HashSet<GameObject> damagedTargets =
        new();

    private Coroutine attackRoutine;

    private bool isActive;
    private bool isInitialized;
    private bool isShuttingDown;

    #endregion

    #region Public API

    public void Cancel()
    {
        if (!isActive)
            return;

        FinishHammerAttack(
            restoreMovementControl: true);
    }

    public bool IsActive =>
        isActive;

    public bool IsInitialized =>
        isInitialized;

    public void SetInputEnabled(
        bool enabled)
    {
        readPlayerInput =
            enabled;
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
        if (!InitializeAmyHammerAttack())
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
            isActive)
        {
            return;
        }

        if (Input.GetKeyDown(
            attackKey))
        {
            TryActivate();
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
        attackRadius =
            Mathf.Max(
                0.1f,
                attackRadius);

        damage =
            Mathf.Max(
                0f,
                damage);

        knockbackForce =
            Mathf.Max(
                0f,
                knockbackForce);

        startupDuration =
            Mathf.Max(
                MinimumDuration,
                startupDuration);

        activeDuration =
            Mathf.Max(
                MinimumDuration,
                activeDuration);

        recoveryDuration =
            Mathf.Max(
                MinimumDuration,
                recoveryDuration);

        forwardLungeDistance =
            Mathf.Max(
                0f,
                forwardLungeDistance);

        forwardLungeDuration =
            Mathf.Max(
                MinimumDuration,
                forwardLungeDuration);

        effectLifetime =
            Mathf.Max(
                0f,
                effectLifetime);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawAttackRadius)
            return;

        Gizmos.DrawWireSphere(
            GetAttackPosition(),
            attackRadius);
    }

    #endregion

    #region Initialization

    public bool InitializeAmyHammerAttack()
    {
        if (isInitialized)
            return true;

        CacheComponents();
        ResolveReferences();

        if (!ValidateConfiguration())
        {
            Debug.LogError(
                $"AmyHammerAttack failed to initialize on '{name}'.",
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

        audioSource ??=
            GetComponentInParent<AudioSource>();
    }

    private void ResolveReferences()
    {
        animator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        attackOrigin ??=
            transform;
    }

    #endregion

    #region Hammer Attack State

    private bool IsAmySpeedCharacter()
    {
        if (TeamSetup.Instance == null)
        {
            return false;
        }

        Transform speedCharacter =
            actionController.SpeedCharacter;

        TeamComposition team =
            TeamSetup.Instance.Team;

        if (speedCharacter == null ||
            team == null ||
            team.SpeedCharacterPrefab == null)
        {
            return false;
        }

        string characterName =
            RemoveCloneSuffix(
                speedCharacter.name);

        string prefabName =
            RemoveCloneSuffix(
                team.SpeedCharacterPrefab.name);

        return
            string.Equals(
                characterName,
                prefabName,
                System.StringComparison.OrdinalIgnoreCase);
    }

    private static string RemoveCloneSuffix(
        string objectName)
    {
        if (string.IsNullOrWhiteSpace(
                objectName))
        {
            return string.Empty;
        }

        const string cloneSuffix =
            "(Clone)";

        return objectName
            .Replace(
                cloneSuffix,
                string.Empty)
            .Trim();
    }

    private bool CanActivate()
    {
        return
            isInitialized &&
            !isActive &&
            actionController != null &&
            characterSwitch != null &&
            TeamSetup.Instance != null &&
            TeamSetup.Instance.PlayableTeam ==
                PlayableTeam.TeamRose &&
            actionController.CurrentFormation ==
                TeamActionController.TeamFormation.Speed &&
            characterSwitch.CurrentLeaderType ==
                CHARACTERTYPES.Speed &&
            IsAmySpeedCharacter();
    }

    public bool TryActivate()
    {
        if (!CanActivate())
            return false;

        bool accepted =
            actionController.TryBeginAction(
                TeamActionController.TeamAction.AmyHammerAttack,
                TeamActionController.TeamFormation.Speed,
                mustBeGrounded: !allowAirborneAttack,
                mustBeAirborne: false,
                surrenderMovementControl: surrenderMovementControl);

        if (!accepted)
            return false;

        BeginHammerAttack();
        return true;
    }

    private void BeginHammerAttack()
    {
        isActive = true;
        damagedTargets.Clear();

        PlayAnimation();
        PlaySound(
            startupSound);

        SpawnEffect(
            startupEffect,
            GetAttackPosition());

        attackRoutine =
            StartCoroutine(
                HammerAttackRoutine());

        LogStateChange(
            "Amy Hammer Attack started.");
    }

    private IEnumerator HammerAttackRoutine()
    {
        yield return
            new WaitForSeconds(
                startupDuration);

        if (forwardLungeDistance > 0f &&
            forwardLungeDuration > 0f)
        {
            yield return
                LungeForward();
        }

        ApplyHammerImpact();

        yield return
            new WaitForSeconds(
                activeDuration);

        yield return
            new WaitForSeconds(
                recoveryDuration);

        FinishHammerAttack(
            restoreMovementControl: true);
    }

    private void FinishHammerAttack(
        bool restoreMovementControl)
    {
        if (!isActive)
            return;

        isActive = false;

        if (attackRoutine != null)
        {
            StopCoroutine(
                attackRoutine);

            attackRoutine = null;
        }

        damagedTargets.Clear();

        if (actionController != null &&
            actionController.CurrentAction ==
                TeamActionController.TeamAction.AmyHammerAttack)
        {
            actionController.EndAction(
                restoreMovementControl);
        }

        LogStateChange(
            "Amy Hammer Attack finished.");
    }

    private void ResetRuntimeState()
    {
        isActive = false;
        attackRoutine = null;
        damagedTargets.Clear();
    }

    #endregion

    #region Movement

    private IEnumerator LungeForward()
    {
        Transform amy =
            actionController.SpeedCharacter;

        if (amy == null)
            yield break;

        Vector3 direction =
            amy.forward;

        if (direction.sqrMagnitude <=
            MinimumDirectionMagnitude)
        {
            direction =
                transform.forward;
        }

        direction.Normalize();

        Vector3 startingPosition =
            amy.position;

        Vector3 targetPosition =
            startingPosition +
            direction *
            forwardLungeDistance;

        float elapsed = 0f;

        while (elapsed <
               forwardLungeDuration)
        {
            elapsed +=
                Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsed /
                    forwardLungeDuration);

            amy.position =
                Vector3.Lerp(
                    startingPosition,
                    targetPosition,
                    progress);

            yield return null;
        }
    }

    #endregion

    #region Damage

    private void ApplyHammerImpact()
    {
        Vector3 attackPosition =
            GetAttackPosition();

        SpawnEffect(
            impactEffect,
            attackPosition);

        PlaySound(
            impactSound);

        int resultCount =
            Physics.OverlapSphereNonAlloc(
                attackPosition,
                attackRadius,
                targetResults,
                enemyLayers,
                QueryTriggerInteraction.Collide);

        for (int index = 0;
             index < resultCount;
             index++)
        {
            Collider hitCollider =
                targetResults[index];

            targetResults[index] = null;

            if (hitCollider == null)
                continue;

            GameObject target =
                hitCollider.attachedRigidbody != null
                    ? hitCollider.attachedRigidbody.gameObject
                    : hitCollider.gameObject;

            if (IsTeamCharacter(target) ||
                !damagedTargets.Add(target))
            {
                continue;
            }

            target.SendMessage(
                "TakeDamage",
                damage,
                SendMessageOptions.DontRequireReceiver);

            ApplyKnockback(
                target,
                hitCollider.attachedRigidbody);

            if (destroyBreakableTargets)
            {
                target.SendMessage(
                    "Break",
                    SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    private void ApplyKnockback(
        GameObject target,
        Rigidbody targetRigidbody)
    {
        if (target == null ||
            targetRigidbody == null ||
            knockbackForce <= 0f)
        {
            return;
        }

        Vector3 direction =
            target.transform.position -
            GetAttackPosition();

        direction.y =
            Mathf.Max(
                direction.y,
                0.25f);

        if (direction.sqrMagnitude <=
            MinimumDirectionMagnitude)
        {
            direction =
                transform.forward;
        }

        targetRigidbody.AddForce(
            direction.normalized *
            knockbackForce,
            ForceMode.VelocityChange);
    }

    #endregion

    #region Team Filtering

    private bool IsTeamCharacter(
        GameObject target)
    {
        if (target == null ||
            actionController == null)
        {
            return false;
        }

        return
            MatchesCharacter(
                target,
                actionController.SpeedCharacter) ||
            MatchesCharacter(
                target,
                actionController.FlyCharacter) ||
            MatchesCharacter(
                target,
                actionController.PowerCharacter);
    }

    private static bool MatchesCharacter(
        GameObject target,
        Transform character)
    {
        return
            character != null &&
            (target == character.gameObject ||
             target.transform.IsChildOf(character));
    }

    #endregion

    #region Effects

    private void SpawnEffect(
        GameObject effectPrefab,
        Vector3 position)
    {
        if (effectPrefab == null)
            return;

        GameObject spawnedEffect =
            Instantiate(
                effectPrefab,
                position,
                transform.rotation);

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
            HammerAttackHash);
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

    #region Utility

    private Vector3 GetAttackPosition()
    {
        return
            attackOrigin != null
                ? attackOrigin.position
                : transform.position;
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

        if (characterSwitch == null)
        {
            Debug.LogWarning(
                "AmyHammerAttack could not find CharacterSwitch.",
                this);
        }

        if (animator == null)
        {
            Debug.LogWarning(
                "AmyHammerAttack could not find an Animator.",
                this);
        }

        if (audioSource == null)
        {
            Debug.LogWarning(
                "AmyHammerAttack could not find an AudioSource.",
                this);
        }

        if (startupEffect == null)
        {
            Debug.LogWarning(
                "AmyHammerAttack has no startup effect.",
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
            $"AmyHammerAttack requires {displayName}.",
            this);

        return false;
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(
                attackRoutine);

            attackRoutine = null;
        }

        if (isActive)
        {
            FinishHammerAttack(
                restoreMovementControl: true);
        }
    }

    private void CleanupDestroyedState()
    {
        CleanupRuntimeState();

        isInitialized = false;

        actionController = null;
        characterSwitch = null;
        animator = null;
        audioSource = null;
        attackOrigin = null;

        startupEffect = null;
        impactEffect = null;

        damagedTargets.Clear();
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
