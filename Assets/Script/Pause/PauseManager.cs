using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    [Header("UI Groups")]
    public GameObject pauseGroup;            // 일시정지 UI 전체를 감싸는 부모
    public GameObject pauseMenuContent;      // 기본 메뉴 창 (버튼들)
    public GameObject settingsContentsGroup; // 설정 창

    [Header("Volume Settings")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    private bool isPaused = false;

    void Start()
    {
        // Ensure the game is not paused and the pause menu is hidden at the start
        Time.timeScale = 1f;
        pauseGroup.SetActive(false);

        // AudioManager에서 현재 볼륨 값을 가져와 슬라이더에 설정
        if (AudioManager.Instance != null)
        {
            // SetValueWithoutNotify를 사용하여 이벤트가 발생하지 않도록 값을 설정
            bgmSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("BGMVolume", 0.75f));
            sfxSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("SFXVolume", 0.75f));
        }

        // 슬라이더 이벤트에 리스너 추가
        bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
        sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
    }

    void Update()
    {
        // ESC 키 입력 감지
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // 설정 창이 활성화되어 있으면 설정 창을 닫음
            if (settingsContentsGroup != null && settingsContentsGroup.activeSelf)
            {
                CloseSettings();
            }
            // 일시정지 상태가 아니면 일시정지
            else if (!isPaused)
            {
                PauseGame();
            }
            // 일시정지 상태면 게임 재개
            else
            {
                ResumeGame();
            }
        }
    }

    private void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f; // 시간 흐름을 멈춤

        pauseGroup.SetActive(true);
        pauseMenuContent.SetActive(true);
        settingsContentsGroup.SetActive(false);
    }

    // '계속하기' 버튼에 연결될 함수
    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f; // 시간 흐름을 되돌림
        pauseGroup.SetActive(false);
    }

    // '설정' 버튼에 연결될 함수
    public void OpenSettings()
    {
        pauseMenuContent.SetActive(false);
        settingsContentsGroup.SetActive(true);
    }

    // 설정 창의 '닫기' 버튼에 연결될 함수
    public void CloseSettings()
    {
        settingsContentsGroup.SetActive(false);
        pauseMenuContent.SetActive(true);
    }

    // '게임 종료' 버튼에 연결될 함수
    public void ExitGame()
    {
        // 유니티 에디터에서는 플레이 모드를 중지하고,
        // 빌드된 게임에서는 애플리케이션을 종료합니다.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // BGM 슬라이더 값이 변경될 때 호출될 함수
    public void OnBGMVolumeChanged(float volume)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetBGMVolume(volume);
        }
    }

    // SFX 슬라이더 값이 변경될 때 호출될 함수
    public void OnSFXVolumeChanged(float volume)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(volume);
        }
    }

    // '타이틀로 돌아가기' 버튼에 연결될 함수
    public void ReturnToTitle()
    {
        Time.timeScale = 1f; // 시간 흐름을 되돌림
        TimeManager.elapsedTime = 0f; // 타이머 초기화
        SceneManager.LoadScene("FIXER Title"); // "FIXER Title" 씬을 불러옴
    }
}
