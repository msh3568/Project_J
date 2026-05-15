using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public static class CutsceneGrapplePromptAutoSetup
{
    private const string DirectorName = "Cutscene_Director";
    private const string PlayerObjectName = "Player";
    private const string GrappleTargetObjectName = "LatencyDroneStrong2";
    private const string TrackName = "Grapple Prompt";
    private const string ClipName = "Drone Grapple";
    private const double PromptStart = 95.0d;
    private const double PromptDuration = 4.0d;
    private const string GalmuriFontGuid = "688a543337d911744a79b86c1f624e3c";
    private const string GalmuriFontSourceGuid = "1a923220d1e5c19468671e9533851a3d";
    [MenuItem("Tools/Cutscene/Setup Grapple Prompt Clip")]
    private static void SetupGrapplePromptClipIfPresent()
    {
        if (IsPlayModeChangingOrActive() || IsTimelinePreviewing())
            return;

        GameObject directorObject = FindSceneObject(DirectorName);
        if (directorObject == null)
            return;

        PlayableDirector director = directorObject.GetComponent<PlayableDirector>();
        if (director == null)
            return;

        CutsceneGrapplePromptPlayer promptPlayer = directorObject.GetComponent<CutsceneGrapplePromptPlayer>();
        bool addedComponent = false;
        if (promptPlayer == null)
        {
            promptPlayer = Undo.AddComponent<CutsceneGrapplePromptPlayer>(directorObject);
            addedComponent = true;
        }

        SerializedObject serializedObject = new SerializedObject(promptPlayer);
        SetObjectReferenceIfEmpty(serializedObject, "director", director);
        GameObject playerObject = FindSceneObject(PlayerObjectName);
        SetObjectReferenceIfEmpty(serializedObject, "playerObject", playerObject);
        SetObjectReferenceIfEmpty(serializedObject, "player", playerObject != null ? playerObject.GetComponent<Player>() : null);
        SetObjectReference(serializedObject, "grappleTarget", FindCutsceneGrappleTarget(director));
        SetObjectReferenceIfEmpty(serializedObject, "fontAsset", LoadFontByGuid(GalmuriFontGuid));
        SetObjectReferenceIfEmpty(serializedObject, "dynamicFontSource", LoadUnityFontByGuid(GalmuriFontSourceGuid));
        SetString(serializedObject, "playerObjectName", PlayerObjectName);
        SetString(serializedObject, "grappleTargetObjectName", GrappleTargetObjectName);
        bool componentChanged = serializedObject.ApplyModifiedProperties();

        bool timelineChanged = SetupTimelineTrack(director, promptPlayer);

        if (addedComponent || componentChanged)
        {
            EditorUtility.SetDirty(promptPlayer);
            EditorUtility.SetDirty(directorObject);
            EditorSceneManager.MarkSceneDirty(directorObject.scene);
        }

        if (addedComponent || componentChanged || timelineChanged)
            Debug.Log("Cutscene grapple prompt clip is set up near the second drone reveal.", directorObject);
    }

    private static bool SetupTimelineTrack(PlayableDirector director, CutsceneGrapplePromptPlayer promptPlayer)
    {
        if (director == null || promptPlayer == null)
            return false;

        TimelineAsset timelineAsset = director.playableAsset as TimelineAsset;
        if (timelineAsset == null)
            return false;

        bool changed = false;
        CutsceneGrapplePromptTrack promptTrack = FindTrackByName<CutsceneGrapplePromptTrack>(timelineAsset, TrackName);
        if (promptTrack == null)
        {
            Undo.RegisterCompleteObjectUndo(timelineAsset, "Create cutscene grapple prompt track");
            promptTrack = timelineAsset.CreateTrack<CutsceneGrapplePromptTrack>(TrackName);
            changed = true;
        }

        if (director.GetGenericBinding(promptTrack) != promptPlayer)
        {
            director.SetGenericBinding(promptTrack, promptPlayer);
            changed = true;
        }

        TimelineClip promptTimelineClip = FindPromptClip(promptTrack);
        bool createdPromptClip = false;
        if (promptTimelineClip == null)
        {
            promptTimelineClip = promptTrack.CreateClip<CutsceneGrapplePromptClip>();
            createdPromptClip = true;
            changed = true;
        }

        changed |= ConfigurePromptClip(promptTimelineClip, createdPromptClip);

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

    private static TimelineClip FindPromptClip(CutsceneGrapplePromptTrack promptTrack)
    {
        if (promptTrack == null)
            return null;

        foreach (TimelineClip clip in promptTrack.GetClips())
        {
            if (clip.asset is CutsceneGrapplePromptClip)
                return clip;
        }

        return null;
    }

    private static bool ConfigurePromptClip(TimelineClip timelineClip, bool configureTiming)
    {
        if (timelineClip == null)
            return false;

        bool changed = false;
        if (configureTiming && Mathf.Abs((float)(timelineClip.start - PromptStart)) > 0.0001f)
        {
            timelineClip.start = PromptStart;
            changed = true;
        }

        if (configureTiming && Mathf.Abs((float)(timelineClip.duration - PromptDuration)) > 0.0001f)
        {
            timelineClip.duration = PromptDuration;
            changed = true;
        }

        if (timelineClip.displayName != ClipName)
        {
            timelineClip.displayName = ClipName;
            changed = true;
        }

        CutsceneGrapplePromptClip promptClip = timelineClip.asset as CutsceneGrapplePromptClip;
        if (promptClip == null || promptClip.template == null)
            return changed;

        CutsceneGrapplePromptBehaviour template = promptClip.template;
        if (template.promptText != "ALT\uB97C \uBE60\uB974\uAC8C \uB450 \uBC88 \uB20C\uB7EC \uADF8\uB798\uD50C\uB9C1\uD558\uC138\uC694.")
        {
            template.promptText = "ALT\uB97C \uBE60\uB974\uAC8C \uB450 \uBC88 \uB20C\uB7EC \uADF8\uB798\uD50C\uB9C1\uD558\uC138\uC694.";
            changed = true;
        }

        if (!template.pauseTimelineUntilSuccess)
        {
            template.pauseTimelineUntilSuccess = true;
            changed = true;
        }

        if (!template.waitForGrappleEnd)
        {
            template.waitForGrappleEnd = true;
            changed = true;
        }

        if (!Mathf.Approximately(template.slowTimeScale, 0.18f))
        {
            template.slowTimeScale = 0.18f;
            changed = true;
        }

        if (!Mathf.Approximately(template.doubleTapMaxInterval, 0.6f))
        {
            template.doubleTapMaxInterval = 0.6f;
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

    private static GrappleTargetBase FindSceneGrappleTarget(string objectName)
    {
        GameObject targetObject = FindSceneObject(objectName);
        if (targetObject != null)
        {
            GrappleTargetBase directTarget = targetObject.GetComponentInChildren<GrappleTargetBase>(true);
            if (directTarget != null)
                return directTarget;
        }

        GrappleTargetBase[] targets = Resources.FindObjectsOfTypeAll<GrappleTargetBase>();
        string normalizedTarget = NormalizeName(objectName);
        for (int i = 0; i < targets.Length; i++)
        {
            GrappleTargetBase target = targets[i];
            if (target == null || EditorUtility.IsPersistent(target) || !target.gameObject.scene.IsValid())
                continue;

            if (NormalizeName(target.name) == normalizedTarget)
                return target;
        }

        return null;
    }

    private static GrappleTargetBase FindCutsceneGrappleTarget(PlayableDirector director)
    {
        if (director != null && director.playableAsset is TimelineAsset timelineAsset)
        {
            foreach (TrackAsset track in timelineAsset.GetOutputTracks())
            {
                if (track == null || NormalizeName(track.name) != "DroneVisible")
                    continue;

                Object binding = director.GetGenericBinding(track);
                if (binding is GameObject boundObject)
                    return boundObject.GetComponentInChildren<GrappleTargetBase>(true);

                if (binding is Component boundComponent)
                {
                    GrappleTargetBase childTarget = boundComponent.GetComponentInChildren<GrappleTargetBase>(true);
                    if (childTarget != null)
                        return childTarget;

                    return boundComponent.GetComponentInParent<GrappleTargetBase>();
                }
            }
        }

        return FindSceneGrappleTarget(GrappleTargetObjectName);
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

    private static void SetObjectReferenceIfEmpty(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null && property.objectReferenceValue == null)
            property.objectReferenceValue = value;
    }

    private static void SetObjectReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null && value != null && property.objectReferenceValue != value)
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
