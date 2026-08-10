using System.Collections;
using UnityEngine;

public class SuperSonicPower : MonoBehaviour
{
    #region Animator

    private static readonly int SuperSonicPowerHash =
        Animator.StringToHash(
            "Super Sonic Power");

    #endregion

    #region References

    [Header("Super Team Sonic")]

    [SerializeField]
    private TeamActionController actionController;

    [SerializeField]
    private Transform superSonic;

    [SerializeField]
    private Transform superTails;

    [SerializeField]
    private Transform superKnuckles;

    [SerializeField]
    private Animator animator;

    [SerializeField]
    private AudioSource audioSource;

    #endregion

    #region Metal Overlord

    [Header("Metal Overlord")]

    [SerializeField]
    private MetalOverlord metalOverlord;

    [SerializeField, Min(1)]
    private int bossHits = 1;

    [SerializeField, Min(0f)]
    private float bossHitInterval = 0.15f;

    #endregion

    #region Attack

    [Header("Super Sonic Power")]

    [SerializeField, Min(0f)]
    private float startupDuration = 0.35f;

    [SerializeField, Min(0f)]
    private float attackDuration = 1.25f;

    [SerializeField, Min(0f)]
    private float recoveryDuration = 0.35f;

    #endregion

    #region Effects

    [Header("Effects")]

    [SerializeField]
    private Transform effectOrigin;

    [SerializeField]
    private GameObject activationEffect;

    [SerializeField]
    private GameObject bossImpactEffect;

    [SerializeField, Min(0f)]
    private float effectLifetime = 5f;

    #endregion

    #region Audio

    [Header("Audio")]

    [SerializeField]
    private AudioClip activationSound;

    [SerializeField]
    private AudioClip bossImpactSound;

    [SerializeField]
    private AudioClip completionSound;

    #endregion

    #region Runtime

    private Coroutine powerRoutine;

    private bool isInitialized;
    private bool isActive;
    private bool bossWasHit;

    #endregion

    #region Properties

    public bool IsInitialized =>
        isInitialized;

    public bool IsActive =>
        isActive;

    public bool HasMetalOverlordTarget =>
        metalOverlord != null;

    public bool BossWasHit =>
        bossWasHit;

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

    private void OnDestroy()
    {
        Cancel();

        actionController =
            null;

        superSonic =
            null;

        superTails =
            null;

        superKnuckles =
            null;

        animator =
            null;

        audioSource =
            null;

        effectOrigin =
            null;

        metalOverlord =
            null;

        activationEffect =
            null;

        bossImpactEffect =
            null;

        isInitialized =
            false;
    }

    private void OnValidate()
    {
        bossHits =
            Mathf.Max(
                1,
                bossHits);

        bossHitInterval =
            Mathf.Max(
                0f,
                bossHitInterval);

        startupDuration =
            Mathf.Max(
                0f,
                startupDuration);

        attackDuration =
            Mathf.Max(
                0f,
                attackDuration);

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

    #region Initialization

    public bool Initialize()
    {
        if (isInitialized)
        {
            return true;
        }

        ResolveReferences();

        if (actionController == null ||
            metalOverlord == null)
        {
            return false;
        }

        ResolveSuperTeam();

        if (!HasValidSuperTeam())
        {
            return false;
        }

        isActive =
            false;

        bossWasHit =
            false;

        powerRoutine =
            null;

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

        effectOrigin ??=
            transform;

        if (metalOverlord == null)
        {
            metalOverlord =
                FindAnyObjectByType<
                    MetalOverlord>();
        }
    }

    private void ResolveSuperTeam()
    {
        if (actionController == null)
        {
            return;
        }

        superSonic ??=
            actionController.SpeedCharacter;

        superTails ??=
            actionController.FlyCharacter;

        superKnuckles ??=
            actionController.PowerCharacter;
    }

    private bool HasValidSuperTeam()
    {
        return
            superSonic != null &&
            superTails != null &&
            superKnuckles != null &&
            superSonic.gameObject.activeInHierarchy &&
            superTails.gameObject.activeInHierarchy &&
            superKnuckles.gameObject.activeInHierarchy;
    }

    #endregion

    #region Public API

    public bool TryActivate()
    {
        if (!CanActivate())
        {
            return false;
        }

        bossWasHit =
            false;

        isActive =
            true;

        powerRoutine =
            StartCoroutine(
                SuperSonicPowerRoutine());

        return true;
    }

    public void Cancel()
    {
        if (powerRoutine != null)
        {
            StopCoroutine(
                powerRoutine);

            powerRoutine =
                null;
        }

        if (!isActive)
        {
            return;
        }

        isActive =
            false;

        RestoreTeamControl();
    }

    #endregion

    #region Activation

    private bool CanActivate()
    {
        if (!isInitialized ||
            isActive ||
            actionController == null)
        {
            return false;
        }

        if (metalOverlord == null ||
            !metalOverlord.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!HasValidSuperTeam())
        {
            return false;
        }

        return true;
    }

    private IEnumerator SuperSonicPowerRoutine()
    {
        LockTeamControl();

        PlayAnimation();

        PlaySound(
            activationSound);

        SpawnEffect(
            activationEffect,
            GetEffectPosition());

        if (startupDuration > 0f)
        {
            yield return
                new WaitForSeconds(
                    startupDuration);
        }

        if (metalOverlord == null)
        {
            FinishPower();

            yield break;
        }

        yield return
            HitMetalOverlord();

        if (attackDuration > 0f)
        {
            yield return
                new WaitForSeconds(
                    attackDuration);
        }

        if (recoveryDuration > 0f)
        {
            yield return
                new WaitForSeconds(
                    recoveryDuration);
        }

        FinishPower();
    }

    #endregion

    #region Boss Attack

    private IEnumerator HitMetalOverlord()
    {
        for (int hitIndex = 0;
            hitIndex < bossHits;
            hitIndex++)
        {
            if (metalOverlord == null ||
                !metalOverlord.gameObject.activeInHierarchy)
            {
                yield break;
            }

            metalOverlord.OnTeamBlastHit();

            bossWasHit =
                true;

            SpawnEffect(
                bossImpactEffect,
                metalOverlord.transform.position);

            PlaySound(
                bossImpactSound);

            if (hitIndex <
                    bossHits - 1 &&
                bossHitInterval > 0f)
            {
                yield return
                    new WaitForSeconds(
                        bossHitInterval);
            }
        }
    }

    #endregion

    #region Team Control

    private void LockTeamControl()
    {
        SetCharacterMovement(
            superSonic,
            false);

        SetCharacterMovement(
            superTails,
            false);

        SetCharacterMovement(
            superKnuckles,
            false);
    }

    private void RestoreTeamControl()
    {
        SetCharacterMovement(
            superSonic,
            true);

        SetCharacterMovement(
            superTails,
            true);

        SetCharacterMovement(
            superKnuckles,
            true);
    }

    private static void SetCharacterMovement(
        Transform character,
        bool enabled)
    {
        if (character == null)
        {
            return;
        }

        UltimatePlayerMovement movement =
            character.GetComponent<
                UltimatePlayerMovement>();

        movement ??=
            character.GetComponentInParent<
                UltimatePlayerMovement>();

        movement ??=
            character.GetComponentInChildren<
                UltimatePlayerMovement>(
                    includeInactive: true);

        if (movement == null)
        {
            return;
        }

        if (enabled)
        {
            movement.EnableMovement();
        }
        else
        {
            movement.DisableMovement();
        }
    }

    #endregion

    #region Completion

    private void FinishPower()
    {
        if (!isActive)
        {
            return;
        }

        isActive =
            false;

        powerRoutine =
            null;

        RestoreTeamControl();

        PlaySound(
            completionSound);
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
            SuperSonicPowerHash);
    }

    #endregion

    #region Effects

    private Vector3 GetEffectPosition()
    {
        if (effectOrigin != null &&
            IsFiniteVector(
                effectOrigin.position))
        {
            return
                effectOrigin.position;
        }

        return
            transform.position;
    }

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

    #region Validation

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
}