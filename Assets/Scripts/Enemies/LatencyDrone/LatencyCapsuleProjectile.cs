using UnityEngine;
using System.Collections;

public class LatencyCapsuleProjectile : MonoBehaviour, IParryable
{
    [Header("Projectile Settings")]
    [SerializeField] private float damageToPlayer = 1f;
    [SerializeField] public float projectileSpeed = 12f;
    [SerializeField] private float parriedSpeedMultiplier = 1.5f;
    [SerializeField] private Color projectileColor = Color.red;
    [SerializeField] private float trailTime = 0.5f;
    [SerializeField] private float trailStartWidth = 0.1f;
    [SerializeField] private Material trailMaterial;

    [Header("Muzzle Flash Settings")]
    [SerializeField] private float flashDuration = 0.05f;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private Vector3 flashScaleMultiplier = new Vector3(1.5f, 1.5f, 1f);

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private TrailRenderer trailRenderer;
    private bool isParried = false;
    private Transform originalDroneTransform;

    public GameObject GetGameObject() => gameObject;
    public float GetProjectileSpeed() => projectileSpeed;
    public float GetParriedSpeedMultiplier() => parriedSpeedMultiplier;

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
                trailRenderer.material = new Material(Shader.Find("Sprites/Default"));
            }
            trailRenderer.startColor = projectileColor;
            trailRenderer.endColor = new Color(projectileColor.r, projectileColor.g, projectileColor.b, 0);
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

    private void HandleCollision(GameObject other)
    {
        if (other == null) return;

        if (!isParried && originalDroneTransform != null && other.gameObject == originalDroneTransform.gameObject)
        {
            Destroy(gameObject);
            return;
        }

        if (other.CompareTag("Ground"))
        {
            Destroy(gameObject);
        }
        else if (other.CompareTag("Player"))
        {
            if (isParried)
            {
                return; 
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
                Destroy(gameObject);
            }
        }
        else if (isParried)
        {
            IDamageable damagableObject = other.GetComponent<IDamageable>();
            if (damagableObject != null && !other.CompareTag("Player"))
            {
                damagableObject.TakeDamage(1000f, transform);
                Destroy(gameObject);
                return;
            }
            Destroy(gameObject);
        }
    }
    
    public void SetParriedState(bool parried)
    {
        if (this.isParried == parried) return;

        this.isParried = parried;
        if (parried)
        {
            rb.linearVelocity = Vector2.zero;
            rb.isKinematic = true;
            Debug.Log("Projectile state set to PARRIED.");
        }
    }

    public void LaunchParried(Vector2 direction)
    {
        if (!isParried) return;
        
        rb.isKinematic = false;
        rb.linearVelocity = direction.normalized * projectileSpeed * parriedSpeedMultiplier;
        Debug.Log($"Projectile LAUNCHED by player in direction {direction}.");

        Destroy(gameObject, 5f);
    }
}
