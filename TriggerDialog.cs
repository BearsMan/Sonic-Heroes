using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(UltimatePlayerMovement))]
[RequireComponent(typeof(AudioClip))]
[RequireComponent (typeof(AudioSource))]
public class TriggerDialog : MonoBehaviour
{
    [Header("Dialog")]
    public List<AudioClip> dialogs = new();
    public AudioSource omochaoTriggerDisable;

    [Header("Runtime")]
    public bool pause = false;

    private bool dialogRead = false;
    private UltimatePlayerMovement playerMovement;

    private void Awake()
    {
        playerMovement = FindAnyObjectByType<UltimatePlayerMovement>();

        if (omochaoTriggerDisable == null)
        {
            omochaoTriggerDisable = GetComponent<AudioSource>();
        }
    }
    private Coroutine dialogCoroutine;

    private void OnTriggerEnter(Collider other)
    {
        if (dialogRead || !other.CompareTag("Player"))
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
            playerMovement = FindAnyObjectByType<UltimatePlayerMovement>();
        }

        playerMovement?.DisableMovement();

        try
        {
            if (omochaoTriggerDisable == null)
            {
                Debug.LogError($"{name}: No AudioSource assigned to TriggerDialog.", this);

                yield break;
            }

            foreach (AudioClip clip in dialogs)
            {
                if (clip == null)
                {
                    continue;
                }

                omochaoTriggerDisable.clip = clip;
                omochaoTriggerDisable.Play();

                while (omochaoTriggerDisable.isPlaying || pause)
                {
                    yield return null;
                }
            }
        }
        finally
        {
            dialogCoroutine = null;
            playerMovement?.EnableMovement();
        }
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
        }

        pause = false;
        playerMovement?.EnableMovement();
    }
}