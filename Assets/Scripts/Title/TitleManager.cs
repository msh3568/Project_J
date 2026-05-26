using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleManager : MonoBehaviour
{
    public TextMeshProUGUI pressEnterText;
    public string sceneToLoad = "GameSceneRespawn 1";
    public float delayBeforePressEnter = 3.0f;

    [Header("Mode Buttons")]
    [SerializeField] private bool showModeButtons = false;
    [SerializeField] private string storyModeScene = "GameSceneRespawn 1";
    [SerializeField] private string rankingModeScene = "GameSceneRespawn";
    [SerializeField] private Vector2 modeButtonCenter = new Vector2(0f, -285f);
    [SerializeField] private Vector2 modeButtonSize = new Vector2(420f, 78f);
    [SerializeField] private float modeButtonSpacing = 18f;
    [SerializeField] private Color modeButtonColor = new Color(0.015f, 0.035f, 0.028f, 0.94f);
    [SerializeField] private Color modeButtonHighlightedColor = new Color(0.03f, 0.16f, 0.08f, 0.98f);
    [SerializeField] private Color modeButtonPressedColor = new Color(0.01f, 0.09f, 0.045f, 1f);
    [SerializeField] private Color modeButtonBorderColor = new Color(0f, 1f, 0.2129209f, 0.8f);
    [SerializeField] private Color modeButtonDepthColor = new Color(0f, 0.42f, 0.11f, 0.9f);
    [SerializeField] private Color modeButtonInnerHighlightColor = new Color(0.55f, 1f, 0.68f, 0.22f);
    [SerializeField] private Color modeButtonTextColor = new Color(0f, 1f, 0.2129209f, 1f);
    [SerializeField] private float buttonSceneLoadDelay = 0.12f;

    private static Sprite modeButtonSprite;
    private Coroutine sceneLoadCoroutine;

    private void Start()
    {
        if (pressEnterText != null)
            pressEnterText.enabled = false;

        if (showModeButtons)
        {
            CreateModeButtons();
            return;
        }

        StartCoroutine(ShowPressEnterTextAfterDelay());
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            LoadSceneByName(sceneToLoad);
    }

    public void LoadStoryMode()
    {
        LoadSceneAfterButtonFeedback(storyModeScene);
    }

    public void LoadRankingMode()
    {
        LoadSceneAfterButtonFeedback(rankingModeScene);
    }

    private IEnumerator ShowPressEnterTextAfterDelay()
    {
        yield return new WaitForSeconds(delayBeforePressEnter);

        if (pressEnterText != null)
            pressEnterText.enabled = true;
    }

    private void CreateModeButtons()
    {
        Canvas canvas = pressEnterText != null
            ? pressEnterText.GetComponentInParent<Canvas>()
            : Object.FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning("TitleManager: Cannot create mode buttons because no Canvas was found.", this);
            return;
        }

        float offset = (modeButtonSize.y + modeButtonSpacing) * 0.5f;
        CreateModeButton(canvas.transform, "StoryModeButton", "StoryMode", modeButtonCenter + Vector2.up * offset, LoadStoryMode);
        CreateModeButton(canvas.transform, "RankingModeButton", "RankingMode", modeButtonCenter + Vector2.down * offset, LoadRankingMode);
    }

    private void CreateModeButton(Transform parent, string objectName, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            return;
        }

        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform));
        buttonObject.layer = parent.gameObject.layer;
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = modeButtonSize;

        Image image = buttonObject.AddComponent<Image>();
        image.sprite = GetModeButtonSprite();
        image.type = Image.Type.Sliced;
        image.color = modeButtonColor;

        Shadow shadow = buttonObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        shadow.effectDistance = new Vector2(0f, -7f);
        shadow.useGraphicAlpha = true;

        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = modeButtonBorderColor;
        outline.effectDistance = new Vector2(2.4f, -2.4f);
        outline.useGraphicAlpha = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        ColorBlock colors = button.colors;
        colors.normalColor = modeButtonColor;
        colors.highlightedColor = modeButtonHighlightedColor;
        colors.selectedColor = modeButtonHighlightedColor;
        colors.pressedColor = modeButtonPressedColor;
        colors.disabledColor = new Color(modeButtonColor.r, modeButtonColor.g, modeButtonColor.b, 0.35f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        AddButtonChrome(buttonObject.transform);
        AddPressFeedback(buttonObject, rectTransform, anchoredPosition);

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.layer = parent.gameObject.layer;
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI labelText = textObject.AddComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.fontSize = 40f;
        labelText.fontStyle = FontStyles.Bold;
        labelText.characterSpacing = 2f;
        labelText.color = modeButtonTextColor;
        labelText.raycastTarget = false;

        Shadow textShadow = textObject.AddComponent<Shadow>();
        textShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        textShadow.effectDistance = new Vector2(2f, -2f);
        textShadow.useGraphicAlpha = true;

        if (pressEnterText != null)
        {
            labelText.font = pressEnterText.font;
            labelText.fontSharedMaterial = pressEnterText.fontSharedMaterial;
        }
    }

    private void AddButtonChrome(Transform parent)
    {
        CreateButtonBar(parent, "TopHighlight", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -9f), new Vector2(-44f, 4f), modeButtonInnerHighlightColor);
        CreateButtonBar(parent, "BottomDepth", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(-34f, 11f), modeButtonDepthColor);
        CreateButtonBar(parent, "LeftMarker", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(7f, 36f), new Color(modeButtonBorderColor.r, modeButtonBorderColor.g, modeButtonBorderColor.b, 0.75f));
        CreateButtonBar(parent, "RightMarker", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(7f, 36f), new Color(modeButtonBorderColor.r, modeButtonBorderColor.g, modeButtonBorderColor.b, 0.75f));
    }

    private static void CreateButtonBar(Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        GameObject barObject = new GameObject(objectName, typeof(RectTransform));
        barObject.layer = parent.gameObject.layer;
        barObject.transform.SetParent(parent, false);

        RectTransform barRect = barObject.GetComponent<RectTransform>();
        barRect.anchorMin = anchorMin;
        barRect.anchorMax = anchorMax;
        barRect.pivot = pivot;
        barRect.anchoredPosition = anchoredPosition;
        barRect.sizeDelta = sizeDelta;

        Image barImage = barObject.AddComponent<Image>();
        barImage.color = color;
        barImage.raycastTarget = false;
    }

    private void AddPressFeedback(GameObject buttonObject, RectTransform rectTransform, Vector2 basePosition)
    {
        EventTrigger trigger = buttonObject.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerDown, _ => SetButtonPressed(rectTransform, basePosition, true));
        AddTrigger(trigger, EventTriggerType.PointerUp, _ => SetButtonPressed(rectTransform, basePosition, false));
        AddTrigger(trigger, EventTriggerType.PointerExit, _ => SetButtonPressed(rectTransform, basePosition, false));
        AddTrigger(trigger, EventTriggerType.Cancel, _ => SetButtonPressed(rectTransform, basePosition, false));
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

    private static void SetButtonPressed(RectTransform rectTransform, Vector2 basePosition, bool pressed)
    {
        if (rectTransform == null)
            return;

        rectTransform.localScale = pressed ? new Vector3(0.96f, 0.96f, 1f) : Vector3.one;
        rectTransform.anchoredPosition = pressed ? basePosition + new Vector2(0f, -4f) : basePosition;
    }

    private void LoadSceneAfterButtonFeedback(string targetScene)
    {
        if (sceneLoadCoroutine != null)
            StopCoroutine(sceneLoadCoroutine);

        sceneLoadCoroutine = StartCoroutine(LoadSceneAfterButtonFeedbackRoutine(targetScene));
    }

    private IEnumerator LoadSceneAfterButtonFeedbackRoutine(string targetScene)
    {
        yield return new WaitForSeconds(buttonSceneLoadDelay);
        LoadSceneByName(targetScene);
        sceneLoadCoroutine = null;
    }

    private static Sprite GetModeButtonSprite()
    {
        if (modeButtonSprite != null)
            return modeButtonSprite;

        const int size = 64;
        const float radius = 12f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "RuntimeModeButtonSprite",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32 clear = new Color32(255, 255, 255, 0);
        Color32 solid = new Color32(255, 255, 255, 255);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Min(x, size - 1 - x);
                float py = Mathf.Min(y, size - 1 - y);
                bool inside = px >= radius || py >= radius || new Vector2(px - radius, py - radius).sqrMagnitude <= radius * radius;
                texture.SetPixel(x, y, inside ? solid : clear);
            }
        }

        texture.Apply(false, true);
        modeButtonSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(16f, 16f, 16f, 16f));
        return modeButtonSprite;
    }

    private void LoadSceneByName(string targetScene)
    {
        if (string.IsNullOrWhiteSpace(targetScene))
        {
            Debug.LogWarning("TitleManager: Target scene name is empty.", this);
            return;
        }

        SceneManager.LoadScene(targetScene);
    }
}
