using UnityEngine;
using System.Collections; // For IEnumerator

public class LatencyCapsuleProjectile : MonoBehaviour, IParryable
{
    [Header("Projectile Settings")]
    [SerializeField] private float damageToPlayer = 1f; // 플레이어에게 입힐 데미지
    public float projectileSpeed = 12f;
    [SerializeField] private float parriedSpeedMultiplier = 1.5f; // Faster when parried
    [SerializeField] private Color projectileColor = Color.red;
    [SerializeField] private float trailTime = 0.5f;
    [SerializeField] private float trailStartWidth = 0.1f;
    [SerializeField] private Material trailMaterial; // Assign a default material like Sprites-Default. If null, a default will be used if possible.

    [Header("Muzzle Flash Settings")]
    [SerializeField] private float flashDuration = 0.05f; // Short flash
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private Vector3 flashScaleMultiplier = new Vector3(1.5f, 1.5f, 1f);

    [Header("Parry Aiming Settings")]
    [SerializeField] private float slow_duration = 5.0f; // From user request
    [SerializeField] private float slow_scale = 0.3f; // Default from ParriableProjectile
    [SerializeField] private float aimSweepSpeed = 2.0f;
    [SerializeField] private int trajectoryPointCount = 50;
    [SerializeField] private float trajectoryPointSpacing = 0.1f;
    [SerializeField] private Material trajectoryLineMaterial; // Assign a material for the line renderer

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private TrailRenderer trailRenderer;
    private LineRenderer lineRenderer; // For aiming trajectory
    private bool isParried = false;
    private Transform originalDroneTransform; // To target when parried
    private Player player; // Reference to the player for aiming input
    private Vector2 lastAimDirection; // Stores the direction player aimed

    public GameObject GetGameObject() => gameObject; // Implementation for IParryable

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0; // Projectiles usually aren't affected by gravity
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // Better for fast-moving objects
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            // A placeholder sprite should be assigned in the editor for visibility
            // For a capsule shape, use a small white square sprite and scale it
            spriteRenderer.color = projectileColor;
        }

        CapsuleCollider2D collider = GetComponent<CapsuleCollider2D>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.2f, 0.5f); // Example size, adjust as needed
            collider.direction = CapsuleDirection2D.Vertical; // Vertical capsule shape
            collider.isTrigger = false; // Projectile is now a solid collider, like SpikeBall.cs
        }

        trailRenderer = GetComponent<TrailRenderer>();
        if (trailRenderer == null)
        {
            trailRenderer = gameObject.AddComponent<TrailRenderer>();
            trailRenderer.time = trailTime;
            trailRenderer.startWidth = trailStartWidth;
            trailRenderer.endWidth = 0f;
            if (trailMaterial != null)
            {
                trailRenderer.material = trailMaterial;
            }
            else
            {
                // Attempt to use a default material if none is assigned
                trailRenderer.material = new Material(Shader.Find("Sprites/Default"));
            }
            trailRenderer.startColor = projectileColor;
            trailRenderer.endColor = new Color(projectileColor.r, projectileColor.g, projectileColor.b, 0); // Fades to transparent
        }

        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.enabled = false; // Start disabled
            lineRenderer.positionCount = 0;
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;
            if (trajectoryLineMaterial != null)
            {
                lineRenderer.material = trajectoryLineMaterial;
            }
            else
            {
                // Fallback material for trajectory line
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            }
            lineRenderer.startColor = Color.yellow;
            lineRenderer.endColor = new Color(Color.yellow.r, Color.yellow.g, Color.yellow.b, 0);
        }
    }

    void Start()
    {
        player = FindObjectOfType<Player>(); // Get player reference
        if (player == null)
        {
            Debug.LogError("Player not found in scene for LatencyCapsuleProjectile aiming!");
        }
    }

    public void Initialize(Vector2 direction, Transform droneTransform)
    {
        originalDroneTransform = droneTransform;
        rb.linearVelocity = direction.normalized * projectileSpeed;
        StartCoroutine(MuzzleFlashEffect());
    }

    private IEnumerator MuzzleFlashEffect()
    {
        if (spriteRenderer == null) yield break; // Safety check

        Vector3 originalScale = transform.localScale;
        Color originalColor = spriteRenderer.color;

        // Apply flash
        spriteRenderer.color = flashColor;
        transform.localScale = originalScale * flashScaleMultiplier.x; 

        yield return new WaitForSeconds(flashDuration);

        // Revert
        spriteRenderer.color = originalColor; // Revert to original color (which is projectileColor)
        transform.localScale = originalScale; // Revert to original scale
    }
    
    // Using OnCollisionEnter2D because isTrigger is false, similar to SpikeBall.cs
    void OnCollisionEnter2D(Collision2D collision)
    {
        HandleCollision(collision.gameObject);
    }

    private void HandleCollision(GameObject other)
    {
        // Projectile disappears on hitting ground or player, or if parried and hits IDamagable
        if (other == null) return; // Safety check

        // Check if unparried projectile hits its own original drone
        if (!isParried && originalDroneTransform != null && other.gameObject == originalDroneTransform.gameObject)
        {
            Debug.Log("[Projectile Collision] Unparried projectile hit its own drone. Disappearing.");
            Destroy(gameObject); // Disappear without damage
            return;
        }

        if (other.CompareTag("Ground")) // Assuming "Ground" tag for ground objects
        {
            Destroy(gameObject);
        }
        else if (other.CompareTag("Player")) // Assuming "Player" tag for player object
        {
            // If the projectile is ALREADY parried, it should NOT affect the player.
            // It should ONLY affect the player if it's an unparried drone projectile.
            if (isParried)
            {
                // Debug.Log("[Projectile Collision] Parried projectile passed through player (as intended).");
                // Do nothing to player, and DO NOT destroy projectile yet if it's parried.
                // It's supposed to continue to the drone.
                return; // Important: Exit so it doesn't run the rest of the player damage or self-destroy logic.
            }
            else // If NOT parried, then it's a regular drone shot hitting the player
            {
                Player_Health playerHealth = other.GetComponent<Player_Health>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damageToPlayer, transform); // 플레이어 쉴드/체력 감소
                }

                // Apply debuff and reduce armor (기존 로직 유지)
                LatencyDebuffReceiver debuffReceiver = other.GetComponent<LatencyDebuffReceiver>();
                if (debuffReceiver != null)
                {
                    debuffReceiver.ApplyDebuff();
                    IArmor playerArmor = other.GetComponent<IArmor>();
                    debuffReceiver.OnHitReduceArmor(playerArmor);
                }
                // Projectile is destroyed after hitting unparried player
                Destroy(gameObject);
            }
        }
        else if (isParried) // If it's parried, it's now dangerous to enemies and should hit IDamagable objects
        {
            // Debugging: Confirm parried projectile hits something
            Debug.Log($"[Projectile Collision] Parried projectile hit: {other.name}, Tag: {other.tag}");

            IDamageable damagableObject = other.GetComponent<IDamageable>();
            // If it hits an IDamagable object AND it's not the player (to prevent damaging self with parried projectile)
            if (damagableObject != null && !other.CompareTag("Player"))
            {
                Debug.Log($"[Projectile Collision] Parried projectile hit IDamagable: {other.name}. Calling TakeDamage (1000f).");
                damagableObject.TakeDamage(1000f, transform); // Instant kill anything IDamagable
                Destroy(gameObject); // Projectile is spent after hitting a target
                return; // Exit after dealing damage and destroying
            }
            // If it hits something that is not IDamagable (e.g., environment) OR hits the player
            // For general environment objects after parrying, it should also be destroyed to not fly indefinitely.
            Destroy(gameObject); // Projectile is spent
        }
    }

    public void OnParried(Vector2 reflectDirection)
    {
        if (isParried) return; // Already parried
        if (player == null) // Safety check for player reference
        {
            Debug.LogError("Player not found in scene for LatencyCapsuleProjectile aiming!");
            isParried = true; // Mark as parried to prevent re-parry
            rb.linearVelocity = reflectDirection.normalized * projectileSpeed * parriedSpeedMultiplier; // Just reflect immediately
            GameManager.Instance.RequestSlowMotion(slow_scale, slow_duration); // Still apply slow motion
            GameManager.Instance.EndSlowMotion(); // End it immediately since no aiming
            return;
        }

        isParried = true;
        rb.linearVelocity = Vector2.zero; // Pause movement
        rb.isKinematic = true; // Make it kinematic during aiming
        
        GameManager.Instance.RequestSlowMotion(slow_scale, slow_duration);
        StartCoroutine(AimAndFireSequence());

        Debug.Log("Projectile parried! Aiming sequence started.");
    }

    private IEnumerator AimAndFireSequence()
    {
        if (lineRenderer != null)
        {
            lineRenderer.enabled = true;
        }

        while (player != null && player.IsCounterAttackBeingHeld()) // Check player's counter attack input
        {
            UpdateTrajectory();
            yield return null;
        }

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
        FireAimedShot();
    }

    private void UpdateTrajectory()
    {
        if (player == null) return;

        // Calculate oscillating angle based on player facing direction
        float angle_0_to_1 = (Mathf.Sin(Time.time * aimSweepSpeed) + 1) / 2.0f; // Oscillates 0..1
        float targetAngle;

        // Assuming player.facingDir is -1 for left, 1 for right
        if (player.facingDir > 0) // Facing right
        {
            targetAngle = Mathf.Lerp(90, 0, angle_0_to_1); // Sweep between Up (90) and Forward (0)
        }
        else // Facing left
        {
            targetAngle = Mathf.Lerp(90, 180, angle_0_to_1); // Sweep between Up (90) and Forward (180)
        }

        lastAimDirection = new Vector2(Mathf.Cos(targetAngle * Mathf.Deg2Rad), Mathf.Sin(targetAngle * Mathf.Deg2Rad));

        DrawParabolicArc(lastAimDirection * projectileSpeed * parriedSpeedMultiplier);
    }

    private void DrawParabolicArc(Vector2 initialVelocity)
    {
        if (lineRenderer == null || !lineRenderer.enabled) return;
        
        lineRenderer.positionCount = trajectoryPointCount;
        Vector2 startPos = transform.position;
        // Assuming negligible gravity during parry slow-motion or for projectile in 2D
        // Use projectile's own gravity scale if any, but default is 0 for this projectile
        Vector2 gravity = new Vector2(0, Physics2D.gravity.y * rb.gravityScale); 

        for (int i = 0; i < trajectoryPointCount; i++)
        {
            float t = i * trajectoryPointSpacing;
            Vector2 currentPos = startPos + initialVelocity * t + 0.5f * gravity * t * t;
            lineRenderer.SetPosition(i, currentPos);
        }
    }

    private void FireAimedShot()
    {
        if (!rb.isKinematic) return; // Should be kinematic from aiming phase

        rb.isKinematic = false; // Make dynamic again
        rb.linearVelocity = lastAimDirection * projectileSpeed * parriedSpeedMultiplier;
        GameManager.Instance.EndSlowMotion(); // End slow motion

        // No layer change here, it remains "Projectile" for colliding with "Enemy"

        Destroy(gameObject, 5f); // Destroy after a certain duration
    }
}