using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SonicOverdrive : MonoBehaviour
{
    #region Constants

    private const int MaximumHitResults = 64;

    private static readonly int SonicOverdriveHash =
        Animator.StringToHash(
            "Sonic Overdrive");

    #endregion

    #region References

    [Header("References")]

    [SerializeField]
    private TeamActionController actionController;

    [SerializeField]
    private Transform blastOrigin;

    [SerializeField]
    private Animator animator;

    [SerializeField]
    private AudioSource audioSource;

    #endregion

    #region Team Blast

    [Header("Sonic Overdrive")]

    [SerializeField, Min(1)]
    private int damage = 3;

    [SerializeField, Min(0.1f)]
    private float blastRadius = 18f;

    [SerializeField, Min(1)]
    private int pulseCount = 3;

    [SerializeField, Min(0f)]
    private float pulseInterval = 0.12f;

    [SerializeField, Min(0.05f)]
    private float totalDuration = 0.75f;

    [SerializeField]
    private LayerMask enemyLayers = ~0;

    #endregion

    #region Team Rush

    [Header("Team Rush")]

    [SerializeField]
    private bool moveTeamForward = true;

    [SerializeField, Min(0f)]
    private float rushDistance = 8f;

    [SerializeField, Min(0.01f)]
    private float rushDuration = 0.2f;

    #endregion

    #region Knockback

    [Header("Knockback")]

    [SerializeField, Min(0f)]
    private float knockbackForce = 25f;

    [SerializeField, Min(0f)]
    private float upwardKnockback = 4f;

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

    [SerializeField]
    private AudioClip completionSound;

    #endregion

    #region Runtime State

    private readonly Collider[] hitResults =
        new Collider[MaximumHitResults];

    private readonly HashSet<AIController> damagedEnemies =
        new();

    private readonly HashSet<GameObject> brokenObjects =
        new();

    private Coroutine overdriveRoutine;

    private bool isActive;
    private bool isInitialized;

    #endregion

    #region Properties

    public bool IsActive =>
        isActive;

    public bool IsInitialized =>
        isInitialized;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        Initialize();
    }

    private void OnDisable()
    {
        Cancel();
    }

    private void OnValidate()
    {
        damage =
            Mathf.Max(
                1,
                damage);

        blastRadius =
            Mathf.Max(
                0.1f,
                blastRadius);

        pulseCount =
            Mathf.Max(
                1,
                pulseCount);

        pulseInterval =
            Mathf.Max(
                0f,
                pulseInterval);

        totalDuration =
            Mathf.Max(
                0.05f,
                totalDuration);

        rushDistance =
            Mathf.Max(
                0f,
                rushDistance);

        rushDuration =
            Mathf.Max(
                0.01f,
                rushDuration);

        knockbackForce =
            Mathf.Max(
                0f,
                knockbackForce);

        upwardKnockback =
            Mathf.Max(
                0f,
                upwardKnockback);

        effectLifetime =
            Mathf.Max(
                0f,
                effectLifetime);
    }

    #endregion

    #region Initialization

    public bool Initialize()
    {
        if (isInitialized)
        {
            return true;
        }

        ResolveReferences();

        if (actionController == null)
        {
            return false;
        }

        isActive =
            false;

        overdriveRoutine =
            null;

        damagedEnemies.Clear();
        brokenObjects.Clear();

        isInitialized =
            true;

        return true;
    }

    private void ResolveReferences()
    {
        actionController ??=
            GetComponentInParent<
                TeamActionController>();

        animator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        audioSource ??=
            GetComponent<AudioSource>();

        audioSource ??=
            GetComponentInParent<AudioSource>();

        blastOrigin ??=
            transform;
    }

    #endregion

    #region Public API

    public bool TryActivate()
    {
        if (!CanActivate())
        {
            return false;
        }

        BeginOverdrive();

        return true;
    }

    public void Cancel()
    {
        if (!isActive &&
            overdriveRoutine == null)
        {
            return;
        }

        if (overdriveRoutine != null)
        {
            StopCoroutine(
                overdriveRoutine);

            overdriveRoutine =
                null;
        }

        FinishOverdrive();
    }

    #endregion

    #region Activation

    private bool CanActivate()
    {
        return
            isInitialized &&
            !isActive &&
            actionController != null;
    }

    private void BeginOverdrive()
    {
        isActive =
            true;

        damagedEnemies.Clear();
        brokenObjects.Clear();

        PlayAnimation();

        PlaySound(
            activationSound);

        SpawnEffect(
            activationEffect,
            GetBlastPosition());

        overdriveRoutine =
            StartCoroutine(
                OverdriveRoutine());
    }

    private IEnumerator OverdriveRoutine()
    {
        if (moveTeamForward &&
            rushDistance > 0f &&
            rushDuration > 0f)
        {
            yield return
                RushTeamForward();
        }

        float attackStartTime =
            Time.time;

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

        float elapsed =
            Time.time -
            attackStartTime;

        float remainingDuration =
            Mathf.Max(
                0f,
                totalDuration -
                    elapsed);

        if (remainingDuration > 0f)
        {
            yield return
                new WaitForSeconds(
                    remainingDuration);
        }

        overdriveRoutine =
            null;

        FinishOverdrive();
    }

    private void FinishOverdrive()
    {
        if (!isActive)
        {
            return;
        }

        isActive =
            false;

        damagedEnemies.Clear();
        brokenObjects.Clear();

        PlaySound(
            completionSound);
    }

    #endregion

    #region Team Rush

    private IEnumerator RushTeamForward()
    {
        Transform speedCharacter =
            actionController.SpeedCharacter;

        Transform flyCharacter =
            actionController.FlyCharacter;

        Transform powerCharacter =
            actionController.PowerCharacter;

        Transform[] team =
        {
            speedCharacter,
            flyCharacter,
            powerCharacter
        };

        Vector3[] startingPositions =
            new Vector3[team.Length];

        Vector3 forward =
            transform.forward;

        forward.y =
            0f;

        if (!IsFiniteVector(
                forward) ||
            forward.sqrMagnitude <=
                0.0001f)
        {
            forward =
                Vector3.forward;
        }

        forward.Normalize();

        for (int index = 0;
            index < team.Length;
            index++)
        {
            if (team[index] == null)
            {
                continue;
            }

            startingPositions[index] =
                team[index].position;
        }

        float elapsed =
            0f;

        while (elapsed <
            rushDuration)
        {
            elapsed +=
                Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsed /
                        rushDuration);

            progress =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    progress);

            for (int index = 0;
                index < team.Length;
                index++)
            {
                Transform character =
                    team[index];

                if (character == null)
                {
                    continue;
                }

                Vector3 destination =
                    startingPositions[index] +
                    forward *
                        rushDistance;

                Vector3 position =
                    Vector3.Lerp(
                        startingPositions[index],
                        destination,
                        progress);

                if (IsFiniteVector(
                        position))
                {
                    character.position =
                        position;
                }
            }

            yield return null;
        }
    }

    #endregion

    #region Attack Pulse

    private void ApplyPulse()
    {
        Vector3 blastPosition =
            GetBlastPosition();

        if (!IsFiniteVector(
                blastPosition))
        {
            return;
        }

        SpawnEffect(
            pulseEffect,
            blastPosition);

        PlaySound(
            pulseSound);

        int resultCount =
            Physics.OverlapSphereNonAlloc(
                blastPosition,
                blastRadius,
                hitResults,
                enemyLayers,
                QueryTriggerInteraction.Collide);

        for (int index = 0;
            index < resultCount;
            index++)
        {
            Collider hit =
                hitResults[index];

            hitResults[index] =
                null;

            if (hit == null)
            {
                continue;
            }

            TryDamageEnemy(
                hit,
                blastPosition);

            TryBreakObject(
                hit);
        }
    }

    #endregion

    #region Enemy Damage

    private void TryDamageEnemy(
        Collider hit,
        Vector3 blastPosition)
    {
        AIController enemy =
            hit.GetComponent<AIController>();

        enemy ??=
            hit.GetComponentInParent<
                AIController>();

        enemy ??=
            hit.GetComponentInChildren<
                AIController>();

        if (enemy == null ||
            enemy.IsDead ||
            !enemy.isActiveAndEnabled ||
            damagedEnemies.Contains(
                enemy))
        {
            return;
        }

        if (!enemy.TakeDamage(
                damage))
        {
            return;
        }

        damagedEnemies.Add(
            enemy);

        ApplyKnockback(
            enemy.transform,
            blastPosition);
    }

    private void ApplyKnockback(
        Transform target,
        Vector3 blastPosition)
    {
        if (target == null ||
            knockbackForce <= 0f)
        {
            return;
        }

        Rigidbody targetBody =
            target.GetComponent<Rigidbody>();

        targetBody ??=
            target.GetComponentInParent<
                Rigidbody>();

        targetBody ??=
            target.GetComponentInChildren<
                Rigidbody>();

        if (targetBody == null ||
            targetBody.isKinematic)
        {
            return;
        }

        Vector3 direction =
            target.position -
            blastPosition;

        direction.y =
            0f;

        if (!IsFiniteVector(
                direction) ||
            direction.sqrMagnitude <=
                0.0001f)
        {
            direction =
                transform.forward;
        }

        direction.Normalize();

        Vector3 force =
            direction *
                knockbackForce +
            Vector3.up *
                upwardKnockback;

        if (!IsFiniteVector(
                force))
        {
            return;
        }

        targetBody.AddForce(
            force,
            ForceMode.VelocityChange);
    }

    #endregion

    #region Breakables

    private void TryBreakObject(
        Collider hit)
    {
        if (hit == null)
        {
            return;
        }

        GameObject target =
            hit.attachedRigidbody != null
                ? hit.attachedRigidbody
                    .gameObject
                : hit.gameObject;

        if (target == null ||
            brokenObjects.Contains(
                target) ||
            IsTeamCharacter(
                target.transform))
        {
            return;
        }

        brokenObjects.Add(
            target);

        target.SendMessage(
            "Break",
            SendMessageOptions
                .DontRequireReceiver);
    }

    #endregion

    #region Team Filtering

    private bool IsTeamCharacter(
        Transform candidate)
    {
        if (candidate == null ||
            actionController == null)
        {
            return false;
        }

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
            candidate ==
                character ||
            candidate.IsChildOf(
                character) ||
            character.IsChildOf(
                candidate);
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

        animator.SetTrigger(
            SonicOverdriveHash);
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

    #region Utility

    private Vector3 GetBlastPosition()
    {
        return
            blastOrigin != null
                ? blastOrigin.position
                : transform.position;
    }

    private static bool IsFiniteVector(
        Vector3 value)
    {
        return
            float.IsFinite(
                value.x) &&
            float.IsFinite(
                value.y) &&
            float.IsFinite(
                value.z);
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Vector3 position =
            blastOrigin != null
                ? blastOrigin.position
                : transform.position;

        if (!IsFiniteVector(
                position))
        {
            return;
        }

        Gizmos.DrawWireSphere(
            position,
            blastRadius);
    }

    #endregion
}