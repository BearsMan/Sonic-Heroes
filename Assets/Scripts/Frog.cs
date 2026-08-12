using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class Frog : MonoBehaviour
{
    #region Enums

    public enum FrogType
    {
        Green,
        Black
    }

    public enum FrogState
    {
        Idle,
        Detected,
        Croaking,
        Raining,
        Leaving,
        Inactive
    }

    #endregion

    #region References

    [Header("References")]
    [SerializeField]
    private Animator animator;

    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private Collider detectionVolume;

    [SerializeField]
    private Transform frogVisual;

    #endregion

    #region Frog Type

    [Header("Frog Type")]
    [SerializeField]
    private FrogType frogType =
        FrogType.Green;

    #endregion

    #region Detection

    [Header("Detection")]
    [SerializeField]
    private bool detectPlayer = true;

    [SerializeField, Min(0f)]
    private float activationDelay = 0.15f;

    [SerializeField]
    private bool triggerOnlyOnce = true;

    #endregion

    #region Rain

    [Header("Rain")]
    [SerializeField]
    private ParticleSystem greenRainEffect;

    [SerializeField]
    private ParticleSystem blackRainEffect;

    [SerializeField, Min(0.1f)]
    private float rainDuration = 4f;

    [SerializeField, Min(0f)]
    private float rainStartDelay = 0.35f;

    #endregion

    #region Audio

    [Header("Audio")]
    [SerializeField]
    private AudioClip greenCroak;

    [SerializeField]
    private AudioClip blackCroak;

    #endregion

    #region Animation

    [Header("Animation")]
    [SerializeField]
    private string croakTrigger =
        "Croak";

    [SerializeField]
    private string jumpTrigger =
        "Jump";

    #endregion

    #region Departure

    [Header("Departure")]
    [SerializeField]
    private bool leaveAfterRain = true;

    [SerializeField, Min(0f)]
    private float leaveDelay = 0.25f;

    [SerializeField, Min(0f)]
    private float leaveJumpHeight = 6f;

    [SerializeField, Min(0f)]
    private float leaveDistance = 10f;

    [SerializeField, Min(0.1f)]
    private float leaveDuration = 0.8f;

    [SerializeField]
    private Vector3 leaveDirection =
        Vector3.forward;

    #endregion

    #region Power Hit

    [Header("Power Character Interaction")]
    [SerializeField]
    private bool leaveImmediatelyWhenHit =
        true;

    #endregion

    #region Stage Events

    [Header("Stage Events")]

    [Tooltip(
        "Called when a green frog's rain begins. " +
        "Connect plant-growth and platform systems here.")]
    [SerializeField]
    private UnityEvent onGreenRain;

    [Tooltip(
        "Called when a black frog's rain begins. " +
        "Connect plant-withering and falling-fruit systems here.")]
    [SerializeField]
    private UnityEvent onBlackRain;

    [Tooltip(
        "Called when this frog detects the team. " +
        "Useful for Team Chaotix Frog Forest missions.")]
    [SerializeField]
    private UnityEvent onPlayerDetected;

    #endregion

    #region Runtime

    private FrogState currentState =
        FrogState.Idle;

    private Coroutine activationRoutine;
    private Coroutine leaveRoutine;

    private Vector3 startingPosition;
    private Quaternion startingRotation;

    private bool activated;
    private bool rainActive;
    private bool initialized;
    private bool shuttingDown;

    #endregion

    #region Properties

    public FrogType Type =>
        frogType;

    public FrogState State =>
        currentState;

    public bool HasActivated =>
        activated;

    public bool IsRaining =>
        rainActive;

    public bool IsGreen =>
        frogType == FrogType.Green;

    public bool IsBlack =>
        frogType == FrogType.Black;

    public event Action<Frog> Detected;

    public event Action<Frog, FrogType>
        RainStarted;

    public event Action<Frog, FrogType>
        RainEnded;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheReferences();

        startingPosition =
            transform.position;

        startingRotation =
            transform.rotation;

        ConfigureDetectionVolume();

        initialized =
            true;
    }

    private void OnEnable()
    {
        if (shuttingDown)
        {
            return;
        }

        if (!initialized)
        {
            CacheReferences();
            ConfigureDetectionVolume();

            initialized =
                true;
        }
    }

    private void OnDisable()
    {
        StopRuntimeCoroutines();
        StopRainEffects();

        rainActive =
            false;
    }

    private void OnDestroy()
    {
        shuttingDown =
            true;

        StopRuntimeCoroutines();

        Detected =
            null;

        RainStarted =
            null;

        RainEnded =
            null;
    }

    private void OnValidate()
    {
        activationDelay =
            Mathf.Max(
                0f,
                activationDelay);

        rainDuration =
            Mathf.Max(
                0.1f,
                rainDuration);

        rainStartDelay =
            Mathf.Max(
                0f,
                rainStartDelay);

        leaveDelay =
            Mathf.Max(
                0f,
                leaveDelay);

        leaveJumpHeight =
            Mathf.Max(
                0f,
                leaveJumpHeight);

        leaveDistance =
            Mathf.Max(
                0f,
                leaveDistance);

        leaveDuration =
            Mathf.Max(
                0.1f,
                leaveDuration);
    }

    #endregion

    #region Detection

    private void OnTriggerEnter(
        Collider other)
    {
        if (!CanDetectPlayer(
            other))
        {
            return;
        }

        ActivateFrog();
    }

    private bool CanDetectPlayer(
        Collider other)
    {
        if (!initialized ||
            !detectPlayer ||
            other == null)
        {
            return false;
        }

        if (currentState ==
                FrogState.Leaving ||
            currentState ==
                FrogState.Inactive)
        {
            return false;
        }

        if (triggerOnlyOnce &&
            activated)
        {
            return false;
        }

        UltimatePlayerMovement player =
            other.GetComponentInParent<
                UltimatePlayerMovement>();

        return
            player != null &&
            player.gameObject.activeInHierarchy;
    }

    #endregion

    #region Activation

    public void ActivateFrog()
    {
        if (!initialized ||
            shuttingDown)
        {
            return;
        }

        if (triggerOnlyOnce &&
            activated)
        {
            return;
        }

        if (activationRoutine != null)
        {
            return;
        }

        activated =
            true;

        currentState =
            FrogState.Detected;

        onPlayerDetected?.Invoke();

        Detected?.Invoke(
            this);

        activationRoutine =
            StartCoroutine(
                ActivationRoutine());
    }

    private IEnumerator ActivationRoutine()
    {
        if (activationDelay > 0f)
        {
            yield return
                new WaitForSeconds(
                    activationDelay);
        }

        if (shuttingDown)
        {
            yield break;
        }

        currentState =
            FrogState.Croaking;

        PlayCroak();
        PlayCroakAnimation();

        if (rainStartDelay > 0f)
        {
            yield return
                new WaitForSeconds(
                    rainStartDelay);
        }

        if (shuttingDown)
        {
            yield break;
        }

        StartRain();

        yield return
            new WaitForSeconds(
                rainDuration);

        StopRain();

        activationRoutine =
            null;

        if (leaveAfterRain)
        {
            BeginLeave();
        }
        else
        {
            currentState =
                FrogState.Idle;
        }
    }

    #endregion

    #region Rain

    private void StartRain()
    {
        rainActive =
            true;

        currentState =
            FrogState.Raining;

        ParticleSystem effect =
            GetRainEffect();

        if (effect != null &&
            !effect.isPlaying)
        {
            effect.Play();
        }

        switch (frogType)
        {
            case FrogType.Green:
                onGreenRain?.Invoke();
                break;

            case FrogType.Black:
                onBlackRain?.Invoke();
                break;
        }

        RainStarted?.Invoke(
            this,
            frogType);
    }

    private void StopRain()
    {
        if (!rainActive)
        {
            return;
        }

        rainActive =
            false;

        StopRainEffects();

        RainEnded?.Invoke(
            this,
            frogType);
    }

    private ParticleSystem GetRainEffect()
    {
        return frogType switch
        {
            FrogType.Green =>
                greenRainEffect,

            FrogType.Black =>
                blackRainEffect,

            _ =>
                null
        };
    }

    private void StopRainEffects()
    {
        StopEffect(
            greenRainEffect);

        StopEffect(
            blackRainEffect);
    }

    private static void StopEffect(
        ParticleSystem effect)
    {
        if (effect == null ||
            !effect.isPlaying)
        {
            return;
        }

        effect.Stop(
            true,
            ParticleSystemStopBehavior
                .StopEmitting);
    }

    #endregion

    #region Croak

    private void PlayCroak()
    {
        if (audioSource == null)
        {
            return;
        }

        AudioClip clip =
            frogType switch
            {
                FrogType.Green =>
                    greenCroak,

                FrogType.Black =>
                    blackCroak,

                _ =>
                    null
            };

        if (clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(
            clip);
    }

    private void PlayCroakAnimation()
    {
        SetAnimatorTrigger(
            croakTrigger);
    }

    #endregion

    #region Power Interaction

    public void HitByPowerCharacter()
    {
        if (!initialized ||
            shuttingDown)
        {
            return;
        }

        if (!activated)
        {
            ActivateFrog();
        }

        if (!leaveImmediatelyWhenHit)
        {
            return;
        }

        /*
         * Do not stop the rain.
         * The frog may leave while its
         * environmental rain effect continues.
         */
        BeginLeave(
            preserveRain: true);
    }

    #endregion

    #region Departure

    private void BeginLeave(
        bool preserveRain = false)
    {
        if (leaveRoutine != null ||
            currentState ==
                FrogState.Leaving ||
            currentState ==
                FrogState.Inactive)
        {
            return;
        }

        if (activationRoutine != null &&
            preserveRain)
        {
            /*
             * Keep the rain lifecycle alive while
             * allowing the visual frog to leave.
             */
        }
        else if (activationRoutine != null)
        {
            StopCoroutine(
                activationRoutine);

            activationRoutine =
                null;
        }

        leaveRoutine =
            StartCoroutine(
                LeaveRoutine());
    }

    private IEnumerator LeaveRoutine()
    {
        currentState =
            FrogState.Leaving;

        SetAnimatorTrigger(
            jumpTrigger);

        if (leaveDelay > 0f)
        {
            yield return
                new WaitForSeconds(
                    leaveDelay);
        }

        Vector3 start =
            transform.position;

        Vector3 direction =
            GetLeaveDirection();

        Vector3 destination =
            start +
            direction *
            leaveDistance;

        float elapsed =
            0f;

        while (elapsed <
            leaveDuration)
        {
            float normalized =
                Mathf.Clamp01(
                    elapsed /
                    leaveDuration);

            Vector3 position =
                Vector3.Lerp(
                    start,
                    destination,
                    normalized);

            float arc =
                4f *
                normalized *
                (1f - normalized);

            position.y +=
                arc *
                leaveJumpHeight;

            if (IsFinite(
                position))
            {
                transform.position =
                    position;
            }

            elapsed +=
                Time.deltaTime;

            yield return null;
        }

        if (IsFinite(
            destination))
        {
            transform.position =
                destination;
        }

        currentState =
            FrogState.Inactive;

        if (detectionVolume != null)
        {
            detectionVolume.enabled =
                false;
        }

        if (frogVisual != null)
        {
            frogVisual.gameObject.SetActive(
                false);
        }

        leaveRoutine =
            null;
    }

    private Vector3 GetLeaveDirection()
    {
        if (!IsFinite(
            leaveDirection) ||
            leaveDirection.sqrMagnitude <=
            Mathf.Epsilon)
        {
            return transform.forward;
        }

        Vector3 direction =
            transform.TransformDirection(
                leaveDirection.normalized);

        direction.y =
            0f;

        if (direction.sqrMagnitude <=
            Mathf.Epsilon)
        {
            direction =
                transform.forward;
        }

        return direction.normalized;
    }

    #endregion

    #region Reset

    public void ResetFrog()
    {
        StopRuntimeCoroutines();
        StopRainEffects();

        transform.position =
            startingPosition;

        transform.rotation =
            startingRotation;

        if (frogVisual != null)
        {
            frogVisual.gameObject.SetActive(
                true);
        }

        if (detectionVolume != null)
        {
            detectionVolume.enabled =
                true;
        }

        rainActive =
            false;

        activated =
            false;

        currentState =
            FrogState.Idle;
    }

    #endregion

    #region Setup

    private void CacheReferences()
    {
        if (animator == null)
        {
            animator =
                GetComponentInChildren<
                    Animator>();
        }

        if (audioSource == null)
        {
            audioSource =
                GetComponent<AudioSource>();
        }

        if (detectionVolume == null)
        {
            detectionVolume =
                GetComponent<Collider>();
        }

        if (frogVisual == null)
        {
            frogVisual =
                transform;
        }
    }

    private void ConfigureDetectionVolume()
    {
        if (detectionVolume == null)
        {
            return;
        }

        detectionVolume.isTrigger =
            true;
    }

    #endregion

    #region Animation

    private void SetAnimatorTrigger(
        string parameter)
    {
        if (animator == null ||
            !animator.isActiveAndEnabled ||
            string.IsNullOrWhiteSpace(
                parameter))
        {
            return;
        }

        animator.SetTrigger(
            parameter);
    }

    #endregion

    #region Cleanup

    private void StopRuntimeCoroutines()
    {
        if (activationRoutine != null)
        {
            StopCoroutine(
                activationRoutine);

            activationRoutine =
                null;
        }

        if (leaveRoutine != null)
        {
            StopCoroutine(
                leaveRoutine);

            leaveRoutine =
                null;
        }
    }

    #endregion

    #region Safety

    private static bool IsFinite(
        Vector3 value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }

    #endregion
}