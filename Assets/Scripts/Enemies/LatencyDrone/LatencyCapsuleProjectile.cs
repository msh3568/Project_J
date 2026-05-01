using UnityEngine;
using System.Collections;

public class LatencyCapsuleProjectile : MonoBehaviour, IParryable
{
    [Header("Projectile Settings")]
    [SerializeField] private float damageToPlayer = 1f;
    [SerializeField] private bool useFirewallDamage = false;
    [SerializeField] private int firewallDamage = 1;
    [SerializeField] public float projectileSpeed = 12f;
    [SerializeField] private float parriedSpeedMultiplier = 4f;
    [SerializeField] private Color projectileColor = Color.red;
    [SerializeField] private float trailTime = 0.5f;
    [SerializeField] private float trailStartWidth = 0.1f;
    [SerializeField] private Material trailMaterial;

    [Header("Muzzle Flash Settings")]
    [SerializeField] private float flashDuration = 0.05f;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private Vector3 flashScaleMultiplier = new Vector3(1.5f, 1.5f, 1f);

    [Header("Parry Hit Detection")]
    [SerializeField] private float hitRadius = 0.5f; // Keep hitRadius for OverlapCircle
    [SerializeField] private LayerMask whatIsTarget; // NEW: LayerMask for OverlapCircle

    [Header("Parry Auto Return")]
    [SerializeField, Min(0.05f)] private float autoReturnImpactRadius = 0.35f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private TrailRenderer trailRenderer;
    private Collider2D cachedCollider;
    private Transform autoReturnTarget;
    private bool isAutoReturningToSource;
    
    void Update()
    {
        if (isAutoReturningToSource)
        {
            UpdateAutoReturnToSource();
            return;
        }

        if (isParried && GetComponent<Collider2D>().enabled)
        {
            // Active hit detection using whatIsTarget LayerMask
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, hitRadius, whatIsTarget); // Using whatIsTarget
            foreach (var hit in hits)
            {
                // Only damage if it's not the player and it's damageable
                if (!hit.CompareTag("Player"))
                {
                    if (DamageableLookup.TryGetDamageable(hit, out IDamageable damageable) && damageSource != null)
                    {
                        Debug.Log($"[LatencyCapsule Active] Parried projectile hitting '{hit.name}'. Dealing 1000 damage from source '{damageSource.name}'.");
                        damageable.TakeDamage(1000f, damageSource);
                        GameManager.Instance?.RequestHitSlowMoAndShake();
                        
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

    private bool isParried = false;
    private Transform originalDroneTransform;
    private Transform damageSource; // To store who parried us

    public GameObject GetGameObject() => gameObject;
    public float GetProjectileSpeed() => projectileSpeed;
    public float GetParriedSpeedMultiplier() => parriedSpeedMultiplier;
    public bool CanAutoReturnToSource => originalDroneTransform != null;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.color = projectileColor;
        }

        CapsuleCollider2D collider = GetComponent<CapsuleCollider2D>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.2f, 0.5f);
            collider.direction = CapsuleDirection2D.Vertical;
            collider.isTrigger = false;
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
            // This is a common pattern for default material if none is assigned
                trailRenderer.material = new Material(Shader.Find("Sprites/Default")); 
            }
            trailRenderer.startColor = projectileColor;
            trailRenderer.endColor = new Color(projectileColor.r, projectileColor.g, projectileColor.b, 0);
        }

        cachedCollider = GetComponent<Collider2D>();
    }

    public void Initialize(Vector2 direction, Transform droneTransform)
    {
        originalDroneTransform = droneTransform;
        rb.linearVelocity = direction.normalized * projectileSpeed;
        StartCoroutine(MuzzleFlashEffect());
    }

    public void ConfigureImpactMode(bool shouldUseFirewallDamage, int firewallDamageAmount)
    {
        useFirewallDamage = shouldUseFirewallDamage;
        firewallDamage = Mathf.Max(1, firewallDamageAmount);
    }

    private IEnumerator MuzzleFlashEffect()
    {
        if (spriteRenderer == null) yield break;

        Vector3 originalScale = transform.localScale;
        Color originalColor = spriteRenderer.color;

        spriteRenderer.color = flashColor;
        transform.localScale = originalScale * flashScaleMultiplier.x;

        yield return new WaitForSeconds(flashDuration);

        spriteRenderer.color = originalColor;
        transform.localScale = originalScale;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        HandleCollision(collision.gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        HandleCollision(other != null ? other.gameObject : null);
    }

    private void HandleCollision(GameObject other)
    {
        if (other == null) return;

        // If parried, only care about hitting the ground
        if (isParried)
        {
            if (other.CompareTag("Ground") || other.CompareTag("Wall"))
            {
                Destroy(gameObject);
            }
            return;
        }

        // --- Original logic for when not parried ---

        // Don't collide with the drone that fired it
        if (originalDroneTransform != null && other.gameObject == originalDroneTransform.gameObject)
        {
            Destroy(gameObject);
            return;
        }
        
        if (other.CompareTag("Player"))
        {
            if (useFirewallDamage)
            {
                IFirewallDamageable firewall = other.GetComponent<IFirewallDamageable>();
                if (firewall != null)
                {
                    firewall.TakeFirewallDamage(firewallDamage);
                }
                else
                {
                    Player_Health playerHealth = other.GetComponent<Player_Health>();
                    if (playerHealth != null)
                    {
                        playerHealth.TakeDamage(Mathf.Max(1f, damageToPlayer), transform);
                    }
                }
            }
            else
            {
                Player_Health playerHealth = other.GetComponent<Player_Health>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damageToPlayer, transform);
                }

                LatencyDebuffReceiver debuffReceiver = other.GetComponent<LatencyDebuffReceiver>();
                if (debuffReceiver != null)
                {
                    debuffReceiver.ApplyDebuff();
                    IArmor playerArmor = other.GetComponent<IArmor>();
                    debuffReceiver.OnHitReduceArmor(playerArmor);
                }
            }

            Destroy(gameObject);
        }
        else if (other.CompareTag("Ground") || other.CompareTag("Wall"))
        {
            Destroy(gameObject);
        }
    }
    
    public void SetParriedState(bool parried)
    {
        if (this.isParried == parried) return;

        this.isParried = parried;
        isAutoReturningToSource = false;
        autoReturnTarget = null;
        if (cachedCollider != null)
            cachedCollider.isTrigger = parried;

        if (parried)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
            Debug.Log("Projectile state set to PARRIED.");
        }
    }

    public void LaunchParried(Vector2 direction, Transform playerTransform)
    {
        if (!isParried) return;
        
        this.damageSource = playerTransform; // Store the player as the damage source

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.linearVelocity = direction.normalized * projectileSpeed * parriedSpeedMultiplier;
        gameObject.layer = LayerMask.NameToLayer("PlayerProjectile");
        Debug.Log($"Projectile LAUNCHED by player in direction {direction}.");

        Destroy(gameObject, 5f);
    }

    public bool TryLaunchParriedToSource(Transform playerTransform)
    {
        if (!isParried || originalDroneTransform == null)
            return false;

        damageSource = playerTransform;
        autoReturnTarget = originalDroneTransform;
        isAutoReturningToSource = true;
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        gameObject.layer = LayerMask.NameToLayer("PlayerProjectile");
        return true;
    }

    private void UpdateAutoReturnToSource()
    {
        if (autoReturnTarget == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector2 targetPosition = autoReturnTarget.position;
        Vector2 nextPosition = Vector2.MoveTowards(
            transform.position,
            targetPosition,
            projectileSpeed * parriedSpeedMultiplier * Time.deltaTime);

        transform.position = nextPosition;

        if (((Vector2)transform.position - targetPosition).sqrMagnitude <= autoReturnImpactRadius * autoReturnImpactRadius)
        {
            ExplodeOnAutoReturnTarget();
        }
    }

    private void ExplodeOnAutoReturnTarget()
    {
        if (autoReturnTarget == null)
        {
            Destroy(gameObject);
            return;
        }

        IDamageable damageable = autoReturnTarget.GetComponentInParent<IDamageable>();
        if (damageable != null && damageSource != null)
        {
            damageable.TakeDamage(1000f, damageSource);
            GameManager.Instance?.RequestHitSlowMoAndShake();
        }

        if (cachedCollider != null)
            cachedCollider.enabled = false;

        Destroy(gameObject);
    }
}
