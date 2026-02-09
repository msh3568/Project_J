using UnityEngine;

[ExecuteAlways]
public class GlitchFeatureKillSwitch : MonoBehaviour
{
    [SerializeField] private bool disableGlitchFeature = true;

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            Apply();
        }
    }

    private void Apply()
    {
        GlitchFullScreenFeature.ForceDisable = disableGlitchFeature;
    }
}
