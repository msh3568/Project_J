using UnityEngine;
using UnityEngine.SceneManagement; //  ȯ   ʿմϴ!

public class TitleManager : MonoBehaviour
{
    [Header("UI Group Objects")]
    public GameObject titleContentsGroup;    // " "  ⺻ ư ִ ׷
    public GameObject settingsContentsGroup; //  UI ҵ ִ ׷

    /// <summary>
    /// ' ' ư Ŭ  ȣ˴ϴ.
    /// </summary>
    public void OnStartButtonClick()
    {
        if (AnalyticsManager.Instance != null)
        {
            AnalyticsManager.Instance.StartSession();
        }

        // "GameScene"̶ ̸  ҷɴϴ.
        // Ʈ ִ   ̸ Ȯ ƾ մϴ.
        SceneManager.LoadScene("GameScene");
    }

    /// <summary>
    /// '' ư Ŭ  ȣ˴ϴ.
    /// </summary>
    public void OnSettingsButtonClick()
    {
        // ŸƲ ⺻ UI 
        if (titleContentsGroup != null)
        {
            titleContentsGroup.SetActive(false);
        }

        //  UI մϴ.
        if (settingsContentsGroup != null)
        {
            settingsContentsGroup.SetActive(true);
        }
    }

    /// <summary>
    ///  ȭ 'ݱ' ư Ŭ  ȣ˴ϴ.
    /// </summary>
    public void OnSettingsCloseButtonClick()
    {
        //  UI 
        if (settingsContentsGroup != null)
        {
            settingsContentsGroup.SetActive(false);
        }

        // ŸƲ ⺻ UI ٽ մϴ.
        if (titleContentsGroup != null)
        {
            titleContentsGroup.SetActive(true);
        }
    }

    /// <summary>
    /// ' ' ư Ŭ  ȣ˴ϴ.
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
