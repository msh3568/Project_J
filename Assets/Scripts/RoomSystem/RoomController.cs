using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class RoomController : MonoBehaviour
{
    public enum DoorLockMode
    {
        ExitOnly = 0,
        EntranceAndExit = 1
    }

    [Header("Trigger")]
    [SerializeField] private bool activateOnPlayerEnter = true;
    [SerializeField] private bool activateOnlyOnce = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool debugLogs = false;

    [Header("Locking")]
    [SerializeField] private bool lockOnEnter = true;
    [SerializeField] private DoorLockMode lockMode = DoorLockMode.ExitOnly;
    [SerializeField] private List<DoorController> entranceDoors = new List<DoorController>();
    [SerializeField] private List<DoorController> exitDoors = new List<DoorController>();
    [Tooltip("Doors in this list will lock on entry but will NOT unlock when the room is cleared.")]
    [SerializeField] private List<DoorController> entranceDoorsToStayLocked = new List<DoorController>();

    [Header("Clear Conditions")]
    [SerializeField] private List<RoomClearConditionBase> clearConditions = new List<RoomClearConditionBase>();
    [SerializeField] private bool autoFindConditionsInChildren = false;
    [SerializeField] private bool requireAllConditions = true;
    [SerializeField] private bool autoCompleteWhenNoConditions = false;

    [Header("Optimization")]
    [SerializeField] private bool optimizeObjects = true;
    [SerializeField] private bool autoCollectEnemies = true;
    [SerializeField] private List<GameObject> objectsToOptimize = new List<GameObject>();
    [SerializeField] private bool deactivateOnStart = true;
    [SerializeField] private bool deactivateOnExit = true;

    [Header("Events (Optional)")]
    [SerializeField] private UnityEvent onRoomActivated;
    [SerializeField] private UnityEvent onRoomLocked;
    [SerializeField] private UnityEvent onRoomCleared;

    public bool IsRoomActive { get; private set; }
    public bool IsRoomCleared { get; private set; }

    private readonly List<RoomClearConditionBase> runtimeConditions = new List<RoomClearConditionBase>();
    private readonly HashSet<DoorController> lockedDoors = new HashSet<DoorController>();
    private bool hasActivatedAtLeastOnce;
    private bool warnedNoConditions;

    private void Awake()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null && !trigger.isTrigger)
        {
            Debug.LogWarning("[RoomController] Collider was not Trigger. Auto enabling isTrigger on " + name, this);
            trigger.isTrigger = true;
        }

        if (optimizeObjects && autoCollectEnemies)
        {
            CollectEnemiesInHierarchy();
        }

        BuildRuntimeConditionList();

        if (optimizeObjects && deactivateOnStart)
        {
            SetObjectsState(false);
        }
    }

    private void CollectEnemiesInHierarchy()
    {
        // Find all RoomTrackedUnit in children to identify enemies
        RoomTrackedUnit[] units = GetComponentsInChildren<RoomTrackedUnit>(true);
        foreach (var unit in units)
        {
            if (unit != null && !objectsToOptimize.Contains(unit.gameObject))
            {
                objectsToOptimize.Add(unit.gameObject);
            }
        }
    }

    private void SetObjectsState(bool state)
    {
        for (int i = 0; i < objectsToOptimize.Count; i++)
        {
            GameObject obj = objectsToOptimize[i];
            if (obj == null) continue;

            // If we are deactivating, tell children units to suppress their clear-on-disable behavior
            if (!state)
            {
                RoomTrackedUnit[] units = obj.GetComponentsInChildren<RoomTrackedUnit>(true);
                foreach (var unit in units)
                {
                    if (unit != null) unit.suppressClearOnDisable = true;
                }
            }

            obj.SetActive(state);
        }
    }

    private void OnDisable()
    {
        UnsubscribeConditions();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!activateOnPlayerEnter)
            return;

        if (!IsPlayerCollider(other))
            return;

        BeginRoom();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!optimizeObjects || !deactivateOnExit)
            return;

        if (!IsPlayerCollider(other))
            return;

        // Keep room content alive while the encounter is still in progress.
        // Fast player movement, especially grapples, can momentarily leave the trigger bounds.
        if (IsRoomActive && !IsRoomCleared)
        {
            if (debugLogs)
                Debug.Log("[RoomController] Player exited during active encounter. Keeping objects active on " + name, this);
            return;
        }

        if (debugLogs)
            Debug.Log("[RoomController] Player exited. Deactivating objects on " + name, this);

        SetObjectsState(false);
        IsRoomActive = false;
    }

    public void BeginRoom()
    {
        if (activateOnlyOnce && hasActivatedAtLeastOnce)
            return;

        if (optimizeObjects)
        {
            SetObjectsState(true);
        }

        hasActivatedAtLeastOnce = true;
        IsRoomActive = true;
        IsRoomCleared = false;
        warnedNoConditions = false;

        BuildRuntimeConditionList();
        InitializeConditions();

        if (lockOnEnter)
        {
            LockConfiguredDoors();
            if (onRoomLocked != null)
                onRoomLocked.Invoke();
        }

        if (onRoomActivated != null)
            onRoomActivated.Invoke();

        EvaluateRoomClearState();
    }

    public void ResetRoomRuntime(bool unlockDoors = true)
    {
        IsRoomActive = false;
        IsRoomCleared = false;
        warnedNoConditions = false;

        UnsubscribeConditions();
        BuildRuntimeConditionList();

        if (unlockDoors)
            UnlockManagedDoors();
    }

    public void ResetRoomFull()
    {
        IsRoomActive = false;
        IsRoomCleared = false;
        warnedNoConditions = false;
        hasActivatedAtLeastOnce = false;

        UnsubscribeConditions();
        BuildRuntimeConditionList();

        // Unlock everything first to have a clean state
        UnlockManagedDoors();

        // Re-lock if it should be locked on awake
        if (lockOnEnter)
        {
            // We don't call LockConfiguredDoors() here because we want the 
            // initial state before the player enters the trigger.
        }

        // Deactivate objects if optimization is enabled
        if (optimizeObjects)
        {
            SetObjectsState(false);
        }
    }

    public void ForceEvaluateClear()
    {
        EvaluateRoomClearState();
    }

    private void BuildRuntimeConditionList()
    {
        runtimeConditions.Clear();

        if (autoFindConditionsInChildren)
        {
            RoomClearConditionBase[] found = GetComponentsInChildren<RoomClearConditionBase>(true);
            for (int i = 0; i < found.Length; i++)
            {
                AddConditionIfValid(found[i]);
            }
            return;
        }

        for (int i = 0; i < clearConditions.Count; i++)
        {
            AddConditionIfValid(clearConditions[i]);
        }
    }

    private void AddConditionIfValid(RoomClearConditionBase condition)
    {
        if (condition == null)
            return;

        if (runtimeConditions.Contains(condition))
            return;

        runtimeConditions.Add(condition);
    }

    private void InitializeConditions()
    {
        UnsubscribeConditions();

        for (int i = 0; i < runtimeConditions.Count; i++)
        {
            RoomClearConditionBase condition = runtimeConditions[i];
            if (condition == null)
                continue;

            condition.ConditionStateChanged += HandleConditionStateChanged;
            condition.Initialize(this);
        }
    }

    private void UnsubscribeConditions()
    {
        for (int i = 0; i < runtimeConditions.Count; i++)
        {
            RoomClearConditionBase condition = runtimeConditions[i];
            if (condition == null)
                continue;

            condition.ConditionStateChanged -= HandleConditionStateChanged;
        }
    }

    private void HandleConditionStateChanged(IRoomClearCondition _)
    {
        EvaluateRoomClearState();
    }

    private void EvaluateRoomClearState()
    {
        if (!IsRoomActive || IsRoomCleared)
            return;

        if (runtimeConditions.Count == 0)
        {
            if (!warnedNoConditions)
            {
                warnedNoConditions = true;
                Debug.LogWarning("[RoomController] No clear conditions configured on " + name, this);
            }

            if (autoCompleteWhenNoConditions)
                CompleteRoom();
            return;
        }

        bool allComplete = true;
        bool anyComplete = false;

        for (int i = 0; i < runtimeConditions.Count; i++)
        {
            RoomClearConditionBase condition = runtimeConditions[i];
            if (condition == null)
                continue;

            bool conditionComplete = condition.IsComplete;
            allComplete &= conditionComplete;
            anyComplete |= conditionComplete;
        }

        bool shouldComplete = requireAllConditions ? allComplete : anyComplete;
        if (shouldComplete)
            CompleteRoom();
    }

    private void CompleteRoom()
    {
        if (IsRoomCleared)
            return;

        IsRoomCleared = true;
        UnlockManagedDoors();

        if (debugLogs)
            Debug.Log("[RoomController] Room cleared: " + name, this);

        if (onRoomCleared != null)
            onRoomCleared.Invoke();
    }

    private void LockConfiguredDoors()
    {
        lockedDoors.Clear();

        if (lockMode == DoorLockMode.EntranceAndExit)
        {
            LockDoorList(entranceDoors);
        }

        LockDoorList(exitDoors);

        // Lock permanent doors (don't add to lockedDoors so they aren't unlocked)
        for (int i = 0; i < entranceDoorsToStayLocked.Count; i++)
        {
            if (entranceDoorsToStayLocked[i] != null)
                entranceDoorsToStayLocked[i].Lock();
        }

        if (debugLogs)
            Debug.Log("[RoomController] Locked doors count: " + lockedDoors.Count + " in " + name, this);
    }

    private void LockDoorList(List<DoorController> doors)
    {
        for (int i = 0; i < doors.Count; i++)
        {
            DoorController door = doors[i];
            if (door == null)
                continue;

            door.Lock();
            lockedDoors.Add(door);
        }
    }

    private void UnlockManagedDoors()
    {
        foreach (DoorController door in lockedDoors)
        {
            if (door == null)
                continue;

            door.Unlock();
        }

        lockedDoors.Clear();
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null)
            return false;

        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag))
            return true;

        Transform root = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;
        if (root != null && !string.IsNullOrEmpty(playerTag) && root.CompareTag(playerTag))
            return true;

        return false;
    }
}
