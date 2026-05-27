using UnityEngine;

public class GrappleDroneTarget : GrappleTargetBase, ICheckpointRespawnable
{
    [SerializeField] private bool enforceEnemyLayer = true;
    [SerializeField, Min(0f)] private float grappleDamage = 9999f;
    [SerializeField, Min(0f)] private float retriggerCooldown = 0.1f;

    private float nextAllowedTriggerTime;

    private void Awake()
    {
        if (!enforceEnemyLayer)
            return;

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            gameObject.layer = enemyLayer;
    }

    public override Vector2 GetArrivalPosition(Player player, LockOnGrappleConfig config, Vector2 startPosition)
    {
        float stopShortDistance = config != null ? config.enemyArrivalStopShortDistance : 0f;
        return ResolveStopShortArrivalPosition(startPosition, stopShortDistance);
    }

    public override void OnGrappleArrive(Player player)
    {
        if (Time.time < nextAllowedTriggerTime)
            return;

        nextAllowedTriggerTime = Time.time + retriggerCooldown;

        // Reuse the parent enemy's own death pipeline (explosion/sound/cleanup).
        if (DamageableLookup.TryGetDamageable(this, out IDamageable damageable))
        {
            damageable.TakeDamage(grappleDamage, player != null ? player.transform : transform);
            GameManager.Instance?.RequestHitSlowMoAndShake();
        }
        else
        {
            // Fallback: keep prior kill-impact feel if this target is on a non-damageable object.
            GameManager.Instance?.RequestHitSlowMoAndShake();
            Destroy(gameObject);
        }
    }
    public void OnCheckpointRespawn()
    {
        nextAllowedTriggerTime = 0f;
    }
}

