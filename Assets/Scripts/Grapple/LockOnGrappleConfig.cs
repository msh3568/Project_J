using UnityEngine;

public enum GrappleInputAction
{
    Jump = 0,
    Dash = 1,
    CounterAttack = 2
}

[CreateAssetMenu(menuName = "Grapple/LockOn Grapple Config", fileName = "LockOnGrappleConfig")]
public class LockOnGrappleConfig : ScriptableObject
{
    [Header("Input")]
    public GrappleInputAction inputAction = GrappleInputAction.Jump;

    [Header("Target Search")]
    public LayerMask targetLayerMask;
    public bool includeEnemyLayer = true;
    public float searchRadius = 18f;
    [Range(1f, 180f)] public float coneAngle = 120f;
    [Range(1f, 180f)] public float fallbackConeAngle = 180f;
    [Range(0f, 1f)] public float upInputThreshold = 0.25f;
    [Range(0f, 2f)] public float upBias = 0.7f;
    public bool requireOnScreen = true;


    [Header("Occlusion")]
    public bool blockBehindWalls = true;
    public LayerMask occlusionMask;
    [Min(0f)] public float occlusionNearTargetGrace = 0.6f;
    [Min(0f)] public float occlusionMinBlockedDistance = 0.2f;

    [Header("Scoring (lower is better)")]
    [Min(0f)] public float distWeight = 1f;
    [Min(0f)] public float angleWeight = 1f;

    [Header("Recent Target Exclusion")]
    [Min(0f)] public float excludeTime = 1.2f;

    [Header("Travel")]
    [Min(0.01f)] public float travelTime = 0.15f;
    [Min(0f)] public float enemyArrivalStopShortDistance = 0.75f;
    [Range(0f, 0.99f)] public float attackAnimationTriggerProgress = 0.55f;
    [Min(0f)] public float postAttackStateHoldDuration = 0.2f;
    public bool phaseThroughDuringGrapple = true;
    public bool invincibleDuringGrapple = true;

    [Header("Time Effects")]
    [Range(0.1f, 1f)] public float startSlowScale = 0.7f;
    [Min(0f)] public float startSlowDuration = 0.06f;
    [Range(0.05f, 1f)] public float droneArriveSlowScale = 0.2f;
    [Min(0f)] public float droneArriveSlowDuration = 0.1f;

    [Header("Feedback")]
    public AudioClip grappleStartSfx;
    [Range(0f, 1f)] public float grappleStartSfxVolume = 1f;
    public bool spawnAfterImageOnStart = true;

    public bool WasGrapplePressed(PlayerInputSet input)
    {
        if (input == null)
            return false;

        switch (inputAction)
        {
            case GrappleInputAction.Dash:
                return input.Player.Dash.WasPressedThisFrame();
            case GrappleInputAction.CounterAttack:
                return input.Player.CounterAttack.WasPressedThisFrame();
            default:
                return input.Player.Jump.WasPressedThisFrame();
        }
    }

    public LayerMask GetSearchLayerMask()
    {
        int mask = targetLayerMask.value;

        if (includeEnemyLayer)
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
                mask |= (1 << enemyLayer);
        }

        // If mask is empty, search everything to avoid hard-fail due to misconfiguration.
        if (mask == 0)
            mask = ~0;

        return mask;
    }
}
