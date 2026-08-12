using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    #region Enums

    public enum MusicCategory
    {
        Theme,
        Stage,
        Event,
        Boss,
        Menu,
        SpecialStage,
        Results,
        Other
    }

    public enum SoundEffect
    {
        None,

        Jump,
        Land,
        Ring,
        RingLoss,
        Spring,
        DashPanel,
        Switch,
        Checkpoint,

        HomingAttack,
        LightDash,
        ThunderShoot,
        FireDunk,
        TriangleJump,
        TriangleDive,
        RocketAccel,
        TeamBlast,

        RailGrind,
        Fly,
        PowerAttack,

        EnemyHit,
        EnemyDestroyed,
        Explosion,

        MenuMove,
        MenuConfirm,
        MenuCancel,

        StageClear,
        RankReveal
    }

    public enum CharacterVoice
    {
        None,

        Sonic,
        Tails,
        Knuckles,

        Shadow,
        Rouge,
        Omega,

        Amy,
        Cream,
        Big,

        Vector,
        Espio,
        Charmy,

        Eggman,
        Omochao,
        MetalSonic
    }

    public enum VoiceContext
    {
        None,

        StageStart,
        StageIdle,

        SpeedFormation,
        FlyFormation,
        PowerFormation,

        Jump,
        Attack,
        HomingAttack,
        Flight,
        PowerAction,

        TeamBlastReady,
        TeamBlast,

        Hurt,
        Recover,

        Checkpoint,
        StageEvent,

        BossStart,
        BossEvent,

        StageClear,
        RankA,
        RankB,
        RankC,
        RankD,
        RankE
    }

    public enum VoicePriority
    {
        Low = 0,
        Normal = 10,
        Important = 20,
        Critical = 30
    }

    #endregion

    #region Serializable Entries

    [Serializable]
    private sealed class MusicEntry
    {
        public string id;
        public MusicCategory category;
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume = 1f;

        public bool loop = true;
    }

    [Serializable]
    private sealed class SoundEffectEntry
    {
        public SoundEffect effect;
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume = 1f;

        [Min(0f)]
        public float minimumRepeatDelay;
    }

    [Serializable]
    private sealed class VoiceEntry
    {
        public CharacterVoice character;
        public VoiceContext context;
        public AudioClip[] clips;

        [Range(0f, 1f)]
        public float volume = 1f;

        public VoicePriority priority =
            VoicePriority.Normal;

        [Min(0f)]
        public float minimumRepeatDelay = 0.25f;
    }

    #endregion

    #region Singleton

    public static AudioManager Instance
    {
        get;
        private set;
    }

    #endregion

    #region Sources

    [Header("Audio Sources")]

    [SerializeField]
    private AudioSource musicSourceA;

    [SerializeField]
    private AudioSource musicSourceB;

    [SerializeField]
    private AudioSource jingleSource;

    [SerializeField]
    private AudioSource soundEffectSource;

    [SerializeField]
    private AudioSource voiceSource;

    [SerializeField]
    private AudioSource ambientSource;

    #endregion

    #region Library

    [Header("Music")]
    [SerializeField]
    private MusicEntry[] musicLibrary;

    [Header("Sound Effects")]
    [SerializeField]
    private SoundEffectEntry[] soundEffectLibrary;

    [Header("Character Voices")]
    [SerializeField]
    private VoiceEntry[] voiceLibrary;

    #endregion

    #region Music Settings

    [Header("Music Settings")]

    [SerializeField, Min(0f)]
    private float musicFadeDuration = 0.5f;

    #endregion

    #region Volume

    [Header("Volume")]

    [SerializeField, Range(0f, 1f)]
    private float musicVolume = 1f;

    [SerializeField, Range(0f, 1f)]
    private float soundEffectVolume = 1f;

    [SerializeField, Range(0f, 1f)]
    private float voiceVolume = 1f;

    [SerializeField, Range(0f, 1f)]
    private float ambientVolume = 1f;

    #endregion

    #region Runtime

    private readonly Dictionary<string, MusicEntry>
        musicById =
            new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<SoundEffect, SoundEffectEntry>
        soundEffects =
            new();

    private readonly Dictionary<SoundEffect, float>
        lastSoundEffectTimes =
            new();

    private readonly Dictionary<
        (CharacterVoice, VoiceContext),
        VoiceEntry>
        voices =
            new();

    private readonly Dictionary<
        (CharacterVoice, VoiceContext),
        float>
        lastVoiceTimes =
            new();

    private AudioSource activeMusicSource;
    private AudioSource inactiveMusicSource;

    private Coroutine musicFadeRoutine;

    private string currentMusicId =
        string.Empty;

    private VoicePriority currentVoicePriority =
        VoicePriority.Low;

    private bool initialized;
    private bool shuttingDown;

    #endregion

    #region Properties

    public string CurrentMusicId =>
        currentMusicId;

    public bool IsMusicPlaying =>
        activeMusicSource != null &&
        activeMusicSource.isPlaying;

    public bool IsVoicePlaying =>
        voiceSource != null &&
        voiceSource.isPlaying;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(
            gameObject);

        CacheSources();
        BuildLibraries();
        LoadVolumes();

        activeMusicSource =
            musicSourceA;

        inactiveMusicSource =
            musicSourceB;

        initialized = true;
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        shuttingDown = true;

        StopAllCoroutines();

        musicById.Clear();
        soundEffects.Clear();
        lastSoundEffectTimes.Clear();
        voices.Clear();
        lastVoiceTimes.Clear();

        Instance = null;
    }

    private void OnValidate()
    {
        musicFadeDuration =
            Mathf.Max(
                0f,
                musicFadeDuration);

        musicVolume =
            Mathf.Clamp01(
                musicVolume);

        soundEffectVolume =
            Mathf.Clamp01(
                soundEffectVolume);

        voiceVolume =
            Mathf.Clamp01(
                voiceVolume);

        ambientVolume =
            Mathf.Clamp01(
                ambientVolume);
    }

    #endregion

    #region Music

    public bool PlayMusic(
        string musicId,
        bool restartIfAlreadyPlaying = false)
    {
        if (!initialized ||
            string.IsNullOrWhiteSpace(
                musicId))
        {
            return false;
        }

        if (!musicById.TryGetValue(
            musicId,
            out MusicEntry entry) ||
            entry == null ||
            entry.clip == null)
        {
            return false;
        }

        if (!restartIfAlreadyPlaying &&
            string.Equals(
                currentMusicId,
                musicId,
                StringComparison.OrdinalIgnoreCase) &&
            IsMusicPlaying)
        {
            return true;
        }

        StartMusicTransition(
            entry);

        currentMusicId =
            musicId;

        return true;
    }

    public void StopMusic(
        bool fadeOut = true)
    {
        currentMusicId =
            string.Empty;

        if (musicFadeRoutine != null)
        {
            StopCoroutine(
                musicFadeRoutine);

            musicFadeRoutine =
                null;
        }

        if (!fadeOut ||
            musicFadeDuration <= 0f)
        {
            StopMusicSource(
                musicSourceA);

            StopMusicSource(
                musicSourceB);

            return;
        }

        musicFadeRoutine =
            StartCoroutine(
                FadeOutMusic());
    }

    private void StartMusicTransition(
        MusicEntry entry)
    {
        if (musicFadeRoutine != null)
        {
            StopCoroutine(
                musicFadeRoutine);
        }

        musicFadeRoutine =
            StartCoroutine(
                CrossfadeMusic(
                    entry));
    }

    private IEnumerator CrossfadeMusic(
        MusicEntry entry)
    {
        AudioSource from =
            activeMusicSource;

        AudioSource to =
            inactiveMusicSource;

        if (to == null)
        {
            yield break;
        }

        to.Stop();

        to.clip =
            entry.clip;

        to.loop =
            entry.loop;

        to.volume =
            0f;

        to.Play();

        float targetVolume =
            entry.volume *
            musicVolume;

        if (musicFadeDuration <= 0f)
        {
            if (from != null)
            {
                from.Stop();
                from.volume = 0f;
            }

            to.volume =
                targetVolume;
        }
        else
        {
            float startFromVolume =
                from != null
                    ? from.volume
                    : 0f;

            float elapsed =
                0f;

            while (elapsed <
                musicFadeDuration)
            {
                float t =
                    elapsed /
                    musicFadeDuration;

                if (from != null)
                {
                    from.volume =
                        Mathf.Lerp(
                            startFromVolume,
                            0f,
                            t);
                }

                to.volume =
                    Mathf.Lerp(
                        0f,
                        targetVolume,
                        t);

                elapsed +=
                    Time.unscaledDeltaTime;

                yield return null;
            }

            if (from != null)
            {
                from.Stop();
                from.volume = 0f;
            }

            to.volume =
                targetVolume;
        }

        activeMusicSource =
            to;

        inactiveMusicSource =
            from;

        musicFadeRoutine =
            null;
    }

    private IEnumerator FadeOutMusic()
    {
        AudioSource source =
            activeMusicSource;

        if (source == null)
        {
            yield break;
        }

        float startVolume =
            source.volume;

        float elapsed =
            0f;

        while (elapsed <
            musicFadeDuration)
        {
            source.volume =
                Mathf.Lerp(
                    startVolume,
                    0f,
                    elapsed /
                    musicFadeDuration);

            elapsed +=
                Time.unscaledDeltaTime;

            yield return null;
        }

        source.Stop();
        source.volume = 0f;

        musicFadeRoutine =
            null;
    }

    #endregion

    #region Jingles

    public void PlayJingle(
        AudioClip clip,
        float volume = 1f)
    {
        if (jingleSource == null ||
            clip == null)
        {
            return;
        }

        jingleSource.PlayOneShot(
            clip,
            Mathf.Clamp01(volume) *
            musicVolume);
    }

    #endregion

    #region Sound Effects

    public bool PlaySoundEffect(
        SoundEffect effect)
    {
        if (!initialized ||
            effect == SoundEffect.None)
        {
            return false;
        }

        if (!soundEffects.TryGetValue(
            effect,
            out SoundEffectEntry entry) ||
            entry == null ||
            entry.clip == null ||
            soundEffectSource == null)
        {
            return false;
        }

        if (lastSoundEffectTimes.TryGetValue(
                effect,
                out float lastTime) &&
            Time.unscaledTime -
                lastTime <
            entry.minimumRepeatDelay)
        {
            return false;
        }

        lastSoundEffectTimes[effect] =
            Time.unscaledTime;

        soundEffectSource.PlayOneShot(
            entry.clip,
            entry.volume *
            soundEffectVolume);

        return true;
    }

    public void PlaySoundEffect(
        AudioClip clip,
        float volume = 1f)
    {
        if (soundEffectSource == null ||
            clip == null)
        {
            return;
        }

        soundEffectSource.PlayOneShot(
            clip,
            Mathf.Clamp01(volume) *
            soundEffectVolume);
    }

    #endregion

    #region Voices

    public bool PlayVoice(
        CharacterVoice character,
        VoiceContext context)
    {
        if (!initialized ||
            character == CharacterVoice.None ||
            context == VoiceContext.None ||
            voiceSource == null)
        {
            return false;
        }

        var key =
            (character, context);

        if (!voices.TryGetValue(
            key,
            out VoiceEntry entry) ||
            entry == null ||
            entry.clips == null ||
            entry.clips.Length == 0)
        {
            return false;
        }

        if (lastVoiceTimes.TryGetValue(
                key,
                out float lastTime) &&
            Time.unscaledTime -
                lastTime <
            entry.minimumRepeatDelay)
        {
            return false;
        }

        if (voiceSource.isPlaying &&
            entry.priority <
            currentVoicePriority)
        {
            return false;
        }

        AudioClip clip =
            GetRandomVoiceClip(
                entry.clips);

        if (clip == null)
        {
            return false;
        }

        lastVoiceTimes[key] =
            Time.unscaledTime;

        currentVoicePriority =
            entry.priority;

        voiceSource.Stop();

        voiceSource.clip =
            clip;

        voiceSource.volume =
            entry.volume *
            voiceVolume;

        voiceSource.Play();

        return true;
    }

    public void StopVoice()
    {
        if (voiceSource == null)
        {
            return;
        }

        voiceSource.Stop();

        currentVoicePriority =
            VoicePriority.Low;
    }

    private static AudioClip GetRandomVoiceClip(
        AudioClip[] clips)
    {
        if (clips == null ||
            clips.Length == 0)
        {
            return null;
        }

        int startIndex =
            UnityEngine.Random.Range(
                0,
                clips.Length);

        for (int offset = 0;
            offset < clips.Length;
            offset++)
        {
            int index =
                (startIndex + offset) %
                clips.Length;

            if (clips[index] != null)
            {
                return clips[index];
            }
        }

        return null;
    }

    #endregion

    #region Ambient

    public void PlayAmbient(
        AudioClip clip,
        bool loop = true)
    {
        if (ambientSource == null ||
            clip == null)
        {
            return;
        }

        if (ambientSource.clip == clip &&
            ambientSource.isPlaying)
        {
            return;
        }

        ambientSource.Stop();

        ambientSource.clip =
            clip;

        ambientSource.loop =
            loop;

        ambientSource.volume =
            ambientVolume;

        ambientSource.Play();
    }

    public void StopAmbient()
    {
        ambientSource?.Stop();
    }

    #endregion

    #region Volume

    public void SetMusicVolume(
        float volume)
    {
        musicVolume =
            Mathf.Clamp01(
                volume);

        PlayerPrefs.SetFloat(
            "MusicVolume",
            musicVolume);

        PlayerPrefs.Save();

        UpdateMusicVolumes();
    }

    public void SetSoundEffectVolume(
        float volume)
    {
        soundEffectVolume =
            Mathf.Clamp01(
                volume);

        PlayerPrefs.SetFloat(
            "SoundEffectVolume",
            soundEffectVolume);

        PlayerPrefs.Save();

        if (soundEffectSource != null)
        {
            soundEffectSource.volume =
                1f;
        }
    }

    public void SetVoiceVolume(
        float volume)
    {
        voiceVolume =
            Mathf.Clamp01(
                volume);

        PlayerPrefs.SetFloat(
            "VoiceVolume",
            voiceVolume);

        PlayerPrefs.Save();

        if (voiceSource != null)
        {
            voiceSource.volume =
                voiceVolume;
        }
    }

    public void SetAmbientVolume(
        float volume)
    {
        ambientVolume =
            Mathf.Clamp01(
                volume);

        PlayerPrefs.SetFloat(
            "AmbientVolume",
            ambientVolume);

        PlayerPrefs.Save();

        if (ambientSource != null)
        {
            ambientSource.volume =
                ambientVolume;
        }
    }

    private void LoadVolumes()
    {
        musicVolume =
            Mathf.Clamp01(
                PlayerPrefs.GetFloat(
                    "MusicVolume",
                    1f));

        soundEffectVolume =
            Mathf.Clamp01(
                PlayerPrefs.GetFloat(
                    "SoundEffectVolume",
                    1f));

        voiceVolume =
            Mathf.Clamp01(
                PlayerPrefs.GetFloat(
                    "VoiceVolume",
                    1f));

        ambientVolume =
            Mathf.Clamp01(
                PlayerPrefs.GetFloat(
                    "AmbientVolume",
                    1f));

        UpdateMusicVolumes();
    }

    private void UpdateMusicVolumes()
    {
        if (activeMusicSource != null &&
            activeMusicSource.clip != null)
        {
            activeMusicSource.volume =
                musicVolume;
        }
    }

    #endregion

    #region Library Setup

    private void BuildLibraries()
    {
        musicById.Clear();
        soundEffects.Clear();
        voices.Clear();

        if (musicLibrary != null)
        {
            foreach (MusicEntry entry in
                musicLibrary)
            {
                if (entry == null ||
                    entry.clip == null ||
                    string.IsNullOrWhiteSpace(
                        entry.id))
                {
                    continue;
                }

                musicById[entry.id] =
                    entry;
            }
        }

        if (soundEffectLibrary != null)
        {
            foreach (SoundEffectEntry entry in
                soundEffectLibrary)
            {
                if (entry == null ||
                    entry.effect ==
                        SoundEffect.None ||
                    entry.clip == null)
                {
                    continue;
                }

                soundEffects[entry.effect] =
                    entry;
            }
        }

        if (voiceLibrary != null)
        {
            foreach (VoiceEntry entry in
                voiceLibrary)
            {
                if (entry == null ||
                    entry.character ==
                        CharacterVoice.None ||
                    entry.context ==
                        VoiceContext.None ||
                    entry.clips == null ||
                    entry.clips.Length == 0)
                {
                    continue;
                }

                voices[
                    (
                        entry.character,
                        entry.context
                    )] =
                    entry;
            }
        }
    }

    private void CacheSources()
    {
        AudioSource[] sources =
            GetComponents<AudioSource>();

        if (musicSourceA == null &&
            sources.Length > 0)
        {
            musicSourceA =
                sources[0];
        }

        if (musicSourceB == null)
        {
            musicSourceB =
                gameObject.AddComponent<
                    AudioSource>();
        }

        if (jingleSource == null)
        {
            jingleSource =
                gameObject.AddComponent<
                    AudioSource>();
        }

        if (soundEffectSource == null)
        {
            soundEffectSource =
                gameObject.AddComponent<
                    AudioSource>();
        }

        if (voiceSource == null)
        {
            voiceSource =
                gameObject.AddComponent<
                    AudioSource>();
        }

        if (ambientSource == null)
        {
            ambientSource =
                gameObject.AddComponent<
                    AudioSource>();
        }

        ConfigureSource(
            musicSourceA);

        ConfigureSource(
            musicSourceB);

        ConfigureSource(
            jingleSource);

        ConfigureSource(
            soundEffectSource);

        ConfigureSource(
            voiceSource);

        ConfigureSource(
            ambientSource);
    }

    private static void ConfigureSource(
        AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake =
            false;

        source.spatialBlend =
            0f;
    }

    #endregion

    #region Utility

    private static void StopMusicSource(
        AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.Stop();
        source.clip = null;
        source.volume = 0f;
    }

    #endregion
}