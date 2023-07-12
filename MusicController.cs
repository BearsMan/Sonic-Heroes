using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

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
    public void Playmyaudio()
    {
        StopAllCoroutines();
        StartCoroutine(PlaymyAudioCoroutine());
    }
    public void StopAudio()
    {
        
    }
    public IEnumerator PlaymyAudioCoroutine()
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
