using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class ObjectDestroyCondition : RoomClearConditionBase
{
    [Header("Objectives (registered only)")]
    [SerializeField] private List<RoomDestroyObjective> objectives = new List<RoomDestroyObjective>();

    [Header("Optional Auto Collect")]
    [SerializeField] private bool autoCollectFromChildren = false;
    [SerializeField] private Transform collectRoot;
    [SerializeField] private bool includeInactiveChildren = true;

    [Header("Behavior")]
    [SerializeField] private bool completeWhenNoObjectives = false;
    [FormerlySerializedAs("debugLogs")]
    [SerializeField] private bool conditionDebugLogs = false;

    private readonly List<RoomDestroyObjective> runtimeObjectives = new List<RoomDestroyObjective>();

    public override void ResetCondition()
    {
        UnsubscribeAll();
        BuildRuntimeObjectiveList();
        SubscribeAll();
        EvaluateCompletion();
    }

    public void RegisterObjective(RoomDestroyObjective objective)
    {
        if (objective == null)
            return;

        if (runtimeObjectives.Contains(objective))
            return;

        runtimeObjectives.Add(objective);
        objective.Destroyed += HandleObjectiveDestroyed;
        EvaluateCompletion();
    }

    public void UnregisterObjective(RoomDestroyObjective objective)
    {
        if (objective == null)
            return;

        if (!runtimeObjectives.Remove(objective))
            return;

        objective.Destroyed -= HandleObjectiveDestroyed;
        EvaluateCompletion();
    }

    private void OnDisable()
    {
        UnsubscribeAll();
    }

    private void BuildRuntimeObjectiveList()
    {
        runtimeObjectives.Clear();

        if (autoCollectFromChildren)
        {
            Transform root = collectRoot != null ? collectRoot : transform;
            RoomDestroyObjective[] found = root.GetComponentsInChildren<RoomDestroyObjective>(includeInactiveChildren);
            for (int i = 0; i < found.Length; i++)
            {
                AddRuntimeObjective(found[i]);
            }
            return;
        }

        for (int i = 0; i < objectives.Count; i++)
        {
            AddRuntimeObjective(objectives[i]);
        }
    }

    private void AddRuntimeObjective(RoomDestroyObjective objective)
    {
        if (objective == null)
            return;

        if (runtimeObjectives.Contains(objective))
            return;

        runtimeObjectives.Add(objective);
    }

    private void SubscribeAll()
    {
        for (int i = 0; i < runtimeObjectives.Count; i++)
        {
            RoomDestroyObjective objective = runtimeObjectives[i];
            if (objective == null)
                continue;

            objective.Destroyed -= HandleObjectiveDestroyed;
            objective.Destroyed += HandleObjectiveDestroyed;
        }
    }

    private void UnsubscribeAll()
    {
        for (int i = 0; i < runtimeObjectives.Count; i++)
        {
            RoomDestroyObjective objective = runtimeObjectives[i];
            if (objective == null)
                continue;

            objective.Destroyed -= HandleObjectiveDestroyed;
        }
    }

    private void HandleObjectiveDestroyed(IRoomObjective _)
    {
        EvaluateCompletion();
    }

    private void EvaluateCompletion()
    {
        int total = runtimeObjectives.Count;
        if (total == 0)
        {
            SetComplete(completeWhenNoObjectives);
            if (conditionDebugLogs)
                Debug.Log("[ObjectDestroyCondition] No objectives. Complete = " + completeWhenNoObjectives, this);
            return;
        }

        int destroyed = 0;
        for (int i = 0; i < runtimeObjectives.Count; i++)
        {
            RoomDestroyObjective objective = runtimeObjectives[i];
            if (objective == null || objective.IsDestroyed)
                destroyed++;
        }

        bool completed = destroyed >= total;
        SetComplete(completed);

        if (conditionDebugLogs)
            Debug.Log("[ObjectDestroyCondition] " + destroyed + "/" + total + " destroyed", this);
    }
}
