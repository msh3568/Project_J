using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CutsceneDroneRevealFade : MonoBehaviour
{
    [SerializeField] private bool playOnEnable = true;
    [SerializeField, Min(0f)] private float fadeDuration = 0.6f;
    [SerializeField] private Color revealColor = new Color(0.15f, 1f, 0.35f, 1f);
    [SerializeField, Range(0f, 1f)] private float startAlpha = 0f;
    [SerializeField, Range(0f, 1f)] private float endAlpha = 1f;
    [SerializeField, Min(0f)] private float greenHoldDuration = 0.08f;
    [SerializeField] private bool fadeToOriginalColor = true;
    [SerializeField, Min(0f)] private float returnToOriginalDuration = 0.35f;

    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private Coroutine revealCoroutine;
    private bool revealStartedThisEnable;

    private void Awake()
    {
        CacheRenderers();
    }

    private void OnEnable()
    {
        revealStartedThisEnable = false;
        if (playOnEnable)
            PlayRevealFade();
    }

    private void Start()
    {
        if (playOnEnable && !revealStartedThisEnable)
            PlayRevealFade();
    }

    private void OnDisable()
    {
        if (revealCoroutine != null)
        {
            StopCoroutine(revealCoroutine);
            revealCoroutine = null;
        }
    }

    public void PlayRevealFade()
    {
        CacheRenderers();

        if (spriteRenderers == null || spriteRenderers.Length == 0)
            return;

        revealStartedThisEnable = true;

        if (revealCoroutine != null)
            StopCoroutine(revealCoroutine);

        if (fadeDuration <= 0f)
        {
            ApplyFadeInColor(1f);
            return;
        }

        ApplyFadeInColor(0f);
        revealCoroutine = StartCoroutine(RevealFadeRoutine());
    }

    public bool ConfigureReturnToOriginalColor()
    {
        if (fadeToOriginalColor)
            return false;

        fadeToOriginalColor = true;
        return true;
    }

    private IEnumerator RevealFadeRoutine()
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            ApplyFadeInColor(Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        ApplyFadeInColor(1f);

        if (greenHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(greenHoldDuration);

        if (fadeToOriginalColor)
            yield return ReturnToOriginalColorRoutine();

        revealCoroutine = null;
    }

    private IEnumerator ReturnToOriginalColorRoutine()
    {
        if (returnToOriginalDuration <= 0f)
        {
            ApplyOriginalColors();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < returnToOriginalDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            ApplyReturnToOriginalColor(Mathf.Clamp01(elapsed / returnToOriginalDuration));
            yield return null;
        }

        ApplyOriginalColors();
    }

    private void ApplyFadeInColor(float t)
    {
        if (spriteRenderers == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
                continue;

            Color endColor = revealColor;
            endColor.a = endAlpha;

            Color startColor = revealColor;
            startColor.a = startAlpha;

            spriteRenderer.color = Color.Lerp(startColor, endColor, t);
        }
    }

    private void ApplyReturnToOriginalColor(float t)
    {
        if (spriteRenderers == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
                continue;

            Color startColor = revealColor;
            startColor.a = endAlpha;
            Color endColor = originalColors != null && i < originalColors.Length
                ? originalColors[i]
                : Color.white;

            spriteRenderer.color = Color.Lerp(startColor, endColor, t);
        }
    }

    private void ApplyOriginalColors()
    {
        if (spriteRenderers == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null && originalColors != null && i < originalColors.Length)
                spriteRenderers[i].color = originalColors[i];
        }
    }

    private void CacheRenderers()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (spriteRenderers != null && originalColors != null && spriteRenderers.Length == renderers.Length)
            return;

        spriteRenderers = renderers;
        originalColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            originalColors[i] = spriteRenderers[i] != null ? spriteRenderers[i].color : Color.white;
        }
    }
}
