using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChaosInferno : MonoBehaviour
{
    private static readonly int ChaosInfernoHash =
        Animator.StringToHash("Chaos Inferno");

    #region Team Blast

    [Header("Team Blast")]

    [SerializeField, Min(0.1f)]
    private float blastRadius = 20f;

    [SerializeField, Min(1)]
    private int damage = 100;

    [SerializeField, Min(0f)]
    private float knockbackForce = 16f;

    [SerializeField, Min(1)]
    private int pulseCount = 2;

    [SerializeField, Min(0f)]
    private float pulseInterval = 0.2f;

    [SerializeField]
    private LayerMask enemyLayers = ~0;

    #endregion

    #region Timing

    [Header("Timing")]

    [SerializeField, Min(0f)]
    private float activationDelay = 0.1f;

    [SerializeField, Min(0f)]
    private float recoveryDuration = 0.5f;

    #endregion

    #region References

    [Header("References")]

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

    [SerializeField, Min(0f)]
    private float effectLifetime = 3f;

    #endregion

    #region Audio

    [Header("Audio")]

    [SerializeField]
    private AudioClip activationSound;

    [SerializeField]
    private AudioClip pulseSound;

    #endregion

    #region Debug

    [Header("Debug")]

    [SerializeField]
    private bool drawBlastRadius = true;

    #endregion

    #region Runtime State

    private readonly HashSet<Health> affectedEnemies =
        new();

    private Coroutine infernoRoutine;

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

        infernoRoutine =
            StartCoroutine(
                ChaosInfernoRoutine());

        return true;
    }

    public void Cancel()
    {
        if (!isActive)
        {
            return;
        }

        if (infernoRoutine != null)
        {
            StopCoroutine(
                infernoRoutine);

            infernoRoutine = null;
        }

        Finish();
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

        return IsTeamDark();
    }

    private IEnumerator ChaosInfernoRoutine()
    {
        ResolveReferences();

        PlayAnimation();

        PlaySound(
            activationSound);

        SpawnEffect(
            activationEffect,
            GetBlastPosition());

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
            ApplyPulse();

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

        infernoRoutine = null;

        Finish();
    }

    private void Finish()
    {
        isActive = false;

        affectedEnemies.Clear();
    }

    #endregion

    #region Damage

    private void ApplyPulse()
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
            if (enemy == null)
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
        Collider target,
        Vector3 blastPosition)
    {
        if (target == null ||
            knockbackForce <= 0f)
        {
            return;
        }

        Rigidbody body =
            target.attachedRigidbody;

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
                direction.y,
                0.25f);

        if (!IsFiniteVector(
                direction) ||
            direction.sqrMagnitude <=
                0.0001f)
        {
            direction =
                transform.forward +
                Vector3.up * 0.25f;
        }

        body.AddForce(
            direction.normalized *
                knockbackForce,
            ForceMode.VelocityChange);
    }

    #endregion

    #region Team Dark

    private bool IsTeamDark()
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
            "Team Dark";
    }

    #endregion

    #region References

    private void ResolveReferences()
    {
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
            return
                blastOrigin.position;
        }

        return
            transform.position;
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
                ChaosInfernoHash)
            {
                continue;
            }

            animator.SetTrigger(
                ChaosInfernoHash);

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