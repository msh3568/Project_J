using UnityEngine;
using UnityEngine.UI;

public class OverlayCanvasInspector : MonoBehaviour
{
    [SerializeField] private bool logOnStart = true;

    private void Start()
    {
        if (logOnStart)
        {
            LogActiveOverlays();
        }
    }

    public void LogActiveOverlays()
    {
        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay || !canvas.gameObject.activeInHierarchy)
                continue;

            var images = canvas.GetComponentsInChildren<Image>(true);
            foreach (var image in images)
            {
                if (image == null || !image.gameObject.activeInHierarchy)
                    continue;

                if (image.color.a <= 0.01f)
                    continue;

                var rect = image.rectTransform;
                float w = Mathf.Abs(rect.rect.width);
                float h = Mathf.Abs(rect.rect.height);
                if (w >= Screen.width * 0.9f && h >= Screen.height * 0.9f)
                {
                    Debug.Log($"[OverlayCanvasInspector] Fullscreen overlay: {image.gameObject.name} (Canvas: {canvas.gameObject.name}, alpha={image.color.a})", image);
                }
            }
        }
    }
}
