using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    private static readonly Vector2Int[] ResolutionOptions =
    {
        new Vector2Int(1920, 1080),
        new Vector2Int(1600, 900),
        new Vector2Int(1280, 720)
    };
    [Header("UI Groups")]
    public GameObject pauseGroup;            // ?쇱떆?뺤? UI ?꾩껜瑜?媛먯떥??遺紐?
    public GameObject pauseMenuContent;      // 湲곕낯 硫붾돱 李?(踰꾪듉??
    public GameObject settingsContentsGroup; // ?ㅼ젙 李?
    [SerializeField] private Button firstSelectedButton;
    [SerializeField] private int pauseSortingOrder = 32767;

    [Header("Volume Settings")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    public static bool IsGamePaused { get; private set; } = false;
    private Canvas pauseCanvas;
    private GraphicRaycaster pauseGraphicRaycaster;
    private CanvasGroup pauseCanvasGroup;
    private StandaloneInputModule pauseInputModule;
    private readonly Dictionary<BaseInputModule, bool> previousInputModuleStates = new Dictionary<BaseInputModule, bool>();
    private readonly List<RaycastResult> pauseRaycastResults = new List<RaycastResult>();
    private readonly Vector3[] buttonWorldCorners = new Vector3[4];
    private PointerEventData pausePointerEventData;
    private Button hoveredFallbackButton;
    private Button pressedFallbackButton;
    private bool inputModulesOverridden;

    void Start()
    {
        CachePauseUiComponents();

        // Ensure the game is not paused and the pause menu is hidden at the start
        Time.timeScale = 1f;
        if (pauseGroup != null)
            pauseGroup.SetActive(false);
        IsGamePaused = false;

        // AudioManager?먯꽌 ?꾩옱 蹂쇰ⅷ 媛믪쓣 媛?몄? ?щ씪?대뜑???ㅼ젙
        if (AudioManager.Instance != null)
        {
            // SetValueWithoutNotify瑜??ъ슜?섏뿬 ?대깽?멸? 諛쒖깮?섏? ?딅룄濡?媛믪쓣 ?ㅼ젙
            if (bgmSlider != null)
                bgmSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("BGMVolume", 0.75f));

            if (sfxSlider != null)
                sfxSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("SFXVolume", 0.75f));
        }

        // ?щ씪?대뜑 ?대깽?몄뿉 由ъ뒪??異붽?
        if (bgmSlider != null)
            bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);

        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
    }

    void Update()
    {
        // ESC ???낅젰 媛먯?
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!IsGamePaused && CutsceneDialoguePlayer.IsAnyTimelineRunning)
                return;

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

        if (IsGamePaused)
            DrivePausePointerFallback();
    }

    private void PauseGame()
    {
        IsGamePaused = true;
        Time.timeScale = 0f; // ?쒓컙 ?먮쫫??硫덉땄

        if (pauseGroup != null)
            pauseGroup.SetActive(true);

        if (pauseMenuContent != null)
            pauseMenuContent.SetActive(true);

        if (settingsContentsGroup != null)
            settingsContentsGroup.SetActive(false);

        EnsurePauseUiCanReceiveInput();
    }

    // '怨꾩냽?섍린' 踰꾪듉???곌껐???⑥닔
    public void ResumeGame()
    {
        IsGamePaused = false;
        Time.timeScale = 1f; // ?쒓컙 ?먮쫫???섎룎由?
        if (pauseGroup != null)
            pauseGroup.SetActive(false);

        ClearPausePointerFallbackState();
        RestoreInputModules();
    }

    // '?ㅼ젙' 踰꾪듉???곌껐???⑥닔
    public void OpenSettings()
    {
        if (pauseMenuContent != null)
            pauseMenuContent.SetActive(false);

        if (settingsContentsGroup != null)
            settingsContentsGroup.SetActive(true);

        EnsurePauseUiCanReceiveInput();
    }

    // ?ㅼ젙 李쎌쓽 '?リ린' 踰꾪듉???곌껐???⑥닔
    public void CloseSettings()
    {
        if (settingsContentsGroup != null)
            settingsContentsGroup.SetActive(false);

        if (pauseMenuContent != null)
            pauseMenuContent.SetActive(true);

        EnsurePauseUiCanReceiveInput();
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

    public void ApplyResolution(int width, int height)
    {
        Debug.Log($"PauseManager.ApplyResolution: {width}x{height}");
        DisplaySettings.ApplyResolution(width, height);
    }

    public void ApplyResolutionByIndex(int index)
    {
        if (index < 0 || index >= ResolutionOptions.Length)
        {
            Debug.LogWarning($"PauseManager.ApplyResolutionByIndex: invalid index {index}");
            return;
        }

        var resolution = ResolutionOptions[index];
        Debug.Log($"PauseManager.ApplyResolutionByIndex: index={index}, {resolution.x}x{resolution.y}");
        DisplaySettings.ApplyResolution(resolution.x, resolution.y);
    }

    public void ApplyWindowMode(int modeIndex)
    {
        if (modeIndex < 0 || modeIndex > 2)
        {
            Debug.LogWarning($"PauseManager.ApplyWindowMode: invalid index {modeIndex}");
            return;
        }

        Debug.Log($"PauseManager.ApplyWindowMode: index={modeIndex}");
        DisplaySettings.ApplyWindowMode((DisplaySettings.WindowMode)modeIndex);
    }

    private void CachePauseUiComponents()
    {
        if (pauseGroup == null)
            return;

        pauseCanvas = pauseGroup.GetComponent<Canvas>();
        pauseGraphicRaycaster = pauseGroup.GetComponent<GraphicRaycaster>();
        pauseCanvasGroup = pauseGroup.GetComponent<CanvasGroup>();

        if (firstSelectedButton == null && pauseMenuContent != null)
            firstSelectedButton = pauseMenuContent.GetComponentInChildren<Button>(true);
    }

    private void EnsurePauseUiCanReceiveInput()
    {
        if (pauseGroup == null)
            return;

        CachePauseUiComponents();
        pauseGroup.transform.SetAsLastSibling();

        if (pauseCanvas == null)
            pauseCanvas = pauseGroup.AddComponent<Canvas>();

        pauseCanvas.overrideSorting = true;
        pauseCanvas.sortingOrder = pauseSortingOrder;

        if (pauseGraphicRaycaster == null)
            pauseGraphicRaycaster = pauseGroup.AddComponent<GraphicRaycaster>();

        pauseGraphicRaycaster.enabled = true;

        if (pauseCanvasGroup == null)
            pauseCanvasGroup = pauseGroup.AddComponent<CanvasGroup>();

        pauseCanvasGroup.alpha = 1f;
        pauseCanvasGroup.interactable = true;
        pauseCanvasGroup.blocksRaycasts = true;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (EventSystem.current == null)
            CreateFallbackEventSystem();

        EventSystem.current.enabled = true;
        UseLegacyMouseInputWhilePaused(EventSystem.current);
        EventSystem.current.SetSelectedGameObject(null);

        if (firstSelectedButton != null && firstSelectedButton.gameObject.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(firstSelectedButton.gameObject);
    }

    private void UseLegacyMouseInputWhilePaused(EventSystem eventSystem)
    {
        if (eventSystem == null)
            return;

        if (pauseInputModule == null)
            pauseInputModule = eventSystem.GetComponent<StandaloneInputModule>();

        if (pauseInputModule == null)
            pauseInputModule = eventSystem.gameObject.AddComponent<StandaloneInputModule>();

        if (!inputModulesOverridden)
        {
            previousInputModuleStates.Clear();
            BaseInputModule[] inputModules = eventSystem.GetComponents<BaseInputModule>();
            for (int i = 0; i < inputModules.Length; i++)
            {
                if (inputModules[i] != null)
                    previousInputModuleStates[inputModules[i]] = inputModules[i].enabled;
            }

            inputModulesOverridden = true;
        }

        BaseInputModule[] modules = eventSystem.GetComponents<BaseInputModule>();
        for (int i = 0; i < modules.Length; i++)
        {
            if (modules[i] != null)
                modules[i].enabled = modules[i] == pauseInputModule;
        }
    }

    private void DrivePausePointerFallback()
    {
        if (EventSystem.current != null && pausePointerEventData == null)
            pausePointerEventData = new PointerEventData(EventSystem.current);

        if (pausePointerEventData != null)
        {
            pausePointerEventData.Reset();
            pausePointerEventData.position = Input.mousePosition;
            pausePointerEventData.button = PointerEventData.InputButton.Left;
        }

        Button currentButton = FindButtonUnderMouse();
        if (currentButton != hoveredFallbackButton)
        {
            if (hoveredFallbackButton != null)
            {
                ExecutePausePointerEvent(hoveredFallbackButton.gameObject, ExecuteEvents.pointerExitHandler);
                SetButtonFallbackColor(hoveredFallbackButton, false, false);
            }

            hoveredFallbackButton = currentButton;

            if (hoveredFallbackButton != null)
            {
                ExecutePausePointerEvent(hoveredFallbackButton.gameObject, ExecuteEvents.pointerEnterHandler);
                SetButtonFallbackColor(hoveredFallbackButton, false, true);
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            pressedFallbackButton = currentButton;

            if (pressedFallbackButton != null)
            {
                ExecutePausePointerEvent(pressedFallbackButton.gameObject, ExecuteEvents.pointerDownHandler);
                SetButtonFallbackColor(pressedFallbackButton, true, true);
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            Button releasedButton = pressedFallbackButton;

            if (releasedButton != null)
            {
                ExecutePausePointerEvent(releasedButton.gameObject, ExecuteEvents.pointerUpHandler);
                SetButtonFallbackColor(releasedButton, false, releasedButton == hoveredFallbackButton);
            }

            if (releasedButton != null && releasedButton == currentButton)
                releasedButton.onClick.Invoke();

            pressedFallbackButton = null;
        }
    }

    private void ExecutePausePointerEvent<T>(GameObject target, ExecuteEvents.EventFunction<T> eventFunction)
        where T : IEventSystemHandler
    {
        if (target == null || pausePointerEventData == null)
            return;

        ExecuteEvents.Execute(target, pausePointerEventData, eventFunction);
    }

    private static void SetButtonFallbackColor(Button button, bool pressed, bool hovered)
    {
        if (button == null || button.targetGraphic == null)
            return;

        ColorBlock colors = button.colors;
        Color color = colors.normalColor;
        if (pressed)
            color = colors.pressedColor;
        else if (hovered)
            color = colors.highlightedColor;

        button.targetGraphic.color = color * colors.colorMultiplier;
    }

    private Button FindButtonUnderMouse()
    {
        Button directButton = FindButtonUnderMouseByRect();
        if (directButton != null)
            return directButton;

        pauseRaycastResults.Clear();
        if (pauseGraphicRaycaster != null && pausePointerEventData != null)
            pauseGraphicRaycaster.Raycast(pausePointerEventData, pauseRaycastResults);
        return FindFirstInteractableButton(pauseRaycastResults);
    }

    private static void CreateFallbackEventSystem()
    {
        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private Button FindButtonUnderMouseByRect()
    {
        Transform searchRoot = null;

        if (pauseMenuContent != null && pauseMenuContent.activeInHierarchy)
            searchRoot = pauseMenuContent.transform;
        else if (settingsContentsGroup != null && settingsContentsGroup.activeInHierarchy)
            searchRoot = settingsContentsGroup.transform;
        else if (pauseGroup != null)
            searchRoot = pauseGroup.transform;

        if (searchRoot == null)
            return null;

        Button[] buttons = searchRoot.GetComponentsInChildren<Button>(false);
        for (int i = buttons.Length - 1; i >= 0; i--)
        {
            Button button = buttons[i];
            if (button == null || !button.gameObject.activeInHierarchy || !button.IsInteractable())
                continue;

            RectTransform rectTransform = button.transform as RectTransform;
            Camera eventCamera = GetEventCamera(rectTransform);
            if (rectTransform != null &&
                (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Input.mousePosition, eventCamera) ||
                 ContainsScreenPointByWorldCorners(rectTransform, Input.mousePosition, eventCamera)))
            {
                return button;
            }
        }

        return null;
    }

    private bool ContainsScreenPointByWorldCorners(RectTransform rectTransform, Vector2 screenPosition, Camera eventCamera)
    {
        if (rectTransform == null)
            return false;

        rectTransform.GetWorldCorners(buttonWorldCorners);

        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        for (int i = 0; i < buttonWorldCorners.Length; i++)
        {
            Vector2 corner = RectTransformUtility.WorldToScreenPoint(eventCamera, buttonWorldCorners[i]);
            minX = Mathf.Min(minX, corner.x);
            minY = Mathf.Min(minY, corner.y);
            maxX = Mathf.Max(maxX, corner.x);
            maxY = Mathf.Max(maxY, corner.y);
        }

        return screenPosition.x >= minX &&
               screenPosition.x <= maxX &&
               screenPosition.y >= minY &&
               screenPosition.y <= maxY;
    }

    private static Camera GetEventCamera(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return null;

        Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        if (canvas.worldCamera != null)
            return canvas.worldCamera;

        return Camera.main;
    }

    private static Button FindFirstInteractableButton(List<RaycastResult> results)
    {
        for (int i = 0; i < results.Count; i++)
        {
            Button button = results[i].gameObject.GetComponentInParent<Button>();
            if (button != null && button.gameObject.activeInHierarchy && button.IsInteractable())
                return button;
        }

        return null;
    }

    private void ClearPausePointerFallbackState()
    {
        if (hoveredFallbackButton != null)
        {
            if (EventSystem.current != null && pausePointerEventData == null)
                pausePointerEventData = new PointerEventData(EventSystem.current);

            ExecutePausePointerEvent(hoveredFallbackButton.gameObject, ExecuteEvents.pointerExitHandler);
            SetButtonFallbackColor(hoveredFallbackButton, false, false);
        }

        hoveredFallbackButton = null;
        pressedFallbackButton = null;
    }

    private void RestoreInputModules()
    {
        if (!inputModulesOverridden)
            return;

        foreach (var moduleState in previousInputModuleStates)
        {
            if (moduleState.Key != null)
                moduleState.Key.enabled = moduleState.Value;
        }

        if (pauseInputModule != null && !previousInputModuleStates.ContainsKey(pauseInputModule))
            pauseInputModule.enabled = false;

        previousInputModuleStates.Clear();
        inputModulesOverridden = false;
    }
}
