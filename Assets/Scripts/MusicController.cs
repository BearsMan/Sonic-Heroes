using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MusicController : MonoBehaviour
{
    public AudioSource asource;
    public List<AudioClip> myclips = new List<AudioClip>();
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
    public void PlayMyAudio()
    {
        StopAllCoroutines();
        StartCoroutine(PlayMyAudioCoroutine());
    }
    public void StopAudio()
    {

    }
    public IEnumerator PlayMyAudioCoroutine()
    {
        foreach (AudioClip a in myclips)
        {
            asource.clip = a;
            asource.Play();

            while (asource.isPlaying)
            {
                yield return new WaitForSeconds(Time.deltaTime);
            }
        }
    }
}
