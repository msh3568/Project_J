using UnityEngine;

[DisallowMultipleComponent]
public class CutsceneNpcActor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer[] spriteRenderers;
    [SerializeField] private Collider2D interactionCollider;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheMissingReferences();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheMissingReferences();
    }
#endif

    public void SetVisible(bool visible)
    {
        CacheMissingReferences();

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].enabled = visible;
        }
    }

    public void FaceRight()
    {
        SetFacing(true);
    }

    public void FaceLeft()
    {
        SetFacing(false);
    }

    public void SetFacing(bool right)
    {
        Transform target = visualRoot != null ? visualRoot : transform;
        Vector3 scale = target.localScale;
        scale.x = Mathf.Abs(scale.x) * (right ? 1f : -1f);
        target.localScale = scale;
    }

    public void SetSpriteFlipX(bool flipX)
    {
        CacheMissingReferences();

        if (spriteRenderers == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].flipX = flipX;
        }
    }

    public void LookAt(Transform target)
    {
        if (target == null)
            return;

        SetFacing(target.position.x >= transform.position.x);
    }

    public void PlayAnimation(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        animator.Play(stateName);
    }

    public void SetAnimatorTrigger(string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName))
            return;

        animator.SetTrigger(triggerName);
    }

    public void SetAnimatorBool(string parameterName, bool value)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
            return;

        animator.SetBool(parameterName, value);
    }

    public void SetAnimatorFloat(string parameterName, float value)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
            return;

        animator.SetFloat(parameterName, value);
    }

    public void SnapTo(Transform point)
    {
        if (point == null)
            return;

        transform.SetPositionAndRotation(point.position, point.rotation);
    }

    public void SetInteractionCollider(bool active)
    {
        if (interactionCollider != null)
            interactionCollider.enabled = active;
    }

    public void SetSortingOrder(int order)
    {
        CacheMissingReferences();

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].sortingOrder = order;
        }
    }

    private void CacheReferences()
    {
        animator = GetComponentInChildren<Animator>(true);
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        if (visualRoot == null && spriteRenderers.Length > 0 && spriteRenderers[0] != null)
            visualRoot = spriteRenderers[0].transform;

        interactionCollider = GetComponent<Collider2D>();
    }

    private void CacheMissingReferences()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (spriteRenderers == null || spriteRenderers.Length == 0)
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        if (visualRoot == null && spriteRenderers != null && spriteRenderers.Length > 0 && spriteRenderers[0] != null)
            visualRoot = spriteRenderers[0].transform;

        if (interactionCollider == null)
            interactionCollider = GetComponent<Collider2D>();
    }
}
