using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireDunk : MonoBehaviour
{
    private static readonly int FireDunkHash =
        Animator.StringToHash("Fire Dunk");

    #region Input

    [Header("Input")]

    [SerializeField]
    private KeyCode fireDunkKey = KeyCode.B;

    [SerializeField]
    private bool readPlayerInput = true;

    #endregion

    #region Projectile Movement

    [Header("Projectile Movement")]

    [SerializeField, Min(0.1f)]
    private float projectileSpeed = 35f;

    [SerializeField, Min(0f)]
    private float downwardAmount = 0.75f;

    [SerializeField, Min(0.1f)]
    private float maximumTravelTime = 1.5f;

    [SerializeField, Min(0f)]
    private float launchSpacing = 0.15f;

    [SerializeField, Min(0f)]
    private float returnDelay = 0.2f;

    [SerializeField]
    private float launchHeight = 0.75f;

    [SerializeField]
    private float launchSideDistance = 0.75f;

    #endregion

    #region Impact

    [Header("Impact")]

    [SerializeField, Min(0.05f)]
    private float projectileRadius = 0.5f;

    [SerializeField, Min(0.1f)]
    private float explosionRadius = 3f;

    [SerializeField, Min(1)]
    private int damage = 25;

    [SerializeField, Min(0f)]
    private float knockbackForce = 12f;

    [SerializeField]
    private LayerMask hitLayers = ~0;

    [SerializeField]
    private LayerMask enemyLayers = ~0;

    #endregion

    #region References

    [Header("References")]

    [SerializeField]
    private CharacterSwitch characterSwitch;

    [SerializeField]
    private Animator animator;

    [SerializeField]
    private AudioSource audioSource;

    #endregion

    #region Effects

    [Header("Effects")]

    [SerializeField]
    private GameObject speedFireEffect;

    [SerializeField]
    private GameObject flyFireEffect;

    [SerializeField]
    private GameObject impactEffect;

    [SerializeField, Min(0f)]
    private float effectLifetime = 3f;

    #endregion

    #region Audio

    [Header("Audio")]

    [SerializeField]
    private AudioClip launchSound;

    [SerializeField]
    private AudioClip impactSound;

    [SerializeField]
    private AudioClip finishSound;

    #endregion

    #region Debug

    [Header("Debug")]

    [SerializeField]
    private bool drawTrajectory = true;

    #endregion

    #region Runtime State

    private readonly List<FireProjectile> projectiles =
        new();

    private readonly HashSet<Health> damagedTargets =
        new();

    private Coroutine fireDunkRoutine;

    private bool isPerforming;

    #endregion

    #region Public API

    public bool IsPerforming =>
        isPerforming;

    public bool TryStartFireDunk()
    {
        if (!CanUseFireDunk())
        {
            return false;
        }

        isPerforming =
            true;

        fireDunkRoutine =
            StartCoroutine(
                FireDunkRoutine());

        return true;
    }

    public void CancelFireDunk()
    {
        if (!isPerforming)
        {
            return;
        }

        if (fireDunkRoutine != null)
        {
            StopCoroutine(
                fireDunkRoutine);

            fireDunkRoutine =
                null;
        }

        FinishFireDunk();
    }

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
        ResolveReferences();
    }

    private void Update()
    {
        if (!readPlayerInput ||
            isPerforming)
        {
            return;
        }

        if (Input.GetKeyDown(
                fireDunkKey))
        {
            TryStartFireDunk();
        }
    }

    private void OnDisable()
    {
        CancelFireDunk();
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
                0.05f,
                projectileRadius);

        explosionRadius =
            Mathf.Max(
                0.1f,
                explosionRadius);

        damage =
            Mathf.Max(
                1,
                damage);

        knockbackForce =
            Mathf.Max(
                0f,
                knockbackForce);

        effectLifetime =
            Mathf.Max(
                0f,
                effectLifetime);
    }

    #endregion

    #region Activation

    private bool CanUseFireDunk()
    {
        if (isPerforming)
        {
            return false;
        }

        ResolveReferences();

        if (characterSwitch == null)
        {
            return false;
        }

        if (!IsTeamSonic())
        {
            return false;
        }

        if (characterSwitch.currentCharacterType !=
            CHARACTERTYPES.Power)
        {
            return false;
        }

        return
            characterSwitch.speedCharacter != null ||
            characterSwitch.flyingCharacter != null;
    }

    private bool IsTeamSonic()
    {
        TeamSetup teamSetup =
            Object.FindAnyObjectByType<TeamSetup>();

        if (teamSetup == null ||
            teamSetup.CurrentTeam == null)
        {
            return false;
        }

        return
            teamSetup.CurrentTeam.name ==
            "Team Sonic";
    }

    #endregion

    #region Fire Dunk Routine

    private IEnumerator FireDunkRoutine()
    {
        ResolveReferences();

        PlayAnimation();

        PlaySound(
            launchSound);

        projectiles.Clear();

        CreateProjectile(
            characterSwitch.speedCharacter,
            -launchSideDistance,
            speedFireEffect);

        CreateProjectile(
            characterSwitch.flyingCharacter,
            launchSideDistance,
            flyFireEffect);

        if (projectiles.Count == 0)
        {
            FinishFireDunk();

            yield break;
        }

        for (int index = 0;
            index < projectiles.Count;
            index++)
        {
            LaunchProjectile(
                projectiles[index]);

            if (index <
                    projectiles.Count - 1 &&
                launchSpacing > 0f)
            {
                yield return
                    new WaitForSeconds(
                        launchSpacing);
            }
        }

        float elapsed =
            0f;

        WaitForFixedUpdate fixedUpdate =
            new();

        while (elapsed <
                maximumTravelTime &&
            HasActiveProjectile())
        {
            elapsed +=
                Time.fixedDeltaTime;

            foreach (FireProjectile projectile
                in projectiles)
            {
                if (projectile == null ||
                    projectile.HasImpacted)
                {
                    continue;
                }

                MoveProjectile(
                    projectile);
            }

            yield return
                fixedUpdate;
        }

        foreach (FireProjectile projectile
            in projectiles)
        {
            if (projectile == null ||
                projectile.HasImpacted ||
                projectile.Character == null)
            {
                continue;
            }

            ImpactProjectile(
                projectile,
                projectile.Character.position);
        }

        if (returnDelay > 0f)
        {
            yield return
                new WaitForSeconds(
                    returnDelay);
        }

        fireDunkRoutine =
            null;

        FinishFireDunk();
    }

    #endregion

    #region Projectile Creation

    private void CreateProjectile(
        Transform character,
        float sideOffset,
        GameObject fireEffectPrefab)
    {
        if (character == null)
        {
            return;
        }

        FireProjectile projectile =
            new()
            {
                Character =
                    character,

                OriginalParent =
                    character.parent,

                OriginalSiblingIndex =
                    character.GetSiblingIndex(),

                OriginalLocalPosition =
                    character.localPosition,

                OriginalLocalRotation =
                    character.localRotation,

                OriginalLocalScale =
                    character.localScale,

                SideOffset =
                    sideOffset,

                FireEffectPrefab =
                    fireEffectPrefab
            };

        projectile.Follower =
            character.GetComponentInParent<FollowerNavigation>();

        if (projectile.Follower != null)
        {
            projectile.FollowerWasEnabled =
                projectile.Follower.enabled;
        }

        projectiles.Add(
            projectile);
    }

    private void LaunchProjectile(
        FireProjectile projectile)
    {
        if (projectile == null ||
            projectile.Character == null)
        {
            return;
        }

        if (projectile.Follower != null)
        {
            projectile.Follower.enabled =
                false;
        }

        projectile.Character.SetParent(
            null,
            worldPositionStays: true);

        projectile.Character.position =
            transform.position +
            Vector3.up *
                launchHeight +
            transform.right *
                projectile.SideOffset;

        projectile.Direction =
            GetLaunchDirection(
                projectile.SideOffset);

        if (projectile.Direction.sqrMagnitude >
            0.0001f)
        {
            projectile.Character.rotation =
                Quaternion.LookRotation(
                    projectile.Direction,
                    Vector3.up);
        }

        if (projectile.FireEffectPrefab != null)
        {
            projectile.FireEffect =
                Instantiate(
                    projectile.FireEffectPrefab,
                    projectile.Character);

            projectile.FireEffect.transform.localPosition =
                Vector3.zero;

            projectile.FireEffect.transform.localRotation =
                Quaternion.identity;
        }
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

        if (!IsFiniteVector(
                direction) ||
            direction.sqrMagnitude <=
                0.0001f)
        {
            return Vector3.down;
        }

        return
            direction.normalized;
    }

    #endregion

    #region Projectile Movement

    private void MoveProjectile(
        FireProjectile projectile)
    {
        if (projectile.Character == null)
        {
            projectile.HasImpacted =
                true;

            return;
        }

        Vector3 currentPosition =
            projectile.Character.position;

        if (!IsFiniteVector(
                currentPosition))
        {
            projectile.HasImpacted =
                true;

            return;
        }

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
            if (IsTeamCollider(
                    hit.collider))
            {
                MoveProjectileForward(
                    projectile,
                    currentPosition,
                    distance);

                return;
            }

            projectile.Character.position =
                hit.point;

            ImpactProjectile(
                projectile,
                hit.point);

            return;
        }

        MoveProjectileForward(
            projectile,
            currentPosition,
            distance);
    }

    private void MoveProjectileForward(
        FireProjectile projectile,
        Vector3 currentPosition,
        float distance)
    {
        Vector3 nextPosition =
            currentPosition +
            projectile.Direction *
                distance;

        if (!IsFiniteVector(
                nextPosition))
        {
            projectile.HasImpacted =
                true;

            return;
        }

        projectile.Character.position =
            nextPosition;
    }

    private bool HasActiveProjectile()
    {
        foreach (FireProjectile projectile
            in projectiles)
        {
            if (projectile != null &&
                !projectile.HasImpacted &&
                projectile.Character != null)
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Impact

    private void ImpactProjectile(
        FireProjectile projectile,
        Vector3 impactPosition)
    {
        if (projectile == null ||
            projectile.HasImpacted ||
            !IsFiniteVector(
                impactPosition))
        {
            return;
        }

        projectile.HasImpacted =
            true;

        DamageArea(
            impactPosition);

        SpawnImpactEffect(
            impactPosition);

        PlaySound(
            impactSound);

        if (projectile.FireEffect != null)
        {
            Destroy(
                projectile.FireEffect);

            projectile.FireEffect =
                null;
        }
    }

    private void DamageArea(
        Vector3 impactPosition)
    {
        damagedTargets.Clear();

        Collider[] hits =
            Physics.OverlapSphere(
                impactPosition,
                explosionRadius,
                enemyLayers,
                QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            if (hit == null ||
                IsTeamCollider(
                    hit))
            {
                continue;
            }

            Health health =
                hit.GetComponent<Health>();

            health ??=
                hit.GetComponentInParent<Health>();

            if (health == null ||
                health.dead ||
                !damagedTargets.Add(
                    health))
            {
                continue;
            }

            health.TakeDamage(
                damage);

            ApplyKnockback(
                hit,
                impactPosition);
        }
    }

    private void ApplyKnockback(
        Collider target,
        Vector3 impactPosition)
    {
        if (knockbackForce <= 0f)
        {
            return;
        }

        Rigidbody targetBody =
            target.attachedRigidbody;

        if (targetBody == null ||
            targetBody.isKinematic)
        {
            return;
        }

        Vector3 direction =
            targetBody.worldCenterOfMass -
            impactPosition;

        direction.y =
            Mathf.Max(
                direction.y,
                0.25f);

        if (!IsFiniteVector(
                direction) ||
            direction.sqrMagnitude <=
                0.0001f)
        {
            direction =
                Vector3.up;
        }

        targetBody.AddForce(
            direction.normalized *
                knockbackForce,
            ForceMode.VelocityChange);
    }

    #endregion

    #region Restoration

    private void FinishFireDunk()
    {
        RestoreTeammates();

        isPerforming =
            false;

        PlaySound(
            finishSound);
    }

    private void RestoreTeammates()
    {
        foreach (FireProjectile projectile
            in projectiles)
        {
            if (projectile == null ||
                projectile.Character == null)
            {
                continue;
            }

            if (projectile.FireEffect != null)
            {
                Destroy(
                    projectile.FireEffect);

                projectile.FireEffect =
                    null;
            }

            if (projectile.OriginalParent != null)
            {
                projectile.Character.SetParent(
                    projectile.OriginalParent,
                    worldPositionStays: false);

                projectile.Character.localPosition =
                    projectile.OriginalLocalPosition;

                projectile.Character.localRotation =
                    projectile.OriginalLocalRotation;

                projectile.Character.localScale =
                    projectile.OriginalLocalScale;

                int siblingIndex =
                    Mathf.Clamp(
                        projectile.OriginalSiblingIndex,
                        0,
                        Mathf.Max(
                            0,
                            projectile.OriginalParent.childCount - 1));

                projectile.Character.SetSiblingIndex(
                    siblingIndex);
            }

            if (projectile.Follower != null)
            {
                projectile.Follower.enabled =
                    projectile.FollowerWasEnabled;
            }
        }

        projectiles.Clear();
        damagedTargets.Clear();
    }

    #endregion

    #region Team Filtering

    private bool IsTeamCollider(
        Collider candidate)
    {
        if (candidate == null ||
            characterSwitch == null)
        {
            return false;
        }

        Transform candidateTransform =
            candidate.transform;

        return
            MatchesCharacter(
                candidateTransform,
                characterSwitch.speedCharacter) ||
            MatchesCharacter(
                candidateTransform,
                characterSwitch.flyingCharacter) ||
            MatchesCharacter(
                candidateTransform,
                characterSwitch.powerCharacter);
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
            candidate.IsChildOf(
                character) ||
            character.IsChildOf(
                candidate);
    }

    #endregion

    #region References

    private void ResolveReferences()
    {
        characterSwitch ??=
            GetComponentInParent<CharacterSwitch>();

        characterSwitch ??=
            Object.FindAnyObjectByType<CharacterSwitch>();

        if (animator == null &&
            characterSwitch != null &&
            characterSwitch.powerCharacter != null)
        {
            animator =
                characterSwitch.powerCharacter
                    .GetComponentInChildren<Animator>(
                        includeInactive: true);
        }

        audioSource ??=
            GetComponent<AudioSource>();

        audioSource ??=
            GetComponentInParent<AudioSource>();
    }

    #endregion

    #region Presentation

    private void PlayAnimation()
    {
        if (animator == null ||
            !animator.isActiveAndEnabled ||
            animator.runtimeAnimatorController ==
                null)
        {
            return;
        }

        foreach (
            AnimatorControllerParameter parameter
            in animator.parameters)
        {
            if (parameter.nameHash !=
                FireDunkHash)
            {
                continue;
            }

            animator.SetTrigger(
                FireDunkHash);

            return;
        }
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

    private void SpawnImpactEffect(
        Vector3 position)
    {
        if (impactEffect == null ||
            !IsFiniteVector(
                position))
        {
            return;
        }

        GameObject effect =
            Instantiate(
                impactEffect,
                position,
                Quaternion.identity);

        if (effectLifetime > 0f)
        {
            Destroy(
                effect,
                effectLifetime);
        }
    }

    #endregion

    #region Validation

    private static bool IsFiniteVector(
        Vector3 value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (!drawTrajectory)
        {
            return;
        }

        Vector3 direction =
            GetLaunchDirection(
                0f);

        Vector3 destination =
            transform.position +
            direction *
                projectileSpeed *
                maximumTravelTime;

        Gizmos.DrawLine(
            transform.position,
            destination);

        Gizmos.DrawWireSphere(
            destination,
            explosionRadius);
    }

    #endregion

    #region Internal Types

    private class FireProjectile
    {
        public Transform Character;
        public Transform OriginalParent;

        public FollowerNavigation Follower;

        public GameObject FireEffectPrefab;
        public GameObject FireEffect;

        public Vector3 OriginalLocalPosition;
        public Quaternion OriginalLocalRotation;
        public Vector3 OriginalLocalScale;
        public Vector3 Direction;

        public float SideOffset;

        public int OriginalSiblingIndex;

        public bool FollowerWasEnabled;
        public bool HasImpacted;
    }

    #endregion
}