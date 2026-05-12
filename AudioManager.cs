using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// AudioManager based on Sonic Heroes (2003) audio design.
/// Handles stage BGM, jingles (goal, extra life, game over), and SFX,
/// mirroring how the game interrupts music for jingles then resumes.
/// </summary>
public class AudioManager : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Singleton
    // -----------------------------------------------------------------------
    public static AudioManager Instance { get; private set; }

    // -----------------------------------------------------------------------
    // Inspector References
    // -----------------------------------------------------------------------
    [Header("Mixer Groups")]
    public AudioMixerGroup musicMixerGroup;
    public AudioMixerGroup sfxMixerGroup;

    // ═══════════════════════════════════════════════════════════════════════
    // COMPLETE TRINITY — DISC 1
    // All 28 tracks from the original Japanese two-disc release (2004).
    // ═══════════════════════════════════════════════════════════════════════

    [Header("── Disc 1 · Opening / Title ──────────────────────────────────")]
    // D1-01  Sonic Heroes / Opening ver.  (Crush 40 — main theme, plays over the FMV intro)
    public AudioClip sonicHeroesOpeningVer;

    [Header("── Disc 1 · Action Stages ──────────────────────────────────")]
    // All four teams share these stage tracks; the BGM does not change per team.
    // D1-02  Stage 01 : Seaside Hill
    public AudioClip seasideHillBGM;
    // D1-03  Stage 02 : Ocean Palace
    public AudioClip oceanPalaceBGM;
    // D1-06  Stage 03 : Grand Metropolis
    public AudioClip grandMetropolisBGM;
    // D1-07  Stage 04 : Power Plant  (same melody as Grand Metropolis, darker mix)
    public AudioClip powerPlantBGM;
    // D1-11  Stage 05 : Casino Park
    public AudioClip casinoParkBGM;
    // D1-12  Stage 06 : Bingo Highway
    public AudioClip bingoHighwayBGM;
    // D1-17  Stage 07 : Rail Canyon
    public AudioClip railCanyonBGM;
    // D1-18  Stage 08 : Bullet Station
    public AudioClip bulletStationBGM;

    [Header("── Disc 1 · Special & Bonus Stages ────────────────────────")]
    // D1-08  Special Stage : Bonus Challenge  (bobsled half-pipe, first set)
    public AudioClip specialStageBonusChallengeBGM;

    [Header("── Disc 1 · Boss Music ─────────────────────────────────────")]
    // D1-04  Boss : Egg Hawk        (Team Sonic/Dark/Rose first boss)
    public AudioClip bossEggHawkBGM;
    // D1-10  Boss : VS. Team Battle (fought against rival teams mid-story)
    public AudioClip bossVsTeamBattleBGM;
    // D1-16  Boss : Robot Carnival / Robot Storm  (Team Sonic/Dark/Rose mid-boss)
    public AudioClip bossRobotCarnivalRobotStormBGM;
    // D1-21  Boss : Egg Albatross   (Team Chaotix airship boss)
    public AudioClip bossEggAlbatrossBGM;

    [Header("── Disc 1 · Event / Cutscene Stingers ───────────────────────")]
    // D1-09  Event : Strange Guys      (Team Chaotix first meeting with client)
    public AudioClip eventStrangeGuysBGM;
    // D1-14  Event : Monkey Business   (Team Chaotix comedy cutscene)
    public AudioClip eventMonkeyBusinessBGM;
    // D1-15  Event : My World          (Team Rose story intro stinger)
    public AudioClip eventMyWorldBGM;
    // D1-22  Event : Disquieting Shadow (Shadow's "who am I?" moment — Team Dark)
    public AudioClip eventDisquietingShadowBGM;

    [Header("── Disc 1 · System Screens ─────────────────────────────────")]
    // D1-05  System Screen : Select    (team/character selection screen)
    public AudioClip systemScreenSelectBGM;
    // D1-23  System Screen : Menu      (main pause/options menu)
    public AudioClip systemScreenMenuBGM;
    // D1-26  System Screen : 2P VS. Menu (two-player mode menu)
    public AudioClip systemScreen2PVsMenuBGM;

    [Header("── Disc 1 · 2P Battle Mode ──────────────────────────────────")]
    // D1-13  Battle : Casino Area   (2P battle on casino-themed map)
    public AudioClip battle2PCasinoAreaBGM;
    // D1-24  Battle : City Area     (2P battle on city map)
    public AudioClip battle2PCityAreaBGM;
    // D1-25  Battle : Sea Area      (2P battle on ocean/beach map)
    public AudioClip battle2PSeaAreaBGM;
    // D1-27  Battle : Quick Race    (2P race mode)
    public AudioClip battle2PQuickRaceBGM;
    // D1-28  Battle : Ring Race     (2P ring-collection race)
    public AudioClip battle2PRingRaceBGM;

    [Header("── Disc 1 · Jingles ──────────────────────────────────────────")]
    // D1-19  Jingle : Speed Up      (speed shoes power-up active)
    public AudioClip jingleSpeedUp;
    // D1-20  Jingle : Invincible    (invincibility star active)
    public AudioClip jingleInvincible;

    // ═══════════════════════════════════════════════════════════════════════
    // COMPLETE TRINITY — DISC 2
    // All 22 tracks (19 main + 3 bonus) from Disc 2.
    // ═══════════════════════════════════════════════════════════════════════

    [Header("── Disc 2 · Title / Opening ────────────────────────────────")]
    // D2-01  Sonic Heroes / Title ver.  (short orchestral title-screen sting)
    public AudioClip sonicHeroesTitleVer;

    [Header("── Disc 2 · Action Stages ──────────────────────────────────")]
    // D2-02  Stage 00 : Sea Gate    (tutorial/prologue stage — Team Sonic only)
    public AudioClip seaGateBGM;
    // D2-03  Stage 09 : Frog Forest
    public AudioClip frogForestBGM;
    // D2-04  Stage 10 : Lost Jungle
    public AudioClip lostJungleBGM;
    // D2-09  Stage 11 : Hang Castle
    public AudioClip hangCastleBGM;
    // D2-10  Stage 12 : Mystic Mansion
    public AudioClip mysticMansionBGM;
    // D2-12  Stage 13 : Egg Fleet
    public AudioClip eggFleetBGM;
    // D2-13  Stage 14 : Final Fortress
    public AudioClip finalFortressBGM;

    [Header("── Disc 2 · Special Stage ──────────────────────────────────")]
    // D2-07  Special Stage : Emerald Challenge  (chaos emerald collection half-pipe)
    public AudioClip specialStageEmeraldChallengeBGM;

    [Header("── Disc 2 · Boss Music ─────────────────────────────────────")]
    // D2-14  Boss : Egg Emperor     (Stages 13-14 mid-boss / final fortress boss)
    public AudioClip bossEggEmperorBGM;
    // D2-17  Last Boss ver. 1 : Metal Madness   (Neo Metal Sonic first phase)
    public AudioClip lastBossMetalMadnessBGM;
    // D2-18  Last Boss ver. 2 : Metal Overlord  ("What I'm Made Of..." — Crush 40,
    //        plays during the final Metal Overlord fight; also the game's ending theme)
    public AudioClip lastBossMetalOverlordWhatImMadeOfBGM;

    [Header("── Disc 2 · Event / Cutscene Music ──────────────────────────")]
    // D2-05  Event : Excuse Me?              (Team Rose comedic encounter stinger)
    public AudioClip eventExcuseMeBGM;
    // D2-06  Event : Unexpected Encounter    (Team Dark vs Team Sonic confrontation)
    public AudioClip eventUnexpectedEncounterBGM;
    // D2-08  Event : No Past to Remember     (Shadow's amnesia story beat — Team Dark)
    public AudioClip eventNoPastToRememberBGM;
    // D2-11  Event : My Ambition             (Eggman's villain monologue reveal)
    public AudioClip eventMyAmbitionBGM;
    // D2-15  Event : Metal Sonic... The Ultimate Overlord  (Metal Sonic reveal cutscene)
    public AudioClip eventMetalSonicUltimateOverlordBGM;
    // D2-16  Event : All Heroes Gather       (all four teams unite before final boss)
    public AudioClip eventAllHeroesGatherBGM;
    // D2-19  Event : Finale... Adventure Must Go On
    //        (post-credits epilogue stinger; plays as teams depart)
    public AudioClip eventFinaleAdventureMustGoOnBGM;

    [Header("── Disc 2 · Bonus Tracks ─────────────────────────────────────")]
    // D2-20  Special Stage : Bonus Challenge / Extended ver.
    public AudioClip specialStageBonusChallengeExtendedBGM;
    // D2-21  Casino Park / Original ver.    (pre-remix version of the Casino Park theme)
    public AudioClip casinoParkOriginalVerBGM;
    // D2-22  Bingo Highway / Remix ver.     (alternate remix by Keiichi Sugiyama)
    public AudioClip bingoHighwayRemixVerBGM;

    // ═══════════════════════════════════════════════════════════════════════
    // TRIPLE THREAT — VOCAL TRAX
    // The six in-game vocal themes released on the companion vocal album (2004).
    // Each song plays during its team's story intro FMV / ending sequence.
    // ═══════════════════════════════════════════════════════════════════════

    [Header("── Triple Threat · Team Vocal Themes & Ending Songs ──────────")]
    // Track 1  – "Sonic Heroes"     Crush 40  (main theme; plays in opening FMV)
    public AudioClip vocalSonicHeroes;
    // Track 2  – "We Can"           Ted Poley & Tony Harnell  (Team Sonic theme;
    //            plays during Team Sonic's story intro and ending staff roll)
    public AudioClip vocalWeCanTeamSonic;
    // Track 3  – "This Machine"     Julien-K  (Team Dark theme;
    //            plays during Team Dark's story intro and ending)
    public AudioClip vocalThisMachineTeamDark;
    // Track 4  – "Follow Me"        Kay Hanley  (Team Rose theme;
    //            plays during Team Rose's story intro and ending)
    public AudioClip vocalFollowMeTeamRose;
    // Track 5  – "Team Chaotix"     Gunnar Nelson  (Team Chaotix theme;
    //            plays during Team Chaotix's story intro and ending)
    public AudioClip vocalTeamChaotix;
    // Track 6  – "What I'm Made of..."  Crush 40  (final boss / Last Story ending;
    //            full vocal version plays over the true ending and staff roll)
    public AudioClip vocalWhatImMadeOfLastStory;

    [Header("── In-Game Jingles (interrupt BGM, then resume) ──────────────")]
    // These short stingers pause the BGM and resume it afterwards (see PlayJingle).
    public AudioClip jingleGoalRing;       // Stage clear
    public AudioClip jingleExtraLife;      // 1-Up / extra life
    public AudioClip jingleGameOver;       // Game over screen
    public AudioClip jingleMissionStart;   // Mission start (Team Chaotix objectives)

    [Header("SFX Pool")]
    [Tooltip("Max simultaneous SFX sources. Sonic Heroes used hardware voice limits.")]
    [Range(8, 32)]
    public int maxSFXSources = 16;

    [Header("Music Transition")]
    [Tooltip("Seconds to cross-fade between BGM tracks.")]
    [Range(0f, 3f)]
    public float musicFadeDuration = 1.2f;

    // -----------------------------------------------------------------------
    // Private State
    // -----------------------------------------------------------------------
    private AudioSource musicSource;
    private AudioSource jingleSource;        // Dedicated source so jingles don't stomp BGM state
    private Queue<AudioSource> sfxPool;

    private AudioClip pendingResumeClip;     // BGM to resume after a jingle
    private float pendingResumeTime;         // Resume at the position it was paused
    private Coroutine fadeCoroutine;
    private Coroutine jingleCoroutine;

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------
    private void Awake()
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

        InitialiseSources();
        InitialiseSFXPool();
    }

    private void InitialiseSources()
    {
        // BGM source — looping, routed through music mixer
        musicSource = CreateAudioSource(musicMixerGroup, loop: true);

        // Jingle source — one-shot, also through music mixer so volume is consistent
        jingleSource = CreateAudioSource(musicMixerGroup, loop: false);
    }

    private void InitialiseSFXPool()
    {
        sfxPool = new Queue<AudioSource>();
        for (int i = 0; i < maxSFXSources; i++)
        {
            AudioSource src = CreateAudioSource(sfxMixerGroup, loop: false);
            sfxPool.Enqueue(src);
        }
    }

    private AudioSource CreateAudioSource(AudioMixerGroup group, bool loop)
    {
        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.outputAudioMixerGroup = group;
        src.loop = loop;
        src.playOnAwake = false;
        return src;
    }

    // -----------------------------------------------------------------------
    // BGM — mirrors Sonic Heroes stage music flow
    // -----------------------------------------------------------------------

    /// <summary>
    /// Play a BGM clip, cross-fading from whatever is currently playing.
    /// Calling this with the same clip that is already playing is a no-op.
    /// </summary>
    public void PlayMusic(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[AudioManager] PlayMusic called with a null clip.");
            return;
        }

        // Avoid restarting the same track (Sonic Heroes keeps BGM running between acts)
        if (musicSource.clip == clip && musicSource.isPlaying)
            return;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(CrossFadeMusic(clip));
    }

    /// <summary>
    /// Stop BGM immediately (e.g. entering a cutscene).
    /// </summary>
    public void StopMusic()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        musicSource.Stop();
        musicSource.clip = null;
    }

    /// <summary>
    /// Set BGM volume directly (0–1). Useful for options menus.
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        musicSource.volume = Mathf.Clamp01(volume);
    }

    // -----------------------------------------------------------------------
    // BGM convenience wrappers — one method per Sonic Heroes track
    // Grouped to mirror the Complete Trinity disc structure.
    // -----------------------------------------------------------------------

    // ── Disc 1 · Opening ────────────────────────────────────────────────
    public void PlayBGM_OpeningVer() => PlayMusic(sonicHeroesOpeningVer);

    // ── Disc 1 · Action Stages ──────────────────────────────────────────
    public void PlayBGM_SeasideHill() => PlayMusic(seasideHillBGM);
    public void PlayBGM_OceanPalace() => PlayMusic(oceanPalaceBGM);
    public void PlayBGM_GrandMetropolis() => PlayMusic(grandMetropolisBGM);
    public void PlayBGM_PowerPlant() => PlayMusic(powerPlantBGM);
    public void PlayBGM_CasinoPark() => PlayMusic(casinoParkBGM);
    public void PlayBGM_BingoHighway() => PlayMusic(bingoHighwayBGM);
    public void PlayBGM_RailCanyon() => PlayMusic(railCanyonBGM);
    public void PlayBGM_BulletStation() => PlayMusic(bulletStationBGM);

    // ── Disc 1 · Special Stage ──────────────────────────────────────────
    public void PlayBGM_SpecialStageBonusChallenge() => PlayMusic(specialStageBonusChallengeBGM);

    // ── Disc 1 · Bosses ─────────────────────────────────────────────────
    public void PlayBGM_BossEggHawk() => PlayMusic(bossEggHawkBGM);
    public void PlayBGM_BossVsTeamBattle() => PlayMusic(bossVsTeamBattleBGM);
    public void PlayBGM_BossRobotCarnivalRobotStorm() => PlayMusic(bossRobotCarnivalRobotStormBGM);
    public void PlayBGM_BossEggAlbatross() => PlayMusic(bossEggAlbatrossBGM);

    // ── Disc 1 · Events / Cutscenes ─────────────────────────────────────
    public void PlayBGM_EventStrangeGuys() => PlayMusic(eventStrangeGuysBGM);
    public void PlayBGM_EventMonkeyBusiness() => PlayMusic(eventMonkeyBusinessBGM);
    public void PlayBGM_EventMyWorld() => PlayMusic(eventMyWorldBGM);
    public void PlayBGM_EventDisquietingShadow() => PlayMusic(eventDisquietingShadowBGM);

    // ── Disc 1 · System Screens ─────────────────────────────────────────
    public void PlayBGM_SystemScreenSelect() => PlayMusic(systemScreenSelectBGM);
    public void PlayBGM_SystemScreenMenu() => PlayMusic(systemScreenMenuBGM);
    public void PlayBGM_SystemScreen2PVsMenu() => PlayMusic(systemScreen2PVsMenuBGM);

    // ── Disc 1 · 2P Battle Mode ─────────────────────────────────────────
    public void PlayBGM_Battle2PCasinoArea() => PlayMusic(battle2PCasinoAreaBGM);
    public void PlayBGM_Battle2PCityArea() => PlayMusic(battle2PCityAreaBGM);
    public void PlayBGM_Battle2PSeaArea() => PlayMusic(battle2PSeaAreaBGM);
    public void PlayBGM_Battle2PQuickRace() => PlayMusic(battle2PQuickRaceBGM);
    public void PlayBGM_Battle2PRingRace() => PlayMusic(battle2PRingRaceBGM);

    // ── Disc 2 · Title ──────────────────────────────────────────────────
    public void PlayBGM_TitleVer() => PlayMusic(sonicHeroesTitleVer);

    // ── Disc 2 · Action Stages ──────────────────────────────────────────
    public void PlayBGM_SeaGate() => PlayMusic(seaGateBGM);
    public void PlayBGM_FrogForest() => PlayMusic(frogForestBGM);
    public void PlayBGM_LostJungle() => PlayMusic(lostJungleBGM);
    public void PlayBGM_HangCastle() => PlayMusic(hangCastleBGM);
    public void PlayBGM_MysticMansion() => PlayMusic(mysticMansionBGM);
    public void PlayBGM_EggFleet() => PlayMusic(eggFleetBGM);
    public void PlayBGM_FinalFortress() => PlayMusic(finalFortressBGM);

    // ── Disc 2 · Special Stage ──────────────────────────────────────────
    public void PlayBGM_SpecialStageEmeraldChallenge() => PlayMusic(specialStageEmeraldChallengeBGM);

    // ── Disc 2 · Bosses (including Last Story) ───────────────────────────
    public void PlayBGM_BossEggEmperor() => PlayMusic(bossEggEmperorBGM);
    public void PlayBGM_LastBossMetalMadness() => PlayMusic(lastBossMetalMadnessBGM);
    // "What I'm Made Of..." — Crush 40. Plays over the Metal Overlord fight
    // AND continues into the Last Story ending / true staff roll.
    public void PlayBGM_LastBossMetalOverlord_WhatImMadeOf() => PlayMusic(lastBossMetalOverlordWhatImMadeOfBGM);

    // ── Disc 2 · Events / Cutscenes ─────────────────────────────────────
    public void PlayBGM_EventExcuseMe() => PlayMusic(eventExcuseMeBGM);
    public void PlayBGM_EventUnexpectedEncounter() => PlayMusic(eventUnexpectedEncounterBGM);
    public void PlayBGM_EventNoPastToRemember() => PlayMusic(eventNoPastToRememberBGM);
    public void PlayBGM_EventMyAmbition() => PlayMusic(eventMyAmbitionBGM);
    public void PlayBGM_EventMetalSonicUltimateOverlord() => PlayMusic(eventMetalSonicUltimateOverlordBGM);
    public void PlayBGM_EventAllHeroesGather() => PlayMusic(eventAllHeroesGatherBGM);
    // Epilogue stinger — plays as each team departs after their ending cutscene.
    public void PlayBGM_EventFinaleAdventureMustGoOn() => PlayMusic(eventFinaleAdventureMustGoOnBGM);

    // ── Disc 2 · Bonus Tracks ───────────────────────────────────────────
    public void PlayBGM_SpecialStageBonusChallengeExtended() => PlayMusic(specialStageBonusChallengeExtendedBGM);
    public void PlayBGM_CasinoParkOriginalVer() => PlayMusic(casinoParkOriginalVerBGM);
    public void PlayBGM_BingoHighwayRemixVer() => PlayMusic(bingoHighwayRemixVerBGM);

    // ── Triple Threat · Vocal / Story / Ending Themes ───────────────────
    // Call these during each team's FMV intro, story ending, or credits roll.
    public void PlayVocal_SonicHeroes() => PlayMusic(vocalSonicHeroes);
    public void PlayVocal_WeCanTeamSonic() => PlayMusic(vocalWeCanTeamSonic);
    public void PlayVocal_ThisMachineTeamDark() => PlayMusic(vocalThisMachineTeamDark);
    public void PlayVocal_FollowMeTeamRose() => PlayMusic(vocalFollowMeTeamRose);
    public void PlayVocal_TeamChaotix() => PlayMusic(vocalTeamChaotix);
    // True ending / Last Story credits — "What I'm Made of..." vocal version.
    public void PlayVocal_WhatImMadeOfLastStory() => PlayMusic(vocalWhatImMadeOfLastStory);

    // -----------------------------------------------------------------------
    // Jingles — Sonic Heroes pauses BGM, plays jingle, then resumes BGM
    // -----------------------------------------------------------------------

    /// <summary>
    /// Play a jingle (goal ring, extra life, game over).
    /// BGM is paused for the duration and then resumed, exactly as in Sonic Heroes.
    /// </summary>
    public void PlayJingle(AudioClip jingle)
    {
        if (jingle == null)
        {
            Debug.LogWarning("[AudioManager] PlayJingle called with a null clip.");
            return;
        }

        if (jingleCoroutine != null)
            StopCoroutine(jingleCoroutine);

        jingleCoroutine = StartCoroutine(PlayJingleThenResumeBGM(jingle));
    }

    // Convenience wrappers matching Sonic Heroes in-game events
    public void PlayJingle_GoalRing() => PlayJingle(jingleGoalRing);
    public void PlayJingle_ExtraLife() => PlayJingle(jingleExtraLife);
    public void PlayJingle_GameOver() => PlayJingle(jingleGameOver);
    public void PlayJingle_MissionStart() => PlayJingle(jingleMissionStart);
    public void PlayJingle_SpeedUp() => PlayJingle(jingleSpeedUp);
    public void PlayJingle_Invincible() => PlayJingle(jingleInvincible);

    // -----------------------------------------------------------------------
    // SFX — pooled, capped, one-shot
    // -----------------------------------------------------------------------

    /// <summary>
    /// Play a sound effect from the pre-warmed pool.
    /// If the pool is exhausted the oldest free source is recycled.
    /// </summary>
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[AudioManager] PlaySFX called with a null clip.");
            return;
        }

        AudioSource source = GetFreeSFXSource();
        if (source == null)
        {
            Debug.LogWarning("[AudioManager] SFX pool exhausted; consider raising maxSFXSources.");
            return;
        }

        source.clip = clip;
        source.Play();
        StartCoroutine(ReturnSFXSourceToPool(source));
    }

    /// <summary>
    /// Set SFX volume across all pooled sources (0–1).
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        foreach (AudioSource src in sfxPool)
            src.volume = volume;
    }

    // -----------------------------------------------------------------------
    // Coroutines
    // -----------------------------------------------------------------------

    private IEnumerator CrossFadeMusic(AudioClip newClip)
    {
        float elapsed = 0f;
        float startVolume = musicSource.volume;

        // Fade out current track
        while (elapsed < musicFadeDuration)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / musicFadeDuration);
            yield return null;
        }

        musicSource.Stop();
        musicSource.clip = newClip;
        musicSource.volume = 0f;
        musicSource.Play();

        // Fade in new track
        elapsed = 0f;
        while (elapsed < musicFadeDuration)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(0f, startVolume, elapsed / musicFadeDuration);
            yield return null;
        }

        musicSource.volume = startVolume;
        fadeCoroutine = null;
    }

    private IEnumerator PlayJingleThenResumeBGM(AudioClip jingle)
    {
        // Snapshot BGM state before interrupting
        pendingResumeClip = musicSource.clip;
        pendingResumeTime = musicSource.time;

        musicSource.Pause();

        jingleSource.clip = jingle;
        jingleSource.Play();

        // Wait for the jingle to finish using clip length — avoids WaitUntil hang
        yield return new WaitForSeconds(jingle.length);

        // Resume BGM if it hasn't been changed externally during the jingle
        if (pendingResumeClip != null && musicSource.clip == pendingResumeClip)
        {
            musicSource.time = pendingResumeTime;
            musicSource.UnPause();
        }

        jingleCoroutine = null;
    }

    private IEnumerator ReturnSFXSourceToPool(AudioSource source)
    {
        // Wait the exact clip length; avoids WaitUntil hanging on paused sources
        yield return new WaitForSeconds(source.clip.length);
        source.Stop();
        source.clip = null;
        sfxPool.Enqueue(source);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private AudioSource GetFreeSFXSource()
    {
        // Dequeue a source that has finished playing
        int attempts = sfxPool.Count;
        for (int i = 0; i < attempts; i++)
        {
            AudioSource candidate = sfxPool.Dequeue();
            if (!candidate.isPlaying)
                return candidate;

            // Still in use — put it back and try the next
            sfxPool.Enqueue(candidate);
        }
        return null; // All sources busy
    }
}