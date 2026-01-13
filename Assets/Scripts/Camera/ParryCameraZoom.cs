using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class ParryCameraZoom : MonoBehaviour
{
    public static ParryCameraZoom Instance { get; private set; }

    [SerializeField, InspectorName("Cinemachine 카메라"), Tooltip("줌을 적용할 대상 Cinemachine 카메라")] private CinemachineCamera cinemachineCamera;

    [Header("줌 목표값(0이면 배율 적용)")]
    [SerializeField, InspectorName("줌인 Ortho Size(작을수록 더 줌인)"), Tooltip("Orthographic 모드 줌인 크기. 0이면 배율 적용")] private float zoomInOrthoSize = 0f;
    [SerializeField, InspectorName("줌인 FOV(작을수록 더 줌인)"), Tooltip("Perspective 모드 줌인 FOV. 0이면 배율 적용")] private float zoomInFov = 0f;
    [SerializeField, Range(0.1f, 1f), InspectorName("줌인 배율(작을수록 더 줌인)"), Tooltip("기본값에 곱하는 줌 배율")] private float zoomInMultiplier = 0.8f;

    [Header("타이밍")]
    [SerializeField, InspectorName("줌인 시간(초)"), Tooltip("줌인에 걸리는 시간(초)")] private float zoomInDuration = 0.12f;
    [SerializeField, InspectorName("줌아웃 시간(초)"), Tooltip("줌아웃에 걸리는 시간(초)")] private float zoomOutDuration = 0.18f;
    [SerializeField, InspectorName("펄스 유지(초)"), Tooltip("펄스 중간 유지 시간(초)")] private float pulseHoldTime = 0.08f;
    [SerializeField, InspectorName("언스케일 시간 사용"), Tooltip("슬로모션 영향 없이 진행")] private bool useUnscaledTime = true;

    private float baseOrthoSize;
    private float baseFov;
    private Coroutine zoomCoroutine;

    private void Awake()
    {
        if (cinemachineCamera == null)
        {
            cinemachineCamera = GetComponent<CinemachineCamera>();
        }
        if (cinemachineCamera == null)
        {
            cinemachineCamera = GetComponentInChildren<CinemachineCamera>();
        }
        CacheBaseLens();
    }

    private void OnEnable()
    {
        Instance = this;
        CacheBaseLens();
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void CacheBaseLens()
    {
        if (cinemachineCamera == null) return;
        var lens = cinemachineCamera.Lens;
        baseOrthoSize = lens.OrthographicSize;
        baseFov = lens.FieldOfView;
    }

    public void BeginParryZoom()
    {
        if (cinemachineCamera == null) return;
        StartZoom(GetZoomInTarget(), zoomInDuration);
    }

    public void EndParryZoom()
    {
        if (cinemachineCamera == null) return;
        StartZoom(GetBaseLensValue(), zoomOutDuration);
    }

    public void Pulse()
    {
        if (cinemachineCamera == null || !gameObject.activeInHierarchy) return;
        if (zoomCoroutine != null)
        {
            StopCoroutine(zoomCoroutine);
        }
        zoomCoroutine = StartCoroutine(PulseRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        StartZoom(GetZoomInTarget(), zoomInDuration);
        if (pulseHoldTime > 0f)
        {
            if (useUnscaledTime)
            {
                yield return new WaitForSecondsRealtime(pulseHoldTime);
            }
            else
            {
                yield return new WaitForSeconds(pulseHoldTime);
            }
        }
        StartZoom(GetBaseLensValue(), zoomOutDuration);
    }

    private void StartZoom(float targetValue, float duration)
    {
        if (zoomCoroutine != null)
        {
            StopCoroutine(zoomCoroutine);
        }
        zoomCoroutine = StartCoroutine(ZoomRoutine(targetValue, duration));
    }

    private IEnumerator ZoomRoutine(float targetValue, float duration)
    {
        float startValue = GetCurrentLensValue();
        if (duration <= 0f)
        {
            SetLensValue(targetValue);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetLensValue(Mathf.Lerp(startValue, targetValue, t));
            yield return null;
        }
        SetLensValue(targetValue);
    }

    private float GetZoomInTarget()
    {
        var lens = cinemachineCamera.Lens;
        if (lens.Orthographic)
        {
            return zoomInOrthoSize > 0f ? zoomInOrthoSize : baseOrthoSize * zoomInMultiplier;
        }
        return zoomInFov > 0f ? zoomInFov : baseFov * zoomInMultiplier;
    }

    private float GetBaseLensValue()
    {
        var lens = cinemachineCamera.Lens;
        return lens.Orthographic ? baseOrthoSize : baseFov;
    }

    private float GetCurrentLensValue()
    {
        var lens = cinemachineCamera.Lens;
        return lens.Orthographic ? lens.OrthographicSize : lens.FieldOfView;
    }

    private void SetLensValue(float value)
    {
        var lens = cinemachineCamera.Lens;
        if (lens.Orthographic)
        {
            lens.OrthographicSize = value;
        }
        else
        {
            lens.FieldOfView = value;
        }
        cinemachineCamera.Lens = lens;
    }
}
