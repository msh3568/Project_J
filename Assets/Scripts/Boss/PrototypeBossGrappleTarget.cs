using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PrototypeBossGrappleTarget : GrappleTargetBase, ICheckpointRespawnable
{
    [SerializeField] private PrototypeBossController boss;
    [SerializeField] private Transform targetPart;
    [SerializeField] private bool triggersLeftArmPunish;
    [SerializeField] private bool triggersRightArmSwat;
    [SerializeField] private bool enforceEnemyLayer = true;
    [SerializeField, Min(0f)] private float retriggerCooldown = 0.15f;

    private float nextAllowedTriggerTime;

    private void Awake()
    {
        ResolveReferences();
        ApplyLayer();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    public void Configure(PrototypeBossController bossController, Transform part, bool shouldTriggerLeftArmPunish, bool shouldTriggerRightArmSwat)
    {
        boss = bossController;
        targetPart = part != null ? part : transform;
        triggersLeftArmPunish = shouldTriggerLeftArmPunish;
        triggersRightArmSwat = shouldTriggerRightArmSwat;
        ApplyLayer();
    }

    public override bool IsAvailableForGrapple(Player player)
    {
        return base.IsAvailableForGrapple(player)
            && boss != null
            && boss.CanBeGrappled(player);
    }

    public override Vector2 GetAimPosition()
    {
        if (targetPart != null)
            return targetPart.position;

        return base.GetAimPosition();
    }

    public override Vector2 GetArrivalPosition(Player player, LockOnGrappleConfig config, Vector2 startPosition)
    {
        float stopShortDistance = config != null ? config.enemyArrivalStopShortDistance : 0f;
        return ResolveStopShortArrivalPosition(startPosition, stopShortDistance);
    }

    public override float GetLockOnScore(Player player, LockOnGrappleConfig config, float normalizedDistance, float normalizedAngle)
    {
        return normalizedDistance;
    }

    public override void OnGrappleArrive(Player player)
    {
        if (!triggersLeftArmPunish && !triggersRightArmSwat)
            return;

        if (Time.time < nextAllowedTriggerTime)
            return;

        nextAllowedTriggerTime = Time.time + retriggerCooldown;

        if (boss == null)
            ResolveReferences();

        if (triggersLeftArmPunish)
            boss?.TryStartLeftArmGrapplePunish(player);
        else if (triggersRightArmSwat)
            boss?.TryStartRightArmGrappleSwat(player);
    }

    public override bool ShouldPlayArrivalVfx(Player player)
    {
        return !triggersLeftArmPunish && !triggersRightArmSwat;
    }

    public override bool ShouldSuppressGrappleAttackHit(Player player, Collider2D hitTarget, IDamageable damageable)
    {
        if (!triggersLeftArmPunish && !triggersRightArmSwat)
            return false;

        if (boss == null)
            ResolveReferences();

        if (boss == null || damageable == null)
            return false;

        if (ReferenceEquals(damageable, boss))
            return true;

        return hitTarget != null && hitTarget.transform.IsChildOf(boss.transform);
    }

    public void OnCheckpointRespawn()
    {
        nextAllowedTriggerTime = 0f;
    }

    private void ResolveReferences()
    {
        if (boss == null)
            boss = GetComponentInParent<PrototypeBossController>();

        if (targetPart == null)
            targetPart = transform;
    }

    private void ApplyLayer()
    {
        if (!enforceEnemyLayer)
            return;

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            gameObject.layer = enemyLayer;
    }
}
