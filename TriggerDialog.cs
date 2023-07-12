using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerDialog : MonoBehaviour
{
    public List<AudioClip> dialogs = new List<AudioClip>();
    private bool dialogRead;
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
        if (dialogRead == false && other.CompareTag("Player"))
        {
            dialogRead = true;

            StartCoroutine(ReadDialog());
        }
    }
    public IEnumerator ReadDialog()
    {
        FindFirstObjectByType<UltimatePlayerMovement>().tutorialPlaying = true;
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
        FindFirstObjectByType<UltimatePlayerMovement>().tutorialPlaying = false;
    }
}
