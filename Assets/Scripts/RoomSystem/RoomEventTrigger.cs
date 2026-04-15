using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class RoomEventTrigger : MonoBehaviour
{
    [Header("Trigger Settings")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool triggerOnlyOnce = true;

    [Header("Phase 1: Entry & Lock")]
    [SerializeField] private float inputDisableDuration = 0.5f;
    [SerializeField] private List<DoorController> doorsToLock = new List<DoorController>();
    
    [Header("Phase 2: Clear Condition")]
    [SerializeField] private RoomClearConditionBase clearCondition;

    [Header("Events")]
    [SerializeField] private UnityEvent onRoomLocked;
    [SerializeField] private UnityEvent onRoomCleared;
    [SerializeField] private bool debugLogs = false;

    private bool hasTriggered;
    private bool isRoomActive;
    private bool isSequenceRunning;
    private Player cachedPlayer;

    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        // Initialize condition if assigned
        if (clearCondition != null)
        {
            clearCondition.ConditionStateChanged += HandleConditionStateChanged;
            clearCondition.ResetCondition();
        }
    }

    private void OnDestroy()
    {
        if (clearCondition != null)
        {
            clearCondition.ConditionStateChanged -= HandleConditionStateChanged;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered && triggerOnlyOnce)
            return;

        if (!IsPlayer(other))
            return;

        StartRoomEvent(other.GetComponentInParent<Player>());
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (hasTriggered)
            return;

        if (!IsPlayer(other))
            return;

        StartRoomEvent(other.GetComponentInParent<Player>());
    }

    private void StartRoomEvent(Player player)
    {
        if (isSequenceRunning || isRoomActive)
            return;

        if (hasTriggered && triggerOnlyOnce)
            return;

        hasTriggered = true;
        cachedPlayer = player;
        isSequenceRunning = true;
        StartCoroutine(RoomSequenceRoutine());
    }

    private IEnumerator RoomSequenceRoutine()
    {
        if (debugLogs) Debug.Log($"[RoomEventTrigger] {name} Starting sequence for {cachedPlayer.name}", this);

        // Step 1: Restrict Input
        if (cachedPlayer != null)
        {
            cachedPlayer.Immobilize(inputDisableDuration);
        }

        // Step 2: Wait for delay
        yield return new WaitForSeconds(inputDisableDuration);

        // Step 3: Close (Lock) Doors
        foreach (var door in doorsToLock)
        {
            if (door != null)
                door.Lock();
        }

        // --- CRITICAL: Only mark room as active AFTER locking doors ---
        isRoomActive = true; 
        onRoomLocked?.Invoke();
        
        if (debugLogs) Debug.Log($"[RoomEventTrigger] {name} Doors Locked. Evaluating clear conditions...", this);

        // Step 4: Reset and Evaluate Condition
        if (clearCondition != null)
        {
            clearCondition.ResetCondition();
            
            // If already complete, unlock immediately
            if (clearCondition.IsComplete)
            {
                if (debugLogs) Debug.Log($"[RoomEventTrigger] {name} Already clear, unlocking!", this);
                UnlockRoom();
            }
        }

        isSequenceRunning = false;
    }

    private void HandleConditionStateChanged(IRoomClearCondition condition)
    {
        if (!isRoomActive)
            return;

        if (condition.IsComplete)
        {
            UnlockRoom();
        }
    }

    private void UnlockRoom()
    {
        isRoomActive = false;

        // Step 5: Unlock Doors
        foreach (var door in doorsToLock)
        {
            if (door != null)
                door.Unlock();
        }

        onRoomCleared?.Invoke();
    }

    public void ResetTriggerRuntime(bool restoreDoorsToInitialState = true)
    {
        StopAllCoroutines();
        hasTriggered = false;
        isRoomActive = false;
        isSequenceRunning = false;
        cachedPlayer = null;

        if (!restoreDoorsToInitialState)
            return;

        foreach (var door in doorsToLock)
        {
            if (door != null)
                door.ResetToInitialState();
        }
    }

    private bool IsPlayer(Collider2D other)
    {
        if (string.IsNullOrEmpty(playerTag))
            return true;
        
        return other.CompareTag(playerTag) || (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(playerTag));
    }
}
