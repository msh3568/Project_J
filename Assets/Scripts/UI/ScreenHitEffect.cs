using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenHitEffect : MonoBehaviour
{
    [Header("Overlay")]
    [SerializeField] private Image redOverlay;
    [SerializeField, Range(0f, 1f)] private float maxOverlayAlpha = 0.45f;
    [SerializeField] private float duration = 0.8f;
    [SerializeField] private AnimationCurve overlayFade = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Twist (Optional)")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float twistAngle = 0.6f;
    [SerializeField] private float twistFrequency = 18f;
    [SerializeField] private float positionJitter = 0.03f;

    private Coroutine effectCoroutine;
    private Vector3 cachedCameraLocalPos;
    private Quaternion cachedCameraLocalRot;

    public void Play()
    {
        if (effectCoroutine != null)
            StopCoroutine(effectCoroutine);

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

            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyOverlay(1f);
        RestoreCameraTransform();
        effectCoroutine = null;
    }

    private void ApplyOverlay(float t)
    {
        if (redOverlay == null)
            return;

        float alpha = maxOverlayAlpha * Mathf.Clamp01(overlayFade.Evaluate(t));
        Color c = redOverlay.color;
        c.a = alpha;
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
}
