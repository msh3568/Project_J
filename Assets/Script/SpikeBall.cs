using UnityEngine;

public class SpikeBall : MonoBehaviour
{
    [Header("Spike Ball Settings")]
    public float lifetime = 5f;
    public float immobilizationDuration = 2f;

    [Header("Sound")]
    public SoundEffect hitSound;

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
                player.PlaySound(hitSound);
                player.Immobilize(immobilizationDuration);
            }
            Destroy(gameObject);
        }
        else if (other.gameObject.CompareTag("Ground"))
        {
            Destroy(gameObject);
        }
    }
}
