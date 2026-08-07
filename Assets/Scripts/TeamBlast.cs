using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class TeamBlast : MonoBehaviour
{
    #region Constants

    private const float MinimumGaugeValue = 0f;
    private const float MinimumDuration = 0.05f;

    #endregion

    #region Animator Hashes

    private static readonly int TeamBlastHash =
        Animator.StringToHash("Team Blast");

    private static readonly int SonicOverdriveHash =
        Animator.StringToHash("Sonic Overdrive");

    private static readonly int ChaosInfernoHash =
        Animator.StringToHash("Chaos Inferno");

    private static readonly int FlowerFestivalHash =
        Animator.StringToHash("Flower Festival");

    private static readonly int ChaotixRecitalHash =
        Animator.StringToHash("Chaotix Recital");

    private static readonly int SuperSonicPowerHash =
        Animator.StringToHash("Super Sonic Power");

    #endregion

    #region Enums

    public enum TeamBlastType
    {
        SonicOverdrive,
        ChaosInferno,
        FlowerFestival,
        ChaotixRecital,
        SuperSonicPower
    }

    #endregion

    #region Inspector

    [Header("References")]
    [SerializeField] private TeamActionController actionController;
    [SerializeField] private CharacterSwitch characterSwitch;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Transform blastOrigin;
    [SerializeField] private SonicOverdrive sonicOverdrive;
    [SerializeField] private ChaosInferno chaosInferno;
    [SerializeField] private FlowerFestival flowerFestival;
    [SerializeField] private ChaotixRecital chaotixRecital;
    [SerializeField] private SuperSonicPower superSonicPower;

    [Header("Team Blast")]
    [SerializeField]
    private TeamBlastType currentTeamBlast =
        TeamBlastType.SonicOverdrive;

    [SerializeField] private bool deriveBlastFromPlayableTeam = true;
    [SerializeField] private bool allowSuperSonicPower;
    [SerializeField] private KeyCode teamBlastKey = KeyCode.Z;
    [SerializeField] private bool readPlayerInput = true;

    [Header("Gauge")]
    [SerializeField, Min(1f)] private float maxGauge = 100f;
    [SerializeField, Min(0f)] private float startingGauge;
    [SerializeField] private bool consumeGaugeOnUse = true;

    [Header("Shared Blast")]
    [SerializeField, Min(MinimumDuration)]
    private float actionDuration = 0.75f;

    [SerializeField, Min(0f)]
    private float invincibilityDuration = 5f;

    [Header("Effects")]
    [SerializeField] private GameObject sonicOverdriveEffect;
    [SerializeField] private GameObject chaosInfernoEffect;
    [SerializeField] private GameObject flowerFestivalEffect;
    [SerializeField] private GameObject chaotixRecitalEffect;
    [SerializeField] private GameObject superSonicPowerEffect;
    [SerializeField, Min(0f)] private float effectLifetime = 4f;

    [Header("Audio")]
    [SerializeField] private AudioClip sonicOverdriveSound;
    [SerializeField] private AudioClip chaosInfernoSound;
    [SerializeField] private AudioClip flowerFestivalSound;
    [SerializeField] private AudioClip chaotixRecitalSound;
    [SerializeField] private AudioClip superSonicPowerSound;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    #endregion

    #region Runtime State

    private Coroutine blastRoutine;
    private Coroutine invincibilityRoutine;

    private bool isPerformingTeamBlast;
    private bool isInvincible;
    private bool isInitialized;
    private bool isShuttingDown;

    #endregion

    #region Public API

    public TeamBlastType CurrentTeamBlast =>
        currentTeamBlast;

    public bool IsPerformingTeamBlast =>
        isPerformingTeamBlast;

    public bool IsInvincible =>
        isInvincible;

    public bool IsInitialized =>
        isInitialized;

    public float CurrentGauge =>
        Mathf.Clamp(
            GameInstance.teamBlastMeter,
            MinimumGaugeValue,
            maxGauge);

    public float MaxGauge =>
        maxGauge;

    public float GaugePercent =>
        maxGauge > 0f
            ? CurrentGauge / maxGauge
            : 0f;

    public bool BlastReady =>
        CurrentGauge >= maxGauge;

    public bool TryActivateTeamBlast()
    {
        if (!CanActivateTeamBlast())
            return false;

        bool accepted =
            actionController.TryBeginAction(
                TeamActionController.TeamAction.TeamBlast,
                actionController.CurrentFormation,
                mustBeGrounded: false,
                mustBeAirborne: false,
                surrenderMovementControl: true);

        if (!accepted)
            return false;

        BeginTeamBlast();
        return true;
    }

    private bool ValidateSelectedTeamBlast()
    {
        return currentTeamBlast switch
        {
            TeamBlastType.SonicOverdrive =>
                ValidateOptionalBlastReference(
                    sonicOverdrive,
                    nameof(SonicOverdrive)),

            TeamBlastType.ChaosInferno =>
                ValidateOptionalBlastReference(
                    chaosInferno,
                    nameof(ChaosInferno)),

            TeamBlastType.FlowerFestival =>
                ValidateOptionalBlastReference(
                    flowerFestival,
                    nameof(FlowerFestival)),

            TeamBlastType.ChaotixRecital =>
                ValidateOptionalBlastReference(
                    chaotixRecital,
                    nameof(ChaotixRecital)),

            TeamBlastType.SuperSonicPower =>
                ValidateOptionalBlastReference(
                    superSonicPower,
                    nameof(SuperSonicPower)),

            _ =>
                false
        };
    }

    private bool ValidateOptionalBlastReference(
        Object reference,
        string displayName)
    {
        if (reference != null)
            return true;

        Debug.LogWarning(
            $"TeamBlast could not find {displayName}.",
            this);

        return false;
    }

    public void CancelTeamBlast()
    {
        if (!isPerformingTeamBlast)
            return;

        FinishTeamBlast();
    }

    public void AddGauge(
        float amount)
    {
        if (!isInitialized ||
            amount <= 0f)
        {
            return;
        }

        GameInstance.teamBlastMeter =
            Mathf.Clamp(
                GameInstance.teamBlastMeter + amount,
                MinimumGaugeValue,
                maxGauge);
    }

    public void ResetGauge()
    {
        GameInstance.teamBlastMeter =
            MinimumGaugeValue;
    }

    public void SetTeamBlastType(
        TeamBlastType teamBlastType)
    {
        if (isPerformingTeamBlast)
            return;

        currentTeamBlast =
            teamBlastType;
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
        CacheComponents();
        ResolveReferences();
    }

    private void Start()
    {
        if (!InitializeTeamBlast())
        {
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (isShuttingDown)
            return;

        CacheComponents();
        ResolveReferences();

        if (isInitialized &&
            deriveBlastFromPlayableTeam)
        {
            ResolveTeamBlastFromPlayableTeam();
        }
    }

    private void Update()
    {
        if (!isInitialized ||
            !readPlayerInput ||
            isPerformingTeamBlast)
        {
            return;
        }

        if (deriveBlastFromPlayableTeam)
        {
            ResolveTeamBlastFromPlayableTeam();
        }

        if (BlastReady &&
            Input.GetKeyDown(teamBlastKey))
        {
            TryActivateTeamBlast();
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
        maxGauge =
            Mathf.Max(
                1f,
                maxGauge);

        startingGauge =
            Mathf.Clamp(
                startingGauge,
                MinimumGaugeValue,
                maxGauge);

        actionDuration =
            Mathf.Max(
                MinimumDuration,
                actionDuration);

        invincibilityDuration =
            Mathf.Max(
                0f,
                invincibilityDuration);

        effectLifetime =
            Mathf.Max(
                0f,
                effectLifetime);
    }

    #endregion

    #region Initialization

    private bool InitializeTeamBlast()
    {
        if (isInitialized)
            return true;

        CacheComponents();
        ResolveReferences();

        if (deriveBlastFromPlayableTeam)
        {
            ResolveTeamBlastFromPlayableTeam();
        }

        if (!ValidateConfiguration())
        {
            Debug.LogError(
                $"TeamBlast failed to initialize on '{name}'.",
                this);

            isInitialized = false;
            return false;
        }

        GameInstance.maxTeamBlastMeter =
            maxGauge;

        GameInstance.teamBlastMeter =
            Mathf.Clamp(
                GameInstance.teamBlastMeter > MinimumGaugeValue
                    ? GameInstance.teamBlastMeter
                    : startingGauge,
                MinimumGaugeValue,
                maxGauge);

        ResetRuntimeState();

        isInitialized = true;

        LogStateChange(
            $"TeamBlast initialized as {GetTeamBlastName()}.");

        return true;
    }

    private void CacheComponents()
    {
        Transform teamRoot =
            TeamSetup.Instance != null
                ? TeamSetup.Instance.transform
                : transform.root;

        actionController ??=
            GetComponent<TeamActionController>();

        actionController ??=
            GetComponentInParent<TeamActionController>();

        if (actionController == null &&
            teamRoot != null)
        {
            actionController =
                teamRoot.GetComponentInChildren<TeamActionController>(
                    includeInactive: true);
        }

        characterSwitch ??=
            GetComponent<CharacterSwitch>();

        characterSwitch ??=
            GetComponentInParent<CharacterSwitch>();

        if (characterSwitch == null &&
            teamRoot != null)
        {
            characterSwitch =
                teamRoot.GetComponentInChildren<CharacterSwitch>(
                    includeInactive: true);
        }

        sonicOverdrive ??=
            FindTeamBlastComponent<SonicOverdrive>(
                teamRoot);

        chaosInferno ??=
            FindTeamBlastComponent<ChaosInferno>(
                teamRoot);

        flowerFestival ??=
            FindTeamBlastComponent<FlowerFestival>(
                teamRoot);

        chaotixRecital ??=
            FindTeamBlastComponent<ChaotixRecital>(
                teamRoot);

        superSonicPower ??=
            FindTeamBlastComponent<SuperSonicPower>(
                teamRoot);

        audioSource ??=
            GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource =
                gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    private T FindTeamBlastComponent<T>(
        Transform searchRoot)
        where T : Component
    {
        T component =
            GetComponent<T>();

        component ??=
            GetComponentInParent<T>();

        if (component == null &&
            searchRoot != null)
        {
            component =
                searchRoot.GetComponentInChildren<T>(
                    includeInactive: true);
        }

        return component;
    }

    private void ResolveReferences()
    {
        animator ??=
            GetComponent<Animator>();

        animator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        if (animator == null &&
            characterSwitch != null &&
            characterSwitch.CurrentLeader != null)
        {
            animator =
                characterSwitch.CurrentLeader
                    .GetComponentInChildren<Animator>(
                        includeInactive: true);
        }

        blastOrigin ??=
            characterSwitch != null &&
            characterSwitch.CurrentLeader != null
                ? characterSwitch.CurrentLeader
                : transform;
    }

    #endregion

    #region Activation

    private bool CanActivateTeamBlast()
    {
        if (!isInitialized ||
            isPerformingTeamBlast ||
            !BlastReady ||
            actionController == null ||
            !IsTeamBlastAllowed())
        {
            return false;
        }

        if (!ValidateSelectedTeamBlast())
        {
            Debug.LogWarning(
                $"{GetTeamBlastName()} cannot activate because its behavior component is missing.",
                this);

            return false;
        }

        return true;
    }

    private bool IsTeamBlastAllowed()
    {
        if (currentTeamBlast !=
            TeamBlastType.SuperSonicPower)
        {
            return true;
        }

        return allowSuperSonicPower;
    }

    private void BeginTeamBlast()
    {
        isPerformingTeamBlast = true;

        if (consumeGaugeOnUse)
        {
            ResetGauge();
        }

        ActivateInvincibility();

        PlayTeamBlastAnimation();
        PlayTeamBlastSound();
        SpawnTeamBlastEffect();

        ApplyTeamBlastEffect();

        blastRoutine =
            StartCoroutine(
                TeamBlastRoutine());

        LogStateChange(
            $"{GetTeamBlastName()} activated.");
    }

    private IEnumerator TeamBlastRoutine()
    {
        yield return
            new WaitForSeconds(
                actionDuration);

        FinishTeamBlast();
    }

    private void FinishTeamBlast()
    {
        if (!isPerformingTeamBlast)
            return;

        isPerformingTeamBlast = false;

        if (blastRoutine != null)
        {
            StopCoroutine(
                blastRoutine);

            blastRoutine = null;
        }

        sonicOverdrive?.Cancel();
        chaosInferno?.Cancel();
        flowerFestival?.Cancel();
        chaotixRecital?.Cancel();
        superSonicPower?.Cancel();

        if (actionController != null &&
            actionController.CurrentAction ==
                TeamActionController.TeamAction.TeamBlast)
        {
            actionController.EndAction(
                restoreMovementControl: true);
        }

        LogStateChange(
            $"{GetTeamBlastName()} finished.");
    }

    private void ResetRuntimeState()
    {
        isPerformingTeamBlast = false;
        isInvincible = false;
        blastRoutine = null;
        invincibilityRoutine = null;
    }

    #endregion

    #region Team Blast Routing

    private void ApplyTeamBlastEffect()
    {
        switch (currentTeamBlast)
        {
            case TeamBlastType.SonicOverdrive:
                ApplySonicOverdrive();
                break;

            case TeamBlastType.ChaosInferno:
                ApplyChaosInferno();
                break;

            case TeamBlastType.FlowerFestival:
                ApplyFlowerFestival();
                break;

            case TeamBlastType.ChaotixRecital:
                ApplyChaotixRecital();
                break;

            case TeamBlastType.SuperSonicPower:
                ApplySuperSonicPower();
                break;

            default:
                Debug.LogError(
                    $"Unsupported Team Blast: {currentTeamBlast}.",
                    this);
                break;
        }
    }

    private void ResolveTeamBlastFromPlayableTeam()
    {
        if (TeamSetup.Instance == null)
            return;

        currentTeamBlast =
            TeamSetup.Instance.PlayableTeam switch
            {
                PlayableTeam.TeamSonic =>
                    TeamBlastType.SonicOverdrive,

                PlayableTeam.TeamDark =>
                    TeamBlastType.ChaosInferno,

                PlayableTeam.TeamRose =>
                    TeamBlastType.FlowerFestival,

                PlayableTeam.TeamChaotix =>
                    TeamBlastType.ChaotixRecital,

                _ =>
                    currentTeamBlast
            };
    }

    #endregion


    #region Sonic Overdrive
    private void ApplySonicOverdrive()
    {
        if (sonicOverdrive != null)
        {
            sonicOverdrive.TryActivate();
            return;
        }

        Debug.LogWarning(
            "TeamBlast could not activate SonicOverdrive.",
            this);
    }
    #endregion

    #region Chaos Inferno
    private void ApplyChaosInferno()
    {
        if (chaosInferno != null)
        {
            chaosInferno.TryActivate();
            return;
        }

        Debug.LogWarning(
            "TeamBlast could not activate ChaosInferno.",
            this);
    }
    #endregion

    #region Flower Festival
    private void ApplyFlowerFestival()
    {
        if (flowerFestival != null)
        {
            flowerFestival.TryActivate();
            return;
        }

        Debug.LogWarning(
            "TeamBlast could not activate FlowerFestival.",
            this);
    }
    #endregion

    #region Chaotix Recital
    private void ApplyChaotixRecital()
    {
        if (chaotixRecital != null)
        {
            chaotixRecital.TryActivate();
            return;
        }

        Debug.LogWarning(
            "TeamBlast could not activate ChaotixRecital.",
            this);
    }
    #endregion

    #region Super Sonic Power
    private void ApplySuperSonicPower()
    {
        if (superSonicPower != null)
        {
            superSonicPower.TryActivate();
            return;
        }

        Debug.LogWarning(
            "TeamBlast could not activate SuperSonicPower.",
            this);
    }

    #endregion

    #region Helpers
    private void BroadcastToTeam(
        string methodName,
        float value)
    {
        if (actionController == null)
            return;

        SendToCharacter(
            actionController.SpeedCharacter,
            methodName,
            value);

        SendToCharacter(
            actionController.FlyCharacter,
            methodName,
            value);

        SendToCharacter(
            actionController.PowerCharacter,
            methodName,
            value);
    }

    private static void SendToCharacter(
        Transform character,
        string methodName,
        float value)
    {
        if (character == null)
            return;

        character.gameObject.SendMessage(
            methodName,
            value,
            SendMessageOptions.DontRequireReceiver);
    }
    #endregion

    #region Invincibility

    private void ActivateInvincibility()
    {
        if (invincibilityDuration <= 0f)
            return;

        if (invincibilityRoutine != null)
        {
            StopCoroutine(
                invincibilityRoutine);
        }

        invincibilityRoutine =
            StartCoroutine(
                TeamBlastInvincibilityRoutine());
    }

    private IEnumerator TeamBlastInvincibilityRoutine()
    {
        isInvincible = true;

        BroadcastToTeam(
            "SetInvincible",
            1f);

        yield return
            new WaitForSeconds(
                invincibilityDuration);

        isInvincible = false;

        BroadcastToTeam(
            "SetInvincible",
            0f);

        invincibilityRoutine = null;
    }

    #endregion

    #region Effects

    private void SpawnTeamBlastEffect()
    {
        GameObject effectPrefab =
            currentTeamBlast switch
            {
                TeamBlastType.SonicOverdrive =>
                    sonicOverdriveEffect,

                TeamBlastType.ChaosInferno =>
                    chaosInfernoEffect,

                TeamBlastType.FlowerFestival =>
                    flowerFestivalEffect,

                TeamBlastType.ChaotixRecital =>
                    chaotixRecitalEffect,

                TeamBlastType.SuperSonicPower =>
                    superSonicPowerEffect,

                _ =>
                    null
            };

        if (effectPrefab == null)
            return;

        GameObject spawnedEffect =
            Instantiate(
                effectPrefab,
                GetBlastPosition(),
                transform.rotation);

        if (effectLifetime > 0f)
        {
            Destroy(
                spawnedEffect,
                effectLifetime);
        }
    }

    #endregion

    #region Animation

    private void PlayTeamBlastAnimation()
    {
        if (animator == null)
            return;

        animator.SetTrigger(
            TeamBlastHash);

        animator.SetTrigger(
            GetTeamBlastAnimationHash());
    }

    private int GetTeamBlastAnimationHash()
    {
        return currentTeamBlast switch
        {
            TeamBlastType.SonicOverdrive =>
                SonicOverdriveHash,

            TeamBlastType.ChaosInferno =>
                ChaosInfernoHash,

            TeamBlastType.FlowerFestival =>
                FlowerFestivalHash,

            TeamBlastType.ChaotixRecital =>
                ChaotixRecitalHash,

            TeamBlastType.SuperSonicPower =>
                SuperSonicPowerHash,

            _ =>
                TeamBlastHash
        };
    }

    #endregion

    #region Audio

    private void PlayTeamBlastSound()
    {
        if (audioSource == null)
            return;

        AudioClip clip =
            currentTeamBlast switch
            {
                TeamBlastType.SonicOverdrive =>
                    sonicOverdriveSound,

                TeamBlastType.ChaosInferno =>
                    chaosInfernoSound,

                TeamBlastType.FlowerFestival =>
                    flowerFestivalSound,

                TeamBlastType.ChaotixRecital =>
                    chaotixRecitalSound,

                TeamBlastType.SuperSonicPower =>
                    superSonicPowerSound,

                _ =>
                    null
            };

        if (clip != null)
        {
            audioSource.PlayOneShot(
                clip);
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

    private string GetTeamBlastName()
    {
        return currentTeamBlast switch
        {
            TeamBlastType.SonicOverdrive =>
                "Sonic Overdrive",

            TeamBlastType.ChaosInferno =>
                "Chaos Inferno",

            TeamBlastType.FlowerFestival =>
                "Flower Festival",

            TeamBlastType.ChaotixRecital =>
                "Chaotix Recital",

            TeamBlastType.SuperSonicPower =>
                "Super Sonic Power",

            _ =>
                "Team Blast"
        };
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
                audioSource,
                nameof(AudioSource));

        if (characterSwitch == null)
        {
            Debug.LogWarning(
                "TeamBlast could not find CharacterSwitch.",
                this);
        }

        if (animator == null)
        {
            Debug.LogWarning(
                "TeamBlast could not find an Animator.",
                this);
        }

        if (!ValidateSelectedTeamBlast())
        {
            Debug.LogWarning(
                $"{GetTeamBlastName()} is not currently available. " +
                "TeamBlast will remain initialized, but activation will be rejected.",
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
            $"TeamBlast requires {displayName}.",
            this);

        return false;
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        if (blastRoutine != null)
        {
            StopCoroutine(blastRoutine);
            blastRoutine = null;
        }

        if (invincibilityRoutine != null)
        {
            StopCoroutine(invincibilityRoutine);
            invincibilityRoutine = null;
        }

        if (isPerformingTeamBlast)
        {
            isPerformingTeamBlast = false;

            sonicOverdrive?.Cancel();
            chaosInferno?.Cancel();
            flowerFestival?.Cancel();
            chaotixRecital?.Cancel();
            superSonicPower?.Cancel();

            if (actionController != null &&
                actionController.CurrentAction ==
                    TeamActionController.TeamAction.TeamBlast)
            {
                actionController.EndAction(
                    restoreMovementControl: true);
            }
        }
    }

    private void CleanupDestroyedState()
    {
        CleanupRuntimeState();

        isInitialized = false;

        actionController = null;
        characterSwitch = null;
        animator = null;
        audioSource = null;
        blastOrigin = null;

        sonicOverdrive = null;
        chaosInferno = null;
        flowerFestival = null;
        chaotixRecital = null;
        superSonicPower = null;

        sonicOverdriveEffect = null;
        chaosInfernoEffect = null;
        flowerFestivalEffect = null;
        chaotixRecitalEffect = null;
        superSonicPowerEffect = null;
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
}