using System;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class AwakeningManager : MonoBehaviour
{
    public static event Action GlobalKill;

    [Header("Rules")]
    [SerializeField, Min(0.1f)] private float triggerWindowSeconds = 5f;
    [SerializeField, Min(1)] private int parryTriggerCount = 3;
    [SerializeField, Min(1)] private int killTriggerCount = 3;
    [SerializeField, Min(0.1f)] private float awakeningDuration = 8f;

    [Header("Grapple Overrides")]
    [SerializeField, Min(0f)] private float defaultGrappleCooldown = 3f;
    [SerializeField, Min(0f)] private float awakeningMinInterval = 0.5f;
    [SerializeField, Min(0.1f)] private float awakeningSpeedMultiplier = 1.4f;
    [SerializeField, Min(0.1f)] private float awakeningAccelMultiplier = 1.6f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    [Header("Events (Optional)")]
    [SerializeField] private UnityEvent onAwakeningEnter;
    [SerializeField] private UnityEvent onAwakeningExit;

    private readonly List<float> parrySuccessTimes = new();
    private readonly List<float> killTimes = new();
    private float awakeningEndTime;
    private static readonly int TvPowerId = Shader.PropertyToID("_TVPower");

    public bool IsAwakening { get; private set; }

    public float GrappleCooldownOverride => IsAwakening ? 0f : defaultGrappleCooldown;
    public float GrappleMinInterval => IsAwakening ? awakeningMinInterval : 0f;
    public float GrappleSpeedMultiplier => IsAwakening ? awakeningSpeedMultiplier : 1f;
    public float GrappleAccelMultiplier => IsAwakening ? awakeningAccelMultiplier : 1f;

    private void OnEnable()
    {
        Shader.SetGlobalFloat(TvPowerId, 1f);
        if (enableDebugLogs)
            Debug.Log("[AwakeningManager] OnEnable defaults applied (tvPower=1).", this);
        GlobalKill += RegisterKill;
    }

    private void OnDisable()
    {
        GlobalKill -= RegisterKill;
    }

    private void Update()
    {
        if (!IsAwakening)
            return;

        if (Time.time >= awakeningEndTime)
        {
            EndAwakening();
        }
    }

    public void RegisterParrySuccess()
    {
        if (IsAwakening)
            return;

        float now = Time.time;
        parrySuccessTimes.Add(now);
        PruneOld(parrySuccessTimes, now);

        if (parrySuccessTimes.Count >= parryTriggerCount)
        {
            EnterAwakening();
        }
    }

    public void RegisterParryFail()
    {
        if (IsAwakening)
            return;

        parrySuccessTimes.Clear();
    }

    public void RegisterKill()
    {
        if (IsAwakening)
            return;

        float now = Time.time;
        killTimes.Add(now);
        PruneOld(killTimes, now);

        if (killTimes.Count >= killTriggerCount)
        {
            EnterAwakening();
        }
    }

    public void OnGrappleEnded()
    {
        // Reserved for future tuning/telemetry. Intentionally empty.
    }

    public static void RaiseGlobalKill()
    {
        GlobalKill?.Invoke();
    }

    private void EnterAwakening()
    {
        IsAwakening = true;
        awakeningEndTime = Time.time + awakeningDuration;
        parrySuccessTimes.Clear();
        killTimes.Clear();
        onAwakeningEnter?.Invoke();
        if (enableDebugLogs)
            Debug.Log("[AwakeningManager] Enter Awakening.", this);
    }

    private void EndAwakening()
    {
        IsAwakening = false;
        onAwakeningExit?.Invoke();
        if (enableDebugLogs)
            Debug.Log("[AwakeningManager] Exit Awakening.", this);
    }

    private void PruneOld(List<float> times, float now)
    {
        float cutoff = now - triggerWindowSeconds;
        for (int i = times.Count - 1; i >= 0; i--)
        {
            if (times[i] < cutoff)
                times.RemoveAt(i);
        }
    }
}








