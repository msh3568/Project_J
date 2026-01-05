using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class LatencyDroneWeak : MonoBehaviour, IDamageable
{
    [Header("Drone Settings")]
    [SerializeField] private int currentHP = 1;

    [Header("Detection & Movement")]
    [SerializeField] private float detectionRange = 10f; // Default from user request
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private LayerMask groundLayer; // To prevent passing through ground
    [SerializeField] private float hoverHeight = 1f; // How high above the ground it tries to hover
    [SerializeField] private float hoverForce = 10f; // Force to maintain hover
    [SerializeField] private float playerFollowHeightOffset = 1f; // Offset from player's y position

    [Header("Shooting Settings - Pattern A")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireRate = 1.6f; // Default from user request
    [SerializeField] private float projectileSpeed = 12f; // Default from user request
    [SerializeField] private SpriteRenderer muzzleFlashSprite; // For pre-fire visual
    [SerializeField] private Color muzzleFlashColor = Color.white;
    [SerializeField] private float muzzleFlashDuration = 0.05f; // Short flash
    [SerializeField] private Vector3 muzzleFlashScale = new Vector3(1.5f, 1.5f, 1f);

    [Header("Feedback")]
    [SerializeField] private ParticleSystem hitFeedbackParticles; // Noise/glitch particles
    [SerializeField] private AudioSource hitFeedbackSFX; // Short "삐-" SFX hook

    [Header("Destruction")]
    [SerializeField] private DroneBreakEffectAdapter breakEffectAdapter; // Reference to adapter

    private Transform playerTransform;
    private Rigidbody2D rb;
    private float nextFireTime;
    private bool playerDetected = false;
    private Vector3 initialMuzzleScale;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // Ensure gravity is off for a hovering drone
        if (rb != null)
        {
            rb.gravityScale = 0;
            rb.linearDamping = 5f; // Add some drag for smoother movement
        }

        // Setup for drone visual (circle)
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = Color.gray; // Default drone color
            sr.drawMode = SpriteDrawMode.Simple;
            sr.size = new Vector2(1f, 1f); // Circle size
        }

        CircleCollider2D cc = GetComponent<CircleCollider2D>();
        if (cc != null)
        {
            cc.isTrigger = false; // Collides with player/world
            cc.radius = 0.5f; // Matches circle size
        }

        if (muzzleFlashSprite != null)
        {
            initialMuzzleScale = muzzleFlashSprite.transform.localScale;
            muzzleFlashSprite.enabled = false;
        }

        // Find the player (assuming player has "Player" tag)
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogWarning("Player not found! Ensure player GameObject has 'Player' tag.", this);
        }
    }

    private void Update()
    {
        if (playerTransform == null) return;

        DetectPlayer();

        if (playerDetected)
        {
            MoveTowardsPlayer();
            AttemptFire();
        }
    }

    private void FixedUpdate()
    {
        ApplyHoverForce();
    }

    private void DetectPlayer()
    {
        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        playerDetected = distanceToPlayer <= detectionRange;
    }

    private void MoveTowardsPlayer()
    {
        Vector2 directionToPlayer = (playerTransform.position - transform.position).normalized;
        
        // Horizontal movement
        rb.linearVelocity = new Vector2(directionToPlayer.x * moveSpeed, rb.linearVelocity.y);

        // Vertical movement to follow player's height with an offset
        float targetY = playerTransform.position.y + playerFollowHeightOffset;
        float currentY = transform.position.y;
        float verticalDifference = targetY - currentY;
        
        // Simple proportional control for vertical movement
        float verticalForce = verticalDifference * hoverForce * 0.1f; // Reduced force for smoother follow
        rb.AddForce(new Vector2(0, verticalForce), ForceMode2D.Force);
    }

    private void ApplyHoverForce()
    {
        // Raycast downwards to find ground
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, hoverHeight + 0.1f, groundLayer);

        if (hit.collider != null)
        {
            // If too close to ground, push up
            float distance = Mathf.Abs(hit.point.y - transform.position.y);
            float hoverError = hoverHeight - distance;
            if (hoverError > 0) // If below desired hover height
            {
                rb.AddForce(Vector2.up * hoverForce * hoverError, ForceMode2D.Force);
            }
        }
        else // No ground below, prevent falling too much, but allow some descent
        {
             // Add a slight upward force to counteract natural descent/gravity if there's no ground
             // or to maintain hovering altitude when player is high up.
             rb.AddForce(Vector2.up * hoverForce * 0.1f, ForceMode2D.Force);
        }
    }

    private void AttemptFire()
    {
        if (Time.time > nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            StartCoroutine(PreFireEffectAndShoot());
        }
    }

    private IEnumerator PreFireEffectAndShoot()
    {
        // Muzzle Flash
        if (muzzleFlashSprite != null)
        {
            muzzleFlashSprite.enabled = true;
            muzzleFlashSprite.color = muzzleFlashColor;
            muzzleFlashSprite.transform.localScale = muzzleFlashScale;
        }

        yield return new WaitForSeconds(muzzleFlashDuration);

        if (muzzleFlashSprite != null)
        {
            muzzleFlashSprite.enabled = false;
            muzzleFlashSprite.transform.localScale = initialMuzzleScale;
        }

        ShootProjectile();
    }

    private void ShootProjectile()
    {
        if (projectilePrefab == null || firePoint == null)
        {
            Debug.LogWarning("Projectile Prefab or Fire Point is not set on LatencyDroneWeak.", this);
            return;
        }

        GameObject projectileGO = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        LatencyCapsuleProjectile projectile = projectileGO.GetComponent<LatencyCapsuleProjectile>();

        if (projectile != null)
        {
            Vector2 directionToPlayer = (playerTransform.position - firePoint.position).normalized;
            projectile.Initialize(directionToPlayer, gameObject); // Pass drone itself as owner
        }
        else
        {
            Debug.LogError("Projectile prefab does not have LatencyCapsuleProjectile component!", projectileGO);
        }
    }

    public void TakeDamage(int damage)
    {
        currentHP -= damage;
        Debug.Log($"Drone took {damage} damage. Current HP: {currentHP}");

        // Hit feedback on the drone itself if needed (optional)
        // e.g., brief flash, sound

        if (currentHP <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("Drone destroyed!");
        // Play destruction effect via adapter
        if (breakEffectAdapter != null)
        {
            breakEffectAdapter.transform.position = transform.position; // Ensure effect plays at drone's position
            breakEffectAdapter.PlayBreakEffect();
        }
        else
        {
            Debug.LogWarning("DroneBreakEffectAdapter not assigned to LatencyDroneWeak. No destruction effect will play.", this);
        }
        Destroy(gameObject);
    }

    // Player hit feedback - called by projectile when it hits player
    public void OnPlayerHitFeedback()
    {
        if (hitFeedbackParticles != null)
        {
            hitFeedbackParticles.Play();
        }
        if (hitFeedbackSFX != null)
        {
            hitFeedbackSFX.Play();
        }
    }

    // Helper for visualising detection range in editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
