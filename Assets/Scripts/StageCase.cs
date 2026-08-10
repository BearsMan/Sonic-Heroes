using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StageCase : MonoBehaviour
{
    #region Types

    public enum CaseType
    {
        Item,
        RandomItem,
        MultipleItems,
        Rings,
        PowerUp,
        SwitchActivator,
        Breakable,
        Reward,
        External
    }

    public enum ActivationType
    {
        PlayerContact,
        PlayerAttack,
        Interaction,
        External
    }

    public enum CaseState
    {
        Closed,
        Opening,
        Opened,
        Resetting,
        Disabled
    }

    #endregion

    #region Registry

    private static readonly HashSet<StageCase> allCases =
        new();

    public static int CaseCount =>
        allCases.Count;

    public static int OpenedCaseCount
    {
        get
        {
            int count =
                0;

            foreach (StageCase stageCase
                in allCases)
            {
                if (stageCase != null &&
                    stageCase.isOpened)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public static bool AreAllCasesOpened
    {
        get
        {
            if (allCases.Count == 0)
            {
                return false;
            }

            foreach (StageCase stageCase
                in allCases)
            {
                if (stageCase == null ||
                    !stageCase.isOpened)
                {
                    return false;
                }
            }

            return true;
        }
    }

    #endregion

    #region Case

    [Header("Case")]

    [SerializeField]
    private CaseType caseType =
        CaseType.Item;

    [SerializeField]
    private ActivationType activationType =
        ActivationType.PlayerContact;

    [SerializeField]
    private string caseGroup =
        "Default";

    #endregion

    #region Contents

    [Header("Contents")]

    [SerializeField]
    private GameObject[] contents =
        Array.Empty<GameObject>();

    [SerializeField]
    private Transform spawnPoint;

    [SerializeField, Min(1)]
    private int spawnAmount = 1;

    [SerializeField]
    private bool allowDuplicateRandomItems = true;

    [SerializeField, Min(0f)]
    private float itemLaunchForce = 5f;

    [SerializeField, Min(0f)]
    private float itemSpread = 0.5f;

    #endregion

    #region Switches

    [Header("Switches")]

    [SerializeField]
    private StageSwitch[] linkedSwitches =
        Array.Empty<StageSwitch>();

    [SerializeField]
    private bool activateLinkedSwitches;

    [SerializeField]
    private bool requireAllLinkedSwitches;

    #endregion

    #region Activation

    [Header("Activation")]

    [SerializeField]
    private KeyCode interactionKey =
        KeyCode.E;

    [SerializeField]
    private bool allowTeamMembers = true;

    [SerializeField]
    private bool openOnlyOnce = true;

    [SerializeField, Min(0f)]
    private float openDelay;

    [SerializeField, Min(0f)]
    private float resetDelay = 2f;

    #endregion

    #region Visuals

    [Header("Visuals")]

    [SerializeField]
    private Transform caseVisual;

    [SerializeField]
    private Vector3 openedPositionOffset =
        Vector3.zero;

    [SerializeField]
    private Vector3 openedRotationOffset =
        new(0f, 0f, 90f);

    [SerializeField, Min(0f)]
    private float openingDuration = 0.25f;

    #endregion

    #region Collision

    [Header("Collision")]

    [SerializeField]
    private Collider activationCollider;

    [SerializeField]
    private Collider solidCollider;

    #endregion

    #region Animation

    [Header("Animation")]

    [SerializeField]
    private Animator animator;

    [SerializeField]
    private string openTrigger =
        "Open";

    [SerializeField]
    private string resetTrigger =
        "Reset";

    #endregion

    #region Audio

    [Header("Audio")]

    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip openSound;

    [SerializeField]
    private AudioClip itemSpawnSound;

    [SerializeField]
    private AudioClip resetSound;

    #endregion

    #region Effects

    [Header("Effects")]

    [SerializeField]
    private GameObject openEffect;

    [SerializeField]
    private GameObject spawnEffect;

    [SerializeField]
    private GameObject resetEffect;

    [SerializeField, Min(0f)]
    private float effectLifetime = 3f;

    #endregion

    #region State

    [Header("State")]

    [SerializeField]
    private CaseState currentState =
        CaseState.Closed;

    private Vector3 startingVisualPosition;
    private Quaternion startingVisualRotation;

    private Coroutine caseRoutine;

    private bool playerInRange;
    private bool isOpened;

    #endregion

    #region Properties

    public CaseType Type =>
        caseType;

    public ActivationType Activation =>
        activationType;

    public CaseState CurrentState =>
        currentState;

    public string Group =>
        caseGroup;

    public bool IsOpened =>
        isOpened;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
        CacheStartingState();
    }

    private void OnEnable()
    {
        allCases.Add(
            this);

        if (currentState ==
            CaseState.Disabled)
        {
            currentState =
                isOpened
                    ? CaseState.Opened
                    : CaseState.Closed;
        }
    }

    private void OnDisable()
    {
        allCases.Remove(
            this);

        CancelRoutine();

        playerInRange =
            false;

        currentState =
            CaseState.Disabled;
    }

    private void OnDestroy()
    {
        allCases.Remove(
            this);
    }

    private void Update()
    {
        if (activationType !=
                ActivationType.Interaction ||
            !playerInRange ||
            isOpened)
        {
            return;
        }

        if (Input.GetKeyDown(
                interactionKey))
        {
            OpenCase();
        }
    }

    private void OnValidate()
    {
        spawnAmount =
            Mathf.Max(
                1,
                spawnAmount);

        itemLaunchForce =
            Mathf.Max(
                0f,
                itemLaunchForce);

        itemSpread =
            Mathf.Max(
                0f,
                itemSpread);

        openDelay =
            Mathf.Max(
                0f,
                openDelay);

        resetDelay =
            Mathf.Max(
                0f,
                resetDelay);

        openingDuration =
            Mathf.Max(
                0f,
                openingDuration);

        effectLifetime =
            Mathf.Max(
                0f,
                effectLifetime);

        contents ??=
            Array.Empty<GameObject>();

        linkedSwitches ??=
            Array.Empty<StageSwitch>();

        if (string.IsNullOrWhiteSpace(
                caseGroup))
        {
            caseGroup =
                "Default";
        }
    }

    #endregion

    #region Trigger Detection

    private void OnTriggerEnter(
        Collider other)
    {
        if (!IsValidPlayer(
                other))
        {
            return;
        }

        playerInRange =
            true;

        if (activationType ==
            ActivationType.PlayerContact)
        {
            OpenCase();
        }
    }

    private void OnTriggerExit(
        Collider other)
    {
        if (!IsValidPlayer(
                other))
        {
            return;
        }

        playerInRange =
            false;
    }

    private bool IsValidPlayer(
        Collider candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        UltimatePlayerMovement movement =
            candidate.GetComponentInParent<
                UltimatePlayerMovement>();

        if (movement != null &&
            movement.isActiveAndEnabled)
        {
            return true;
        }

        if (!allowTeamMembers)
        {
            return false;
        }

        FollowerNavigation follower =
            candidate.GetComponentInParent<
                FollowerNavigation>();

        return
            follower != null &&
            follower.isActiveAndEnabled;
    }

    #endregion

    #region Public API

    public bool OpenCase()
    {
        if (!CanOpen())
        {
            return false;
        }

        CancelRoutine();

        caseRoutine =
            StartCoroutine(
                OpenRoutine());

        return true;
    }

    public bool OpenFromAttack()
    {
        if (activationType !=
            ActivationType.PlayerAttack)
        {
            return false;
        }

        return OpenCase();
    }

    public bool OpenExternally()
    {
        if (activationType !=
            ActivationType.External)
        {
            return false;
        }

        return OpenCase();
    }

    public bool ResetCase()
    {
        if (!isOpened &&
            currentState ==
                CaseState.Closed)
        {
            return false;
        }

        CancelRoutine();

        caseRoutine =
            StartCoroutine(
                ResetRoutine());

        return true;
    }

    public static void ResetAllCases()
    {
        foreach (StageCase stageCase
            in allCases)
        {
            if (stageCase != null)
            {
                stageCase.ResetCase();
            }
        }
    }

    public static bool AreGroupCasesOpened(
        string group)
    {
        if (string.IsNullOrWhiteSpace(
                group))
        {
            return false;
        }

        bool found =
            false;

        foreach (StageCase stageCase
            in allCases)
        {
            if (stageCase == null ||
                !string.Equals(
                    stageCase.caseGroup,
                    group,
                    StringComparison.Ordinal))
            {
                continue;
            }

            found =
                true;

            if (!stageCase.isOpened)
            {
                return false;
            }
        }

        return found;
    }

    #endregion

    #region Opening

    private bool CanOpen()
    {
        if (currentState ==
                CaseState.Opening ||
            currentState ==
                CaseState.Resetting ||
            currentState ==
                CaseState.Disabled)
        {
            return false;
        }

        if (isOpened &&
            openOnlyOnce)
        {
            return false;
        }

        return true;
    }

    private IEnumerator OpenRoutine()
    {
        currentState =
            CaseState.Opening;

        if (openDelay > 0f)
        {
            yield return
                new WaitForSeconds(
                    openDelay);
        }

        PlayAnimatorTrigger(
            openTrigger);

        PlaySound(
            openSound);

        SpawnEffect(
            openEffect,
            transform.position);

        yield return
            AnimateVisual(
                true);

        isOpened =
            true;

        currentState =
            CaseState.Opened;

        if (activationCollider != null)
        {
            activationCollider.enabled =
                false;
        }

        ExecuteCaseContents();

        if (activateLinkedSwitches)
        {
            ActivateSwitches();
        }

        caseRoutine =
            null;

        if (!openOnlyOnce &&
            resetDelay > 0f)
        {
            caseRoutine =
                StartCoroutine(
                    DelayedResetRoutine());
        }
    }

    #endregion

    #region Case Contents

    private void ExecuteCaseContents()
    {
        switch (caseType)
        {
            case CaseType.Item:
                SpawnFirstItem();
                break;

            case CaseType.RandomItem:
                SpawnRandomItems();
                break;

            case CaseType.MultipleItems:
                SpawnAllItems();
                break;

            case CaseType.Rings:
            case CaseType.PowerUp:
            case CaseType.Reward:
                SpawnConfiguredItems();
                break;

            case CaseType.SwitchActivator:
                ActivateSwitches();
                break;

            case CaseType.Breakable:
                SpawnConfiguredItems();
                break;

            case CaseType.External:
                SpawnConfiguredItems();
                break;
        }
    }

    private void SpawnFirstItem()
    {
        if (contents == null ||
            contents.Length == 0)
        {
            return;
        }

        foreach (GameObject item
            in contents)
        {
            if (item == null)
            {
                continue;
            }

            SpawnItem(
                item,
                0);

            return;
        }
    }

    private void SpawnRandomItems()
    {
        if (contents == null ||
            contents.Length == 0)
        {
            return;
        }

        HashSet<int> usedIndices =
            new();

        for (int count = 0;
            count < spawnAmount;
            count++)
        {
            int index =
                GetRandomValidIndex(
                    usedIndices);

            if (index < 0)
            {
                break;
            }

            if (!allowDuplicateRandomItems)
            {
                usedIndices.Add(
                    index);
            }

            SpawnItem(
                contents[index],
                count);
        }
    }

    private int GetRandomValidIndex(
        HashSet<int> usedIndices)
    {
        if (contents == null ||
            contents.Length == 0)
        {
            return -1;
        }

        int start =
            UnityEngine.Random.Range(
                0,
                contents.Length);

        for (int offset = 0;
            offset < contents.Length;
            offset++)
        {
            int index =
                (start + offset) %
                contents.Length;

            if (contents[index] == null)
            {
                continue;
            }

            if (!allowDuplicateRandomItems &&
                usedIndices.Contains(
                    index))
            {
                continue;
            }

            return index;
        }

        return -1;
    }

    private void SpawnAllItems()
    {
        if (contents == null)
        {
            return;
        }

        int spawnIndex =
            0;

        foreach (GameObject item
            in contents)
        {
            if (item == null)
            {
                continue;
            }

            SpawnItem(
                item,
                spawnIndex);

            spawnIndex++;
        }
    }

    private void SpawnConfiguredItems()
    {
        if (contents == null ||
            contents.Length == 0)
        {
            return;
        }

        for (int index = 0;
            index < spawnAmount;
            index++)
        {
            GameObject item =
                contents[
                    index %
                    contents.Length];

            if (item != null)
            {
                SpawnItem(
                    item,
                    index);
            }
        }
    }

    private void SpawnItem(
        GameObject prefab,
        int index)
    {
        if (prefab == null)
        {
            return;
        }

        Transform origin =
            spawnPoint != null
                ? spawnPoint
                : transform;

        Vector3 offset =
            GetSpawnOffset(
                index);

        Vector3 position =
            origin.position +
            offset;

        if (!IsFiniteVector(
                position))
        {
            return;
        }

        GameObject instance =
            Instantiate(
                prefab,
                position,
                origin.rotation);

        PlaySound(
            itemSpawnSound);

        SpawnEffect(
            spawnEffect,
            position);

        Rigidbody body =
            instance.GetComponent<Rigidbody>();

        body ??=
            instance.GetComponentInChildren<Rigidbody>();

        if (body != null &&
            !body.isKinematic &&
            itemLaunchForce > 0f)
        {
            Vector3 launchDirection =
                Vector3.up +
                offset.normalized *
                    0.25f;

            if (launchDirection.sqrMagnitude <=
                0.0001f)
            {
                launchDirection =
                    Vector3.up;
            }

            body.AddForce(
                launchDirection.normalized *
                    itemLaunchForce,
                ForceMode.Impulse);
        }
    }

    private Vector3 GetSpawnOffset(
        int index)
    {
        if (itemSpread <= 0f ||
            index <= 0)
        {
            return
                Vector3.zero;
        }

        float angle =
            index *
            137.5f *
            Mathf.Deg2Rad;

        return
            new Vector3(
                Mathf.Cos(angle),
                0f,
                Mathf.Sin(angle)) *
            itemSpread;
    }

    #endregion

    #region Switches

    private void ActivateSwitches()
    {
        if (linkedSwitches == null ||
            linkedSwitches.Length == 0)
        {
            return;
        }

        int activated =
            0;

        int valid =
            0;

        foreach (StageSwitch stageSwitch
            in linkedSwitches)
        {
            if (stageSwitch == null)
            {
                continue;
            }

            valid++;

            if (stageSwitch.ActivateSwitch())
            {
                activated++;
            }
            else if (stageSwitch.IsActivated)
            {
                activated++;
            }
        }

        if (requireAllLinkedSwitches &&
            valid > 0 &&
            activated < valid)
        {
            return;
        }
    }

    #endregion

    #region Reset

    private IEnumerator DelayedResetRoutine()
    {
        yield return
            new WaitForSeconds(
                resetDelay);

        yield return
            ResetRoutine();
    }

    private IEnumerator ResetRoutine()
    {
        currentState =
            CaseState.Resetting;

        PlayAnimatorTrigger(
            resetTrigger);

        PlaySound(
            resetSound);

        SpawnEffect(
            resetEffect,
            transform.position);

        yield return
            AnimateVisual(
                false);

        isOpened =
            false;

        if (activationCollider != null)
        {
            activationCollider.enabled =
                true;
        }

        if (solidCollider != null)
        {
            solidCollider.enabled =
                true;
        }

        currentState =
            CaseState.Closed;

        caseRoutine =
            null;
    }

    #endregion

    #region Visual Animation

    private IEnumerator AnimateVisual(
        bool opening)
    {
        if (caseVisual == null)
        {
            yield break;
        }

        Vector3 startPosition =
            caseVisual.localPosition;

        Quaternion startRotation =
            caseVisual.localRotation;

        Vector3 targetPosition =
            opening
                ? startingVisualPosition +
                    openedPositionOffset
                : startingVisualPosition;

        Quaternion targetRotation =
            opening
                ? startingVisualRotation *
                    Quaternion.Euler(
                        openedRotationOffset)
                : startingVisualRotation;

        if (openingDuration <= 0f)
        {
            caseVisual.localPosition =
                targetPosition;

            caseVisual.localRotation =
                targetRotation;

            yield break;
        }

        float elapsed =
            0f;

        while (elapsed <
            openingDuration)
        {
            elapsed +=
                Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsed /
                        openingDuration);

            progress =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    progress);

            caseVisual.localPosition =
                Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    progress);

            caseVisual.localRotation =
                Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    progress);

            yield return null;
        }

        caseVisual.localPosition =
            targetPosition;

        caseVisual.localRotation =
            targetRotation;
    }

    #endregion

    #region References

    private void ResolveReferences()
    {
        spawnPoint ??=
            transform;

        caseVisual ??=
            transform;

        activationCollider ??=
            GetComponent<Collider>();

        animator ??=
            GetComponentInChildren<Animator>(
                includeInactive: true);

        audioSource ??=
            GetComponent<AudioSource>();

        audioSource ??=
            GetComponentInChildren<AudioSource>(
                includeInactive: true);

        contents ??=
            Array.Empty<GameObject>();

        linkedSwitches ??=
            Array.Empty<StageSwitch>();
    }

    private void CacheStartingState()
    {
        if (caseVisual == null)
        {
            return;
        }

        startingVisualPosition =
            caseVisual.localPosition;

        startingVisualRotation =
            caseVisual.localRotation;
    }

    #endregion

    #region Animation

    private void PlayAnimatorTrigger(
        string parameter)
    {
        if (animator == null ||
            !animator.isActiveAndEnabled ||
            animator.runtimeAnimatorController ==
                null ||
            string.IsNullOrWhiteSpace(
                parameter))
        {
            return;
        }

        foreach (
            AnimatorControllerParameter animatorParameter
            in animator.parameters)
        {
            if (animatorParameter.name !=
                    parameter ||
                animatorParameter.type !=
                    AnimatorControllerParameterType.Trigger)
            {
                continue;
            }

            animator.SetTrigger(
                parameter);

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

    #region Cleanup

    private void CancelRoutine()
    {
        if (caseRoutine == null)
        {
            return;
        }

        StopCoroutine(
            caseRoutine);

        caseRoutine =
            null;
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
}