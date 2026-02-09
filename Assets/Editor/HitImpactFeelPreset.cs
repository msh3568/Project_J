using System;
using System.Linq;
using MoreMountains.Feedbacks;
using MoreMountains.FeedbacksForThirdParty;
using MoreMountains.Tools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class HitImpactFeelPreset
{
    private const string TargetPlayerName = "hitimpact";
    private const string HitOverlayName = "HitOverlay";

    [MenuItem("Tools/Feel/Apply HitImpact Preset")]
    public static void ApplyPreset()
    {
        MMF_Player[] players = UnityEngine.Object.FindObjectsOfType<MMF_Player>(true);
        MMF_Player[] targets = players
            .Where(p => p != null && string.Equals(p.gameObject.name, TargetPlayerName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (targets.Length == 0)
        {
            EditorUtility.DisplayDialog("HitImpact Preset", $"MMF_Player named '{TargetPlayerName}' not found in the active scene.", "OK");
            return;
        }

        Image hitOverlay = FindHitOverlay();

        foreach (MMF_Player player in targets)
        {
            ApplyToPlayer(player, hitOverlay);
            EditorUtility.SetDirty(player);
        }

        if (targets.Length > 0)
        {
            EditorSceneManager.MarkSceneDirty(targets[0].gameObject.scene);
        }

        EditorUtility.DisplayDialog("HitImpact Preset", $"Applied preset to {targets.Length} MMF_Player(s).", "OK");
    }

    private static Image FindHitOverlay()
    {
        Image[] images = UnityEngine.Object.FindObjectsOfType<Image>(true);
        return images.FirstOrDefault(i =>
            i != null &&
            string.Equals(i.gameObject.name, HitOverlayName, StringComparison.OrdinalIgnoreCase));
    }

    private static void ApplyToPlayer(MMF_Player player, Image hitOverlay)
    {
        if (player.FeedbacksList == null)
            player.FeedbacksList = new System.Collections.Generic.List<MMF_Feedback>();

        MMF_ImageAlpha imageAlpha = GetOrAdd<MMF_ImageAlpha>(player);
        ConfigureImageAlpha(imageAlpha, hitOverlay);

        MMF_ChromaticAberration_URP chroma = GetOrAdd<MMF_ChromaticAberration_URP>(player);
        ConfigureChromatic(chroma);

        MMF_LensDistortion_URP lens = GetOrAdd<MMF_LensDistortion_URP>(player);
        ConfigureLensDistortion(lens);

        MMF_PaniniProjection_URP panini = GetOrAdd<MMF_PaniniProjection_URP>(player);
        ConfigurePanini(panini);

        MMF_Vignette_URP vignette = GetOrAdd<MMF_Vignette_URP>(player);
        ConfigureVignette(vignette);

        MMF_FilmGrain_URP filmGrain = GetOrAdd<MMF_FilmGrain_URP>(player);
        ConfigureFilmGrain(filmGrain);

        MMF_CameraShake cameraShake = GetOrAdd<MMF_CameraShake>(player);
        ConfigureCameraShake(cameraShake);

        MMF_FreezeFrame freeze = GetOrAdd<MMF_FreezeFrame>(player);
        ConfigureFreezeFrame(freeze);

        MMF_TimescaleModifier timescale = GetOrAdd<MMF_TimescaleModifier>(player);
        ConfigureTimescale(timescale);
    }

    private static T GetOrAdd<T>(MMF_Player player) where T : MMF_Feedback, new()
    {
        T existing = player.FeedbacksList.OfType<T>().FirstOrDefault();
        if (existing != null)
            return existing;

        T created = new T();
        player.FeedbacksList.Add(created);
        return created;
    }

    private static AnimationCurve SharpSpikeCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.2f, 1f),
            new Keyframe(0.4f, 0f)
        );
    }

    private static AnimationCurve SoftSpikeCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 0f)
        );
    }

    private static void ConfigureImageAlpha(MMF_ImageAlpha feedback, Image hitOverlay)
    {
        feedback.Label = "Hit Overlay";
        feedback.BoundImage = hitOverlay;
        feedback.Mode = MMF_ImageAlpha.Modes.OverTime;
        feedback.Duration = 0.28f;
        feedback.AllowAdditivePlays = false;
        feedback.Curve = new MMTweenType(SharpSpikeCurve(), "", "Mode", (int)MMF_ImageAlpha.Modes.OverTime, (int)MMF_ImageAlpha.Modes.ToDestination);
        feedback.CurveRemapZero = 0f;
        feedback.CurveRemapOne = 0.4f;
        feedback.DisableOnStop = false;

        if (hitOverlay != null)
        {
            Color c = hitOverlay.color;
            c.a = 0f;
            hitOverlay.color = c;
        }
    }

    private static void ConfigureChromatic(MMF_ChromaticAberration_URP feedback)
    {
        feedback.Label = "Chromatic Aberration";
        feedback.Duration = 0.1f;
        feedback.ResetShakerValuesAfterShake = true;
        feedback.ResetTargetValuesAfterShake = true;
        feedback.RelativeIntensity = false;
        feedback.Intensity = SharpSpikeCurve();
        feedback.RemapIntensityZero = 0f;
        feedback.RemapIntensityOne = 0.55f;
    }

    private static void ConfigureLensDistortion(MMF_LensDistortion_URP feedback)
    {
        feedback.Label = "Lens Distortion";
        feedback.Duration = 0.12f;
        feedback.ResetShakerValuesAfterShake = true;
        feedback.ResetTargetValuesAfterShake = true;
        feedback.RelativeIntensity = false;
        feedback.Intensity = SharpSpikeCurve();
        feedback.RemapIntensityZero = 0f;
        feedback.RemapIntensityOne = -0.25f;
    }

    private static void ConfigurePanini(MMF_PaniniProjection_URP feedback)
    {
        feedback.Label = "Panini Projection";
        feedback.Duration = 0.12f;
        feedback.ResetShakerValuesAfterShake = true;
        feedback.ResetTargetValuesAfterShake = true;
        feedback.RelativeDistance = false;
        feedback.ShakeDistance = SharpSpikeCurve();
        feedback.RemapDistanceZero = 0f;
        feedback.RemapDistanceOne = 0.2f;
    }

    private static void ConfigureVignette(MMF_Vignette_URP feedback)
    {
        feedback.Label = "Vignette";
        feedback.Duration = 0.2f;
        feedback.ResetShakerValuesAfterShake = true;
        feedback.ResetTargetValuesAfterShake = true;
        feedback.RelativeIntensity = true;
        feedback.Intensity = SoftSpikeCurve();
        feedback.RemapIntensityZero = 0f;
        feedback.RemapIntensityOne = 0.2f;
        feedback.InterpolateColor = false;
    }

    private static void ConfigureFilmGrain(MMF_FilmGrain_URP feedback)
    {
        feedback.Label = "Film Grain";
        feedback.Duration = 0.15f;
        feedback.ResetShakerValuesAfterShake = true;
        feedback.ResetTargetValuesAfterShake = true;
        feedback.RelativeIntensity = false;
        feedback.Intensity = SoftSpikeCurve();
        feedback.RemapIntensityZero = 0f;
        feedback.RemapIntensityOne = 0.35f;
    }

    private static void ConfigureCameraShake(MMF_CameraShake feedback)
    {
        feedback.Label = "Camera Shake";
        feedback.RepeatUntilStopped = false;
        feedback.CameraShakeProperties = new MMCameraShakeProperties(0.1f, 0.55f, 22f);
    }

    private static void ConfigureFreezeFrame(MMF_FreezeFrame feedback)
    {
        feedback.Label = "Freeze Frame";
        feedback.FreezeFrameDuration = 0.03f;
        feedback.MinimumTimescaleThreshold = 0.1f;
    }

    private static void ConfigureTimescale(MMF_TimescaleModifier feedback)
    {
        feedback.Label = "Timescale Modifier";
        feedback.Mode = MMF_TimescaleModifier.Modes.Shake;
        feedback.TimeScale = 0.15f;
        feedback.TimeScaleDuration = 0.1f;
        feedback.ResetTimescaleOnStop = false;
        feedback.UnfreezeTimescaleOnStop = false;
        feedback.TimeScaleLerp = false;
    }
}
