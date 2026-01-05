using UnityEngine;
using System.Collections; // For Coroutines

public class LatencyDroneWeak : MonoBehaviour, IDamageable
{
    [Header("Drone Settings")]
    [SerializeField] private float health = 1f; // Drone HP: 1
    [SerializeField] private float movementSpeed = 3f;
    [SerializeField] private float detectionRange = 10f; // Default 10 units if camera based calculation is hard
    [SerializeField] private float stopDistance = 3f; // Distance from player to stop moving and start firing

    [Header("Hovering Effect")]
    [SerializeField] private float hoverAmplitude = 0.2f; // How high it floats up and down
    [SerializeField] private float hoverFrequency = 1f; // How fast it floats up and down
    [SerializeField] private float hoverOffset = 0f; // A random offset to make multiple drones hover asynchronously

    [Header("Firing Settings (Pattern A - Single Shot)")]
    [SerializeField] private LatencyCapsuleProjectile projectilePrefab;
    [SerializeField] private Transform firePoint; // Where projectiles are spawned
    [SerializeField] private float fireCooldown = 1.6f;
    [SerializeField] private float projectileSpawnOffset = 0.5f; // Offset from firePoint to prevent self-collision
    [SerializeField] private float recoilForce = 5f; // Force of recoil when firing
    [SerializeField] private float recoilDuration = 0.1f; // How long the recoil force is applied

    [Header("Destruction Settings")]
    [SerializeField] private GameObject fragmentPrefab; // This prefab should have Fragment.cs attached
    [SerializeField] private int explosionFragmentCount = 30;
    [SerializeField] private float explosionFragmentForce = 150f;
    [SerializeField] private float explosionFragmentLifetime = 1.5f; // Default from SimpleExplosion
    [SerializeField] private float explosionFragmentFadeDelay = 1.0f; // Default from SimpleExplosion

    private Transform playerTransform;
    private Rigidbody2D rb;
    private SpriteRenderer sr; // Reference to the SpriteRenderer for flipping
    private float nextFireTime;
    private bool isDead = false;
    private float hoverBaseY; // Store the base Y position for hovering

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0; // Drones usually float
            rb.freezeRotation = true;
        }

        // Setup for Circle Sprite representation
        sr = GetComponent<SpriteRenderer>(); // Assign sr in Awake
        if (sr == null)
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
            // Assign a circular sprite in the editor. A default white circle sprite can be used.
            sr.color = Color.gray; // Example drone color
        }

        CircleCollider2D collider = GetComponent<CircleCollider2D>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f; // Adjust as needed
            collider.isTrigger = false; // Solid collider for drone
        }

        // Ensure SimpleExplosion script is present in project for destruction to work
        // It's not added here, but referenced later.
        // It expects a fragmentPrefab which itself needs Fragment.cs
    }

    void Start()
    {
        // Find player. In a real game, this would likely be managed by a GameManager or ObjectPool.
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            playerTransform = playerGO.transform;
        }
        else
        {
            Debug.LogWarning("Player not found with tag 'Player'. LatencyDroneWeak will not move or fire.");
            enabled = false; // Disable script if no player
            return;
        }

        hoverBaseY = transform.position.y; // Initialize hoverBaseY
        hoverOffset = Random.Range(0f, 2f * Mathf.PI); // Randomize hover start for asynchronous movement
        nextFireTime = Time.time + fireCooldown; // Initial delay before first shot
    }

    void Update()
    {
        if (isDead || playerTransform == null) return;

        // Player Detection and Movement
        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer < detectionRange)
        {
            Vector2 directionToPlayer = (playerTransform.position - transform.position).normalized;

            // --- Flipping Logic ---
            Vector3 currentScale = transform.localScale;
            if (directionToPlayer.x < 0) // Player is to the left
            {
                transform.localScale = new Vector3(Mathf.Abs(currentScale.x), currentScale.y, currentScale.z); // Face right (positive scale)
            }
            else if (directionToPlayer.x > 0) // Player is to the right
            {
                transform.localScale = new Vector3(-Mathf.Abs(currentScale.x), currentScale.y, currentScale.z); // Face left (negative scale)
            }
            // --- End Flipping Logic ---

            if (distanceToPlayer > stopDistance)
            {
                // Move towards player
                rb.linearVelocity = directionToPlayer * movementSpeed;
                // Update hoverBaseY to follow drone's actual Y position while moving
                hoverBaseY = transform.position.y;
            }
            else // Drone is stationary (within stopDistance)
            {
                // Stop moving
                rb.linearVelocity = Vector2.zero;
            }

            // --- Hovering Effect (Simplified and applied only when stationary) ---
            // Only apply hovering if not moving actively
            if (rb.linearVelocity == Vector2.zero && !isDead)
            {
                float targetHoverY = hoverBaseY + Mathf.Sin(Time.time * hoverFrequency + hoverOffset) * hoverAmplitude;
                transform.position = new Vector3(transform.position.x, targetHoverY, transform.position.z);
            }
            // --- End Hovering Effect ---

            // Firing Logic
            if (Time.time >= nextFireTime)
            {
                FireProjectile(directionToPlayer);
                nextFireTime = Time.time + fireCooldown;
            }
        }
        else // Player out of detection range
        {
            rb.linearVelocity = Vector2.zero; // Stop moving
            // Apply hovering when out of range and stationary
            if (!isDead)
            {
                float targetHoverY = hoverBaseY + Mathf.Sin(Time.time * hoverFrequency + hoverOffset) * hoverAmplitude;
                transform.position = new Vector3(transform.position.x, targetHoverY, transform.position.z);
            }
        }
    }

    void FireProjectile(Vector2 direction)
    {
        if (projectilePrefab == null || firePoint == null)
        {
            Debug.LogError("Projectile Prefab or Fire Point is not assigned for LatencyDroneWeak.");
            return;
        }

        // Calculate spawn position slightly offset from firePoint in the firing direction
        Vector3 spawnPosition = firePoint.position + (Vector3)direction.normalized * projectileSpawnOffset;

        LatencyCapsuleProjectile newProjectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        newProjectile.Initialize(direction, transform);

        // --- Recoil Effect ---
        StartCoroutine(ApplyRecoil(-direction.normalized));
        // --- End Recoil Effect ---
    }

    private IEnumerator ApplyRecoil(Vector2 recoilDirection)
    {
        float timer = 0f;
        while (timer < recoilDuration)
        {
            // Apply recoil force continuously over the duration
            rb.AddForce(recoilDirection * recoilForce * Time.deltaTime, ForceMode2D.Force);
            timer += Time.deltaTime;
            yield return null;
        }
    }

    public void TakeDamage(float damage, Transform damageSource)
    {
        if (isDead) return;

        health -= damage;
        Debug.Log($"[Drone Damage] Drone took {damage} damage from {damageSource.name}. Remaining HP: {health}");

        if (health <= 0)
        {
            isDead = true;
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("[Drone Destruction] Latency Drone is dying!");
        // Stop all movement and firing
        rb.linearVelocity = Vector2.zero;
        enabled = false; // Disable this script to stop further updates

        // --- Explosion Effect Integration (reusing SimpleExplosion logic) ---
        // Create an empty GameObject to host the explosion effect
        GameObject explosionEffect = new GameObject("DroneExplosionEffect");
        explosionEffect.transform.position = transform.position;

        // Add the SimpleExplosion script and configure it
        // Ensure SimpleExplosion.cs is in your project and compiled.
        SimpleExplosion explosion = explosionEffect.AddComponent<SimpleExplosion>();
        if (explosion != null)
        {
            explosion.fragmentPrefab = this.fragmentPrefab; // This prefab should have Fragment.cs
            explosion.fragmentCount = explosionFragmentCount;
            explosion.explosionForce = explosionFragmentForce;
            explosion.fragmentColor = Color.grey; // Default color for drone fragments
            explosion.fragmentLifetime = explosionFragmentLifetime;
            explosion.fragmentFadeDelay = explosionFragmentFadeDelay;
        }
        else
        {
            Debug.LogError("SimpleExplosion component not found on ExplosionEffect GameObject! Make sure SimpleExplosion.cs is in a compiled folder (e.g., Assets/Scripts).");
        }

        Destroy(gameObject); // Destroy the drone itself
    }
}