using UnityEngine;

[ExecuteAlways]
public class ScreenGlitchDefaults : MonoBehaviour
{
    private static readonly int TvPowerId = Shader.PropertyToID("_TVPower");
    private static readonly int GlitchStrengthId = Shader.PropertyToID("_GlitchStrength");
    private static readonly int GlitchColorSplitId = Shader.PropertyToID("_GlitchColorSplit");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyDefaultsOnLoad()
    {
        Shader.SetGlobalFloat(TvPowerId, 1f);
        Shader.SetGlobalFloat(GlitchStrengthId, 0f);
        Shader.SetGlobalFloat(GlitchColorSplitId, 0f);
    }

    private void OnEnable()
    {
        ApplyDefaultsIfNotPlaying();
    }

    private void Update()
    {
        ApplyDefaultsIfNotPlaying();
    }

    private void ApplyDefaultsIfNotPlaying()
    {
        if (Application.isPlaying)
            return;

        Shader.SetGlobalFloat(TvPowerId, 1f);
        Shader.SetGlobalFloat(GlitchStrengthId, 0f);
        Shader.SetGlobalFloat(GlitchColorSplitId, 0f);
    }
}
