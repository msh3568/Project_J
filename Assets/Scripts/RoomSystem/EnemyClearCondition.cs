using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyClearCondition : RoomClearConditionBase
{
    [Header("Tracked Units (registered only)")]
    [SerializeField] private List<RoomTrackedUnit> trackedUnits = new List<RoomTrackedUnit>();

    [Header("Optional Auto Collect")]
    [SerializeField] private bool autoCollectFromChildren = false;
    [SerializeField] private Transform collectRoot;
    [SerializeField] private bool includeInactiveChildren = true;

    [Header("Behavior")]
    [SerializeField] private bool completeWhenNoUnits = true;
    [SerializeField] private bool debugLogs = false;

    private readonly List<RoomTrackedUnit> runtimeUnits = new List<RoomTrackedUnit>();

    public override void ResetCondition()
    {
        UnsubscribeAll();
        BuildRuntimeUnitList();
        SubscribeAll();
        EvaluateCompletion();
    }

    public void RegisterUnit(RoomTrackedUnit unit)
    {
        if (unit == null)
            return;

        if (runtimeUnits.Contains(unit))
            return;

        runtimeUnits.Add(unit);
        unit.Cleared += HandleUnitCleared;
        EvaluateCompletion();
    }

    public void UnregisterUnit(RoomTrackedUnit unit)
    {
        if (unit == null)
            return;

        if (!runtimeUnits.Remove(unit))
            return;

        unit.Cleared -= HandleUnitCleared;
        EvaluateCompletion();
    }

    private void OnDisable()
    {
        UnsubscribeAll();
    }

    private void BuildRuntimeUnitList()
    {
        runtimeUnits.Clear();

        if (autoCollectFromChildren)
        {
            Transform root = collectRoot != null ? collectRoot : transform;
            RoomTrackedUnit[] found = root.GetComponentsInChildren<RoomTrackedUnit>(includeInactiveChildren);
            for (int i = 0; i < found.Length; i++)
            {
                AddRuntimeUnit(found[i]);
            }
            return;
        }

        for (int i = 0; i < trackedUnits.Count; i++)
        {
            AddRuntimeUnit(trackedUnits[i]);
        }
    }

    private void AddRuntimeUnit(RoomTrackedUnit unit)
    {
        if (unit == null)
            return;

        if (runtimeUnits.Contains(unit))
            return;

        runtimeUnits.Add(unit);
    }

    private void SubscribeAll()
    {
        for (int i = 0; i < runtimeUnits.Count; i++)
        {
            RoomTrackedUnit unit = runtimeUnits[i];
            if (unit == null)
                continue;

            unit.Cleared -= HandleUnitCleared;
            unit.Cleared += HandleUnitCleared;
        }
    }

    private void UnsubscribeAll()
    {
        for (int i = 0; i < runtimeUnits.Count; i++)
        {
            RoomTrackedUnit unit = runtimeUnits[i];
            if (unit == null)
                continue;

            unit.Cleared -= HandleUnitCleared;
        }
    }

    private void HandleUnitCleared(IRoomClearUnit _)
    {
        EvaluateCompletion();
    }

    private void EvaluateCompletion()
    {
        int total = runtimeUnits.Count;
        if (total == 0)
        {
            SetComplete(completeWhenNoUnits);
            if (debugLogs)
                Debug.Log("[EnemyClearCondition] No tracked units. Complete = " + completeWhenNoUnits, this);
            return;
        }

        int cleared = 0;
        for (int i = 0; i < runtimeUnits.Count; i++)
        {
            RoomTrackedUnit unit = runtimeUnits[i];
            if (unit == null || unit.IsCleared)
                cleared++;
        }

        bool completed = cleared >= total;
        SetComplete(completed);

        if (debugLogs)
            Debug.Log("[EnemyClearCondition] " + cleared + "/" + total + " cleared", this);
    }
}
