using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using MoreMountains.Feedbacks;

public class AwakeningScreenFX : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AwakeningManager awakeningManager;
    [SerializeField] private Volume volume;
    [SerializeField] private Volume awakeningVolume;
    [SerializeField] private VolumeProfile awakeningProfileTemplate;
    [SerializeField] private Player player;
    [SerializeField] private Player_Health playerHealth;

    [Header("Volume Split")]
    [SerializeField] private bool autoCreateAwakeningVolume = true;
    [SerializeField] private string autoCreatedVolumeName = "Awakening FX Volume";
    [SerializeField, Min(0.1f)] private float autoCreatedPriorityOffset = 10f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool disableGlobalShaderGlitch = true;
    [SerializeField] private bool traceSaturationLogs = true;

    [Header("Awakening Exit Pulse")]
    [SerializeField] private bool playHitPulseOnAwakeningExit = true;
    [SerializeField, Min(0f)] private float awakeningExitHitPulseDelay = 0f;
    [SerializeField, Min(0.1f)] private float awakeningExitHitPulseIntensity = 1.25f;
    [SerializeField] private MMF_Player awakeningExitFeedback;
    [SerializeField] private bool awakeningExitPulseTriggersHitSlowMo = true;
    [SerializeField] private bool awakeningExitPulsePlaysShieldVfxAndSound = true;

    [Header("TV Power")]
    [SerializeField, Min(0.01f)] private float powerTransitionDuration = 0.2f;

    [Header("Glitch (Renderer Feature)")]
    [SerializeField] private float glitchStrength = 0.7f;
    [SerializeField] private float glitchHorizontal = 0.05f;
    [SerializeField] private float glitchBlockSize = 120f;
    [SerializeField] private float glitchLineJitter = 18f;
    [SerializeField] private float rgbSplit = 0.003f;
    [SerializeField, Min(1f)] private float grapplingFxMultiplier = 1.35f;

    [Header("Tint")]
    [SerializeField] private Color neonTint = new Color(0.2f, 0.85f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float tintAmount = 0.45f;
    [SerializeField, Range(0f, 1f)] private float chromaticIntensity = 0.6f;
    [SerializeField, Range(-1f, 1f)] private float lensDistortionIntensity = -0.35f;
    [SerializeField, Range(0f, 1f)] private float paniniDistance = 0.22f;
    [SerializeField, Range(0f, 1f)] private float filmGrainIntensity = 0.45f;
    [SerializeField] private bool useFallbackTintWhenNeutral = false;
    [SerializeField] private Color fallbackVisibleTint = new Color(0.2f, 0.85f, 1f, 1f);
    [SerializeField, Range(-100f, 100f)] private float contrastBoost = 28f;
    [SerializeField, Range(-100f, 100f)] private float saturationBoost = 24f;
    [SerializeField, Range(-5f, 5f)] private float postExposureBoost = 0f;

    private ColorAdjustments colorAdjustments;
    private ChromaticAberration chromatic;
    private LensDistortion lensDistortion;
    private PaniniProjection paniniProjection;
    private FilmGrain filmGrain;
    private bool cachedColorActive;
    private bool cachedChromaticActive;
    private bool cachedLensDistortionActive;
    private bool cachedPaniniActive;
    private bool cachedFilmGrainActive;
    private Color cachedColorFilter;
    private float cachedContrast;
    private float cachedSaturation;
    private float cachedPostExposure;
    private float cachedChromatic;
    private float cachedLensDistortion;
    private float cachedPaniniDistance;
    private float cachedFilmGrainIntensity;
    private float cachedVolumeWeight;
    private bool ownsAwakeningVolume;
    private bool ownsAwakeningProfile;

    private Coroutine powerCoroutine;
    private Coroutine awakeningExitPulseCoroutine;
    private bool lastAwakening;

    private static readonly int TvPowerId = Shader.PropertyToID("_TVPower");
    private static readonly int GlitchStrengthId = Shader.PropertyToID("_GlitchStrength");
    private static readonly int GlitchHorizontalId = Shader.PropertyToID("_GlitchHorizontal");
    private static readonly int GlitchBlockSizeId = Shader.PropertyToID("_GlitchBlockSize");
    private static readonly int GlitchLineJitterId = Shader.PropertyToID("_GlitchLineJitter");
    private static readonly int GlitchColorSplitId = Shader.PropertyToID("_GlitchColorSplit");

    private void Awake()
    {
        ResolveReferences();
        EnsureAwakeningVolumeReady();
        CachePostFxState();

        if (disableGlobalShaderGlitch)
            ResetGlobalGlitchDefaults();
        else
            Shader.SetGlobalFloat(TvPowerId, 1f);

        RestorePostFx();
        RestoreGlitch();

        if (enableDebugLogs)
            Debug.Log("[AwakeningScreenFX] Awake defaults applied.", this);
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureAwakeningVolumeReady();
        CachePostFxState();

        if (disableGlobalShaderGlitch)
            ResetGlobalGlitchDefaults();
        else
            Shader.SetGlobalFloat(TvPowerId, 1f);

        RestorePostFx();
        RestoreGlitch();

        if (enableDebugLogs)
            Debug.Log("[AwakeningScreenFX] OnEnable defaults applied.", this);
    }

    private void Update()
    {
        ResolveReferences();
        EnsureAwakeningVolumeReady();

        bool isGrappling = player != null && player.IsGrappling;
        bool isAwakening = awakeningManager != null && awakeningManager.IsAwakening;
        if (isAwakening != lastAwakening)
        {
            if (isAwakening)
                PlayPowerOn();
            else
            {
                PlayPowerOff();
                TriggerAwakeningExitHitPulse();
            }

            lastAwakening = isAwakening;
        }

        if (isAwakening)
        {
            float intensityScale = isGrappling ? grapplingFxMultiplier : 1f;
            ApplyHold(intensityScale);
        }
        else
        {
            RestorePostFx();
            RestoreGlitch();
            if (!disableGlobalShaderGlitch)
                Shader.SetGlobalFloat(TvPowerId, 1f);
        }
    }

    private void ResolveReferences()
    {
        if (player == null)
            player = Object.FindFirstObjectByType<Player>(FindObjectsInactive.Include);

        if (player != null && player.AwakeningManager != null)
            awakeningManager = player.AwakeningManager;
        if (awakeningManager == null)
            awakeningManager = Object.FindFirstObjectByType<AwakeningManager>(FindObjectsInactive.Include);
        if (playerHealth == null && player != null)
            playerHealth = player.GetComponent<Player_Health>();

        if (volume == null || !volume.isGlobal)
            volume = FindFirstGlobalVolume();
    }

    private Volume FindFirstGlobalVolume()
    {
        Volume[] volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < volumes.Length; i++)
        {
            if (volumes[i] != null && volumes[i].isGlobal)
                return volumes[i];
        }

        return volumes.Length > 0 ? volumes[0] : null;
    }

    private void EnsureAwakeningVolumeReady()
    {
        if (awakeningVolume == volume)
        {
            // Prevent shared ownership with grapple/hit FEEL.
            awakeningVolume = null;
        }

        if (awakeningVolume == null && autoCreateAwakeningVolume)
        {
            GameObject volumeObject = new GameObject(string.IsNullOrWhiteSpace(autoCreatedVolumeName)
                ? "Awakening FX Volume"
                : autoCreatedVolumeName);
            awakeningVolume = volumeObject.AddComponent<Volume>();
            awakeningVolume.isGlobal = true;
            awakeningVolume.priority = (volume != null ? volume.priority : 0f) + autoCreatedPriorityOffset;
            awakeningVolume.weight = 0f;
            ownsAwakeningVolume = true;
        }

        if (awakeningVolume == null)
            return;

        awakeningVolume.isGlobal = true;
        if (awakeningVolume == volume)
            return;

        if (awakeningVolume.profile == null)
        {
            VolumeProfile runtimeProfile = CreateRuntimeProfile();
            awakeningVolume.profile = runtimeProfile;
            ownsAwakeningProfile = true;
        }

        EnsureRequiredOverrides(awakeningVolume.profile);
    }

    private VolumeProfile CreateRuntimeProfile()
    {
        if (awakeningProfileTemplate != null)
            return Instantiate(awakeningProfileTemplate);

        VolumeProfile sourceProfile = null;
        if (volume != null)
            sourceProfile = volume.sharedProfile != null ? volume.sharedProfile : volume.profile;
        if (sourceProfile != null)
            return Instantiate(sourceProfile);

        return ScriptableObject.CreateInstance<VolumeProfile>();
    }

    private void EnsureRequiredOverrides(VolumeProfile profile)
    {
        if (profile == null)
            return;

        colorAdjustments = GetOrAddOverride<ColorAdjustments>(profile);
        chromatic = GetOrAddOverride<ChromaticAberration>(profile);
        lensDistortion = GetOrAddOverride<LensDistortion>(profile);
        paniniProjection = GetOrAddOverride<PaniniProjection>(profile);
        filmGrain = GetOrAddOverride<FilmGrain>(profile);
    }

    private static T GetOrAddOverride<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T component))
            return component;

        return profile.Add<T>(true);
    }

    private void PlayPowerOn()
    {
        CachePostFxState();
        StartPowerRoutine(0f, 1f);
        ApplyHold(1f);
    }

    private void PlayPowerOff()
    {
        StartPowerRoutine(1f, 0f);
        RestorePostFx();
        RestoreGlitch();
    }

    private void StartPowerRoutine(float from, float to)
    {
        if (powerCoroutine != null)
            StopCoroutine(powerCoroutine);

        powerCoroutine = StartCoroutine(PowerRoutine(from, to));
    }

    private IEnumerator PowerRoutine(float from, float to)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, powerTransitionDuration);
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            if (!disableGlobalShaderGlitch)
                Shader.SetGlobalFloat(TvPowerId, Mathf.Lerp(from, to, t));
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!disableGlobalShaderGlitch)
            Shader.SetGlobalFloat(TvPowerId, to);
        powerCoroutine = null;
    }

    private void TriggerAwakeningExitHitPulse()
    {
        if (!playHitPulseOnAwakeningExit)
            return;

        if (awakeningExitPulseCoroutine != null)
            StopCoroutine(awakeningExitPulseCoroutine);

        awakeningExitPulseCoroutine = StartCoroutine(PlayAwakeningExitHitPulseRoutine());
    }

    private IEnumerator PlayAwakeningExitHitPulseRoutine()
    {
        if (awakeningExitHitPulseDelay > 0f)
            yield return new WaitForSeconds(awakeningExitHitPulseDelay);

        if (playerHealth == null && player != null)
            playerHealth = player.GetComponent<Player_Health>();
        if (playerHealth == null)
            playerHealth = Object.FindFirstObjectByType<Player_Health>(FindObjectsInactive.Include);

        if (awakeningExitPulseTriggersHitSlowMo)
            GameManager.Instance?.RequestHitSlowMo();

        bool playedDedicatedExitFeedback = false;
        if (awakeningExitFeedback != null)
        {
            PlayFeedback(awakeningExitFeedback, awakeningExitHitPulseIntensity);
            playedDedicatedExitFeedback = true;
        }

        if (!playedDedicatedExitFeedback && playerHealth != null)
        {
            // Fallback to shared hit feedback if a dedicated awakening-exit feedback isn't assigned.
            playerHealth.PlayHitImpactFeedbackOnly(
                awakeningExitHitPulseIntensity,
                includeShieldHitVfxAndSound: awakeningExitPulsePlaysShieldVfxAndSound,
                allowLegacyScreenEffect: false);
        }
        else if (playedDedicatedExitFeedback && awakeningExitPulsePlaysShieldVfxAndSound && playerHealth != null)
        {
            playerHealth.PlayHitImpactVfxAndSoundOnly();
        }

        awakeningExitPulseCoroutine = null;
    }

    private static void PlayFeedback(MMF_Player feedback, float intensityMultiplier = 1f)
    {
        if (feedback == null)
            return;

        feedback.StopFeedbacks();
        feedback.RestoreInitialValues();
        feedback.PlayFeedbacks(feedback.transform.position, Mathf.Max(0f, intensityMultiplier));
    }

    private void ApplyHold(float intensityScale)
    {
        ApplyGlitch(glitchStrength * intensityScale);
        ApplyTint(intensityScale);
    }

    private void ApplyGlitch(float strength)
    {
        if (disableGlobalShaderGlitch)
            return;
        Shader.SetGlobalFloat(GlitchStrengthId, strength);
        Shader.SetGlobalFloat(GlitchHorizontalId, glitchHorizontal);
        Shader.SetGlobalFloat(GlitchBlockSizeId, glitchBlockSize);
        Shader.SetGlobalFloat(GlitchLineJitterId, glitchLineJitter);
        Shader.SetGlobalFloat(GlitchColorSplitId, rgbSplit);
    }

    private void RestoreGlitch()
    {
        if (disableGlobalShaderGlitch)
            return;
        Shader.SetGlobalFloat(GlitchStrengthId, 0f);
        Shader.SetGlobalFloat(GlitchColorSplitId, 0f);
    }

    private void ApplyTint(float intensityScale)
    {
        if (awakeningVolume == null)
            return;

        float tintBlend = Mathf.Clamp01(tintAmount * intensityScale);

        if (colorAdjustments != null)
        {
            Color targetTint = ResolveEffectiveTintColor();
            colorAdjustments.colorFilter.Override(Color.Lerp(cachedColorFilter, targetTint, tintBlend));
            float targetContrast = Mathf.Clamp(cachedContrast + (contrastBoost * intensityScale), -100f, 100f);
            float targetSaturation = Mathf.Clamp(cachedSaturation + (saturationBoost * intensityScale), -100f, 100f);
            float targetPostExposure = Mathf.Clamp(cachedPostExposure + (postExposureBoost * intensityScale), -5f, 5f);
            float currentSaturation = colorAdjustments.saturation.value;
            float appliedSaturation = Mathf.Lerp(cachedSaturation, targetSaturation, tintBlend);
            if (!Mathf.Approximately(currentSaturation, appliedSaturation))
                LogSaturationTrace("ApplyTint", currentSaturation, appliedSaturation);
            colorAdjustments.contrast.Override(Mathf.Lerp(cachedContrast, targetContrast, tintBlend));
            colorAdjustments.saturation.Override(appliedSaturation);
            colorAdjustments.postExposure.Override(Mathf.Lerp(cachedPostExposure, targetPostExposure, tintBlend));
            colorAdjustments.active = true;
        }

        if (chromatic != null)
        {
            float boostedChromatic = chromaticIntensity * intensityScale;
            chromatic.intensity.Override(Mathf.Max(cachedChromatic, boostedChromatic));
            chromatic.active = true;
        }

        if (lensDistortion != null)
        {
            float boostedLens = Mathf.Clamp(lensDistortionIntensity * intensityScale, -1f, 1f);
            lensDistortion.intensity.Override(Mathf.Lerp(cachedLensDistortion, boostedLens, tintBlend));
            lensDistortion.active = true;
        }

        if (paniniProjection != null)
        {
            float boostedPanini = Mathf.Clamp01(paniniDistance * intensityScale);
            paniniProjection.distance.Override(Mathf.Max(cachedPaniniDistance, boostedPanini));
            paniniProjection.active = true;
        }

        if (filmGrain != null)
        {
            float boostedFilmGrain = Mathf.Clamp01(filmGrainIntensity * intensityScale);
            filmGrain.intensity.Override(Mathf.Max(cachedFilmGrainIntensity, boostedFilmGrain));
            filmGrain.active = true;
        }

        awakeningVolume.weight = 1f;
    }

    private Color ResolveEffectiveTintColor()
    {
        if (!useFallbackTintWhenNeutral)
            return neonTint;

        bool tintLooksNeutral = Mathf.Approximately(neonTint.r, 1f)
                                && Mathf.Approximately(neonTint.g, 1f)
                                && Mathf.Approximately(neonTint.b, 1f);
        if (!tintLooksNeutral)
            return neonTint;

        bool baseLooksNeutral = Mathf.Approximately(cachedColorFilter.r, 1f)
                                && Mathf.Approximately(cachedColorFilter.g, 1f)
                                && Mathf.Approximately(cachedColorFilter.b, 1f);

        return baseLooksNeutral ? fallbackVisibleTint : neonTint;
    }

    private void RestorePostFx()
    {
        if (colorAdjustments != null)
        {
            float currentSaturation = colorAdjustments.saturation.value;
            if (!Mathf.Approximately(currentSaturation, cachedSaturation))
                LogSaturationTrace("RestorePostFx", currentSaturation, cachedSaturation);
            colorAdjustments.colorFilter.Override(cachedColorFilter);
            colorAdjustments.contrast.Override(cachedContrast);
            colorAdjustments.saturation.Override(cachedSaturation);
            colorAdjustments.postExposure.Override(cachedPostExposure);
            colorAdjustments.active = cachedColorActive;
        }

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

        if (paniniProjection != null)
        {
            paniniProjection.distance.Override(cachedPaniniDistance);
            paniniProjection.active = cachedPaniniActive;
        }

        if (filmGrain != null)
        {
            filmGrain.intensity.Override(cachedFilmGrainIntensity);
            filmGrain.active = cachedFilmGrainActive;
        }

        if (awakeningVolume != null)
            awakeningVolume.weight = cachedVolumeWeight;
    }

    private void CachePostFxState()
    {
        if (awakeningVolume == null || awakeningVolume.profile == null)
            return;

        EnsureRequiredOverrides(awakeningVolume.profile);
        cachedVolumeWeight = awakeningVolume.weight;

        if (colorAdjustments != null)
        {
            cachedColorFilter = colorAdjustments.colorFilter.value;
            cachedContrast = colorAdjustments.contrast.value;
            cachedSaturation = colorAdjustments.saturation.value;
            cachedPostExposure = colorAdjustments.postExposure.value;
            cachedColorActive = colorAdjustments.active;
        }

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

        if (paniniProjection != null)
        {
            cachedPaniniDistance = paniniProjection.distance.value;
            cachedPaniniActive = paniniProjection.active;
        }

        if (filmGrain != null)
        {
            cachedFilmGrainIntensity = filmGrain.intensity.value;
            cachedFilmGrainActive = filmGrain.active;
        }
    }

    private void OnDisable()
    {
        if (awakeningExitPulseCoroutine != null)
        {
            StopCoroutine(awakeningExitPulseCoroutine);
            awakeningExitPulseCoroutine = null;
        }

        RestorePostFx();
        RestoreGlitch();
        if (disableGlobalShaderGlitch)
            ResetGlobalGlitchDefaults();
        else
            Shader.SetGlobalFloat(TvPowerId, 1f);

        if (enableDebugLogs)
            Debug.Log("[AwakeningScreenFX] OnDisable defaults applied.", this);
    }

    private void OnDestroy()
    {
        if (ownsAwakeningVolume && awakeningVolume != null)
        {
            Destroy(awakeningVolume.gameObject);
            awakeningVolume = null;
            return;
        }

        if (ownsAwakeningProfile && awakeningVolume != null && awakeningVolume.profile != null)
        {
            Destroy(awakeningVolume.profile);
        }
    }

    private void ResetGlobalGlitchDefaults()
    {
        Shader.SetGlobalFloat(TvPowerId, 1f);
        Shader.SetGlobalFloat(GlitchStrengthId, 0f);
        Shader.SetGlobalFloat(GlitchColorSplitId, 0f);
    }

    private void LogSaturationTrace(string source, float before, float after)
    {
        if (!traceSaturationLogs)
            return;

        float volumeWeight = awakeningVolume != null ? awakeningVolume.weight : -1f;
        bool isAwakening = awakeningManager != null && awakeningManager.IsAwakening;
        bool isGrappling = player != null && player.IsGrappling;
        string volumeName = awakeningVolume != null && awakeningVolume.gameObject != null
            ? awakeningVolume.gameObject.name
            : "(null)";

        Debug.Log(
            $"[SAT_TRACE][AwakeningScreenFX.{source}] volume='{volumeName}' sat {before:F2}->{after:F2} weight={volumeWeight:F2} awakening={isAwakening} grappling={isGrappling}",
            this);
    }
}
