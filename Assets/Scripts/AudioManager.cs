using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement; // 씬 관리를 위해 추가

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Mixer")]
    [SerializeField] public AudioMixer audioMixer;
    [SerializeField] private AudioMixerGroup bgmMixerGroup;

    private const string BGM_MIXER_PARAM = "BGM Volume";
    private const string SFX_MIXER_PARAM = "SFX Volume";
    private const string BGM_PREFS_KEY = "BGMVolume";
    private const string SFX_PREFS_KEY = "SFXVolume";

    private AudioSource bgmSource;
    private string[] scenesWithoutBGM = { "FixerEndding" }; // BGM을 재생하지 않을 씬 목록

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

        // 씬 로드 이벤트 구독
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        // 저장된 볼륨 값 불러오기 및 적용
        float bgmVolume = PlayerPrefs.GetFloat(BGM_PREFS_KEY, 0.75f);
        float sfxVolume = PlayerPrefs.GetFloat(SFX_PREFS_KEY, 0.75f);
        SetBGMVolume(bgmVolume);
        SetSFXVolume(sfxVolume);
        
        // 현재 씬을 기준으로 BGM 처리
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        // 오브젝트 파괴 시 이벤트 구독 해제
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
            // BGM을 재생해야 하는 씬
            if (bgmSource == null || !bgmSource.isPlaying)
            {
                FindAndPlayBgmSource();
            }
        }
        else
        {
            // BGM을 정지해야 하는 씬
            StopBGM();
        }
    }

    void FindAndPlayBgmSource()
    {
        // 이미 유효한 소스가 있다면 다시 찾지 않음
        if (bgmSource != null && bgmSource.isPlaying) return;

        // 1. "soundmanager" 이름으로 찾아보기
        GameObject soundManagerObj = GameObject.Find("soundmanager");
        if (soundManagerObj != null) bgmSource = soundManagerObj.GetComponent<AudioSource>();

        // 2. "GameManager" 이름으로 찾아보기
        if (bgmSource == null)
        {
            GameObject gameManagerObj = GameObject.Find("GameManager");
            if (gameManagerObj != null) bgmSource = gameManagerObj.GetComponent<AudioSource>();
        }
        
        // 3. 씬에서 재생중인 AudioSource를 찾기 (최후의 수단)
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
}