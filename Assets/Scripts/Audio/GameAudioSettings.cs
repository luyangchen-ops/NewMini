using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameAudioChannel
{
    Music,
    SoundEffects
}

/// <summary>Persistent volume state shared by every scene.</summary>
public static class GameAudioSettings
{
    private const string MasterKey = "audio.master";
    private const string MusicKey = "audio.music";
    private const string SfxKey = "audio.sfx";

    private static float masterVolume = 1f;
    private static float musicVolume = 0.8f;
    private static float sfxVolume = 0.8f;

    public static event Action VolumesChanged;

    public static float MasterVolume
    {
        get => masterVolume;
        set
        {
            masterVolume = Mathf.Clamp01(value);
            AudioListener.volume = masterVolume;
            VolumesChanged?.Invoke();
        }
    }

    public static float MusicVolume
    {
        get => musicVolume;
        set
        {
            musicVolume = Mathf.Clamp01(value);
            VolumesChanged?.Invoke();
        }
    }

    public static float SfxVolume
    {
        get => sfxVolume;
        set
        {
            sfxVolume = Mathf.Clamp01(value);
            VolumesChanged?.Invoke();
        }
    }

    public static float GetChannelVolume(GameAudioChannel channel) =>
        channel == GameAudioChannel.Music ? musicVolume : sfxVolume;

    public static void Load()
    {
        masterVolume = PlayerPrefs.GetFloat(MasterKey, 1f);
        musicVolume = PlayerPrefs.GetFloat(MusicKey, 0.8f);
        sfxVolume = PlayerPrefs.GetFloat(SfxKey, 0.8f);
        AudioListener.volume = masterVolume;
        VolumesChanged?.Invoke();
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(MasterKey, masterVolume);
        PlayerPrefs.SetFloat(MusicKey, musicVolume);
        PlayerPrefs.SetFloat(SfxKey, sfxVolume);
        PlayerPrefs.Save();
    }
}

public enum GameSfx
{
    BreakableDestroyed,
    HealingActivated,
    ShieldActivated,
    ThrowingKnifeLaunched,
    ShieldWarriorBlock,
    ShieldWarriorAttack
}

/// <summary>Centralized, persistent playback for shared 2D sound effects.</summary>
[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public sealed class GameAudioManager : MonoBehaviour
{
    private const int MaximumSfxVoices = 32;
    private const string BattleMusicResourcePath = "Audio/BG/BGM_InkWuxia_MistBlade_RapidCombat_Loop";
    private const string BossMusicResourcePath = "Audio/BG/Boss BG";

    private static readonly HashSet<string> BattleSceneNames = new()
    {
        "Extra",
        "Level_01_BambooCourtyard",
        "Level_02_InkSnowCourtyard",
        "Level_LD",
        "clyTest"
    };

    private static readonly Dictionary<GameSfx, string> ResourcePaths = new()
    {
        { GameSfx.BreakableDestroyed, "Audio/SE/SFX_Prop_WoodBarrel_Break" },
        { GameSfx.HealingActivated, "Audio/SE/SFX_Item_Heal_Activate" },
        { GameSfx.ShieldActivated, "Audio/SE/SFX_Item_Shield_Activate" },
        { GameSfx.ThrowingKnifeLaunched, "Audio/SE/SFX_Weapon_ThrowingKnife_Launch" },
        { GameSfx.ShieldWarriorBlock, "Audio/SE/Enemy/ShieldWarrior_Block" },
        { GameSfx.ShieldWarriorAttack, "Audio/SE/Enemy/ShieldWarrior_Attack" }
    };

    private static readonly Dictionary<GameSfx, AudioClip> ClipCache = new();
    private static readonly HashSet<GameSfx> MissingClipWarnings = new();
    private static GameAudioManager instance;
    private static bool musicPausedForDialogue;
    private static bool bossMusicLocked;
    private static ulong bossMusicSceneHandleRaw;

    private sealed class SfxVoice
    {
        public AudioSource Source;
        public float VolumeScale;
        public float StartedAt;
    }

    private readonly List<SfxVoice> sfxVoices = new();
    private AudioSource loopSfxSource;
    private AudioSource musicSource;
    private float loopSfxVolumeScale;
    private AudioClip battleMusicClip;
    private AudioClip bossMusicClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        musicPausedForDialogue = false;
        bossMusicLocked = false;
        bossMusicSceneHandleRaw = 0;
        ClipCache.Clear();
        MissingClipWarnings.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() => EnsureInstance();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RefreshMusicAfterSceneLoad()
    {
        GameAudioManager manager = EnsureInstance();
        manager.ApplyMusicForScene(SceneManager.GetActiveScene());
    }

    public static void PlaySfx(GameSfx sound, float volumeScale = 1f)
    {
        AudioClip clip = GetClip(sound);
        PlaySfx(clip, volumeScale);
    }

    /// <summary>Switches the shared looping music source to the final Boss theme.</summary>
    public static void PlayBossMusic()
    {
        musicPausedForDialogue = false;
        bossMusicLocked = true;
        bossMusicSceneHandleRaw = SceneManager.GetActiveScene().handle.GetRawData();
        GameAudioManager manager = EnsureInstance();
        manager.PlayMusic(ref manager.bossMusicClip, BossMusicResourcePath);
    }

    /// <summary>Stops shared music until an explicit clip is played or a new scene loads.</summary>
    public static void StopMusicForDialogue()
    {
        musicPausedForDialogue = true;
        GameAudioManager manager = EnsureInstance();
        if (manager.musicSource != null) manager.musicSource.Stop();
    }

    /// <summary>Resumes the music selected for the active scene after an interrupted dialogue.</summary>
    public static void ResumeSceneMusic()
    {
        musicPausedForDialogue = false;
        GameAudioManager manager = EnsureInstance();
        manager.ApplyMusicForScene(SceneManager.GetActiveScene());
    }

    /// <summary>
    /// Plays an authored sound effect through the shared SFX source so it is
    /// controlled by the Sound Effects slider in the main menu.
    /// </summary>
    public static void PlaySfx(AudioClip clip, float volumeScale = 1f, float pitch = 1f)
    {
        if (clip == null) return;

        GameAudioManager manager = EnsureInstance();
        manager.PlaySfxInternal(clip, volumeScale, pitch);
    }

    /// <summary>Starts or updates the shared looping sound-effect channel.</summary>
    public static void SetSfxLoop(AudioClip clip, float volumeScale, bool shouldPlay)
    {
        GameAudioManager manager = EnsureInstance();
        manager.SetSfxLoopInternal(clip, volumeScale, shouldPlay);
    }

    /// <summary>Stops the shared looping sound effect if it is playing the supplied clip.</summary>
    public static void StopSfxLoop(AudioClip clip = null)
    {
        if (instance == null || instance.loopSfxSource == null) return;
        if (clip != null && instance.loopSfxSource.clip != clip) return;

        instance.loopSfxSource.Stop();
        instance.loopSfxSource.clip = null;
        instance.loopSfxVolumeScale = 0f;
    }

    private static GameAudioManager EnsureInstance()
    {
        if (instance != null)
        {
            instance.EnsureAudioSources();
            return instance;
        }

        instance = FindAnyObjectByType<GameAudioManager>();
        if (instance != null)
        {
            instance.EnsureAudioSources();
            return instance;
        }

        GameObject managerObject = new GameObject("Audio_GameAudioManager");
        instance = managerObject.AddComponent<GameAudioManager>();
        DontDestroyOnLoad(managerObject);
        return instance;
    }

    private static AudioClip GetClip(GameSfx sound)
    {
        if (ClipCache.TryGetValue(sound, out AudioClip cachedClip)) return cachedClip;
        if (!ResourcePaths.TryGetValue(sound, out string path)) return null;

        AudioClip clip = Resources.Load<AudioClip>(path);
        if (clip != null)
        {
            ClipCache.Add(sound, clip);
            return clip;
        }

        if (MissingClipWarnings.Add(sound))
            Debug.LogWarning($"GameAudioManager could not load Resources/{path}.");
        return null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureAudioSources();
        ApplyVolume();
    }

    private void OnEnable()
    {
        GameAudioSettings.VolumesChanged += ApplyVolume;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ApplyVolume();
        ApplyMusicForScene(SceneManager.GetActiveScene());
    }

    private void Start()
    {
        // Covers direct play-mode entry when scene reload is disabled and no
        // SceneManager.sceneLoaded callback is emitted for the active scene.
        ApplyMusicForScene(SceneManager.GetActiveScene());
    }

    private void Update()
    {
        EnsureAudioSources();
        if (musicSource == null || musicSource.isPlaying || musicPausedForDialogue) return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (IsBossMusicLockedFor(activeScene))
        {
            PlayBossMusic();
            return;
        }

        if (BattleSceneNames.Contains(activeScene.name))
        {
            PlayBattleMusic();
        }
    }

    private void OnDisable()
    {
        GameAudioSettings.VolumesChanged -= ApplyVolume;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void ApplyVolume()
    {
        EnsureAudioSources();
        float sfxVolume = GameAudioSettings.GetChannelVolume(GameAudioChannel.SoundEffects);
        foreach (SfxVoice voice in sfxVoices)
            if (voice.Source != null)
                voice.Source.volume = voice.VolumeScale * sfxVolume;

        if (loopSfxSource != null)
            loopSfxSource.volume = loopSfxVolumeScale * GameAudioSettings.GetChannelVolume(GameAudioChannel.SoundEffects);

        if (musicSource != null)
            musicSource.volume = GameAudioSettings.GetChannelVolume(GameAudioChannel.Music);
    }

    private void SetSfxLoopInternal(AudioClip clip, float volumeScale, bool shouldPlay)
    {
        EnsureAudioSources();
        loopSfxVolumeScale = Mathf.Clamp01(volumeScale);
        loopSfxSource.volume = loopSfxVolumeScale * GameAudioSettings.GetChannelVolume(GameAudioChannel.SoundEffects);

        if (!shouldPlay || clip == null || loopSfxVolumeScale <= .001f)
        {
            if (loopSfxSource.isPlaying) loopSfxSource.Stop();
            loopSfxSource.clip = null;
            return;
        }

        if (loopSfxSource.clip != clip)
        {
            loopSfxSource.Stop();
            loopSfxSource.clip = clip;
        }

        if (!loopSfxSource.isPlaying) loopSfxSource.Play();
    }

    private void PlaySfxInternal(AudioClip clip, float volumeScale, float pitch)
    {
        EnsureAudioSources();
        SfxVoice voice = GetAvailableSfxVoice();
        voice.Source.Stop();
        voice.Source.clip = clip;
        voice.Source.pitch = Mathf.Clamp(pitch, .01f, 3f);
        voice.VolumeScale = Mathf.Clamp01(volumeScale);
        voice.Source.volume = voice.VolumeScale * GameAudioSettings.GetChannelVolume(GameAudioChannel.SoundEffects);
        voice.StartedAt = Time.unscaledTime;
        voice.Source.Play();
    }

    private SfxVoice GetAvailableSfxVoice()
    {
        foreach (SfxVoice voice in sfxVoices)
            if (!voice.Source.isPlaying)
                return voice;

        if (sfxVoices.Count < MaximumSfxVoices)
            return CreateSfxVoice();

        SfxVoice oldestVoice = sfxVoices[0];
        for (int i = 1; i < sfxVoices.Count; i++)
            if (sfxVoices[i].StartedAt < oldestVoice.StartedAt)
                oldestVoice = sfxVoices[i];
        return oldestVoice;
    }

    private SfxVoice CreateSfxVoice(AudioSource source = null)
    {
        // Unity's destroyed-object sentinel is not CLR null, so ??= is unsafe here
        // when Enter Play Mode Options keeps the domain alive between sessions.
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.priority = 64;

        SfxVoice voice = new() { Source = source };
        sfxVoices.Add(voice);
        return voice;
    }

    private void EnsureAudioSources()
    {
        for (int i = sfxVoices.Count - 1; i >= 0; i--)
            if (sfxVoices[i] == null || sfxVoices[i].Source == null)
                sfxVoices.RemoveAt(i);

        if (sfxVoices.Count == 0)
            CreateSfxVoice(GetComponent<AudioSource>());

        if (loopSfxSource == null)
        {
            loopSfxSource = gameObject.AddComponent<AudioSource>();
            loopSfxSource.playOnAwake = false;
            loopSfxSource.loop = true;
            loopSfxSource.spatialBlend = 0f;
            loopSfxSource.priority = 160;
        }

        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        musicPausedForDialogue = false;
        StopAllSfx();
        StopSfxLoop();
        Scene activeScene = SceneManager.GetActiveScene();
        if (bossMusicLocked && activeScene.handle.GetRawData() != bossMusicSceneHandleRaw)
        {
            bossMusicLocked = false;
            bossMusicSceneHandleRaw = 0;
        }
        ApplyMusicForScene(activeScene);
    }

    private void StopAllSfx()
    {
        foreach (SfxVoice voice in sfxVoices)
        {
            if (voice.Source == null) continue;
            voice.Source.Stop();
            voice.Source.clip = null;
            voice.VolumeScale = 0f;
        }
    }

    private void ApplyMusicForScene(Scene scene)
    {
        if (musicPausedForDialogue)
        {
            if (musicSource != null && musicSource.isPlaying) musicSource.Stop();
            return;
        }

        if (IsBossMusicLockedFor(scene))
        {
            PlayBossMusic();
            return;
        }

        if (BattleSceneNames.Contains(scene.name))
        {
            PlayBattleMusic();
            return;
        }

        if (musicSource != null && musicSource.isPlaying)
            musicSource.Stop();
    }

    private void PlayBattleMusic()
    {
        PlayMusic(ref battleMusicClip, BattleMusicResourcePath);
    }

    private void PlayMusic(ref AudioClip clip, string resourcePath)
    {
        EnsureAudioSources();
        if (musicSource == null) return;

        // Keep all shared BGM looping even if another runtime component changed
        // the AudioSource configuration after the manager was created.
        musicSource.loop = true;

        clip ??= Resources.Load<AudioClip>(resourcePath);
        if (clip == null)
        {
            Debug.LogWarning($"GameAudioManager could not load Resources/{resourcePath}.");
            return;
        }

        if (musicSource.clip == clip && musicSource.isPlaying) return;
        musicSource.clip = clip;
        musicSource.Play();
    }

    private static bool IsBossMusicLockedFor(Scene scene) =>
        bossMusicLocked && scene.IsValid() && scene.handle.GetRawData() == bossMusicSceneHandleRaw;
}
