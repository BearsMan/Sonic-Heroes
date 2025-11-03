using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mixer & Groups")]
    public AudioMixer masterMixer;
    public AudioMixerGroup bgmGroup;
    public AudioMixerGroup sfxGroup;
    public AudioMixerGroup voiceGroup;

    [Header("Audio Sources")]
    public AudioSource bgmSourceA;
    public AudioSource bgmSourceB;
    public AudioSource sfxSource;
    public AudioSource voiceSource;

    [Header("Settings")]
    public float bgmFadeTime = 1.5f;
    public bool persistAcrossScenes = true;

    private bool usingSourceA = true;
    private Dictionary<string, AudioClip> clipCache = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (persistAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    // -----------------------------------
    // 🎵 MUSIC CONTROL
    // -----------------------------------
    public void PlayBGM(AudioClip clip, bool loop = true, float volume = 1f)
    {
        if (clip == null) return;

        AudioSource active = usingSourceA ? bgmSourceA : bgmSourceB;
        AudioSource next = usingSourceA ? bgmSourceB : bgmSourceA;

        next.clip = clip;
        next.loop = loop;
        next.volume = 0f;
        next.outputAudioMixerGroup = bgmGroup;
        next.Play();

        StartCoroutine(CrossfadeBGM(active, next, volume));
        usingSourceA = !usingSourceA;
    }

    private IEnumerator CrossfadeBGM(AudioSource from, AudioSource to, float targetVolume)
    {
        float t = 0f;
        while (t < bgmFadeTime)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / bgmFadeTime);
            if (from) from.volume = Mathf.Lerp(1f, 0f, p);
            if (to) to.volume = Mathf.Lerp(0f, targetVolume, p);
            yield return null;
        }
        if (from)
        {
            from.Stop();
        }
        if (to)
        {
            to.volume = targetVolume;
        } 
    }

    public void StopBGM(float fadeOutTime = 1f)
    {
        AudioSource current = usingSourceA ? bgmSourceB : bgmSourceA;
        if (current.isPlaying)
        {
            StartCoroutine(FadeOut(current, fadeOutTime));
        }
    }

    private IEnumerator FadeOut(AudioSource source, float time)
    {
        float start = source.volume;
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(start, 0, t / time);
            yield return null;
        }
        source.Stop();
    }

    // -----------------------------------
    // 🔊 SOUND EFFECTS
    // -----------------------------------
    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
        {
            return;
        }
        sfxSource.outputAudioMixerGroup = sfxGroup;
        sfxSource.PlayOneShot(clip, volume);
    }

    public void PlaySFX(string clipName, float volume = 1f)
    {
        var clip = LoadClip(clipName);
        PlaySFX(clip, volume);
    }

    // -----------------------------------
    // 🗣️ VOICE LINES
    // -----------------------------------
    public void PlayVoice(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
        {
            return;
        } 
        // Optionally stop current voice to prevent overlap
        voiceSource.outputAudioMixerGroup = voiceGroup;
        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.volume = volume;
        voiceSource.Play();
    }

    public void PlayVoice(string clipName, float volume = 1f)
    {
        var clip = LoadClip(clipName);
        PlayVoice(clip, volume);
    }

    // -----------------------------------
    // ⚙️ MIXER VOLUME CONTROLS
    // -----------------------------------
    public void SetMasterVolume(float db)
    {
        masterMixer.SetFloat("MasterVol", db);
    }

    public void SetBGMVolume(float db)
    {
        masterMixer.SetFloat("BGMVol", db);
    }

    public void SetSFXVolume(float db)
    {
        masterMixer.SetFloat("SFXVol", db);
    }

    public void SetVoiceVolume(float db)
    {
        masterMixer.SetFloat("VoiceVol", db);
    }

    // -----------------------------------
    // 🗂️ CLIP LOADING
    // -----------------------------------
    private AudioClip LoadClip(string name)
    {
        if (clipCache.TryGetValue(name, out var cached))
        {
            return cached;
        }

        AudioClip clip = Resources.Load<AudioClip>("Audio/" + name);
        if (clip != null)
        {
           clipCache[name] = clip;
        }
        else
        {
           Debug.LogWarning($"Audio clip not found: {name}");
        }

        return clip;
    }
}
