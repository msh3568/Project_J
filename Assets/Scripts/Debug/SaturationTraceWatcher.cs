using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class SaturationTraceWatcher : MonoBehaviour
{
    [SerializeField] private bool enableTraceLogs = true;
    [SerializeField, Min(0.001f)] private float saturationThreshold = 0.01f;
    [SerializeField, Min(0.001f)] private float volumeWeightThreshold = 0.01f;
    [SerializeField, Min(0.1f)] private float rescanInterval = 1f;
    [SerializeField] private bool includeVolumeWeightChanges = true;

    private readonly Dictionary<int, TrackedVolume> trackedVolumes = new Dictionary<int, TrackedVolume>();
    private readonly List<int> staleIds = new List<int>();
    private float nextRescanTime;

    private sealed class TrackedVolume
    {
        public Volume Volume;
        public ColorAdjustments ColorAdjustments;
        public float LastSaturation;
        public bool LastActive;
        public float LastWeight;
    }

    private void Awake()
    {
        RescanVolumes(logInitialState: true);
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextRescanTime)
            RescanVolumes(logInitialState: false);

        foreach (TrackedVolume tracked in trackedVolumes.Values)
        {
            if (tracked == null || tracked.Volume == null || tracked.ColorAdjustments == null)
                continue;

            float currentSaturation = tracked.ColorAdjustments.saturation.value;
            bool currentActive = tracked.ColorAdjustments.active;
            float currentWeight = tracked.Volume.weight;

            bool saturationChanged = Mathf.Abs(currentSaturation - tracked.LastSaturation) > saturationThreshold;
            bool activeChanged = currentActive != tracked.LastActive;
            bool weightChanged = includeVolumeWeightChanges
                                 && Mathf.Abs(currentWeight - tracked.LastWeight) > volumeWeightThreshold;

            if (!saturationChanged && !activeChanged && !weightChanged)
                continue;

            if (enableTraceLogs)
            {
                string volumeName = tracked.Volume.gameObject != null ? tracked.Volume.gameObject.name : "(null)";
                Debug.Log(
                    $"[SAT_TRACE][Watcher] volume='{volumeName}' sat {tracked.LastSaturation:F2}->{currentSaturation:F2} active {tracked.LastActive}->{currentActive} weight {tracked.LastWeight:F2}->{currentWeight:F2}",
                    tracked.Volume);
            }

            tracked.LastSaturation = currentSaturation;
            tracked.LastActive = currentActive;
            tracked.LastWeight = currentWeight;
        }
    }

    private void RescanVolumes(bool logInitialState)
    {
        Volume[] sceneVolumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        staleIds.Clear();
        foreach (int id in trackedVolumes.Keys)
            staleIds.Add(id);

        for (int i = 0; i < sceneVolumes.Length; i++)
        {
            Volume volume = sceneVolumes[i];
            if (volume == null || !volume.isGlobal || volume.profile == null)
                continue;

            if (!volume.profile.TryGet(out ColorAdjustments colorAdjustments))
                continue;

            int id = volume.GetInstanceID();
            staleIds.Remove(id);

            if (!trackedVolumes.TryGetValue(id, out TrackedVolume tracked))
            {
                tracked = new TrackedVolume
                {
                    Volume = volume,
                    ColorAdjustments = colorAdjustments,
                    LastSaturation = colorAdjustments.saturation.value,
                    LastActive = colorAdjustments.active,
                    LastWeight = volume.weight
                };
                trackedVolumes.Add(id, tracked);

                if (logInitialState && enableTraceLogs)
                {
                    string volumeName = volume.gameObject != null ? volume.gameObject.name : "(null)";
                    Debug.Log(
                        $"[SAT_TRACE][Watcher.Init] volume='{volumeName}' sat={tracked.LastSaturation:F2} active={tracked.LastActive} weight={tracked.LastWeight:F2}",
                        volume);
                }
                continue;
            }

            tracked.Volume = volume;
            tracked.ColorAdjustments = colorAdjustments;
        }

        for (int i = 0; i < staleIds.Count; i++)
            trackedVolumes.Remove(staleIds[i]);

        nextRescanTime = Time.unscaledTime + Mathf.Max(0.1f, rescanInterval);
    }
}
