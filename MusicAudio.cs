using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MusicAudio : MonoBehaviour
{
    public AudioSource source;
    public AudioClip musicClip;
    private float songTime;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.clip = musicClip;
        source.Play();
    }
    public void PlayMusic(AudioClip clip)
    {
        if (source.clip == musicClip)
        {
            songTime = source.time;
        }
        
        source.clip = clip;
        source.loop = false;
        source.Play();
        StartCoroutine(PlayTrack());
    }

    private IEnumerator PlayTrack()
    {
        yield return new WaitForSecondsRealtime(0.1f);
        while (source.isPlaying)
        {
            yield return new WaitForEndOfFrame();
        }

        source.clip = musicClip;
        source.time = songTime;
        source.loop = true;
        source.Play();
    }
}
