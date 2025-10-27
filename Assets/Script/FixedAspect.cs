using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraAspectFitter : MonoBehaviour
{
    public float targetAspect = 16f / 9f; // 1920x1080 기준 비율

    void Start()
    {
        Camera cam = GetComponent<Camera>();
        float windowAspect = (float)Screen.width / (float)Screen.height;
        float scale = windowAspect / targetAspect;

        if (scale < 1.0f)
        {
            // 세로가 긴 화면 → 상하 잘림 (zoom in)
            cam.orthographicSize /= scale;
        }
        else
        {
            // 가로가 넓은 화면 → 좌우 잘림 (zoom in)
            cam.orthographicSize *= scale;
        }
    }
}
