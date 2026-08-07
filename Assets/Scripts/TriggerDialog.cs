using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class TriggerDialog : MonoBehaviour
{
    [Header("Dialog")]
    [SerializeField] private List<AudioClip> dialogs = new();
    [SerializeField] private AudioSource omochaoTriggerDisable;
    [SerializeField] private bool isPaused = false;

    private bool dialogRead;
    private Coroutine dialogCoroutine;
    private UltimatePlayerMovement playerMovement;

    private void Awake()
    {
        playerMovement = FindAnyObjectByType<UltimatePlayerMovement>();

        if (omochaoTriggerDisable == null)
        {
            omochaoTriggerDisable = GetComponent<AudioSource>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (dialogRead ||
            dialogCoroutine != null ||
            !other.CompareTag("Player"))
        {
            return;
        }

        dialogRead = true;
        dialogCoroutine = StartCoroutine(ReadDialog());
    }

    private IEnumerator ReadDialog()
    {
        if (playerMovement == null)
        {
            Debug.LogError(
                $"{name}: UltimatePlayerMovement not found.",
                this);

            dialogRead = false;
            dialogCoroutine = null;
            yield break;
        }

        if (omochaoTriggerDisable == null)
        {
            Debug.LogError(
                $"{name}: AudioSource missing.",
                this);

            dialogRead = false;
            dialogCoroutine = null;
            yield break;
        }

        if (dialogs.Count == 0)
        {
            Debug.LogWarning(
                $"{name}: No dialog clips assigned.",
                this);

            dialogRead = false;
            dialogCoroutine = null;
            yield break;
        }

        playerMovement.DisableMovement();

        try
        {
            foreach (AudioClip clip in dialogs)
            {
                if (clip == null)
                {
                    continue;
                }

                omochaoTriggerDisable.clip = clip;
                omochaoTriggerDisable.Play();

                while (omochaoTriggerDisable.isPlaying || isPaused)
                {
                    yield return null;
                }
            }
        }
        finally
        {
            if (omochaoTriggerDisable != null)
            {
                omochaoTriggerDisable.clip = null;
            }

            dialogCoroutine = null;
            playerMovement?.EnableMovement();
        }
    }

    public void PauseDialog()
    {
        isPaused = true;
    }

    public void ResumeDialog()
    {
        isPaused = false;
    }
    private void OnDisable()
    {
        if (dialogCoroutine != null)
        {
            StopCoroutine(dialogCoroutine);
            dialogCoroutine = null;
        }

        if (omochaoTriggerDisable != null)
        {
            omochaoTriggerDisable.Stop();
            omochaoTriggerDisable.clip = null;
        }
        playerMovement?.EnableMovement();
    }
}