using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Mixer")]
    [SerializeField] public AudioMixer audioMixer;
    [SerializeField] private AudioMixerGroup bgmMixerGroup; // BGM 믹서 그룹을 여기에 할당

    // Mixer에 노출된 매개변수 이름 (띄어쓰기 포함)
    private const string BGM_MIXER_PARAM = "BGM Volume";
    private const string SFX_MIXER_PARAM = "SFX Volume";

    // PlayerPrefs에 저장될 키 이름 (띄어쓰기 없음)
    private const string BGM_PREFS_KEY = "BGMVolume";
    private const string SFX_PREFS_KEY = "SFXVolume";

    private AudioSource bgmSource;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // BGM AudioSource 찾기 및 믹서 그룹 할당
        FindAndAssignBgmSource();

        // 저장된 볼륨 값 불러오기
        float bgmVolume = PlayerPrefs.GetFloat(BGM_PREFS_KEY, 0.75f);
        float sfxVolume = PlayerPrefs.GetFloat(SFX_PREFS_KEY, 0.75f);

        // 믹서 볼륨 초기 설정
        SetBGMVolume(bgmVolume);
        SetSFXVolume(sfxVolume);
    }

    void FindAndAssignBgmSource()
    {
        // 1. "soundmanager" 이름으로 찾아보기
        GameObject soundManagerObj = GameObject.Find("soundmanager");
        if (soundManagerObj != null)
        {
            bgmSource = soundManagerObj.GetComponent<AudioSource>();
            if (bgmSource != null)
            {
                bgmSource.outputAudioMixerGroup = bgmMixerGroup;
                Debug.Log("BGM source found on 'soundmanager' and assigned to mixer group.");
                return;
            }
        }

        // 2. "GameManager" 이름으로 찾아보기
        GameObject gameManagerObj = GameObject.Find("GameManager");
        if (gameManagerObj != null)
        {
            bgmSource = gameManagerObj.GetComponent<AudioSource>();
            if (bgmSource != null)
            {
                bgmSource.outputAudioMixerGroup = bgmMixerGroup;
                Debug.Log("BGM source found on 'GameManager' and assigned to mixer group.");
                return;
            }
        }
        
        // 3. 씬에서 재생중인 AudioSource를 찾기 (최후의 수단)
        AudioSource[] allAudioSources = FindObjectsOfType<AudioSource>();
        foreach (AudioSource source in allAudioSources)
        {
            if (source.isPlaying && source.loop)
            {
                bgmSource = source;
                bgmSource.outputAudioMixerGroup = bgmMixerGroup;
                Debug.Log($"Looping BGM source found on '{source.gameObject.name}' and assigned to mixer group.");
                return;
            }
        }

        Debug.LogWarning("Could not find BGM AudioSource.");
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