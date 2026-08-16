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

/// <summary>
/// Add this beside an AudioSource and choose Music or SoundEffects to make it
/// follow the corresponding menu slider while preserving its authored volume.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public sealed class GameAudioSource : MonoBehaviour
{
    [SerializeField] private GameAudioChannel channel = GameAudioChannel.SoundEffects;
    [SerializeField, Range(0f, 1f)] private float baseVolume = 1f;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        ApplyVolume();
    }

    private void OnEnable()
    {
        GameAudioSettings.VolumesChanged += ApplyVolume;
        ApplyVolume();
    }

    private void OnDisable()
    {
        GameAudioSettings.VolumesChanged -= ApplyVolume;
    }

    private void OnValidate()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        ApplyVolume();
    }

    private void ApplyVolume()
    {
        if (audioSource != null)
        {
            audioSource.volume = baseVolume * GameAudioSettings.GetChannelVolume(channel);
        }
    }
}

public enum GameSfx
{
    BreakableDestroyed,
    HealingActivated,
    ShieldActivated,
    ThrowingKnifeLaunched
}

/// <summary>Centralized, persistent playback for shared 2D sound effects.</summary>
public sealed class GameAudioManager : MonoBehaviour
{
    private const string BattleMusicResourcePath = "Audio/BG/BGM_InkWuxia_MistBlade_RapidCombat_Loop";

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
        { GameSfx.ThrowingKnifeLaunched, "Audio/SE/SFX_Weapon_ThrowingKnife_Launch" }
    };

    private static readonly Dictionary<GameSfx, AudioClip> ClipCache = new();
    private static readonly HashSet<GameSfx> MissingClipWarnings = new();
    private static GameAudioManager instance;

    private AudioSource sfxSource;
    private AudioSource musicSource;
    private AudioClip battleMusicClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        ClipCache.Clear();
        MissingClipWarnings.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() => EnsureInstance();

    public static void PlaySfx(GameSfx sound, float volumeScale = 1f)
    {
        GameAudioManager manager = EnsureInstance();
        AudioClip clip = GetClip(sound);
        if (clip == null) return;

        manager.sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    private static GameAudioManager EnsureInstance()
    {
        if (instance != null) return instance;

        instance = FindAnyObjectByType<GameAudioManager>();
        if (instance != null) return instance;

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
        sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
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

    private void OnDisable()
    {
        GameAudioSettings.VolumesChanged -= ApplyVolume;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void ApplyVolume()
    {
        if (sfxSource != null)
            sfxSource.volume = GameAudioSettings.GetChannelVolume(GameAudioChannel.SoundEffects);

        if (musicSource != null)
            musicSource.volume = GameAudioSettings.GetChannelVolume(GameAudioChannel.Music);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyMusicForScene(scene);
    }

    private void ApplyMusicForScene(Scene scene)
    {
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
        if (musicSource == null || musicSource.isPlaying) return;

        battleMusicClip ??= Resources.Load<AudioClip>(BattleMusicResourcePath);
        if (battleMusicClip == null)
        {
            Debug.LogWarning($"GameAudioManager could not load Resources/{BattleMusicResourcePath}.");
            return;
        }

        musicSource.clip = battleMusicClip;
        musicSource.Play();
    }
}
