using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerDialog : MonoBehaviour
{
    public List<AudioClip> dialogs = new List<AudioClip>();
    private bool dialogread;
    public AudioSource omochaoTriggerDisable;
    public bool pause = false;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
    private void OnTriggerEnter(Collider other)
    {
        if (dialogread == false && other.CompareTag("Player"))
        {
            dialogread = true;

            StartCoroutine(ReadDialog());
        }
    }
    public IEnumerator ReadDialog()
    {
        var up = Object.FindAnyObjectByType<UltimatePlayerMovement>();
        if (up != null) up.tutorialPlaying = true;
        int counter = 0;

        while (counter < dialogs.Count)
        {
            omochaoTriggerDisable.clip = dialogs[counter];
            omochaoTriggerDisable.Play();
            while (omochaoTriggerDisable.isPlaying || pause)
            {
                yield return new WaitForSeconds(Time.deltaTime);
            }
            counter += 1;
        }
        var up2 = Object.FindAnyObjectByType<UltimatePlayerMovement>();
        if (up2 != null) up2.tutorialPlaying = false;
    }
}
