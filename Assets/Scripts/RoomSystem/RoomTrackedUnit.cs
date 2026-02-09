using System;
using UnityEngine;

[DisallowMultipleComponent]
public class RoomTrackedUnit : MonoBehaviour, IRoomClearUnit
{
    [Header("Clear Triggers")]
    [SerializeField] private bool clearOnDisable = true;
    [SerializeField] private bool clearOnDestroy = true;
    [SerializeField] private bool debugLogs = false;

    public bool IsCleared { get; private set; }
    public event Action<IRoomClearUnit> Cleared;

    public void NotifyDead()
    {
        MarkCleared("NotifyDead");
    }

    public void MarkCleared()
    {
        MarkCleared("MarkCleared");
    }

    public void ResetClearedState()
    {
        IsCleared = false;
    }

    private void OnDisable()
    {
        if (clearOnDisable)
            MarkCleared("OnDisable");
    }

    private void OnDestroy()
    {
        if (clearOnDestroy)
            MarkCleared("OnDestroy");
    }

    private void MarkCleared(string source)
    {
        if (IsCleared)
            return;

        IsCleared = true;
        if (debugLogs)
            Debug.Log("[RoomTrackedUnit] Cleared from " + source + " on " + name, this);

        if (Cleared != null)
            Cleared(this);
    }
}
