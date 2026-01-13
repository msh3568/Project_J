using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    [Header("UI Groups")]
    public GameObject pauseGroup;            // ?쇱떆?뺤? UI ?꾩껜瑜?媛먯떥??遺紐?
    public GameObject pauseMenuContent;      // 湲곕낯 硫붾돱 李?(踰꾪듉??
    public GameObject settingsContentsGroup; // ?ㅼ젙 李?

    [Header("Volume Settings")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    public static bool IsGamePaused { get; private set; } = false;

    void Start()
    {
        // Ensure the game is not paused and the pause menu is hidden at the start
        Time.timeScale = 1f;
        pauseGroup.SetActive(false);
        IsGamePaused = false;

        // AudioManager?먯꽌 ?꾩옱 蹂쇰ⅷ 媛믪쓣 媛?몄? ?щ씪?대뜑???ㅼ젙
        if (AudioManager.Instance != null)
        {
            // SetValueWithoutNotify瑜??ъ슜?섏뿬 ?대깽?멸? 諛쒖깮?섏? ?딅룄濡?媛믪쓣 ?ㅼ젙
            bgmSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("BGMVolume", 0.75f));
            sfxSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("SFXVolume", 0.75f));
        }

        // ?щ씪?대뜑 ?대깽?몄뿉 由ъ뒪??異붽?
        bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
        sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
    }

    void Update()
    {
        // ESC ???낅젰 媛먯?
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // ?ㅼ젙 李쎌씠 ?쒖꽦?붾릺???덉쑝硫??ㅼ젙 李쎌쓣 ?レ쓬
            if (settingsContentsGroup != null && settingsContentsGroup.activeSelf)
            {
                CloseSettings();
            }
            // ?쇱떆?뺤? ?곹깭媛 ?꾨땲硫??쇱떆?뺤?
            else if (!IsGamePaused)
            {
                PauseGame();
            }
            // ?쇱떆?뺤? ?곹깭硫?寃뚯엫 ?ш컻
            else
            {
                ResumeGame();
            }
        }
    }

    private void PauseGame()
    {
        IsGamePaused = true;
        Time.timeScale = 0f; // ?쒓컙 ?먮쫫??硫덉땄

        pauseGroup.SetActive(true);
        pauseMenuContent.SetActive(true);
        settingsContentsGroup.SetActive(false);
    }

    // '怨꾩냽?섍린' 踰꾪듉???곌껐???⑥닔
    public void ResumeGame()
    {
        IsGamePaused = false;
        Time.timeScale = 1f; // ?쒓컙 ?먮쫫???섎룎由?
        pauseGroup.SetActive(false);
    }

    // '?ㅼ젙' 踰꾪듉???곌껐???⑥닔
    public void OpenSettings()
    {
        pauseMenuContent.SetActive(false);
        settingsContentsGroup.SetActive(true);
    }

    // ?ㅼ젙 李쎌쓽 '?リ린' 踰꾪듉???곌껐???⑥닔
    public void CloseSettings()
    {
        settingsContentsGroup.SetActive(false);
        pauseMenuContent.SetActive(true);
    }

    // '寃뚯엫 醫낅즺' 踰꾪듉???곌껐???⑥닔
    public void ExitGame()
    {
        // ?좊땲???먮뵒?곗뿉?쒕뒗 ?뚮젅??紐⑤뱶瑜?以묒??섍퀬,
        // 鍮뚮뱶??寃뚯엫?먯꽌???좏뵆由ъ??댁뀡??醫낅즺?⑸땲??
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // BGM ?щ씪?대뜑 媛믪씠 蹂寃쎈맆 ???몄텧???⑥닔
    public void OnBGMVolumeChanged(float volume)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetBGMVolume(volume);
        }
    }

    // SFX ?щ씪?대뜑 媛믪씠 蹂寃쎈맆 ???몄텧???⑥닔
    public void OnSFXVolumeChanged(float volume)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(volume);
        }
    }

    // '??댄?濡??뚯븘媛湲? 踰꾪듉???곌껐???⑥닔
    public void ReturnToTitle()
    {
        Time.timeScale = 1f; // ?쒓컙 ?먮쫫???섎룎由?
        TimeManager.elapsedTime = 0f; // ??대㉧ 珥덇린??
        SceneManager.LoadScene("FIXER Title"); // "FIXER Title" ?ъ쓣 遺덈윭??
    }
}
