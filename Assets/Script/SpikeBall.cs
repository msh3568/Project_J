using UnityEngine;

public class SpikeBall : MonoBehaviour
{
    [Header("Spike Ball Settings")]
    public float lifetime = 5f;
    public float immobilizationDuration = 2f;
    public float knockbackForce = 10f;

    void Start()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0;
        }

        Destroy(gameObject, lifetime);
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            Player player = other.gameObject.GetComponent<Player>();

            if (player != null)
            { 
                if (AnalyticsManager.Instance != null)
                {
                    AnalyticsManager.Instance.LogTrapEvent("SpikeBall", player.transform.position);
                }
                
                Vector2 knockbackDirection = (player.transform.position - transform.position).normalized;
                player.ReciveKnockback(knockbackDirection * knockbackForce, immobilizationDuration);
                
                if (player.hitSound != null && player.hitSound.clip != null)
                {
                    player.PlaySound(player.hitSound);
                }
            }
            Destroy(gameObject);
        }
        else if (other.gameObject.CompareTag("Ground"))
        {
            Destroy(gameObject);
        }
    }
}
