using UnityEngine;

public class Entity_Combat : MonoBehaviour
{
    protected Entity_VFX vfx;
    public float damage = 10;

    [Header("Target detection")]
    [SerializeField] private Transform targetCheck;
    [SerializeField] private float targetCheckRadius = 1;
    [SerializeField] private LayerMask whatIsTarget;

    private void Awake()
    {
        vfx = GetComponent<Entity_VFX>();
    }


    public virtual void PerformAttack()
    {
        Collider2D[] detectedColliders = GetDetectedColliders();

        if (!AllowMultiHit)
        {
            Collider2D primaryTarget = GetPrimaryTarget(detectedColliders);
            TryDamageTarget(primaryTarget);
            return;
        }

        foreach (var target in detectedColliders)
        {
            TryDamageTarget(target);
        }
    }

    protected virtual void OnSuccessfulHit(Collider2D target, IDamageable damagable)
    {
    }

    protected virtual bool AllowMultiHit => true;

    protected virtual Collider2D GetPrimaryTarget(Collider2D[] detectedColliders)
    {
        if (detectedColliders == null)
            return null;

        for (int i = 0; i < detectedColliders.Length; i++)
        {
            if (HasDamageable(detectedColliders[i]))
                return detectedColliders[i];
        }

        return null;
    }

    protected Vector2 GetAttackOrigin()
    {
        return targetCheck != null ? targetCheck.position : transform.position;
    }

    private bool TryDamageTarget(Collider2D target)
    {
        if (!TryGetDamageable(target, out IDamageable damagable))
            return false;

        damagable.TakeDamage(damage, transform);

        if (vfx != null)
            vfx.CreateOnHitVFX(target.transform);

        OnSuccessfulHit(target, damagable);
        return true;
    }

    protected bool HasDamageable(Collider2D target)
    {
        return target != null && target.GetComponent<IDamageable>() != null;
    }

    protected bool TryGetDamageable(Collider2D target, out IDamageable damagable)
    {
        damagable = target != null ? target.GetComponent<IDamageable>() : null;
        return damagable != null;
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
