using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    [Header("UI Groups")]
    public GameObject pauseGroup;            // 일시정지 UI 전체를 감싸는 부모
    public GameObject pauseMenuContent;      // 기본 메뉴 창 (버튼들)
    public GameObject settingsContentsGroup; // 설정 창

    private bool isPaused = false;

    void Update()
    {
        // ESC 키 입력 감지
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // 설정 창이 열려있으면 설정 창만 닫음
            if (settingsContentsGroup != null && settingsContentsGroup.activeSelf)
            {
                CloseSettings();
            }
            // 일시정지가 아니면 일시정지
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

    // '게임 재개' 버튼에 연결할 함수
    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f; // 시간 흐름을 되돌림
        pauseGroup.SetActive(false);
    }

    // '설정' 버튼에 연결할 함수
    public void OpenSettings()
    {
        pauseMenuContent.SetActive(false);
        settingsContentsGroup.SetActive(true);
    }

    // 설정 창의 '닫기' 버튼에 연결할 함수
    public void CloseSettings()
    {
        settingsContentsGroup.SetActive(false);
        pauseMenuContent.SetActive(true);
    }

    // '게임 종료' 버튼에 연결할 함수
    public void ExitGame()
    {
        // 유니티 에디터에서는 플레이 모드를 중지하고,
        // 실제 빌드된 게임에서는 어플리케이션을 종료합니다.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}