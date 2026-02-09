using UnityEngine;

[RequireComponent(typeof(GrappleLockOnSystem))]
public class GrappleVisualizer : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Vector3 lineStartOffset;
    [SerializeField] private Vector3 lineEndOffset;
    [SerializeField] private bool hideWhenGrappleUnavailable = true;

    private GrappleLockOnSystem lockOnSystem;
    private Player player;

    private void Awake()
    {
        lockOnSystem = GetComponent<GrappleLockOnSystem>();
        player = GetComponent<Player>();

        if (lineRenderer != null)
            lineRenderer.positionCount = 2;
    }

    private void LateUpdate()
    {
        if (lineRenderer == null)
            return;

        if (hideWhenGrappleUnavailable && player != null && !player.IsGrappleReadyForUI())
        {
            lineRenderer.enabled = false;
            return;
        }

        GrappleTargetBase target = lockOnSystem != null ? lockOnSystem.CurrentTarget : null;
        if (target == null)
        {
            lineRenderer.enabled = false;
            return;
        }

        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, transform.position + lineStartOffset);
        lineRenderer.SetPosition(1, (Vector3)target.GetAimPosition() + lineEndOffset);
    }
}
