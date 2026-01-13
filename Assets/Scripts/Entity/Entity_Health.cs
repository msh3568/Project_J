using UnityEngine;
using UnityEngine.UI;

public class Entity_Health : MonoBehaviour, IDamagable, IDamageable
{
    protected Entity_VFX entityVfx;
    protected Entity entity;

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

        Debug.Log($"[TakeDamage] {gameObject.name} received {damage} damage from {damagedealer.name}.");

        Player player = GetComponent<Player>();
        if (player != null)
        {
            player.PlaySound(player.hitSound);

            float damageReduction = (float)player.defense / 100f;
            damage *= (1 - damageReduction);
        }

        if (ShouldShowDamagePopup(damagedealer))
        {
            DamagePopup.Spawn(transform.position, damage);
        }

        ReduceHp(damage);

        // If damage was lethal, Die() is called inside ReduceHp and isDead becomes true.
        // We must not apply knockback to a dead entity.
        if (isDead)
        {
            Debug.Log($"[TakeDamage] {gameObject.name} is dead. Halting TakeDamage execution.");
            return;
        }

        Vector2 knockback = CalculateKnockback(damage, damagedealer);
        float duration = CalculateDuration(damage);

        Debug.Log($"[TakeDamage] Applying knockback to {gameObject.name}.");
        entity?.ReciveKnockback(knockback, duration);

        if (entityVfx != null)
        {
            entityVfx?.PlayOnDamageVfx();
        }
    }


    protected void ReduceHp(float damage)
    {
        float oldHp = currentHp;
        currentHp -= damage;
        Debug.Log($"[ReduceHp] {gameObject.name}'s HP reduced from {oldHp} to {currentHp}.");
        onHealthChanged?.Invoke(currentHp, maxHp);

        if (currentHp <= 0)
        {
            Debug.Log($"[ReduceHp] {gameObject.name}'s HP is at or below zero. Calling Die().");
            Die();
        }
    }

    protected void InvokeOnHealthChanged(float current, float max)
    {
        onHealthChanged?.Invoke(current, max);
    }

    protected virtual void Die()
    {
        Debug.Log($"[Die] {gameObject.name} Die() method called.");
        isDead = true;
        entity.onEntityDeath();
    }

    private bool ShouldShowDamagePopup(Transform damageDealer)
    {
        if (damageDealer == null) return false;
        return damageDealer.GetComponentInParent<Player>() != null;
    }

    protected virtual Vector2 CalculateKnockback(float damage, Transform damagedealer)
    {
        int direction = transform.position.x > damagedealer.position.x ? 1 : -1;
        Vector2 knockback = IsHeavyDamage(damage) ? heavyKnockbackPower : knockbackPower;

        knockback.x = knockback.x * direction;

        return knockback;
    }
    protected virtual float CalculateDuration(float damage) => IsHeavyDamage(damage) ? heavyKnockbackDuration : knockbackDuration;
    protected virtual bool IsHeavyDamage(float damage) => damage / maxHp > heavyDamageThreshold;
    
}
