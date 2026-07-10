using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Music")]
    public AudioSource musicSource;
    public AudioClip musicClip;

    [Header("Sound Effects")]
    public List<AudioClip> soundEffects;
    public AudioSource soundEffectSource;

    [Header("Voice Lines")]
    public List<AudioClip> teamVoiceLines;
    public AudioSource voiceSource;
    private bool musicPlaying = false;
    public enum SoundEffect
    {
        Jump,
        Shoot,
        Explosion,
        PowerUp
    }

    public enum TeamVoiceLine
    {
        TeamSonic,
        TeamDark,
        TeamRose,
        TeamChaotix,
        LastStory
    }
    // Start is called before the first frame update
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        else
        {
            Destroy(gameObject);
            return;
        }

        if (musicSource != null)
        {
            musicSource.loop = true;
        }
    }

    void Start()
    {
        LoadVolumes();
        PlayMusic();
    }

    public void LoadVolumes()
    {
        float musicVolume = PlayerPrefs.GetFloat("MusicVolume", 1f);
        float soundEffectVolume = PlayerPrefs.GetFloat("SoundEffectVolume", 1f);
        float voiceLineVolume = PlayerPrefs.GetFloat("VoiceLineVolume", 1f);
        SetMusicVolume(musicVolume);
        SetSoundEffectVolume(soundEffectVolume);
        SetVoiceLineVolume(voiceLineVolume);
    }

    public void PlayMusic()
    {
        if (musicSource == null || musicClip == null)
        {
            return;
        }

        if (!musicPlaying)
        {
            musicSource.clip = musicClip;
            musicSource.Play();
            musicPlaying = true;
        }
    }

    public void PlaySoundEffect(SoundEffect effect)
    {
        if (soundEffectSource == null || soundEffects == null || soundEffects.Count == 0)
        {
            return;
        }
        int index = (int)effect;
        if (index >= 0 && index < soundEffects.Count)
        {
            AudioClip clip = soundEffects[index];
            if (clip != null)
            {
                soundEffectSource.PlayOneShot(clip);
            }
        }
    }

    public void SetMusicVolume(float volume)
    {
        if (musicSource == null)
        {
            Debug.LogWarning("Music source is not assigned.");
            return;
        }

        if (musicSource != null)
        {
            musicSource.volume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat("MusicVolume", musicSource.volume);
            PlayerPrefs.Save();
        }
    }

    public void SetSoundEffectVolume(float volume)
    {
        if (soundEffectSource != null)
        {
            soundEffectSource.volume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat("SoundEffectVolume", soundEffectSource.volume);
            PlayerPrefs.Save();
        }
    }

    public void SetVoiceLineVolume(float volume)
    {
        if (voiceSource != null)
        {
            voiceSource.volume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat("VoiceLineVolume", voiceSource.volume);
            PlayerPrefs.Save();
        }
    }
    public void PlayTeamVoiceLine(TeamVoiceLine voiceLine)
    {
        if (voiceSource == null || teamVoiceLines == null || teamVoiceLines.Count == 0)
        {
            return;
        }
        int index = (int)voiceLine;
        if (index >= 0 && index < teamVoiceLines.Count)
        {
            AudioClip clip = teamVoiceLines[index];
            if (clip != null)
            {
                voiceSource.PlayOneShot(clip);
            }
        }
    }
}
