using UnityEngine;
using Unity.Cinemachine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineCamera))]
public class CameraBoundsConfiner : MonoBehaviour
{
    [SerializeField] private Collider2D bounds;
    [SerializeField, Range(0f, 5f)] private float damping = 0f;
    [SerializeField, Min(0f)] private float slowingDistance = 0f;
    [SerializeField] private bool invalidateCacheOnEnable = true;

    private CinemachineConfiner2D confiner;

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        Apply();
    }

    public void Apply()
    {
        if (confiner == null)
        {
            confiner = GetComponent<CinemachineConfiner2D>();
        }

        if (confiner == null)
        {
            confiner = gameObject.AddComponent<CinemachineConfiner2D>();
        }

        confiner.BoundingShape2D = bounds;
        confiner.Damping = damping;
        confiner.SlowingDistance = slowingDistance;

        if (invalidateCacheOnEnable && bounds != null)
        {
            confiner.InvalidateBoundingShapeCache();
        }
    }
}
