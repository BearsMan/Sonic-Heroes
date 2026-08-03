using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FireDunk : MonoBehaviour
{
    #region Animator Hashes

    private static readonly int FireDunkHash =
        Animator.StringToHash("Fire Dunk");

    #endregion

    #region Inspector

    [Header("References")]
    [SerializeField] private TeamActionController actionController;
    [SerializeField] private UltimatePlayerMovement movement;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Animator fireDunkAnimator;
    [SerializeField] private AudioSource fireDunkAudioSource;
    [SerializeField] private CharacterSwitch characterSwitch;

    [Header("Input")]
    [SerializeField] private KeyCode fireDunkKey = KeyCode.B;
    [SerializeField] private bool readPlayerInput = true;

    [Header("Projectile Movement")]
    [SerializeField, Min(0.1f)] private float projectileSpeed = 35f;
    [SerializeField, Min(0f)] private float downwardAmount = 0.75f;
    [SerializeField, Min(0.1f)] private float maximumTravelTime = 1.5f;
    [SerializeField, Min(0f)] private float launchSpacing = 0.2f;
    [SerializeField, Min(0f)] private float returnDelay = 0.15f;
    [SerializeField] private float launchHeightOffset = 0.5f;
    [SerializeField] private float launchSideOffset = 0.75f;

    [Header("Impact")]
    [SerializeField, Min(0.1f)] private float projectileRadius = 0.5f;
    [SerializeField, Min(0.1f)] private float explosionRadius = 3f;
    [SerializeField, Min(0f)] private float damage = 25f;
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private LayerMask damageLayers = ~0;

    [Header("Effects")]
    [SerializeField] private GameObject speedProjectileEffect;
    [SerializeField] private GameObject flyProjectileEffect;
    [SerializeField] private GameObject impactEffect;
    [SerializeField, Min(0f)] private float impactEffectLifetime = 3f;

    [Header("Audio")]
    [SerializeField] private AudioClip launchSound;
    [SerializeField] private AudioClip impactSound;
    [SerializeField] private AudioClip finishSound;

    [Header("Debug")]
    [SerializeField] private bool drawImpactRadius = true;
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private readonly List<ProjectileState> activeProjectiles = new();

    private Coroutine fireDunkRoutine;
    private bool isPerformingFireDunk;
    private bool isInitialized;
    private bool isShuttingDown;
    private bool previousGravityState;

    #endregion

    #region Public API

    public bool IsPerformingFireDunk => isPerformingFireDunk;
    public bool IsInitialized => isInitialized;

    public bool TryStartFireDunk()
    {
        if (!CanStartFireDunk())
            return false;

        bool accepted =
            actionController.TryBeginAction(
                TeamActionController.TeamAction.FireDunk,
                TeamActionController.TeamFormation.Power,
                mustBeGrounded: false,
                mustBeAirborne: true,
                surrenderMovementControl: true);

        if (!accepted)
            return false;

        BeginFireDunk();
        return true;
    }

    public void CancelFireDunk()
    {
        if (isPerformingFireDunk)
            FinishFireDunk();
    }

    public void SetInputEnabled(bool enabled)
    {
        readPlayerInput = enabled;
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheComponents();
        ResolveReferences();
        SetProjectileEffectsActive(false);
    }

    private void Start()
    {
        if (!InitializeFireDunk())
            enabled = false;
    }

    private void OnEnable()
    {
        if (isShuttingDown)
            return;

        CacheComponents();
        ResolveReferences();

        if (isInitialized)
            SetProjectileEffectsActive(false);
    }

    private void Update()
    {
        if (!isInitialized ||
            !readPlayerInput ||
            isPerformingFireDunk)
        {
            return;
        }

        if (Input.GetKeyDown(fireDunkKey))
            TryStartFireDunk();
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
        projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
        downwardAmount = Mathf.Max(0f, downwardAmount);
        maximumTravelTime = Mathf.Max(0.1f, maximumTravelTime);
        launchSpacing = Mathf.Max(0f, launchSpacing);
        returnDelay = Mathf.Max(0f, returnDelay);
        projectileRadius = Mathf.Max(0.1f, projectileRadius);
        explosionRadius = Mathf.Max(0.1f, explosionRadius);
        damage = Mathf.Max(0f, damage);
        impactEffectLifetime = Mathf.Max(0f, impactEffectLifetime);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawImpactRadius)
            return;

        Vector3 direction =
            (transform.forward +
             Vector3.down * downwardAmount)
            .normalized;

        Vector3 previewPoint =
            transform.position +
            direction *
            projectileSpeed *
            maximumTravelTime;

        Gizmos.DrawLine(transform.position, previewPoint);
        Gizmos.DrawWireSphere(previewPoint, explosionRadius);
    }

    #endregion

    #region Initialization

    public bool InitializeFireDunk()
    {
        if (isInitialized)
            return true;

        CacheComponents();
        ResolveReferences();

        if (!ValidateConfiguration())
        {
            Debug.LogError(
                $"FireDunk failed to initialize on '{name}'.",
                this);

            isInitialized = false;
            return false;
        }

        ResetRuntimeState();
        SetProjectileEffectsActive(false);

        isInitialized = true;
        return true;
    }

    private void CacheComponents()
    {
        actionController ??=
            GetComponentInParent<TeamActionController>();

        movement ??=
            GetComponentInParent<UltimatePlayerMovement>();

        playerRigidbody ??=
            GetComponentInParent<Rigidbody>();

        fireDunkAudioSource ??=
            GetComponentInParent<AudioSource>();

        characterSwitch ??=
            GetComponentInParent<CharacterSwitch>();
    }

    private void ResolveReferences()
    {
        fireDunkAnimator ??=
            GetComponentInChildren<Animator>(includeInactive: true);

        if (fireDunkAnimator == null &&
            movement != null)
        {
            fireDunkAnimator =
                movement.GetComponentInChildren<Animator>(includeInactive: true);
        }
    }

    #endregion

    #region Fire Dunk State

    private bool CanUseFireDunk()
    {
        return
            TeamSetup.Instance != null &&
            TeamSetup.Instance.PlayableTeam == PlayableTeam.TeamSonic &&
            characterSwitch != null &&
            characterSwitch.CurrentLeaderType == CHARACTERTYPES.Power &&
            actionController != null &&
            actionController.CurrentFormation ==
                TeamActionController.TeamFormation.Power;
    }

    private bool CanStartFireDunk()
    {
        return
            CanUseFireDunk() &&
            isInitialized &&
            !isPerformingFireDunk &&
            playerRigidbody != null &&
            HasAvailableTeammate();
    }

    private bool HasAvailableTeammate()
    {
        return
            actionController.SpeedCharacter != null ||
            actionController.FlyCharacter != null;
    }

    private void BeginFireDunk()
    {
        isPerformingFireDunk = true;
        previousGravityState = playerRigidbody.useGravity;

        playerRigidbody.linearVelocity = Vector3.zero;
        playerRigidbody.angularVelocity = Vector3.zero;
        playerRigidbody.useGravity = false;

        actionController.DisableFollowers();

        PlayAnimation();
        PlaySound(launchSound);

        fireDunkRoutine =
            StartCoroutine(FireDunkRoutine());

        LogStateChange("Fire Dunk started.");
    }

    private void FinishFireDunk()
    {
        if (!isPerformingFireDunk)
            return;

        isPerformingFireDunk = false;

        if (fireDunkRoutine != null)
        {
            StopCoroutine(fireDunkRoutine);
            fireDunkRoutine = null;
        }

        RestoreProjectiles();

        if (playerRigidbody != null)
            playerRigidbody.useGravity = previousGravityState;

        if (actionController != null)
        {
            actionController.EnableFollowers();

            if (actionController.CurrentAction ==
                TeamActionController.TeamAction.FireDunk)
            {
                actionController.EndAction(restoreMovementControl: true);
            }
        }

        PlaySound(finishSound);
        LogStateChange("Fire Dunk finished.");
    }

    private void ResetRuntimeState()
    {
        isPerformingFireDunk = false;
        previousGravityState = true;
        fireDunkRoutine = null;
        activeProjectiles.Clear();
    }

    #endregion

    #region Fire Dunk Routine

    private IEnumerator FireDunkRoutine()
    {
        activeProjectiles.Clear();

        AddProjectileIfAvailable(
            actionController.SpeedCharacter,
            -launchSideOffset,
            speedProjectileEffect);

        AddProjectileIfAvailable(
            actionController.FlyCharacter,
            launchSideOffset,
            flyProjectileEffect);

        if (activeProjectiles.Count == 0)
        {
            FinishFireDunk();
            yield break;
        }

        for (int index = 0;
             index < activeProjectiles.Count;
             index++)
        {
            PositionProjectileForLaunch(activeProjectiles[index]);

            if (index < activeProjectiles.Count - 1 &&
                launchSpacing > 0f)
            {
                yield return new WaitForSeconds(launchSpacing);
            }
        }

        float elapsed = 0f;
        WaitForFixedUpdate fixedUpdate = new();

        while (elapsed < maximumTravelTime &&
               HasActiveProjectile())
        {
            elapsed += Time.fixedDeltaTime;

            for (int index = 0;
                 index < activeProjectiles.Count;
                 index++)
            {
                ProjectileState projectile =
                    activeProjectiles[index];

                if (!projectile.Finished)
                    MoveProjectile(projectile);
            }

            yield return fixedUpdate;
        }

        for (int index = 0;
             index < activeProjectiles.Count;
             index++)
        {
            ProjectileState projectile =
                activeProjectiles[index];

            if (!projectile.Finished &&
                projectile.Transform != null)
            {
                ImpactProjectile(
                    projectile,
                    projectile.Transform.position);
            }
        }

        if (returnDelay > 0f)
            yield return new WaitForSeconds(returnDelay);

        FinishFireDunk();
    }

    #endregion

    #region Projectile Management

    private void AddProjectileIfAvailable(
        Transform character,
        float sideOffset,
        GameObject effect)
    {
        if (character == null ||
            character.gameObject == gameObject)
        {
            return;
        }

        activeProjectiles.Add(
            CreateProjectile(
                character,
                sideOffset,
                effect));
    }

    private ProjectileState CreateProjectile(
        Transform character,
        float sideOffset,
        GameObject effect)
    {
        return new ProjectileState
        {
            Transform = character,
            OriginalParent = character.parent,
            OriginalLocalPosition = character.localPosition,
            OriginalLocalRotation = character.localRotation,
            Direction = GetLaunchDirection(sideOffset),
            SideOffset = sideOffset,
            Effect = effect,
            Finished = false
        };
    }

    private Vector3 GetLaunchDirection(float sideOffset)
    {
        Vector3 direction =
            transform.forward +
            transform.right * sideOffset * 0.15f +
            Vector3.down * downwardAmount;

        return direction.normalized;
    }

    private void PositionProjectileForLaunch(
        ProjectileState projectile)
    {
        if (projectile?.Transform == null)
            return;

        projectile.Transform.SetParent(null, worldPositionStays: true);

        projectile.Transform.position =
            transform.position +
            Vector3.up * launchHeightOffset +
            transform.right * projectile.SideOffset;

        if (projectile.Direction.sqrMagnitude > 0.0001f)
        {
            projectile.Transform.rotation =
                Quaternion.LookRotation(
                    projectile.Direction,
                    Vector3.up);
        }

        if (projectile.Effect != null)
        {
            projectile.Effect.transform.SetParent(
                projectile.Transform,
                worldPositionStays: false);

            projectile.Effect.transform.localPosition = Vector3.zero;
            projectile.Effect.SetActive(true);
        }
    }

    private void MoveProjectile(ProjectileState projectile)
    {
        if (projectile?.Transform == null)
        {
            if (projectile != null)
                projectile.Finished = true;

            return;
        }

        Vector3 currentPosition =
            projectile.Transform.position;

        float distance =
            projectileSpeed * Time.fixedDeltaTime;

        if (Physics.SphereCast(
                currentPosition,
                projectileRadius,
                projectile.Direction,
                out RaycastHit hit,
                distance,
                hitLayers,
                QueryTriggerInteraction.Ignore))
        {
            projectile.Transform.position = hit.point;
            ImpactProjectile(projectile, hit.point);
            return;
        }

        projectile.Transform.position =
            currentPosition +
            projectile.Direction * distance;
    }

    private void ImpactProjectile(
        ProjectileState projectile,
        Vector3 impactPosition)
    {
        if (projectile == null ||
            projectile.Finished)
        {
            return;
        }

        projectile.Finished = true;

        DamageArea(impactPosition);
        SpawnImpactEffect(impactPosition);

        if (projectile.Effect != null)
            projectile.Effect.SetActive(false);

        PlaySound(impactSound);
    }

    private bool HasActiveProjectile()
    {
        for (int index = 0;
             index < activeProjectiles.Count;
             index++)
        {
            if (!activeProjectiles[index].Finished)
                return true;
        }

        return false;
    }

    private void RestoreProjectiles()
    {
        for (int index = 0;
             index < activeProjectiles.Count;
             index++)
        {
            ProjectileState projectile =
                activeProjectiles[index];

            if (projectile?.Transform == null)
                continue;

            if (projectile.Effect != null)
                projectile.Effect.SetActive(false);

            projectile.Transform.SetParent(
                projectile.OriginalParent,
                worldPositionStays: false);

            projectile.Transform.localPosition =
                projectile.OriginalLocalPosition;

            projectile.Transform.localRotation =
                projectile.OriginalLocalRotation;
        }

        activeProjectiles.Clear();
    }

    #endregion

    #region Damage

    private void DamageArea(Vector3 impactPosition)
    {
        Collider[] hits =
            Physics.OverlapSphere(
                impactPosition,
                explosionRadius,
                damageLayers,
                QueryTriggerInteraction.Collide);

        HashSet<GameObject> damagedObjects = new();

        foreach (Collider hit in hits)
        {
            if (hit == null)
                continue;

            GameObject target =
                hit.attachedRigidbody != null
                    ? hit.attachedRigidbody.gameObject
                    : hit.gameObject;

            if (target == gameObject ||
                IsTeamCharacter(target) ||
                !damagedObjects.Add(target))
            {
                continue;
            }

            target.SendMessage(
                "TakeDamage",
                damage,
                SendMessageOptions.DontRequireReceiver);

            target.SendMessage(
                "Break",
                SendMessageOptions.DontRequireReceiver);
        }
    }

    private bool IsTeamCharacter(GameObject target)
    {
        if (target == null ||
            actionController == null)
        {
            return false;
        }

        return
            MatchesCharacter(target, actionController.SpeedCharacter) ||
            MatchesCharacter(target, actionController.FlyCharacter) ||
            MatchesCharacter(target, actionController.PowerCharacter) ||
            target.transform.IsChildOf(transform);
    }

    private static bool MatchesCharacter(
        GameObject target,
        Transform character)
    {
        return
            character != null &&
            target == character.gameObject;
    }

    #endregion

    #region Effects

    private void SpawnImpactEffect(Vector3 impactPosition)
    {
        if (impactEffect == null)
            return;

        GameObject spawnedEffect =
            Instantiate(
                impactEffect,
                impactPosition,
                Quaternion.identity);

        if (impactEffectLifetime > 0f)
            Destroy(spawnedEffect, impactEffectLifetime);
    }

    private void SetProjectileEffectsActive(bool active)
    {
        if (speedProjectileEffect != null)
            speedProjectileEffect.SetActive(active);

        if (flyProjectileEffect != null)
            flyProjectileEffect.SetActive(active);
    }

    #endregion

    #region Animation

    private void PlayAnimation()
    {
        if (fireDunkAnimator != null)
            fireDunkAnimator.SetTrigger(FireDunkHash);
    }

    #endregion

    #region Audio

    private void PlaySound(AudioClip clip)
    {
        if (fireDunkAudioSource == null ||
            clip == null)
        {
            return;
        }

        fireDunkAudioSource.PlayOneShot(clip);
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
                playerRigidbody,
                nameof(Rigidbody));

        valid &=
            ValidateReference(
                characterSwitch,
                nameof(CharacterSwitch));

        if (movement == null)
        {
            Debug.LogWarning(
                "FireDunk could not find UltimatePlayerMovement.",
                this);
        }

        if (fireDunkAnimator == null)
        {
            Debug.LogWarning(
                "FireDunk could not find an Animator.",
                this);
        }

        if (fireDunkAudioSource == null)
        {
            Debug.LogWarning(
                "FireDunk could not find an AudioSource.",
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
            $"FireDunk requires {displayName}.",
            this);

        return false;
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        if (isPerformingFireDunk)
            FinishFireDunk();
        else
            SetProjectileEffectsActive(false);
    }

    private void CleanupDestroyedState()
    {
        CleanupRuntimeState();

        isInitialized = false;

        actionController = null;
        movement = null;
        playerRigidbody = null;
        fireDunkAnimator = null;
        fireDunkAudioSource = null;

        speedProjectileEffect = null;
        flyProjectileEffect = null;
        impactEffect = null;
        characterSwitch = null;
    }

    #endregion

    #region Debug

    private void LogStateChange(string message)
    {
        if (!logStateChanges)
            return;

        Debug.Log(message, this);
    }

    #endregion

    #region Internal Types

    private sealed class ProjectileState
    {
        public Transform Transform;
        public Transform OriginalParent;
        public Vector3 OriginalLocalPosition;
        public Quaternion OriginalLocalRotation;
        public Vector3 Direction;
        public float SideOffset;
        public GameObject Effect;
        public bool Finished;
    }

    #endregion
}
