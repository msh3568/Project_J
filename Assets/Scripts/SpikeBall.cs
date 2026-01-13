using UnityEngine;

public class SpikeBall : MonoBehaviour, IParryable
{
    [Header("Spike Ball Settings")]
    public float lifetime = 5f;
    public float immobilizationDuration = 2f;
    public float knockbackForce = 10f; // Used for hitting the player
    public float damage = 1f;
    
    [Header("Parry Settings")]
    public float speed = 10f; // Used for IParryable
    public float parriedSpeedMultiplier = 4f;
    public float parriedDamage = 1000f;
    [SerializeField] private float hitRadius = 0.5f; // Keep hitRadius for OverlapCircle
    [SerializeField] private LayerMask whatIsTarget; // NEW: LayerMask for OverlapCircle

    public bool isParried = false; // Now public for temporary fix, will revert later
    private Rigidbody2D rb;
    private Transform damageSource; // To store who parried us

    #region IParryable Implementation
    public GameObject GetGameObject() => gameObject;
    public float GetProjectileSpeed() => speed;
    public float GetParriedSpeedMultiplier() => parriedSpeedMultiplier;

    public void SetParriedState(bool parried)
    {
        if (this.isParried == parried) return;

        this.isParried = parried;
        if (parried)
        {
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            rb.linearVelocity = Vector2.zero;
            rb.isKinematic = true;
        }
    }

    public void LaunchParried(Vector2 direction, Transform playerTransform)
    {
        if (!isParried) return;

        this.damageSource = playerTransform; // Store the player as the damage source

        if (rb == null) rb = GetComponent<Rigidbody2D>();
        rb.isKinematic = false;
        rb.linearVelocity = direction.normalized * speed * parriedSpeedMultiplier;
        gameObject.layer = LayerMask.NameToLayer("PlayerProjectile");

        // Make sure it doesn't get destroyed by its original lifetime timer after being parried
        Destroy(gameObject, lifetime * 2); 
    }
    #endregion

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0;
        }
    }

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (isParried && GetComponent<Collider2D>().enabled)
        {
            // Active hit detection using whatIsTarget LayerMask
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, hitRadius, whatIsTarget); // Using whatIsTarget
            foreach (var hit in hits)
            {
                // Only damage if it's not the player and it's damageable
                if (!hit.CompareTag("Player"))
                {
                    IDamageable damageable = hit.GetComponent<IDamageable>();
                    if (damageable != null && damageSource != null)
                    {
                        Debug.Log($"[SpikeBall Active] Parried projectile hitting '{hit.name}'. Dealing {parriedDamage} damage from source '{damageSource.name}'.");
                        damageable.TakeDamage(parriedDamage, damageSource);
                        
                        // Disable own collider to prevent hitting multiple times
                        GetComponent<Collider2D>().enabled = false;
                        
                        // Destroy after a delay
                        Destroy(gameObject, 0.5f);
                        
                        // Stop checking after the first hit
                        return; 
                    }
                }
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        // This method now only handles non-parried collisions, and ground collision for parried projectiles
        if (isParried)
        {
            // If a parried projectile hits the ground, destroy it
            if (other.gameObject.CompareTag("Ground"))
            {
                Destroy(gameObject);
            }
            return;
        }

        // --- Original logic for when not parried ---
        HandleNormalCollision(other.gameObject);
    }

    private void HandleNormalCollision(GameObject other)
    {
        if (other.CompareTag("Player"))
        {
            Player player = other.GetComponent<Player>();
            if (player != null)
            {
                Player_Health playerHealth = player.GetComponent<Player_Health>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damage, transform);
                }

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
