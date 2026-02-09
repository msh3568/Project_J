using System;
using UnityEngine;

[DisallowMultipleComponent]
public class RoomDestroyObjective : MonoBehaviour, IRoomObjective
{
    [Header("Destroy Triggers")]
    [SerializeField] private bool destroyOnDisable = true;
    [SerializeField] private bool destroyOnDestroy = true;
    [SerializeField] private bool debugLogs = false;

    public bool IsDestroyed { get; private set; }
    public event Action<IRoomObjective> Destroyed;

    public void NotifyObjectiveDestroyed()
    {
        MarkDestroyed("NotifyObjectiveDestroyed");
    }

    public void MarkDestroyed()
    {
        MarkDestroyed("MarkDestroyed");
    }

    public void ResetDestroyedState()
    {
        IsDestroyed = false;
    }

    private void OnDisable()
    {
        if (destroyOnDisable)
            MarkDestroyed("OnDisable");
    }

    private void OnDestroy()
    {
        if (destroyOnDestroy)
            MarkDestroyed("OnDestroy");
    }

    private void MarkDestroyed(string source)
    {
        if (IsDestroyed)
            return;

        IsDestroyed = true;
        if (debugLogs)
            Debug.Log("[RoomDestroyObjective] Destroyed from " + source + " on " + name, this);

        if (Destroyed != null)
            Destroyed(this);
    }
}
