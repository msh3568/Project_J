using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CapsuleCollider2D))]
[RequireComponent(typeof(TrailRenderer))]
public class LatencyCapsuleProjectile : MonoBehaviour, IParryable
{
    [Header("Projectile Settings")]
    [SerializeField] private float moveSpeed = 12f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private Color projectileColor = Color.red;
    [SerializeField] private float parrySuccessSlowDuration = 5.0f; // As per request

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private TrailRenderer trailRenderer;
    private CapsuleCollider2D capsuleCollider;

    private bool isParried = false;
    private GameObject ownerDrone; // To prevent hitting the drone that fired it initially

    // Hook for global time slow on parry
    public static event System.Action<float> OnParrySuccessGlobalSlow;

    public bool IsParryable => !isParried; // Can only be parried once

    public void Initialize(Vector2 direction, GameObject drone)
    {
        ownerDrone = drone;
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();
        trailRenderer = GetComponent<TrailRenderer>();

        if (rb != null)
        {
            rb.gravityScale = 0; // Projectile shouldn't be affected by gravity
            rb.linearVelocity = direction.normalized * moveSpeed;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = projectileColor;
            // Make it look like a capsule
            spriteRenderer.drawMode = SpriteDrawMode.Simple;
            // Set some default size for a capsule look, will be adjusted in Unity Editor
            spriteRenderer.size = new Vector2(0.2f, 0.8f); 
        }

        if (capsuleCollider != null)
        {
            capsuleCollider.isTrigger = true;
            // Adjust collider size/direction to match capsule visual
            capsuleCollider.direction = CapsuleDirection2D.Vertical; // Assuming vertical capsule
            capsuleCollider.size = new Vector2(0.2f, 0.8f);
        }

        if (trailRenderer != null)
        {
            trailRenderer.enabled = true;
            trailRenderer.startColor = projectileColor;
            trailRenderer.endColor = new Color(projectileColor.r, projectileColor.g, projectileColor.b, 0); // Fade out
            trailRenderer.time = 0.3f; // Short trail
            trailRenderer.startWidth = 0.2f;
            trailRenderer.endWidth = 0;
        }
        
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isParried)
        {
            // If parried, only hit drones
            LatencyDroneWeak drone = other.GetComponent<LatencyDroneWeak>();
            if (drone != null && drone.gameObject == ownerDrone) // Ensure it's the *original* drone that fired it
            {
                drone.TakeDamage(1); // Instant kill for weak drone
                Destroy(gameObject);
            }
        }
        else
        {
            // Not parried, hit player
            if (other.CompareTag("Player")) // Assuming player has "Player" tag
            {
                LatencyDebuffReceiver debuffReceiver = other.GetComponent<LatencyDebuffReceiver>();
                if (debuffReceiver != null)
                {
                    debuffReceiver.ApplyDebuff();
                    // Implement hit feedback (particle, sfx) via event or direct call if player has component
                    // For now, log and assume player has a way to handle feedback
                    Debug.Log("Player hit by Latency Projectile! Applying debuff.");

                    // Armor reduction hook
                    // Check if player has IArmor component
                    IDamageable playerDamageable = other.GetComponent<IDamageable>();
                    if (playerDamageable != null)
                    {
                        // Assuming IArmor would be part of IDamageable or a separate component
                        // For now, just call playerDamageable.TakeDamage which will reduce armor if implemented
                        playerDamageable.TakeDamage(1); // Reduce armor by 1 or equivalent damage
                    }
                }
                Destroy(gameObject);
            }
        }
    }

    public void OnParried(Vector2 reflectDir)
    {
        if (!isParried)
        {
            isParried = true;
            Debug.Log("Projectile parried! Reflecting...");

            if (rb != null)
            {
                rb.linearVelocity = reflectDir.normalized * moveSpeed * 1.5f; // Reflect with a bit more speed
            }

            // Trigger global slow
            OnParrySuccessGlobalSlow?.Invoke(parrySuccessSlowDuration);

            // Change color to indicate parried state? (Optional)
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.cyan; // Example color for parried
            }
        }
    }
}
