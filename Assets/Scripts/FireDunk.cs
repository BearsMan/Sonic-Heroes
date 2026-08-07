using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class FireDunk : MonoBehaviour
{
    #region Animator Hashes

    private static readonly int FireDunkHash =
        Animator.StringToHash("Fire Dunk");

    #endregion

    #region Inspector

    [Header("References")]
    [SerializeField] private TeamActionController actionController;
    [SerializeField] private CharacterSwitch characterSwitch;
    [SerializeField] private UltimatePlayerMovement movement;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Animator fireDunkAnimator;
    [SerializeField] private AudioSource fireDunkAudioSource;

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
    private readonly HashSet<GameObject> damagedObjects = new();

    private Coroutine fireDunkRoutine;

    private bool isPerformingFireDunk;
    private bool isInitialized;
    private bool isShuttingDown;

    private bool previousUseGravity;
    private bool previousIsKinematic;
    private Vector3 previousLinearVelocity;
    private Vector3 previousAngularVelocity;

    #endregion

    #region Public API

    public bool IsPerformingFireDunk => isPerformingFireDunk;
    public bool IsInitialized => isInitialized;

    public bool InitializeFireDunk()
    {
        if (isInitialized)
            return true;

        ResolveAllReferences();

        if (!ValidateConfiguration())
        {
            AttemptAutomaticRepair();

            if (!ValidateConfiguration())
            {
                Debug.LogError(
                    $"FireDunk failed to initialize on '{name}'.",
                    this);

                isInitialized = false;
                return false;
            }
        }

        ResetRuntimeState();
        SetProjectileEffectsActive(false);

        isInitialized = true;
        return true;
    }

    public bool TryStartFireDunk()
    {
        if (!isInitialized &&
            !InitializeFireDunk())
        {
            return false;
        }

        ResolveDynamicReferences();

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
        {
            FinishFireDunk();
        }
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
        ResolveAllReferences();
        SetProjectileEffectsActive(false);
    }

    private IEnumerator Start()
    {
        yield return null;

        if (!InitializeFireDunk())
        {
            yield return null;
            InitializeFireDunk();
        }
    }

    private void OnEnable()
    {
        if (isShuttingDown)
            return;

        ResolveAllReferences();

        if (isInitialized)
        {
            SetProjectileEffectsActive(false);
        }
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
        {
            TryStartFireDunk();
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
        projectileSpeed =
            Mathf.Max(
                0.1f,
                projectileSpeed);

        downwardAmount =
            Mathf.Max(
                0f,
                downwardAmount);

        maximumTravelTime =
            Mathf.Max(
                0.1f,
                maximumTravelTime);

        launchSpacing =
            Mathf.Max(
                0f,
                launchSpacing);

        returnDelay =
            Mathf.Max(
                0f,
                returnDelay);

        projectileRadius =
            Mathf.Max(
                0.1f,
                projectileRadius);

        explosionRadius =
            Mathf.Max(
                0.1f,
                explosionRadius);

        damage =
            Mathf.Max(
                0f,
                damage);

        impactEffectLifetime =
            Mathf.Max(
                0f,
                impactEffectLifetime);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ResolveAllReferences();
        }
#endif
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawImpactRadius)
            return;

        Vector3 direction =
            GetLaunchDirection(0f);

        Vector3 previewPoint =
            transform.position +
            direction *
            projectileSpeed *
            maximumTravelTime;

        Gizmos.DrawLine(
            transform.position,
            previewPoint);

        Gizmos.DrawWireSphere(
            previewPoint,
            explosionRadius);
    }

    #endregion

    #region Reference Resolution

    private void ResolveAllReferences()
    {
        ResolveStaticReferences();
        ResolveDynamicReferences();
        ConfigurePhysicsReference();
    }

    private void ResolveStaticReferences()
    {
        actionController ??=
            GetComponent<TeamActionController>();

        actionController ??=
    GetComponentInChildren<TeamActionController>(
        includeInactive: true);

        actionController ??=
            GetComponentInParent<TeamActionController>();

        actionController ??=
            GetComponentInChildren<TeamActionController>(
                includeInactive: true);

        actionController ??=
            FindAnyObjectByType<TeamActionController>(
                FindObjectsInactive.Include);

        characterSwitch ??=
            GetComponent<CharacterSwitch>();

        characterSwitch ??=
            GetComponentInParent<CharacterSwitch>();

        characterSwitch ??=
            GetComponentInChildren<CharacterSwitch>(
                includeInactive: true);

        characterSwitch ??=
            FindAnyObjectByType<CharacterSwitch>(
                FindObjectsInactive.Include);

        playerRigidbody =
    GetComponent<Rigidbody>();

        if (playerRigidbody == null)
        {
            playerRigidbody =
                gameObject.AddComponent<Rigidbody>();
        }

        if (fireDunkAudioSource == null)
        {
            fireDunkAudioSource =
                GetComponent<AudioSource>();
        }

        if (fireDunkAudioSource == null)
        {
            fireDunkAudioSource =
                gameObject.AddComponent<AudioSource>();
        }

        if (fireDunkAudioSource != null)
        {
            fireDunkAudioSource.playOnAwake = false;
            fireDunkAudioSource.loop = false;
        }
    }

    private void ResolveDynamicReferences()
    {
        movement =
            ResolveLeaderMovement();

        Transform powerCharacter =
            ResolvePowerCharacter();

        if (powerCharacter != null)
        {
            fireDunkAnimator =
                powerCharacter.GetComponentInChildren<Animator>(
                    includeInactive: true);
        }

        if (fireDunkAnimator == null)
        {
            fireDunkAnimator =
                GetComponentInChildren<Animator>(
                    includeInactive: true);
        }

        if (fireDunkAnimator == null &&
            movement != null)
        {
            fireDunkAnimator =
                movement.GetComponentInChildren<Animator>(
                    includeInactive: true);
        }
    }

    private UltimatePlayerMovement ResolveLeaderMovement()
    {
        if (movement != null &&
            movement.gameObject.activeInHierarchy)
        {
            return movement;
        }

        UltimatePlayerMovement resolvedMovement =
            GetComponent<UltimatePlayerMovement>();

        resolvedMovement ??=
            GetComponentInParent<UltimatePlayerMovement>();

        if (characterSwitch != null)
        {
            Transform currentLeader =
                characterSwitch.CurrentLeader;

            if (currentLeader != null)
            {
                resolvedMovement ??=
                    currentLeader.GetComponent<UltimatePlayerMovement>();

                resolvedMovement ??=
                    currentLeader.GetComponentInParent<UltimatePlayerMovement>();
            }
        }

        resolvedMovement ??=
            FindAnyObjectByType<UltimatePlayerMovement>(
                FindObjectsInactive.Include);

        return resolvedMovement;
    }

    private Transform ResolvePowerCharacter()
    {
        if (characterSwitch != null &&
            characterSwitch.PowerCharacter != null)
        {
            return characterSwitch.PowerCharacter;
        }

        if (actionController != null &&
            actionController.PowerCharacter != null)
        {
            return actionController.PowerCharacter;
        }

        return null;
    }

    private void AttemptAutomaticRepair()
    {
        ResolveStaticReferences();

        if (playerRigidbody == null)
        {
            playerRigidbody =
                GetComponent<Rigidbody>();
        }

        if (playerRigidbody == null)
        {
            playerRigidbody =
                gameObject.AddComponent<Rigidbody>();
        }

        ResolveDynamicReferences();
        ConfigurePhysicsReference();
    }

    private void ConfigurePhysicsReference()
    {
        if (playerRigidbody == null)
            return;

        playerRigidbody.interpolation =
            RigidbodyInterpolation.Interpolate;

        playerRigidbody.collisionDetectionMode =
            playerRigidbody.isKinematic
                ? CollisionDetectionMode.ContinuousSpeculative
                : CollisionDetectionMode.ContinuousDynamic;

        playerRigidbody.constraints |=
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationY |
            RigidbodyConstraints.FreezeRotationZ;

        playerRigidbody.detectCollisions = true;
    }

    #endregion

    #region Fire Dunk State

    private bool CanStartFireDunk()
    {
        return
            isInitialized &&
            !isPerformingFireDunk &&
            actionController != null &&
            HasAvailableTeammate();
    }

    private bool HasAvailableTeammate()
    {
        if (actionController == null)
            return false;

        return
            actionController.SpeedCharacter != null ||
            actionController.FlyCharacter != null;
    }

    private void BeginFireDunk()
    {
        isPerformingFireDunk = true;

        CachePhysicsState();
        StopHostMotion();

        actionController.DisableFollowers();

        PlayAnimation();
        PlaySound(launchSound);

        fireDunkRoutine =
            StartCoroutine(
                FireDunkRoutine());

        LogStateChange(
            "Fire Dunk started.");
    }

    private void FinishFireDunk()
    {
        if (!isPerformingFireDunk)
            return;

        isPerformingFireDunk = false;

        if (fireDunkRoutine != null)
        {
            StopCoroutine(
                fireDunkRoutine);

            fireDunkRoutine = null;
        }

        RestoreProjectiles();
        RestorePhysicsState();

        if (actionController != null)
        {
            actionController.EnableFollowers();

            if (actionController.CurrentAction ==
                TeamActionController.TeamAction.FireDunk)
            {
                actionController.EndAction(
                    restoreMovementControl: true);
            }
        }
        else
        {
            movement?.EnableMovement();
        }

        PlaySound(finishSound);

        LogStateChange(
            "Fire Dunk finished.");
    }

    private void ResetRuntimeState()
    {
        isPerformingFireDunk = false;
        fireDunkRoutine = null;

        activeProjectiles.Clear();
        damagedObjects.Clear();
    }

    private void CachePhysicsState()
    {
        if (playerRigidbody == null)
            return;

        previousUseGravity =
            playerRigidbody.useGravity;

        previousIsKinematic =
            playerRigidbody.isKinematic;

        previousLinearVelocity =
            playerRigidbody.linearVelocity;

        previousAngularVelocity =
            playerRigidbody.angularVelocity;
    }

    private void StopHostMotion()
    {
        if (playerRigidbody == null)
            return;

        if (!playerRigidbody.isKinematic)
        {
            playerRigidbody.linearVelocity =
                Vector3.zero;

            playerRigidbody.angularVelocity =
                Vector3.zero;

            playerRigidbody.useGravity = false;
        }
    }

    private void RestorePhysicsState()
    {
        if (playerRigidbody == null)
            return;

        playerRigidbody.isKinematic =
            previousIsKinematic;

        playerRigidbody.useGravity =
            previousUseGravity;

        if (!playerRigidbody.isKinematic)
        {
            playerRigidbody.linearVelocity =
                previousLinearVelocity;

            playerRigidbody.angularVelocity =
                previousAngularVelocity;

            playerRigidbody.WakeUp();
        }
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
            PositionProjectileForLaunch(
                activeProjectiles[index]);

            if (index < activeProjectiles.Count - 1 &&
                launchSpacing > 0f)
            {
                yield return new WaitForSeconds(
                    launchSpacing);
            }
        }

        float elapsed = 0f;
        WaitForFixedUpdate fixedUpdate = new();

        while (elapsed < maximumTravelTime &&
               HasActiveProjectile())
        {
            elapsed +=
                Time.fixedDeltaTime;

            for (int index = 0;
                 index < activeProjectiles.Count;
                 index++)
            {
                ProjectileState projectile =
                    activeProjectiles[index];

                if (!projectile.Finished)
                {
                    MoveProjectile(
                        projectile);
                }
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
        {
            yield return new WaitForSeconds(
                returnDelay);
        }

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
            IsHostObject(character))
        {
            return;
        }

        ProjectileState projectile =
            CreateProjectile(
                character,
                sideOffset,
                effect);

        if (projectile != null)
        {
            activeProjectiles.Add(
                projectile);
        }
    }

    private ProjectileState CreateProjectile(
        Transform character,
        float sideOffset,
        GameObject effect)
    {
        if (character == null)
            return null;

        return new ProjectileState
        {
            Transform = character,
            OriginalParent = character.parent,
            OriginalSiblingIndex = character.GetSiblingIndex(),
            OriginalLocalPosition = character.localPosition,
            OriginalLocalRotation = character.localRotation,
            OriginalLocalScale = character.localScale,
            Direction = GetLaunchDirection(sideOffset),
            SideOffset = sideOffset,
            Effect = effect,
            Finished = false
        };
    }

    private Vector3 GetLaunchDirection(
        float sideOffset)
    {
        Vector3 direction =
            transform.forward +
            transform.right *
            sideOffset *
            0.15f +
            Vector3.down *
            downwardAmount;

        return direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector3.down;
    }

    private void PositionProjectileForLaunch(
        ProjectileState projectile)
    {
        if (projectile?.Transform == null)
            return;

        projectile.Transform.SetParent(
            null,
            worldPositionStays: true);

        projectile.Transform.position =
            transform.position +
            Vector3.up *
            launchHeightOffset +
            transform.right *
            projectile.SideOffset;

        if (projectile.Direction.sqrMagnitude > 0.0001f)
        {
            projectile.Transform.rotation =
                Quaternion.LookRotation(
                    projectile.Direction,
                    Vector3.up);
        }

        ConfigureProjectileEffect(
            projectile);
    }

    private void ConfigureProjectileEffect(
        ProjectileState projectile)
    {
        if (projectile?.Effect == null ||
            projectile.Transform == null)
        {
            return;
        }

        projectile.Effect.transform.SetParent(
            projectile.Transform,
            worldPositionStays: false);

        projectile.Effect.transform.localPosition =
            Vector3.zero;

        projectile.Effect.transform.localRotation =
            Quaternion.identity;

        projectile.Effect.SetActive(true);
    }

    private void MoveProjectile(
        ProjectileState projectile)
    {
        if (projectile?.Transform == null)
        {
            if (projectile != null)
            {
                projectile.Finished = true;
            }

            return;
        }

        Vector3 currentPosition =
            projectile.Transform.position;

        float distance =
            projectileSpeed *
            Time.fixedDeltaTime;

        if (Physics.SphereCast(
                currentPosition,
                projectileRadius,
                projectile.Direction,
                out RaycastHit hit,
                distance,
                hitLayers,
                QueryTriggerInteraction.Ignore))
        {
            if (IsTeamCollider(hit.collider))
            {
                projectile.Transform.position =
                    currentPosition +
                    projectile.Direction *
                    distance;

                return;
            }

            projectile.Transform.position =
                hit.point;

            ImpactProjectile(
                projectile,
                hit.point);

            return;
        }

        projectile.Transform.position =
            currentPosition +
            projectile.Direction *
            distance;
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

        DamageArea(
            impactPosition);

        SpawnImpactEffect(
            impactPosition);

        if (projectile.Effect != null)
        {
            projectile.Effect.SetActive(false);
        }

        PlaySound(impactSound);
    }

    private bool HasActiveProjectile()
    {
        for (int index = 0;
             index < activeProjectiles.Count;
             index++)
        {
            ProjectileState projectile =
                activeProjectiles[index];

            if (projectile != null &&
                !projectile.Finished)
            {
                return true;
            }
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
            {
                projectile.Effect.SetActive(false);
            }

            if (projectile.OriginalParent != null)
            {
                projectile.Transform.SetParent(
                    projectile.OriginalParent,
                    worldPositionStays: false);

                int maximumSiblingIndex =
                    Mathf.Max(
                        0,
                        projectile.OriginalParent.childCount - 1);

                projectile.Transform.SetSiblingIndex(
                    Mathf.Clamp(
                        projectile.OriginalSiblingIndex,
                        0,
                        maximumSiblingIndex));

                projectile.Transform.localPosition =
                    projectile.OriginalLocalPosition;

                projectile.Transform.localRotation =
                    projectile.OriginalLocalRotation;

                projectile.Transform.localScale =
                    projectile.OriginalLocalScale;
            }
        }

        activeProjectiles.Clear();
    }

    #endregion

    #region Damage

    private void DamageArea(
        Vector3 impactPosition)
    {
        Collider[] hits =
            Physics.OverlapSphere(
                impactPosition,
                explosionRadius,
                damageLayers,
                QueryTriggerInteraction.Collide);

        damagedObjects.Clear();

        foreach (Collider hit in hits)
        {
            if (hit == null ||
                IsTeamCollider(hit))
            {
                continue;
            }

            GameObject target =
                hit.attachedRigidbody != null
                    ? hit.attachedRigidbody.gameObject
                    : hit.gameObject;

            if (target == null ||
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

    private bool IsTeamCollider(
        Collider candidate)
    {
        if (candidate == null)
            return false;

        return IsTeamTransform(
            candidate.transform);
    }

    private bool IsTeamTransform(
        Transform candidate)
    {
        if (candidate == null)
            return false;

        if (candidate == transform ||
            candidate.IsChildOf(transform) ||
            transform.IsChildOf(candidate))
        {
            return true;
        }

        if (actionController == null)
            return false;

        return
            MatchesCharacter(
                candidate,
                actionController.SpeedCharacter) ||
            MatchesCharacter(
                candidate,
                actionController.FlyCharacter) ||
            MatchesCharacter(
                candidate,
                actionController.PowerCharacter);
    }

    private bool IsHostObject(
        Transform candidate)
    {
        return
            candidate == null ||
            candidate == transform ||
            candidate.IsChildOf(transform) ||
            transform.IsChildOf(candidate);
    }

    private static bool MatchesCharacter(
        Transform candidate,
        Transform character)
    {
        if (candidate == null ||
            character == null)
        {
            return false;
        }

        return
            candidate == character ||
            candidate.IsChildOf(character) ||
            character.IsChildOf(candidate);
    }

    #endregion

    #region Effects

    private void SpawnImpactEffect(
        Vector3 impactPosition)
    {
        if (impactEffect == null)
            return;

        GameObject spawnedEffect =
            Instantiate(
                impactEffect,
                impactPosition,
                Quaternion.identity);

        if (impactEffectLifetime > 0f)
        {
            Destroy(
                spawnedEffect,
                impactEffectLifetime);
        }
    }

    private void SetProjectileEffectsActive(
        bool active)
    {
        if (speedProjectileEffect != null)
        {
            speedProjectileEffect.SetActive(active);
        }

        if (flyProjectileEffect != null)
        {
            flyProjectileEffect.SetActive(active);
        }
    }

    #endregion

    #region Animation And Audio

    private void PlayAnimation()
    {
        ResolveDynamicReferences();

        if (fireDunkAnimator != null)
        {
            fireDunkAnimator.SetTrigger(
                FireDunkHash);
        }
    }

    private void PlaySound(
        AudioClip clip)
    {
        if (fireDunkAudioSource == null ||
            clip == null)
        {
            return;
        }

        fireDunkAudioSource.PlayOneShot(
            clip);
    }

    #endregion

    #region Validation

    private bool ValidateConfiguration()
    {
        bool valid = true;

        if (actionController == null)
        {
            Debug.LogError(
                "FireDunk requires TeamActionController.",
                this);

            valid = false;
        }

        if (characterSwitch == null)
        {
            Debug.LogWarning(
                "FireDunk could not find CharacterSwitch. " +
                "The Power character Animator will use fallback resolution.",
                this);
        }

        if (playerRigidbody == null)
        {
            Debug.LogError(
                "FireDunk could not create or resolve its Rigidbody.",
                this);

            valid = false;
        }

        if (fireDunkAnimator == null)
        {
            Debug.LogWarning(
                "FireDunk could not find the Power character Animator. The attack will continue without animation.",
                this);
        }

        if (fireDunkAudioSource == null)
        {
            Debug.LogWarning(
                "FireDunk could not create or resolve an AudioSource. The attack will continue without audio.",
                this);
        }

        return valid;
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        if (isPerformingFireDunk)
        {
            FinishFireDunk();
        }
        else
        {
            RestoreProjectiles();
            SetProjectileEffectsActive(false);
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
        fireDunkAnimator = null;
        fireDunkAudioSource = null;

        speedProjectileEffect = null;
        flyProjectileEffect = null;
        impactEffect = null;

        damagedObjects.Clear();
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

    #region Internal Types

    private sealed class ProjectileState
    {
        public Transform Transform;
        public Transform OriginalParent;
        public int OriginalSiblingIndex;
        public Vector3 OriginalLocalPosition;
        public Quaternion OriginalLocalRotation;
        public Vector3 OriginalLocalScale;
        public Vector3 Direction;
        public float SideOffset;
        public GameObject Effect;
        public bool Finished;
    }

    #endregion
}
