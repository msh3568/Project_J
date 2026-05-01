using System.Collections;
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

    [Header("Visual Movement")]
    [SerializeField] private bool useMovement = true;
    [SerializeField] private Transform movingPart;
    [SerializeField] private Vector3 openedOffset = new Vector3(0, 4, 0);
    [SerializeField] private float moveDuration = 1.0f;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public bool IsLocked { get; private set; }
    private Vector3 closedPosition;
    private Coroutine moveCoroutine;

    private void Awake()
    {
        CacheReferencesIfMissing();
        if (movingPart == null) movingPart = transform;
        closedPosition = movingPart.localPosition;
        
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

    public void ResetToInitialState()
    {
        ApplyLockedState(lockOnAwake, true);
    }

    private void ApplyLockedState(bool shouldLock, bool force)
    {
        if (!force && IsLocked == shouldLock)
            return;

        IsLocked = shouldLock;
        ApplyColliderState(shouldLock);
        ApplyAnimatorState(shouldLock);
        ApplySpriteState(shouldLock);

        if (useMovement)
        {
            Vector3 targetPos = shouldLock ? closedPosition : closedPosition + openedOffset;
            if (force)
            {
                movingPart.localPosition = targetPos;
            }
            else
            {
                if (moveCoroutine != null) StopCoroutine(moveCoroutine);
                moveCoroutine = StartCoroutine(MoveRoutine(targetPos));
            }
        }

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

    private IEnumerator MoveRoutine(Vector3 targetPos)
    {
        Vector3 startPos = movingPart.localPosition;
        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = moveCurve.Evaluate(elapsed / moveDuration);
            movingPart.localPosition = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }
        movingPart.localPosition = targetPos;
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
