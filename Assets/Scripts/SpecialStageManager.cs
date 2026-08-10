using UnityEngine;
using UnityEngine.SceneManagement;

public class SpecialStageManager : MonoBehaviour
{
    #region Types

    public enum ChaosEmerald
    {
        Green = 0,
        Blue = 1,
        Yellow = 2,
        White = 3,
        LightBlue = 4,
        Purple = 5,
        Red = 6
    }

    #endregion

    #region Special Stage

    [Header("Special Stage")]

    [SerializeField, Range(1, 7)]
    private int specialStageNumber = 1;

    [SerializeField]
    private ChaosEmerald rewardEmerald =
        ChaosEmerald.Green;

    #endregion

    #region Gate Key

    [Header("Gate Key")]

    [SerializeField]
    private bool requireGateKey = true;

    [SerializeField]
    private bool playerHasGateKey;

    #endregion

    #region Timer

    [Header("Timer")]

    [SerializeField]
    private bool useTimeLimit = true;

    [SerializeField, Min(1f)]
    private float timeLimit = 90f;

    #endregion

    #region Scene Return

    [Header("Scene Return")]

    [SerializeField]
    private string returnScene;

    [SerializeField, Min(0f)]
    private float returnDelay = 1f;

    #endregion

    #region Audio

    [Header("Audio")]

    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip startSound;

    [SerializeField]
    private AudioClip successSound;

    [SerializeField]
    private AudioClip failureSound;

    #endregion

    #region Effects

    [Header("Effects")]

    [SerializeField]
    private GameObject successEffect;

    [SerializeField]
    private GameObject failureEffect;

    [SerializeField, Min(0f)]
    private float effectLifetime = 3f;

    #endregion

    #region Runtime State

    private float remainingTime;

    private bool stageStarted;
    private bool finished;
    private bool returnScheduled;

    #endregion

    #region Properties

    public int SpecialStageNumber =>
        specialStageNumber;

    public ChaosEmerald RewardEmerald =>
        rewardEmerald;

    public float RemainingTime =>
        remainingTime;

    public bool HasStarted =>
        stageStarted;

    public bool IsFinished =>
        finished;

    public bool HasGateKey =>
        playerHasGateKey;

    public bool EmeraldAlreadyCollected =>
        IsEmeraldCollected(
            rewardEmerald);

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();

        EnsureEmeraldCollection();

        remainingTime =
            timeLimit;
    }

    private void Update()
    {
        if (!stageStarted ||
            finished ||
            !useTimeLimit)
        {
            return;
        }

        remainingTime =
            Mathf.Max(
                0f,
                remainingTime -
                Time.deltaTime);

        if (remainingTime <= 0f)
        {
            FailStage();
        }
    }

    private void OnValidate()
    {
        specialStageNumber =
            Mathf.Clamp(
                specialStageNumber,
                1,
                7);

        timeLimit =
            Mathf.Max(
                1f,
                timeLimit);

        returnDelay =
            Mathf.Max(
                0f,
                returnDelay);

        effectLifetime =
            Mathf.Max(
                0f,
                effectLifetime);
    }

    #endregion

    #region Gate Key

    public void GiveGateKey()
    {
        playerHasGateKey =
            true;
    }

    public void RemoveGateKey()
    {
        playerHasGateKey =
            false;
    }

    public void SetGateKey(
        bool hasKey)
    {
        playerHasGateKey =
            hasKey;
    }

    #endregion

    #region Stage Entry

    public bool TryStartSpecialStage()
    {
        if (stageStarted ||
            finished)
        {
            return false;
        }

        if (requireGateKey &&
            !playerHasGateKey)
        {
            return false;
        }

        if (requireGateKey)
        {
            playerHasGateKey =
                false;
        }

        remainingTime =
            timeLimit;

        stageStarted =
            true;

        PlaySound(
            startSound);

        return true;
    }

    #endregion

    #region Completion

    public bool CompleteStage()
    {
        if (!stageStarted ||
            finished)
        {
            return false;
        }

        finished =
            true;

        stageStarted =
            false;

        AwardChaosEmerald();

        PlaySound(
            successSound);

        SpawnEffect(
            successEffect);

        ScheduleReturn();

        return true;
    }

    private void AwardChaosEmerald()
    {
        EnsureEmeraldCollection();

        int index =
            (int)rewardEmerald;

        if (!IsValidEmeraldIndex(
                index))
        {
            return;
        }

        GameInstance.emerald[
            index] =
            true;
    }

    #endregion

    #region Failure

    public bool FailStage()
    {
        if (!stageStarted ||
            finished)
        {
            return false;
        }

        finished =
            true;

        stageStarted =
            false;

        PlaySound(
            failureSound);

        SpawnEffect(
            failureEffect);

        ScheduleReturn();

        return true;
    }

    #endregion

    #region Emerald Progress

    public bool IsEmeraldCollected(
        ChaosEmerald emerald)
    {
        EnsureEmeraldCollection();

        int index =
            (int)emerald;

        return
            IsValidEmeraldIndex(
                index) &&
            GameInstance.emerald[
                index];
    }

    public int GetCollectedEmeraldCount()
    {
        EnsureEmeraldCollection();

        int count =
            0;

        for (int index = 0;
            index < 7;
            index++)
        {
            if (GameInstance.emerald[
                    index])
            {
                count++;
            }
        }

        return count;
    }

    public bool HasAllChaosEmeralds()
    {
        EnsureEmeraldCollection();

        for (int index = 0;
            index < 7;
            index++)
        {
            if (!GameInstance.emerald[
                    index])
            {
                return false;
            }
        }

        return true;
    }

    private static void EnsureEmeraldCollection()
    {
        if (GameInstance.emerald != null &&
            GameInstance.emerald.Length >= 7)
        {
            return;
        }

        bool[] previous =
            GameInstance.emerald;

        GameInstance.emerald =
            new bool[7];

        if (previous == null)
        {
            return;
        }

        int copyCount =
            Mathf.Min(
                previous.Length,
                GameInstance.emerald.Length);

        for (int index = 0;
            index < copyCount;
            index++)
        {
            GameInstance.emerald[
                index] =
                previous[index];
        }
    }

    private static bool IsValidEmeraldIndex(
        int index)
    {
        return
            GameInstance.emerald != null &&
            index >= 0 &&
            index < 7 &&
            index <
                GameInstance.emerald.Length;
    }

    #endregion

    #region Scene Return

    public void SetReturnScene(
        string sceneName)
    {
        returnScene =
            sceneName;
    }

    private void ScheduleReturn()
    {
        if (returnScheduled)
        {
            return;
        }

        returnScheduled =
            true;

        if (returnDelay <= 0f)
        {
            ReturnFromSpecialStage();

            return;
        }

        Invoke(
            nameof(ReturnFromSpecialStage),
            returnDelay);
    }

    private void ReturnFromSpecialStage()
    {
        if (string.IsNullOrWhiteSpace(
                returnScene))
        {
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(
                returnScene))
        {
            return;
        }

        SceneManager.LoadScene(
            returnScene);
    }

    #endregion

    #region References

    private void ResolveReferences()
    {
        audioSource ??=
            GetComponent<AudioSource>();

        audioSource ??=
            GetComponentInChildren<AudioSource>(
                includeInactive: true);
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
        GameObject effect)
    {
        if (effect == null ||
            !IsFiniteVector(
                transform.position))
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