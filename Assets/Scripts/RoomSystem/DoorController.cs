using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class DoorController : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private bool lockOnAwake = false;
    [SerializeField] private bool debugLogs = false;

    [Header("Blocking")]
    [SerializeField] private Collider2D[] blockingColliders = new Collider2D[0];
    [SerializeField] private bool autoCollectChildColliders = false;
    [SerializeField] private bool ignoreTriggerColliders = true;

    [Header("Animator Hook (Optional)")]
    [SerializeField] private Animator animator;
    [SerializeField] private string lockedBoolParameter = "Locked";
    [SerializeField] private string lockTriggerParameter = "";
    [SerializeField] private string unlockTriggerParameter = "";

    [Header("Sprite Hook (Optional)")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite lockedSprite;
    [SerializeField] private Sprite unlockedSprite;

    [Header("Events (Optional)")]
    [SerializeField] private UnityEvent onLocked;
    [SerializeField] private UnityEvent onUnlocked;

    public bool IsLocked { get; private set; }

    private void Awake()
    {
        CacheReferencesIfMissing();
        ApplyLockedState(lockOnAwake, true);
    }

    public void Lock()
    {
        ApplyLockedState(true, false);
    }

    public void Unlock()
    {
        ApplyLockedState(false, false);
    }

    public void SetLocked(bool value)
    {
        ApplyLockedState(value, false);
    }

    private void ApplyLockedState(bool shouldLock, bool force)
    {
        if (!force && IsLocked == shouldLock)
            return;

        IsLocked = shouldLock;
        ApplyColliderState(shouldLock);
        ApplyAnimatorState(shouldLock);
        ApplySpriteState(shouldLock);

        if (shouldLock)
        {
            if (onLocked != null)
                onLocked.Invoke();
        }
        else
        {
            if (onUnlocked != null)
                onUnlocked.Invoke();
        }

        if (debugLogs)
            Debug.Log("[DoorController] " + name + " -> " + (shouldLock ? "Locked" : "Unlocked"), this);
    }

    private void CacheReferencesIfMissing()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (blockingColliders != null && blockingColliders.Length > 0)
            return;

        blockingColliders = autoCollectChildColliders
            ? GetComponentsInChildren<Collider2D>(true)
            : GetComponents<Collider2D>();
    }

    private void ApplyColliderState(bool locked)
    {
        if (blockingColliders == null)
            return;

        for (int i = 0; i < blockingColliders.Length; i++)
        {
            Collider2D col = blockingColliders[i];
            if (col == null)
                continue;
            if (ignoreTriggerColliders && col.isTrigger)
                continue;

            col.enabled = locked;
        }
    }

    private void ApplyAnimatorState(bool locked)
    {
        if (animator == null)
            return;

        if (!string.IsNullOrEmpty(lockedBoolParameter) && HasAnimatorParameter(lockedBoolParameter, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(lockedBoolParameter, locked);
        }

        if (locked && !string.IsNullOrEmpty(lockTriggerParameter) && HasAnimatorParameter(lockTriggerParameter, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(lockTriggerParameter);
        }

        if (!locked && !string.IsNullOrEmpty(unlockTriggerParameter) && HasAnimatorParameter(unlockTriggerParameter, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(unlockTriggerParameter);
        }
    }

    private void ApplySpriteState(bool locked)
    {
        if (spriteRenderer == null)
            return;

        if (locked && lockedSprite != null)
            spriteRenderer.sprite = lockedSprite;
        else if (!locked && unlockedSprite != null)
            spriteRenderer.sprite = unlockedSprite;
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName))
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == parameterType && parameter.name == parameterName)
                return true;
        }

        return false;
    }
}
