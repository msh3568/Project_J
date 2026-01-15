using UnityEngine;

public class FirewallDamageAdapter : MonoBehaviour, IFirewallDamageable
{
    [SerializeField] private int firewallPerHit = 1;

    public void TakeFirewallDamage(int amount)
    {
        int applied = Mathf.Max(1, amount);
        IArmor armor = GetComponent<IArmor>();
        if (armor != null && armor.HasArmor())
        {
            armor.ReduceArmor(applied);
            return;
        }

        Player_Health health = GetComponent<Player_Health>();
        if (health != null)
        {
            health.TakeDamage(applied, transform);
        }
    }
}
