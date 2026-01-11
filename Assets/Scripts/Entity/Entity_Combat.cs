using UnityEngine;

public class Entity_Combat : MonoBehaviour
{
    private Entity_VFX vfx;
    public float damage = 10;

    [Header("Target detection")]
    [SerializeField] private Transform targetCheck;
    [SerializeField] private float targetCheckRadius = 1;
    [SerializeField] private LayerMask whatIsTarget;

    private void Awake()
    {
        vfx = GetComponent<Entity_VFX>();
    }

    public void PerformMultiAttack()
    {
        foreach (var target in GetDetectedColliders())
        {
            // 멀티플레 전용 로직. 공격 감지 -> 서버로 전송
            if (target.TryGetComponent<NetPlayer>(out var netPlayer))
            {
                netPlayer.AttackToPlayer(damage);
                continue;
            }

            /*IDamagable damagable = target.GetComponent<IDamagable>();

            if (damagable == null)
                continue;

            damagable.TakeDamage(damage, transform);*/
            vfx.CreateOnHitVFX(target.transform);
        }

    }


    public void PerformAttack()
    {
        foreach (var target in GetDetectedColliders())
        {
            IDamagable damagable = target.GetComponent<IDamagable>();

            if(damagable == null)
                continue;

            damagable.TakeDamage(damage, transform);
            vfx.CreateOnHitVFX(target.transform);
        }
        
    }


    protected Collider2D[] GetDetectedColliders()
    {
        return Physics2D.OverlapCircleAll(targetCheck.position, targetCheckRadius, whatIsTarget);
    }


    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(targetCheck.position, targetCheckRadius);
    }
}
