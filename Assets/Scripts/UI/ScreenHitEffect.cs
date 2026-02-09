using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ScreenHitEffect : MonoBehaviour
{
    [Header("Overlay")]
    [SerializeField] private Image redOverlay;
    [SerializeField, Range(0f, 1f)] private float maxOverlayAlpha = 0.45f;
    [SerializeField] private float duration = 0.8f;
    [SerializeField] private AnimationCurve overlayFade = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] private bool onlyApplyOverlayDuringAwakening = true;
    [SerializeField] private bool allowOverlayOutsideAwakening = true;
    [SerializeField] private bool suppressWhenGrappling = false;

    [Header("Twist (Optional)")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float twistAngle = 0.6f;
    [SerializeField] private float twistFrequency = 18f;
    [SerializeField] private float positionJitter = 0.03f;

    [Header("URP Glitch (Optional)")]

    [SerializeField] private AwakeningManager awakeningManager;
    [SerializeField] private Player player;
    [SerializeField] private bool onlyApplyPostFxDuringAwakening = true;
    [SerializeField] private Volume volume;
    [SerializeField] private float chromaticIntensity = 0.6f;
    [SerializeField] private float lensDistortionIntensity = -0.12f;
    [SerializeField] private bool addToExistingPostFx = true;
    [SerializeField] private AnimationCurve postFxCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] private bool driveVolumeWeight = true;
    [SerializeField] private bool tracePostFxLogs = true;

    [Header("Full Screen Glitch (Renderer Feature)")]
    [SerializeField] private bool driveFullscreenGlitch = true;
    [SerializeField] private float glitchStrength = 1f;
    [SerializeField] private float glitchHorizontal = 0.05f;
    [SerializeField] private float glitchBlockSize = 120f;
    [SerializeField] private float glitchLineJitter = 18f;
    [SerializeField] private float glitchColorSplit = 0.003f;
    [SerializeField] private bool disableGlobalShaderGlitch = true;

    private Coroutine effectCoroutine;
    private Vector3 cachedCameraLocalPos;
    private Quaternion cachedCameraLocalRot;
    private ChromaticAberration chromatic;
    private LensDistortion lensDistortion;
    private float cachedChromatic;
    private float cachedLensDistortion;
    private bool cachedChromaticActive;
    private bool cachedLensDistortionActive;
    private float cachedVolumeWeight;
    private bool droveVolumeWeightThisPlay;
    private static readonly int GlitchStrengthId = Shader.PropertyToID("_GlitchStrength");
    private static readonly int GlitchHorizontalId = Shader.PropertyToID("_GlitchHorizontal");
    private static readonly int GlitchBlockSizeId = Shader.PropertyToID("_GlitchBlockSize");
    private static readonly int GlitchLineJitterId = Shader.PropertyToID("_GlitchLineJitter");
    private static readonly int GlitchColorSplitId = Shader.PropertyToID("_GlitchColorSplit");

    public void StopAndClearImmediate()
    {
        if (effectCoroutine != null)
        {
            StopCoroutine(effectCoroutine);
            effectCoroutine = null;
        }

        ForceClearOverlay();
        RestorePostFx();
        RestoreFullscreenGlitch();
        RestoreCameraTransform();
    }

    public void Play()
    {
        droveVolumeWeightThisPlay = false;

        if (suppressWhenGrappling && player != null && player.IsGrappling)
        {
            StopAndClearImmediate();
            return;
        }

        if (effectCoroutine != null)
            StopCoroutine(effectCoroutine);

        CachePostFxState();
        effectCoroutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        CacheCameraTransform();
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            ApplyOverlay(t);
            ApplyTwist(t);
            ApplyPostFx(t);
            ApplyFullscreenGlitch(t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyOverlay(1f);
        RestorePostFx();
        RestoreFullscreenGlitch();
        RestoreCameraTransform();
        effectCoroutine = null;
    }

    private void ApplyOverlay(float t)
    {
        if (onlyApplyOverlayDuringAwakening && !allowOverlayOutsideAwakening
            && (awakeningManager == null || !awakeningManager.IsAwakening))
        {
            ForceClearOverlay();
            return;
        }

        if (suppressWhenGrappling && player != null && player.IsGrappling)
        {
            ForceClearOverlay();
            return;
        }

        if (redOverlay == null)
            return;

        float alpha = maxOverlayAlpha * Mathf.Clamp01(overlayFade.Evaluate(t));
        Color c = redOverlay.color;
        c.a = alpha;
        redOverlay.color = c;
    }

    private void ForceClearOverlay()
    {
        if (redOverlay == null)
            return;

        Color c = redOverlay.color;
        c.a = 0f;
        redOverlay.color = c;
    }

    private void ApplyTwist(float t)
    {
        if (cameraTransform == null)
            return;

        float falloff = 1f - t;
        float angle = Mathf.Sin(Time.time * twistFrequency) * twistAngle * falloff;
        float jitter = Mathf.PerlinNoise(Time.time * twistFrequency, 0.15f) * 2f - 1f;

        cameraTransform.localRotation = cachedCameraLocalRot * Quaternion.Euler(0f, 0f, angle);
        cameraTransform.localPosition = cachedCameraLocalPos + new Vector3(jitter * positionJitter * falloff, 0f, 0f);
    }

    private void ApplyPostFx(float t)
    {
        if (onlyApplyPostFxDuringAwakening && (awakeningManager == null || !awakeningManager.IsAwakening))
            return;

        if (suppressWhenGrappling && player != null && player.IsGrappling)
        {
            RestorePostFx();
            return;
        }

        if (volume == null || chromatic == null || lensDistortion == null)
            return;

        float weight = Mathf.Clamp01(postFxCurve.Evaluate(t));
        if (driveVolumeWeight)
        {
            droveVolumeWeightThisPlay = true;
            float previousWeight = volume.weight;
            if (!Mathf.Approximately(previousWeight, weight))
                LogPostFxWeightTrace("ApplyPostFx", previousWeight, weight);
            volume.weight = weight;
            return;
        }
        float chromaValue = chromaticIntensity * weight;
        float lensValue = lensDistortionIntensity * weight;

        if (addToExistingPostFx)
        {
            chromaValue += cachedChromatic;
            lensValue += cachedLensDistortion;
        }

        chromatic.intensity.Override(chromaValue);
        lensDistortion.intensity.Override(lensValue);
        chromatic.active = cachedChromaticActive || weight > 0.01f;
        lensDistortion.active = cachedLensDistortionActive || weight > 0.01f;
    }

    private void CacheCameraTransform()
    {
        if (cameraTransform == null)
            return;

        cachedCameraLocalPos = cameraTransform.localPosition;
        cachedCameraLocalRot = cameraTransform.localRotation;
    }

    private void RestoreCameraTransform()
    {
        if (cameraTransform == null)
            return;

        cameraTransform.localPosition = cachedCameraLocalPos;
        cameraTransform.localRotation = cachedCameraLocalRot;
    }

    private void RestorePostFx()
    {
        if (chromatic != null)
        {
            chromatic.intensity.Override(cachedChromatic);
            chromatic.active = cachedChromaticActive;
        }
        if (lensDistortion != null)
        {
            lensDistortion.intensity.Override(cachedLensDistortion);
            lensDistortion.active = cachedLensDistortionActive;
        }
        if (volume != null && droveVolumeWeightThisPlay)
        {
            float previousWeight = volume.weight;
            if (!Mathf.Approximately(previousWeight, cachedVolumeWeight))
                LogPostFxWeightTrace("RestorePostFx", previousWeight, cachedVolumeWeight);
            volume.weight = cachedVolumeWeight;
        }

        droveVolumeWeightThisPlay = false;
    }

    private void ApplyFullscreenGlitch(float t)
    {
        if (disableGlobalShaderGlitch)
            return;
        if (onlyApplyPostFxDuringAwakening && (awakeningManager == null || !awakeningManager.IsAwakening))
            return;
        if (suppressWhenGrappling && player != null && player.IsGrappling)
        {
            RestoreFullscreenGlitch();
            return;
        }

        if (!driveFullscreenGlitch)
            return;

        float weight = Mathf.Clamp01(postFxCurve.Evaluate(t)) * glitchStrength;
        Shader.SetGlobalFloat(GlitchStrengthId, weight);
        Shader.SetGlobalFloat(GlitchHorizontalId, glitchHorizontal);
        Shader.SetGlobalFloat(GlitchBlockSizeId, glitchBlockSize);
        Shader.SetGlobalFloat(GlitchLineJitterId, glitchLineJitter);
        Shader.SetGlobalFloat(GlitchColorSplitId, glitchColorSplit);
    }

    private void RestoreFullscreenGlitch()
    {
        if (disableGlobalShaderGlitch)
            return;
        Shader.SetGlobalFloat(GlitchStrengthId, 0f);
    }

    private void Awake()
    {
        if (awakeningManager == null)
            awakeningManager = Object.FindFirstObjectByType<AwakeningManager>(FindObjectsInactive.Include);

        if (player == null)
            player = Object.FindFirstObjectByType<Player>(FindObjectsInactive.Include);

        if (volume == null)
            volume = Object.FindFirstObjectByType<Volume>(FindObjectsInactive.Include);

        CachePostFxState();
    }

    private void CachePostFxState()
    {
        if (volume == null || volume.profile == null)
            return;

        cachedVolumeWeight = volume.weight;
        volume.profile.TryGet(out chromatic);
        volume.profile.TryGet(out lensDistortion);

        if (chromatic != null)
        {
            cachedChromatic = chromatic.intensity.value;
            cachedChromaticActive = chromatic.active;
        }
        if (lensDistortion != null)
        {
            cachedLensDistortion = lensDistortion.intensity.value;
            cachedLensDistortionActive = lensDistortion.active;
        }
    }

    private void OnDisable()
    {
        RestorePostFx();
        RestoreFullscreenGlitch();
        RestoreCameraTransform();
    }

    private void LogPostFxWeightTrace(string source, float before, float after)
    {
        if (!tracePostFxLogs)
            return;

        string volumeName = volume != null && volume.gameObject != null ? volume.gameObject.name : "(null)";
        bool isAwakening = awakeningManager != null && awakeningManager.IsAwakening;
        bool isGrappling = player != null && player.IsGrappling;
        float saturation = float.NaN;
        if (volume != null && volume.profile != null && volume.profile.TryGet(out ColorAdjustments colorAdjustments))
            saturation = colorAdjustments.saturation.value;

        Debug.Log(
            $"[SAT_TRACE][ScreenHitEffect.{source}] volume='{volumeName}' weight {before:F2}->{after:F2} sat={saturation:F2} awakening={isAwakening} grappling={isGrappling}",
            this);
    }
}
