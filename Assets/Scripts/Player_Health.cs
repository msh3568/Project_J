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

    public bool CanRegenerate { get; private set; }

    protected override void Awake()
    {
        entity = GetComponent<Entity>();
        entityVfx = GetComponent<Entity_VFX>();
        currentShield = maxShield;
    }

    private void Start()
    {
        InvokeOnHealthChanged(currentShield, maxShield);
        Debug.Log($"초기 보호막: {currentShield}개");
    }

    private void Update()
    {
        timeSinceLastHit += Time.deltaTime;

        CanRegenerate = timeSinceLastHit > regenerationDelayAfterHit && currentShield < maxShield;

        if (CanRegenerate)
        {
            regenerationTimer += Time.deltaTime;
            if (regenerationTimer >= regenerationTime)
            {
                currentShield++;
                InvokeOnHealthChanged(currentShield, maxShield);
                Debug.Log($"보호막 재생! 현재 보호막: {currentShield}개");
                regenerationTimer = 0f;
            }
        }
    }

    public override void TakeDamage(float damage, Transform damageDealer)
    {
        if (isDead)
            return;

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
