using UnityEngine;

[RequireComponent(typeof(Player))]
public class GrappleLockOnSystem : MonoBehaviour
{
    [SerializeField] private LockOnGrappleConfig config;

    private readonly Collider2D[] overlapBuffer = new Collider2D[64];

    private Player player;
    private Camera mainCamera;
    private GrappleTargetBase currentTarget;
    private GrappleTargetBase lastUsedTarget;
    private float lastUsedUntil;

    public GrappleTargetBase CurrentTarget => currentTarget;
    public LockOnGrappleConfig Config => config;

    private void Awake()
    {
        player = GetComponent<Player>();
    }

    private void Update()
    {
        RefreshLockOn();
    }

    public void SetConfig(LockOnGrappleConfig newConfig)
    {
        config = newConfig;
    }

    public void RefreshLockOn()
    {
        if (player == null || config == null)
        {
            currentTarget = null;
            return;
        }

        currentTarget = FindBestTarget();
    }

    public void MarkTargetAsRecentlyUsed(GrappleTargetBase target)
    {
        if (target == null || config == null || config.excludeTime <= 0f)
            return;

        lastUsedTarget = target;
        lastUsedUntil = Time.time + config.excludeTime;
    }

    private GrappleTargetBase FindBestTarget()
    {
        LayerMask searchMask = config.GetSearchLayerMask();
        int hitCount = Physics2D.OverlapCircleNonAlloc(player.transform.position, config.searchRadius, overlapBuffer, searchMask);
        if (hitCount <= 0)
            return null;

        Vector2 forward = GetForwardDirection();
        float halfPrimary = config.coneAngle * 0.5f;
        float halfFallback = config.fallbackConeAngle * 0.5f;

        GrappleTargetBase bestPrimary = null;
        GrappleTargetBase bestFallback = null;
        float bestPrimaryScore = float.MaxValue;
        float bestFallbackScore = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = overlapBuffer[i];
            if (col == null)
                continue;

            GrappleTargetBase target = col.GetComponentInParent<GrappleTargetBase>();
            if (target == null)
                target = col.GetComponent<GrappleTargetBase>();

            if (target == null || !target.IsAvailableForGrapple(player))
                continue;

            if (target == lastUsedTarget && Time.time < lastUsedUntil)
                continue;

            Vector2 targetPos = target.GetAimPosition();
            Vector2 toTarget = targetPos - (Vector2)player.transform.position;
            float distance = toTarget.magnitude;
            if (distance <= 0.001f || distance > config.searchRadius)
                continue;

            if (config.requireOnScreen && !IsOnScreen(targetPos))
                continue;

            if (IsOccluded((Vector2)player.transform.position, targetPos, target))
                continue;

            float angle = Vector2.Angle(forward, toTarget.normalized);

            bool insidePrimary = angle <= halfPrimary;
            bool insideFallback = angle <= halfFallback;
            if (!insideFallback)
                continue;

            float normDist = Mathf.Clamp01(distance / Mathf.Max(0.001f, config.searchRadius));
            float normAnglePrimary = Mathf.Clamp01(angle / Mathf.Max(1f, halfPrimary));
            float normAngleFallback = Mathf.Clamp01(angle / Mathf.Max(1f, halfFallback));

            if (insidePrimary)
            {
                float score = config.distWeight * normDist + config.angleWeight * normAnglePrimary;
                if (score < bestPrimaryScore)
                {
                    bestPrimaryScore = score;
                    bestPrimary = target;
                }
            }
            else
            {
                float score = config.distWeight * normDist + config.angleWeight * normAngleFallback;
                if (score < bestFallbackScore)
                {
                    bestFallbackScore = score;
                    bestFallback = target;
                }
            }
        }

        return bestPrimary != null ? bestPrimary : bestFallback;
    }

    private Vector2 GetForwardDirection()
    {
        // Allow lock-on while jumping/falling by prioritizing current movement input's vertical intent.
        Vector2 forward = new Vector2(player.facingDir, 0f);

        if (player.moveInput.y > config.upInputThreshold)
            forward += Vector2.up * config.upBias;

        if (forward.sqrMagnitude < 0.001f)
            return Vector2.right;

        return forward.normalized;
    }



    private bool IsOccluded(Vector2 from, Vector2 to, GrappleTargetBase target)
    {
        if (config == null || !config.blockBehindWalls)
            return false;

        int mask = config.occlusionMask.value;
        if (mask == 0)
            return false;

        RaycastHit2D hit = Physics2D.Linecast(from, to, mask);
        if (hit.collider == null)
            return false;

        Transform hitTransform = hit.collider.transform;
        if (hitTransform.IsChildOf(player.transform))
            return false;
        if (target != null && hitTransform.IsChildOf(target.transform))
            return false;

        float totalDistance = Vector2.Distance(from, to);
        float blockedDistance = hit.distance;

        // Keep some forgiveness: near-target cover or tiny clips should still allow lock-on.
        if ((totalDistance - blockedDistance) <= config.occlusionNearTargetGrace)
            return false;
        if (blockedDistance <= config.occlusionMinBlockedDistance)
            return false;

        return true;
    }

    private bool IsOnScreen(Vector2 worldPos)
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            return true;

        Vector3 viewport = mainCamera.WorldToViewportPoint(worldPos);
        return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
    }

    private void OnDrawGizmosSelected()
    {
        if (config == null)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, config.searchRadius);
    }
}
