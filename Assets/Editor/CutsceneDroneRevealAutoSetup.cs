using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[InitializeOnLoad]
public static class CutsceneDroneRevealAutoSetup
{
    private const string DirectorName = "Cutscene_Director";
    private const string EnemyRoomName = "Room_Trigger_A_Enemy";
    private const string TargetDroneName = "LatencyDroneStrong2";
    private const string SpawnEffectObjectName = "Cutscene_SpawnEffect";
    private const string SpawnEffectTrackName = "Spawn Effect";
    private const string CombatLockTrackName = "Drone No Attack";
    private const string CombatLockClipName = "No Attack";
    private const string ActivationTrackName = "Drone Visible";
    private const string IdleTrackName = "Drone Idle";
    private const string MovementTrackName = "Drone Movement";
    private const string SpawnEffectPrefabGuid = "8c3e07a3a8419db40bca7d6a208dd1b0";
    private const string IdleClipPath = "Assets/ART/Cutscene/DroneIdle.anim";
    private const string MovementClipPath = "Assets/ART/Cutscene/DroneRevealMovement.anim";
    private const string IdleClipName = "DroneIdle";
    private const string MovementClipName = "DroneRevealMovement";
    private const double RevealStart = 9.1d;
    private const double RevealDuration = 2.4d;
    private const double SpawnEffectDuration = 0.45d;

    private static bool setupQueued;

    static CutsceneDroneRevealAutoSetup()
    {
        QueueSetup();
        EditorSceneManager.sceneOpened += (_, _) => QueueSetup();
    }

    [MenuItem("Tools/Cutscene/Setup Drone Reveal Clip")]
    public static void SetupFromMenu()
    {
        setupQueued = false;
        SetupDroneRevealClip(logWhenMissing: true);
    }

    private static void QueueSetup()
    {
        if (IsPlayModeChangingOrActive() || setupQueued)
            return;

        setupQueued = true;
        EditorApplication.delayCall += RunQueuedSetup;
    }

    private static void RunQueuedSetup()
    {
        setupQueued = false;
        SetupDroneRevealClip(logWhenMissing: false);
    }

    private static void SetupDroneRevealClip(bool logWhenMissing)
    {
        if (IsPlayModeChangingOrActive() || IsTimelinePreviewing())
            return;

        GameObject directorObject = FindSceneObject(DirectorName);
        if (directorObject == null)
        {
            LogMissing(logWhenMissing, "Cutscene_Director was not found.");
            return;
        }

        PlayableDirector director = directorObject.GetComponent<PlayableDirector>();
        TimelineAsset timelineAsset = director != null ? director.playableAsset as TimelineAsset : null;
        if (director == null || timelineAsset == null)
        {
            LogMissing(logWhenMissing, "Cutscene_Director does not have a Timeline asset.");
            return;
        }

        GameObject droneObject = ResolveTargetDrone();
        if (droneObject == null)
        {
            LogMissing(logWhenMissing, "LatencyDroneStrong2 was not found under Room_Trigger_A_Enemy.");
            return;
        }

        GameObject spawnEffectObject = EnsureSpawnEffectObject(droneObject);
        Animator droneAnimator = EnsureAnimator(droneObject);
        LatencyDroneWeak droneCombat = droneObject.GetComponent<LatencyDroneWeak>();
        AnimationClip idleClip = EnsureIdleClip();
        AnimationClip movementClip = EnsureMovementClip(droneObject);
        if (droneAnimator == null || idleClip == null || movementClip == null)
            return;

        Undo.RegisterCompleteObjectUndo(timelineAsset, "Setup drone reveal Timeline clips");
        bool changed = false;
        changed |= ConfigureSpawnEffectObject(spawnEffectObject, droneObject);
        changed |= ConfigureActivationTrack(timelineAsset, director, droneObject);
        changed |= ConfigureSpawnEffectTrack(timelineAsset, director, spawnEffectObject);
        changed |= ConfigureCombatLockTrack(timelineAsset, director, droneCombat);
        changed |= ConfigureIdleTrack(timelineAsset, director, droneAnimator, idleClip);
        changed |= ConfigureMovementTrack(timelineAsset, director, droneAnimator, movementClip);

        if (!changed)
            return;

        EditorUtility.SetDirty(timelineAsset);
        EditorUtility.SetDirty(director);
        EditorUtility.SetDirty(directorObject);
        EditorSceneManager.MarkSceneDirty(directorObject.scene);
        AssetDatabase.SaveAssets();
        TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved | RefreshReason.WindowNeedsRedraw);
        Debug.Log("Drone reveal clips are set up on Cutscene_Director. Edit Spawn Effect, Drone Visible, Drone No Attack, Drone Idle, and Drone Movement on the Timeline.", directorObject);
    }

    private static bool ConfigureActivationTrack(TimelineAsset timelineAsset, PlayableDirector director, GameObject droneObject)
    {
        ActivationTrack track = FindTrackByName<ActivationTrack>(timelineAsset, ActivationTrackName);
        bool changed = false;

        if (track == null)
        {
            track = timelineAsset.CreateTrack<ActivationTrack>(ActivationTrackName);
            changed = true;
        }

        if (track.name != ActivationTrackName)
        {
            track.name = ActivationTrackName;
            changed = true;
        }

        if (track.muted)
        {
            track.muted = false;
            changed = true;
        }

        if (track.postPlaybackState != ActivationTrack.PostPlaybackState.Revert)
        {
            track.postPlaybackState = ActivationTrack.PostPlaybackState.Revert;
            changed = true;
        }

        TimelineClip revealClip = FindClipByName(track, "Drone Visible");
        if (revealClip == null)
            revealClip = FindFirstClip(track);

        if (revealClip == null)
        {
            revealClip = track.CreateDefaultClip();
            changed |= ConfigureClipTiming(revealClip, "Drone Visible", RevealStart, RevealDuration);
            changed = true;
        }

        if (director.GetGenericBinding(track) != droneObject)
        {
            director.SetGenericBinding(track, droneObject);
            changed = true;
        }

        EditorUtility.SetDirty(track);
        return changed;
    }

    private static bool ConfigureCombatLockTrack(TimelineAsset timelineAsset, PlayableDirector director, LatencyDroneWeak droneCombat)
    {
        if (droneCombat == null)
            return false;

        CutsceneDroneCombatLockTrack track = FindTrackByName<CutsceneDroneCombatLockTrack>(timelineAsset, CombatLockTrackName);
        bool changed = false;

        if (track == null)
        {
            track = timelineAsset.CreateTrack<CutsceneDroneCombatLockTrack>(CombatLockTrackName);
            changed = true;
        }

        if (track.name != CombatLockTrackName)
        {
            track.name = CombatLockTrackName;
            changed = true;
        }

        if (track.muted)
        {
            track.muted = false;
            changed = true;
        }

        TimelineClip lockClip = FindClipByName(track, CombatLockClipName);
        if (lockClip == null)
            lockClip = FindFirstClip(track);

        if (lockClip == null)
        {
            lockClip = track.CreateClip<CutsceneDroneCombatLockClip>();
            double start = ResolveTrackClipStart(timelineAsset, ActivationTrackName, RevealStart);
            double duration = ResolveTrackClipDuration(timelineAsset, ActivationTrackName, RevealDuration);
            changed |= ConfigureClipTiming(lockClip, CombatLockClipName, start, duration);
            changed = true;
        }

        if (director.GetGenericBinding(track) != droneCombat)
        {
            director.SetGenericBinding(track, droneCombat);
            changed = true;
        }

        EditorUtility.SetDirty(track);
        return changed;
    }

    private static bool ConfigureSpawnEffectTrack(TimelineAsset timelineAsset, PlayableDirector director, GameObject spawnEffectObject)
    {
        if (spawnEffectObject == null)
            return false;

        ActivationTrack track = FindTrackByName<ActivationTrack>(timelineAsset, SpawnEffectTrackName);
        bool changed = false;

        if (track == null)
        {
            track = timelineAsset.CreateTrack<ActivationTrack>(SpawnEffectTrackName);
            changed = true;
        }

        if (track.name != SpawnEffectTrackName)
        {
            track.name = SpawnEffectTrackName;
            changed = true;
        }

        if (track.muted)
        {
            track.muted = false;
            changed = true;
        }

        if (track.postPlaybackState != ActivationTrack.PostPlaybackState.Revert)
        {
            track.postPlaybackState = ActivationTrack.PostPlaybackState.Revert;
            changed = true;
        }

        TimelineClip spawnEffectClip = FindClipByName(track, SpawnEffectTrackName);
        if (spawnEffectClip == null)
            spawnEffectClip = FindFirstClip(track);

        if (spawnEffectClip == null)
        {
            spawnEffectClip = track.CreateDefaultClip();
            double start = ResolveTrackClipStart(timelineAsset, ActivationTrackName, RevealStart);
            changed |= ConfigureClipTiming(spawnEffectClip, SpawnEffectTrackName, start, SpawnEffectDuration);
            changed = true;
        }

        if (director.GetGenericBinding(track) != spawnEffectObject)
        {
            director.SetGenericBinding(track, spawnEffectObject);
            changed = true;
        }

        EditorUtility.SetDirty(track);
        return changed;
    }

    private static bool ConfigureIdleTrack(TimelineAsset timelineAsset, PlayableDirector director, Animator droneAnimator, AnimationClip idleClip)
    {
        return ConfigureAnimationTrack(timelineAsset, director, droneAnimator, idleClip, IdleTrackName, IdleClipName);
    }

    private static bool ConfigureMovementTrack(TimelineAsset timelineAsset, PlayableDirector director, Animator droneAnimator, AnimationClip movementClip)
    {
        return ConfigureAnimationTrack(timelineAsset, director, droneAnimator, movementClip, MovementTrackName, MovementClipName);
    }

    private static bool ConfigureAnimationTrack(
        TimelineAsset timelineAsset,
        PlayableDirector director,
        Animator droneAnimator,
        AnimationClip animationClip,
        string trackName,
        string clipDisplayName)
    {
        AnimationTrack track = FindTrackByName<AnimationTrack>(timelineAsset, trackName);
        bool changed = false;

        if (track == null)
        {
            track = timelineAsset.CreateTrack<AnimationTrack>(trackName);
            changed = true;
        }

        if (track.name != trackName)
        {
            track.name = trackName;
            changed = true;
        }

        if (track.muted)
        {
            track.muted = false;
            changed = true;
        }

        TimelineClip animationTimelineClip = FindAnimationClip(track, animationClip);
        bool createdAnimationClip = false;
        if (animationTimelineClip == null)
        {
            animationTimelineClip = track.CreateClip<AnimationPlayableAsset>();
            changed |= ConfigureClipTiming(animationTimelineClip, clipDisplayName, RevealStart, RevealDuration);
            createdAnimationClip = true;
            changed = true;
        }

        if (animationTimelineClip.clipIn != 0d)
        {
            animationTimelineClip.clipIn = 0d;
            changed = true;
        }

        if (animationTimelineClip.timeScale != 1d)
        {
            animationTimelineClip.timeScale = 1d;
            changed = true;
        }

        AnimationPlayableAsset playableAsset = animationTimelineClip.asset as AnimationPlayableAsset;
        if (playableAsset != null)
        {
            if (playableAsset.clip != animationClip)
            {
                playableAsset.clip = animationClip;
                changed = true;
            }

            if (playableAsset.loop != AnimationPlayableAsset.LoopMode.Off)
            {
                playableAsset.loop = AnimationPlayableAsset.LoopMode.Off;
                changed = true;
            }

            if (playableAsset.applyFootIK)
            {
                playableAsset.applyFootIK = false;
                changed = true;
            }

            if (createdAnimationClip)
            {
                if (playableAsset.position != Vector3.zero)
                {
                    playableAsset.position = Vector3.zero;
                    changed = true;
                }

                if (playableAsset.eulerAngles != Vector3.zero)
                {
                    playableAsset.eulerAngles = Vector3.zero;
                    changed = true;
                }

                if (playableAsset.removeStartOffset)
                {
                    playableAsset.removeStartOffset = false;
                    changed = true;
                }
            }

            EditorUtility.SetDirty(playableAsset);
        }

        if (director.GetGenericBinding(track) != droneAnimator)
        {
            director.SetGenericBinding(track, droneAnimator);
            changed = true;
        }

        EditorUtility.SetDirty(track);
        return changed;
    }

    private static bool ConfigureClipTiming(TimelineClip clip, string displayName, double start, double duration)
    {
        bool changed = false;

        if (clip.displayName != displayName)
        {
            clip.displayName = displayName;
            changed = true;
        }

        if (System.Math.Abs(clip.start - start) > 0.0001d)
        {
            clip.start = start;
            changed = true;
        }

        if (System.Math.Abs(clip.duration - duration) > 0.0001d)
        {
            clip.duration = duration;
            changed = true;
        }

        return changed;
    }

    private static Animator EnsureAnimator(GameObject droneObject)
    {
        Animator animator = droneObject.GetComponent<Animator>();
        if (animator != null)
            return animator;

        animator = Undo.AddComponent<Animator>(droneObject);
        animator.applyRootMotion = false;
        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(droneObject);
        EditorSceneManager.MarkSceneDirty(droneObject.scene);
        return animator;
    }

    private static GameObject EnsureSpawnEffectObject(GameObject droneObject)
    {
        GameObject spawnEffectObject = FindSceneObject(SpawnEffectObjectName);
        if (spawnEffectObject != null)
            return spawnEffectObject;

        string prefabPath = AssetDatabase.GUIDToAssetPath(SpawnEffectPrefabGuid);
        GameObject spawnEffectPrefab = string.IsNullOrEmpty(prefabPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (spawnEffectPrefab == null)
            return null;

        spawnEffectObject = PrefabUtility.InstantiatePrefab(spawnEffectPrefab) as GameObject;
        if (spawnEffectObject == null)
            return null;

        spawnEffectObject.name = SpawnEffectObjectName;
        if (droneObject != null)
            spawnEffectObject.transform.position = droneObject.transform.position;

        DestroyAfterAnimation destroyAfterAnimation = spawnEffectObject.GetComponent<DestroyAfterAnimation>();
        if (destroyAfterAnimation != null)
            destroyAfterAnimation.enabled = false;

        spawnEffectObject.SetActive(false);
        EditorUtility.SetDirty(spawnEffectObject);
        EditorSceneManager.MarkSceneDirty(spawnEffectObject.scene);
        return spawnEffectObject;
    }

    private static bool ConfigureSpawnEffectObject(GameObject spawnEffectObject, GameObject droneObject)
    {
        if (spawnEffectObject == null)
            return false;

        bool changed = false;

        DestroyAfterAnimation destroyAfterAnimation = spawnEffectObject.GetComponent<DestroyAfterAnimation>();
        if (destroyAfterAnimation != null && destroyAfterAnimation.enabled)
        {
            Undo.RecordObject(destroyAfterAnimation, "Disable cutscene spawn effect destroy");
            destroyAfterAnimation.enabled = false;
            EditorUtility.SetDirty(destroyAfterAnimation);
            changed = true;
        }

        CutsceneSpawnEffectReplayer replayer = spawnEffectObject.GetComponent<CutsceneSpawnEffectReplayer>();
        if (replayer == null)
        {
            replayer = Undo.AddComponent<CutsceneSpawnEffectReplayer>(spawnEffectObject);
            changed = true;
        }

        Transform target = droneObject != null ? droneObject.transform : null;
        Vector3 targetOffset = Vector3.zero;
        if (replayer != null && replayer.Configure(target, true, false, targetOffset))
        {
            EditorUtility.SetDirty(replayer);
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(spawnEffectObject);
            EditorSceneManager.MarkSceneDirty(spawnEffectObject.scene);
        }

        return changed;
    }

    private static AnimationClip EnsureMovementClip(GameObject droneObject)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(MovementClipPath);
        bool created = false;

        if (clip == null)
        {
            clip = new AnimationClip
            {
                name = MovementClipName,
                frameRate = 60f
            };
            AssetDatabase.CreateAsset(clip, MovementClipPath);
            created = true;
        }

        if (created || AnimationUtility.GetCurveBindings(clip).Length == 0)
            SeedMovementCurves(clip, droneObject);

        return clip;
    }

    private static AnimationClip EnsureIdleClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
        bool created = false;

        if (clip == null)
        {
            clip = new AnimationClip
            {
                name = IdleClipName,
                frameRate = 60f
            };
            AssetDatabase.CreateAsset(clip, IdleClipPath);
            created = true;
        }

        if (created)
            SeedIdleCurves(clip);

        return clip;
    }

    private static void SeedIdleCurves(AnimationClip clip)
    {
        EditorUtility.SetDirty(clip);
    }

    private static void SeedMovementCurves(AnimationClip clip, GameObject droneObject)
    {
        RectTransform rectTransform = droneObject.transform as RectTransform;
        float duration = (float)RevealDuration;

        if (rectTransform != null)
        {
            Vector2 start = rectTransform.anchoredPosition;
            SetCurve(clip, typeof(RectTransform), "m_AnchoredPosition.x", start.x, start.x, duration);
            SetCurve(clip, typeof(RectTransform), "m_AnchoredPosition.y", start.y, start.y, duration);
            SetCurve(clip, typeof(Transform), "m_LocalPosition.z", rectTransform.localPosition.z, rectTransform.localPosition.z, duration);
        }
        else
        {
            Vector3 start = droneObject.transform.localPosition;
            SetCurve(clip, typeof(Transform), "m_LocalPosition.x", start.x, start.x, duration);
            SetCurve(clip, typeof(Transform), "m_LocalPosition.y", start.y, start.y, duration);
            SetCurve(clip, typeof(Transform), "m_LocalPosition.z", start.z, start.z, duration);
        }

        clip.EnsureQuaternionContinuity();
        EditorUtility.SetDirty(clip);
    }

    private static void SetCurve(AnimationClip clip, System.Type bindingType, string propertyName, float startValue, float endValue, float duration)
    {
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, startValue),
            new Keyframe(duration, endValue));
        SetCurve(clip, bindingType, propertyName, curve);
    }

    private static void SetCurve(AnimationClip clip, System.Type bindingType, string propertyName, AnimationCurve curve)
    {
        EditorCurveBinding binding = EditorCurveBinding.FloatCurve(string.Empty, bindingType, propertyName);
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    private static GameObject ResolveTargetDrone()
    {
        GameObject roomObject = FindSceneObject(EnemyRoomName);
        if (roomObject == null)
            return null;

        Transform target = FindChildRecursive(roomObject.transform, TargetDroneName);
        return target != null ? target.gameObject : null;
    }

    private static Transform FindChildRecursive(Transform parent, string normalizedTargetName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (NormalizeName(child.name) == normalizedTargetName)
                return child;

            Transform nested = FindChildRecursive(child, normalizedTargetName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static string NormalizeName(string name)
    {
        return string.IsNullOrEmpty(name) ? string.Empty : name.Replace(" ", string.Empty);
    }

    private static TimelineClip FindClipByName(TrackAsset track, string clipName)
    {
        foreach (TimelineClip clip in track.GetClips())
        {
            if (clip.displayName == clipName)
                return clip;
        }

        return null;
    }

    private static double ResolveTrackClipStart(TimelineAsset timelineAsset, string trackName, double fallbackStart)
    {
        TrackAsset track = FindTrackByName<TrackAsset>(timelineAsset, trackName);
        if (track == null)
            return fallbackStart;

        TimelineClip firstClip = FindFirstClip(track);
        return firstClip != null ? firstClip.start : fallbackStart;
    }

    private static double ResolveTrackClipDuration(TimelineAsset timelineAsset, string trackName, double fallbackDuration)
    {
        TrackAsset track = FindTrackByName<TrackAsset>(timelineAsset, trackName);
        if (track == null)
            return fallbackDuration;

        TimelineClip firstClip = FindFirstClip(track);
        return firstClip != null ? firstClip.duration : fallbackDuration;
    }

    private static TimelineClip FindFirstClip(TrackAsset track)
    {
        foreach (TimelineClip clip in track.GetClips())
        {
            return clip;
        }

        return null;
    }

    private static TimelineClip FindAnimationClip(AnimationTrack track, AnimationClip animationClip)
    {
        foreach (TimelineClip clip in track.GetClips())
        {
            AnimationPlayableAsset playableAsset = clip.asset as AnimationPlayableAsset;
            if (playableAsset != null && playableAsset.clip == animationClip)
                return clip;
        }

        return null;
    }

    private static T FindTrackByName<T>(TimelineAsset timelineAsset, string trackName) where T : TrackAsset
    {
        foreach (TrackAsset track in timelineAsset.GetOutputTracks())
        {
            if (track is T typedTrack && track.name == trackName)
                return typedTrack;
        }

        return null;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        GameObject[] sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < sceneObjects.Length; i++)
        {
            GameObject sceneObject = sceneObjects[i];
            if (sceneObject == null || sceneObject.name != objectName)
                continue;

            if (EditorUtility.IsPersistent(sceneObject) || !sceneObject.scene.IsValid())
                continue;

            return sceneObject;
        }

        return null;
    }

    private static bool IsPlayModeChangingOrActive()
    {
        return EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying;
    }

    private static bool IsTimelinePreviewing()
    {
        PlayableDirector inspectedDirector = TimelineEditor.inspectedDirector;
        return inspectedDirector != null && inspectedDirector.state == PlayState.Playing;
    }

    private static void LogMissing(bool logWhenMissing, string message)
    {
        if (logWhenMissing)
            Debug.LogWarning(message);
    }
}
