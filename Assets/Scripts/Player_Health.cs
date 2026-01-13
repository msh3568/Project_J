using UnityEngine;
using System.Collections;
using Unity.Cinemachine;

public class Player_Health : Entity_Health
{
    [SerializeField] public int maxShield = 5;
    public int currentShield;

    [Header("Regeneration")]
    [SerializeField] public float regenerationTime = 10f;
    [SerializeField] public float regenerationDelayAfterHit = 3f;

    private float timeSinceLastHit;
    private float regenerationTimer;
    private int lastLoggedSecond; // New field to track last logged second

    private CinemachineImpulseSource impulseSource;

    public bool IsInvincible { get; set; }
    public bool CanRegenerate { get; private set; }

    private CameraShake cameraShake;
    private SpriteRenderer spriteRenderer;

    protected override void Awake()
    {
        entity = GetComponent<Entity>();
        entityVfx = GetComponent<Entity_VFX>();
        cameraShake = GetComponent<CameraShake>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        currentShield = maxShield;
        IsInvincible = false;
        CanRegenerate = false; // Initialize

        if (spriteRenderer == null)
        {
            Debug.LogError("Player_Health: SpriteRenderer component not found on child objects!");
        }
    }

   
    private void Start()
    {
        InvokeOnHealthChanged(currentShield, maxShield);
        Debug.Log($"Initial shield: {currentShield}");
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    private void Update()
    {
        timeSinceLastHit += Time.deltaTime;

        bool wasCanRegenerate = CanRegenerate;
        CanRegenerate = timeSinceLastHit > regenerationDelayAfterHit && currentShield < maxShield && !isDead;

        if (CanRegenerate && !wasCanRegenerate)
        {
            Debug.Log($"Shield regen ready after {regenerationDelayAfterHit:F2}s. Starting regen.");
            regenerationTimer = 0f; // Reset timer when regeneration starts
            lastLoggedSecond = -1; // Reset for new regeneration cycle
        }

        if (CanRegenerate)
        {
            regenerationTimer += Time.deltaTime;

            int currentSecond = Mathf.FloorToInt(regenerationTimer);
            if (currentSecond > lastLoggedSecond && currentSecond > 0)
            {
                Debug.Log($"Shield regen ticking. Next shield in {regenerationTime - regenerationTimer:F2}s (elapsed {regenerationTimer:F2}s/{regenerationTime:F2}s).");
                lastLoggedSecond = currentSecond;
            }

            if (regenerationTimer >= regenerationTime)
            {
                currentShield++;
                InvokeOnHealthChanged(currentShield, maxShield);
                Debug.Log($"Shield regenerated. Current shield: {currentShield}");
                regenerationTimer = 0f;
                lastLoggedSecond = -1; // Reset for next shield point regeneration
            }
        }
    }

    public override void TakeDamage(float damage, Transform damageDealer)
    {
        if (isDead || IsInvincible) return;

        CameraShakeManager.instance.CamerShake(impulseSource);

        timeSinceLastHit = 0f;
        regenerationTimer = 0f;

        if (currentShield > 0)
        {
            currentShield--;
            InvokeOnHealthChanged(currentShield, maxShield);

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }
            
            entityVfx?.PlayOnDamageVfx();

            if (currentShield > 0)
            {
                Debug.Log($"Hit! Shield remaining: {currentShield}");
            }
            else
            {
                Debug.Log("Shield broken! Next hit will be lethal.");
            }
            
            Player player = GetComponent<Player>();
            if (player != null)
            {
                player.PlaySound(player.hitSound);
            }
            
            Vector2 knockback = CalculateKnockback(damage, damageDealer);
            float duration = CalculateDuration(damage);
            entity?.ReciveKnockback(knockback, duration);
        }
        else
        {
            Die();
        }
    }

    protected override bool IsHeavyDamage(float damage)
    {
        return false;
    }

    protected override void Die()
    {
        if (!isDead)
        {
            Debug.Log("Player has died!");
            base.Die();
        }
    }
}
