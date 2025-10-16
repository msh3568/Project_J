using UnityEngine;
using UnityEngine.SceneManagement; // 씬 전환을 위해 꼭 필요합니다!

public class TitleManager : MonoBehaviour
{
    [Header("UI Group Objects")]
    public GameObject titleContentsGroup;    // "게임 시작" 등 기본 버튼이 있는 그룹
    public GameObject settingsContentsGroup; // 설정 UI 요소들이 있는 그룹

    /// <summary>
    /// '게임 시작' 버튼을 클릭했을 때 호출됩니다.
    /// </summary>
    public void OnStartButtonClick()
    {
        // "GameScene"이라는 이름의 씬을 불러옵니다.
        // 프로젝트에 있는 씬 파일 이름과 정확히 같아야 합니다.
        SceneManager.LoadScene("GameScene");
    }

    /// <summary>
    /// '설정' 버튼을 클릭했을 때 호출됩니다.
    /// </summary>
    public void OnSettingsButtonClick()
    {
        // 타이틀 기본 UI는 끄고
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
    /// 설정 화면의 '닫기' 버튼을 클릭했을 때 호출됩니다.
    /// </summary>
    public void OnSettingsCloseButtonClick()
    {
        // 설정 UI는 끄고
        if (settingsContentsGroup != null)
        {
            settingsContentsGroup.SetActive(false);
        }

        // 타이틀 기본 UI는 다시 켭니다.
        if (titleContentsGroup != null)
        {
            titleContentsGroup.SetActive(true);
        }
    }

    /// <summary>
    /// '게임 종료' 버튼을 클릭했을 때 호출됩니다.
    /// </summary>
    public void OnExitButtonClick()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}