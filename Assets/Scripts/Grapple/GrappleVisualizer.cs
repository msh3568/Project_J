using UnityEngine;

[RequireComponent(typeof(GrappleLockOnSystem))]
public class GrappleVisualizer : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Vector3 lineStartOffset;
    [SerializeField] private Vector3 lineEndOffset;
    [SerializeField] private bool hideWhenGrappleUnavailable = true;
    [SerializeField] private Material lineMaterialOverride;
    [SerializeField] private Color lockOnLineColor = Color.black;
    [SerializeField] private bool tintMaterialColor = true;

    private GrappleLockOnSystem lockOnSystem;
    private Player player;
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        lockOnSystem = GetComponent<GrappleLockOnSystem>();
        player = GetComponent<Player>();

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            ApplyLineStyle();
        }
    }

    private void OnValidate()
    {
        if (lineRenderer != null)
            ApplyLineStyle();
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

    private void ApplyLineStyle()
    {
        if (lineMaterialOverride != null)
        {
            if (lineRenderer.sharedMaterial != lineMaterialOverride)
                lineRenderer.sharedMaterial = lineMaterialOverride;
        }
        else if (lineRenderer.sharedMaterial == null)
        {
            Shader defaultShader = Shader.Find("Sprites/Default");
            if (defaultShader != null)
                lineRenderer.sharedMaterial = new Material(defaultShader);
        }

        Color start = lineRenderer.startColor;
        Color end = lineRenderer.endColor;
        lineRenderer.startColor = new Color(lockOnLineColor.r, lockOnLineColor.g, lockOnLineColor.b, start.a);
        lineRenderer.endColor = new Color(lockOnLineColor.r, lockOnLineColor.g, lockOnLineColor.b, end.a);

        Material material = lineRenderer.sharedMaterial;
        if (tintMaterialColor && material != null && material.HasProperty(ColorPropertyId))
        {
            Color materialColor = material.color;
            material.color = new Color(lockOnLineColor.r, lockOnLineColor.g, lockOnLineColor.b, materialColor.a);
        }
    }
}
