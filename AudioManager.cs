using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;
    public AudioSource voiceSource;

    [Header("Music Clips")]
    public AudioClip levelTheme;
    public AudioClip speedMusic;
    public AudioClip powerMusic;
    public AudioClip flyMusic;

    [Header("SFX Clips")]
    public List<AudioClip> soundEffects = new List<AudioClip>();

    private Dictionary<string, AudioClip> sfxDictionary;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSFX();
        }
        else
        {
            Destroy (gameObject);
        }
    }

    private void InitializeSFX()
    {
        sfxDictionary = new Dictionary<string, AudioClip>();
        foreach (AudioClip clip in soundEffects)
        {
            sfxDictionary[clip.name] = clip;
        }
    }

    // MUSIC CONTROL
    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (clip == null)
        {
            return;
        } 

        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.Play();
    }

    public void SwitchMusic(string mode)
    {
        switch (mode.ToLower())
        {
            case "speed":
                PlayMusic(speedMusic);
                break;
            case "power":
                PlayMusic(powerMusic);
                break;
            case "fly":
                PlayMusic(flyMusic);
                break;
            default:
                PlayMusic(levelTheme);
                break;
        }
    }

    // SFX CONTROL
    public void PlaySFX(string name, float volume = 1f)
    {
        if (sfxDictionary.TryGetValue(name, out AudioClip clip))
        {
            sfxSource.PlayOneShot(clip, volume);
        }

        else
        {
            Debug.LogWarning($"SFX '{name}' not found!");
        }
    }

    // VOICE CONTROL
    public void PlayVoice(AudioClip clip)
    {
        if (clip != null)
        {
            voiceSource.clip = clip;
            voiceSource.Play();
        }
    }

    // FADE OUT MUSIC
    public IEnumerator FadeOutMusic(float fadeTime)
    {
        float startVolume = musicSource.volume;
        while (musicSource.volume > 0)
        {
            musicSource.volume -= startVolume * Time.deltaTime / fadeTime;
            yield return null;
        }
        musicSource.Stop();
        musicSource.volume = startVolume;
    }
}
