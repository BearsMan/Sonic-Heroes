using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StageSwitch : MonoBehaviour
{
    #region Types

    public enum SwitchType
    {
        Button,
        FloorPlate,
        Lever,
        Target,
        Timed,
        Toggle,
        TeamAction,
        External
    }

    public enum ActivationMode
    {
        Once,
        Reusable,
        Toggle,
        Hold,
        Timed
    }

    public enum TargetAction
    {
        None,
        ActivateObject,
        DeactivateObject,
        ToggleObject,
        EnableBehaviour,
        DisableBehaviour,
        ToggleBehaviour,
        EnableCollider,
        DisableCollider,
        ToggleCollider,
        AnimatorTrigger,
        AnimatorBoolTrue,
        AnimatorBoolFalse
    }

    [Serializable]
    public class TargetBinding
    {
        [SerializeField]
        private TargetAction action =
            TargetAction.None;

        [SerializeField]
        private GameObject targetObject;

        [SerializeField]
        private Behaviour targetBehaviour;

        [SerializeField]
        private Collider targetCollider;

        [SerializeField]
        private Animator targetAnimator;

        [SerializeField]
        private string animatorParameter;

        public void Apply(
            bool active)
        {
            switch (action)
            {
                case TargetAction.None:
                    break;

                case TargetAction.ActivateObject:
                    if (targetObject != null)
                    {
                        targetObject.SetActive(
                            true);
                    }
                    break;

                case TargetAction.DeactivateObject:
                    if (targetObject != null)
                    {
                        targetObject.SetActive(
                            false);
                    }
                    break;

                case TargetAction.ToggleObject:
                    if (targetObject != null)
                    {
                        targetObject.SetActive(
                            !targetObject.activeSelf);
                    }
                    break;

                case TargetAction.EnableBehaviour:
                    if (targetBehaviour != null)
                    {
                        targetBehaviour.enabled =
                            true;
                    }
                    break;

                case TargetAction.DisableBehaviour:
                    if (targetBehaviour != null)
                    {
                        targetBehaviour.enabled =
                            false;
                    }
                    break;

                case TargetAction.ToggleBehaviour:
                    if (targetBehaviour != null)
                    {
                        targetBehaviour.enabled =
                            !targetBehaviour.enabled;
                    }
                    break;

                case TargetAction.EnableCollider:
                    if (targetCollider != null)
                    {
                        targetCollider.enabled =
                            true;
                    }
                    break;

                case TargetAction.DisableCollider:
                    if (targetCollider != null)
                    {
                        targetCollider.enabled =
                            false;
                    }
                    break;

                case TargetAction.ToggleCollider:
                    if (targetCollider != null)
                    {
                        targetCollider.enabled =
                            !targetCollider.enabled;
                    }
                    break;

                case TargetAction.AnimatorTrigger:
                    SetAnimatorTrigger();
                    break;

                case TargetAction.AnimatorBoolTrue:
                    SetAnimatorBool(
                        true);
                    break;

                case TargetAction.AnimatorBoolFalse:
                    SetAnimatorBool(
                        false);
                    break;
            }
        }

        public void Restore(
            bool active)
        {
            switch (action)
            {
                case TargetAction.ActivateObject:
                    if (targetObject != null)
                    {
                        targetObject.SetActive(
                            active);
                    }
                    break;

                case TargetAction.DeactivateObject:
                    if (targetObject != null)
                    {
                        targetObject.SetActive(
                            !active);
                    }
                    break;

                case TargetAction.EnableBehaviour:
                    if (targetBehaviour != null)
                    {
                        targetBehaviour.enabled =
                            active;
                    }
                    break;

                case TargetAction.DisableBehaviour:
                    if (targetBehaviour != null)
                    {
                        targetBehaviour.enabled =
                            !active;
                    }
                    break;

                case TargetAction.EnableCollider:
                    if (targetCollider != null)
                    {
                        targetCollider.enabled =
                            active;
                    }
                    break;

                case TargetAction.DisableCollider:
                    if (targetCollider != null)
                    {
                        targetCollider.enabled =
                            !active;
                    }
                    break;

                case TargetAction.AnimatorBoolTrue:
                case TargetAction.AnimatorBoolFalse:
                    SetAnimatorBool(
                        active);
                    break;
            }
        }

        private void SetAnimatorTrigger()
        {
            if (targetAnimator == null ||
                !targetAnimator.isActiveAndEnabled ||
                targetAnimator.runtimeAnimatorController ==
                    null ||
                string.IsNullOrWhiteSpace(
                    animatorParameter))
            {
                return;
            }

            foreach (
                AnimatorControllerParameter parameter
                in targetAnimator.parameters)
            {
                if (parameter.name !=
                        animatorParameter ||
                    parameter.type !=
                        AnimatorControllerParameterType.Trigger)
                {
                    continue;
                }

                targetAnimator.SetTrigger(
                    animatorParameter);

                return;
            }
        }

        private void SetAnimatorBool(
            bool value)
        {
            if (targetAnimator == null ||
                !targetAnimator.isActiveAndEnabled ||
                targetAnimator.runtimeAnimatorController ==
                    null ||
                string.IsNullOrWhiteSpace(
                    animatorParameter))
            {
                return;
            }

            foreach (
                AnimatorControllerParameter parameter
                in targetAnimator.parameters)
            {
                if (parameter.name !=
                        animatorParameter ||
                    parameter.type !=
                        AnimatorControllerParameterType.Bool)
                {
                    continue;
                }

                targetAnimator.SetBool(
                    animatorParameter,
                    value);

                return;
            }
        }
    }

    #endregion

    #region Registry

    private static readonly HashSet<StageSwitch> allSwitches =
        new();

    public static int SwitchCount =>
        allSwitches.Count;

    public static int ActivatedSwitchCount
    {
        get
        {
            int count =
                0;

            foreach (StageSwitch stageSwitch
                in allSwitches)
            {
                if (stageSwitch != null &&
                    stageSwitch.isActivated)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public static bool AreAllSwitchesActivated
    {
        get
        {
            if (allSwitches.Count == 0)
            {
                return false;
            }

            foreach (StageSwitch stageSwitch
                in allSwitches)
            {
                if (stageSwitch == null ||
                    !stageSwitch.isActivated)
                {
                    return false;
                }
            }

            return true;
        }
    }

    #endregion

    #region Switch Configuration

    [Header("Switch")]

    [SerializeField]
    private SwitchType switchType =
        SwitchType.Button;

    [SerializeField]
    private ActivationMode activationMode =
        ActivationMode.Once;

    [SerializeField]
    private string switchGroup =
        "Default";

    #endregion

    #region Activation

    [Header("Activation")]

    [SerializeField]
    private bool activateOnPlayerContact = true;

    [SerializeField]
    private bool requireInteractionInput;

    [SerializeField]
    private KeyCode interactionKey =
        KeyCode.E;

    [SerializeField, Min(0f)]
    private float cooldown = 0.25f;

    [SerializeField, Min(0f)]
    private float timedDuration = 3f;

    #endregion

    #region Team Rules

    [Header("Team Rules")]

    [SerializeField]
    private bool requireSpecificCharacterType;

    [SerializeField]
    private CHARACTERTYPES requiredCharacterType =
        CHARACTERTYPES.Speed;

    #endregion

    #region Visual Movement

    [Header("Visual Movement")]

    [SerializeField]
    private Transform switchHandle;

    [SerializeField]
    private Vector3 activePositionOffset =
        Vector3.zero;

    [SerializeField]
    private Vector3 activeRotationOffset =
        new(70f, 0f, 0f);

    [SerializeField, Min(0f)]
    private float movementDuration =
        0.25f;

    #endregion

    #region Targets

    [Header("Targets")]

    [SerializeField]
    private TargetBinding[] targets =
        Array.Empty<TargetBinding>();

    [SerializeField]
    private bool waitForAllSwitches;

    [SerializeField]
    private bool onlyCheckOwnGroup = true;

    #endregion

    #region Presentation

    [Header("Presentation")]

    [SerializeField]
    private Animator animator;

    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip activationSound;

    [SerializeField]
    private AudioClip deactivationSound;

    [SerializeField]
    private GameObject activationEffect;

    [SerializeField]
    private GameObject deactivationEffect;

    [SerializeField, Min(0f)]
    private float effectLifetime = 3f;

    #endregion

    #region Runtime State

    private Vector3 startingHandlePosition;
    private Quaternion startingHandleRotation;

    private Coroutine stateRoutine;

    private float cooldownTimer;
    private float timedTimer;

    private int activatorCount;

    private bool isActivated;

    #endregion

    #region Properties

    public SwitchType Type =>
        switchType;

    public ActivationMode Mode =>
        activationMode;

    public string Group =>
        switchGroup;

    public bool IsActivated =>
        isActivated;

    public bool PlayerInRange =>
        activatorCount > 0;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
        CacheStartingState();
    }

    private void OnEnable()
    {
        allSwitches.Add(
            this);
    }

    private void OnDisable()
    {
        allSwitches.Remove(
            this);

        CancelRoutine();

        activatorCount =
            0;
    }

    private void OnDestroy()
    {
        allSwitches.Remove(
            this);
    }

    private void Update()
    {
        UpdateCooldown();
        UpdateTimedSwitch();
        UpdateInteraction();
    }

    private void OnValidate()
    {
        cooldown =
            Mathf.Max(
                0f,
                cooldown);

        timedDuration =
            Mathf.Max(
                0f,
                timedDuration);

        movementDuration =
            Mathf.Max(
                0f,
                movementDuration);

        effectLifetime =
            Mathf.Max(
                0f,
                effectLifetime);

        targets ??=
            Array.Empty<TargetBinding>();

        if (string.IsNullOrWhiteSpace(
                switchGroup))
        {
            switchGroup =
                "Default";
        }
    }

    #endregion

    #region Trigger Detection

    private void OnTriggerEnter(
        Collider other)
    {
        if (!IsValidActivator(
                other))
        {
            return;
        }

        activatorCount++;

        switch (switchType)
        {
            case SwitchType.FloorPlate:
                ActivateSwitch();
                break;

            case SwitchType.Button:
            case SwitchType.Lever:
                if (activateOnPlayerContact &&
                    !requireInteractionInput)
                {
                    ActivateSwitch();
                }
                break;

            case SwitchType.Timed:
                if (activateOnPlayerContact)
                {
                    ActivateSwitch();
                }
                break;

            case SwitchType.Toggle:
                if (activateOnPlayerContact &&
                    !requireInteractionInput)
                {
                    ToggleSwitch();
                }
                break;

            case SwitchType.TeamAction:
                if (activateOnPlayerContact &&
                    CanUseTeamActionSwitch())
                {
                    ActivateSwitch();
                }
                break;
        }
    }

    private void OnTriggerExit(
        Collider other)
    {
        if (!IsValidActivator(
                other))
        {
            return;
        }

        activatorCount =
            Mathf.Max(
                0,
                activatorCount - 1);

        if (activationMode ==
                ActivationMode.Hold &&
            activatorCount == 0)
        {
            DeactivateSwitch();
        }

        if (switchType ==
                SwitchType.FloorPlate &&
            activationMode !=
                ActivationMode.Once &&
            activatorCount == 0)
        {
            DeactivateSwitch();
        }
    }

    #endregion

    #region Public API

    public bool ActivateSwitch()
    {
        if (!CanActivate())
        {
            return false;
        }

        if (activationMode ==
                ActivationMode.Toggle &&
            isActivated)
        {
            return
                DeactivateSwitch();
        }

        CancelRoutine();

        stateRoutine =
            StartCoroutine(
                SetSwitchStateRoutine(
                    true));

        return true;
    }

    public bool DeactivateSwitch()
    {
        if (!isActivated)
        {
            return false;
        }

        if (activationMode ==
            ActivationMode.Once)
        {
            return false;
        }

        CancelRoutine();

        stateRoutine =
            StartCoroutine(
                SetSwitchStateRoutine(
                    false));

        return true;
    }

    public bool ToggleSwitch()
    {
        return
            isActivated
                ? DeactivateSwitch()
                : ActivateSwitch();
    }

    public void ActivateExternally()
    {
        if (switchType ==
            SwitchType.External)
        {
            ActivateSwitch();
        }
    }

    public void DeactivateExternally()
    {
        if (switchType ==
            SwitchType.External)
        {
            DeactivateSwitch();
        }
    }

    public void ResetSwitch()
    {
        CancelRoutine();

        isActivated =
            false;

        cooldownTimer =
            0f;

        timedTimer =
            0f;

        activatorCount =
            0;

        RestoreHandle();

        RestoreTargets();
    }

    public static void ResetAllSwitches()
    {
        foreach (StageSwitch stageSwitch
            in allSwitches)
        {
            if (stageSwitch != null)
            {
                stageSwitch.ResetSwitch();
            }
        }
    }

    #endregion

    #region State

    private IEnumerator SetSwitchStateRoutine(
        bool active)
    {
        if (active)
        {
            PlaySound(
                activationSound);

            SpawnEffect(
                activationEffect);
        }
        else
        {
            PlaySound(
                deactivationSound);

            SpawnEffect(
                deactivationEffect);
        }

        yield return
            MoveHandle(
                active);

        isActivated =
            active;

        if (active)
        {
            if (activationMode ==
                ActivationMode.Timed)
            {
                timedTimer =
                    timedDuration;
            }

            if (!waitForAllSwitches ||
                AreRequiredSwitchesActivated())
            {
                ExecuteTargets(
                    true);
            }

            NotifySwitchGroup();
        }
        else
        {
            ExecuteTargets(
                false);

            cooldownTimer =
                cooldown;
        }

        stateRoutine =
            null;
    }

    #endregion

    #region Global Switch Checking

    private void NotifySwitchGroup()
    {
        foreach (StageSwitch stageSwitch
            in allSwitches)
        {
            if (stageSwitch == null ||
                !stageSwitch.waitForAllSwitches)
            {
                continue;
            }

            if (stageSwitch.onlyCheckOwnGroup &&
                !string.Equals(
                    stageSwitch.switchGroup,
                    switchGroup,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (stageSwitch.AreRequiredSwitchesActivated())
            {
                stageSwitch.ExecuteTargets(
                    true);
            }
        }
    }

    private bool AreRequiredSwitchesActivated()
    {
        bool foundSwitch =
            false;

        foreach (StageSwitch stageSwitch
            in allSwitches)
        {
            if (stageSwitch == null)
            {
                continue;
            }

            if (onlyCheckOwnGroup &&
                !string.Equals(
                    stageSwitch.switchGroup,
                    switchGroup,
                    StringComparison.Ordinal))
            {
                continue;
            }

            foundSwitch =
                true;

            if (!stageSwitch.isActivated)
            {
                return false;
            }
        }

        return
            foundSwitch;
    }

    #endregion

    #region Activation Rules

    private bool CanActivate()
    {
        if (isActivated &&
            activationMode !=
                ActivationMode.Toggle)
        {
            return false;
        }

        if (cooldownTimer > 0f)
        {
            return false;
        }

        if (switchType ==
                SwitchType.TeamAction &&
            !CanUseTeamActionSwitch())
        {
            return false;
        }

        return true;
    }

    private bool CanUseTeamActionSwitch()
    {
        if (!requireSpecificCharacterType)
        {
            return true;
        }

        CharacterSwitch characterSwitch =
            FindAnyObjectByType<CharacterSwitch>();

        if (characterSwitch == null)
        {
            return false;
        }

        return
            characterSwitch.currentCharacterType ==
            requiredCharacterType;
    }

    private bool IsValidActivator(
        Collider other)
    {
        if (other == null)
        {
            return false;
        }

        UltimatePlayerMovement movement =
            other.GetComponentInParent<
                UltimatePlayerMovement>();

        return
            movement != null &&
            movement.isActiveAndEnabled;
    }

    #endregion

    #region Input

    private void UpdateInteraction()
    {
        if (!requireInteractionInput ||
            !PlayerInRange ||
            !Input.GetKeyDown(
                interactionKey))
        {
            return;
        }

        switch (switchType)
        {
            case SwitchType.Toggle:
                ToggleSwitch();
                break;

            case SwitchType.External:
                break;

            default:
                ActivateSwitch();
                break;
        }
    }

    #endregion

    #region Timers

    private void UpdateCooldown()
    {
        if (cooldownTimer <= 0f)
        {
            return;
        }

        cooldownTimer =
            Mathf.Max(
                0f,
                cooldownTimer -
                    Time.deltaTime);
    }

    private void UpdateTimedSwitch()
    {
        if (!isActivated ||
            activationMode !=
                ActivationMode.Timed)
        {
            return;
        }

        timedTimer =
            Mathf.Max(
                0f,
                timedTimer -
                    Time.deltaTime);

        if (timedTimer <= 0f)
        {
            DeactivateSwitch();
        }
    }

    #endregion

    #region Handle

    private IEnumerator MoveHandle(
        bool active)
    {
        if (switchHandle == null)
        {
            yield break;
        }

        Vector3 startPosition =
            switchHandle.localPosition;

        Quaternion startRotation =
            switchHandle.localRotation;

        Vector3 destinationPosition =
            active
                ? startingHandlePosition +
                    activePositionOffset
                : startingHandlePosition;

        Quaternion destinationRotation =
            active
                ? startingHandleRotation *
                    Quaternion.Euler(
                        activeRotationOffset)
                : startingHandleRotation;

        if (movementDuration <= 0f)
        {
            switchHandle.localPosition =
                destinationPosition;

            switchHandle.localRotation =
                destinationRotation;

            yield break;
        }

        float elapsed =
            0f;

        while (elapsed <
            movementDuration)
        {
            elapsed +=
                Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsed /
                        movementDuration);

            progress =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    progress);

            switchHandle.localPosition =
                Vector3.Lerp(
                    startPosition,
                    destinationPosition,
                    progress);

            switchHandle.localRotation =
                Quaternion.Slerp(
                    startRotation,
                    destinationRotation,
                    progress);

            yield return null;
        }

        switchHandle.localPosition =
            destinationPosition;

        switchHandle.localRotation =
            destinationRotation;
    }

    private void RestoreHandle()
    {
        if (switchHandle == null)
        {
            return;
        }

        switchHandle.localPosition =
            startingHandlePosition;

        switchHandle.localRotation =
            startingHandleRotation;
    }

    #endregion

    #region Targets

    private void ExecuteTargets(
        bool active)
    {
        if (targets == null)
        {
            return;
        }

        foreach (TargetBinding target
            in targets)
        {
            target?.Apply(
                active);
        }
    }

    private void RestoreTargets()
    {
        if (targets == null)
        {
            return;
        }

        foreach (TargetBinding target
            in targets)
        {
            target?.Restore(
                false);
        }
    }

    #endregion

    #region References

    private void ResolveReferences()
    {
        animator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        audioSource ??=
            GetComponent<AudioSource>();

        audioSource ??=
            GetComponentInChildren<AudioSource>(
                includeInactive: true);
    }

    private void CacheStartingState()
    {
        if (switchHandle == null)
        {
            return;
        }

        startingHandlePosition =
            switchHandle.localPosition;

        startingHandleRotation =
            switchHandle.localRotation;
    }

    #endregion

    #region Presentation

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

    private void SpawnEffect(
        GameObject effect)
    {
        if (effect == null)
        {
            return;
        }

        GameObject instance =
            Instantiate(
                effect,
                transform.position,
                transform.rotation);

        if (effectLifetime > 0f)
        {
            Destroy(
                instance,
                effectLifetime);
        }
    }

    #endregion

    #region Cleanup

    private void CancelRoutine()
    {
        if (stateRoutine == null)
        {
            return;
        }

        StopCoroutine(
            stateRoutine);

        stateRoutine =
            null;
    }

    #endregion
}