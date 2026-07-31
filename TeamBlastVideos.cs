using System.Collections;
using UnityEngine;
using UnityEngine.Video;

[DisallowMultipleComponent]
public sealed class TeamBlastVideos : MonoBehaviour
{
    [Header("Video")]
    [SerializeField] private GameObject teamBlastVideoPlayer;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField, Min(0f)] private float preparationTimeout = 10f;

    [Header("Audio")]
    [SerializeField] private AudioSource stageMusic;
    [SerializeField] private AudioSource omochaoDialog;

    [Header("Blast Damage")]
    [SerializeField, Min(0f)] private float blastRadius = 30f;
    [SerializeField, Min(0)] private int blastDamage = 10;
    [SerializeField, Min(0f)] private float damageDelay = 1f;

    [Header("Dependencies")]
    [SerializeField] private TeamSetup teamSetup;
    [SerializeField] private UltimatePlayerMovement playerMovement;
    [SerializeField] private StageSession stageSession;

    private Coroutine blastRoutine;
    private float stageMusicVolume;
    private float omochaoDialogVolume;
    private bool sessionWasPaused;
    private bool audioMuted;
    private bool sessionPaused;

    public bool IsPlaying => blastRoutine != null;

    private void Awake()
    {
        ResolveReferences();
        ValidateReferences();

        if (teamBlastVideoPlayer != null)
            teamBlastVideoPlayer.SetActive(false);
    }

    private void OnDisable()
    {
        if (blastRoutine != null)
        {
            StopCoroutine(blastRoutine);
            blastRoutine = null;
        }

        CleanupBlast();
    }

    public void PlayTeamBlast()
    {
        if (IsPlaying)
            return;

        ResolveReferences();

        if (!TryGetTeamBlastClip(out VideoClip teamBlastClip))
            return;

        if (videoPlayer == null ||
            teamBlastVideoPlayer == null)
        {
            Debug.LogWarning(
                "Team Blast cannot play because its video references are missing.",
                this);

            return;
        }

        blastRoutine =
            StartCoroutine(
                PlayTeamBlastRoutine(teamBlastClip));
    }

    private IEnumerator PlayTeamBlastRoutine(
        VideoClip teamBlastClip)
    {
        PauseStageSession();
        MuteAudio();

        teamBlastVideoPlayer.SetActive(true);

        videoPlayer.Stop();
        videoPlayer.clip = teamBlastClip;
        videoPlayer.Prepare();

        float elapsedPreparationTime = 0f;

        while (!videoPlayer.isPrepared)
        {
            elapsedPreparationTime +=
                Time.unscaledDeltaTime;

            if (elapsedPreparationTime >= preparationTimeout)
            {
                Debug.LogWarning(
                    $"Team Blast video preparation timed out after {preparationTimeout:0.##} seconds.",
                    this);

                FinishBlast();
                yield break;
            }

            yield return null;
        }

        videoPlayer.Play();

        while (videoPlayer.isPlaying)
            yield return null;

        teamBlastVideoPlayer.SetActive(false);

        if (damageDelay > 0f)
        {
            yield return
                new WaitForSecondsRealtime(
                    damageDelay);
        }

        ApplyBlastDamage();
        FinishBlast();
    }

    private bool TryGetTeamBlastClip(
        out VideoClip teamBlastClip)
    {
        teamBlastClip = null;

        if (teamSetup == null)
        {
            Debug.LogWarning(
                "Team Blast could not find TeamSetup.",
                this);

            return false;
        }

        if (teamSetup.CurrentTeam == null)
        {
            Debug.LogWarning(
                "Team Blast could not find the current TeamComposition.",
                this);

            return false;
        }

        teamBlastClip =
    teamSetup.CurrentTeam.TeamBlastVideo;

        if (teamBlastClip != null)
            return true;

        Debug.LogWarning(
            $"The team '{teamSetup.CurrentTeam.name}' has no Team Blast video assigned.",
            teamSetup.CurrentTeam);

        return false;
    }

    private void ApplyBlastDamage()
    {
        if (blastDamage <= 0 ||
            blastRadius <= 0f)
        {
            return;
        }

        if (playerMovement == null)
        {
            playerMovement =
                Object.FindAnyObjectByType<UltimatePlayerMovement>();
        }

        if (playerMovement == null)
        {
            Debug.LogWarning(
                "Team Blast could not find UltimatePlayerMovement, so damage was not applied.",
                this);

            return;
        }

        Vector3 playerPosition =
            playerMovement.transform.position;

        float squaredRadius =
            blastRadius * blastRadius;

        Health[] enemies = Object.FindObjectsByType<Health>(FindObjectsInactive.Exclude);

        foreach (Health enemy in enemies)
        {
            if (enemy == null)
                continue;

            float squaredDistance =
                (enemy.transform.position -
                 playerPosition).sqrMagnitude;

            if (squaredDistance <= squaredRadius)
                enemy.TakeDamage(blastDamage);
        }
    }

    private void PauseStageSession()
    {
        if (stageSession == null)
            return;

        sessionWasPaused =
            stageSession.IsPaused;

        stageSession.SetPaused(true);
        sessionPaused = true;
    }

    private void RestoreStageSession()
    {
        if (!sessionPaused ||
            stageSession == null)
        {
            return;
        }

        stageSession.SetPaused(
            sessionWasPaused);

        sessionPaused = false;
    }

    private void MuteAudio()
    {
        if (stageMusic != null)
        {
            stageMusicVolume =
                stageMusic.volume;

            stageMusic.volume = 0f;
        }

        if (omochaoDialog != null)
        {
            omochaoDialogVolume =
                omochaoDialog.volume;

            omochaoDialog.volume = 0f;
        }

        audioMuted = true;
    }

    private void RestoreAudio()
    {
        if (!audioMuted)
            return;

        if (stageMusic != null)
        {
            stageMusic.volume =
                stageMusicVolume;
        }

        if (omochaoDialog != null)
        {
            omochaoDialog.volume =
                omochaoDialogVolume;
        }

        audioMuted = false;
    }

    private void FinishBlast()
    {
        CleanupBlast();
        blastRoutine = null;
    }

    private void CleanupBlast()
    {
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
            videoPlayer.clip = null;
        }

        if (teamBlastVideoPlayer != null)
            teamBlastVideoPlayer.SetActive(false);

        RestoreAudio();
        RestoreStageSession();
    }

    private void ResolveReferences()
    {
        if (teamSetup == null)
            teamSetup = TeamSetup.Instance;

        if (teamSetup == null)
        {
            teamSetup =
                Object.FindAnyObjectByType<TeamSetup>();
        }

        if (playerMovement == null)
        {
            playerMovement =
                Object.FindAnyObjectByType<UltimatePlayerMovement>();
        }

        if (stageSession == null)
            stageSession = StageSession.Instance;

        if (stageSession == null)
        {
            stageSession =
                Object.FindAnyObjectByType<StageSession>();
        }
    }

    private void ValidateReferences()
    {
        if (teamBlastVideoPlayer == null)
        {
            Debug.LogWarning(
                "Team Blast Video Player object is not assigned.",
                this);
        }

        if (videoPlayer == null)
        {
            Debug.LogWarning(
                "Team Blast VideoPlayer component is not assigned.",
                this);
        }

        if (stageMusic == null)
        {
            Debug.LogWarning(
                "Team Blast Stage Music AudioSource is not assigned.",
                this);
        }

        if (omochaoDialog == null)
        {
            Debug.LogWarning(
                "Team Blast Omochao Dialog AudioSource is not assigned.",
                this);
        }

        if (teamSetup == null)
        {
            Debug.LogWarning(
                "Team Blast could not find TeamSetup.",
                this);
        }

        if (playerMovement == null)
        {
            Debug.LogWarning(
                "Team Blast could not find UltimatePlayerMovement.",
                this);
        }

        if (stageSession == null)
        {
            Debug.LogWarning(
                "Team Blast could not find StageSession.",
                this);
        }
    }

    private void OnValidate()
    {
        preparationTimeout =
            Mathf.Max(0f, preparationTimeout);

        blastRadius =
            Mathf.Max(0f, blastRadius);

        blastDamage =
            Mathf.Max(0, blastDamage);

        damageDelay =
            Mathf.Max(0f, damageDelay);
    }
}