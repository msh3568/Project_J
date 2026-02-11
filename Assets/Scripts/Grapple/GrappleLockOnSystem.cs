using UnityEngine;

[RequireComponent(typeof(Player))]
public class GrappleLockOnSystem : MonoBehaviour
{
    [SerializeField] private LockOnGrappleConfig config;
    [SerializeField] private bool forceGroundWallOcclusion = true;

    private readonly Collider2D[] overlapBuffer = new Collider2D[64];

    private Player player;
    private Camera mainCamera;
    private GrappleTargetBase currentTarget;
    private GrappleTargetBase lastUsedTarget;
    private float lastUsedUntil;
    private bool loggedMissingConfig;
    private bool loggedMissingOcclusionMask;
    private bool loggedLockOnBlockedByOcclusion;
    private bool loggedFallbackLayerScan;

    public GrappleTargetBase CurrentTarget => currentTarget;
    public LockOnGrappleConfig Config => config;

    private void Awake()
    {
        player = GetComponent<Player>();
        EnsureConfig();
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
        EnsureConfig();
        if (player == null || config == null)
        {
            currentTarget = null;
            return;
        }

        EnsureOcclusionMask();
        if (!HasValidOcclusionMask())
        {
            currentTarget = null;
            return;
        }
        currentTarget = FindBestTarget();
    }

    private void EnsureConfig()
    {
        if (config != null)
            return;

        if (player != null && player.GrappleConfig != null)
        {
            config = player.GrappleConfig;
            return;
        }

        config = ScriptableObject.CreateInstance<LockOnGrappleConfig>();
        config.hideFlags = HideFlags.DontSave;
        config.targetLayerMask = ~0;
        EnsureOcclusionMask();

        if (!loggedMissingConfig)
        {
            loggedMissingConfig = true;
            Debug.LogWarning("[GrappleLockOnSystem] Missing LockOnGrappleConfig. Using runtime defaults.", this);
        }
    }

    private void EnsureOcclusionMask()
    {
        if (config == null || !ShouldBlockBehindWalls())
            return;

        int forcedMask = ResolveGroundWallMask();
        if (forcedMask != 0)
        {
            config.occlusionMask = config.occlusionMask.value | forcedMask;
        }

        if (config.occlusionMask.value != 0)
            return;

        int combinedMask = 0;
        if (player != null)
        {
            combinedMask |= player.WallLayerMask.value;
            combinedMask |= player.GroundLayerMask.value;
        }

        if (combinedMask != 0)
        {
            config.occlusionMask = combinedMask;
            return;
        }

        int wallLayer = LayerMask.NameToLayer("Wall");
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (wallLayer >= 0)
        {
            combinedMask |= 1 << wallLayer;
        }
        if (groundLayer >= 0)
        {
            combinedMask |= 1 << groundLayer;
        }
        if (combinedMask != 0)
        {
            config.occlusionMask = combinedMask;
        }

        if (!loggedMissingOcclusionMask)
        {
            loggedMissingOcclusionMask = true;
            Debug.LogWarning("[GrappleLockOnSystem] Occlusion mask is empty. Grapple may lock through walls.", this);
        }
    }

    private bool HasValidOcclusionMask()
    {
        if (config == null || !ShouldBlockBehindWalls())
            return true;

        int mask = config.occlusionMask.value | ResolveGroundWallMask();
        if (mask == 0 && player != null)
            mask = player.WallLayerMask.value | player.GroundLayerMask.value;
        if (mask == 0)
        {
            EnsureOcclusionMask();
            mask = config != null ? (config.occlusionMask.value | ResolveGroundWallMask()) : 0;
        }

        if (mask != 0)
            return true;

        if (!loggedLockOnBlockedByOcclusion)
        {
            loggedLockOnBlockedByOcclusion = true;
            Debug.LogWarning("[GrappleLockOnSystem] Occlusion mask missing. Lock-on disabled to prevent grappling through walls.", this);
        }

        return false;
    }

    private bool ShouldBlockBehindWalls()
    {
        if (config == null)
            return false;

        return config.blockBehindWalls || forceGroundWallOcclusion;
    }

    private int ResolveGroundWallMask()
    {
        int mask = 0;
        if (player != null)
        {
            mask |= player.WallLayerMask.value;
            mask |= player.GroundLayerMask.value;
        }

        if (mask != 0)
            return mask;

        int wallLayer = LayerMask.NameToLayer("Wall");
        if (wallLayer >= 0)
            mask |= 1 << wallLayer;
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
            mask |= 1 << groundLayer;

        return mask;
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
        Vector2 forward = GetForwardDirection();
        float halfPrimary = config.coneAngle * 0.5f;
        float halfFallback = config.fallbackConeAngle * 0.5f;
        int primaryMask = config.GetSearchLayerMask().value;
        bool shouldRunFallbackPass = primaryMask != ~0;
        int passCount = shouldRunFallbackPass ? 2 : 1;

        for (int pass = 0; pass < passCount; pass++)
        {
            int mask = pass == 0 ? primaryMask : ~0;
            int hitCount = Physics2D.OverlapCircleNonAlloc(player.transform.position, config.searchRadius, overlapBuffer, mask);
            if (hitCount <= 0)
                continue;

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

            GrappleTargetBase selected = bestPrimary != null ? bestPrimary : bestFallback;
            if (selected != null)
            {
                if (pass == 1 && !loggedFallbackLayerScan)
                {
                    loggedFallbackLayerScan = true;
                    Debug.LogWarning("[GrappleLockOnSystem] Target found only via all-layer fallback. Check targetLayerMask/layer setup for GrapplePointTarget.", this);
                }

                return selected;
            }
        }

        return null;
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
        if (config == null || !ShouldBlockBehindWalls())
            return false;

        int mask = config.occlusionMask.value | ResolveGroundWallMask();
        if (mask == 0 && player != null)
            mask = player.WallLayerMask.value | player.GroundLayerMask.value;
        if (mask == 0)
        {
            EnsureOcclusionMask();
            mask = config != null ? (config.occlusionMask.value | ResolveGroundWallMask()) : 0;
        }
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
