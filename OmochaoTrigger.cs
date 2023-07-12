using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OmochaoTrigger : MonoBehaviour
{
    public List<AudioClip> dialogs = new List<AudioClip>();
    public AudioSource asource;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
    public IEnumerator ReadDialog()
    {

        int counter = 0;
        while (counter < dialogs.Count)
        {
            asource.clip = dialogs[counter];
            asource.Play();
            while (asource.isPlaying)
            {
                yield return new WaitForSeconds(Time.deltaTime);
            }
            counter += 1;
        }
        yield return null;

       
    }
    public void DialogRead()
    {
        StartCoroutine(ReadDialog());
    }
}
