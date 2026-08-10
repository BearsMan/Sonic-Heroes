using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ChaotixRecital : MonoBehaviour
{
    private static readonly int RecitalHash =
        Animator.StringToHash("Chaotix Recital");

    #region Team Blast

    [Header("Team Blast")]

    [SerializeField, Min(0.1f)]
    private float blastRadius = 25f;

    [SerializeField, Min(1)]
    private int blastDamage = 100;

    [SerializeField, Min(0f)]
    private float knockbackForce = 15f;

    [SerializeField]
    private LayerMask enemyLayers = ~0;

    #endregion

    #region Timing

    [Header("Timing")]

    [SerializeField, Min(0f)]
    private float activationDelay = 0.25f;

    [SerializeField, Min(0f)]
    private float activeDuration = 1f;

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
    private GameObject blastEffect;

    [SerializeField, Min(0f)]
    private float effectLifetime = 4f;

    #endregion

    #region Audio

    [Header("Audio")]

    [SerializeField]
    private AudioClip activationSound;

    [SerializeField]
    private AudioClip blastSound;

    #endregion

    #region Debug

    [Header("Debug")]

    [SerializeField]
    private bool drawBlastRadius = true;

    #endregion

    #region Runtime State

    private readonly HashSet<Health> affectedEnemies =
        new();

    private Coroutine recitalRoutine;

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

        isActive =
            true;

        affectedEnemies.Clear();

        recitalRoutine =
            StartCoroutine(
                RecitalRoutine());

        return true;
    }

    public void Cancel()
    {
        if (!isActive)
        {
            return;
        }

        if (recitalRoutine != null)
        {
            StopCoroutine(
                recitalRoutine);

            recitalRoutine =
                null;
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

        blastDamage =
            Mathf.Max(
                1,
                blastDamage);

        knockbackForce =
            Mathf.Max(
                0f,
                knockbackForce);

        activationDelay =
            Mathf.Max(
                0f,
                activationDelay);

        activeDuration =
            Mathf.Max(
                0f,
                activeDuration);

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

        return IsTeamChaotix();
    }

    private IEnumerator RecitalRoutine()
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

        PerformTeamBlast();

        PlaySound(
            blastSound);

        SpawnEffect(
            blastEffect,
            GetBlastPosition());

        if (activeDuration > 0f)
        {
            yield return
                new WaitForSeconds(
                    activeDuration);
        }

        if (recoveryDuration > 0f)
        {
            yield return
                new WaitForSeconds(
                    recoveryDuration);
        }

        recitalRoutine =
            null;

        Finish();
    }

    private void Finish()
    {
        isActive =
            false;

        affectedEnemies.Clear();
    }

    #endregion

    #region Team Blast Damage

    private void PerformTeamBlast()
    {
        Vector3 position =
            GetBlastPosition();

        if (!IsFiniteVector(
                position))
        {
            return;
        }

        affectedEnemies.Clear();

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
                FindEnemyHealth(
                    enemy);

            if (health == null ||
                health.dead ||
                !affectedEnemies.Add(
                    health))
            {
                continue;
            }

            health.TakeDamage(
                blastDamage);

            ApplyKnockback(
                enemy,
                position);
        }
    }

    private static Health FindEnemyHealth(
        Collider enemy)
    {
        Health health =
            enemy.GetComponent<Health>();

        health ??=
            enemy.GetComponentInParent<Health>();

        return health;
    }

    #endregion

    #region Knockback

    private void ApplyKnockback(
        Collider enemy,
        Vector3 blastPosition)
    {
        if (knockbackForce <= 0f)
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

        body.AddForce(
            direction.normalized *
                knockbackForce,
            ForceMode.VelocityChange);
    }

    #endregion

    #region Team Restriction

    private bool IsTeamChaotix()
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
            "Team Chaotix";
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

        if (audioSource == null)
        {
            audioSource =
                GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource =
                GetComponentInParent<AudioSource>();
        }
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
                RecitalHash)
            {
                continue;
            }

            animator.SetTrigger(
                RecitalHash);

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