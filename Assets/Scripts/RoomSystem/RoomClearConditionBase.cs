using System;
using UnityEngine;

public abstract class RoomClearConditionBase : MonoBehaviour, IRoomClearCondition
{
    [SerializeField] private bool debugLogs = false;

    public bool IsComplete { get; private set; }
    public event Action<IRoomClearCondition> ConditionStateChanged;

    protected RoomController Room { get; private set; }

    public virtual void Initialize(RoomController room)
    {
        Room = room;
        ResetCondition();
    }

    public abstract void ResetCondition();

    protected void SetComplete(bool value)
    {
        if (IsComplete == value)
            return;

        IsComplete = value;
        if (debugLogs)
            Debug.Log("[RoomClearCondition] " + GetType().Name + " -> " + IsComplete, this);

        if (ConditionStateChanged != null)
            ConditionStateChanged(this);
    }
}
