using UnityEngine;

[DisallowMultipleComponent]
public sealed class StageSession : MonoBehaviour
{
    public static StageSession Instance { get; private set; }

    [SerializeField] private bool timerRunsOnStart = true;

    public float ElapsedTime { get; private set; }
    public bool TimerRunning { get; private set; }
    public bool IsPaused { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        ResetSession();
    }

    private void Start()
    {
        TimerRunning =
            timerRunsOnStart;
    }

    private void Update()
    {
        if (!TimerRunning ||
            IsPaused)
        {
            return;
        }

        ElapsedTime +=
            Time.deltaTime;
    }

    public void StartTimer()
    {
        TimerRunning = true;
    }

    public void StopTimer()
    {
        TimerRunning = false;
    }

    public void SetPaused(
        bool paused)
    {
        IsPaused = paused;
    }

    public void SetElapsedTime(
        float elapsedTime)
    {
        ElapsedTime =
            Mathf.Max(
                0f,
                elapsedTime);
    }

    public void ResetSession()
    {
        ElapsedTime = 0f;
        TimerRunning = false;
        IsPaused = false;
    }

    public void ResetTimer()
    {
        ElapsedTime = 0f;
    }
}