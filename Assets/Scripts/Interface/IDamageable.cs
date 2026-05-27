using UnityEngine;

public interface IDamageable
{
    public void TakeDamage(float damage, Transform damageDealer);
}

public interface IDamageableStatus
{
    bool CanReceiveDamage { get; }
}
