using UnityEngine;

public static class DamageableLookup
{
    public static bool TryGetDamageable(Component source, out IDamageable damageable)
    {
        damageable = Resolve<IDamageable>(source);
        return IsDamageableValid(damageable);
    }

    public static bool TryGetDamageable(GameObject source, out IDamageable damageable)
    {
        damageable = Resolve<IDamageable>(source != null ? source.transform : null);
        return IsDamageableValid(damageable);
    }

    public static bool TryGetDamageable(Collision2D collision, out IDamageable damageable)
    {
        damageable = null;
        if (collision == null)
            return false;

        if (TryGetDamageable(collision.collider, out damageable))
            return true;

        if (collision.rigidbody != null)
            return TryGetDamageable(collision.rigidbody.transform, out damageable);

        return false;
    }

    public static bool TryGetInSelfOrParent<T>(Component source, out T component) where T : class
    {
        component = Resolve<T>(source);
        return component != null;
    }

    private static T Resolve<T>(Component source) where T : class
    {
        if (source == null)
            return null;

        T resolved = source.GetComponent(typeof(T)) as T;
        if (resolved != null)
            return resolved;

        resolved = source.GetComponentInParent(typeof(T)) as T;
        if (resolved != null)
            return resolved;

        if (source is Collider2D collider && collider.attachedRigidbody != null)
        {
            resolved = collider.attachedRigidbody.GetComponent(typeof(T)) as T;
            if (resolved != null)
                return resolved;

            resolved = collider.attachedRigidbody.GetComponentInParent(typeof(T)) as T;
        }

        return resolved;
    }

    private static bool IsDamageableValid(IDamageable damageable)
    {
        if (damageable == null)
            return false;

        if (damageable is IDamageableStatus status)
            return status.CanReceiveDamage;

        return true;
    }
}
