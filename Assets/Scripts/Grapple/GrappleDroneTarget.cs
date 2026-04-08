using UnityEngine;

public class GrappleDroneTarget : GrappleTargetBase, ICheckpointRespawnable
{
    [SerializeField] private bool enforceEnemyLayer = true;

    private bool triggered;

    private void Awake()
    {
        if (!enforceEnemyLayer)
            return;

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            gameObject.layer = enemyLayer;
    }

    public override void OnGrappleArrive(Player player)
    {
        if (triggered)
            return;

        triggered = true;

        // Reuse the parent enemy's own death pipeline (explosion/sound/cleanup).
        IDamageable damageable = GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(9999f, player != null ? player.transform : transform);
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
        triggered = false;
    }
}

