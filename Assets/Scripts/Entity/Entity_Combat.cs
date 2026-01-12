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


    public void PerformAttack()
    {
        foreach (var target in GetDetectedColliders())
        {
            IDamageable damagable = target.GetComponent<IDamageable>();

            if(damagable == null)
                continue;

            damagable.TakeDamage(damage, transform);
            
            if (vfx != null)
                vfx.CreateOnHitVFX(target.transform);
        }
        
    }


    protected Collider2D[] GetDetectedColliders()
    {
        return Physics2D.OverlapCircleAll(targetCheck.position, targetCheckRadius, whatIsTarget);
    }


    protected virtual void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(targetCheck.position, targetCheckRadius);
    }
}
