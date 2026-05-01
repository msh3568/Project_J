using UnityEngine;

public class Entity_Combat : MonoBehaviour
{
    protected Entity_VFX vfx;
    public float damage = 10;

    [Header("Target detection")]
    [SerializeField] private Transform targetCheck;
    [SerializeField] private float targetCheckRadius = 1;
    [SerializeField] private LayerMask whatIsTarget;

    protected virtual void Awake()
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

        if (!ShouldDamageTarget(target, damagable))
            return false;

        damagable.TakeDamage(damage, transform);

        if (vfx != null)
            vfx.CreateOnHitVFX(target.transform, GetHitPoint(target));

        OnSuccessfulHit(target, damagable);
        return true;
    }

    protected virtual bool ShouldDamageTarget(Collider2D target, IDamageable damageable)
    {
        return true;
    }

    protected virtual Vector2 GetHitPoint(Collider2D target)
    {
        if (target == null)
            return transform.position;

        Vector2 attackOrigin = GetAttackOrigin();
        Vector2 hitPoint = target.ClosestPoint(attackOrigin);

        if (float.IsNaN(hitPoint.x) || float.IsNaN(hitPoint.y))
            return target.transform.position;

        return hitPoint;
    }

    protected bool HasDamageable(Collider2D target)
    {
        return DamageableLookup.TryGetDamageable(target, out _);
    }

    protected bool TryGetDamageable(Collider2D target, out IDamageable damagable)
    {
        return DamageableLookup.TryGetDamageable(target, out damagable);
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
