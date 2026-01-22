using UnityEngine;
using System.Collections;
using Unity.Cinemachine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

public class Player_Health : Entity_Health
{
    [SerializeField] public int maxShield = 5;
    public int currentShield;

    [Header("Regeneration")]
    [SerializeField] public float regenerationTime = 10f;
    [SerializeField] public float regenerationDelayAfterHit = 3f;
    [SerializeField] private float shieldHitInvulnerability = 1.5f;

    private float timeSinceLastHit;
    private float regenerationTimer;
    private int lastLoggedSecond; // New field to track last logged second
    private float nextShieldDamageTime;

    private CinemachineImpulseSource impulseSource;

    public bool IsInvincible { get; set; }
    public bool CanRegenerate { get; private set; }

    private CameraShake cameraShake;
    private SpriteRenderer spriteRenderer;
    private bool isFirewallRespawning;

    [Header("Firewall Respawn")]
    [SerializeField] private float firewallBlackoutDuration = 0.25f;
    [SerializeField] private Color firewallBlackoutColor = Color.black;
    [SerializeField] private bool freezePlayerDuringRespawn = true;

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
        if (isFirewallRespawning) return;

        CameraShakeManager.instance.CamerShake(impulseSource);

        timeSinceLastHit = 0f;
        regenerationTimer = 0f;

        if (currentShield > 0)
        {
            if (Time.time < nextShieldDamageTime)
            {
                entityVfx?.PlayShieldHitVfx();
                return;
            }

            entityVfx?.PlayShieldHitVfx();
            if (currentShield <= 2)
            {
                entityVfx?.PlayLastShieldHitVfx();
            }
            currentShield--;
            InvokeOnHealthChanged(currentShield, maxShield);
            nextShieldDamageTime = Time.time + shieldHitInvulnerability;

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
            StartCoroutine(FirewallRespawnRoutine());
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

    public void ResetShieldToMax()
    {
        currentShield = maxShield;
        timeSinceLastHit = 0f;
        regenerationTimer = 0f;
        CanRegenerate = false;
        InvokeOnHealthChanged(currentShield, maxShield);
    }

    private IEnumerator FirewallRespawnRoutine()
    {
        isFirewallRespawning = true;
        IsInvincible = true;

        var player = GetComponent<Player>();
        if (freezePlayerDuringRespawn && player != null)
        {
            player.Immobilize(firewallBlackoutDuration);
        }

        var spriteCaches = new List<(SpriteRenderer renderer, Color color)>();
        var sprites = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var sr in sprites)
        {
            if (sr == null) continue;
            if (sr.transform.IsChildOf(transform)) continue;
            spriteCaches.Add((sr, sr.color));
            sr.color = firewallBlackoutColor;
        }

        var tilemapCaches = new List<(Tilemap tilemap, Color color)>();
        var tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var tilemap in tilemaps)
        {
            if (tilemap == null) continue;
            tilemapCaches.Add((tilemap, tilemap.color));
            tilemap.color = firewallBlackoutColor;
        }

        yield return new WaitForSeconds(firewallBlackoutDuration);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RespawnPlayerAtLastCheckpoint();
        }

        foreach (var (renderer, color) in spriteCaches)
        {
            if (renderer != null)
                renderer.color = color;
        }

        foreach (var (tilemap, color) in tilemapCaches)
        {
            if (tilemap != null)
                tilemap.color = color;
        }

        IsInvincible = false;
        isFirewallRespawning = false;
    }
}
