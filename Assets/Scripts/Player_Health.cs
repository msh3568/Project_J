using UnityEngine;

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

    public bool IsInvincible { get; set; }
    public bool CanRegenerate { get; private set; }

    protected override void Awake()
    {
        entity = GetComponent<Entity>();
        entityVfx = GetComponent<Entity_VFX>();
        currentShield = maxShield;
        IsInvincible = false;
        CanRegenerate = false; // Initialize
    }

    private void Start()
    {
        InvokeOnHealthChanged(currentShield, maxShield);
        Debug.Log($"초기 보호막: {currentShield}개");
    }

    private void Update()
    {
        timeSinceLastHit += Time.deltaTime;

        bool wasCanRegenerate = CanRegenerate;
        CanRegenerate = timeSinceLastHit > regenerationDelayAfterHit && currentShield < maxShield && !isDead;

        if (CanRegenerate && !wasCanRegenerate)
        {
            Debug.Log($"보호막 재생 지연 ({regenerationDelayAfterHit:F2}초) 완료! 보호막 재생을 시작합니다.");
            regenerationTimer = 0f; // Reset timer when regeneration starts
            lastLoggedSecond = -1; // Reset for new regeneration cycle
        }

        if (CanRegenerate)
        {
            regenerationTimer += Time.deltaTime;

            int currentSecond = Mathf.FloorToInt(regenerationTimer);
            if (currentSecond > lastLoggedSecond && currentSecond > 0)
            {
                Debug.Log($"보호막 재생 중... 다음 보호막까지 {regenerationTime - regenerationTimer:F2}초 남음. (현재 진행: {regenerationTimer:F2}초 / {regenerationTime:F2}초)");
                lastLoggedSecond = currentSecond;
            }

            if (regenerationTimer >= regenerationTime)
            {
                currentShield++;
                InvokeOnHealthChanged(currentShield, maxShield);
                Debug.Log($"보호막 재생! 현재 보호막: {currentShield}개");
                regenerationTimer = 0f;
                lastLoggedSecond = -1; // Reset for next shield point regeneration
            }
        }
    }

    public override void TakeDamage(float damage, Transform damageDealer)
    {
        if (isDead || IsInvincible) return;

        timeSinceLastHit = 0f;
        regenerationTimer = 0f;

        if (currentShield > 0)
        {
            currentShield--;
            InvokeOnHealthChanged(currentShield, maxShield);

            if (currentShield > 0)
            {
                Debug.Log($"피격! 남은 보호막: {currentShield}개");
            }
            else
            {
                Debug.Log("보호막 소진! 다음 공격은 치명적입니다.");
            }
            
            Player player = GetComponent<Player>();
            if (player != null)
            {
                player.PlaySound(player.hitSound);
            }
            
            Vector2 knockback = CalculateKnockback(damage, damageDealer);
            float duration = CalculateDuration(damage);
            entity?.ReciveKnockback(knockback, duration);
            entityVfx?.PlayOnDamageVfx();
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
            Debug.Log("플레이어가 사망했습니다!");
            base.Die();
        }
    }

}
