using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // UI 관련 클래스를 사용하기 위해 추가

public class TitleManager : MonoBehaviour
{
    [Header("UI Group Objects")]
    public GameObject titleContentsGroup;    // 타이틀의 기본 버튼들이 있는 그룹
    public GameObject settingsContentsGroup; // 설정 UI 요소들이 있는 그룹

    [Header("Volume Settings")]
    [SerializeField] private Slider bgmSlider; // BGM 볼륨 조절 슬라이더
    [SerializeField] private Slider sfxSlider; // SFX 볼륨 조절 슬라이더

    void Start()
    {
        // AudioManager가 존재하고, 슬라이더들이 할당되어 있을 때
        if (AudioManager.Instance != null && bgmSlider != null && sfxSlider != null)
        {
            // SetValueWithoutNotify를 사용하여 이벤트가 발생하지 않도록 값을 설정
            bgmSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("BGMVolume", 0.75f));
            sfxSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("SFXVolume", 0.75f));

            // 슬라이더 값 변경 이벤트에 리스너 추가
            bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }
    }

    /// <summary>
    /// '게임 시작' 버튼 클릭 시 호출됩니다.
    /// </summary>
    public void OnStartButtonClick()
    {
        if (AnalyticsManager.Instance != null)
        {
            AnalyticsManager.Instance.StartSession();
        }

        // "GameScene"이라는 이름의 씬을 불러옵니다.
        // 프로젝트에 있는 씬 이름과 일치하는지 확인해야 합니다.
        SceneManager.LoadScene("GameScene");
    }

    /// <summary>
    /// '설정' 버튼 클릭 시 호출됩니다.
    /// </summary>
    public void OnSettingsButtonClick()
    {
        // 타이틀 기본 UI를 비활성화합니다.
        if (titleContentsGroup != null)
        {
            titleContentsGroup.SetActive(false);
        }

        // 설정 UI를 활성화합니다.
        if (settingsContentsGroup != null)
        {
            settingsContentsGroup.SetActive(true);
        }
    }

    /// <summary>
    /// 설정 화면의 '닫기' 버튼 클릭 시 호출됩니다.
    /// </summary>
    public void OnSettingsCloseButtonClick()
    {
        // 설정 UI를 비활성화합니다.
        if (settingsContentsGroup != null)
        {
            settingsContentsGroup.SetActive(false);
        }

        // 타이틀 기본 UI를 다시 활성화합니다.
        if (titleContentsGroup != null)
        {
            titleContentsGroup.SetActive(true);
        }
    }

    /// <summary>
    /// '게임 종료' 버튼 클릭 시 호출됩니다.
    /// </summary>
    public void OnExitButtonClick()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // BGM 슬라이더 값이 변경될 때 호출될 함수
    private void OnBGMVolumeChanged(float volume)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetBGMVolume(volume);
        }
    }

    // SFX 슬라이더 값이 변경될 때 호출될 함수
    private void OnSFXVolumeChanged(float volume)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(volume);
        }
    }
}