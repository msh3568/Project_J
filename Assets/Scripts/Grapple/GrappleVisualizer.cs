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
    [SerializeField] private bool matchPlayerSortingLayer = true;
    [SerializeField] private int lineSortingOrderOffset = 5;

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
            ApplyLineSorting(createControllerIfMissing: false);
        }
    }

    private void OnValidate()
    {
        if (lineRenderer != null)
        {
            ApplyLineStyle();
            ApplyLineSorting(createControllerIfMissing: false);
        }
    }

    private void LateUpdate()
    {
        if (lineRenderer == null)
            return;

        GrappleTargetBase target = GetTargetToVisualize();
        if (hideWhenGrappleUnavailable && !ShouldShowVisualizer(target))
        {
            lineRenderer.enabled = false;
            return;
        }

        if (target == null)
        {
            lineRenderer.enabled = false;
            return;
        }

        ApplyLineSorting(createControllerIfMissing: true);
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

    private GrappleTargetBase GetTargetToVisualize()
    {
        if (player != null && player.IsGrappling && player.grappleState != null && player.grappleState.ActiveTarget != null)
            return player.grappleState.ActiveTarget;

        return lockOnSystem != null ? lockOnSystem.CurrentTarget : null;
    }

    private bool ShouldShowVisualizer(GrappleTargetBase target)
    {
        if (target == null)
            return false;

        if (player != null && player.IsGrappling)
            return true;

        return player == null || player.IsGrappleReadyForUI();
    }

    private void ApplyLineSorting(bool createControllerIfMissing)
    {
        if (lineRenderer == null || player == null)
            return;

        PlayerPresentationController presentationController = createControllerIfMissing
            ? PlayerPresentationController.GetOrAdd(player)
            : player.GetComponent<PlayerPresentationController>();
        if (presentationController == null)
            return;

        SpriteRenderer playerRenderer = presentationController.GetPrimarySpriteRenderer();
        if (playerRenderer == null)
            return;

        if (matchPlayerSortingLayer)
            lineRenderer.sortingLayerID = playerRenderer.sortingLayerID;

        lineRenderer.sortingOrder = playerRenderer.sortingOrder + lineSortingOrderOffset;
    }
}
