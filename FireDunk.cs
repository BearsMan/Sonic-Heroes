using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Power Formation aerial attack.
/// Launches the Speed and Fly teammates downward as fiery projectiles.
/// </summary>
public class FireDunk : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TeamActionController actionController;
    [SerializeField] private UltimatePlayerMovement movement;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Animator fireDunkAnimator;
    [SerializeField] private AudioSource fireDunkAudioSource;

    [Header("Input")]
    [SerializeField] private KeyCode fireDunkKey = KeyCode.B;

    [Header("Projectile Movement")]
    [SerializeField, Min(0.1f)] private float projectileSpeed = 35f;
    [SerializeField, Min(0f)] private float downwardAmount = 0.75f;
    [SerializeField, Min(0.1f)] private float maximumTravelTime = 1.5f;
    [SerializeField, Min(0f)] private float launchSpacing = 0.2f;
    [SerializeField, Min(0f)] private float returnDelay = 0.15f;
    [SerializeField] private float launchHeightOffset = 0.5f;
    [SerializeField] private float launchSideOffSet = 0.75f;

    [Header("Impact")]
    [SerializeField, Min(0.1f)] private float projectileRadius = 0.5f;
    [SerializeField, Min(0.1f)] private float explosionRadius = 3f;
    [SerializeField, Min(0f)] private float damage = 25f;
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private LayerMask damageLayers = ~0;

    [Header("Animation")]
    [SerializeField] private string fireDunkTrigger = "Fire Dunk";

    [Header("Effects")]
    [SerializeField] private GameObject speedProjectileEffect;
    [SerializeField] private GameObject flyProjectileEffect;
    [SerializeField] private GameObject impactEffect;

    [Header("Audio")]
    [SerializeField] private AudioClip launchSound;
    [SerializeField] private AudioClip impactSound;
    [SerializeField] private AudioClip finishSound;

    [Header("Debug")]
    [SerializeField] private bool drawImpactRadius = true;
    [SerializeField] private bool logStateChanges;

    private readonly List<ProjectileState> activeProjectiles = new();
    private Coroutine fireDunkCoroutine;
    private bool isPerformingFireDunk;
    private bool previousGravityState;
    private bool movementDisabledDirectly;

    public bool IsPerformingFireDunk => isPerformingFireDunk;

    private sealed class ProjectileState
    {
        public GameObject Character;
        public Transform Transform;
        public Transform OriginalParent;
        public Vector3 OriginalLocalPosition;
        public Quaternion OriginalLocalRotation;
        public Vector3 Direction;
        public float sideOffSet;
        public GameObject Effect;
        public bool Finished;
    }

    private void Awake()
    {
        ResolveReferences();
        SetEffectsActive(false);
    }

    private void Update()
    {
        if (isPerformingFireDunk)
            return;
        if (Input.GetKeyDown(fireDunkKey))
            TryStartFireDunk();
    }

    private void ResolveReferences()
    {
        if (actionController == null)
            actionController = GetComponent<TeamActionController>();
        if (actionController == null)
            actionController = GetComponentInParent<TeamActionController>();
        if (movement == null)
            movement = GetComponent<UltimatePlayerMovement>();
        if (movement == null)
            movement = GetComponentInParent<UltimatePlayerMovement>();
        if (playerRigidbody == null)
            playerRigidbody = GetComponent<Rigidbody>();
        if (playerRigidbody == null && movement != null)
            playerRigidbody = movement.GetComponent<Rigidbody>();
        if (fireDunkAnimator == null)
            fireDunkAnimator = GetComponentInChildren<Animator>();
        if (fireDunkAudioSource == null)
            fireDunkAudioSource = GetComponent<AudioSource>();
    }

    public bool TryStartFireDunk()
    {
        if (isPerformingFireDunk)
            return false;
        ResolveReferences();
        if (actionController == null || playerRigidbody == null)
            return false;
        if (!HasAvailableTeammate())
            return false;
        bool accepted = actionController.TryBeginAction(
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

    private bool HasAvailableTeammate()
    {
        if (actionController == null)
            return false;
        return actionController.SpeedCharacter != null ||
               actionController.FlyCharacter != null;
    }

    private void BeginFireDunk()
    {
        isPerformingFireDunk = true;
        movementDisabledDirectly = false;
        previousGravityState = playerRigidbody.useGravity;
        playerRigidbody.linearVelocity = Vector3.zero;
        playerRigidbody.useGravity = false;
        actionController.DisableFollowers();
        PlayAnimation();
        PlaySound(launchSound);
        fireDunkCoroutine = StartCoroutine(FireDunkRoutine());
        if (logStateChanges)
            Debug.Log("Fire Dunk started.", this);
    }

    private IEnumerator FireDunkRoutine()
    {
        activeProjectiles.Clear();
        GameObject speedCharacter = actionController.SpeedCharacter;
        GameObject flyCharacter = actionController.FlyCharacter;
        if (speedCharacter != null && speedCharacter != gameObject)
            activeProjectiles.Add(CreateProjectile(speedCharacter, -launchSideOffSet, speedProjectileEffect));
        if (flyCharacter != null && flyCharacter != gameObject)
            activeProjectiles.Add(CreateProjectile(flyCharacter, launchSideOffSet, flyProjectileEffect));
        if (activeProjectiles.Count == 0)
        {
            FinishFireDunk();
            yield break;
        }
        for (int i = 0; i < activeProjectiles.Count; i++)
        {
            ProjectileState projectile = activeProjectiles[i];
            PositionProjectileForLaunch(projectile);
            if (i < activeProjectiles.Count - 1 && launchSpacing > 0f)
                yield return new WaitForSeconds(launchSpacing);
        }
        float timer = 0f;
        WaitForFixedUpdate fixedUpdate = new();
        while (timer < maximumTravelTime && HasActiveProjectile())
        {
            timer += Time.fixedDeltaTime;
            for (int i = 0; i < activeProjectiles.Count; i++)
            {
                ProjectileState projectile = activeProjectiles[i];
                if (projectile.Finished)
                    continue;
                MoveProjectile(projectile);
            }
            yield return fixedUpdate;
        }
        for (int i = 0; i < activeProjectiles.Count; i++)
        {
            if (!activeProjectiles[i].Finished)
                ImpactProjectile(activeProjectiles[i], activeProjectiles[i].Transform.position);
        }
        if (returnDelay > 0f)
            yield return new WaitForSeconds(returnDelay);
        FinishFireDunk();
    }

    private ProjectileState CreateProjectile(GameObject character, float sideOffSet, GameObject effect)
    {
        Transform characterTransform = character.transform;
        return new ProjectileState
        {
            Character = character,
            Transform = characterTransform,
            OriginalParent = characterTransform.parent,
            OriginalLocalPosition = characterTransform.localPosition,
            OriginalLocalRotation = characterTransform.localRotation,
            Direction = GetLaunchDirection(sideOffSet),
            sideOffSet = sideOffSet,
            Effect = effect,
            Finished = false
        };
    }

    private Vector3 GetLaunchDirection(float sideOffSet)
    {
        Vector3 direction = transform.forward +
                            transform.right * sideOffSet * 0.15f +
                            Vector3.down * downwardAmount;
        return direction.normalized;
    }

    private void PositionProjectileForLaunch(ProjectileState projectile)
    {
        if (projectile == null || projectile.Transform == null)
            return;
        projectile.Transform.SetParent(null, true);
        projectile.Transform.position = transform.position +
                                        Vector3.up * launchHeightOffset +
                                        transform.right * projectile.sideOffSet;
        if (projectile.Direction.sqrMagnitude > 0.0001f)
            projectile.Transform.rotation = Quaternion.LookRotation(projectile.Direction, Vector3.up);
        if (projectile.Effect != null)
        {
            projectile.Effect.transform.SetParent(projectile.Transform);
            projectile.Effect.transform.localPosition = Vector3.zero;
            projectile.Effect.SetActive(true);
        }
    }

    private void MoveProjectile(ProjectileState projectile)
    {
        if (projectile == null || projectile.Transform == null)
        {
            if (projectile != null)
                projectile.Finished = true;
            return;
        }
        Vector3 currentPosition = projectile.Transform.position;
        float distance = projectileSpeed * Time.fixedDeltaTime;
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
        projectile.Transform.position = currentPosition + projectile.Direction * distance;
    }

    private void ImpactProjectile(ProjectileState projectile, Vector3 impactPosition)
    {
        if (projectile == null || projectile.Finished)
            return;
        projectile.Finished = true;
        DamageArea(impactPosition);
        if (impactEffect != null)
        {
            GameObject spawnedEffect = Instantiate(
                impactEffect,
                impactPosition,
                Quaternion.identity);
            Destroy(spawnedEffect, 3f);
        }
        if (projectile.Effect != null)
            projectile.Effect.SetActive(false);
        PlaySound(impactSound);
    }

    private void DamageArea(Vector3 impactPosition)
    {
        Collider[] hits = Physics.OverlapSphere(
            impactPosition,
            explosionRadius,
            damageLayers,
            QueryTriggerInteraction.Collide);
        HashSet<GameObject> damagedObjects = new();
        foreach (Collider hit in hits)
        {
            if (hit == null)
                continue;
            GameObject target = hit.attachedRigidbody != null
                ? hit.attachedRigidbody.gameObject
                : hit.gameObject;
            if (target == gameObject || IsTeamCharacter(target))
                continue;
            if (!damagedObjects.Add(target))
                continue;
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
        if (target == null || actionController == null)
            return false;
        return target == actionController.SpeedCharacter ||
               target == actionController.FlyCharacter ||
               target == actionController.PowerCharacter ||
               target.transform.IsChildOf(transform);
    }

    private bool HasActiveProjectile()
    {
        for (int i = 0; i < activeProjectiles.Count; i++)
        {
            if (!activeProjectiles[i].Finished)
                return true;
        }
        return false;
    }

    public void CancelFireDunk()
    {
        if (!isPerformingFireDunk)
            return;
        FinishFireDunk();
    }

    private void FinishFireDunk()
    {
        if (!isPerformingFireDunk)
            return;
        isPerformingFireDunk = false;
        if (fireDunkCoroutine != null)
        {
            StopCoroutine(fireDunkCoroutine);
            fireDunkCoroutine = null;
        }
        RestoreProjectiles();
        if (playerRigidbody != null)
            playerRigidbody.useGravity = previousGravityState;
        if (actionController != null)
        {
            actionController.EnableFollowers();
            if (actionController.CurrentAction == TeamActionController.TeamAction.FireDunk)
                actionController.EndAction(restoreMovementControl: true);
        }
        else if (movementDisabledDirectly && movement != null)
        {
            movement.EnableMovement();
        }
        movementDisabledDirectly = false;
        PlaySound(finishSound);
        if (logStateChanges)
            Debug.Log("Fire Dunk finished.", this);
    }

    private void RestoreProjectiles()
    {
        for (int i = 0; i < activeProjectiles.Count; i++)
        {
            ProjectileState projectile = activeProjectiles[i];
            if (projectile == null || projectile.Transform == null)
                continue;
            if (projectile.Effect != null)
                projectile.Effect.SetActive(false);
            projectile.Transform.SetParent(projectile.OriginalParent, false);
            projectile.Transform.localPosition = projectile.OriginalLocalPosition;
            projectile.Transform.localRotation = projectile.OriginalLocalRotation;
        }
        activeProjectiles.Clear();
    }

    private void PlayAnimation()
    {
        if (fireDunkAnimator == null || string.IsNullOrWhiteSpace(fireDunkTrigger))
            return;
        fireDunkAnimator.SetTrigger(fireDunkTrigger);
    }

    private void PlaySound(AudioClip clip)
    {
        if (fireDunkAudioSource == null || clip == null)
            return;
        fireDunkAudioSource.PlayOneShot(clip);
    }

    private void SetEffectsActive(bool active)
    {
        if (speedProjectileEffect != null)
            speedProjectileEffect.SetActive(active);
        if (flyProjectileEffect != null)
            flyProjectileEffect.SetActive(active);
    }

    private void OnDisable()
    {
        if (isPerformingFireDunk)
            FinishFireDunk();
        else
            SetEffectsActive(false);
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
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawImpactRadius)
            return;
        Vector3 direction = (
            transform.forward +
            Vector3.down * downwardAmount).normalized;
        Vector3 previewPoint = transform.position +
                               direction * projectileSpeed * maximumTravelTime;
        Gizmos.DrawLine(transform.position, previewPoint);
        Gizmos.DrawWireSphere(previewPoint, explosionRadius);
    }
}