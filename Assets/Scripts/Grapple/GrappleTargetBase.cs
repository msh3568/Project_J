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

    public virtual void OnGrappleArrive(Player player)
    {
    }
}
