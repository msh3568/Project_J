using UnityEngine;

public class ProjectileDamager : MonoBehaviour
{
    public float damage;

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (DamageableLookup.TryGetDamageable(other, out IDamageable enemy))
        {
            enemy.TakeDamage(damage, transform);
            Destroy(gameObject);
        }
        else if (other.gameObject.CompareTag("Ground"))
        {
            Destroy(gameObject);
        }
    }
}
