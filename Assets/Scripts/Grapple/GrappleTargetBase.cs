using UnityEngine;

public abstract class GrappleTargetBase : MonoBehaviour
{
    [SerializeField] private Transform aimPoint;
    [SerializeField] private bool canBeTargeted = true;

    public virtual bool IsAvailableForGrapple(Player player)
    {
        return canBeTargeted && isActiveAndEnabled && gameObject.activeInHierarchy;
    }

    public virtual Vector2 GetAimPosition()
    {
        if (aimPoint != null)
            return aimPoint.position;

        return transform.position;
    }

    public virtual Vector2 GetArrivalPosition(Player player, LockOnGrappleConfig config, Vector2 startPosition)
    {
        return GetAimPosition();
    }

    public virtual void OnGrappleArrive(Player player)
    {
    }

    protected Vector2 ResolveStopShortArrivalPosition(Vector2 startPosition, float stopShortDistance)
    {
        Vector2 aimPosition = GetAimPosition();
        if (stopShortDistance <= 0f)
            return aimPosition;

        Vector2 toTarget = aimPosition - startPosition;
        float distance = toTarget.magnitude;
        if (distance <= Mathf.Max(0.001f, stopShortDistance))
            return aimPosition;

        return aimPosition - (toTarget / distance) * stopShortDistance;
    }
}
