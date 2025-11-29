using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Mixer")]
    [SerializeField] public AudioMixer audioMixer;
    [SerializeField] private AudioMixerGroup bgmMixerGroup;
    [SerializeField] private AudioMixerGroup sfxMixerGroup; // For centralized SFX playback

    private const string BGM_MIXER_PARAM = "BGM Volume";
    private const string SFX_MIXER_PARAM = "SFX Volume";
    private const string BGM_PREFS_KEY = "BGMVolume";
    private const string SFX_PREFS_KEY = "SFXVolume";

    private AudioSource bgmSource;
    private AudioSource sfxSource; // Dedicated source for SFX
    private string[] scenesWithoutBGM = { "FixerEndding" }; // BGM to not play in these scenes

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (transform.parent != null)
        {
            transform.SetParent(null);
        }
        DontDestroyOnLoad(gameObject);

        // Setup SFX Source
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        if (sfxMixerGroup != null)
        {
            sfxSource.outputAudioMixerGroup = sfxMixerGroup;
        }
        else
        {
            var sfxGroups = audioMixer.FindMatchingGroups("SFX");
            if (sfxGroups.Length > 0)
            {
                sfxSource.outputAudioMixerGroup = sfxGroups[0];
            }
            else
            {
                Debug.LogWarning("AudioManager: SFX mixer group not found or assigned. SFX will play without a mixer group.");
            }
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        float bgmVolume = PlayerPrefs.GetFloat(BGM_PREFS_KEY, 0.75f);
        float sfxVolume = PlayerPrefs.GetFloat(SFX_PREFS_KEY, 0.75f);
        SetBGMVolume(bgmVolume);
        SetSFXVolume(sfxVolume);
        
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool playBGM = true;
        foreach (var sceneName in scenesWithoutBGM)
        {
            if (scene.name == sceneName)
            {
                playBGM = false;
                break;
            }
        }

        if (playBGM)
        {
            if (bgmSource == null || !bgmSource.isPlaying)
            {
                FindAndPlayBgmSource();
            }
        }
        else
        {
            StopBGM();
        }
    }

    void FindAndPlayBgmSource()
    {
        if (bgmSource != null && bgmSource.isPlaying) return;

        GameObject soundManagerObj = GameObject.Find("soundmanager");
        if (soundManagerObj != null) bgmSource = soundManagerObj.GetComponent<AudioSource>();

        if (bgmSource == null)
        {
            GameObject gameManagerObj = GameObject.Find("GameManager");
            if (gameManagerObj != null) bgmSource = gameManagerObj.GetComponent<AudioSource>();
        }
        
        if (bgmSource == null)
        {
            AudioSource[] allAudioSources = FindObjectsOfType<AudioSource>();
            foreach (AudioSource source in allAudioSources)
            {
                if (source.isPlaying && source.loop)
                {
                    bgmSource = source;
                    break;
                }
            }
        }

        if (bgmSource != null)
        {
            bgmSource.outputAudioMixerGroup = bgmMixerGroup;
            if (!bgmSource.isPlaying)
            {
                bgmSource.Play();
            }
            Debug.Log($"BGM source is now '{bgmSource.gameObject.name}'. Playing: {bgmSource.isPlaying}");
        }
        else
        {
            Debug.LogWarning("Could not find BGM AudioSource to play.");
        }
    }

    public void StopBGM()
    {
        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.Stop();
            Debug.Log($"BGM stopped on '{bgmSource.gameObject.name}'.");
        }
    }

    public void SetBGMVolume(float volume)
    {
        audioMixer.SetFloat(BGM_MIXER_PARAM, volume > 0.001f ? Mathf.Log10(volume) * 20 : -80f);
        PlayerPrefs.SetFloat(BGM_PREFS_KEY, volume);
    }

    public void SetSFXVolume(float volume)
    {
        audioMixer.SetFloat(SFX_MIXER_PARAM, volume > 0.001f ? Mathf.Log10(volume) * 20 : -80f);
        PlayerPrefs.SetFloat(SFX_PREFS_KEY, volume);
    }

    /// <summary>
    /// Plays a sound effect one time.
    /// </summary>
    /// <param name="clip">The audio clip to play.</param>
    /// <param name="volume">The volume to play the clip at (0.0 to 1.0).</param>
    public void PlaySFX(AudioClip clip, float volume = 1.0f)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, volume);
        }
    }
}