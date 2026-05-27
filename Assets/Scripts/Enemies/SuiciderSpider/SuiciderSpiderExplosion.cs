using UnityEngine;

public class SuiciderSpiderExplosion : MonoBehaviour
{
    [Header("Explosion Settings")]
    [SerializeField] private float radius = 1.5f;
    [SerializeField] private float enemyDamage = 10f;
    [SerializeField] private int firewallDamage = 1;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LayerMask playerLayer;

    [Header("Knockback (Optional)")]
    [SerializeField] private bool applyKnockback = false;
    [SerializeField] private float knockbackForce = 6f;
    [SerializeField] private float knockbackDuration = 0.15f;

    public void Configure(
        float radiusValue,
        float enemyDamageValue,
        int firewallDamageValue,
        LayerMask enemyMask,
        LayerMask playerMask,
        bool knockbackEnabled,
        float knockbackForceValue,
        float knockbackDurationValue)
    {
        radius = radiusValue;
        enemyDamage = enemyDamageValue;
        firewallDamage = firewallDamageValue;
        enemyLayer = enemyMask;
        playerLayer = playerMask;
        applyKnockback = knockbackEnabled;
        knockbackForce = knockbackForceValue;
        knockbackDuration = knockbackDurationValue;
    }

    public void Explode()
    {
        Vector2 origin = transform.position;

        Collider2D[] enemies = Physics2D.OverlapCircleAll(origin, radius, enemyLayer);
        foreach (var enemy in enemies)
        {
            if (DamageableLookup.TryGetDamageable(enemy, out IDamageable damageable))
            {
                damageable.TakeDamage(enemyDamage, transform);
            }

            if (applyKnockback)
            {
                Entity entity = enemy.GetComponent<Entity>();
                if (entity != null)
                {
                    Vector2 dir = ((Vector2)enemy.transform.position - origin).normalized;
                    entity.ReciveKnockback(dir * knockbackForce, knockbackDuration);
                }
            }
        }

        Collider2D playerHit = Physics2D.OverlapCircle(origin, radius, playerLayer);
        if (playerHit != null)
        {
            IFirewallDamageable firewall = playerHit.GetComponent<IFirewallDamageable>();
            if (firewall != null)
            {
                firewall.TakeFirewallDamage(firewallDamage);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
