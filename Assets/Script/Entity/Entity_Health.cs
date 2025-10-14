using UnityEngine;
using UnityEngine.UI;

public class Entity_Health : MonoBehaviour, IDamagable
{
    private Entity_VFX entityVfx;
    private Entity entity;

    public event System.Action<float, float> onHealthChanged;

    [SerializeField] public float currentHp;
    [SerializeField] public float maxHp = 100;
    [SerializeField] protected bool isDead;

    [Header("On Damage Knockback")]
    [SerializeField] private Vector2 knockbackPower = new Vector2(1.5f, 2.5f);
    [SerializeField] private Vector2 heavyKnockbackPower = new Vector2(7,7);
    [SerializeField] private float knockbackDuration = .2f;
    [SerializeField] private float heavyKnockbackDuration = .5f;
    [Header("On Heavy Damage")]
    [SerializeField] private float heavyDamageThreshold = .3f;

    protected virtual void Awake()
    {
        entityVfx  = GetComponent<Entity_VFX>();
        entity = GetComponent<Entity>();

        currentHp = maxHp;
        onHealthChanged?.Invoke(currentHp, maxHp);
    }

    public virtual object GetEntityVfx()
    {
        return entityVfx;
    }

    public virtual void TakeDamage(float damage,Transform damagedealer)
    {
        if (isDead)
            return;

        Player player = GetComponent<Player>();
        if (player != null)
        {
            float damageReduction = (float)player.defense / 100f;
            damage *= (1 - damageReduction);
        }

        Vector2 knockback = CalculateKnockback(damage, damagedealer);
        float duration = CalculateDuration(damage);

        entity?.ReciveKnockback(knockback, duration);

        if (entityVfx != null)
        {
            entityVfx?.PlayOnDamageVfx();
        }

        ReduceHp(damage);
    }


    protected void ReduceHp(float damage)
    {
        currentHp -= damage;
        onHealthChanged?.Invoke(currentHp, maxHp);

        if (currentHp <= 0)
            Die();

       
    }

    protected virtual void Die()
    {
        isDead = true;
        entity.onEntityDeath();
    }

    private Vector2 CalculateKnockback(float damage, Transform damagedealer)
    {
        int direction = transform.position.x > damagedealer.position.x ? 1 : -1;
        Vector2 knockback = IsHeavyDamage(damage) ? heavyKnockbackPower : knockbackPower;

        knockback.x = knockback.x * direction;

        return knockback;
    }
    private float CalculateDuration(float damage) => IsHeavyDamage(damage) ? heavyKnockbackDuration : knockbackDuration;
    private bool IsHeavyDamage(float damage) => damage / maxHp > heavyDamageThreshold;
    
}
