using System.Collections;
using TMPro;
using UnityEngine;

public class LogPanel : MonoBehaviour
{
    public static LogPanel Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text logText;

    [Header("Settings")]
    [SerializeField] private float defaultShowTime = 2f;
    [SerializeField] private float fadeOutTime = 0.6f;

    private Coroutine _routine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Show(string message, float showTime = -1f)
    {
        SetText(message, Color.white);
        StartShowRoutine(showTime);
    }

    public void ShowInfo(string message, float showTime = -1f)
    {
        SetText(message, Color.white);
        StartShowRoutine(showTime);
    }

    public void ShowError(string message, float showTime = -1f)
    {
        SetText(message, Color.red);
        StartShowRoutine(showTime);
    }

    public void HideImmediate()
    {
        StopRoutine();
        SetAlpha(0f);
        SetInteractable(false);
    }

    private void StartShowRoutine(float showTime)
    {
        if (showTime <= 0f)
            showTime = defaultShowTime;

        StopRoutine();
        _routine = StartCoroutine(ShowAndFade(showTime));
    }

    private IEnumerator ShowAndFade(float showTime)
    {
        if (canvasGroup == null)
            yield break;

        canvasGroup.gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        yield return new WaitForSeconds(showTime);

        float t = 0f;
        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;

        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(startAlpha, 0f, t / fadeOutTime);
            SetAlpha(a);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.gameObject.SetActive(false);
        _routine = null;
    }

    private void StopRoutine()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    private void SetText(string message, Color color)
    {
        if (logText == null) return;
        logText.text = message;
        logText.color = color;
    }

    private void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = alpha;
    }

    private void SetInteractable(bool value)
    {
        if (canvasGroup == null) return;
        canvasGroup.blocksRaycasts = value;
        canvasGroup.interactable = value;
    }
}
