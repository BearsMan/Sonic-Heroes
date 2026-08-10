using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlowerFestival : MonoBehaviour
{
    private static readonly int FlowerFestivalHash =
        Animator.StringToHash("Flower Festival");

    #region Team Blast

    [Header("Team Blast")]

    [SerializeField, Min(0.1f)]
    private float blastRadius = 20f;

    [SerializeField, Min(1)]
    private int damage = 100;

    [SerializeField, Min(0f)]
    private float knockbackForce = 12f;

    [SerializeField, Min(1)]
    private int pulseCount = 2;

    [SerializeField, Min(0f)]
    private float pulseInterval = 0.25f;

    [SerializeField]
    private LayerMask enemyLayers = ~0;

    #endregion

    #region Team Support

    [Header("Team Support")]

    [SerializeField]
    private bool restoreTeamHealth = true;

    [SerializeField]
    private bool reviveDefeatedTeammates = true;

    [SerializeField]
    private bool applySupportOnActivation = true;

    #endregion

    #region Timing

    [Header("Timing")]

    [SerializeField, Min(0f)]
    private float activationDelay = 0.2f;

    [SerializeField, Min(0f)]
    private float recoveryDuration = 0.75f;

    #endregion

    #region References

    [Header("References")]

    [SerializeField]
    private CharacterSwitch characterSwitch;

    [SerializeField]
    private Transform blastOrigin;

    [SerializeField]
    private Animator animator;

    [SerializeField]
    private AudioSource audioSource;

    #endregion

    #region Effects

    [Header("Effects")]

    [SerializeField]
    private GameObject activationEffect;

    [SerializeField]
    private GameObject pulseEffect;

    [SerializeField]
    private GameObject supportEffect;

    [SerializeField, Min(0f)]
    private float effectLifetime = 4f;

    #endregion

    #region Audio

    [Header("Audio")]

    [SerializeField]
    private AudioClip activationSound;

    [SerializeField]
    private AudioClip pulseSound;

    [SerializeField]
    private AudioClip supportSound;

    #endregion

    #region Debug

    [Header("Debug")]

    [SerializeField]
    private bool drawBlastRadius = true;

    #endregion

    #region Runtime State

    private readonly HashSet<Health> affectedEnemies =
        new();

    private Coroutine festivalRoutine;

    private bool isActive;

    #endregion

    #region Public API

    public bool IsActive =>
        isActive;

    public bool TryActivate()
    {
        if (!CanActivate())
        {
            return false;
        }

        isActive = true;

        festivalRoutine =
            StartCoroutine(
                FlowerFestivalRoutine());

        return true;
    }

    public void Cancel()
    {
        if (!isActive)
        {
            return;
        }

        if (festivalRoutine != null)
        {
            StopCoroutine(
                festivalRoutine);

            festivalRoutine = null;
        }

        FinishFlowerFestival();
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        Cancel();
    }

    private void OnValidate()
    {
        blastRadius =
            Mathf.Max(
                0.1f,
                blastRadius);

        damage =
            Mathf.Max(
                1,
                damage);

        knockbackForce =
            Mathf.Max(
                0f,
                knockbackForce);

        pulseCount =
            Mathf.Max(
                1,
                pulseCount);

        pulseInterval =
            Mathf.Max(
                0f,
                pulseInterval);

        activationDelay =
            Mathf.Max(
                0f,
                activationDelay);

        recoveryDuration =
            Mathf.Max(
                0f,
                recoveryDuration);

        effectLifetime =
            Mathf.Max(
                0f,
                effectLifetime);
    }

    #endregion

    #region Activation

    private bool CanActivate()
    {
        if (isActive)
        {
            return false;
        }

        ResolveReferences();

        return IsTeamRose();
    }

    private IEnumerator FlowerFestivalRoutine()
    {
        ResolveReferences();

        PlayAnimation();

        PlaySound(
            activationSound);

        SpawnEffect(
            activationEffect,
            GetBlastPosition());

        if (applySupportOnActivation)
        {
            ApplyTeamSupport();
        }

        if (activationDelay > 0f)
        {
            yield return
                new WaitForSeconds(
                    activationDelay);
        }

        for (int pulse = 0;
            pulse < pulseCount;
            pulse++)
        {
            ApplyFestivalPulse();

            if (pulse <
                    pulseCount - 1 &&
                pulseInterval > 0f)
            {
                yield return
                    new WaitForSeconds(
                        pulseInterval);
            }
        }

        if (recoveryDuration > 0f)
        {
            yield return
                new WaitForSeconds(
                    recoveryDuration);
        }

        festivalRoutine = null;

        FinishFlowerFestival();
    }

    private void FinishFlowerFestival()
    {
        isActive = false;

        affectedEnemies.Clear();
    }

    #endregion

    #region Festival Attack

    private void ApplyFestivalPulse()
    {
        Vector3 position =
            GetBlastPosition();

        if (!IsFiniteVector(
                position))
        {
            return;
        }

        affectedEnemies.Clear();

        SpawnEffect(
            pulseEffect,
            position);

        PlaySound(
            pulseSound);

        Collider[] enemies =
            Physics.OverlapSphere(
                position,
                blastRadius,
                enemyLayers,
                QueryTriggerInteraction.Ignore);

        foreach (Collider enemy in enemies)
        {
            if (enemy == null ||
                IsTeamCollider(
                    enemy))
            {
                continue;
            }

            Health health =
                FindHealth(
                    enemy);

            if (health == null ||
                health.dead ||
                !affectedEnemies.Add(
                    health))
            {
                continue;
            }

            health.TakeDamage(
                damage);

            ApplyKnockback(
                enemy,
                position);
        }
    }

    private static Health FindHealth(
        Collider target)
    {
        if (target == null)
        {
            return null;
        }

        Health health =
            target.GetComponent<Health>();

        health ??=
            target.GetComponentInParent<Health>();

        health ??=
            target.GetComponentInChildren<Health>(
                includeInactive: true);

        return health;
    }

    #endregion

    #region Knockback

    private void ApplyKnockback(
        Collider enemy,
        Vector3 blastPosition)
    {
        if (enemy == null ||
            knockbackForce <= 0f)
        {
            return;
        }

        Rigidbody body =
            enemy.attachedRigidbody;

        if (body == null ||
            body.isKinematic)
        {
            return;
        }

        Vector3 direction =
            body.worldCenterOfMass -
            blastPosition;

        direction.y =
            Mathf.Max(
                0.25f,
                direction.y);

        if (!IsFiniteVector(
                direction) ||
            direction.sqrMagnitude <=
                0.0001f)
        {
            direction =
                Vector3.up;
        }

        body.AddForce(
            direction.normalized *
                knockbackForce,
            ForceMode.VelocityChange);
    }

    #endregion

    #region Team Support

    private void ApplyTeamSupport()
    {
        if (characterSwitch == null)
        {
            return;
        }

        PlaySound(
            supportSound);

        ApplySupportToCharacter(
            characterSwitch.speedCharacter);

        ApplySupportToCharacter(
            characterSwitch.flyingCharacter);

        ApplySupportToCharacter(
            characterSwitch.powerCharacter);
    }

    private void ApplySupportToCharacter(
        Transform character)
    {
        if (character == null)
        {
            return;
        }

        Health health =
            character.GetComponent<Health>();

        health ??=
            character.GetComponentInParent<Health>();

        health ??=
            character.GetComponentInChildren<Health>(
                includeInactive: true);

        if (health != null &&
            (restoreTeamHealth ||
                (reviveDefeatedTeammates &&
                 health.dead)))
        {
            health.HealthValue = 100;
        }

        SpawnEffect(
            supportEffect,
            character.position);
    }

    #endregion

    #region Team Rose

    private bool IsTeamRose()
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
            "Team Rose";
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

        Transform target =
            candidate.transform;

        return
            MatchesCharacter(
                target,
                characterSwitch.speedCharacter) ||
            MatchesCharacter(
                target,
                characterSwitch.flyingCharacter) ||
            MatchesCharacter(
                target,
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

        blastOrigin ??=
            transform;

        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>(
                    includeInactive: true);
        }

        audioSource ??=
            GetComponent<AudioSource>();

        audioSource ??=
            GetComponentInParent<AudioSource>();
    }

    private Vector3 GetBlastPosition()
    {
        if (blastOrigin != null &&
            IsFiniteVector(
                blastOrigin.position))
        {
            return blastOrigin.position;
        }

        return transform.position;
    }

    #endregion

    #region Animation

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
                FlowerFestivalHash)
            {
                continue;
            }

            animator.SetTrigger(
                FlowerFestivalHash);

            return;
        }
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

    #region Effects

    private void SpawnEffect(
        GameObject effect,
        Vector3 position)
    {
        if (effect == null ||
            !IsFiniteVector(
                position))
        {
            return;
        }

        GameObject instance =
            Instantiate(
                effect,
                position,
                transform.rotation);

        if (effectLifetime > 0f)
        {
            Destroy(
                instance,
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
        if (!drawBlastRadius)
        {
            return;
        }

        Gizmos.DrawWireSphere(
            GetBlastPosition(),
            blastRadius);
    }

    #endregion
}