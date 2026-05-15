using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[InitializeOnLoad]
public static class CutsceneParryPromptAutoSetup
{
    private const string DirectorName = "Cutscene_Director";
    private const string PlayerObjectName = "Player";
    private const string DroneObjectName = "LatencyDroneStrong2";
    private const string TrackName = "Parry Prompt";
    private const string ClipName = "Drone Projectile Parry";
    private const double PromptStart = 63.14d;
    private const double PromptDuration = 4.0d;
    private const string GalmuriFontGuid = "688a543337d911744a79b86c1f624e3c";
    private const string GalmuriFontSourceGuid = "1a923220d1e5c19468671e9533851a3d";
    private static bool setupQueued;

    static CutsceneParryPromptAutoSetup()
    {
        QueueSetup();
        EditorSceneManager.sceneOpened += (_, _) => QueueSetup();
    }

    [MenuItem("Tools/Cutscene/Setup Parry Prompt Clip")]
    private static void SetupParryPromptClipIfPresent()
    {
        setupQueued = false;

        if (IsPlayModeChangingOrActive() || IsTimelinePreviewing())
            return;

        GameObject directorObject = FindSceneObject(DirectorName);
        if (directorObject == null)
            return;

        PlayableDirector director = directorObject.GetComponent<PlayableDirector>();
        if (director == null)
            return;

        CutsceneParryPromptPlayer promptPlayer = directorObject.GetComponent<CutsceneParryPromptPlayer>();
        bool addedComponent = false;
        if (promptPlayer == null)
        {
            promptPlayer = Undo.AddComponent<CutsceneParryPromptPlayer>(directorObject);
            addedComponent = true;
        }

        SerializedObject serializedObject = new SerializedObject(promptPlayer);
        SetObjectReferenceIfEmpty(serializedObject, "director", director);
        GameObject playerObject = FindSceneObject(PlayerObjectName);
        SetObjectReferenceIfEmpty(serializedObject, "playerObject", playerObject);
        SetObjectReferenceIfEmpty(serializedObject, "player", playerObject != null ? playerObject.GetComponent<Player>() : null);
        SetObjectReferenceIfEmpty(serializedObject, "drone", FindSceneDrone(DroneObjectName));
        SetObjectReferenceIfEmpty(serializedObject, "fontAsset", LoadFontByGuid(GalmuriFontGuid));
        SetObjectReferenceIfEmpty(serializedObject, "dynamicFontSource", LoadUnityFontByGuid(GalmuriFontSourceGuid));
        SetString(serializedObject, "playerObjectName", PlayerObjectName);
        SetString(serializedObject, "droneObjectName", DroneObjectName);
        bool componentChanged = serializedObject.ApplyModifiedProperties();

        bool timelineChanged = SetupTimelineTrack(director, promptPlayer);

        if (addedComponent || componentChanged)
        {
            EditorUtility.SetDirty(promptPlayer);
            EditorUtility.SetDirty(directorObject);
            EditorSceneManager.MarkSceneDirty(directorObject.scene);
        }

        if (addedComponent || componentChanged || timelineChanged)
            Debug.Log("Cutscene parry prompt clip is set up at 63.14 seconds on Cutscene_Director.", directorObject);
    }

    private static void QueueSetup()
    {
        if (IsPlayModeChangingOrActive() || setupQueued)
            return;

        setupQueued = true;
        EditorApplication.delayCall += SetupParryPromptClipIfPresent;
    }

    private static bool SetupTimelineTrack(PlayableDirector director, CutsceneParryPromptPlayer promptPlayer)
    {
        if (director == null || promptPlayer == null)
            return false;

        TimelineAsset timelineAsset = director.playableAsset as TimelineAsset;
        if (timelineAsset == null)
            return false;

        bool changed = false;
        CutsceneParryPromptTrack promptTrack = FindTrackByName<CutsceneParryPromptTrack>(timelineAsset, TrackName);
        if (promptTrack == null)
        {
            Undo.RegisterCompleteObjectUndo(timelineAsset, "Create cutscene parry prompt track");
            promptTrack = timelineAsset.CreateTrack<CutsceneParryPromptTrack>(TrackName);
            changed = true;
        }

        if (director.GetGenericBinding(promptTrack) != promptPlayer)
        {
            director.SetGenericBinding(promptTrack, promptPlayer);
            changed = true;
        }

        TimelineClip promptTimelineClip = FindPromptClip(promptTrack);
        if (promptTimelineClip == null)
        {
            promptTimelineClip = promptTrack.CreateClip<CutsceneParryPromptClip>();
            changed = true;
        }

        changed |= ConfigurePromptClip(promptTimelineClip);

        if (!changed)
            return false;

        EditorUtility.SetDirty(promptTrack);
        EditorUtility.SetDirty(timelineAsset);
        EditorUtility.SetDirty(director);
        EditorUtility.SetDirty(director.gameObject);
        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        AssetDatabase.SaveAssets();
        TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved | RefreshReason.WindowNeedsRedraw);
        return true;
    }

    private static TimelineClip FindPromptClip(CutsceneParryPromptTrack promptTrack)
    {
        if (promptTrack == null)
            return null;

        foreach (TimelineClip clip in promptTrack.GetClips())
        {
            if (clip.asset is CutsceneParryPromptClip)
                return clip;
        }

        return null;
    }

    private static bool ConfigurePromptClip(TimelineClip timelineClip)
    {
        if (timelineClip == null)
            return false;

        bool changed = false;
        if (Mathf.Abs((float)(timelineClip.start - PromptStart)) > 0.0001f)
        {
            timelineClip.start = PromptStart;
            changed = true;
        }

        if (Mathf.Abs((float)(timelineClip.duration - PromptDuration)) > 0.0001f)
        {
            timelineClip.duration = PromptDuration;
            changed = true;
        }

        if (timelineClip.displayName != ClipName)
        {
            timelineClip.displayName = ClipName;
            changed = true;
        }

        CutsceneParryPromptClip promptClip = timelineClip.asset as CutsceneParryPromptClip;
        if (promptClip == null || promptClip.template == null)
            return changed;

        CutsceneParryPromptBehaviour template = promptClip.template;
        if (template.promptText != "X\uD0A4\uB97C \uB20C\uB7EC \uD328\uB9C1\uD558\uC138\uC694.")
        {
            template.promptText = "X\uD0A4\uB97C \uB20C\uB7EC \uD328\uB9C1\uD558\uC138\uC694.";
            changed = true;
        }

        if (template.parryWindowText != "X\uD0A4\uB97C \uB5BC\uBA70 \uD0C0\uC774\uBC0D\uC5D0 \uB9DE\uAC8C \uD29C\uACA8\uB0B4\uC138\uC694!")
        {
            template.parryWindowText = "X\uD0A4\uB97C \uB5BC\uBA70 \uD0C0\uC774\uBC0D\uC5D0 \uB9DE\uAC8C \uD29C\uACA8\uB0B4\uC138\uC694!";
            changed = true;
        }

        if (template.tooEarlyText != "\uB108\uBB34 \uBE68\uB77C\uC694!")
        {
            template.tooEarlyText = "\uB108\uBB34 \uBE68\uB77C\uC694!";
            changed = true;
        }

        if (template.tooLateText != "\uB108\uBB34 \uB290\uB824\uC694!")
        {
            template.tooLateText = "\uB108\uBB34 \uB290\uB824\uC694!";
            changed = true;
        }

        if (!template.fireProjectileOnStart)
        {
            template.fireProjectileOnStart = true;
            changed = true;
        }

        if (!template.pauseTimelineUntilSuccess)
        {
            template.pauseTimelineUntilSuccess = true;
            changed = true;
        }

        if (!template.showReleasePrompt)
        {
            template.showReleasePrompt = true;
            changed = true;
        }

        if (!template.clearParryCooldown)
        {
            template.clearParryCooldown = true;
            changed = true;
        }

        if (!Mathf.Approximately(template.slowTimeScale, 0.18f))
        {
            template.slowTimeScale = 0.18f;
            changed = true;
        }

        if (!Mathf.Approximately(template.parryWindowDuration, 7f))
        {
            template.parryWindowDuration = 7f;
            changed = true;
        }

        if (!Mathf.Approximately(template.retryDelay, 0.45f))
        {
            template.retryDelay = 0.45f;
            changed = true;
        }

        if (!Mathf.Approximately(template.timingFeedbackDuration, 0.8f))
        {
            template.timingFeedbackDuration = 0.8f;
            changed = true;
        }

        if (!Mathf.Approximately(template.fallbackParryDistance, 1.5f))
        {
            template.fallbackParryDistance = 1.5f;
            changed = true;
        }

        if (!Mathf.Approximately(template.earlyPressDistancePadding, 0.2f))
        {
            template.earlyPressDistancePadding = 0.2f;
            changed = true;
        }

        if (changed)
            EditorUtility.SetDirty(promptClip);

        return changed;
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

    private static T FindTrackByName<T>(TimelineAsset timelineAsset, string trackName) where T : TrackAsset
    {
        if (timelineAsset == null)
            return null;

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
            if (sceneObject == null || EditorUtility.IsPersistent(sceneObject) || !sceneObject.scene.IsValid())
                continue;

            if (NormalizeName(sceneObject.name) == NormalizeName(objectName))
                return sceneObject;
        }

        return null;
    }

    private static LatencyDroneWeak FindSceneDrone(string objectName)
    {
        LatencyDroneWeak[] drones = Resources.FindObjectsOfTypeAll<LatencyDroneWeak>();
        string normalizedTarget = NormalizeName(objectName);

        for (int i = 0; i < drones.Length; i++)
        {
            LatencyDroneWeak drone = drones[i];
            if (drone == null || EditorUtility.IsPersistent(drone) || !drone.gameObject.scene.IsValid())
                continue;

            if (NormalizeName(drone.name) == normalizedTarget)
                return drone;
        }

        return null;
    }

    private static void SetObjectReferenceIfEmpty(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null && property.objectReferenceValue == null)
            property.objectReferenceValue = value;
    }

    private static void SetString(SerializedObject serializedObject, string propertyName, string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.stringValue = value;
    }

    private static TMP_FontAsset LoadFontByGuid(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
    }

    private static Font LoadUnityFontByGuid(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Font>(path);
    }

    private static string NormalizeName(string name)
    {
        return string.IsNullOrEmpty(name) ? string.Empty : name.Replace(" ", string.Empty);
    }
}
