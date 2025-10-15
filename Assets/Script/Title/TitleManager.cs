using UnityEngine;
// 한 씬에서 모든 것을 관리하므로 SceneManagement는 필요 없습니다.

public class TitleManager : MonoBehaviour
{
    [Header("UI Group Objects")]
    public GameObject titleContentsGroup;    // 타이틀 화면 UI 그룹
    public GameObject settingsContentsGroup; // 설정 화면 UI 그룹
    public GameObject inGameGroup;           // 인게임 플레이 UI 및 요소 그룹

    /// <summary>
    /// 게임 시작 버튼을 클릭했을 때 호출됩니다.
    /// </summary>
    public void OnStartButtonClick()
    {
        Debug.Log("게임 시작 버튼 클릭!");

        // 타이틀 UI는 끄고
        if (titleContentsGroup != null)
        {
            titleContentsGroup.SetActive(false);
        }

        // 인게임 요소들은 켭니다.
        if (inGameGroup != null)
        {
            inGameGroup.SetActive(true);
        }
    }

    /// <summary>
    /// 설정 버튼을 클릭했을 때 호출됩니다.
    /// </summary>
    public void OnSettingsButtonClick()
    {
        Debug.Log("설정 버튼 클릭!");

        // 타이틀 UI는 끄고
        if (titleContentsGroup != null)
        {
            titleContentsGroup.SetActive(false);
        }

        // 설정 UI는 켭니다.
        if (settingsContentsGroup != null)
        {
            settingsContentsGroup.SetActive(true);
        }
    }

    /// <summary>
    /// 설정 화면의 닫기 버튼을 클릭했을 때 호출됩니다.
    /// </summary>
    public void OnSettingsCloseButtonClick()
    {
        Debug.Log("설정 닫기 버튼 클릭!");

        // 설정 UI는 끄고
        if (settingsContentsGroup != null)
        {
            settingsContentsGroup.SetActive(false);
        }

        // 타이틀 UI는 다시 켭니다.
        if (titleContentsGroup != null)
        {
            titleContentsGroup.SetActive(true);
        }
    }

    /// <summary>
    /// 게임 종료 버튼을 클릭했을 때 호출됩니다.
    /// </summary>
    public void OnExitButtonClick()
    {
        Debug.Log("게임 종료 버튼 클릭!");

        // 유니티 에디터에서 실행 중일 경우 플레이 모드를 중지합니다.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // 빌드된 게임에서는 어플리케이션을 종료합니다.
        Application.Quit();
#endif
    }
}